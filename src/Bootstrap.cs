using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;

namespace BongoKeyStats
{
    /// <summary>Entry point. The patcher injects a call to <see cref="Init"/> at the start of GlobalKeyHook.Awake.</summary>
    public static class Bootstrap
    {
        public static Action<string> Logger = s => { };

        private static bool _started;
        private static bool _failed;
        private static KeyCounter _counter;
        private static StatsStore _store;
        private static KeyPoller _poller;
        private static readonly object _saveLock = new object();

        internal static KeyCounter Counter { get { return _counter; } }
        internal static StatsStore Store { get { return _store; } }

        public static void Log(string msg)
        {
            try { Logger("[BongoKeyStats] " + msg); } catch { }
        }

        public static void Init()
        {
            if (_started || _failed) return;
            _started = true;
            try
            {
                Logger = s => UnityEngine.Debug.Log(s);
                string dir = ResolveDir();
                _store = new StatsStore(dir);
                _counter = new KeyCounter();
                string note;
                bool loaded = _store.Load(_counter, out note);
                if (note != null) Log(note);
                Log((loaded ? "loaded" : "no previous data;") + " data dir: " + dir);

                _poller = new KeyPoller(_counter, SaveNow);
                _poller.Start();

                var go = new GameObject("BongoKeyStats");
                UnityEngine.Object.DontDestroyOnLoad(go);
                go.AddComponent<StatsBehaviour>();
            }
            catch (Exception e)
            {
                Disable("init failed: " + e);
            }
        }

        private static string ResolveDir()
        {
            string game = Path.GetDirectoryName(Application.dataPath);
            string dir = Path.Combine(game, "KeyStats");
            if (StatsStore.IsWritable(dir)) return dir;
            string fallback = Path.Combine(Application.persistentDataPath, "KeyStats");
            Log("game folder not writable, using " + fallback);
            return fallback;
        }

        internal static void Disable(string reason)
        {
            _failed = true;
            Log("module disabled: " + reason);
            try { if (_poller != null) _poller.Stop(); } catch { }
        }

        public static void SaveNow()
        {
            if (_failed || _counter == null) return;
            lock (_saveLock)
            {
                try { _store.Save(_counter); }
                catch (Exception e) { Log("save failed: " + e.Message); }
            }
        }

        internal static string ExportReport()
        {
            var html = HtmlReportWriter.Build(_counter.TakeSnapshot());
            _store.WriteReport(html);
            return _store.ReportPath;
        }

        internal static bool OpenReport()
        {
            try
            {
                string path = ExportReport();
                Log("report written: " + path);
                Application.OpenURL("file:///" + path.Replace(System.IO.Path.DirectorySeparatorChar, '/'));
                return true;
            }
            catch (Exception e) { Log("export failed: " + e); return false; }
        }

        internal static void Shutdown()
        {
            if (_failed || _counter == null) return;
            try { _poller.Stop(); } catch { }
            SaveNow();
            try { ExportReport(); } catch (Exception e) { Log("report on exit failed: " + e.Message); }
        }
    }

    internal sealed class StatsBehaviour : MonoBehaviour
    {
        private StatsUi _ui;
        private bool _quit;

        private void Update()
        {
            try
            {
                if (_ui == null) _ui = new StatsUi();
                _ui.Tick();
            }
            catch (Exception e)
            {
                // The UI is optional; counting continues without it.
                Bootstrap.Log("ui disabled: " + e);
                enabled = false;
                if (_ui != null) _ui.Destroy();
            }
        }

        private void OnApplicationQuit()
        {
            if (_quit) return;
            _quit = true;
            Bootstrap.Shutdown();
        }
    }
}
