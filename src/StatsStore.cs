using System;
using System.IO;
using System.Text;

namespace BongoKeyStats
{
    /// <summary>Atomic JSON persistence with a .bak fallback.</summary>
    public sealed class StatsStore
    {
        private readonly object _ioLock = new object();
        public string Dir { get; private set; }
        public string DataPath { get { return Path.Combine(Dir, "keystats.json"); } }
        public string BackupPath { get { return DataPath + ".bak"; } }
        public string ReportPath { get { return Path.Combine(Dir, "report.html"); } }

        public StatsStore(string dir) { Dir = dir; Directory.CreateDirectory(dir); }

        /// <summary>Returns true when the directory accepts writes.</summary>
        public static bool IsWritable(string dir)
        {
            try
            {
                Directory.CreateDirectory(dir);
                string p = Path.Combine(dir, ".write_test");
                File.WriteAllText(p, "x");
                File.Delete(p);
                return true;
            }
            catch { return false; }
        }

        public bool Load(KeyCounter counter, out string note)
        {
            lock (_ioLock)
            {
                note = null;
                foreach (string path in new[] { DataPath, BackupPath })
                {
                    if (!File.Exists(path)) continue;
                    try
                    {
                        counter.LoadJson(File.ReadAllText(path));
                        if (path == BackupPath) note = "main file unreadable, restored from .bak";
                        return true;
                    }
                    catch (Exception e) { note = "failed to read " + Path.GetFileName(path) + ": " + e.Message; }
                }
                return false;
            }
        }

        public void Save(KeyCounter counter)
        {
            string json = counter.ToJson();
            lock (_ioLock)
            {
                string tmp = DataPath + ".tmp";
                File.WriteAllText(tmp, json);
                // verify the temp file parses before replacing the good one
                new KeyCounter().LoadJson(File.ReadAllText(tmp));
                if (File.Exists(DataPath)) File.Copy(DataPath, BackupPath, true);
                File.Copy(tmp, DataPath, true);
                File.Delete(tmp);
            }
        }

        public void WriteReport(string html)
        {
            lock (_ioLock)
            {
                string tmp = ReportPath + ".tmp";
                File.WriteAllText(tmp, html);
                File.Copy(tmp, ReportPath, true);
                File.Delete(tmp);
            }
        }
    }
}
