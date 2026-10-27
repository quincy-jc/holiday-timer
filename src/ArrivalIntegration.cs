using System;

namespace HolidayTimer
{
    internal sealed partial class HolidayApplication
    {
        private bool arrivalOpening;

        internal void ProcessCelebrations(DateTime now)
        {
            UpdateGreeting(now);
            if (!CheckArrival(now)) CheckMorning(now);
            bool updated = ReconcilePendingPublicHolidays(now);
            if (EnsurePublicHolidayFallback(now) || updated) RefreshCountdown();
        }

        internal bool CheckArrival(DateTime now)
        {
            if (quitting || arrivalOpening || !SessionAvailable ||
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
