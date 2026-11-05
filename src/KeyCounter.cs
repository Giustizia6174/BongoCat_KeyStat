using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace BongoKeyStats
{
    /// <summary>Thread-safe key counters: session, all-time, per day and per hour of day.</summary>
    public sealed class KeyCounter
    {
        public const int MaxSessions = 200;

        private readonly object _lock = new object();
        private readonly long[] _session = new long[256];
        private readonly long[] _total = new long[256];
        private readonly Dictionary<string, long[]> _daily = new Dictionary<string, long[]>();
        private readonly Dictionary<string, long[]> _hourly = new Dictionary<string, long[]>();
        private readonly List<SessionInfo> _sessions = new List<SessionInfo>();
        private string _firstRun;
        private string _cachedDay;
        private long[] _cachedDaily;
        private long[] _cachedHourly;
        private bool _dirty;

        public DateTime SessionStart { get; private set; }

        public struct SessionInfo { public string Start, End; public long Count; }

        /// <summary>Immutable copy for UI/report code.</summary>
        public sealed class Snapshot
        {
            public long[] Session, Total;
            public Dictionary<string, long[]> Daily, Hourly;
            public List<SessionInfo> Sessions;
            public string FirstRun;
            public DateTime SessionStart;
        }

        public KeyCounter() { SessionStart = DateTime.Now; _firstRun = Day(SessionStart); }

        private static string Day(DateTime t) { return t.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); }

        public bool Dirty { get { lock (_lock) return _dirty; } }

        public void Hit(int vk, DateTime now)
        {
            if (vk < 0 || vk > 255) return;
            lock (_lock)
            {
                _session[vk]++;
                _total[vk]++;
                string day = Day(now);
                if (day != _cachedDay)
                {
                    long[] d, h;
                    if (!_daily.TryGetValue(day, out d)) { d = new long[256]; _daily[day] = d; }
                    if (!_hourly.TryGetValue(day, out h)) { h = new long[24]; _hourly[day] = h; }
                    _cachedDay = day; _cachedDaily = d; _cachedHourly = h;
                }
                _cachedDaily[vk]++;
                _cachedHourly[now.Hour]++;
                _dirty = true;
            }
        }

        public Snapshot TakeSnapshot()
        {
            lock (_lock)
            {
                var s = new Snapshot
                {
                    Session = (long[])_session.Clone(),
                    Total = (long[])_total.Clone(),
                    Daily = new Dictionary<string, long[]>(),
                    Hourly = new Dictionary<string, long[]>(),
                    Sessions = new List<SessionInfo>(_sessions),
                    FirstRun = _firstRun,
                    SessionStart = SessionStart
                };
                foreach (var kv in _daily) s.Daily[kv.Key] = (long[])kv.Value.Clone();
                foreach (var kv in _hourly) s.Hourly[kv.Key] = (long[])kv.Value.Clone();
                s.Sessions.Add(CurrentSession(DateTime.Now));
                return s;
            }
        }

        private SessionInfo CurrentSession(DateTime now)
        {
            long n = 0;
            for (int i = 0; i < 256; i++) n += _session[i];
            return new SessionInfo { Start = SessionStart.ToString("s"), End = now.ToString("s"), Count = n };
        }

        public void Reset()
        {
            lock (_lock)
            {
                Array.Clear(_session, 0, 256);
                Array.Clear(_total, 0, 256);
                _daily.Clear(); _hourly.Clear(); _sessions.Clear();
                _cachedDay = null; _cachedDaily = null; _cachedHourly = null;
                _firstRun = Day(DateTime.Now);
                _dirty = true;
            }
        }

        /// <summary>Serialises everything (including the running session) and clears the dirty flag.</summary>
        public string ToJson()
        {
            lock (_lock)
            {
                var sb = new StringBuilder(4096);
                sb.Append("{\n  \"schema\": 1,\n  \"firstRun\": ").Append(Json.Quote(_firstRun ?? Day(DateTime.Now))).Append(",\n");
                sb.Append("  \"total\": "); Counts(sb, _total); sb.Append(",\n");
                sb.Append("  \"daily\": {");
                bool first = true;
                var days = new List<string>(_daily.Keys); days.Sort(string.CompareOrdinal);
                foreach (string d in days)
                {
                    sb.Append(first ? "\n    " : ",\n    ").Append(Json.Quote(d)).Append(": "); Counts(sb, _daily[d]); first = false;
                }
                sb.Append(first ? "},\n" : "\n  },\n");
                sb.Append("  \"hourly\": {");
                first = true;
                foreach (string d in days)
                {
                    long[] h;
                    if (!_hourly.TryGetValue(d, out h)) continue;
                    sb.Append(first ? "\n    " : ",\n    ").Append(Json.Quote(d)).Append(": [");
                    for (int i = 0; i < 24; i++) { if (i > 0) sb.Append(','); sb.Append(h[i]); }
                    sb.Append(']'); first = false;
                }
                sb.Append(first ? "},\n" : "\n  },\n");
                sb.Append("  \"sessions\": [");
                var all = new List<SessionInfo>(_sessions);
                all.Add(CurrentSession(DateTime.Now));
                int from = Math.Max(0, all.Count - MaxSessions);
                for (int i = from; i < all.Count; i++)
                {
                    if (i > from) sb.Append(',');
                    sb.Append("\n    {\"start\": ").Append(Json.Quote(all[i].Start)).Append(", \"end\": ").Append(Json.Quote(all[i].End))
                      .Append(", \"count\": ").Append(all[i].Count).Append('}');
                }
                sb.Append("\n  ]\n}\n");
                _dirty = false;
                return sb.ToString();
            }
        }

        private static void Counts(StringBuilder sb, long[] a)
        {
            sb.Append('{');
            bool first = true;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] == 0) continue;
                if (!first) sb.Append(", ");
                sb.Append('"').Append(i).Append("\": ").Append(a[i]);
                first = false;
            }
            sb.Append('}');
        }

        /// <summary>Loads a document produced by ToJson. Throws on malformed input; counters are untouched in that case.</summary>
        public void LoadJson(string text)
        {
            var root = Json.Parse(text) as Dictionary<string, object>;
            if (root == null) throw new FormatException("root");
            var total = new long[256];
            var daily = new Dictionary<string, long[]>();
            var hourly = new Dictionary<string, long[]>();
            var sessions = new List<SessionInfo>();
            ReadCounts(root, "total", total);
            object o;
            if (root.TryGetValue("daily", out o))
            {
                foreach (var kv in (Dictionary<string, object>)o)
                {
                    var a = new long[256];
                    ReadCounts((Dictionary<string, object>)kv.Value, null, a);
                    daily[kv.Key] = a;
                }
            }
            if (root.TryGetValue("hourly", out o))
            {
                foreach (var kv in (Dictionary<string, object>)o)
                {
                    var a = new long[24];
                    var l = (List<object>)kv.Value;
                    for (int i = 0; i < 24 && i < l.Count; i++) a[i] = (long)(double)l[i];
                    hourly[kv.Key] = a;
                }
            }
            if (root.TryGetValue("sessions", out o))
            {
                foreach (object e in (List<object>)o)
                {
                    var d = (Dictionary<string, object>)e;
                    sessions.Add(new SessionInfo { Start = (string)d["start"], End = (string)d["end"], Count = (long)(double)d["count"] });
                }
            }
            string first = root.TryGetValue("firstRun", out o) ? o as string : null;

            lock (_lock)
            {
                Array.Copy(total, _total, 256);
                _daily.Clear(); foreach (var kv in daily) _daily[kv.Key] = kv.Value;
                _hourly.Clear(); foreach (var kv in hourly) _hourly[kv.Key] = kv.Value;
                _sessions.Clear(); _sessions.AddRange(sessions);
                if (!string.IsNullOrEmpty(first)) _firstRun = first;
                _cachedDay = null; _cachedDaily = null; _cachedHourly = null;
            }
        }

        private static void ReadCounts(Dictionary<string, object> parent, string key, long[] dst)
        {
            Dictionary<string, object> src = parent;
            if (key != null)
            {
                object o;
                if (!parent.TryGetValue(key, out o)) return;
                src = (Dictionary<string, object>)o;
            }
            foreach (var kv in src)
            {
                int vk = int.Parse(kv.Key, CultureInfo.InvariantCulture);
                if (vk >= 0 && vk < 256) dst[vk] = (long)(double)kv.Value;
            }
        }
    }
}
