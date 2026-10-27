using System;
using System.Globalization;
using System.IO;
using HolidayTimer;

internal static class CoreTests
{
    private static int assertions;

    private static int Main()
    {
        string testDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
            "core-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            CountdownCases();
            WeekCountdownCases();
            MorningCases();
            ArrivalCases();
            PersistenceCases(testDirectory);
            UnitPersistenceCases(testDirectory);
            MusicPreferenceCases(testDirectory);
            ArrivalPersistenceCases(testDirectory);
            Console.WriteLine("PASS: " + assertions + " core acceptance checks.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("FAIL: " + exception);
            return 1;
        }
        finally
        {
            if (Directory.Exists(testDirectory))
            {
                Directory.Delete(testDirectory, true);
            }
        }
    }

    private static void CountdownCases()
    {
        DateTimeOffset now = new DateTimeOffset(2026, 10, 14, 9, 0, 0, TimeSpan.FromHours(11));
        Equal(2L, CountdownCalculator.Remaining(now.AddDays(1).AddSeconds(1), now, CountdownUnit.Days),
            "Days round up the remaining partial day");
        Equal(1L, CountdownCalculator.Remaining(now.AddDays(1), now, CountdownUnit.Days),
            "An exact day is one day");
        Equal(8L, CountdownCalculator.Remaining(now.AddHours(8), now, CountdownUnit.Hours),
            "An exact eight elapsed hours is eight hours");
        Equal(9L, CountdownCalculator.Remaining(now.AddHours(8).AddTicks(1), now, CountdownUnit.Hours),
            "Hours round up the remaining fraction");
        Equal(0L, CountdownCalculator.Remaining(now, now, CountdownUnit.Hours),
            "Hours reach zero at the actual target");
        Equal(2L, CountdownCalculator.Remaining(now.AddMinutes(1).AddTicks(1), now, CountdownUnit.Minutes),
            "Minutes round up the remaining fraction");
        Equal(61L, CountdownCalculator.Remaining(now.AddMinutes(1).AddTicks(1), now, CountdownUnit.Seconds),
            "Seconds round up the remaining fraction");
        Equal(0L, CountdownCalculator.Remaining(now.AddTicks(-1), now, CountdownUnit.Seconds),
            "A past holiday never counts below zero");
        Equal(0L, CountdownCalculator.Remaining(now, now, CountdownUnit.Days),
            "The target instant is zero");

        DateTimeOffset beforeClockChange = new DateTimeOffset(2026, 10, 3, 9, 0, 0, TimeSpan.FromHours(10));
        DateTimeOffset afterClockChange = new DateTimeOffset(2026, 10, 4, 9, 0, 0, TimeSpan.FromHours(11));
        Equal(1380L, CountdownCalculator.Remaining(afterClockChange, beforeClockChange, CountdownUnit.Minutes),
            "Elapsed countdown handles a spring daylight-saving change");
        DateTimeOffset beforeFallBack = new DateTimeOffset(2027, 4, 3, 9, 0, 0, TimeSpan.FromHours(11));
        DateTimeOffset afterFallBack = new DateTimeOffset(2027, 4, 4, 9, 0, 0, TimeSpan.FromHours(10));
        Equal(1500L, CountdownCalculator.Remaining(afterFallBack, beforeFallBack, CountdownUnit.Minutes),
            "Elapsed countdown handles an autumn daylight-saving change");
        Equal(DateTimeKind.Unspecified, CountdownCalculator.ParseTarget("2026-12-25T09:30:00").Kind,
            "Saved target is a local wall time");
        DateTime knownUtcInstant = new DateTime(2026, 10, 14, 1, 0, 0, DateTimeKind.Utc);
        Equal(new DateTimeOffset(knownUtcInstant), CountdownCalculator.ToLocalOffset(knownUtcInstant),
            "An existing UTC instant retains its offset");
        DateTime currentLocalInstant = DateTime.Now;
        Equal(new DateTimeOffset(currentLocalInstant), CountdownCalculator.ToLocalOffset(currentLocalInstant),
            "An existing local instant retains its offset");
        DateTime repeatedWallTime = new DateTime(2026, 4, 5, 2, 30, 0, DateTimeKind.Unspecified);
        if (TimeZoneInfo.Local.IsAmbiguousTime(repeatedWallTime))
        {
            TimeSpan[] repeatedOffsets = TimeZoneInfo.Local.GetAmbiguousTimeOffsets(repeatedWallTime);
            TimeSpan earlyOffset = repeatedOffsets[0] > repeatedOffsets[1] ? repeatedOffsets[0] : repeatedOffsets[1];
            TimeSpan lateOffset = repeatedOffsets[0] < repeatedOffsets[1] ? repeatedOffsets[0] : repeatedOffsets[1];
            DateTimeOffset firstOccurrence = new DateTimeOffset(repeatedWallTime, earlyOffset);
            DateTimeOffset secondOccurrence = new DateTimeOffset(repeatedWallTime, lateOffset);
            DateTime firstLocal = firstOccurrence.UtcDateTime.ToLocalTime();
            Equal(firstOccurrence, CountdownCalculator.ToLocalOffset(firstLocal),
                "A live local time preserves the first occurrence of a repeated clock hour");
            Equal(secondOccurrence, CountdownCalculator.ToLocalOffset(repeatedWallTime),
                "A chosen ambiguous target uses the later occurrence of the clock time");
        }
        Throws<FormatException>(delegate { CountdownCalculator.ParseTarget("25/12/2026"); },
            "Noncanonical dates are rejected");
        Throws<FormatException>(delegate { CountdownCalculator.ParseTarget("2026-02-30T09:00:00"); },
            "Invalid calendar dates are rejected");
        Throws<ArgumentOutOfRangeException>(delegate {
            CountdownCalculator.Remaining(now.AddDays(1), now, (CountdownUnit)100);
        }, "Unknown display units are rejected");
    }

