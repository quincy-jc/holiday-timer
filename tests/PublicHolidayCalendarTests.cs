using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using HolidayTimer;

internal static class PublicHolidayCalendarTests
{
    private static int checks;
    private static void Check(bool condition, string message)
    { if (!condition) throw new Exception("FAIL: " + message); checks++; }
    private static void Throws(Action action, string message)
    {
        bool threw = false;
        try { action(); } catch (Exception error) { threw = error is ArgumentException || error is InvalidDataException || error is NotSupportedException || error is IOException || error is UnauthorizedAccessException; }
        Check(threw, message);
    }
    private static string Table(int year, string movedLabour = null)
    {
        StringBuilder html = new StringBuilder("<html><table><thead><tr><th>Holiday</th><th>" + year + "</th></tr></thead><tbody>");
        // Construct a complete provider-shaped fixture from the independently
        // asserted named baseline, with optional source correction.
        string previous = null;
        foreach (NswPublicHoliday entry in NswPublicHolidays.Current.Entries)
        {
            if (entry.Date.Year != year) continue;
            string name = entry.Name;
            if (name.EndsWith(" (additional public holiday)", StringComparison.Ordinal))
            {
                string basis = name.Substring(0, name.Length - " (additional public holiday)".Length);
                // The official table keeps Boxing Day before Christmas's
                // additional row; ordering here is irrelevant to the dates.
                name = basis == previous ? "Additional Day" : name;
            }
            if (name != "Additional Day") previous = name;
            DateTime date = entry.Name == "Labour Day" && movedLabour != null ? DateTime.ParseExact(movedLabour, "yyyy-MM-dd", CultureInfo.InvariantCulture) : entry.Date;
            html.Append("<tr><td>").Append(name.Replace("'", "&#39;")).Append("</td><td>").Append(date.ToString("dddd d MMMM yyyy", CultureInfo.GetCultureInfo("en-AU"))).Append("</td></tr>");
        }
        html.Append("<tr><td><sup>1</sup>Bank Holiday</td><td>Monday 3 August " + year + "</td></tr>");
        return html.Append("</tbody></table></html>").ToString();
    }
    private static void ExactYear(int year, string expected)
    {
        List<string> actual = new List<string>();
        foreach (NswPublicHoliday entry in NswPublicHolidays.Current.Entries)
            if (entry.Date.Year == year) actual.Add(entry.Date.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
        Check(String.Join(",", actual.ToArray()) == expected, "Exact independently verified holiday dates for " + year);
    }
    private static int Main(string[] args)
    {
        PublicHolidaySnapshot original = NswPublicHolidays.Current;
        string scratch = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "calendar-checks-" + Guid.NewGuid().ToString("N"));
        try
        {
            if (args.Length > 0 && args[0] == "--live")
            {
                Console.WriteLine("HTTPS uses runtime protocols: " + System.Net.ServicePointManager.SecurityProtocol);
                PublicHolidaySnapshot live = NswPublicHolidays.Refresh(scratch);
                Check(live.RefreshedUtc.HasValue && live.OfficialThroughYear >= 2027 && live.Find(new DateTime(2026, 4, 27)) != null,
                    "Actual official HTTPS table validates and persists");
                Check(NswPublicHolidays.Initialize(scratch) == null, "Actual downloaded table reloads from the cache without network");
                NswPublicHoliday liveHoliday = NswPublicHolidays.Next(DateTime.Now);
                DateTime revised = liveHoliday.Date.AddDays(-1);
                while (revised > DateTime.Today && (revised.Year != liveHoliday.Date.Year || NswPublicHolidays.Find(revised) != null)) revised = revised.AddDays(-1);
                Check(revised > DateTime.Today && revised.Year == liveHoliday.Date.Year, "Live fixture has an unused correction date");
                NswPublicHolidays.SaveCorrection(scratch, liveHoliday.Key, revised, "Live test correction");
                NswPublicHolidays.Refresh(scratch);
                Check(NswPublicHolidays.Current.FindByKey(liveHoliday.Key).Date == revised && NswPublicHolidays.Current.FindByKey(liveHoliday.Key).IsEdited,
                    "Actual official refresh preserves a saved personal holiday edit");
                Check(NswPublicHolidays.Initialize(scratch) == null && NswPublicHolidays.Current.FindByKey(liveHoliday.Key).Date == revised,
                    "Actual refreshed source and personal edit reload together");
                Console.WriteLine("PASS " + checks + " live official refresh checks; verified through " + live.OfficialThroughYear + ", bundled horizon " + live.ThroughYear + ". Isolated cache: " + scratch);
                return 0;
            }
            Check(original.FromYear == 2024 && original.ThroughYear == 2031 && original.OfficialThroughYear == 2027 && !original.RefreshedUtc.HasValue,
                "Bundled coverage distinguishes confirmed dates and projected years");
            Check(original.Entries.Count == 94, "Bundled set covers eight complete years without Bank Holiday");
            ExactYear(2024, "20240101,20240126,20240329,20240330,20240331,20240401,20240425,20240610,20241007,20241225,20241226");
            ExactYear(2025, "20250101,20250127,20250418,20250419,20250420,20250421,20250425,20250609,20251006,20251225,20251226");
            ExactYear(2026, "20260101,20260126,20260403,20260404,20260405,20260406,20260425,20260427,20260608,20261005,20261225,20261226,20261228");
            ExactYear(2027, "20270101,20270126,20270326,20270327,20270328,20270329,20270425,20270426,20270614,20271004,20271225,20271226,20271227,20271228");
            ExactYear(2028, "20280101,20280103,20280126,20280414,20280415,20280416,20280417,20280425,20280612,20281002,20281225,20281226");
            ExactYear(2029, "20290101,20290126,20290330,20290331,20290401,20290402,20290425,20290611,20291001,20291225,20291226");
            ExactYear(2030, "20300101,20300128,20300419,20300420,20300421,20300422,20300425,20300610,20301007,20301225,20301226");
            ExactYear(2031, "20310101,20310127,20310411,20310412,20310413,20310414,20310425,20310609,20311006,20311225,20311226");
            foreach (NswPublicHoliday entry in original.Entries)
            {
                Check(entry.Date.Kind == DateTimeKind.Unspecified && entry.Date.TimeOfDay == TimeSpan.Zero, "Named dates are local all-day starts: " + entry.Date.ToString("yyyy-MM-dd"));
                Check(entry.IsOfficial == (entry.Date.Year <= 2027), "Confirmation metadata matches bundled provenance: " + entry.Date.ToString("yyyy-MM-dd"));
            }
            Check(original.Find(new DateTime(2026, 8, 3)) == null && original.Find(new DateTime(2025, 1, 26)) == null,
                "Bank Holiday and substituted Australia Day's original Sunday are excluded");
            Check(original.Next(new DateTime(2026, 10, 27)).Name == "Christmas Day", "Empty or expired October countdown finds Christmas");
            Check(original.Next(new DateTime(2026, 12, 24, 23, 59, 59)).Date == new DateTime(2026, 12, 25), "Next selection resolves holiday's local start");
            Check(original.Next(new DateTime(2026, 12, 25)).Date == new DateTime(2026, 12, 26), "Exact arrival moves to the following date");
            Check(original.Next(new DateTime(2026, 12, 29)).Date == new DateTime(2027, 1, 1), "Next holiday crosses year boundary");
            Check(original.Next(new DateTime(2032, 1, 1)) == null && original.Find(new DateTime(2032, 1, 1)) == null, "Unsupported horizon returns no invented dates");
            Check(original.GetUpcoming(new DateTime(2026, 12, 25), 3).Count == 3 && original.GetUpcoming(DateTime.MinValue, 0).Count == 0, "Upcoming list is bounded and can include today's start");
            Throws(delegate { original.Entries.Add(new NswPublicHoliday(new DateTime(2030, 3, 1), "Bad mutation", false)); }, "Snapshot entries are immutable");
            Throws(delegate { original.GetUpcoming(DateTime.MinValue, 1001); }, "Upcoming list rejects unbounded counts");
            Throws(delegate { new NswPublicHoliday(DateTime.Today, "<script>", false); }, "Holiday labels reject markup");

            string table = Table(2026);
            IList<NswPublicHoliday> parsed = NswPublicHolidays.ParseOfficialHtml(table);
            Check(parsed.Count == 13 && parsed[7].Name == "Anzac Day (additional public holiday)", "Provider-shaped table parses contextual additional days");
            Check(parsed[12].Name == "Boxing Day (additional public holiday)", "Observed Boxing Day is named correctly");
            Throws(delegate { NswPublicHolidays.ParseOfficialHtml("<html>Temporary error</html>"); }, "HTML error page is rejected");
            Throws(delegate { NswPublicHolidays.ParseOfficialHtml(table.Replace("Good Friday", "Unrecognised holiday")); }, "Missing standard holiday cannot erase working-day exclusions");
            Throws(delegate { NswPublicHolidays.ParseOfficialHtml(table.Replace("Friday 3 April 2026", "Thursday 3 April 2026")); }, "Wrong weekday rejects changed/malformed source");
            Throws(delegate { NswPublicHolidays.ParseOfficialHtml(table.Replace("Monday 5 October 2026", "Monday 5 October 2027")); }, "Cross-year date is rejected");
            Throws(delegate { NswPublicHolidays.ParseOfficialHtml(table.Replace("<th>2026</th>", "<th>Year changed</th>")); }, "Changed year heading is rejected");
            Throws(delegate { NswPublicHolidays.ParseOfficialHtml(table + table); }, "Conflicting duplicate tables are rejected");
            Throws(delegate { NswPublicHolidays.ParseOfficialHtml(new string('x', 2 * 1024 * 1024 + 1)); }, "Unbounded responses are rejected");

            DateTime checkedAt = new DateTime(2026, 10, 27, 4, 0, 0, DateTimeKind.Utc);
            PublicHolidaySnapshot updated = NswPublicHolidays.ApplyOfficialHtml(scratch, table, checkedAt);
            Check(updated.RefreshedUtc == checkedAt && updated.Revision > original.Revision && File.Exists(Path.Combine(scratch, "public-holidays.json")),
                "Successful refresh atomically saves and publishes a new snapshot");
            string saved = File.ReadAllText(Path.Combine(scratch, "public-holidays.json"));
            int revision = updated.Revision;
            Throws(delegate { NswPublicHolidays.ApplyOfficialHtml(scratch, "Broken page", checkedAt.AddDays(1)); }, "Malformed refresh fails");
            Check(NswPublicHolidays.Current.Revision == revision && File.ReadAllText(Path.Combine(scratch, "public-holidays.json")) == saved,
                "Failed refresh keeps both live and disk calendars intact");
            string denied = Path.Combine(scratch, "file-not-directory"); File.WriteAllText(denied, "Keep this file");
            Throws(delegate { NswPublicHolidays.ApplyOfficialHtml(denied, table, checkedAt.AddDays(1)); }, "Unwritable cache cannot be reported as a successful refresh");
            Check(NswPublicHolidays.Current.Revision == revision && File.ReadAllText(denied) == "Keep this file", "Disk failure does not publish a partial snapshot");

            string changed = Table(2026, "2026-10-06");
            PublicHolidaySnapshot corrected = NswPublicHolidays.ApplyOfficialHtml(scratch, changed, checkedAt.AddDays(2));
            Check(corrected.Find(new DateTime(2026, 10, 5)) == null && corrected.Find(new DateTime(2026, 10, 6)).Name == "Labour Day",
                "Complete-year replacement removes a moved date and adds its official correction");
            Check(corrected.Find(new DateTime(2027, 4, 26)) != null && corrected.Find(new DateTime(2031, 12, 25)) != null,
                "Replacing one published year retains other confirmed/projected years");
            Check(File.Exists(Path.Combine(scratch, "public-holidays.json.bak")), "Cache replacement retains the previous successful file as backup");
            NswPublicHolidays.RestoreForTests(original);
            Check(NswPublicHolidays.Initialize(scratch) == null && NswPublicHolidays.Find(new DateTime(2026, 10, 6)) != null,
                "Restart initialization loads the last successful correction without network");
            NswPublicHolidays.ApplyOfficialHtml(scratch, Table(2027), checkedAt.AddDays(3));
            NswPublicHolidays.RestoreForTests(original);
            Check(NswPublicHolidays.Initialize(scratch) == null && NswPublicHolidays.Find(new DateTime(2026, 10, 6)) != null && NswPublicHolidays.Find(new DateTime(2026, 10, 5)) == null,
                "A rolling source table cannot erase a previous year's persisted correction");
            NswPublicHoliday christmas = NswPublicHolidays.Find(new DateTime(2026, 12, 25));
            string christmasKey = christmas.Key;
            PublicHolidaySnapshot edited = NswPublicHolidays.SaveCorrection(scratch, christmasKey, new DateTime(2026, 12, 24), "Adjusted Christmas");
            Check(edited.Find(christmas.Date) == null && edited.Find(new DateTime(2026, 12, 24)).Name == "Adjusted Christmas" &&
                edited.FindByKey(christmasKey).IsEdited && !edited.FindByKey(christmasKey).IsOfficial,
                "A manual name/date edit changes the shared calendar without claiming official provenance");
            Check(edited.FindByKey(christmasKey).SourceDate == christmas.Date && edited.FindByKey(christmasKey).SourceName == christmas.Name,
                "An edit retains a stable link to its original holiday");
            string editsPath = Path.Combine(scratch, "public-holiday-edits.json");
            string edits = File.ReadAllText(editsPath);
            int editedRevision = edited.Revision;
            Throws(delegate { NswPublicHolidays.SaveCorrection(scratch, christmasKey, new DateTime(2026, 12, 26), "Collision"); },
                "An edit cannot silently overwrite another holiday");
            Check(NswPublicHolidays.Current.Revision == editedRevision && File.ReadAllText(editsPath) == edits,
                "A rejected conflicting edit keeps the last good live and saved calendar");
            Throws(delegate { NswPublicHolidays.SaveCorrection(scratch, christmasKey, new DateTime(2027, 1, 3), "Wrong year"); },
                "An edit cannot move a holiday into a different source year");
            Check(NswPublicHolidays.Initialize(scratch) == null && NswPublicHolidays.Current.FindByKey(christmasKey).Date == new DateTime(2026, 12, 24),
                "A manual holiday correction survives application restart");
            NswPublicHolidays.ApplyOfficialHtml(scratch, table, checkedAt.AddDays(4));
            Check(NswPublicHolidays.Current.FindByKey(christmasKey).Date == new DateTime(2026, 12, 24) && NswPublicHolidays.Current.FindByKey(christmasKey).IsEdited,
                "Refreshing the official table preserves the user's holiday correction");
            Check(NswPublicHolidays.Initialize(scratch) == null && NswPublicHolidays.Current.FindByKey(christmasKey).Name == "Adjusted Christmas",
                "The official cache never embeds a manual edit and both reload together");
            File.WriteAllText(Path.Combine(scratch, "public-holidays.json"), "not json");
            Check(NswPublicHolidays.Initialize(scratch) != null && NswPublicHolidays.Current.FindByKey(christmasKey).Name == "Adjusted Christmas",
                "A damaged official cache retains a valid personal holiday edit");
            NswPublicHolidays.ApplyOfficialHtml(scratch, table, checkedAt.AddDays(5));
            Check(NswPublicHolidays.Current.FindByKey(christmasKey).Name == "Adjusted Christmas",
                "Refreshing after a damaged official cache preserves the personal edit");
            NswPublicHolidays.SaveCorrection(scratch, christmasKey, null, null);
            Check(NswPublicHolidays.Find(new DateTime(2026, 12, 25)).Name == "Christmas Day" && NswPublicHolidays.Find(new DateTime(2026, 12, 24)) == null,
                "Reset restores the latest source name and date");
            Check(NswPublicHolidays.Initialize(scratch) == null && !NswPublicHolidays.Current.FindByKey(christmasKey).IsEdited,
                "Reset remains effective after restart");
            PublicHolidaySnapshot knownGood = NswPublicHolidays.Current;
            File.WriteAllText(Path.Combine(scratch, "public-holidays.json"), "not json");
            Check(NswPublicHolidays.Initialize(scratch) != null && Object.ReferenceEquals(knownGood, NswPublicHolidays.Current),
                "Corrupt cache leaves the current last-good snapshot available with a warning");
            Check(Directory.GetFiles(scratch, "*.tmp").Length == 0, "Refresh paths leave no temporary files");
            Console.WriteLine("PASS " + checks + " public-holiday data checks. No network, real preferences or installed app were used.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        finally { NswPublicHolidays.RestoreForTests(original); }
    }
}
