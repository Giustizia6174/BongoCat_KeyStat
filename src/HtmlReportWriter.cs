using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;

namespace BongoKeyStats
{
    public static class HtmlReportWriter
    {
        private const string Placeholder = "/*__DATA__*/null";

        public static string Build(KeyCounter.Snapshot s)
        {
            return ReportTemplate.Html.Replace(Placeholder, DataJson(s));
        }

        public static string DataJson(KeyCounter.Snapshot s)
        {
            var sb = new StringBuilder(8192);
            sb.Append("{\"generated\":").Append(Json.Quote(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)));
            sb.Append(",\"firstRun\":").Append(Json.Quote(s.FirstRun ?? ""));
            sb.Append(",\"sessionStart\":").Append(Json.Quote(s.SessionStart.ToString("s")));
            sb.Append(",\"today\":").Append(Json.Quote(DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
            sb.Append(",\"groups\":["); Join(sb, KeyNames.Groups); sb.Append(']');
            sb.Append(",\"names\":{");
            bool first = true;
            for (int i = 0; i < 256; i++)
            {
                if (s.Total[i] == 0 && s.Session[i] == 0) continue;
                if (!first) sb.Append(',');
                sb.Append('"').Append(i).Append("\":[").Append(Json.Quote(KeyNames.Name(i))).Append(',').Append(KeyNames.Group(i)).Append(']');
                first = false;
            }
            sb.Append("},\"layout\":[");
            for (int i = 0; i < KeyNames.Layout.Count; i++)
            {
                var c = KeyNames.Layout[i];
                if (i > 0) sb.Append(',');
                sb.Append('[').Append(c.Vk).Append(',').Append(Json.Quote(c.Label)).Append(',')
                  .Append(F(c.X)).Append(',').Append(F(c.Y)).Append(',').Append(F(c.W)).Append(']');
            }
            sb.Append("],\"total\":"); Arr(sb, s.Total);
            sb.Append(",\"session\":"); Arr(sb, s.Session);
            sb.Append(",\"daily\":{");
            first = true;
            foreach (var kv in s.Daily)
            {
                if (!first) sb.Append(',');
                sb.Append(Json.Quote(kv.Key)).Append(':'); Arr(sb, kv.Value); first = false;
            }
            sb.Append("},\"hourly\":{");
            first = true;
            foreach (var kv in s.Hourly)
            {
                if (!first) sb.Append(',');
                sb.Append(Json.Quote(kv.Key)).Append(':'); Arr(sb, kv.Value); first = false;
            }
            sb.Append("},\"sessions\":[");
            for (int i = 0; i < s.Sessions.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append('[').Append(Json.Quote(s.Sessions[i].Start)).Append(',').Append(Json.Quote(s.Sessions[i].End)).Append(',').Append(s.Sessions[i].Count).Append(']');
            }
            sb.Append("]}");
            return sb.ToString();
        }

        private static string F(float v) { return v.ToString("0.##", CultureInfo.InvariantCulture); }

        private static void Join(StringBuilder sb, string[] a)
        {
            for (int i = 0; i < a.Length; i++) { if (i > 0) sb.Append(','); sb.Append(Json.Quote(a[i])); }
        }

        private static void Arr(StringBuilder sb, long[] a)
        {
            sb.Append('[');
            for (int i = 0; i < a.Length; i++) { if (i > 0) sb.Append(','); sb.Append(a[i]); }
            sb.Append(']');
        }
    }
}