    private static void WeekCountdownCases()
    {
        DateTimeOffset now = new DateTimeOffset(2026, 10, 15, 9, 0, 0, TimeSpan.FromHours(11));
        Equal(1L, CountdownCalculator.Remaining(now.AddTicks(1), now, CountdownUnit.Weeks),
            "A positive fraction of a week rounds up to one week");
        Equal(1L, CountdownCalculator.Remaining(now.AddDays(1), now, CountdownUnit.Weeks),
            "A remaining day is one partial week");
        Equal(1L, CountdownCalculator.Remaining(now.AddDays(7).AddTicks(-1), now, CountdownUnit.Weeks),
            "The instant just before seven elapsed days remains one week");
        Equal(1L, CountdownCalculator.Remaining(now.AddDays(7), now, CountdownUnit.Weeks),
            "Seven elapsed days are exactly one week");
        Equal(2L, CountdownCalculator.Remaining(now.AddDays(7).AddTicks(1), now, CountdownUnit.Weeks),
            "A fraction beyond seven elapsed days rounds up to two weeks");
        Equal(2L, CountdownCalculator.Remaining(now.AddDays(14), now, CountdownUnit.Weeks),
            "Fourteen elapsed days are exactly two weeks");
        Equal(3L, CountdownCalculator.Remaining(now.AddDays(14).AddHours(1), now, CountdownUnit.Weeks),
            "More than two elapsed weeks rounds up to three");
        Equal(0L, CountdownCalculator.Remaining(now, now, CountdownUnit.Weeks),
            "The target instant has no weeks remaining");
        Equal(0L, CountdownCalculator.Remaining(now.AddTicks(-1), now, CountdownUnit.Weeks),
            "A past holiday never has negative weeks remaining");

        DateTimeOffset beforeSpring = new DateTimeOffset(2026, 10, 3, 9, 0, 0, TimeSpan.FromHours(10));
        DateTimeOffset afterSpring = new DateTimeOffset(2026, 10, 10, 9, 0, 0, TimeSpan.FromHours(11));
        Equal(1L, CountdownCalculator.Remaining(afterSpring, beforeSpring, CountdownUnit.Weeks),
            "A spring clock change uses the actual shorter elapsed week");
        DateTimeOffset beforeAutumn = new DateTimeOffset(2027, 4, 3, 9, 0, 0, TimeSpan.FromHours(11));
        DateTimeOffset afterAutumn = new DateTimeOffset(2027, 4, 10, 9, 0, 0, TimeSpan.FromHours(10));
        Equal(2L, CountdownCalculator.Remaining(afterAutumn, beforeAutumn, CountdownUnit.Weeks),
            "An autumn clock change rounds the actual seven days plus one hour into two weeks");
        DateTime localNow = new DateTime(2026, 11, 10, 9, 0, 0, DateTimeKind.Unspecified);
        Equal(2L, CountdownCalculator.Remaining(localNow.AddDays(8), localNow, CountdownUnit.Weeks),
            "The local wall-time overload also supports weeks");
    }

