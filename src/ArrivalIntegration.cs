using System;
using System.Globalization;

namespace HolidayTimer
{
    internal sealed partial class HolidayApplication
    {
        private bool arrivalOpening;
        private bool dayZeroSessionPending, dayZeroOpening;
        private DateTime dayZeroRetryAfter;
        private string automaticDayZeroTarget;

        internal void ArmDayZeroSession() { dayZeroSessionPending = true; dayZeroRetryAfter = DateTime.MinValue; }
        private void ClearAutomaticDayZero() { automaticDayZeroTarget = null; }

        internal void ProcessCelebrations(DateTime now)
        {
            if (quitting || dayZeroOpening) return;
            if (dayZeroSessionPending)
            {
                if (!SessionAvailable || (desktopIntegration && !DesktopSession.IsUnlocked())) return;
                if (ReconcilePendingPublicHolidays(now)) RefreshCountdown();
                if (DayZeroPolicy.Pending(Settings, now))
                {
                    // A startup/unlock while editing or previewing remains pending until dismissal.
                    if (SettingsWindow != null || IsDayZeroOpen || now < dayZeroRetryAfter) return;
                    ShowScheduledDayZero(now);
                    return;
                }
                dayZeroSessionPending = false;
            }
            if (IsDayZeroOpen)
            {
                // Only a scheduled scene can also satisfy an arrival that occurs
                // during it. A manual preview never consumes the real celebration.
                if (automaticDayZeroTarget == Settings.TargetLocal &&
                    ArrivalPolicy.ShouldShow(Settings, CountdownCalculator.ToLocalOffset(now)))
                { Settings.LastArrivalTarget = Settings.TargetLocal; SaveQuietly(); }
                return;
            }
            UpdateGreeting(now);
            if (!CheckArrival(now)) CheckMorning(now);
            bool updated = ReconcilePendingPublicHolidays(now);
            if (EnsurePublicHolidayFallback(now) || updated) RefreshCountdown();
        }

        private void ShowScheduledDayZero(DateTime now)
        {
            dayZeroSessionPending = false;
            dayZeroOpening = true;
            try
            {
                try { ShowDayZero(Settings); }
                catch (DisplayUnavailableException) { RetryDayZero(now); return; }
                catch (System.ComponentModel.Win32Exception) { RetryDayZero(now); return; }
                automaticDayZeroTarget = Settings.TargetLocal;
                string day = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                Settings.LastDayZeroDate = day;
                Settings.LastGreetingDate = day;
                if (ArrivalPolicy.ShouldShow(Settings, CountdownCalculator.ToLocalOffset(now)))
                    Settings.LastArrivalTarget = Settings.TargetLocal;
                SaveQuietly();
            }
            finally { dayZeroOpening = false; }
        }

        private void RetryDayZero(DateTime now)
        {
            // Windows can unlock before monitor geometry is available.
            dayZeroSessionPending = true;
            dayZeroRetryAfter = now.AddSeconds(3);
        }

        internal bool CheckArrival(DateTime now)
        {
            if (quitting || arrivalOpening || IsDayZeroOpen || !SessionAvailable ||
                !ArrivalPolicy.ShouldShow(Settings, CountdownCalculator.ToLocalOffset(now)) ||
                (desktopIntegration && !DesktopSession.IsUnlocked())) return false;

            string previous = Settings.LastArrivalTarget;
            Settings.LastArrivalTarget = Settings.TargetLocal;
            arrivalOpening = true;
            try
            {
                // Set the in-memory marker before opening, so reentrant ticks
                // cannot duplicate the group. Persist only after it was shown.
                PreviewCelebration(Settings, now);
                SaveQuietly();
                return true;
            }
            catch
            {
                Settings.LastArrivalTarget = previous;
                throw;
            }
            finally { arrivalOpening = false; }
        }
    }
}
