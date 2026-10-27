using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Media;

namespace HolidayTimer
{
    internal partial class HolidayApplication
    {
        internal void ToggleWorkdays()
        {
            bool previous = Settings.WorkdayCountdown;
            Settings.WorkdayCountdown = !previous;
            try
            {
                if (Settings.WorkdayCountdown)
                {
                    WorkCountdownCalculator.ValidateSchedule(Settings);
                    WorkCountdownCalculator.ValidateRange(Settings, DateTimeOffset.Now);
                }
            }
            catch (ArgumentException ex)
            {
                Settings.WorkdayCountdown = previous;
                OpenSettings();
                if (SettingsWindow != null) Layouts.Find<TextBlock>(SettingsWindow, "FormError").Text = ex.Message;
                return;
            }
            SaveQuietly(); RefreshCountdown(); UpdateGreeting(DateTime.Now);
        }

        private static void UpdateWorkdayButton(Window window, AppSettings settings)
        {
            Button button = Layouts.Find<Button>(window, "WorkdayButton");
            if (settings.WorkdayCountdown) button.SetResourceReference(Control.BackgroundProperty, "Accent");
            else button.Background = Brushes.Transparent;
            button.SetResourceReference(Control.ForegroundProperty, settings.WorkdayCountdown ? "OnAccent" : "Heading");
            string state = settings.WorkdayCountdown ? "On" : "Off";
            button.ToolTip = "Workdays: " + state + " · " + settings.WorkStart + "–" + settings.WorkFinish + ". Click to " + (settings.WorkdayCountdown ? "count all time." : "count only working time.");
            AutomationProperties.SetName(button, "Workday counting " + state.ToLowerInvariant() + ". Toggle workday counting.");
            AutomationProperties.SetHelpText(button, "Uses Monday to Friday, NSW public holidays, and the work start and finish times in Settings.");
        }

        private static void FillWorkForm(Window form, AppSettings settings)
        {
            Layouts.Find<CheckBox>(form, "WorkdayCountdown").IsChecked = settings.WorkdayCountdown;
            Layouts.Find<TextBox>(form, "WorkStart").Text = settings.WorkStart;
            Layouts.Find<TextBox>(form, "WorkFinish").Text = settings.WorkFinish;
            UpdateWorkSummary(form);
        }

        private void BindWorkForm(Window form)
        {
            RoutedEventHandler changed = delegate { UpdateWorkSummary(form); QueuePreviewUpdate(); };
            Layouts.Find<CheckBox>(form, "WorkdayCountdown").Checked += changed;
            Layouts.Find<CheckBox>(form, "WorkdayCountdown").Unchecked += changed;
            Layouts.Find<TextBox>(form, "WorkStart").TextChanged += delegate { UpdateWorkSummary(form); QueuePreviewUpdate(); };
            Layouts.Find<TextBox>(form, "WorkFinish").TextChanged += delegate { UpdateWorkSummary(form); QueuePreviewUpdate(); };
        }

        private static void UpdateWorkSummary(Window form)
        {
            bool enabled = Layouts.Find<CheckBox>(form, "WorkdayCountdown").IsChecked == true;
            Layouts.Find<StackPanel>(form, "WorkdayScheduleDetails").Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
            AppSettings draft = WorkFormValues(form);
            string text;
            try
            {
                WorkCountdownCalculator.ValidateSchedule(draft);
                DateTime start = DateTime.ParseExact(draft.WorkStart, "HH:mm", CultureInfo.InvariantCulture);
                DateTime finish = DateTime.ParseExact(draft.WorkFinish, "HH:mm", CultureInfo.InvariantCulture);
                double hours = (finish - start).TotalHours;
                text = "1 workday = " + hours.ToString("0.##", CultureInfo.CurrentCulture) + " hours · 1 working week = " + (hours * 5).ToString("0.##", CultureInfo.CurrentCulture) + " hours";
            }
            catch (ArgumentException) { text = "Use HH:mm, with the finish later than the start."; }
            Layouts.Find<TextBlock>(form, "WorkScheduleSummary").Text = text;
        }

        private static AppSettings WorkFormValues(Window form)
        {
            return new AppSettings {
                WorkdayCountdown = Layouts.Find<CheckBox>(form, "WorkdayCountdown").IsChecked == true,
                WorkStart = Layouts.Find<TextBox>(form, "WorkStart").Text.Trim(),
                WorkFinish = Layouts.Find<TextBox>(form, "WorkFinish").Text.Trim()
            };
        }

        private static bool ReadWorkForm(Window form, AppSettings draft, out string error)
        {
            AppSettings values = WorkFormValues(form);
            draft.WorkdayCountdown = values.WorkdayCountdown;
            draft.WorkStart = values.WorkStart; draft.WorkFinish = values.WorkFinish;
            error = null;
            if (!draft.WorkdayCountdown) return true;
            try
            {
                WorkCountdownCalculator.ValidateSchedule(draft);
                WorkCountdownCalculator.ValidateRange(draft, DateTimeOffset.Now);
                return true;
            }
            catch (ArgumentException ex) { error = ex.Message; return false; }
        }

        private static void ReadWorkPreview(Window form, AppSettings draft, AppSettings previous)
        {
            AppSettings values = WorkFormValues(form);
            draft.WorkdayCountdown = values.WorkdayCountdown;
            try
            {
                WorkCountdownCalculator.ValidateSchedule(values);
                draft.WorkStart = values.WorkStart; draft.WorkFinish = values.WorkFinish;
            }
            catch (ArgumentException)
            {
                // Keep the last valid shift while the user types an incomplete time.
                draft.WorkStart = previous.WorkStart; draft.WorkFinish = previous.WorkFinish;
            }
        }

        private static bool TryRemaining(AppSettings settings, DateTimeOffset now, CountdownUnit unit, out long remaining)
        {
            try { remaining = WorkCountdownCalculator.Remaining(settings, now, unit); return true; }
            catch (ArgumentException)
            {
                // An installed calendar can age past its verified dates. Preserve
                // the saved countdown and make the missing update visible.
                if (!settings.WorkdayCountdown) throw;
                remaining = 0; return false;
            }
        }

        private static string RemainingCaption(AppSettings settings, long remaining, bool arrived, bool available)
        {
            if (arrived) return "Your holiday is here!";
            if (!available) return "Calendar update needed";
            return settings.WorkdayCountdown && remaining == 0 ? "No work time left" : UnitLabel(settings.Unit, remaining, settings.WorkdayCountdown) + " to go";
        }
    }
}