    private static void UnitPersistenceCases(string testDirectory)
    {
        Equal(0, (int)CountdownUnit.Days, "Saved Days keeps its original numeric value");
        Equal(1, (int)CountdownUnit.Minutes, "Saved Minutes keeps its original numeric value");
        Equal(2, (int)CountdownUnit.Seconds, "Saved Seconds keeps its original numeric value");
        Equal(3, (int)CountdownUnit.Weeks, "Weeks is added after the three existing saved units");
        Equal(4, (int)CountdownUnit.Hours, "Hours is appended without changing old numeric values");
        SettingsStore store = new SettingsStore(Path.Combine(testDirectory, "unit-compatibility"));
        Directory.CreateDirectory(Path.GetDirectoryName(store.FilePath));
        CountdownUnit[] oldUnits = { CountdownUnit.Days, CountdownUnit.Minutes, CountdownUnit.Seconds };
        for (int i = 0; i < oldUnits.Length; i++)
        {
            string json = "{\"HolidayName\":\"Existing unit choice\",\"TargetLocal\":\"2030-12-25T09:00:00\",\"Unit\":" + i + ",\"IsConfigured\":true}";
            File.WriteAllText(store.FilePath, json);
            AppSettings older = store.Load();
            True(older.IsConfigured, "Existing " + oldUnits[i] + " JSON remains configured after the update");
            Equal(oldUnits[i], older.Unit, "Existing numeric JSON preserves " + oldUnits[i] + " instead of selecting Weeks");
            Equal(null, store.LoadWarning, "Existing " + oldUnits[i] + " JSON loads without a recovery warning");
            Equal(json, File.ReadAllText(store.FilePath), "Reading old " + oldUnits[i] + " settings does not rewrite them");
        }
        AppSettings weeks = store.Load(); weeks.Unit = CountdownUnit.Weeks;
        store.Save(weeks);
        Equal(CountdownUnit.Weeks, store.Load().Unit, "The fourth countdown unit survives save and restart");
        True(File.ReadAllText(store.FilePath).Contains("\"Unit\":3"), "Weeks is persisted using its new numeric value three");
    }

    private static void MorningCases()
    {
        AppSettings settings = new AppSettings();
        settings.IsConfigured = true;
        settings.TargetLocal = "2026-12-25T09:00:00";
        DateTime morning = new DateTime(2026, 10, 14, 8, 0, 0);
        True(MorningPolicy.ShouldShow(settings, morning), "First login in the morning can greet");
        False(MorningPolicy.ShouldShow(settings, morning.Date.AddHours(4).AddMinutes(59)),
            "Before 05:00 is outside the morning interval");
        True(MorningPolicy.ShouldShow(settings, morning.Date.AddHours(5)),
            "05:00 starts the morning interval");
        True(MorningPolicy.ShouldShow(settings, morning.Date.AddHours(11).AddMinutes(59)),
            "11:59 is still in the morning interval");
        False(MorningPolicy.ShouldShow(settings, morning.Date.AddHours(12)),
            "12:00 ends the morning interval");
        settings.LastGreetingDate = "2026-10-14";
        False(MorningPolicy.ShouldShow(settings, morning), "Repeated login on the same day does not greet again");
        True(MorningPolicy.ShouldShow(settings, morning.AddDays(1)), "Next morning can greet again");
        settings.MorningGreeting = false;
        False(MorningPolicy.ShouldShow(settings, morning.AddDays(1)), "The morning switch disables greetings");
        settings.MorningGreeting = true;
        settings.IsConfigured = false;
        False(MorningPolicy.ShouldShow(settings, morning.AddDays(1)), "An unconfigured timer does not greet");
        settings.IsConfigured = true;
        settings.LastGreetingDate = null;
        settings.TargetLocal = "2026-10-14T07:00:00";
        False(MorningPolicy.ShouldShow(settings, morning), "A finished holiday does not receive a remaining-days greeting");
        settings.TargetLocal = "invalid";
        False(MorningPolicy.ShouldShow(settings, morning), "Invalid settings do not cause a greeting failure");
        False(MorningPolicy.ShouldShow(null, morning), "No settings means no greeting");
    }

