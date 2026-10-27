using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Media;
using System.Windows.Input;

namespace HolidayTimer
{
    internal static partial class UiChecks
    {
        private static void CheckWorkControls(HolidayApplication app, Window form, SettingsStore store, string root, bool render)
        {
            AppSettings original; string error;
            Check(app.ReadForm(form, false, out original, out error), "The workday fixture starts with a valid unchanged draft");
            string saved = File.Exists(store.FilePath) ? File.ReadAllText(store.FilePath) : null;
            try
            {
                Check(!original.WorkdayCountdown && Layouts.Find<StackPanel>(form, "WorkdayScheduleDetails").Visibility == Visibility.Collapsed,
                    "Working-time counting is optional and hides its schedule when off");
                Layouts.Find<RadioButton>(form, "UnitHours").IsChecked = true;
                AppSettings draft;
                Check(app.ReadForm(form, false, out draft, out error) && draft.Unit == CountdownUnit.Hours && (int)draft.Unit == 4,
                    "The visible Hours choice has the appended, compatible unit identifier");
                Check(HolidayApplication.UnitLabel(CountdownUnit.Hours, 1) == "hour" && HolidayApplication.UnitLabel(CountdownUnit.Hours, 8) == "hours",
                    "Hours uses ordinary singular and plural labels");
                Check(HolidayApplication.UnitLabel(CountdownUnit.Days, 1, true) == "workday" && HolidayApplication.UnitLabel(CountdownUnit.Days, 8, true) == "workdays",
                    "Working Days labels distinguish one workday from multiple workdays");
                Layouts.Find<DatePicker>(form, "HolidayDate").SelectedDate = DateTime.Now.Date.AddDays(14);
                Layouts.Find<TextBox>(form, "HolidayTime").Text = "17:00";
                Layouts.Find<CheckBox>(form, "WorkdayCountdown").IsChecked = true;
                Check(Layouts.Find<StackPanel>(form, "WorkdayScheduleDetails").Visibility == Visibility.Visible,
                    "One Workdays toggle reveals the daily start and finish times");
                foreach (int shift in new int[] { 6, 8, 10 })
                {
                    Layouts.Find<TextBox>(form, "WorkStart").Text = "07:00";
                    Layouts.Find<TextBox>(form, "WorkFinish").Text = (7 + shift).ToString("00", CultureInfo.InvariantCulture) + ":00";
                    Check(Layouts.Find<TextBlock>(form, "WorkScheduleSummary").Text == "1 workday = " + shift + " hours · 1 working week = " + shift * 5 + " hours",
                        "The " + shift + "-hour shift explains the Days and Weeks conversion");
                    Check(app.ReadForm(form, false, out draft, out error) && draft.WorkdayCountdown && draft.WorkStart == "07:00" && draft.WorkFinish == (7 + shift).ToString("00") + ":00",
                        "The " + shift + "-hour shift enters the actual saved-form draft");
                }
                Layouts.Find<TextBox>(form, "WorkStart").Text = "09:00";
                Layouts.Find<TextBox>(form, "WorkFinish").Text = "17:00";
                app.ShowGadgetPreview(form);
                bool topmost = app.GadgetPreview.Topmost;
                Button workButton = Layouts.Find<Button>(app.GadgetPreview, "WorkdayButton");
                Check(form.FindName("WorkHoursToggle") == null && app.GadgetPreview.FindName("PinButton") == null,
                    "The gadget replaces Pin with Workdays and the schedule uses one mode toggle");
                foreach (CountdownUnit unit in new CountdownUnit[] { CountdownUnit.Days, CountdownUnit.Weeks, CountdownUnit.Hours, CountdownUnit.Minutes, CountdownUnit.Seconds })
                {
                    Layouts.Find<ComboBox>(form, "CountdownUnit").SelectedIndex = (int)unit;
                    Check(app.ReadForm(form, false, out draft, out error), "The working-time " + unit + " draft is valid");
                    DateTimeOffset before = DateTimeOffset.Now;
                    long high = WorkCountdownCalculator.Remaining(draft, before);
                    app.RefreshGadgetPreview();
                    long low = WorkCountdownCalculator.Remaining(draft, DateTimeOffset.Now);
                    long gadget = Int64.Parse(Layouts.Find<TextBlock>(app.GadgetPreview, "RemainingNumber").Text, NumberStyles.Number, CultureInfo.CurrentCulture);
                    long embedded = Int64.Parse(Layouts.Find<TextBlock>(form, "SettingsPreviewNumber").Text, NumberStyles.Number, CultureInfo.CurrentCulture);
                    Check(gadget >= low && gadget <= high && embedded >= low && embedded <= high,
                        "Both actual previews use working time for " + unit);
                    Check(Layouts.Find<TextBlock>(app.GadgetPreview, "RemainingUnit").Text == HolidayApplication.UnitLabel(unit, gadget, true) + " to go" && Layouts.Find<TextBlock>(form, "SettingsPreviewUnit").Text == HolidayApplication.UnitLabel(unit, embedded, true) + " to go",
                        "Both " + unit + " previews explicitly identify working time");
                }
                workButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(Layouts.Find<CheckBox>(form, "WorkdayCountdown").IsChecked == false && app.GadgetPreview.Topmost == topmost,
                    "The preview Workdays button turns off the draft mode without changing always on top");
                Check(((SolidColorBrush)workButton.Background).Color.A == 0, "Workdays off has a clear utility-button background");
                workButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(Layouts.Find<CheckBox>(form, "WorkdayCountdown").IsChecked == true && AutomationProperties.GetName(workButton).Contains("on") && workButton.Content.ToString() == "Workdays",
                    "The preview Workdays button turns mode back on and exposes its state to accessibility");
                Check(((SolidColorBrush)workButton.Background).Color == ((SolidColorBrush)app.GadgetPreview.Resources["Accent"]).Color,
                    "Workdays on uses the selected scheme accent highlight");
                if (render)
                {
                    string images = Path.Combine(root, "previews"); Directory.CreateDirectory(images);
                    Layouts.Find<RadioButton>(form, "UnitDays").IsChecked = true; app.RefreshGadgetPreview();
                    Render(form, Path.Combine(images, "settings-workdays.png"));
                    Layouts.Find<Grid>(app.GadgetPreview, "GadgetHoverArea").RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount) { RoutedEvent = Mouse.MouseEnterEvent });
                    Render(app.GadgetPreview, Path.Combine(images, "gadget-workdays.png"), false, true);
                }
                Check(app.ReadForm(form, false, out draft, out error), "The working-time morning preview uses a valid draft");
                app.ShowGreeting(draft, DateTime.Now, false);
                long morningDays = WorkCountdownCalculator.Remaining(draft, DateTimeOffset.Now, CountdownUnit.Days);
                Check(Layouts.Find<TextBlock>(app.Greeting, "GreetingUnit").Text == HolidayApplication.UnitLabel(CountdownUnit.Days, morningDays, true) + " to go until",
                    "The morning readout visibly identifies workday counting");
                app.Greeting.Close();
                Layouts.Find<TextBox>(form, "WorkFinish").Text = "08:00";
                Check(!app.ReadForm(form, false, out draft, out error) && error.Contains("later"), "A finish before the start cannot be saved");
                Layouts.Find<TextBox>(form, "WorkFinish").Text = "17:00";
                Layouts.Find<DatePicker>(form, "HolidayDate").SelectedDate = new DateTime(2032, 1, 3);
                Check(!app.ReadForm(form, false, out draft, out error) && error.Contains("2031"), "Workday counting rejects dates beyond the supported NSW calendar");
                app.RefreshGadgetPreview();
                Check(Layouts.Find<TextBlock>(app.GadgetPreview, "RemainingNumber").Text == "—" && Layouts.Find<TextBlock>(form, "SettingsPreviewUnit").Text == "Calendar update needed",
                    "An unsupported working-time preview pauses visibly instead of guessing or crashing");
                Layouts.Find<CheckBox>(form, "WorkdayCountdown").IsChecked = false;
                Check(app.ReadForm(form, false, out draft, out error), "Elapsed counting still accepts future dates beyond the work calendar");
                // A one-minute shift already finished today leaves no working
                // time before midnight, but the future holiday has not arrived.
                if (DateTime.Now.TimeOfDay > TimeSpan.FromMinutes(1))
                {
                    Layouts.Find<DatePicker>(form, "HolidayDate").SelectedDate = DateTime.Now.Date.AddDays(1);
                    Layouts.Find<TextBox>(form, "HolidayTime").Text = "00:00";
                    Layouts.Find<TextBox>(form, "WorkStart").Text = "00:00";
                    Layouts.Find<TextBox>(form, "WorkFinish").Text = "00:01";
                    Layouts.Find<CheckBox>(form, "WorkdayCountdown").IsChecked = true;
                    app.RefreshGadgetPreview();
                    Check(Layouts.Find<TextBlock>(app.GadgetPreview, "RemainingNumber").Text == "0" && Layouts.Find<TextBlock>(app.GadgetPreview, "RemainingUnit").Text == "No work time left" && Layouts.Find<TextBlock>(form, "SettingsPreviewUnit").Text == "No work time left",
                        "Zero future working time never announces that the holiday has arrived");
                    Check(app.ReadForm(form, false, out draft, out error), "The zero-worktime future draft is valid");
                    app.ShowGreeting(draft, DateTime.Now, false);
                    Check(Layouts.Find<TextBlock>(app.Greeting, "GreetingNumber").Text == "0" && Layouts.Find<TextBlock>(app.Greeting, "GreetingIntro").Text == "No work time left before your holiday.",
                        "The morning readout uses workday Days and preserves the actual arrival instant");
                    app.Greeting.Close();
                }
            }
            finally
            {
                if (app.Greeting != null) app.Greeting.Close();
                if (app.GadgetPreview != null) app.GadgetPreview.Close();
                HolidayApplication.FillForm(form, original);
                app.RefreshGadgetPreview();
            }
            Check((File.Exists(store.FilePath) ? File.ReadAllText(store.FilePath) : null) == saved && !app.Settings.WorkdayCountdown,
                "Workday previews and validation leave saved settings untouched");
        }

        private static void CheckDirectWorkSwitches(HolidayApplication app, SettingsStore store)
        {
            Button button = Layouts.Find<Button>(app.Widget, "WorkdayButton");
            bool topmost = app.Widget.Topmost;
            app.Settings.WorkStart = "09:00"; app.Settings.WorkFinish = "17:00";
            Check(!app.Settings.WorkdayCountdown && app.SettingsWindow == null, "Direct Workdays switching starts with the saved gadget and settings closed");
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(app.Settings.WorkdayCountdown && store.Load().WorkdayCountdown && app.SettingsWindow == null && app.Widget.Topmost == topmost,
                "The gadget Workdays button saves its mode immediately without changing window behaviour");
            AppSettings saved = store.Load();
            long high = WorkCountdownCalculator.Remaining(saved, DateTimeOffset.Now);
            app.RefreshCountdown();
            long low = WorkCountdownCalculator.Remaining(saved, DateTimeOffset.Now);
            long value = Int64.Parse(Layouts.Find<TextBlock>(app.Widget, "RemainingNumber").Text, NumberStyles.Number, CultureInfo.CurrentCulture);
            Check(value >= low && value <= high && AutomationProperties.GetName(button).Contains("on"),
                "The saved gadget uses working time and presents its enabled state");
            Check(Layouts.Find<TextBlock>(app.Widget, "RemainingUnit").Text == "working seconds to go", "The saved gadget explicitly labels working seconds when Workdays is enabled");
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(!app.Settings.WorkdayCountdown && !store.Load().WorkdayCountdown && app.SettingsWindow == null && app.Widget.Topmost == topmost,
                "The gadget Workdays button saves a return to ordinary elapsed counting");
        }
    }
}
