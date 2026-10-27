using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace HolidayTimer
{
    internal static partial class UiChecks
    {
        private static void CheckPublicHolidayControls(HolidayApplication app, Window form, SettingsStore store, string root, bool render)
        {
            SettingsLayout.SelectSection(form, "CountdownTab");
            Button open = Layouts.Find<Button>(form, "PublicHolidayPickerButton");
            Popup popup = Layouts.Find<Popup>(form, "PublicHolidayPopup");
            ComboBox choices = Layouts.Find<ComboBox>(form, "UpcomingPublicHolidays");
            Button refresh = Layouts.Find<Button>(form, "RefreshPublicHolidays");
            Button use = Layouts.Find<Button>(form, "UsePublicHoliday");
            TextBlock status = Layouts.Find<TextBlock>(form, "PublicHolidayRefreshStatus");
            string name = Layouts.Find<TextBox>(form, "HolidayName").Text;
            DateTime? date = Layouts.Find<DatePicker>(form, "HolidayDate").SelectedDate;
            string time = Layouts.Find<TextBox>(form, "HolidayTime").Text;
            string savedName = app.Settings.HolidayName, savedTarget = app.Settings.TargetLocal;
            string file = File.Exists(store.FilePath) ? File.ReadAllText(store.FilePath) : null;
            Func<string, PublicHolidaySnapshot> previousProvider = app.PublicHolidayRefreshProvider;
            ManualResetEvent release = new ManualResetEvent(false);
            try
            {
                Check(!popup.IsOpen && !String.IsNullOrEmpty(AutomationProperties.GetName(open)), "Upcoming public holidays are available from a named control without occupying the settings layout");
                open.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(WaitFor(delegate { return popup.IsOpen && choices.IsVisible && choices.Items.Count > 20; }), "The actual popup exposes several years of upcoming named NSW public holidays");
                Check(AutomationProperties.GetLiveSetting(status) == AutomationLiveSetting.Polite, "Refresh status is a polite accessible live region");
                bool estimated = false;
                foreach (ComboBoxItem item in choices.Items)
                {
                    NswPublicHoliday holiday = item.Tag as NswPublicHoliday;
                    Check(holiday != null && holiday.Date > DateTime.Now, "Upcoming choices contain only future midnight targets");
                    if (!holiday.IsOfficial)
                    {
                        estimated = true;
                        Check(AutomationProperties.GetName(item).Contains("estimated"), "Unpublished future dates identify their estimate in the accessible option name");
                    }
                }
                Check(estimated && Layouts.Find<TextBlock>(form, "PublicHolidayCoverage").Text.Contains("estimated"), "The picker distinguishes projected years from published official dates");
                NswPublicHoliday originalHoliday = ((ComboBoxItem)choices.Items[0]).Tag as NswPublicHoliday;
                DateTime revisedDate = originalHoliday.Date.AddDays(-1);
                while (revisedDate > DateTime.Today && (revisedDate.Year != originalHoliday.Date.Year || NswPublicHolidays.Find(revisedDate) != null)) revisedDate = revisedDate.AddDays(-1);
                Check(revisedDate > DateTime.Today && revisedDate.Year == originalHoliday.Date.Year, "Fixture found a free future correction date");
                Layouts.Find<Button>(form, "EditPublicHoliday").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(Layouts.Find<StackPanel>(form, "PublicHolidayEditPanel").IsVisible && !refresh.IsEnabled,
                    "Editing exposes a clear shared-calendar editor without allowing concurrent refresh");
                DatePicker editDate = Layouts.Find<DatePicker>(form, "PublicHolidayEditDate");
                editDate.IsDropDownOpen = true;
                Check(WaitFor(delegate { return popup.IsOpen && editDate.IsDropDownOpen && CalendarPicker.GetCalendar(editDate) != null; }),
                    "The holiday correction calendar opens without dismissing its parent editor");
                editDate.IsDropDownOpen = false;
                editDate.SelectedDate = ((NswPublicHoliday)((ComboBoxItem)choices.Items[1]).Tag).Date;
                Layouts.Find<Button>(form, "SavePublicHolidayEdit").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(Layouts.Find<StackPanel>(form, "PublicHolidayEditPanel").IsVisible &&
                    Layouts.Find<TextBlock>(form, "PublicHolidayEditError").Text.Contains("Another holiday"),
                    "A conflicting correction keeps the editor open with an actionable error");
                Layouts.Find<TextBox>(form, "PublicHolidayEditName").Text = "Adjusted " + originalHoliday.Name;
                editDate.SelectedDate = revisedDate;
                Check(Layouts.Find<TextBlock>(form, "PublicHolidayEditError").Text == "", "Editing a failed correction clears its stale error");
                if (render)
                {
                    string images = Path.Combine(root, "previews"); Directory.CreateDirectory(images);
                    RenderPublicHolidayPopup(Layouts.Find<Border>(form, "PublicHolidayPopupSurface"), Path.Combine(images, "public-holidays-editor-dark.png"));
                }
                Layouts.Find<Button>(form, "SavePublicHolidayEdit").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                NswPublicHoliday corrected = NswPublicHolidays.Find(revisedDate);
                Check(corrected != null && corrected.IsEdited && corrected.Key == originalHoliday.Key && !corrected.IsOfficial &&
                    NswPublicHolidays.Find(originalHoliday.Date) == null && NswWorkCalendar.IsPublicHoliday(revisedDate) && !NswWorkCalendar.IsPublicHoliday(originalHoliday.Date),
                    "Saving an edit changes the one shared calendar and its workday exclusion");
                Check(app.Settings.AutomaticPublicHoliday && app.Settings.PublicHolidayKey == originalHoliday.Key &&
                    CountdownCalculator.ParseTarget(app.Settings.TargetLocal).Date == revisedDate && app.Settings.HolidayName == corrected.Name,
                    "The automatic countdown follows the corrected shared holiday");
                Check(NswPublicHolidays.Initialize(app.PublicHolidayCacheDirectory) == null && NswPublicHolidays.Find(revisedDate).IsEdited,
                    "A saved holiday edit reloads from the isolated calendar cache");
                app.PublicHolidaysRefreshed();
                Layouts.Find<Button>(form, "EditPublicHoliday").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(Layouts.Find<Button>(form, "ResetPublicHolidayEdit").IsEnabled, "An edited holiday can be restored to its source date");
                Layouts.Find<Button>(form, "ResetPublicHolidayEdit").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(NswPublicHolidays.Find(originalHoliday.Date) != null && NswPublicHolidays.Find(revisedDate) == null &&
                    app.Settings.TargetLocal == savedTarget && app.Settings.HolidayName == savedName,
                    "Reset restores the source holiday, workday calendar and automatic countdown");
                choices.SelectedIndex = Math.Min(1, choices.Items.Count - 1);
                NswPublicHoliday selected = ((ComboBoxItem)choices.SelectedItem).Tag as NswPublicHoliday;
                use.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(!popup.IsOpen && Layouts.Find<TextBox>(form, "HolidayName").Text == selected.Name &&
                    Layouts.Find<DatePicker>(form, "HolidayDate").SelectedDate == selected.Date && Layouts.Find<TextBox>(form, "HolidayTime").Text == "00:00",
                    "Use holiday fills the real name, date and midnight time then closes only its popup");
                Check(app.Settings.HolidayName == savedName && app.Settings.TargetLocal == savedTarget &&
                    (File.Exists(store.FilePath) ? File.ReadAllText(store.FilePath) : null) == file,
                    "Choosing a public holiday stays in the draft until the main Save countdown action");

                open.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(WaitFor(delegate { return popup.IsOpen; }), "The public-holiday picker can reopen");
                app.PublicHolidayRefreshProvider = delegate { throw new IOException("Synthetic offline result"); };
                int count = choices.Items.Count;
                refresh.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(WaitFor(delegate { return refresh.IsEnabled && status.Text.Contains("Couldn’t refresh"); }), "An offline refresh explains recovery and re-enables retry");
                Check(choices.Items.Count == count && popup.IsOpen && use.IsEnabled, "Failed refresh keeps cached holidays and selection usable");
                app.PublicHolidayRefreshProvider = delegate { return NswPublicHolidays.Current; };
                refresh.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(WaitFor(delegate { return refresh.IsEnabled && status.Text.Contains("dates refreshed"); }), "A successful user refresh updates status through the real dispatcher path");
                Check(choices.Items.Count == count && app.Settings.HolidayName == savedName && app.Settings.TargetLocal == savedTarget,
                    "Refreshing calendar data does not silently commit the settings draft");
                if (render)
                {
                    string images = Path.Combine(root, "previews"); Directory.CreateDirectory(images);
                    RenderPublicHolidayPopup(Layouts.Find<Border>(form, "PublicHolidayPopupSurface"), Path.Combine(images, "public-holidays-popup-dark.png"));
                    Layouts.Find<RadioButton>(form, "AppearanceLight").IsChecked = true;
                    Check(WaitFor(delegate { return (bool)form.Resources["LightAppearance"]; }), "Public-holiday controls follow the selected light appearance");
                    Check(((SolidColorBrush)Layouts.Find<TextBlock>(form, "PublicHolidayEditError").Foreground).Color == Color.FromRgb(161, 45, 37),
                        "Holiday validation uses a readable dark error colour in Light appearance");
                    RenderPublicHolidayPopup(Layouts.Find<Border>(form, "PublicHolidayPopupSurface"), Path.Combine(images, "public-holidays-popup-light.png"));
                    Layouts.Find<Button>(form, "EditPublicHoliday").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    RenderPublicHolidayPopup(Layouts.Find<Border>(form, "PublicHolidayPopupSurface"), Path.Combine(images, "public-holidays-editor-light.png"));
                    Layouts.Find<Button>(form, "CancelPublicHolidayEdit").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Layouts.Find<RadioButton>(form, "AppearanceDark").IsChecked = true;
                    Check(WaitFor(delegate { return !(bool)form.Resources["LightAppearance"]; }), "The public-holiday fixture restores the dark appearance");
                }

                app.PublicHolidayRefreshProvider = delegate { release.WaitOne(); return NswPublicHolidays.Current; };
                refresh.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(!refresh.IsEnabled && status.Text.Contains("Refreshing"), "A pending refresh has an explicit busy state and cannot be duplicated");
                Layouts.Find<Button>(form, "ClosePublicHolidayPopup").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(!popup.IsOpen && form.IsVisible, "Closing while refreshing leaves the settings window open");
                release.Set();
                Check(WaitFor(delegate { return refresh.IsEnabled; }), "Refresh completion safely returns the cached calendar after its popup closes");
                Check(!popup.IsOpen, "Late refresh completion does not reopen the picker or steal focus");

                open.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(WaitFor(delegate { return popup.IsOpen; }), "The picker remains available after closing a refresh");
                KeyEventArgs escape = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(form), Environment.TickCount, Key.Escape)
                { RoutedEvent = Keyboard.PreviewKeyDownEvent };
                Layouts.Find<Border>(form, "PublicHolidayPopupSurface").RaiseEvent(escape);
                Check(escape.Handled && !popup.IsOpen && form.IsVisible, "Escape dismisses public holidays without triggering main Cancel");
                Check(WaitFor(delegate { return open.IsKeyboardFocused; }), "Dismissing the picker returns keyboard focus to its trigger");
            }
            finally
            {
                release.Set();
                WaitFor(delegate { return refresh.IsEnabled; });
                app.PublicHolidayRefreshProvider = previousProvider;
                popup.IsOpen = false;
                Layouts.Find<TextBox>(form, "HolidayName").Text = name;
                Layouts.Find<DatePicker>(form, "HolidayDate").SelectedDate = date;
                Layouts.Find<TextBox>(form, "HolidayTime").Text = time;
                app.RefreshGadgetPreview();
                release.Dispose();
            }
        }

        private static void RenderPublicHolidayPopup(FrameworkElement element, string path)
        {
            element.UpdateLayout();
            int width = Math.Max(1, (int)Math.Ceiling(element.ActualWidth));
            int height = Math.Max(1, (int)Math.Ceiling(element.ActualHeight));
            RenderTargetBitmap bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(element);
            PngBitmapEncoder encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (FileStream stream = File.Create(path)) encoder.Save(stream);
        }
    }
}