    private static void ArrivalCases()
    {
        AppSettings settings = new AppSettings {
            IsConfigured = true,
            TargetLocal = "2026-10-16T20:00:00",
            MorningGreeting = false,
            LastGreetingDate = "2026-10-16"
        };
        DateTimeOffset target = CountdownCalculator.ToLocalTarget(settings.TargetLocal);
        Equal(null, settings.LastArrivalTarget, "A new timer has no arrival marker");
        foreach (CountdownUnit unit in Enum.GetValues(typeof(CountdownUnit)))
        {
            settings.Unit = unit;
            False(ArrivalPolicy.ShouldShow(settings, target.AddTicks(-1)), unit + " does not celebrate before the actual target");
            True(ArrivalPolicy.ShouldShow(settings, target), unit + " celebrates at the exact target without the morning switch");
            True(ArrivalPolicy.ShouldShow(settings, target.AddTicks(1)), unit + " catches up after the target outside morning hours");
        }
        Equal(null, settings.LastArrivalTarget, "Checking eligibility does not consume the arrival marker");
        True(ArrivalPolicy.ShouldShow(settings, target.ToUniversalTime()), "Arrival compares the target instant with a UTC timestamp");
        True(ArrivalPolicy.ShouldShow(settings, target.ToOffset(TimeSpan.FromHours(-7))), "The same instant with a different offset still arrives");
        False(ArrivalPolicy.ShouldShow(settings, target.AddTicks(-1).ToOffset(TimeSpan.FromHours(13))), "A different clock offset cannot celebrate a tick early");

        settings.LastArrivalTarget = settings.TargetLocal;
        False(ArrivalPolicy.ShouldShow(settings, target), "The exact arrival marker prevents a duplicate at zero");
        False(ArrivalPolicy.ShouldShow(settings, target.AddDays(30)), "The marker prevents a later catch-up duplicate");
        settings.HolidayName = "Renamed celebration";
        settings.Theme = HolidayTheme.Festive;
        settings.MorningGreeting = true;
        settings.LastGreetingDate = null;
        settings.Unit = CountdownUnit.Weeks;
        False(ArrivalPolicy.ShouldShow(settings, target.AddDays(1)), "Name, style, daily greeting, and unit edits do not reset arrival deduplication");

        settings.TargetLocal = "2026-10-17T20:00:00";
        DateTimeOffset retargeted = CountdownCalculator.ToLocalTarget(settings.TargetLocal);
        False(ArrivalPolicy.ShouldShow(settings, target), "A different future target does not celebrate immediately");
        True(ArrivalPolicy.ShouldShow(settings, retargeted), "A different target receives its own arrival celebration");
        settings.LastArrivalTarget = settings.TargetLocal;
        False(ArrivalPolicy.ShouldShow(settings, retargeted.AddHours(1)), "The new target is deduplicated after marking");
        settings.TargetLocal = "2026-10-15T20:00:00";
        True(ArrivalPolicy.ShouldShow(settings, retargeted), "Retargeting to an unmarked due target catches up once");
        settings.LastArrivalTarget = settings.TargetLocal.ToLowerInvariant();
        True(ArrivalPolicy.ShouldShow(settings, retargeted), "Arrival markers use ordinal opaque-token comparison");

        settings.IsConfigured = false;
        False(ArrivalPolicy.ShouldShow(settings, retargeted), "An unconfigured timer never celebrates arrival");
        settings.AutomaticPublicHoliday = true;
        True(ArrivalPolicy.ShouldShow(settings, retargeted), "An automatic first-run public holiday can celebrate without saved setup");
        settings.AutomaticPublicHoliday = false;
        False(ArrivalPolicy.ShouldShow(null, retargeted), "Missing settings never celebrate arrival");
        settings.IsConfigured = true;
        settings.LastArrivalTarget = null;
        foreach (string invalidTarget in new string[] { null, "", "not-a-date", "2026-02-30T20:00:00" })
        {
            settings.TargetLocal = invalidTarget;
            False(ArrivalPolicy.ShouldShow(settings, retargeted), "An invalid target cannot trigger an arrival celebration");
        }
        DateTime skippedWallTime = new DateTime(2026, 10, 4, 2, 30, 0);
        if (TimeZoneInfo.Local.IsInvalidTime(skippedWallTime))
        {
            settings.TargetLocal = skippedWallTime.ToString(CountdownCalculator.TargetFormat, CultureInfo.InvariantCulture);
            False(ArrivalPolicy.ShouldShow(settings, retargeted), "A daylight-saving skipped target cannot trigger arrival");
        }

        settings.TargetLocal = "2026-10-19T09:00:00";
        settings.WorkdayCountdown = true;
        settings.WorkStart = "09:00";
        settings.WorkFinish = "17:00";
        DateTimeOffset afterFridayWork = CountdownCalculator.ToLocalTarget("2026-10-16T18:00:00");
        foreach (CountdownUnit unit in Enum.GetValues(typeof(CountdownUnit)))
        {
            settings.Unit = unit;
            Equal(0L, WorkCountdownCalculator.Remaining(settings, afterFridayWork), unit + " can display zero work time before a Monday morning target");
            False(ArrivalPolicy.ShouldShow(settings, afterFridayWork), unit + " work-time zero does not trigger an early celebration");
            True(ArrivalPolicy.ShouldShow(settings, CountdownCalculator.ToLocalTarget(settings.TargetLocal)), unit + " work-time mode still celebrates at the real target");
        }
    }

