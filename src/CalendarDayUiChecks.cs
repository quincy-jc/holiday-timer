using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace HolidayTimer
{
    internal static partial class UiChecks
    {
        private static void CheckCalendarDayDisplays(HolidayApplication app, SettingsStore store)
        {
            string saved = File.ReadAllText(store.FilePath);
            app.OpenSettings(); Window form = app.SettingsWindow;
            try
            {
                Layouts.Find<CheckBox>(form, "WorkdayCountdown").IsChecked = false;
                Layouts.Find<ComboBox>(form, "CountdownUnit").SelectedIndex = (int)CountdownUnit.Days;
                Layouts.Find<TextBox>(form, "HolidayTime").Text = "23:59";
                foreach (int days in new[] { 0, 1, 2 })
                {
                    Layouts.Find<DatePicker>(form, "HolidayDate").SelectedDate = DateTime.Today.AddDays(days);
                    app.ShowGadgetPreview(form); app.RefreshGadgetPreview();
                    string expected = days.ToString();
                    Check(Layouts.Find<TextBlock>(app.GadgetPreview, "RemainingNumber").Text == expected,
                        "The actual gadget renderer shows " + days + " calendar days regardless of departure time");
                    Check(Layouts.Find<TextBlock>(form, "SettingsPreviewNumber").Text == expected,
                        "The settings preview uses the same calendar day count");
                    Layouts.Find<Button>(form, "PreviewGreeting").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Check(app.Greeting != null && Layouts.Find<TextBlock>(app.Greeting, "GreetingNumber").Text == expected,
                        "The celebration text uses the same calendar day count");
                    app.Greeting.Close();
                }
            }
            finally { if (app.Greeting != null) app.Greeting.Close(); form.Close(); }
            Check(File.ReadAllText(store.FilePath) == saved, "Calendar day previews do not change the saved countdown");
        }
    }
}
