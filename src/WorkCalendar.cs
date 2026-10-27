using System;
using System.Collections.Generic;
using System.Globalization;

namespace HolidayTimer
{
    // Work counting and calendar annotations share the named holiday owner.
    // Taking one snapshot keeps a refresh from mixing two calendars in a count.
    public static class NswWorkCalendar
    {
        public const int SupportedFrom = NswPublicHolidays.SupportedFrom;
        public const int SupportedThrough = NswPublicHolidays.SupportedThrough;
        public const string SourceUrl = "https://www.nsw.gov.au/about-nsw/public-holidays";
        public const string HistoricSourceUrl = "https://www.nsw.gov.au/sites/default/files/noindex/2024-08/NSW-Public-Holiday-Guide-2024-2025-2026.pdf";
        public static bool IsPublicHoliday(DateTime localDate)
        {
            PublicHolidaySnapshot calendar = NswPublicHolidays.Current;
            CheckYear(calendar, localDate.Year);
            return calendar.Find(localDate) != null;
        }
        public static string GetPublicHolidayName(DateTime localDate)
        {
            PublicHolidaySnapshot calendar = NswPublicHolidays.Current;
            CheckYear(calendar, localDate.Year);
            NswPublicHoliday holiday = calendar.Find(localDate);
            return holiday == null ? null : holiday.Name;
        }
        public static bool IsWorkday(DateTime localDate)
        {
            return IsWorkday(NswPublicHolidays.Current, localDate);
        }
        internal static void CheckYear(int year)
        {
            CheckYear(NswPublicHolidays.Current, year);
        }
        internal static bool IsWorkday(PublicHolidaySnapshot calendar, DateTime localDate)
        {
            CheckYear(calendar, localDate.Year);
            return localDate.DayOfWeek != DayOfWeek.Saturday && localDate.DayOfWeek != DayOfWeek.Sunday && calendar.Find(localDate) == null;
        }
        internal static void CheckYear(PublicHolidaySnapshot calendar, int year)
        {
            if (year < calendar.FromYear || year > calendar.ThroughYear)
                throw new ArgumentException("The NSW public-holiday calendar covers " + calendar.FromYear.ToString(CultureInfo.InvariantCulture) + " to " +
                    calendar.ThroughYear.ToString(CultureInfo.InvariantCulture) + ". Choose a date in that range or turn off workday counting.");
        }
    }

    public static class WorkCountdownCalculator
    {
        public const int SupportedFrom = NswWorkCalendar.SupportedFrom;
        public const int SupportedThrough = NswWorkCalendar.SupportedThrough;
        private static readonly object CacheGate = new object();
        private static readonly Dictionary<string, WorkWindows> Cache = new Dictionary<string, WorkWindows>();
        internal static int CacheBuildCount { get; private set; }
        internal static int CacheEntryCount { get { lock (CacheGate) return Cache.Count; } }

        private sealed class WorkWindows
        {
            internal DateTime FirstDate;
            internal DateTimeOffset[] Starts, Finishes;
            internal bool[] Working;
            internal long[] Prefix;
        }