    private static void ArrivalPersistenceCases(string testDirectory)
    {
        SettingsStore store = new SettingsStore(Path.Combine(testDirectory, "arrival-compatibility"));
        Directory.CreateDirectory(Path.GetDirectoryName(store.FilePath));
        string legacyJson = "{\"HolidayName\":\"Existing due holiday\",\"TargetLocal\":\"2026-10-16T20:00:00\",\"MorningGreeting\":false,\"IsConfigured\":true}";
        File.WriteAllText(store.FilePath, legacyJson);
        AppSettings legacy = store.Load();
        DateTimeOffset afterArrival = CountdownCalculator.ToLocalTarget(legacy.TargetLocal).AddHours(1);
        True(legacy.IsConfigured, "Legacy settings remain configured without an arrival marker");
        Equal(null, legacy.LastArrivalTarget, "Legacy settings receive a null arrival marker");
        Equal(null, store.LoadWarning, "The optional legacy arrival marker causes no recovery warning");
        Equal(legacyJson, File.ReadAllText(store.FilePath), "Loading legacy arrival settings does not rewrite them");
        True(ArrivalPolicy.ShouldShow(legacy, afterArrival), "A legacy due target can catch up once after upgrade");
        legacy.LastArrivalTarget = legacy.TargetLocal;
        store.Save(legacy);
        AppSettings restarted = store.Load();
        Equal(legacy.TargetLocal, restarted.LastArrivalTarget, "The actual arrival target marker survives restart");
        False(ArrivalPolicy.ShouldShow(restarted, afterArrival), "Restart does not repeat a marked arrival celebration");
        restarted.HolidayName = "Different metadata";
        restarted.Unit = CountdownUnit.Hours;
        restarted.BackgroundOpacity = .6;
        store.Save(restarted);
        False(ArrivalPolicy.ShouldShow(store.Load(), afterArrival), "Saving metadata changes preserves arrival deduplication");
        restarted.LastArrivalTarget = "opaque marker \u00b7 not a date\r\nretained verbatim";
        store.Save(restarted);
        AppSettings opaque = store.Load();
        Equal(restarted.LastArrivalTarget, opaque.LastArrivalTarget, "An arbitrary opaque arrival token survives persistence without parsing");
        Equal(null, store.LoadWarning, "An opaque arrival token does not invalidate otherwise valid settings");
        True(ArrivalPolicy.ShouldShow(opaque, afterArrival), "A different opaque token does not suppress an unmarked target");
        opaque.LastArrivalTarget = null;
        store.Save(opaque);
        Equal(null, store.Load().LastArrivalTarget, "An explicit null arrival marker survives persistence");
    }

