using System;
using System.IO;
using HolidayTimer;

internal static class EffectSettingsTests
{
    private static int checks;

    private static int Main()
    {
        string scratch = Path.Combine(Path.GetTempPath(), "HolidayTimer-EffectSettings-" + Guid.NewGuid().ToString("N"));
        try
        {
            DefaultsAndMigration(scratch);
            TextColourCases();
            RoundTripAndValidation(scratch);
            CelebrationChoices(scratch);
            CompanionChoices(scratch);
            Console.WriteLine("PASS: " + checks + " effect settings acceptance checks.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("FAIL: " + exception);
            return 1;
        }
        finally
        {
            string resolved = Path.GetFullPath(scratch);
            string temporaryRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (resolved.StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase) &&
                Path.GetFileName(resolved).StartsWith("HolidayTimer-EffectSettings-", StringComparison.Ordinal) && Directory.Exists(resolved))
                Directory.Delete(resolved, true);
        }
    }

    private static void DefaultsAndMigration(string scratch)
    {
        AppSettings fresh = new SettingsStore(Path.Combine(scratch, "fresh")).Load();
        Equal(OrbEffect.Glass, fresh.Effect, "New users retain the existing glass orb");
        Equal(.65, fresh.EffectIntensity, "New effects start at a moderate motion intensity");
        Equal("", fresh.CountdownTextColour, "New countdown text uses the appearance default");
        Equal(CountdownUnit.Days, fresh.Unit, "Adding weeks does not change the initial countdown unit");
        False(fresh.MusicResponse, "Choosing an effect does not opt new users into playback capture");
        Equal(CelebrationKind.Fireworks, fresh.CelebrationEffect, "Existing celebrations retain fireworks by default");
        True(fresh.AlwaysAnimateCelebrations, "New users see celebration effects without enabling the override manually");
        Equal(CompanionKind.None, fresh.Companion, "The new companion is optional and defaults to none");
        True(fresh.CompanionScenery, "Companion scenery is enabled by default when a companion is chosen");

        SettingsStore legacyStore = new SettingsStore(Path.Combine(scratch, "legacy"));
        Directory.CreateDirectory(Path.GetDirectoryName(legacyStore.FilePath));
        string legacyJson = "{\"HolidayName\":\"Existing holiday\",\"TargetLocal\":\"2030-12-25T09:00:00\",\"IsConfigured\":true," +
            "\"Theme\":1,\"LightAppearance\":true,\"BackgroundOpacity\":0.55,\"Unit\":2," +
            "\"MusicResponse\":true,\"AudioDeviceId\":\"saved-output\",\"MusicIntensity\":0.8}";
        File.WriteAllText(legacyStore.FilePath, legacyJson);
        AppSettings legacy = legacyStore.Load();
        Equal(OrbEffect.Glass, legacy.Effect, "Older settings keep glass when the effect field is absent");
        True(legacy.AlwaysAnimateCelebrations, "Older settings without a saved animation choice receive the enabled default");
        Equal(CompanionKind.None, legacy.Companion, "Older preferences do not add a companion unexpectedly");
        True(legacy.CompanionScenery, "Older preferences receive the scenery default");
        Equal(.65, legacy.EffectIntensity, "Older settings receive the motion default rather than zero");
        Equal("", legacy.CountdownTextColour, "Older settings keep automatic text colour");
        Equal("Existing holiday", legacy.HolidayName, "Migration preserves the configured holiday");
        Equal(CountdownUnit.Seconds, legacy.Unit, "Migration preserves the selected timer unit");
        Equal(HolidayTheme.Sunset, legacy.Theme, "Migration preserves the palette");
        True(legacy.LightAppearance && legacy.MusicResponse, "Migration preserves independent appearance and music switches");
        Equal(.55, legacy.BackgroundOpacity, "Migration preserves the background opacity");
        Equal("saved-output", legacy.AudioDeviceId, "Migration preserves the chosen output");
        Equal(.8, legacy.MusicIntensity, "Migration preserves the audio response intensity");
        Equal(null, legacyStore.LoadWarning, "A valid older settings file does not produce a warning");
        Equal(legacyJson, File.ReadAllText(legacyStore.FilePath), "Reading older settings does not rewrite them");
    }