        private static TimeSpan ReadTime(string value)
        {
            DateTime parsed;
            if (value == null || !DateTime.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
                throw new ArgumentException("Enter work hours in 24-hour HH:mm format, for example 09:00 and 17:00.");
            return parsed.TimeOfDay;
        }

        public static void ValidateSchedule(AppSettings settings)
        {
            if (settings == null) throw new ArgumentNullException("settings");
            TimeSpan start = ReadTime(settings.WorkStart), finish = ReadTime(settings.WorkFinish);
            if (finish <= start)
                throw new ArgumentException("Work finish must be later than work start on the same day. Overnight shifts are not supported.");
        }

        public static void ValidateRange(AppSettings settings, DateTimeOffset now)
        {
            if (settings == null) throw new ArgumentNullException("settings");
            if (!settings.WorkdayCountdown) return;
            DateTimeOffset target = CountdownCalculator.ToLocalTarget(settings.TargetLocal);
            if (target <= now) return;
            CheckRange(NswPublicHolidays.Current, now, target, TimeZoneInfo.Local);
        }

        private static void CheckRange(PublicHolidaySnapshot calendar, DateTimeOffset now, DateTimeOffset target, TimeZoneInfo zone)
        {
            NswWorkCalendar.CheckYear(calendar, TimeZoneInfo.ConvertTime(now, zone).Year);
            NswWorkCalendar.CheckYear(calendar, TimeZoneInfo.ConvertTime(target, zone).Year);
        }

        public static long Remaining(AppSettings settings, DateTimeOffset now, CountdownUnit? unit = null)
        {
            if (settings == null) throw new ArgumentNullException("settings");
            return Remaining(settings, CountdownCalculator.ToLocalTarget(settings.TargetLocal), now, unit ?? settings.Unit);
        }

        public static long Remaining(AppSettings settings, DateTimeOffset target, DateTimeOffset now, CountdownUnit unit)
        {
            return Remaining(settings, target, now, unit, TimeZoneInfo.Local);
        }

        internal static long Remaining(AppSettings settings, DateTimeOffset target, DateTimeOffset now, CountdownUnit unit, TimeZoneInfo zone)
        {
            if (settings == null) throw new ArgumentNullException("settings");
            if (!settings.WorkdayCountdown) return CountdownCalculator.Remaining(target, now, unit);
            if (target <= now) return 0;
            if (!Enum.IsDefined(typeof(CountdownUnit), unit)) throw new ArgumentOutOfRangeException("unit");
            ValidateSchedule(settings);
            PublicHolidaySnapshot snapshot = NswPublicHolidays.Current;
            CheckRange(snapshot, now, target, zone);
            TimeSpan start = ReadTime(settings.WorkStart), finish = ReadTime(settings.WorkFinish);
            WorkWindows calendar = GetWindows(snapshot, zone, start, finish);
            int first = (TimeZoneInfo.ConvertTime(now, zone).Date - calendar.FirstDate).Days;
            int last = (TimeZoneInfo.ConvertTime(target, zone).Date - calendar.FirstDate).Days;
            long ticks = Intersection(calendar, first, now, target);
            if (last != first)
            {
                ticks += calendar.Prefix[last] - calendar.Prefix[first + 1];
                ticks += Intersection(calendar, last, now, target);
            }
            long divisor;
            switch (unit)
            {
                case CountdownUnit.Days: divisor = (finish - start).Ticks; break;
                case CountdownUnit.Weeks: divisor = (finish - start).Ticks * 5; break;
                case CountdownUnit.Hours: divisor = TimeSpan.TicksPerHour; break;
                case CountdownUnit.Minutes: divisor = TimeSpan.TicksPerMinute; break;
                case CountdownUnit.Seconds: divisor = TimeSpan.TicksPerSecond; break;
                default: throw new ArgumentOutOfRangeException("unit");
            }
            // Integer ceiling preserves a single remaining tick even over years.
            return ticks == 0 ? 0 : (ticks - 1) / divisor + 1;
        }

        private static long Intersection(WorkWindows calendar, int index, DateTimeOffset now, DateTimeOffset target)
        {
            if (!calendar.Working[index]) return 0;
            DateTimeOffset start = now > calendar.Starts[index] ? now : calendar.Starts[index];
            DateTimeOffset finish = target < calendar.Finishes[index] ? target : calendar.Finishes[index];
            return finish > start ? (finish - start).Ticks : 0;
        }

        private static WorkWindows GetWindows(PublicHolidaySnapshot snapshot, TimeZoneInfo zone, TimeSpan start, TimeSpan finish)
        {
            // Include rules, not just zone ID: changing the computer's zone or
            // its rule data cannot reuse a stale schedule. The immutable holiday
            // revision also invalidates moved or removed dates in the same year.
            string key = snapshot.Revision.ToString(CultureInfo.InvariantCulture) + "|" + zone.ToSerializedString() + "|" + start.Ticks + "|" + finish.Ticks;
            lock (CacheGate)
            {
                WorkWindows existing;
                if (Cache.TryGetValue(key, out existing)) return existing;
                DateTime firstDate = new DateTime(snapshot.FromYear, 1, 1), lastDate = new DateTime(snapshot.ThroughYear, 12, 31);
                int days = (lastDate - firstDate).Days + 1;
                WorkWindows result = new WorkWindows { FirstDate = firstDate, Starts = new DateTimeOffset[days], Finishes = new DateTimeOffset[days],
                    Working = new bool[days], Prefix = new long[days + 1] };
                for (int i = 0; i < days; i++)
                {
                    DateTime date = firstDate.AddDays(i);
                    result.Prefix[i + 1] = result.Prefix[i];
                    if (!NswWorkCalendar.IsWorkday(snapshot, date)) continue;
                    DateTimeOffset begin = ResolveBoundary(date + start, zone), end = ResolveBoundary(date + finish, zone);
                    if (end <= begin) continue;
                    result.Starts[i] = begin; result.Finishes[i] = end; result.Working[i] = true;
                    result.Prefix[i + 1] += (end - begin).Ticks;
                }
                if (Cache.Count >= 8) Cache.Clear();
                Cache.Add(key, result); CacheBuildCount++;
                return result;
            }
        }

        internal static DateTimeOffset ResolveBoundary(DateTime wallTime, TimeZoneInfo zone)
        {
            DateTime wall = DateTime.SpecifyKind(wallTime, DateTimeKind.Unspecified);
            // A work boundary in a skipped clock interval advances to the first
            // valid local minute; targets keep the existing strict validation.
            DateTime limit = wall.AddDays(1);
            while (zone.IsInvalidTime(wall) && wall < limit) wall = wall.AddMinutes(1);
            if (zone.IsInvalidTime(wall)) throw new ArgumentException("The work hours do not resolve to a valid local time.");
            TimeSpan offset = zone.GetUtcOffset(wall);
            if (zone.IsAmbiguousTime(wall))
            {
                TimeSpan[] offsets = zone.GetAmbiguousTimeOffsets(wall);
                offset = offsets[0] < offsets[1] ? offsets[0] : offsets[1];
            }
            return new DateTimeOffset(wall, offset);
        }
    }
}