    private static void PersistenceCases(string testDirectory)
    {
        SettingsStore store = new SettingsStore(testDirectory);
        AppSettings first = store.Load();
        False(first.IsConfigured, "An absent settings file starts with setup");
        True(first.ShowFloating && first.AlwaysOnTop && first.StartAtLogin && first.MorningGreeting,
            "Defaults enable the requested gadget modes");
        Equal(HolidayTheme.Beach, first.Theme, "A new timer defaults to the beach theme");
        False(first.LightAppearance, "A new timer keeps the dark appearance default");
        Equal(0.82, first.BackgroundOpacity, "A new timer defaults to a translucent background");
        Equal("Inter Display", first.DisplayFont, "A new timer defaults to Inter Display");
        Equal(1.0, first.WidgetScale, "A new timer defaults to its original gadget size");
        Equal(null, store.LoadWarning, "An absent file is not a corruption warning");

        AppSettings settings = new AppSettings();
        settings.HolidayName = "Mum's holiday ✈ 日本";
        settings.TargetLocal = "2026-12-25T09:30:00";
        settings.Unit = CountdownUnit.Seconds;
        settings.ShowFloating = false;
        settings.AlwaysOnTop = false;
        settings.StartAtLogin = false;
        settings.MorningGreeting = false;
        settings.WidgetLeft = -123.5;
        settings.WidgetTop = 420.25;
        settings.LastGreetingDate = "2026-10-14";
        settings.IsConfigured = true;
        settings.Theme = HolidayTheme.Sunset;
        settings.LightAppearance = true;
        settings.BackgroundOpacity = 0.61;
        settings.DisplayFont = "Segoe UI Variable Display";
        settings.WidgetScale = 1.25;
        store.Save(settings);
        AppSettings loaded = store.Load();
        Equal(settings.HolidayName, loaded.HolidayName, "Holiday names preserve Unicode and punctuation");
        Equal(settings.TargetLocal, loaded.TargetLocal, "The chosen local time survives restart");
        Equal(CountdownUnit.Seconds, loaded.Unit, "Chosen countdown unit survives restart");
        False(loaded.ShowFloating || loaded.AlwaysOnTop || loaded.StartAtLogin || loaded.MorningGreeting,
            "Disabled switches survive restart");
        Equal(settings.WidgetLeft, loaded.WidgetLeft, "Negative monitor coordinates survive restart");
        Equal(settings.WidgetTop, loaded.WidgetTop, "Floating location survives restart");
        Equal(settings.LastGreetingDate, loaded.LastGreetingDate, "Once-daily greeting state survives restart");
        True(loaded.IsConfigured, "Completed setup survives restart");
        Equal(HolidayTheme.Sunset, loaded.Theme, "Selected theme survives restart");
        True(loaded.LightAppearance, "Light appearance survives restart independently of the selected palette");
        Equal(0.61, loaded.BackgroundOpacity, "Selected background opacity survives restart");
        Equal("Segoe UI Variable Display", loaded.DisplayFont, "The selected display font survives restart");
        Equal(1.25, loaded.WidgetScale, "The selected gadget size survives restart");

        settings.HolidayName = "Updated holiday";
        settings.LightAppearance = false;
        store.Save(settings);
        Equal("Updated holiday", store.Load().HolidayName, "Updating a saved timer replaces it successfully");
        False(store.Load().LightAppearance, "Changing back to dark appearance survives restart");
        True(File.Exists(store.FilePath + ".bak"), "Replacing settings preserves the previous file as backup");
        string beforeInvalidSave = File.ReadAllText(store.FilePath);
        settings.TargetLocal = "bad date";
        Throws<FormatException>(delegate { store.Save(settings); }, "Invalid settings cannot overwrite saved settings");
        Equal(beforeInvalidSave, File.ReadAllText(store.FilePath), "A failed validation preserves the previous settings");
        settings.TargetLocal = "2026-12-25T09:30:00";
        settings.Unit = (CountdownUnit)99;
        Throws<ArgumentException>(delegate { store.Save(settings); }, "Unknown units cannot overwrite saved settings");
        settings.Unit = CountdownUnit.Seconds;
        settings.WidgetLeft = Double.NaN;
        Throws<ArgumentException>(delegate { store.Save(settings); }, "NaN positions cannot overwrite saved settings");
        settings.WidgetLeft = -123.5;
        settings.WidgetTop = Double.PositiveInfinity;
        Throws<ArgumentException>(delegate { store.Save(settings); }, "Infinite positions cannot overwrite saved settings");
        settings.WidgetTop = 420.25;
        settings.HolidayName = new String('\u65e5', 101);
        Throws<ArgumentException>(delegate { store.Save(settings); }, "Overlong Unicode names cannot overwrite saved settings");
        settings.HolidayName = "Updated holiday";
        settings.Theme = (HolidayTheme)99;
        Throws<ArgumentException>(delegate { store.Save(settings); }, "Unknown themes cannot overwrite saved settings");
        settings.Theme = HolidayTheme.Sunset;
        foreach (double invalidOpacity in new double[] { 0.3499, 1.0001, Double.NaN, Double.PositiveInfinity, Double.NegativeInfinity })
        {
            settings.BackgroundOpacity = invalidOpacity;
            Throws<ArgumentException>(delegate { store.Save(settings); }, "Invalid background opacity cannot overwrite saved settings");
        }
        settings.BackgroundOpacity = 0.61;
        foreach (string invalidFont in new string[] { null, "", "   ", new String('W', 121) })
        {
            settings.DisplayFont = invalidFont;
            Throws<ArgumentException>(delegate { store.Save(settings); }, "Invalid font names cannot overwrite saved settings");
        }
        settings.DisplayFont = "Segoe UI Variable Display";
        foreach (double invalidScale in new double[] { 0.5999, 1.8001, Double.NaN, Double.PositiveInfinity, Double.NegativeInfinity })
        {
            settings.WidgetScale = invalidScale;
            Throws<ArgumentException>(delegate { store.Save(settings); }, "Invalid gadget sizes cannot overwrite saved settings");
        }
        settings.WidgetScale = 1.25;
        Equal(beforeInvalidSave, File.ReadAllText(store.FilePath), "All failed validations preserve the previous settings");
        using (FileStream lockedFile = new FileStream(store.FilePath, FileMode.Open,
            FileAccess.ReadWrite, FileShare.None))
        {
            settings.HolidayName = "Cannot save while locked";
            Throws<IOException>(delegate { store.Save(settings); }, "A locked settings file causes a safe save failure");
        }
        Equal(beforeInvalidSave, File.ReadAllText(store.FilePath), "An I/O failure preserves the previous settings");
        Equal(0, Directory.GetFiles(testDirectory, "*.tmp").Length, "A failed replacement removes its temporary file");
        settings.HolidayName = new String('\u65e5', 100);
        store.Save(settings);
        Equal(settings.HolidayName, store.Load().HolidayName, "A 100-character Unicode name survives restart");
        settings.Theme = HolidayTheme.Festive;
        settings.BackgroundOpacity = 0.35;
        settings.WidgetScale = 0.60;
        store.Save(settings);
        Equal(HolidayTheme.Festive, store.Load().Theme, "Festive theme survives restart");
        Equal(0.35, store.Load().BackgroundOpacity, "Minimum allowed opacity survives restart");
        Equal(0.60, store.Load().WidgetScale, "Minimum allowed gadget size survives restart");
        settings.BackgroundOpacity = 1;
        settings.WidgetScale = 1.80;
        store.Save(settings);
        Equal(1.0, store.Load().BackgroundOpacity, "Maximum allowed opacity survives restart");
        Equal(1.80, store.Load().WidgetScale, "Maximum allowed gadget size survives restart");

        File.WriteAllText(store.FilePath, "{this is broken");
        AppSettings recovered = store.Load();
        False(recovered.IsConfigured, "Corrupt settings recover to setup defaults");
        False(recovered.LightAppearance, "Corrupt settings recover to dark appearance");
        True(!String.IsNullOrEmpty(store.LoadWarning), "Corruption is reported to the application");
        Equal("{this is broken", File.ReadAllText(store.FilePath), "Loading corruption does not overwrite the source file");

        File.WriteAllText(store.FilePath, "{\"HolidayName\":\"Older holiday\",\"TargetLocal\":\"2026-12-25T09:00:00\",\"IsConfigured\":true}");
        AppSettings older = store.Load();
        Equal("Older holiday", older.HolidayName, "Older settings retain the saved name");
        True(older.ShowFloating && older.AlwaysOnTop && older.StartAtLogin && older.MorningGreeting,
            "Missing fields in older settings receive safe defaults");
        Equal(HolidayTheme.Beach, older.Theme, "Older settings receive the beach theme");
        False(older.LightAppearance, "Older settings preserve dark appearance when the new field is absent");
        Equal(0.82, older.BackgroundOpacity, "Older settings receive the translucent opacity default");
        Equal("Inter Display", older.DisplayFont, "Older settings receive Inter Display as the display font");
        Equal(1.0, older.WidgetScale, "Older settings receive the original gadget size");
        Equal(null, store.LoadWarning, "Successful recovery clears an earlier warning");
        Equal(0, (int)HolidayTheme.Beach, "The saved Beach palette retains its enum value");
        Equal(1, (int)HolidayTheme.Sunset, "The saved Sunset palette retains its enum value");
        Equal(2, (int)HolidayTheme.Festive, "The saved Festive palette retains its enum value");
        foreach (HolidayTheme theme in Enum.GetValues(typeof(HolidayTheme)))
        {
            File.WriteAllText(store.FilePath, "{\"HolidayName\":\"Existing palette\",\"TargetLocal\":\"2026-12-25T09:00:00\",\"Theme\":" + (int)theme + ",\"IsConfigured\":true}");
            AppSettings legacyPalette = store.Load();
            Equal(theme, legacyPalette.Theme, "Existing " + theme + " palette remains compatible with the new appearance field");
            False(legacyPalette.LightAppearance, "Existing " + theme + " settings keep dark appearance");
            Equal(null, store.LoadWarning, "Existing " + theme + " settings load without a warning");
            legacyPalette.LightAppearance = true;
            store.Save(legacyPalette);
            AppSettings upgradedPalette = store.Load();
            True(upgradedPalette.LightAppearance, theme + " can save and reload light appearance");
            Equal(theme, upgradedPalette.Theme, "Light appearance preserves the " + theme + " palette");
        }
        File.WriteAllText(store.FilePath, "{\"HolidayName\":\"Bad unit\",\"TargetLocal\":\"2026-12-25T09:00:00\",\"Unit\":100,\"IsConfigured\":true}");
        False(store.Load().IsConfigured, "Unknown persisted enum values recover to setup defaults");
        True(!String.IsNullOrEmpty(store.LoadWarning), "Unknown persisted enum values report a warning");
        File.WriteAllText(store.FilePath, "{\"HolidayName\":\"Bad position\",\"TargetLocal\":\"2026-12-25T09:00:00\",\"WidgetLeft\":1e309,\"IsConfigured\":true}");
        False(store.Load().IsConfigured, "Nonfinite persisted coordinates recover to setup defaults");
        True(!String.IsNullOrEmpty(store.LoadWarning), "Nonfinite persisted coordinates report a warning");
        File.WriteAllText(store.FilePath, "null");
        False(store.Load().IsConfigured, "A null JSON document recovers to setup defaults");
        True(!String.IsNullOrEmpty(store.LoadWarning), "A null JSON document reports a warning");
        File.WriteAllText(store.FilePath, "{\"HolidayName\":\"Bad theme\",\"TargetLocal\":\"2026-12-25T09:00:00\",\"Theme\":999,\"IsConfigured\":true}");
        False(store.Load().IsConfigured, "An unknown persisted theme recovers to setup defaults");
        True(!String.IsNullOrEmpty(store.LoadWarning), "An unknown persisted theme reports a warning");
        File.WriteAllText(store.FilePath, "{\"HolidayName\":\"Bad opacity\",\"TargetLocal\":\"2026-12-25T09:00:00\",\"BackgroundOpacity\":0.1,\"IsConfigured\":true}");
        False(store.Load().IsConfigured, "An invalid persisted opacity recovers to setup defaults");
        True(!String.IsNullOrEmpty(store.LoadWarning), "An invalid persisted opacity reports a warning");
        File.WriteAllText(store.FilePath, "{\"HolidayName\":\"Bad font\",\"TargetLocal\":\"2026-12-25T09:00:00\",\"DisplayFont\":null,\"IsConfigured\":true}");
        False(store.Load().IsConfigured, "A null persisted font recovers to setup defaults");
        True(!String.IsNullOrEmpty(store.LoadWarning), "A null persisted font reports a warning");
        File.WriteAllText(store.FilePath, "{\"HolidayName\":\"Bad size\",\"TargetLocal\":\"2026-12-25T09:00:00\",\"WidgetScale\":null,\"IsConfigured\":true}");
        False(store.Load().IsConfigured, "A null persisted gadget size recovers to setup defaults");
        True(!String.IsNullOrEmpty(store.LoadWarning), "A null persisted gadget size reports a warning");
        Equal(0, Directory.GetFiles(testDirectory, "*.tmp").Length, "Atomic saves leave no temporary files");
    }

