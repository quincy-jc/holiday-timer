using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace HolidayTimer
{
    public sealed class NswPublicHoliday
    {
        public DateTime Date { get; private set; }
        public string Name { get; private set; }
        public bool IsOfficial { get; private set; }
        public bool IsEdited { get; private set; }
        public DateTime SourceDate { get; private set; }
        public string SourceName { get; private set; }
        internal bool SourceIsOfficial { get; private set; }
        public string Key { get { return SourceDate.Year.ToString(CultureInfo.InvariantCulture) + "|" + SourceName; } }
        public NswPublicHoliday(DateTime date, string name, bool isOfficial)
            : this(date, name, isOfficial, date, name, false) { }
        internal NswPublicHoliday(DateTime date, string name, bool official, DateTime sourceDate, string sourceName, bool edited)
        {
            if (String.IsNullOrWhiteSpace(name) || name.Length > 120 || name.IndexOfAny(new char[] { '<', '>', '\r', '\n' }) >= 0)
                throw new ArgumentException("The public-holiday name is invalid.");
            Date = DateTime.SpecifyKind(date.Date, DateTimeKind.Unspecified);
            Name = name.Trim(); SourceDate = sourceDate.Date; SourceName = sourceName;
            IsEdited = edited; SourceIsOfficial = official; IsOfficial = official && !edited;
        }
        public override string ToString() { return Date.ToString("d MMM yyyy", CultureInfo.CurrentCulture) + " · " + Name; }
    }

    // One immutable lookup is shared by calendar annotations, next-holiday
    // selection, and a complete working-time calculation. Refresh swaps it once.
    public sealed class PublicHolidaySnapshot
    {
        private readonly Dictionary<DateTime, NswPublicHoliday> byDate;
        private readonly IList<NswPublicHoliday> entries;
        public int FromYear { get { return NswPublicHolidays.SupportedFrom; } }
        public int ThroughYear { get { return NswPublicHolidays.SupportedThrough; } }
        public int Revision { get; private set; }
        public DateTime? RefreshedUtc { get; private set; }
        public int OfficialThroughYear { get; private set; }
        public IList<NswPublicHoliday> Entries { get { return entries; } }
        internal PublicHolidaySnapshot(IEnumerable<NswPublicHoliday> values, DateTime? refreshedUtc, int revision)
        {
            List<NswPublicHoliday> sorted = new List<NswPublicHoliday>(values);
            sorted.Sort(delegate(NswPublicHoliday left, NswPublicHoliday right) { return left.Date.CompareTo(right.Date); });
            byDate = new Dictionary<DateTime, NswPublicHoliday>();
            foreach (NswPublicHoliday value in sorted)
            {
                if (value.Date.Year < FromYear || value.Date.Year > ThroughYear || byDate.ContainsKey(value.Date))
                    throw new ArgumentException("The public-holiday calendar contains an invalid or duplicate date.");
                byDate.Add(value.Date, value);
                if (value.SourceIsOfficial) OfficialThroughYear = Math.Max(OfficialThroughYear, value.SourceDate.Year);
            }
            entries = sorted.AsReadOnly(); RefreshedUtc = refreshedUtc; Revision = revision;
        }
        public NswPublicHoliday Find(DateTime localDate)
        { NswPublicHoliday value; return byDate.TryGetValue(localDate.Date, out value) ? value : null; }
        public NswPublicHoliday FindByKey(string key)
        { foreach (NswPublicHoliday value in entries) if (value.Key == key) return value; return null; }
        public NswPublicHoliday Next(DateTime localNow)
        { foreach (NswPublicHoliday value in entries) if (value.Date > localNow) return value; return null; }
        public IList<NswPublicHoliday> GetUpcoming(DateTime from, int count)
        {
            if (count < 0 || count > 1000) throw new ArgumentOutOfRangeException("count");
            List<NswPublicHoliday> result = new List<NswPublicHoliday>();
            foreach (NswPublicHoliday value in entries)
                if (value.Date >= from && result.Count < count) result.Add(value);
            return result.AsReadOnly();
        }
    }

    public static class NswPublicHolidays
    {
        public const int SupportedFrom = 2024;
        public const int SupportedThrough = 2031;
        public const string SourceUrl = "https://www.nsw.gov.au/about-nsw/public-holidays";
        public const string RulesSourceUrl = "https://legislation.nsw.gov.au/view/pdf/inforce/current/act-2010-115";
        private const int MaximumBytes = 2 * 1024 * 1024;
        private static readonly object CacheGate = new object();
        private static int revision;
        private static volatile PublicHolidaySnapshot source = NewSnapshot(BuiltIn(), null);
        private static volatile PublicHolidaySnapshot current = source;
        private static List<CalendarCorrection> corrections = new List<CalendarCorrection>();
        public static PublicHolidaySnapshot Current { get { return current; } }
        public static int Revision { get { return current.Revision; } }
        public static DateTime? LastRefreshUtc { get { return current.RefreshedUtc; } }
        public static int OfficialThrough { get { return current.OfficialThroughYear; } }
        public static NswPublicHoliday Find(DateTime date) { return current.Find(date); }
        public static NswPublicHoliday Next(DateTime now) { return current.Next(now); }
        public static IList<NswPublicHoliday> GetUpcoming(DateTime from, int count) { return current.GetUpcoming(from, count); }
        private static PublicHolidaySnapshot NewSnapshot(IEnumerable<NswPublicHoliday> entries, DateTime? refreshedUtc)
        { return new PublicHolidaySnapshot(entries, refreshedUtc, Interlocked.Increment(ref revision)); }

        private static IEnumerable<NswPublicHoliday> BuiltIn()
        {
            List<NswPublicHoliday> result = new List<NswPublicHoliday>();
            for (int year = SupportedFrom; year <= SupportedThrough; year++)
            {
                bool official = year <= 2027;
                Add(result, new DateTime(year, 1, 1), "New Year's Day", official);
                AddAdditional(result, new DateTime(year, 1, 1), "New Year's Day", official);
                DateTime australia = new DateTime(year, 1, 26);
                if (australia.DayOfWeek == DayOfWeek.Saturday) australia = australia.AddDays(2);
                else if (australia.DayOfWeek == DayOfWeek.Sunday) australia = australia.AddDays(1);
                Add(result, australia, "Australia Day", official);
                DateTime easter = EasterSunday(year);
                Add(result, easter.AddDays(-2), "Good Friday", official);
                Add(result, easter.AddDays(-1), "Easter Saturday", official);
                Add(result, easter, "Easter Sunday", official);
                Add(result, easter.AddDays(1), "Easter Monday", official);
                Add(result, new DateTime(year, 4, 25), "Anzac Day", official);
                // These two additions were specifically declared for 2026/2027.
                // They are not a continuing weekend rule for projected years.
                if (year == 2026 || year == 2027)
                    Add(result, new DateTime(year, 4, year == 2026 ? 27 : 26), "Anzac Day (additional public holiday)", official);
                Add(result, NthMonday(year, 6, 2), "King's Birthday", official);
                Add(result, NthMonday(year, 10, 1), "Labour Day", official);
                Add(result, new DateTime(year, 12, 25), "Christmas Day", official);
                AddAdditional(result, new DateTime(year, 12, 25), "Christmas Day", official);
                Add(result, new DateTime(year, 12, 26), "Boxing Day", official);
                AddAdditional(result, new DateTime(year, 12, 26), "Boxing Day", official);
            }
            return result;
        }
        private static void Add(List<NswPublicHoliday> result, DateTime date, string name, bool official)
        { result.Add(new NswPublicHoliday(date, name, official)); }
        private static void AddAdditional(List<NswPublicHoliday> result, DateTime date, string name, bool official)
        {
            if (date.DayOfWeek == DayOfWeek.Saturday) Add(result, date.AddDays(2), name + " (additional public holiday)", official);
            else if (date.DayOfWeek == DayOfWeek.Sunday) Add(result, date.AddDays(name == "New Year's Day" ? 1 : 2), name + " (additional public holiday)", official);
        }
        private static DateTime NthMonday(int year, int month, int nth)
        { DateTime first = new DateTime(year, month, 1); return first.AddDays(((int)DayOfWeek.Monday - (int)first.DayOfWeek + 7) % 7 + 7 * (nth - 1)); }
        private static DateTime EasterSunday(int year)
        {
            int a = year % 19, b = year / 100, c = year % 100, d = b / 4, e = b % 4;
            int f = (b + 8) / 25, g = (b - f + 1) / 3, h = (19 * a + b - d - g + 15) % 30;
            int i = c / 4, k = c % 4, l = (32 + 2 * e + 2 * i - h - k) % 7, m = (a + 11 * h + 22 * l) / 451;
            int value = h + l - 7 * m + 114;
            return new DateTime(year, value / 31, value % 31 + 1);
        }

        private static readonly string[] RequiredNames = { "New Year's Day", "Australia Day", "Good Friday", "Easter Saturday", "Easter Sunday", "Easter Monday", "Anzac Day", "King's Birthday", "Labour Day", "Christmas Day", "Boxing Day" };
        public static IList<NswPublicHoliday> ParseOfficialHtml(string html)
        { string markup; return ReadOfficialTables(html, out markup).AsReadOnly(); }
        private static List<NswPublicHoliday> ReadOfficialTables(string html, out string markup)
        {
            if (String.IsNullOrWhiteSpace(html) || html.Length > MaximumBytes) throw new InvalidDataException("The NSW calendar response is empty or too large.");
            List<NswPublicHoliday> result = new List<NswPublicHoliday>();
            HashSet<int> completeYears = new HashSet<int>();
            StringBuilder accepted = new StringBuilder();
            foreach (Match table in Regex.Matches(html, @"<table\b[^>]*>.*?</table>", RegexOptions.IgnoreCase | RegexOptions.Singleline))
            {
                MatchCollection rows = Regex.Matches(table.Value, @"<tr\b[^>]*>.*?</tr>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (rows.Count < 2) continue;
                List<string> headers = Cells(rows[0].Value);
                if (headers.Count < 2 || !String.Equals(headers[0], "Holiday", StringComparison.OrdinalIgnoreCase)) continue;
                List<int> years = new List<int>();
                bool relevant = false;
                for (int column = 1; column < headers.Count; column++)
                {
                    int year;
                    if (!Int32.TryParse(headers[column], NumberStyles.None, CultureInfo.InvariantCulture, out year) || year < 2000 || year > 2100)
                        throw new InvalidDataException("The NSW calendar's year headings have changed. The saved calendar is unchanged.");
                    years.Add(year); relevant |= year >= SupportedFrom && year <= SupportedThrough;
                }
                if (!relevant) continue;
                Dictionary<int, List<NswPublicHoliday>> parsed = new Dictionary<int, List<NswPublicHoliday>>();
                Dictionary<int, HashSet<string>> seen = new Dictionary<int, HashSet<string>>();
                foreach (int year in years)
                {
                    if (parsed.ContainsKey(year) || completeYears.Contains(year)) throw new InvalidDataException("The NSW calendar contains duplicate year headings.");
                    parsed.Add(year, new List<NswPublicHoliday>()); seen.Add(year, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
                }
                string preceding = null;
                for (int row = 1; row < rows.Count; row++)
                {
                    List<string> cells = Cells(rows[row].Value);
                    if (cells.Count != headers.Count) throw new InvalidDataException("The NSW calendar table is incomplete. The saved calendar is unchanged.");
                    string label = cells[0];
                    if (String.Equals(label, "Bank Holiday", StringComparison.OrdinalIgnoreCase)) continue;
                    bool additional = String.Equals(label, "Additional Day", StringComparison.OrdinalIgnoreCase);
                    if (additional && preceding == null) throw new InvalidDataException("An additional NSW holiday has no named occasion.");
                    if (!additional) preceding = label;
                    string name = additional ? preceding + " (additional public holiday)" : label;
                    for (int column = 1; column < cells.Count; column++)
                    {
                        int year = years[column - 1];
                        if (year < SupportedFrom || year > SupportedThrough) continue;
                        string text = cells[column];
                        if (String.Equals(text, "Not applicable", StringComparison.OrdinalIgnoreCase) || String.Equals(text, "N/A", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!additional) throw new InvalidDataException("The NSW calendar omits a standard holiday date.");
                            continue;
                        }
                        DateTime date;
                        if (!DateTime.TryParseExact(text, new string[] { "dddd d MMMM yyyy", "dddd dd MMMM yyyy" }, CultureInfo.GetCultureInfo("en-AU"), DateTimeStyles.None, out date) || date.Year != year)
                            throw new InvalidDataException("A NSW public-holiday date could not be verified. The saved calendar is unchanged.");
                        parsed[year].Add(new NswPublicHoliday(date, name, true));
                        if (!additional && !seen[year].Add(label)) throw new InvalidDataException("The NSW calendar repeats a standard holiday.");
                    }
                }
                foreach (int year in years)
                {
                    if (year < SupportedFrom || year > SupportedThrough) continue;
                    foreach (string required in RequiredNames)
                        if (!seen[year].Contains(required)) throw new InvalidDataException("The NSW calendar no longer contains a complete public-holiday year. The saved calendar is unchanged.");
                    List<NswPublicHoliday> entries = parsed[year];
                    if (entries.Count < 11 || entries.Count > 25) throw new InvalidDataException("The NSW calendar contains an unexpected number of holidays.");
                    HashSet<DateTime> dates = new HashSet<DateTime>();
                    foreach (NswPublicHoliday entry in entries)
                        if (!dates.Add(entry.Date)) throw new InvalidDataException("The NSW calendar repeats a public-holiday date.");
                    result.AddRange(entries); completeYears.Add(year);
                }
                accepted.Append(table.Value);
            }
            if (completeYears.Count == 0) throw new InvalidDataException("A complete NSW public-holiday table was not found. The saved calendar is unchanged.");
            result.Sort(delegate(NswPublicHoliday left, NswPublicHoliday right) { return left.Date.CompareTo(right.Date); });
            markup = accepted.ToString(); return result;
        }
        private static List<string> Cells(string row)
        {
            List<string> values = new List<string>();
            foreach (Match cell in Regex.Matches(row, @"<t[dh]\b[^>]*>(.*?)</t[dh]>", RegexOptions.IgnoreCase | RegexOptions.Singleline))
            {
                string text = Regex.Replace(cell.Groups[1].Value, @"<sup\b[^>]*>.*?</sup>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                text = Regex.Replace(text, @"<[^>]+>", " ");
                text = WebUtility.HtmlDecode(text).Replace('\u2019', '\'').Replace('\u2018', '\'');
                values.Add(Regex.Replace(text, @"\s+", " ").Trim());
            }
            return values;
        }

        [DataContract]
        private sealed class CalendarCache
        {
            [DataMember] internal int Version;
            [DataMember] internal string Source;
            [DataMember] internal string Table;
            [DataMember] internal string RefreshedUtc;
        }
        [DataContract]
        private sealed class CalendarCorrection
        {
            [DataMember] internal string Key;
            [DataMember] internal string Date;
            [DataMember] internal string Name;
        }
        [DataContract]
        private sealed class CalendarCorrections
        {
            [DataMember] internal int Version;
            [DataMember] internal List<CalendarCorrection> Items;
        }
        private static string CachePath(string directory)
        {
            if (String.IsNullOrWhiteSpace(directory)) throw new ArgumentException("A calendar cache directory is required.");
            return Path.Combine(directory, "public-holidays.json");
        }
        public static string Initialize(string directory)
        {
            string path = CachePath(directory);
            lock (CacheGate)
            {
                PublicHolidaySnapshot loadedSource = NewSnapshot(BuiltIn(), null);
                try
                {
                    if (File.Exists(path))
                    {
                        if (new FileInfo(path).Length > MaximumBytes) throw new InvalidDataException("Calendar cache is too large.");
                        CalendarCache cache;
                        using (FileStream stream = File.OpenRead(path)) cache = (CalendarCache)new DataContractJsonSerializer(typeof(CalendarCache)).ReadObject(stream);
                        DateTime refreshed;
                        if (cache == null || cache.Version != 1 || cache.Source != SourceUrl || !DateTime.TryParseExact(cache.RefreshedUtc, "o", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out refreshed) || refreshed.Kind != DateTimeKind.Utc)
                            throw new InvalidDataException("Calendar cache format is invalid.");
                        string markup;
                        loadedSource = Merge(ReadOfficialTables(cache.Table, out markup), refreshed, loadedSource);
                    }
                }
                catch (Exception error)
                {
                    if (!(error is IOException) && !(error is UnauthorizedAccessException) && !(error is SerializationException) && !(error is ArgumentException) && !(error is InvalidDataException)) throw;
                    // A damaged official cache must not hide otherwise valid
                    // personal edits when the built-in calendar is usable.
                    try
                    {
                        List<CalendarCorrection> savedEdits = ReadCorrections(directory);
                        if (savedEdits.Count != 0)
                        { PublicHolidaySnapshot kept = ApplyCorrections(source, savedEdits); corrections = savedEdits; current = kept; }
                    }
                    catch (Exception editError)
                    {
                        if (!(editError is IOException) && !(editError is UnauthorizedAccessException) && !(editError is SerializationException) && !(editError is ArgumentException) && !(editError is InvalidDataException)) throw;
                        return "The saved NSW calendar and holiday edits could not be loaded. The last available dates are unchanged.";
                    }
                    return "The saved NSW calendar could not be loaded. The last available dates are unchanged; refresh to try again.";
                }
                try
                {
                    List<CalendarCorrection> loadedCorrections = ReadCorrections(directory);
                    PublicHolidaySnapshot loaded = ApplyCorrections(loadedSource, loadedCorrections);
                    source = loadedSource; corrections = loadedCorrections; current = loaded;
                }
                catch (Exception error)
                {
                    if (!(error is IOException) && !(error is UnauthorizedAccessException) && !(error is SerializationException) && !(error is ArgumentException) && !(error is InvalidDataException)) throw;
                    return "Saved NSW holiday edits could not be loaded. The last available dates are unchanged.";
                }
                return null;
            }
        }
        public static PublicHolidaySnapshot Refresh(string directory)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(SourceUrl);
            request.Method = "GET"; request.Timeout = request.ReadWriteTimeout = 12000;
            request.AllowAutoRedirect = false; request.UserAgent = "HolidayTimer/1.6 NSW-calendar-refresh";
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            string html;
            Stopwatch deadline = Stopwatch.StartNew();
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            {
                if (response.StatusCode != HttpStatusCode.OK || response.ContentLength > MaximumBytes || response.ContentType == null || response.ContentType.IndexOf("text/html", StringComparison.OrdinalIgnoreCase) < 0)
                    throw new InvalidDataException("The NSW calendar service did not return a usable holiday page.");
                using (Stream source = response.GetResponseStream())
                using (MemoryStream bytes = new MemoryStream())
                {
                    byte[] buffer = new byte[8192]; int read;
                    while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        if (deadline.Elapsed.TotalSeconds > 15) throw new WebException("The NSW calendar request timed out. The saved calendar is unchanged.", WebExceptionStatus.Timeout);
                        if (bytes.Length + read > MaximumBytes) throw new InvalidDataException("The NSW calendar response is too large.");
                        bytes.Write(buffer, 0, read);
                    }
                    html = Encoding.UTF8.GetString(bytes.ToArray());
                }
            }
            return ApplyOfficialHtml(directory, html, DateTime.UtcNow);
        }
        internal static PublicHolidaySnapshot ApplyOfficialHtml(string directory, string html, DateTime refreshedUtc)
        {
            string markup;
            List<NswPublicHoliday> official = ReadOfficialTables(html, out markup);
            lock (CacheGate)
            {
                PublicHolidaySnapshot nextSource = Merge(official, refreshedUtc.ToUniversalTime());
                PublicHolidaySnapshot next = ApplyCorrections(nextSource, corrections);
                string path = CachePath(directory);
                Directory.CreateDirectory(directory);
                string temporary = Path.Combine(directory, "public-holidays." + Guid.NewGuid().ToString("N") + ".tmp");
                try
                {
                    CalendarCache cache = new CalendarCache { Version = 1, Source = SourceUrl, Table = CacheTables(nextSource), RefreshedUtc = nextSource.RefreshedUtc.Value.ToString("o", CultureInfo.InvariantCulture) };
                    using (FileStream stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        new DataContractJsonSerializer(typeof(CalendarCache)).WriteObject(stream, cache); stream.Flush(true);
                    }
                    if (File.Exists(path)) File.Replace(temporary, path, path + ".bak", true);
                    else File.Move(temporary, path);
                    source = nextSource; current = next;
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
                return next;
            }
        }
        private static PublicHolidaySnapshot Merge(IList<NswPublicHoliday> official, DateTime refreshedUtc, PublicHolidaySnapshot prior = null)
        {
            HashSet<int> replace = new HashSet<int>();
            foreach (NswPublicHoliday value in official) replace.Add(value.Date.Year);
            List<NswPublicHoliday> values = new List<NswPublicHoliday>();
            foreach (NswPublicHoliday value in (prior ?? source).Entries) if (!replace.Contains(value.Date.Year)) values.Add(value);
            values.AddRange(official);
            return NewSnapshot(values, refreshedUtc);
        }

        private static List<CalendarCorrection> ReadCorrections(string directory)
        {
            string path = Path.Combine(directory, "public-holiday-edits.json");
            if (!File.Exists(path)) return new List<CalendarCorrection>();
            if (new FileInfo(path).Length > MaximumBytes) throw new InvalidDataException("Holiday edits are too large.");
            CalendarCorrections saved;
            using (FileStream stream = File.OpenRead(path)) saved = (CalendarCorrections)new DataContractJsonSerializer(typeof(CalendarCorrections)).ReadObject(stream);
            if (saved == null || saved.Version != 1 || saved.Items == null || saved.Items.Count > 500)
                throw new InvalidDataException("Holiday edits are invalid.");
            HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (CalendarCorrection correction in saved.Items)
            {
                DateTime parsed;
                if (correction == null || String.IsNullOrWhiteSpace(correction.Key) || correction.Key.Length > 150 || !keys.Add(correction.Key) ||
                    !DateTime.TryParseExact(correction.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed) ||
                    String.IsNullOrWhiteSpace(correction.Name) || correction.Name.Length > 100 || correction.Name.IndexOfAny(new char[] { '<', '>', '\r', '\n' }) >= 0)
                    throw new InvalidDataException("A saved holiday edit is invalid.");
            }
            return saved.Items;
        }

        private static PublicHolidaySnapshot ApplyCorrections(PublicHolidaySnapshot original, IList<CalendarCorrection> changes)
        {
            List<NswPublicHoliday> values = new List<NswPublicHoliday>();
            foreach (NswPublicHoliday holiday in original.Entries)
            {
                CalendarCorrection correction = null;
                foreach (CalendarCorrection item in changes) if (item.Key == holiday.Key) { correction = item; break; }
                if (correction == null) { values.Add(holiday); continue; }
                DateTime date = DateTime.ParseExact(correction.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                if (date.Year != holiday.SourceDate.Year) throw new ArgumentException("Keep the holiday in its original year.");
                values.Add(new NswPublicHoliday(date, correction.Name, holiday.SourceIsOfficial, holiday.SourceDate, holiday.SourceName, true));
            }
            return NewSnapshot(values, original.RefreshedUtc);
        }

        public static PublicHolidaySnapshot SaveCorrection(string directory, string key, DateTime? date, string name)
        {
            lock (CacheGate)
            {
                NswPublicHoliday original = source.FindByKey(key);
                if (original == null) throw new ArgumentException("That holiday is no longer available. Refresh the list and try again.");
                List<CalendarCorrection> nextCorrections = new List<CalendarCorrection>();
                foreach (CalendarCorrection item in corrections) if (item.Key != key) nextCorrections.Add(item);
                if (date.HasValue)
                {
                    if (date.Value.Year != original.Date.Year) throw new ArgumentException("Keep the holiday in its original year.");
                    if (String.IsNullOrWhiteSpace(name) || name.Trim().Length > 100 || name.IndexOfAny(new char[] { '<', '>', '\r', '\n' }) >= 0)
                        throw new ArgumentException("Enter a holiday name of up to 100 characters.");
                    if (date.Value.Date != original.Date || name.Trim() != original.Name)
                        nextCorrections.Add(new CalendarCorrection { Key = key, Date = date.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Name = name.Trim() });
                }
                PublicHolidaySnapshot next;
                try { next = ApplyCorrections(source, nextCorrections); }
                catch (ArgumentException) { throw new ArgumentException("Another holiday already uses that date, or the date is outside the calendar."); }
                string path = Path.Combine(directory, "public-holiday-edits.json");
                Directory.CreateDirectory(directory);
                string temporary = Path.Combine(directory, "public-holiday-edits." + Guid.NewGuid().ToString("N") + ".tmp");
                try
                {
                    using (FileStream stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    { new DataContractJsonSerializer(typeof(CalendarCorrections)).WriteObject(stream, new CalendarCorrections { Version = 1, Items = nextCorrections }); stream.Flush(true); }
                    if (File.Exists(path)) File.Replace(temporary, path, path + ".bak", true);
                    else File.Move(temporary, path);
                    corrections = nextCorrections; current = next;
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
                return next;
            }
        }
        private static string CacheTables(PublicHolidaySnapshot snapshot)
        {
            // Keep all successfully known published years when the provider's
            // rolling table advances; a later reload must not discard corrections
            // from a year that is no longer on its current page.
            StringBuilder html = new StringBuilder();
            for (int year = SupportedFrom; year <= SupportedThrough; year++)
            {
                bool opened = false;
                foreach (NswPublicHoliday entry in snapshot.Entries)
                {
                    if (!entry.IsOfficial || entry.Date.Year != year) continue;
                    if (!opened)
                    {
                        html.Append("<table><tr><th>Holiday</th><th>").Append(year).Append("</th></tr>"); opened = true;
                    }
                    html.Append("<tr><td>").Append(WebUtility.HtmlEncode(entry.Name)).Append("</td><td>")
                        .Append(entry.Date.ToString("dddd d MMMM yyyy", CultureInfo.GetCultureInfo("en-AU"))).Append("</td></tr>");
                }
                if (opened) html.Append("</table>");
            }
            return html.ToString();
        }
        internal static PublicHolidaySnapshot ReplaceForTests(IEnumerable<NswPublicHoliday> values)
        {
            PublicHolidaySnapshot previous = current;
            current = NewSnapshot(values, previous.RefreshedUtc); return previous;
        }
        internal static void RestoreForTests(PublicHolidaySnapshot snapshot)
        { current = NewSnapshot(snapshot.Entries, snapshot.RefreshedUtc); }
    }
}