    private static void TextColourCases()
    {
        foreach (string automatic in new string[] { null, "" })
        {
            True(AppSettings.IsValidTextColour(automatic), "Unset text colour remains a valid default");
            Equal("", AppSettings.NormalizeTextColour(automatic), "Unset text colour normalizes to automatic");
        }
        foreach (string valid in new string[] { "#000000", "#FFFFFF", "#a4D5fF", "#012abc" })
        {
            True(AppSettings.IsValidTextColour(valid), "Exact six-digit RGB accepts " + valid);
            Equal(valid.ToUpperInvariant(), AppSettings.NormalizeTextColour(valid), "RGB normalizes consistently for " + valid);
        }
        foreach (string invalid in InvalidColours())
        {
            False(AppSettings.IsValidTextColour(invalid), "Non-RGB input is rejected: " + invalid);
            Equal("", AppSettings.NormalizeTextColour(invalid), "Invalid preview colour falls back to automatic: " + invalid);
        }
    }

    private static void RoundTripAndValidation(string scratch)
    {
        SettingsStore store = new SettingsStore(Path.Combine(scratch, "roundtrip"));
        foreach (OrbEffect effect in Enum.GetValues(typeof(OrbEffect)))
        {
            AppSettings settings = ValidSettings();
            settings.Unit = CountdownUnit.Weeks;
            settings.Effect = effect;
            settings.EffectIntensity = ((int)effect % 3) == 0 ? 0 : ((int)effect % 3) == 1 ? 1 : .65;
            settings.CountdownTextColour = ((int)effect % 2) == 0 ? "" : "#a4d5ff";
            store.Save(settings);
            AppSettings loaded = store.Load();
            Equal(effect, loaded.Effect, "Selected " + effect + " survives restart");
            Equal(CountdownUnit.Weeks, loaded.Unit, effect + " preserves the fourth countdown unit alongside its style settings");
            Equal(settings.EffectIntensity, loaded.EffectIntensity, effect + " intensity survives restart, including endpoints");
            Equal(AppSettings.NormalizeTextColour(settings.CountdownTextColour), loaded.CountdownTextColour,
                effect + " preserves automatic or canonical custom text colour");
            Equal(null, store.LoadWarning, effect + " settings load without a warning");
        }

        string saved = File.ReadAllText(store.FilePath);
        string backup = File.ReadAllText(store.FilePath + ".bak");
        foreach (int invalid in new int[] { -1, 13, Int32.MaxValue })
        {
            AppSettings settings = ValidSettings(); settings.Effect = (OrbEffect)invalid;
            RejectWithoutOverwrite(store, settings, saved, backup, "Unknown effect " + invalid);
        }
        foreach (double invalid in new double[] { -.0001, 1.0001, Double.NaN, Double.PositiveInfinity, Double.NegativeInfinity })
        {
            AppSettings settings = ValidSettings(); settings.EffectIntensity = invalid;
            RejectWithoutOverwrite(store, settings, saved, backup, "Out-of-range or nonfinite motion intensity");
        }
        foreach (string invalid in InvalidColours())
        {
            AppSettings settings = ValidSettings(); settings.CountdownTextColour = invalid;
            RejectWithoutOverwrite(store, settings, saved, backup, "Invalid custom text colour " + invalid);
        }
        Equal(0, Directory.GetFiles(Path.GetDirectoryName(store.FilePath), "*.tmp").Length,
            "Rejected effect settings leave no pending replacement files");

        AppSettings automatic = ValidSettings(); automatic.CountdownTextColour = null; store.Save(automatic);
        Equal("", store.Load().CountdownTextColour, "A persisted null colour becomes the automatic default");
        SettingsStore invalidStore = new SettingsStore(Path.Combine(scratch, "invalid-load"));
        Directory.CreateDirectory(Path.GetDirectoryName(invalidStore.FilePath));
        foreach (string member in new string[] { "\"Effect\":13", "\"EffectIntensity\":-0.1", "\"EffectIntensity\":1.1", "\"CountdownTextColour\":\"#12345G\"" })
        {
            string json = "{\"HolidayName\":\"Invalid saved style\",\"TargetLocal\":\"2030-12-25T09:00:00\",\"IsConfigured\":true," + member + "}";
            File.WriteAllText(invalidStore.FilePath, json);
            AppSettings recovered = invalidStore.Load();
            False(recovered.IsConfigured, "Invalid persisted style recovers to setup: " + member);
            Equal(OrbEffect.Glass, recovered.Effect, "Invalid persisted style restores the safe glass default");
            True(!String.IsNullOrEmpty(invalidStore.LoadWarning), "Invalid persisted style produces a recovery warning");
            Equal(json, File.ReadAllText(invalidStore.FilePath), "Recovery preserves the unreadable source for " + member);
        }
    }