    private static void MusicPreferenceCases(string testDirectory)
    {
        AppSettings defaults = new AppSettings();
        False(defaults.MusicResponse, "Music capture is opt-in for new users");
        Equal("", defaults.AudioDeviceId, "Default music source follows the system output");
        Equal(.35, defaults.MusicIntensity, "Music starts at a restrained intensity");
        SettingsStore store = new SettingsStore(testDirectory);
        File.WriteAllText(store.FilePath, "{\"HolidayName\":\"Existing countdown\",\"TargetLocal\":\"2026-12-25T09:00:00\",\"IsConfigured\":true}");
        AppSettings legacy = store.Load();
        False(legacy.MusicResponse, "An update does not silently start listening to older users' playback");
        Equal("", legacy.AudioDeviceId, "Older preferences get the default output source");
        Equal(.35, legacy.MusicIntensity, "Older preferences get the restrained music intensity");
        legacy.MusicResponse = true; legacy.AudioDeviceId = "{0.0.0.00000000}.chosen-output"; legacy.MusicIntensity = .8;
        store.Save(legacy);
        AppSettings roundTrip = store.Load();
        True(roundTrip.MusicResponse, "Music preference survives restart");
        Equal(legacy.AudioDeviceId, roundTrip.AudioDeviceId, "Chosen output survives restart even if unplugged");
        Equal(.8, roundTrip.MusicIntensity, "Music intensity survives restart");
        string saved = File.ReadAllText(store.FilePath);
        foreach (double invalid in new double[] { -.001, 1.001, Double.NaN, Double.PositiveInfinity })
        {
            legacy.MusicIntensity = invalid;
            Throws<ArgumentException>(delegate { store.Save(legacy); }, "Invalid music intensity cannot overwrite preferences");
        }
        Equal(saved, File.ReadAllText(store.FilePath), "Failed music validation retains the saved countdown");
        legacy.MusicIntensity = 0; store.Save(legacy);
        Equal(0.0, store.Load().MusicIntensity, "Subtle endpoint remains a valid saved intensity");
        legacy.MusicIntensity = 1; store.Save(legacy);
        Equal(1.0, store.Load().MusicIntensity, "Energetic endpoint remains a valid saved intensity");
        legacy.AudioDeviceId = null; store.Save(legacy);
        Equal("", store.Load().AudioDeviceId, "Null source normalizes to the default output");
        legacy.AudioDeviceId = new String('X', 513);
        Throws<ArgumentException>(delegate { store.Save(legacy); }, "Invalid output identifiers cannot overwrite preferences");
    }

    private static void True(bool actual, string message)
    {
        assertions++;
        if (!actual) throw new Exception(message);
    }

    private static void False(bool actual, string message)
    {
        True(!actual, message);
    }

    private static void Equal<T>(T expected, T actual, string message)
    {
        assertions++;
        if (!Object.Equals(expected, actual))
            throw new Exception(message + "; expected " + expected + ", got " + actual);
    }

    private static void Throws<T>(Action action, string message) where T : Exception
    {
        assertions++;
        try { action(); }
        catch (T) { return; }
        throw new Exception(message);
    }
}
