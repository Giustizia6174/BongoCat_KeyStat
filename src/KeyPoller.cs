using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace BongoKeyStats
{
    /// <summary>
    /// Polls GetAsyncKeyState on a background thread (works while the game window is unfocused).
    /// A click is a released-to-pressed transition; auto-repeat does not count. Mouse buttons are skipped.
    /// Only reads the high bit, so it never consumes the "pressed since last call" bit other callers rely on.
    /// </summary>
    public sealed class KeyPoller
    {
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private readonly KeyCounter _counter;
        private readonly Action _onSaveDue;
        private readonly bool[] _down = new bool[256];
        private readonly bool[] _watch = new bool[256];
        private Thread _thread;
        private volatile bool _stop;
        private readonly int _intervalMs;
        private readonly int _saveEveryMs;

        public KeyPoller(KeyCounter counter, Action onSaveDue, int intervalMs = 10, int saveEveryMs = 30000)
        {
            _counter = counter; _onSaveDue = onSaveDue; _intervalMs = intervalMs; _saveEveryMs = saveEveryMs;
            for (int vk = 8; vk < 255; vk++) _watch[vk] = true;
            // generic modifier codes duplicate the left/right variants
            _watch[0x10] = _watch[0x11] = _watch[0x12] = false;
        }

        public void Start()
        {
            // Treat keys already held at startup as pressed so they are not counted.
            for (int vk = 0; vk < 256; vk++) if (_watch[vk]) _down[vk] = (GetAsyncKeyState(vk) & 0x8000) != 0;
            _thread = new Thread(Loop) { IsBackground = true, Name = "BongoKeyStats.Poller" };
            _thread.Start();
        }

        public void Stop() { _stop = true; }

        private void Loop()
        {
            long lastSave = Environment.TickCount;
            while (!_stop)
            {
                try
                {
                    DateTime now = DateTime.Now;
                    for (int vk = 8; vk < 255; vk++)
                    {
                        if (!_watch[vk]) continue;
                        bool pressed = (GetAsyncKeyState(vk) & 0x8000) != 0;
                        if (pressed && !_down[vk]) _counter.Hit(vk, now);
                        _down[vk] = pressed;
                    }
                    if (unchecked(Environment.TickCount - (int)lastSave) >= _saveEveryMs)
                    {
                        lastSave = Environment.TickCount;
                        if (_counter.Dirty && _onSaveDue != null) _onSaveDue();
                    }
                }
                catch (Exception e)
                {
                    Bootstrap.Log("poller error: " + e.Message);
                    Thread.Sleep(1000);
                }
                Thread.Sleep(_intervalMs);
            }
        }
    }
}
