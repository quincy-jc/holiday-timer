using System;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace HolidayTimer
{
    public enum CountdownUnit
    {
        Days = 0,
        Minutes = 1,
        Seconds = 2,
        Weeks = 3,
        Hours = 4
    }

    public enum HolidayTheme
    {
        Beach,
        Sunset,
        Festive
    }

    public enum OrbEffect
    {
        Glass, MusicPulse, LiquidRibbons, OrbitalParticles, WaterRipples,
        SolarCorona, NorthernLights, PlasmaVortex, MagneticWaves, LiquidMetal,
        WarpTunnel, ShockwavePulses, CometSwarm
    }

    public enum CelebrationKind { Fireworks = 0, Flowers = 1 }
    public enum CompanionKind { None = 0, Dog, Cat, Bunny, Bird, Car, Bicycle, Motorcycle, Airplane }

    [DataContract]
    public sealed class AppSettings
    {
        [DataMember] public string HolidayName { get; set; }
        [DataMember] public string TargetLocal { get; set; }
        [DataMember] public CountdownUnit Unit { get; set; }
        [DataMember] public bool ShowFloating { get; set; }
        [DataMember] public bool AlwaysOnTop { get; set; }
        [DataMember] public bool StartAtLogin { get; set; }
        [DataMember] public bool MorningGreeting { get; set; }
        [DataMember] public double? WidgetLeft { get; set; }
        [DataMember] public double? WidgetTop { get; set; }
        [DataMember] public string LastGreetingDate { get; set; }
        [DataMember] public string LastArrivalTarget { get; set; }
        [DataMember] public bool IsConfigured { get; set; }
        [DataMember] public bool AutomaticPublicHoliday { get; set; }
        [DataMember] public string PublicHolidayKey { get; set; }
        [DataMember] public HolidayTheme Theme { get; set; }
        [DataMember] public bool LightAppearance { get; set; }
        [DataMember] public double BackgroundOpacity { get; set; }
        [DataMember] public string DisplayFont { get; set; }
        [DataMember] public double WidgetScale { get; set; }
        [DataMember] public bool MusicResponse { get; set; }
        [DataMember] public string AudioDeviceId { get; set; }
        [DataMember] public double MusicIntensity { get; set; }
        [DataMember] public OrbEffect Effect { get; set; }
        [DataMember] public double EffectIntensity { get; set; }
        [DataMember] public string CountdownTextColour { get; set; }
        [DataMember] public bool WorkdayCountdown { get; set; }
        [DataMember] public string WorkStart { get; set; }
        [DataMember] public string WorkFinish { get; set; }
        [DataMember] public CelebrationKind CelebrationEffect { get; set; }
        [DataMember] public CompanionKind Companion { get; set; }

        public static bool IsValidTextColour(string value)
        {
            if (String.IsNullOrEmpty(value)) return true;
            if (value.Length != 7 || value[0] != '#') return false;
            for (int i = 1; i < value.Length; i++)
                if (!Uri.IsHexDigit(value[i])) return false;
            return true;
        }

        public static string NormalizeTextColour(string value)
        {
            return !String.IsNullOrEmpty(value) && IsValidTextColour(value) ? value.ToUpperInvariant() : "";
        }

        public AppSettings()
        {
            SetDefaults();
        }

        internal AppSettings Copy() { return (AppSettings)MemberwiseClone(); }

        [OnDeserializing]
        private void OnDeserializing(StreamingContext context)
        {
            SetDefaults();
        }

        private void SetDefaults()
        {
            HolidayName = "My holiday";
            TargetLocal = DateTime.Now.Date.AddDays(30).AddHours(9)
                .ToString(CountdownCalculator.TargetFormat, CultureInfo.InvariantCulture);
            Unit = CountdownUnit.Days;
            ShowFloating = true;
            AlwaysOnTop = true;
            StartAtLogin = true;
            MorningGreeting = true;
            WidgetLeft = null;
            WidgetTop = null;
            LastGreetingDate = null;
            LastArrivalTarget = null;
            IsConfigured = false;
            AutomaticPublicHoliday = false;
            PublicHolidayKey = null;
            Theme = HolidayTheme.Beach;
            LightAppearance = false;
            BackgroundOpacity = 0.82;
            DisplayFont = "Inter Display";
            WidgetScale = 1.0;
            MusicResponse = false;
            AudioDeviceId = "";
            MusicIntensity = 0.35;
            Effect = OrbEffect.Glass;
            EffectIntensity = 0.65;
            CountdownTextColour = "";
            WorkdayCountdown = false;
            WorkStart = "09:00";
            WorkFinish = "17:00";
            CelebrationEffect = CelebrationKind.Fireworks;
            Companion = CompanionKind.None;
        }
    }

    public static class CountdownCalculator
    {
        public const string TargetFormat = "yyyy-MM-dd'T'HH:mm:ss";

        public static DateTime ParseTarget(string value)
        {
            DateTime parsed;
            if (!DateTime.TryParseExact(value, TargetFormat, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out parsed))
            {
                throw new FormatException("Choose a valid holiday date and time.");
            }
            return DateTime.SpecifyKind(parsed, DateTimeKind.Unspecified);
        }

        public static DateTimeOffset ToLocalTarget(string value)
        {
            return ToLocalOffset(ParseTarget(value));
        }

        public static DateTimeOffset ToLocalOffset(DateTime localWallTime)
        {
            // Preserve an existing instant, including the first occurrence of a repeated local hour.
            if (localWallTime.Kind != DateTimeKind.Unspecified)
            {
                return new DateTimeOffset(localWallTime);
            }
            DateTime wallTime = DateTime.SpecifyKind(localWallTime, DateTimeKind.Unspecified);
            TimeZoneInfo zone = TimeZoneInfo.Local;
            if (zone.IsInvalidTime(wallTime))
            {
                throw new ArgumentException("That time is skipped when the clocks change. Choose another time.");
            }

            TimeSpan offset = zone.GetUtcOffset(wallTime);
            if (zone.IsAmbiguousTime(wallTime))
            {
                // A repeated clock time uses the later occurrence (the standard-time offset).
                TimeSpan[] offsets = zone.GetAmbiguousTimeOffsets(wallTime);
                offset = offsets[0] < offsets[1] ? offsets[0] : offsets[1];
            }
            return new DateTimeOffset(wallTime, offset);
        }

        public static long Remaining(DateTime targetLocal, DateTime nowLocal, CountdownUnit unit)
        {
            return Remaining(ToLocalOffset(targetLocal), ToLocalOffset(nowLocal), unit);
        }

        public static long Remaining(DateTimeOffset target, DateTimeOffset now, CountdownUnit unit)
        {
            TimeSpan remaining = target - now;
            if (remaining <= TimeSpan.Zero)
            {
                return 0;
            }

            double count;
            switch (unit)
            {
                case CountdownUnit.Days:
                    count = remaining.TotalDays;
                    break;
                case CountdownUnit.Minutes:
                    count = remaining.TotalMinutes;
                    break;
                case CountdownUnit.Seconds:
                    count = remaining.TotalSeconds;
                    break;
                case CountdownUnit.Weeks:
                    count = remaining.TotalDays / 7;
                    break;
                case CountdownUnit.Hours:
                    count = remaining.TotalHours;
                    break;
                default:
                    throw new ArgumentOutOfRangeException("unit");
            }
            return (long)Math.Ceiling(count);
        }
    }

    public static class MorningPolicy
    {
        public static bool ShouldShow(AppSettings settings, DateTime nowLocal)
        {
            if (settings == null || !settings.IsConfigured || !settings.MorningGreeting ||
                nowLocal.Hour < 5 || nowLocal.Hour >= 12 ||
                String.Equals(settings.LastGreetingDate,
                    nowLocal.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), StringComparison.Ordinal))
            {
                return false;
            }

            try
            {
                return CountdownCalculator.ToLocalTarget(settings.TargetLocal) >
                    CountdownCalculator.ToLocalOffset(nowLocal);
            }
            catch (FormatException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }
    }

    public static class ArrivalPolicy
    {
        public static bool ShouldShow(AppSettings settings, DateTimeOffset now)
        {
            if (settings == null || (!settings.IsConfigured && !settings.AutomaticPublicHoliday) ||
                String.Equals(settings.LastArrivalTarget, settings.TargetLocal, StringComparison.Ordinal))
            {
                return false;
            }

            try
            {
                return CountdownCalculator.ToLocalTarget(settings.TargetLocal) <= now;
            }
            catch (FormatException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }
    }

    public sealed class SettingsStore
    {
        private readonly string directory;
        public string FilePath { get; private set; }
        public string LoadWarning { get; private set; }

        public SettingsStore(string directory = null)
        {
            this.directory = directory ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HolidayTimer");
            FilePath = Path.Combine(this.directory, "settings.json");
        }

        public AppSettings Load()
        {
            LoadWarning = null;
            if (!File.Exists(FilePath))
            {
                return new AppSettings();
            }

            try
            {
                using (FileStream stream = File.Open(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    AppSettings settings = (AppSettings)Serializer().ReadObject(stream);
                    Validate(settings);
                    return settings;
                }
            }
            catch (Exception exception)
            {
                if (!(exception is SerializationException) && !(exception is IOException) &&
                    !(exception is UnauthorizedAccessException) && !(exception is FormatException) &&
                    !(exception is ArgumentException))
                {
                    throw;
                }
                LoadWarning = "Your saved timer settings could not be read. Please choose your holiday again.";
                return new AppSettings();
            }
        }

        public void Save(AppSettings settings)
        {
            Validate(settings);
            Directory.CreateDirectory(directory);
            string temporaryPath = Path.Combine(directory, "settings." + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (FileStream stream = new FileStream(temporaryPath, FileMode.CreateNew,
                    FileAccess.Write, FileShare.None))
                {
                    Serializer().WriteObject(stream, settings);
                    stream.Flush(true);
                }

                if (File.Exists(FilePath))
                {
                    File.Replace(temporaryPath, FilePath, FilePath + ".bak", true);
                }
                else
                {
                    File.Move(temporaryPath, FilePath);
                }
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }

        private static DataContractJsonSerializer Serializer()
        {
            return new DataContractJsonSerializer(typeof(AppSettings));
        }

        private static void Validate(AppSettings settings)
        {
            if (settings == null || String.IsNullOrWhiteSpace(settings.HolidayName) ||
                settings.HolidayName.Length > 100)
            {
                throw new ArgumentException("Enter a holiday name of up to 100 characters.");
            }
            CountdownCalculator.ToLocalTarget(settings.TargetLocal);
            if (!Enum.IsDefined(typeof(CelebrationKind), settings.CelebrationEffect))
                throw new ArgumentException("Choose fireworks or flowers for your celebration.");
            if (!Enum.IsDefined(typeof(CompanionKind), settings.Companion))
                throw new ArgumentException("Choose a companion from the list.");
            if (!Enum.IsDefined(typeof(CountdownUnit), settings.Unit))
            {
                throw new ArgumentException("Choose days, weeks, hours, minutes, or seconds.");
            }
            if (settings.WorkdayCountdown)
            {
                WorkCountdownCalculator.ValidateSchedule(settings);
            }
            if (!Enum.IsDefined(typeof(HolidayTheme), settings.Theme))
            {
                throw new ArgumentException("Choose Beach, Sunset, or Festive.");
            }
            if (Double.IsNaN(settings.BackgroundOpacity) || Double.IsInfinity(settings.BackgroundOpacity) ||
                settings.BackgroundOpacity < 0.35 || settings.BackgroundOpacity > 1)
            {
                throw new ArgumentException("Choose a background opacity between 35% and 100%.");
            }
            if (String.IsNullOrWhiteSpace(settings.DisplayFont) || settings.DisplayFont.Length > 120)
            {
                throw new ArgumentException("Choose a display font with a name of up to 120 characters.");
            }
            if (Double.IsNaN(settings.WidgetScale) || Double.IsInfinity(settings.WidgetScale) ||
                settings.WidgetScale < 0.60 || settings.WidgetScale > 1.80)
            {
                throw new ArgumentException("Choose a gadget size between 60% and 180%.");
            }
            if (Double.IsNaN(settings.MusicIntensity) || Double.IsInfinity(settings.MusicIntensity) ||
                settings.MusicIntensity < 0 || settings.MusicIntensity > 1)
            {
                throw new ArgumentException("Choose a music response intensity between 0% and 100%.");
            }
            settings.AudioDeviceId = settings.AudioDeviceId ?? "";
            if (settings.PublicHolidayKey != null && settings.PublicHolidayKey.Length > 150)
                throw new ArgumentException("The selected public holiday is invalid.");
            if (!Enum.IsDefined(typeof(OrbEffect), settings.Effect))
                throw new ArgumentException("Choose an orb effect from the list.");
            if (Double.IsNaN(settings.EffectIntensity) || Double.IsInfinity(settings.EffectIntensity) ||
                settings.EffectIntensity < 0 || settings.EffectIntensity > 1)
                throw new ArgumentException("Choose a motion intensity between 0% and 100%.");
            if (!AppSettings.IsValidTextColour(settings.CountdownTextColour))
                throw new ArgumentException("Enter a text colour as #RRGGBB, for example #B4E5FF.");
            settings.CountdownTextColour = AppSettings.NormalizeTextColour(settings.CountdownTextColour);
            if (settings.AudioDeviceId.Length > 512)
                throw new ArgumentException("Choose an audio output from the list.");
            if ((settings.WidgetLeft.HasValue &&
                (Double.IsNaN(settings.WidgetLeft.Value) || Double.IsInfinity(settings.WidgetLeft.Value))) ||
                (settings.WidgetTop.HasValue &&
                (Double.IsNaN(settings.WidgetTop.Value) || Double.IsInfinity(settings.WidgetTop.Value))))
            {
                throw new ArgumentException("The floating timer position is invalid.");
            }
        }
    }
}