    private static AppSettings ValidSettings()
    { return new AppSettings { HolidayName = "Style test holiday", TargetLocal = "2030-12-25T09:00:00", IsConfigured = true }; }

    private static void CelebrationChoices(string scratch)
    {
        SettingsStore store = new SettingsStore(Path.Combine(scratch, "celebration"));
        foreach (CelebrationKind kind in Enum.GetValues(typeof(CelebrationKind)))
        {
            AppSettings settings = ValidSettings(); settings.CelebrationEffect = kind;
            store.Save(settings);
            Equal(kind, store.Load().CelebrationEffect, kind + " celebration choice survives save and reload");
            foreach (bool alwaysAnimate in new[] { true, false })
            {
                settings.AlwaysAnimateCelebrations = alwaysAnimate; store.Save(settings);
                Equal(alwaysAnimate, store.Load().AlwaysAnimateCelebrations, kind + " motion override survives save and restart");
                Equal(alwaysAnimate, settings.Copy().AlwaysAnimateCelebrations, kind + " draft preserves its motion override");
            }
        }
        string saved = File.ReadAllText(store.FilePath);
        string backup = File.ReadAllText(store.FilePath + ".bak");
        AppSettings invalid = ValidSettings(); invalid.CelebrationEffect = (CelebrationKind)42;
        RejectWithoutOverwrite(store, invalid, saved, backup, "Unknown celebration choice");
        SettingsStore legacy = new SettingsStore(Path.Combine(scratch, "celebration-legacy"));
        Directory.CreateDirectory(Path.GetDirectoryName(legacy.FilePath));
        File.WriteAllText(legacy.FilePath, "{\"HolidayName\":\"Existing countdown\",\"TargetLocal\":\"2030-12-25T09:00:00\",\"IsConfigured\":true}");
        Equal(CelebrationKind.Fireworks, legacy.Load().CelebrationEffect, "Legacy preferences migrate to their existing fireworks celebration");
        True(legacy.Load().AlwaysAnimateCelebrations, "A missing celebration motion preference uses the new enabled default");
        File.WriteAllText(legacy.FilePath, "{\"HolidayName\":\"Existing countdown\",\"TargetLocal\":\"2030-12-25T09:00:00\",\"IsConfigured\":true,\"AlwaysAnimateCelebrations\":false}");
        False(legacy.Load().AlwaysAnimateCelebrations, "An explicitly saved Off remains Off after the default changes");
    }

    private static string[] InvalidColours()
    { return new string[] { "red", "123456", "#FFF", "#12345678", "#12345G", " #123456", "#123456 ", "#12345", "#", "   ", "＃123456" }; }

    private static void CompanionChoices(string scratch)
    {
        SettingsStore store = new SettingsStore(Path.Combine(scratch, "companions"));
        foreach (CompanionKind kind in Enum.GetValues(typeof(CompanionKind)))
        {
            AppSettings settings = ValidSettings(); settings.Companion = kind; settings.Effect = OrbEffect.ShockwavePulses;
            settings.CompanionScenery = ((int)kind % 2 == 0);
            store.Save(settings);
            AppSettings loaded = store.Load();
            Equal(kind, loaded.Companion, kind + " survives save and restart");
            Equal(settings.CompanionScenery, loaded.CompanionScenery, kind + " remembers the scenery choice");
            Equal(OrbEffect.ShockwavePulses, loaded.Effect, kind + " does not replace the selected orb effect");
        }
        string saved = File.ReadAllText(store.FilePath), backup = File.ReadAllText(store.FilePath + ".bak");
        AppSettings invalid = ValidSettings(); invalid.Companion = (CompanionKind)99;
        RejectWithoutOverwrite(store, invalid, saved, backup, "Unknown companion");
    }

    private static void RejectWithoutOverwrite(SettingsStore store, AppSettings settings, string saved, string backup, string message)
    {
        bool rejected = false;
        try { store.Save(settings); } catch (ArgumentException) { rejected = true; }
        True(rejected, message + " is rejected before saving");
        Equal(saved, File.ReadAllText(store.FilePath), message + " preserves the saved countdown");
        Equal(backup, File.ReadAllText(store.FilePath + ".bak"), message + " preserves the recovery backup");
    }

    private static void True(bool condition, string message)
    { checks++; if (!condition) throw new Exception(message); }
    private static void False(bool condition, string message)
    { True(!condition, message); }
    private static void Equal<T>(T expected, T actual, string message)
    { checks++; if (!Object.Equals(expected, actual)) throw new Exception(message + "; expected " + expected + ", got " + actual); }
}
