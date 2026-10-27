using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using HolidayTimer;

internal static class WorkCalendarTests
{
    private static int checks;
    private static readonly TimeZoneInfo Sydney = TimeZoneInfo.FindSystemTimeZoneById("AUS Eastern Standard Time");
    private static void Equal<T>(T expected, T actual, string message)
    { if (!Object.Equals(expected, actual)) throw new Exception(message + ": expected " + expected + ", got " + actual); checks++; }
    private static void True(bool value, string message) { Equal(true, value, message); }
    private static void Throws(Action action, string message)
    { try { action(); } catch (ArgumentException) { checks++; return; } throw new Exception(message + ": no validation error"); }
    private static DateTimeOffset At(string value) { return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture); }
    private static AppSettings Work(string start = "09:00", string finish = "17:00")
    { return new AppSettings { WorkdayCountdown = true, WorkStart = start, WorkFinish = finish }; }
    private static long Count(AppSettings settings, string now, string target, CountdownUnit unit)
    { return WorkCountdownCalculator.Remaining(settings, At(target), At(now), unit, Sydney); }

    private static int Main()
    {
        string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "work-calendar-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            CalendarCases(); ScheduleCases(); CountingCases(); ClockCases(); CompatibilityCases(folder); CacheCases(); RefreshCacheCases(); ManualCorrectionCases(folder);
            Console.WriteLine("PASS: " + checks + " work calendar checks."); return 0;
        }
        catch (Exception error) { Console.Error.WriteLine("FAIL: " + error); return 1; }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }

    private static void CalendarCases()
    {
        Equal("Christmas Day", NswWorkCalendar.GetPublicHolidayName(new DateTime(2026, 12, 25)), "Holiday tooltip names use the shared verified calendar");
        Equal("Boxing Day (additional public holiday)", NswWorkCalendar.GetPublicHolidayName(new DateTime(2026, 12, 28)), "Observed holiday names identify the extra day");
        Equal("Anzac Day (additional public holiday)", NswWorkCalendar.GetPublicHolidayName(new DateTime(2027, 4, 26)), "Current additional Anzac day has a holiday name");
        Equal("Good Friday", NswWorkCalendar.GetPublicHolidayName(new DateTime(2025, 4, 18)), "Easter holiday names identify their weekday");
        Equal(null, NswWorkCalendar.GetPublicHolidayName(new DateTime(2026, 8, 3)), "Bank Holiday does not gain a statewide holiday marker");
        int[][] expected = {
            new[] { 20240101, 20240126, 20240329, 20240330, 20240331, 20240401, 20240425, 20240610, 20241007, 20241225, 20241226 },
            new[] { 20250101, 20250127, 20250418, 20250419, 20250420, 20250421, 20250425, 20250609, 20251006, 20251225, 20251226 },
            new[] { 20260101, 20260126, 20260403, 20260404, 20260405, 20260406, 20260425, 20260427, 20260608, 20261005, 20261225, 20261226, 20261228 },
            new[] { 20270101, 20270126, 20270326, 20270327, 20270328, 20270329, 20270425, 20270426, 20270614, 20271004, 20271225, 20271226, 20271227, 20271228 }
        };
        foreach (int[] year in expected)
        {
            foreach (int day in year)
            {
                DateTime date = new DateTime(day / 10000, day / 100 % 100, day % 100);
                True(NswWorkCalendar.IsPublicHoliday(date), "Official statewide holiday " + day);
                True(!String.IsNullOrEmpty(NswWorkCalendar.GetPublicHolidayName(date)), "Every verified date has a holiday name " + day);
            }
            int found = 0;
            DateTime first = new DateTime(year[0] / 10000, 1, 1);
            for (DateTime date = first; date.Year == first.Year; date = date.AddDays(1))
                if (NswWorkCalendar.IsPublicHoliday(date)) found++;
            Equal(year.Length, found, "No unverified dates added for " + first.Year);
        }
        True(NswWorkCalendar.IsWorkday(new DateTime(2026, 8, 3)), "2026 Bank Holiday remains a statewide workday");
        True(NswWorkCalendar.IsWorkday(new DateTime(2027, 8, 2)), "2027 Bank Holiday remains a statewide workday");
        Equal(false, NswWorkCalendar.IsPublicHoliday(new DateTime(2025, 1, 26)), "2025 Australia Day uses the Monday, not Sunday");
        Equal(false, NswWorkCalendar.IsWorkday(new DateTime(2026, 10, 17)), "Saturday excluded");
        Equal(false, NswWorkCalendar.IsWorkday(new DateTime(2026, 10, 18)), "Sunday excluded");
        Equal(2031, NswWorkCalendar.SupportedThrough, "The built-in work calendar reaches the same horizon as holiday selection");
        Equal(false, NswWorkCalendar.IsWorkday(new DateTime(2028, 1, 3)), "Projected New Year's additional day is excluded from working time");
        Equal(true, NswWorkCalendar.IsWorkday(new DateTime(2028, 1, 4)), "The next ordinary projected-year weekday counts as working time");
        Equal(false, NswPublicHolidays.Current.Find(new DateTime(2028, 1, 3)).IsOfficial, "Future rule-based dates retain their projected provenance");
        Throws(delegate { NswWorkCalendar.IsWorkday(new DateTime(2032, 1, 3)); }, "Beyond-horizon work calendar rejected");
        Throws(delegate { NswWorkCalendar.IsPublicHoliday(new DateTime(2023, 12, 25)); }, "Unsupported historical calendar rejected");
    }

    private static void ScheduleCases()
    {
        foreach (string[] times in new[] { new[] { "09:00", "17:00" }, new[] { "08:30", "14:30" }, new[] { "07:00", "17:00" }, new[] { "00:00", "00:01" }, new[] { "00:00", "23:59" } })
        { WorkCountdownCalculator.ValidateSchedule(Work(times[0], times[1])); checks++; }
        foreach (string[] times in new[] { new[] { "09:00", "09:00" }, new[] { "17:00", "09:00" }, new[] { "9:00", "17:00" }, new[] { "09:00", "24:00" }, new[] { "09:00:00", "17:00" }, new[] { "09:60", "17:00" }, new[] { "", "17:00" } })
            Throws(delegate { WorkCountdownCalculator.ValidateSchedule(Work(times[0], times[1])); }, "Reject invalid schedule " + times[0] + " to " + times[1]);
        Throws(delegate { WorkCountdownCalculator.ValidateSchedule(null); }, "Null settings rejected");
    }

    private static void CountingCases()
    {
        AppSettings settings = Work();
        string start = "2026-10-15T09:00:00+11:00", finish = "2026-10-15T17:00:00+11:00";
        Equal(1L, Count(settings, start, finish, CountdownUnit.Days), "Eight scheduled hours is one day");
        Equal(8L, Count(settings, start, finish, CountdownUnit.Hours), "Eight scheduled hours is eight hours");
        Equal(480L, Count(settings, start, finish, CountdownUnit.Minutes), "All units use the selected work window");
        Equal(28800L, Count(settings, start, finish, CountdownUnit.Seconds), "Full shift seconds");
        Equal(1L, Count(settings, start, finish, CountdownUnit.Weeks), "A partial five-shift week rounds up");
        Equal(1L, Count(Work("09:00", "15:00"), start, "2026-10-15T15:00:00+11:00", CountdownUnit.Days), "Six-hour shift is one day");
        Equal(6L, Count(Work("09:00", "15:00"), start, "2026-10-15T15:00:00+11:00", CountdownUnit.Hours), "Six-hour shift is six hours");
        Equal(1L, Count(Work("07:00", "17:00"), "2026-10-15T07:00:00+11:00", finish, CountdownUnit.Days), "Ten-hour shift is one day");
        Equal(10L, Count(Work("07:00", "17:00"), "2026-10-15T07:00:00+11:00", finish, CountdownUnit.Hours), "Ten-hour shift is ten hours");
        // 12:00–17:00 today plus 09:00–12:00 tomorrow is exactly eight hours.
        Equal(1L, Count(settings, "2026-10-15T12:00:00+11:00", "2026-10-16T12:00:00+11:00", CountdownUnit.Days), "Partial shifts accumulate into one selected day");
        Equal(480L, Count(settings, "2026-10-15T12:00:00+11:00", "2026-10-16T12:00:00+11:00", CountdownUnit.Minutes), "Partial shifts preserve exact minutes");
        Equal(8L, Count(settings, "2026-10-15T00:00:00+11:00", "2026-10-15T23:00:00+11:00", CountdownUnit.Hours), "Outside-shift hours never count");
        Equal(0L, Count(settings, "2026-10-15T17:00:00+11:00", "2026-10-16T09:00:00+11:00", CountdownUnit.Seconds), "Night to next shift start has no work left");
        Equal(0L, Count(settings, "2026-10-17T09:00:00+11:00", "2026-10-19T08:59:00+11:00", CountdownUnit.Seconds), "Future weekend target before Monday shift is zero without arrival");
        True(At("2026-10-19T08:59:00+11:00") > At("2026-10-17T09:00:00+11:00"), "Zero work count is independent of target arrival");
        Equal(16L, Count(settings, "2026-04-24T09:00:00+10:00", "2026-04-28T17:00:00+10:00", CountdownUnit.Hours), "2026 extra Anzac Monday excluded");
        Equal(16L, Count(settings, "2027-04-23T09:00:00+10:00", "2027-04-27T17:00:00+10:00", CountdownUnit.Hours), "2027 extra Anzac Monday excluded");
        Equal(2L, Count(settings, "2026-12-24T16:00:00+11:00", "2026-12-29T10:00:00+11:00", CountdownUnit.Hours), "Christmas, weekend and observed Boxing Day excluded");
        Equal(2L, Count(settings, "2027-12-24T16:00:00+11:00", "2027-12-29T10:00:00+11:00", CountdownUnit.Hours), "Both 2027 Christmas observed days excluded");
        Equal(16L, Count(settings, "2026-12-31T09:00:00+11:00", "2027-01-04T17:00:00+11:00", CountdownUnit.Hours), "Cross-year work skips New Year's Day and weekend");
        Equal(40L, Count(settings, "2026-10-12T09:00:00+11:00", "2026-10-16T17:00:00+11:00", CountdownUnit.Hours), "Five full shifts are forty hours");
        Equal(1L, Count(settings, "2026-10-12T09:00:00+11:00", "2026-10-16T17:00:00+11:00", CountdownUnit.Weeks), "Five shifts are exactly one week");
        Equal(2L, Count(settings, "2026-10-12T09:00:00+11:00", "2026-10-19T09:01:00+11:00", CountdownUnit.Weeks), "A positive extra working minute rounds up the week");
        DateTimeOffset now = At(start);
        foreach (CountdownUnit unit in new[] { CountdownUnit.Days, CountdownUnit.Weeks, CountdownUnit.Hours, CountdownUnit.Minutes, CountdownUnit.Seconds })
        { Equal(1L, WorkCountdownCalculator.Remaining(settings, now.AddTicks(1), now, unit, Sydney), "One working tick rounds up in " + unit); Equal(0L, WorkCountdownCalculator.Remaining(settings, now, now, unit, Sydney), "Target instant zero in " + unit); }
        Equal(0L, WorkCountdownCalculator.Remaining(settings, At("2023-01-01T00:00:00+11:00"), now, CountdownUnit.Days, Sydney), "Already-arrived unsupported year returns zero first");
        Equal(8L, Count(settings, "2028-01-03T09:00:00+11:00", "2028-01-04T17:00:00+11:00", CountdownUnit.Hours), "Projected future holidays and ordinary working shifts share one calendar");
        Throws(delegate { WorkCountdownCalculator.Remaining(settings, At("2032-01-03T09:00:00+11:00"), now, CountdownUnit.Hours, Sydney); }, "Beyond-horizon future target rejected");
        Throws(delegate { WorkCountdownCalculator.Remaining(settings, now.AddDays(1), now, (CountdownUnit)99, Sydney); }, "Unknown work unit rejected");
    }

    private static void ClockCases()
    {
        Equal(16L, Count(Work(), "2026-10-02T09:00:00+10:00", "2026-10-06T17:00:00+11:00", CountdownUnit.Hours), "Spring DST weekend and Labour Day do not distort working shifts");
        Equal(30L, Count(Work(), "2026-10-02T16:45:00+10:00", "2026-10-06T09:15:00+11:00", CountdownUnit.Minutes), "Partial Friday/Tuesday around DST and Labour Day");
        DateTimeOffset skipped = WorkCountdownCalculator.ResolveBoundary(new DateTime(2026, 10, 4, 2, 30, 0), Sydney);
        Equal(At("2026-10-04T03:00:00+11:00"), skipped, "Skipped boundary advances to first valid minute");
        DateTimeOffset repeated = WorkCountdownCalculator.ResolveBoundary(new DateTime(2026, 4, 5, 2, 30, 0), Sydney);
        Equal(At("2026-04-05T02:30:00+10:00"), repeated, "Repeated boundary uses later occurrence");
        TimeZoneInfo.TransitionTime spring = TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 2, 0, 0), 10, 6);
        TimeZoneInfo.TransitionTime autumn = TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 3, 0, 0), 4, 7);
        TimeZoneInfo.AdjustmentRule rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(new DateTime(2024, 1, 1), new DateTime(2027, 12, 31), TimeSpan.FromHours(1), spring, autumn);
        TimeZoneInfo weekdayDst = TimeZoneInfo.CreateCustomTimeZone("Test weekday DST", TimeSpan.FromHours(10), "Test", "Test Standard", "Test Daylight", new[] { rule });
        Equal(60L, WorkCountdownCalculator.Remaining(Work("01:30", "03:30"), At("2026-10-06T03:30:00+11:00"), At("2026-10-06T01:30:00+10:00"), CountdownUnit.Minutes, weekdayDst), "Working interval intersects actual spring-forward instants");
        Equal(30L, WorkCountdownCalculator.Remaining(Work("02:15", "03:30"), At("2026-10-06T03:30:00+11:00"), At("2026-10-06T01:30:00+10:00"), CountdownUnit.Minutes, weekdayDst), "Invalid scheduled start resolves forward on a working date");
        Equal(60L, WorkCountdownCalculator.Remaining(Work("02:15", "03:15"), At("2026-04-07T03:15:00+10:00"), At("2026-04-07T01:00:00+11:00"), CountdownUnit.Minutes, weekdayDst), "Ambiguous work start uses later occurrence on a working date");
    }

    private static void CompatibilityCases(string folder)
    {
        AppSettings original = new AppSettings();
        Equal(false, original.WorkdayCountdown, "New mode defaults off"); Equal("09:00", original.WorkStart, "Default start"); Equal("17:00", original.WorkFinish, "Default finish");
        Equal(0, (int)CountdownUnit.Days, "Days numeric value retained"); Equal(1, (int)CountdownUnit.Minutes, "Minutes retained"); Equal(2, (int)CountdownUnit.Seconds, "Seconds retained"); Equal(3, (int)CountdownUnit.Weeks, "Weeks retained"); Equal(4, (int)CountdownUnit.Hours, "Hours appended");
        AppSettings off = Work("invalid", "invalid"); off.WorkdayCountdown = false;
        DateTimeOffset now = At("2026-10-15T09:00:00+11:00"), target = At("2028-01-03T09:00:00+11:00");
        foreach (CountdownUnit unit in new[] { CountdownUnit.Days, CountdownUnit.Minutes, CountdownUnit.Seconds, CountdownUnit.Weeks, CountdownUnit.Hours })
            Equal(CountdownCalculator.Remaining(target, now, unit), WorkCountdownCalculator.Remaining(off, target, now, unit, Sydney), "Off preserves elapsed calculation and future dates in " + unit);
        Directory.CreateDirectory(folder);
        SettingsStore store = new SettingsStore(folder);
        File.WriteAllText(store.FilePath, "{\"HolidayName\":\"Old timer\",\"TargetLocal\":\"2027-12-31T17:00:00\",\"Unit\":3}");
        AppSettings old = store.Load(); Equal(false, old.WorkdayCountdown, "Old JSON stays elapsed mode"); Equal("09:00", old.WorkStart, "Old JSON gets start"); Equal("17:00", old.WorkFinish, "Old JSON gets finish"); Equal(CountdownUnit.Weeks, old.Unit, "Old unit preserved");
        old.WorkdayCountdown = true; old.WorkStart = "08:30"; old.WorkFinish = "14:30"; old.Unit = CountdownUnit.Hours; store.Save(old);
        AppSettings saved = store.Load(); Equal(true, saved.WorkdayCountdown, "Mode persists"); Equal("08:30", saved.WorkStart, "Start persists"); Equal("14:30", saved.WorkFinish, "Finish persists"); Equal(CountdownUnit.Hours, saved.Unit, "Hours persists");
        old.WorkFinish = old.WorkStart; Throws(delegate { store.Save(old); }, "Store rejects invalid enabled work hours");
        Equal("14:30", store.Load().WorkFinish, "Failed work schedule save leaves previous timer intact");
        saved.TargetLocal = "2032-06-01T09:00:00"; store.Save(saved);
        AppSettings beyondCalendar = store.Load();
        Equal("2032-06-01T09:00:00", beyondCalendar.TargetLocal, "Calendar expiry never discards saved target");
        Equal(true, beyondCalendar.WorkdayCountdown, "Calendar expiry never silently disables work mode");
        Equal(null, store.LoadWarning, "Valid saved configuration remains readable beyond calendar coverage");
        DateTimeOffset publicNow = CountdownCalculator.ToLocalTarget("2026-10-15T09:00:00");
        AppSettings publicSettings = Work(); publicSettings.TargetLocal = "2026-10-15T17:00:00"; publicSettings.Unit = CountdownUnit.Hours;
        Equal(8L, WorkCountdownCalculator.Remaining(publicSettings, publicNow), "Public API reads configured target/unit in computer-local time");
        Equal(1L, WorkCountdownCalculator.Remaining(publicSettings, publicNow, CountdownUnit.Days), "Public API optional unit override");
    }

    private static void CacheCases()
    {
        AppSettings settings = Work(); DateTimeOffset now = At("2026-01-01T00:00:00+11:00"), target = At("2026-12-31T17:00:00+11:00");
        Equal(252L, WorkCountdownCalculator.Remaining(settings, target, now, CountdownUnit.Days, Sydney), "2026 has 252 complete statewide work shifts");
        Equal(252L, WorkCountdownCalculator.Remaining(settings, At("2027-12-31T17:00:00+11:00"), At("2027-01-01T00:00:00+11:00"), CountdownUnit.Days, Sydney), "2027 has 252 complete statewide work shifts");
        int built = WorkCountdownCalculator.CacheBuildCount; Stopwatch watch = Stopwatch.StartNew();
        for (int i = 0; i < 10000; i++) WorkCountdownCalculator.Remaining(settings, target, now.AddSeconds(i), CountdownUnit.Seconds, Sydney);
        watch.Stop(); Equal(built, WorkCountdownCalculator.CacheBuildCount, "Repeated ticking reuses cached full-calendar intersections");
        Console.WriteLine("Cached 10000 tick calculations: " + watch.ElapsedMilliseconds + " ms; no per-tick calendar rebuild.");
        for (int i = 0; i < 25; i++)
        {
            AppSettings edited = Work("09:" + i.ToString("00"), "17:00");
            WorkCountdownCalculator.Remaining(edited, target, now, CountdownUnit.Hours, Sydney);
            True(WorkCountdownCalculator.CacheEntryCount <= 8, "Repeated valid schedule edits retain at most eight cache entries");
        }
    }

    private static void RefreshCacheCases()
    {
        PublicHolidaySnapshot original = NswPublicHolidays.Current;
        DateTime oldDate = new DateTime(2026, 10, 5), movedDate = new DateTime(2026, 10, 6);
        AppSettings settings = Work();
        DateTimeOffset oldStart = At("2026-10-05T09:00:00+11:00"), oldFinish = At("2026-10-05T17:00:00+11:00");
        Equal(0L, WorkCountdownCalculator.Remaining(settings, oldFinish, oldStart, CountdownUnit.Hours, Sydney), "Original Labour Day contributes no work hours");
        List<NswPublicHoliday> changed = new List<NswPublicHoliday>();
        foreach (NswPublicHoliday holiday in original.Entries)
            changed.Add(holiday.Date == oldDate ? new NswPublicHoliday(movedDate, holiday.Name, true) : holiday);
        try
        {
            NswPublicHolidays.ReplaceForTests(changed);
            True(NswPublicHolidays.Current.Revision != original.Revision, "Publishing a changed same-year holiday calendar changes its revision");
            Equal(false, NswWorkCalendar.IsPublicHoliday(oldDate), "Refresh can remove a previous holiday date");
            Equal(null, NswWorkCalendar.GetPublicHolidayName(oldDate), "Removed dates lose their calendar annotation too");
            Equal("Labour Day", NswWorkCalendar.GetPublicHolidayName(movedDate), "Moved date uses the canonical published name");
            int before = WorkCountdownCalculator.CacheBuildCount;
            Equal(8L, WorkCountdownCalculator.Remaining(settings, oldFinish, oldStart, CountdownUnit.Hours, Sydney), "Cached old holiday reopens as a workday after refresh");
            Equal(before + 1, WorkCountdownCalculator.CacheBuildCount, "A holiday revision invalidates the cached prefix without a time-zone or schedule change");
            Equal(0L, Count(settings, "2026-10-06T09:00:00+11:00", "2026-10-06T17:00:00+11:00", CountdownUnit.Hours), "New holiday excludes the moved date in the same cache");
            Equal(before + 1, WorkCountdownCalculator.CacheBuildCount, "Subsequent ticks reuse the refreshed prefix");
            True(original.Find(oldDate) != null && original.Find(movedDate) == null, "A previously acquired snapshot remains immutable across publication");
        }
        finally { NswPublicHolidays.RestoreForTests(original); }
        Equal(0L, WorkCountdownCalculator.Remaining(settings, oldFinish, oldStart, CountdownUnit.Hours, Sydney), "Restoring a snapshot invalidates the changed prefix too");
        Equal(8L, Count(settings, "2026-10-06T09:00:00+11:00", "2026-10-06T17:00:00+11:00", CountdownUnit.Hours), "Restored ordinary date counts normally");
        True(WorkCountdownCalculator.CacheEntryCount <= 8, "Holiday updates retain the bounded work-prefix cache");
    }

    private static void ManualCorrectionCases(string folder)
    {
        PublicHolidaySnapshot original = NswPublicHolidays.Current;
        NswPublicHoliday christmas = original.Find(new DateTime(2026, 12, 25));
        AppSettings settings = Work();
        Equal(8L, Count(settings, "2026-12-24T09:00:00+11:00", "2026-12-24T17:00:00+11:00", CountdownUnit.Hours), "Original Christmas Eve counts as a workday");
        Equal(0L, Count(settings, "2026-12-25T09:00:00+11:00", "2026-12-25T17:00:00+11:00", CountdownUnit.Hours), "Original Christmas is excluded");
        try
        {
            NswPublicHolidays.SaveCorrection(folder, christmas.Key, new DateTime(2026, 12, 24), "Adjusted Christmas");
            Equal(0L, Count(settings, "2026-12-24T09:00:00+11:00", "2026-12-24T17:00:00+11:00", CountdownUnit.Hours), "Corrected date is deducted from working time");
            Equal(8L, Count(settings, "2026-12-25T09:00:00+11:00", "2026-12-25T17:00:00+11:00", CountdownUnit.Hours), "Replaced date becomes a working day");
            Equal("Adjusted Christmas", NswWorkCalendar.GetPublicHolidayName(new DateTime(2026, 12, 24)), "Work calendar uses the corrected name");
            NswPublicHolidays.SaveCorrection(folder, christmas.Key, null, null);
            Equal(8L, Count(settings, "2026-12-24T09:00:00+11:00", "2026-12-24T17:00:00+11:00", CountdownUnit.Hours), "Reset restores the source working-day schedule");
        }
        finally { NswPublicHolidays.RestoreForTests(original); }
    }
}
