using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace HolidayTimer
{
    internal sealed partial class HolidayApplication
    {
        private string publicHolidayWarning;
        private bool publicHolidayResyncPending;
        internal string PublicHolidayCacheDirectory { get { return System.IO.Path.GetDirectoryName(store.FilePath); } }

        private void InitializePublicHolidayCalendar()
        {
            publicHolidayWarning = NswPublicHolidays.Initialize(PublicHolidayCacheDirectory);
            DateTime now = DateTime.Now;
            if (EnsurePublicHolidayFallback(now, true)) return;
            if (Settings.AutomaticPublicHoliday) SelectAutomaticPublicHoliday(now);
            else if (Settings.IsConfigured && !String.IsNullOrEmpty(Settings.PublicHolidayKey)) UpdateLinkedPublicHoliday(now);
        }

        // Startup skips a stale saved event. A running event first completes
        // its arrival presentation; its next target loads after dismissal.
        internal bool EnsurePublicHolidayFallback(DateTime now, bool startup = false)
        {
            if (quitting || (!startup && (!SessionAvailable || Greeting != null ||
                (desktopIntegration && !DesktopSession.IsUnlocked())))) return false;
            bool missing = !Settings.IsConfigured && !Settings.AutomaticPublicHoliday;
            bool expired;
            try { expired = CountdownCalculator.ToLocalTarget(Settings.TargetLocal) <= CountdownCalculator.ToLocalOffset(now); }
            catch (ArgumentException) { expired = true; }
            catch (FormatException) { expired = true; }
            if (!missing && !expired) return false;
            return SelectAutomaticPublicHoliday(now);
        }

        private bool SelectAutomaticPublicHoliday(DateTime now)
        {
            NswPublicHoliday next = NswPublicHolidays.Next(now);
            if (next == null)
            {
                publicHolidayWarning = "No upcoming NSW public holiday is available. Refresh the calendar or choose your own date.";
                return false;
            }
            AppSettings previous = Settings;
            string target = next.Date.ToString(CountdownCalculator.TargetFormat, CultureInfo.InvariantCulture);
            if (Settings.AutomaticPublicHoliday && Settings.TargetLocal == target && Settings.HolidayName == next.Name) return false;
            AppSettings updated = Settings.Copy();
            updated.HolidayName = next.Name;
            updated.TargetLocal = target;
            updated.AutomaticPublicHoliday = true;
            updated.PublicHolidayKey = next.Key;
            // Keep the previous arrival marker: the new target remains distinct.
            // First-run defaults remain unsaved and do not register sign-in.
            Settings = updated;
            SaveQuietly();
            UpdateUnchangedHolidayForm(previous);
            return true;
        }

        private void UpdateUnchangedHolidayForm(AppSettings previous)
        {
            if (SettingsWindow == null) return;
            string target, error;
            if (Layouts.Find<TextBox>(SettingsWindow, "HolidayName").Text.Trim() != previous.HolidayName ||
                !TryReadTarget(SettingsWindow, out target, out error) || target != previous.TargetLocal) return;
            Layouts.Find<TextBox>(SettingsWindow, "HolidayName").Text = Settings.HolidayName;
            Layouts.Find<DatePicker>(SettingsWindow, "HolidayDate").SelectedDate = CountdownCalculator.ParseTarget(Settings.TargetLocal).Date;
            Layouts.Find<TextBox>(SettingsWindow, "HolidayTime").Text = CountdownCalculator.ParseTarget(Settings.TargetLocal).ToString("HH:mm", CultureInfo.InvariantCulture);
            QueuePreviewUpdate();
        }

        internal void PublicHolidaysRefreshed()
        {
            if (quitting) return;
            publicHolidayWarning = null;
            if (Greeting != null || !SessionAvailable || (desktopIntegration && !DesktopSession.IsUnlocked()))
                publicHolidayResyncPending = true;
            else { publicHolidayResyncPending = false; ReconcilePublicHolidayData(DateTime.Now); }
            if (SettingsWindow != null)
            {
                UpdateSelectedHolidayDraft(SettingsWindow);
                CalendarPicker.Refresh(Layouts.Find<DatePicker>(SettingsWindow, "HolidayDate"));
                RefreshPublicHolidayForm(SettingsWindow);
                QueuePreviewUpdate();
            }
            RefreshCountdown();
        }

        internal bool ReconcilePendingPublicHolidays(DateTime now)
        {
            if (!publicHolidayResyncPending || Greeting != null || !SessionAvailable ||
                (desktopIntegration && !DesktopSession.IsUnlocked())) return false;
            publicHolidayResyncPending = false;
            return ReconcilePublicHolidayData(now);
        }

        private bool ReconcilePublicHolidayData(DateTime now)
        {
            if (Settings.AutomaticPublicHoliday) return SelectAutomaticPublicHoliday(now);
            if (!String.IsNullOrEmpty(Settings.PublicHolidayKey)) return UpdateLinkedPublicHoliday(now);
            return EnsurePublicHolidayFallback(now);
        }

        private bool UpdateLinkedPublicHoliday(DateTime now)
        {
            NswPublicHoliday holiday = NswPublicHolidays.Current.FindByKey(Settings.PublicHolidayKey);
            if (holiday == null) return SelectAutomaticPublicHoliday(now);
            DateTime previousTarget = CountdownCalculator.ParseTarget(Settings.TargetLocal);
            DateTime target = holiday.Date.Add(previousTarget.TimeOfDay);
            string targetText = target.ToString(CountdownCalculator.TargetFormat, CultureInfo.InvariantCulture);
            if (Settings.HolidayName == holiday.Name && Settings.TargetLocal == targetText) return false;
            AppSettings previous = Settings;
            AppSettings updated = Settings.Copy();
            updated.HolidayName = holiday.Name; updated.TargetLocal = targetText;
            Settings = updated;
            if (target <= now) { if (SelectAutomaticPublicHoliday(now)) UpdateUnchangedHolidayForm(previous); }
            else { SaveQuietly(); UpdateUnchangedHolidayForm(previous); }
            return true;
        }

        private bool PreparePublicHolidayFallbackForm(Window form, DateTime now)
        {
            string target, error;
            bool blank = String.IsNullOrWhiteSpace(Layouts.Find<DatePicker>(form, "HolidayDate").Text) ||
                String.IsNullOrWhiteSpace(Layouts.Find<TextBox>(form, "HolidayTime").Text);
            bool expired = TryReadTarget(form, out target, out error) &&
                CountdownCalculator.ToLocalTarget(target) <= CountdownCalculator.ToLocalOffset(now);
            if (!blank && !expired) return false;
            NswPublicHoliday next = NswPublicHolidays.Next(now);
            if (next == null) return false;
            Layouts.Find<TextBox>(form, "HolidayName").Text = next.Name;
            Layouts.Find<DatePicker>(form, "HolidayDate").SelectedDate = next.Date;
            Layouts.Find<TextBox>(form, "HolidayTime").Text = "00:00";
            return true;
        }
    }
}
