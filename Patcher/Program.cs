using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BongoKeyStatsPatcher
{
    /// <summary>
    /// Adds the BongoKeyStats module to a Bongo Cat install. Works on the official Assembly-CSharp.dll and on
    /// third-party modified ones alike: the only change is one guarded call at the start of GlobalKeyHook.Awake.
    /// </summary>
    internal static class Program
    {
        private const string TargetType = "BongoCat.OSSpecific.GlobalKeyHook";
        private const string TargetMethod = "Awake";
        private const string ModuleDll = "BongoKeyStats.dll";
        private const string GameDll = "Assembly-CSharp.dll";
        private const string BackupName = "Assembly-CSharp.dll.keystats-original";
        private const string LegacyBackupName = "Assembly-CSharp.dll.mod-original";

        private static int Main(string[] args)
        {
            try
            {
                string pathArg = args.FirstOrDefault(a => !a.StartsWith("--"));
                bool restore = args.Contains("--restore");
                bool check = args.Contains("--check");
                string managed = ResolveManaged(pathArg);
                if (managed == null)
                {
                    Console.WriteLine("Could not find the Bongo Cat install.");
                    Console.WriteLine("Pass the game folder (or BongoCat_Data\\Managed) as an argument, or drag the folder onto patch.bat.");
                    return 2;
                }
                Console.WriteLine("Game: " + managed);
                return restore ? Restore(managed) : check ? Check(managed) : Patch(managed);
            }
            catch (Exception e)
            {
                Console.WriteLine("ERROR: " + e.Message);
                return 1;
            }
        }

        // ---- locating the game ---------------------------------------------------------

        private static string ResolveManaged(string arg)
        {
            if (!string.IsNullOrEmpty(arg))
            {
                arg = arg.Trim('"');
                if (File.Exists(arg)) arg = Path.GetDirectoryName(Path.GetFullPath(arg));
                foreach (string c in new[] { arg, Path.Combine(arg, "Managed"), Path.Combine(arg, "BongoCat_Data", "Managed") })
                    if (File.Exists(Path.Combine(c, GameDll))) return Path.GetFullPath(c);
                return null;
            }
            foreach (string lib in SteamLibraries())
            {
                string m = Path.Combine(lib, "steamapps", "common", "BongoCat", "BongoCat_Data", "Managed");
                if (File.Exists(Path.Combine(m, GameDll))) return m;
            }
            return null;
        }

        private static IEnumerable<string> SteamLibraries()
        {
            var libs = new List<string>();
            string steam = null;
            try
            {
                if (OperatingSystem.IsWindows())
                    steam = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string;
            }
            catch { }
            if (!string.IsNullOrEmpty(steam))
            {
                steam = steam.Replace('/', '\\');
                libs.Add(steam);
                string vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
                if (File.Exists(vdf))
                    foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
                        libs.Add(m.Groups[1].Value.Replace("\\\\", "\\"));
            }
            foreach (string d in new[] { @"C:\Program Files (x86)\Steam", @"C:\Program Files\Steam" }) libs.Add(d);
            return libs.Distinct(StringComparer.OrdinalIgnoreCase);
        }

        // ---- inspection ----------------------------------------------------------------

        private static bool IsPatched(MethodDefinition m)
        {
            return m.HasBody && m.Body.Instructions.Any(i => i.OpCode == OpCodes.Call && i.Operand is MethodReference r
                && r.Name == "Init" && r.DeclaringType.FullName == "BongoKeyStats.Bootstrap");
        }

        private static MethodDefinition FindTarget(ModuleDefinition mod)
        {
            var t = mod.GetType(TargetType);
            if (t == null) throw new InvalidOperationException("type " + TargetType + " not found in " + GameDll + " (game changed? see docs/MAINTENANCE.md)");
            var m = t.Methods.FirstOrDefault(x => x.Name == TargetMethod && !x.HasParameters && x.HasBody);
            if (m == null) throw new InvalidOperationException("method " + TargetType + "." + TargetMethod + " not found (game changed? see docs/MAINTENANCE.md)");
            return m;
        }

        /// <summary>Third-party mods in the wild inject obfuscated types whose names consist only of whitespace characters.</summary>
        private static int CountObfuscatedTypes(ModuleDefinition mod)
        {
            return mod.Types.Count(t => t.Name.Length > 0 && t.Name.All(c => char.IsWhiteSpace(c) || char.IsControl(c)));
        }

        private static ModuleDefinition Read(string path, string managed, bool readWrite)
        {
            var resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory(managed);
            return ModuleDefinition.ReadModule(path, new ReaderParameters { AssemblyResolver = resolver, InMemory = true, ReadWrite = readWrite });
        }

        private static string DescribeFlavour(ModuleDefinition mod)
        {
            int n = CountObfuscatedTypes(mod);
            return n > 0 ? "modified game DLL (" + n + " obfuscated third-party types detected; they are left untouched)" : "unmodified game DLL";
        }

        private static int Check(string managed)
        {
            using (var mod = Read(Path.Combine(managed, GameDll), managed, false))
            {
                Console.WriteLine(DescribeFlavour(mod));
                Console.WriteLine(IsPatched(FindTarget(mod)) ? "status: patched" : "status: not patched");
                return 0;
            }
        }

        // ---- patch ---------------------------------------------------------------------

        private static int Patch(string managed)
        {
            string dll = Path.Combine(managed, GameDll);
            string backup = Path.Combine(managed, BackupName);
            string srcModule = Path.Combine(AppContext.BaseDirectory, ModuleDll);
            if (!File.Exists(srcModule)) throw new FileNotFoundException(ModuleDll + " must sit next to the patcher", srcModule);

            byte[] patched;
            using (var mod = Read(dll, managed, true))
            using (var stats = Read(srcModule, managed, false))
            {
                Console.WriteLine("Detected: " + DescribeFlavour(mod));
                var target = FindTarget(mod);
                if (IsPatched(target))
                {
                    Console.WriteLine(GameDll + " is already patched; refreshing " + ModuleDll + " only.");
                    File.Copy(srcModule, Path.Combine(managed, ModuleDll), true);
                    return 0;
                }
                var init = stats.GetType("BongoKeyStats.Bootstrap").Methods.First(m => m.Name == "Init" && m.IsStatic && !m.HasParameters);
                var initRef = mod.ImportReference(init);
                VerifyModuleReferences(stats);

                // try { Bootstrap.Init(); } catch (Exception) { }  -- the game must never fail because of this module
                var il = target.Body.GetILProcessor();
                var first = target.Body.Instructions[0];
                var call = il.Create(OpCodes.Call, initRef);
                var leaveOk = il.Create(OpCodes.Leave, first);
                var pop = il.Create(OpCodes.Pop);
                var leaveCatch = il.Create(OpCodes.Leave, first);
                il.InsertBefore(first, call);
                il.InsertBefore(first, leaveOk);
                il.InsertBefore(first, pop);
                il.InsertBefore(first, leaveCatch);
                // mscorlib reference is built by hand: ImportReference(typeof(Exception)) would point at .NET's CoreLib.
                var mscorlib = mod.AssemblyReferences.FirstOrDefault(r => r.Name == "mscorlib");
                if (mscorlib == null) throw new InvalidOperationException("mscorlib reference not found in " + GameDll);
                target.Body.ExceptionHandlers.Add(new ExceptionHandler(ExceptionHandlerType.Catch)
                {
                    TryStart = call, TryEnd = pop, HandlerStart = pop, HandlerEnd = first,
                    CatchType = new TypeReference("System", "Exception", mod, mscorlib)
                });
                if (target.Body.MaxStackSize < 1) target.Body.MaxStackSize = 1;

                // the DLL is unpatched here, so it is the newest known-good copy (also covers game updates)
                File.Copy(dll, backup, true);
                Console.WriteLine("Backup: " + backup);
                using (var ms = new MemoryStream())
                {
                    mod.Write(ms);
                    patched = ms.ToArray();
                }
            }

            // verify the result before touching the game directory
            string tmp = dll + ".patching";
            File.WriteAllBytes(tmp, patched);
            try
            {
                using (var verify = Read(tmp, managed, false))
                    if (!IsPatched(FindTarget(verify))) throw new InvalidOperationException("verification of the patched DLL failed; nothing was changed");
                File.Copy(srcModule, Path.Combine(managed, ModuleDll), true);
                File.Copy(tmp, dll, true);
            }
            finally { if (File.Exists(tmp)) File.Delete(tmp); }
            Console.WriteLine("Patched " + TargetType + "." + TargetMethod + " and installed " + ModuleDll + ".");
            Console.WriteLine("Data will be stored in: " + Path.GetFullPath(Path.Combine(managed, "..", "..", "KeyStats")));
            return 0;
        }

        /// <summary>Fails if BongoKeyStats.dll uses a framework member the game's Mono runtime does not provide.</summary>
        private static void VerifyModuleReferences(ModuleDefinition stats)
        {
            var bad = new List<string>();
            foreach (var mr in stats.GetMemberReferences())
            {
                var scopeName = mr.DeclaringType.Scope?.Name ?? "";
                if (scopeName.StartsWith("BongoKeyStats") || mr.DeclaringType.Name.EndsWith("Attribute")) continue;
                try
                {
                    var rt = mr.DeclaringType.Resolve();
                    if (rt == null) { bad.Add(mr.FullName + " (type unresolved)"); continue; }
                    if (mr is MethodReference m)
                    {
                        if (!rt.Methods.Any(x => x.Name == m.Name && x.Parameters.Count == m.Parameters.Count)) bad.Add(mr.FullName);
                    }
                    else if (mr is FieldReference f)
                    {
                        if (!rt.Fields.Any(x => x.Name == f.Name)) bad.Add(mr.FullName);
                    }
                }
                catch (Exception e) { bad.Add(mr.FullName + " (" + e.Message + ")"); }
            }
            if (bad.Count > 0)
                throw new InvalidOperationException(ModuleDll + " uses members missing from the game's runtime:" + Environment.NewLine + "  " + string.Join(Environment.NewLine + "  ", bad));
        }

        // ---- restore -------------------------------------------------------------------

        private static int Restore(string managed)
        {
            string dll = Path.Combine(managed, GameDll);
            string backup = new[] { BackupName, LegacyBackupName }.Select(n => Path.Combine(managed, n)).FirstOrDefault(File.Exists);
            if (backup == null)
            {
                Console.WriteLine("No backup found; nothing to restore. (Steam > Properties > Installed Files > Verify integrity also works.)");
                return 1;
            }
            File.Copy(backup, dll, true);
            string m = Path.Combine(managed, ModuleDll);
            if (File.Exists(m)) File.Delete(m);
            Console.WriteLine("Restored " + GameDll + " from " + Path.GetFileName(backup) + " and removed " + ModuleDll + ". Data in KeyStats\\ is untouched.");
            return 0;
        }
    }
}
