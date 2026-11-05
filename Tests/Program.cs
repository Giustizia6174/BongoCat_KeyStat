using System;
using System.IO;
using BongoKeyStats;

internal static class Tests
{
    private static int _fail;
    private static void Check(bool ok, string what) { Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) _fail++; }

    private static int Main(string[] args)
    {
        string dir = Path.Combine(Path.GetTempPath(), "bks_test_" + Guid.NewGuid().ToString("N"));
        var now = new DateTime(2026, 11, 4, 13, 30, 0);

        // counting: A x10, B x5 -> 66.7% / 33.3%
        var c = new KeyCounter();
        for (int i = 0; i < 10; i++) c.Hit('A', now);
        for (int i = 0; i < 5; i++) c.Hit('B', now);
        var s = c.TakeSnapshot();
        Check(s.Total['A'] == 10 && s.Total['B'] == 5 && s.Session['A'] == 10, "A=10 B=5");
        Check(Math.Round(100.0 * s.Total['A'] / 15, 1) == 66.7, "share 66.7%");
        Check(s.Daily["2026-11-04"]['A'] == 10 && s.Hourly["2026-11-04"][13] == 15, "daily/hourly buckets");

        // persistence roundtrip, session resets, total continues
        var store = new StatsStore(dir);
        store.Save(c);
        var c2 = new KeyCounter();
        string note;
        Check(store.Load(c2, out note), "load ok");
        var s2 = c2.TakeSnapshot();
        Check(s2.Total['A'] == 10 && s2.Session['A'] == 0, "total persists, session is zero");
        c2.Hit('A', now.AddDays(1));
        Check(c2.TakeSnapshot().Total['A'] == 11, "accumulates across launches");
        Check(c2.TakeSnapshot().Daily.Count == 2, "two days");

        // second save creates .bak; corrupt main falls back to .bak
        store.Save(c2);
        Check(File.Exists(store.BackupPath), ".bak exists");
        File.WriteAllText(store.DataPath, "{ corrupt");
        var c3 = new KeyCounter();
        Check(store.Load(c3, out note) && note != null && c3.TakeSnapshot().Total['A'] == 10, "corrupt main -> restored from .bak: " + note);

        // reset
        c3.Reset();
        Check(c3.TakeSnapshot().Total['A'] == 0, "reset");

        // html report
        string html = HtmlReportWriter.Build(s2);
        Check(html.Contains("\"total\":[") && !html.Contains("/*__DATA__*/"), "report embeds data");
        string outPath = args.Length > 0 ? args[0] : Path.Combine(dir, "sample.html");

        // richer sample for visual check
        var big = new KeyCounter();
        var rnd = new Random(7);
        int[] keys = { 'E', 'T', 'A', 'O', 'I', 'N', 'S', 'H', 'R', 'D', 'L', 0x20, 0x0D, 0x08, 0xA0, 0xA2, 0x1B, 0x70, 0x71, 0x25, 0x26, 0x27, 0x28, '1', '2', '3', 0xBC, 0xBE, 0x60, 0x61 };
        for (int day = 40; day >= 0; day--)
            for (int n = 0; n < 400 + rnd.Next(2000); n++)
            {
                int k = keys[(int)(Math.Pow(rnd.NextDouble(), 2.2) * keys.Length)];
                big.Hit(k, DateTime.Now.Date.AddDays(-day).AddHours(8 + rnd.Next(14)));
            }
        File.WriteAllText(outPath, HtmlReportWriter.Build(big.TakeSnapshot()));
        Console.WriteLine("sample report: " + outPath);

        try { Directory.Delete(dir, true); } catch { }
        Console.WriteLine(_fail == 0 ? "ALL PASSED" : _fail + " FAILED");
        return _fail;
    }
}
