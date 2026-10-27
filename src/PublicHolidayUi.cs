using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace HolidayTimer
{
    internal sealed partial class HolidayApplication
    {
        // The test delegate supplies immutable snapshots, so interface checks
        // can exercise the real waiting/error/close paths without networking.
        internal Func<string, PublicHolidaySnapshot> PublicHolidayRefreshProvider;
        private bool publicHolidayRefreshBusy;
        private string publicHolidayRefreshMessage;
        private string draftPublicHolidayKey;
        private bool rebuildingHolidayChoices;

        private void BindPublicHolidayForm(Window form)
        {
            draftPublicHolidayKey = null;
            Popup popup = Layouts.Find<Popup>(form, "PublicHolidayPopup");
            Button trigger = Layouts.Find<Button>(form, "PublicHolidayPickerButton");
            trigger.Click += delegate
            {
                RefreshPublicHolidayForm(form);
                Border surface = Layouts.Find<Border>(form, "PublicHolidayPopupSurface");
                surface.Width = Math.Min(380, Math.Max(260, SystemParameters.WorkArea.Width - 32));
                surface.MaxHeight = Math.Max(200, SystemParameters.WorkArea.Height - 32);
                popup.IsOpen = true;
                form.Dispatcher.BeginInvoke(new Action(delegate
                {
                    if (SettingsWindow == form && popup.IsOpen)
                        Layouts.Find<ComboBox>(form, "UpcomingPublicHolidays").Focus();
                }));
            };
            Layouts.Find<Button>(form, "ClosePublicHolidayPopup").Click += delegate { ClosePublicHolidayPopup(form, true); };
            popup.Closed += delegate { Layouts.Find<StackPanel>(form, "PublicHolidayEditPanel").Visibility = Visibility.Collapsed; if (SettingsWindow == form && form.IsVisible) trigger.Focus(); };
            Layouts.Find<Border>(form, "PublicHolidayPopupSurface").PreviewKeyDown += delegate(object sender, KeyEventArgs args)
            {
                if (args.Key != Key.Escape) return;
                args.Handled = true;
                if (Layouts.Find<StackPanel>(form, "PublicHolidayEditPanel").Visibility == Visibility.Visible)
                { ClosePublicHolidayEditor(form); Layouts.Find<Button>(form, "EditPublicHoliday").Focus(); }
                else ClosePublicHolidayPopup(form, true);
            };
            Layouts.Find<ComboBox>(form, "UpcomingPublicHolidays").SelectionChanged += delegate
            {
                if (rebuildingHolidayChoices) return;
                ClosePublicHolidayEditor(form);
                NswPublicHoliday choice = SelectedPublicHoliday(form);
                Layouts.Find<Button>(form, "UsePublicHoliday").IsEnabled = choice != null && choice.Date > DateTime.Now;
                Layouts.Find<Button>(form, "EditPublicHoliday").IsEnabled = choice != null && !publicHolidayRefreshBusy;
            };
            Layouts.Find<Button>(form, "UsePublicHoliday").Click += delegate
            {
                NswPublicHoliday selected = SelectedPublicHoliday(form);
                if (selected == null || selected.Date <= DateTime.Now) return;
                draftPublicHolidayKey = selected.Key;
                Layouts.Find<TextBox>(form, "HolidayName").Text = selected.Name;
                Layouts.Find<DatePicker>(form, "HolidayDate").SelectedDate = selected.Date;
                Layouts.Find<TextBox>(form, "HolidayTime").Text = "00:00";
                CalendarPicker.Refresh(Layouts.Find<DatePicker>(form, "HolidayDate"));
                QueuePreviewUpdate();
                ClosePublicHolidayPopup(form, true);
            };
            CalendarPicker.Attach(Layouts.Find<DatePicker>(form, "PublicHolidayEditDate"));
            Layouts.Find<TextBox>(form, "PublicHolidayEditName").TextChanged += delegate { Layouts.Find<TextBlock>(form, "PublicHolidayEditError").Text = ""; };
            Layouts.Find<DatePicker>(form, "PublicHolidayEditDate").SelectedDateChanged += delegate { Layouts.Find<TextBlock>(form, "PublicHolidayEditError").Text = ""; };
            Layouts.Find<DatePicker>(form, "PublicHolidayEditDate").AddHandler(TextBox.TextChangedEvent,
                new TextChangedEventHandler(delegate { Layouts.Find<TextBlock>(form, "PublicHolidayEditError").Text = ""; }));
            Layouts.Find<Button>(form, "EditPublicHoliday").Click += delegate
            {
                NswPublicHoliday selected = SelectedPublicHoliday(form);
                if (selected == null || publicHolidayRefreshBusy) return;
                Layouts.Find<TextBox>(form, "PublicHolidayEditName").Text = selected.Name;
                Layouts.Find<DatePicker>(form, "PublicHolidayEditDate").SelectedDate = selected.Date;
                Layouts.Find<TextBlock>(form, "PublicHolidayEditError").Text = "";
                Layouts.Find<Button>(form, "ResetPublicHolidayEdit").IsEnabled = selected.IsEdited;
                Layouts.Find<StackPanel>(form, "PublicHolidayEditPanel").Visibility = Visibility.Visible;
                Layouts.Find<Button>(form, "RefreshPublicHolidays").IsEnabled = false;
                form.Dispatcher.BeginInvoke(new Action(delegate { if (popup.IsOpen) Layouts.Find<TextBox>(form, "PublicHolidayEditName").Focus(); }));
            };
            Layouts.Find<Button>(form, "CancelPublicHolidayEdit").Click += delegate { ClosePublicHolidayEditor(form); Layouts.Find<Button>(form, "EditPublicHoliday").Focus(); };
            Layouts.Find<Button>(form, "SavePublicHolidayEdit").Click += delegate { SavePublicHolidayEdit(form, false); };
            Layouts.Find<Button>(form, "ResetPublicHolidayEdit").Click += delegate { SavePublicHolidayEdit(form, true); };
            Layouts.Find<Button>(form, "RefreshPublicHolidays").Click += delegate { RefreshPublicHolidaysFromSource(); };
            Layouts.Find<Button>(form, "PublicHolidaySource").Click += delegate
            {
                try { Process.Start(NswPublicHolidays.SourceUrl); }
                catch { Layouts.Find<TextBlock>(form, "PublicHolidayRefreshStatus").Text = "The source page could not open. Your saved dates are still available."; }
            };
            form.Closed += delegate { popup.IsOpen = false; };
            RefreshPublicHolidayForm(form);
        }

        private string PublicHolidayKeyForDraft(string name, string target)
        {
            string key = draftPublicHolidayKey ?? Settings.PublicHolidayKey;
            NswPublicHoliday holiday = NswPublicHolidays.Current.FindByKey(key);
            return holiday != null && holiday.Name == name && holiday.Date == CountdownCalculator.ParseTarget(target).Date ? key : null;
        }

        private void UpdateSelectedHolidayDraft(Window form)
        {
            if (String.IsNullOrEmpty(draftPublicHolidayKey)) return;
            NswPublicHoliday before = SelectedPublicHoliday(form);
            NswPublicHoliday after = NswPublicHolidays.Current.FindByKey(draftPublicHolidayKey);
            if (before == null || after == null || before.Key != draftPublicHolidayKey ||
                Layouts.Find<TextBox>(form, "HolidayName").Text.Trim() != before.Name ||
                Layouts.Find<DatePicker>(form, "HolidayDate").SelectedDate != before.Date) return;
            Layouts.Find<TextBox>(form, "HolidayName").Text = after.Name;
            Layouts.Find<DatePicker>(form, "HolidayDate").SelectedDate = after.Date;
            QueuePreviewUpdate();
        }

        private void ClosePublicHolidayEditor(Window form)
        {
            Layouts.Find<StackPanel>(form, "PublicHolidayEditPanel").Visibility = Visibility.Collapsed;
            Layouts.Find<Button>(form, "RefreshPublicHolidays").IsEnabled = !publicHolidayRefreshBusy;
        }

        private void SavePublicHolidayEdit(Window form, bool reset)
        {
            NswPublicHoliday selected = SelectedPublicHoliday(form);
            if (selected == null || publicHolidayRefreshBusy) return;
            DateTime typedDate;
            DateTime? date = null;
            if (!reset)
            {
                DatePicker picker = Layouts.Find<DatePicker>(form, "PublicHolidayEditDate");
                if (!DateTime.TryParse(picker.Text, CultureInfo.CurrentCulture, DateTimeStyles.None, out typedDate))
                { Layouts.Find<TextBlock>(form, "PublicHolidayEditError").Text = "Choose a valid holiday date."; return; }
                date = typedDate.Date;
            }
            try
            {
                NswPublicHolidays.SaveCorrection(PublicHolidayCacheDirectory, selected.Key, date, Layouts.Find<TextBox>(form, "PublicHolidayEditName").Text);
                publicHolidayRefreshMessage = reset ? "Holiday restored to its source date." : "Holiday edit saved for the countdown and workday calendar.";
                ClosePublicHolidayEditor(form);
                PublicHolidaysRefreshed();
                Layouts.Find<Button>(form, "EditPublicHoliday").Focus();
            }
            catch (Exception error)
            {
                if (!(error is ArgumentException) && !(error is System.IO.IOException) && !(error is UnauthorizedAccessException)) throw;
                Layouts.Find<TextBlock>(form, "PublicHolidayEditError").Text = error.Message;
            }
        }

        private static NswPublicHoliday SelectedPublicHoliday(Window form)
        {
            ComboBoxItem item = Layouts.Find<ComboBox>(form, "UpcomingPublicHolidays").SelectedItem as ComboBoxItem;
            return item == null ? null : item.Tag as NswPublicHoliday;
        }

        private void ClosePublicHolidayPopup(Window form, bool restoreFocus)
        {
            Layouts.Find<Popup>(form, "PublicHolidayPopup").IsOpen = false;
            if (restoreFocus && SettingsWindow == form && form.IsVisible)
                Layouts.Find<Button>(form, "PublicHolidayPickerButton").Focus();
        }

        internal void RefreshPublicHolidayForm(Window form)
        {
            PublicHolidaySnapshot snapshot = NswPublicHolidays.Current;
            ComboBox choices = Layouts.Find<ComboBox>(form, "UpcomingPublicHolidays");
            NswPublicHoliday previous = SelectedPublicHoliday(form);
            rebuildingHolidayChoices = true;
            choices.Items.Clear();
            ComboBoxItem selected = null;
            foreach (NswPublicHoliday holiday in snapshot.Entries)
            {
                if (holiday.Date <= DateTime.Now && !(holiday.IsEdited && holiday.SourceDate > DateTime.Now)) continue;
                if (choices.Items.Count >= 90) break;
                string date = holiday.Date.ToString("ddd, d MMM yyyy", CultureInfo.CurrentCulture);
                string qualification = holiday.IsEdited ? " · edited" : holiday.IsOfficial ? "" : " · estimated";
                StackPanel content = new StackPanel();
                TextBlock name = new TextBlock { Text = holiday.Name, FontSize = 13, FontWeight = FontWeights.Medium,
                    TextTrimming = TextTrimming.CharacterEllipsis };
                name.SetResourceReference(TextBlock.ForegroundProperty, "Heading");
                TextBlock details = new TextBlock { Text = date + qualification, FontSize = 11, Margin = new Thickness(0, 3, 0, 0) };
                details.SetResourceReference(TextBlock.ForegroundProperty, "MutedText");
                content.Children.Add(name); content.Children.Add(details);
                ComboBoxItem item = new ComboBoxItem { Content = content, Tag = holiday,
                    ToolTip = holiday.Name + " · " + date + qualification };
                System.Windows.Automation.AutomationProperties.SetName(item, holiday.Name + ", " + date + qualification);
                choices.Items.Add(item);
                if (previous != null && previous.Key == holiday.Key) selected = item;
            }
            choices.SelectedItem = selected ?? (choices.Items.Count == 0 ? null : choices.Items[0]);
            rebuildingHolidayChoices = false;
            NswPublicHoliday choice = SelectedPublicHoliday(form);
            Layouts.Find<Button>(form, "UsePublicHoliday").IsEnabled = choice != null && choice.Date > DateTime.Now;
            Layouts.Find<Button>(form, "EditPublicHoliday").IsEnabled = choice != null && !publicHolidayRefreshBusy;
            Layouts.Find<Button>(form, "RefreshPublicHolidays").IsEnabled = !publicHolidayRefreshBusy && Layouts.Find<StackPanel>(form, "PublicHolidayEditPanel").Visibility != Visibility.Visible;
            Layouts.Find<TextBlock>(form, "PublicHolidayCoverage").Text = "Official dates through " + snapshot.OfficialThroughYear +
                ". Later dates through " + snapshot.ThroughYear + " are estimated and may change.";
            string fallback = snapshot.RefreshedUtc.HasValue ? "Last refreshed " + snapshot.RefreshedUtc.Value.ToLocalTime().ToString("d MMM yyyy, HH:mm", CultureInfo.CurrentCulture) + ". Cached dates are available offline." :
                "Built-in dates are ready offline. Refresh checks the NSW Government.";
            Layouts.Find<TextBlock>(form, "PublicHolidayRefreshStatus").Text = publicHolidayRefreshBusy ? "Refreshing official NSW dates…" : publicHolidayRefreshMessage ?? fallback;
        }

        private void RefreshPublicHolidaysFromSource()
        {
            if (publicHolidayRefreshBusy || quitting) return;
            if (!desktopIntegration && PublicHolidayRefreshProvider == null)
            {
                publicHolidayRefreshMessage = "Refresh is available in the installed app. Built-in dates are ready offline.";
                if (SettingsWindow != null) RefreshPublicHolidayForm(SettingsWindow);
                return;
            }
            publicHolidayRefreshBusy = true;
            if (SettingsWindow != null) RefreshPublicHolidayForm(SettingsWindow);
            string directory = PublicHolidayCacheDirectory;
            Func<string, PublicHolidaySnapshot> operation = PublicHolidayRefreshProvider ?? new Func<string, PublicHolidaySnapshot>(NswPublicHolidays.Refresh);
            ThreadPool.QueueUserWorkItem(delegate
            {
                bool refreshed = false;
                try { operation(directory); refreshed = true; }
                catch { /* Keep the last validated immutable snapshot and selection. */ }
                if (Dispatcher.HasShutdownStarted) return;
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    publicHolidayRefreshBusy = false;
                    if (quitting) return;
                    publicHolidayRefreshMessage = refreshed ? "Official NSW dates refreshed. Your cached calendar is ready offline." :
                        "Couldn’t refresh NSW dates. Your existing calendar is unchanged. Check your connection and try again.";
                    if (refreshed) PublicHolidaysRefreshed();
                    if (SettingsWindow != null) RefreshPublicHolidayForm(SettingsWindow);
                }));
            });
        }
    }
}
