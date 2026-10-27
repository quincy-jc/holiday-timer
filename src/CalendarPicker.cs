using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using Calendar = System.Windows.Controls.Calendar;

namespace HolidayTimer
{
    // Styling and date annotations surround the real WPF calendar. Native
    // selection, typed dates, arrow keys and month/year/decade navigation stay
    // with DatePicker and Calendar; no second selection owner is introduced.
    public static class CalendarPicker
    {
        public static readonly DependencyProperty IsWeekendProperty = DependencyProperty.RegisterAttached("IsWeekend", typeof(bool), typeof(CalendarPicker), new FrameworkPropertyMetadata(false));
        public static readonly DependencyProperty IsHolidayProperty = DependencyProperty.RegisterAttached("IsHoliday", typeof(bool), typeof(CalendarPicker), new FrameworkPropertyMetadata(false));
        public static readonly DependencyProperty SelectionSummaryProperty = DependencyProperty.RegisterAttached("SelectionSummary", typeof(string), typeof(CalendarPicker), new FrameworkPropertyMetadata("Choose your celebration date"));
        public static readonly DependencyProperty CalendarNoticeProperty = DependencyProperty.RegisterAttached("CalendarNotice", typeof(string), typeof(CalendarPicker), new FrameworkPropertyMetadata("NSW public holidays"));
        private static readonly DependencyProperty StateProperty = DependencyProperty.RegisterAttached("State", typeof(State), typeof(CalendarPicker), new PropertyMetadata(null));
        public static bool GetIsWeekend(DependencyObject value) { return (bool)value.GetValue(IsWeekendProperty); }
        public static void SetIsWeekend(DependencyObject value, bool state) { value.SetValue(IsWeekendProperty, state); }
        public static bool GetIsHoliday(DependencyObject value) { return (bool)value.GetValue(IsHolidayProperty); }
        public static void SetIsHoliday(DependencyObject value, bool state) { value.SetValue(IsHolidayProperty, state); }
        public static string GetSelectionSummary(DependencyObject value) { return (string)value.GetValue(SelectionSummaryProperty); }
        public static void SetSelectionSummary(DependencyObject value, string text) { value.SetValue(SelectionSummaryProperty, text); }
        public static string GetCalendarNotice(DependencyObject value) { return (string)value.GetValue(CalendarNoticeProperty); }
        public static void SetCalendarNotice(DependencyObject value, string text) { value.SetValue(CalendarNoticeProperty, text); }

        internal static void Attach(DatePicker picker)
        {
            if (picker == null) throw new ArgumentNullException("picker");
            if (picker.GetValue(StateProperty) != null) return;
            ResourceDictionary resources;
            using (Stream source = Assembly.GetExecutingAssembly().GetManifestResourceStream("HolidayTimer.CalendarStyles.xaml"))
            {
                if (source == null) throw new InvalidOperationException("The calendar layout is missing.");
                resources = (ResourceDictionary)XamlReader.Load(source);
            }
            picker.Resources.MergedDictionaries.Add(resources);
            picker.CalendarStyle = (Style)resources["CelebrationCalendar"];
            picker.Style = (Style)resources["CelebrationDatePicker"];
            // A Monday-first week makes the two weekend columns contiguous.
            picker.FirstDayOfWeek = DayOfWeek.Monday;
            State state = new State(picker);
            picker.SetValue(StateProperty, state);
            picker.CalendarOpened += state.Opened;
            picker.SelectedDateChanged += state.SelectionChanged;
            picker.Loaded += state.Loaded;
            picker.Unloaded += state.Unloaded;
            Refresh(picker);
        }

        internal static void Refresh(DatePicker picker)
        {
            State state = picker == null ? null : picker.GetValue(StateProperty) as State;
            if (state == null) return;
            picker.ApplyTemplate();
            Calendar calendar = GetCalendar(picker);
            if (calendar == null) return;
            state.Use(calendar);
            calendar.ApplyTemplate();
            Annotate(calendar);
            state.Queue();
        }

        internal static Calendar GetCalendar(DatePicker picker)
        {
            if (picker.Template == null) return null;
            Popup popup = picker.Template.FindName("PART_Popup", picker) as Popup;
            return popup == null ? null : FindCalendar(popup.Child);
        }

        private static Calendar FindCalendar(DependencyObject value)
        {
            Calendar found = value as Calendar;
            if (found != null) return found;
            if (value == null) return null;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(value); i++)
            { found = FindCalendar(VisualTreeHelper.GetChild(value, i)); if (found != null) return found; }
            return null;
        }

        internal static IList<CalendarDayButton> DayButtons(Calendar calendar)
        {
            List<CalendarDayButton> result = new List<CalendarDayButton>();
            CollectDays(calendar, result);
            return result;
        }
        private static void CollectDays(DependencyObject value, List<CalendarDayButton> result)
        {
            if (value == null) return;
            CalendarDayButton button = value as CalendarDayButton;
            if (button != null) { result.Add(button); return; }
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(value); i++) CollectDays(VisualTreeHelper.GetChild(value, i), result);
        }

        internal static void Annotate(Calendar calendar)
        {
            PublicHolidaySnapshot snapshot = NswPublicHolidays.Current;
            bool supported = Supported(calendar.DisplayDate, snapshot);
            string coverage = "NSW public holidays · " + snapshot.FromYear + "–" + snapshot.ThroughYear;
            if (calendar.DisplayDate.Year > snapshot.OfficialThroughYear)
                coverage += "\nLater dates are estimated. Refresh NSW holidays for official updates.";
            SetCalendarNotice(calendar, supported ? coverage : "NSW holiday dates are unavailable for this year.\nYou can still choose any celebration date.");
            SetSelectionSummary(calendar, calendar.SelectedDate.HasValue
                ? "Celebration: " + calendar.SelectedDate.Value.ToString("ddd, d MMM yyyy", CultureInfo.CurrentCulture)
                : "Choose your celebration date");
            foreach (CalendarDayButton button in DayButtons(calendar))
            {
                if (!(button.DataContext is DateTime))
                { SetIsWeekend(button, false); SetIsHoliday(button, false); button.ToolTip = null; continue; }
                DateTime date = (DateTime)button.DataContext;
                bool weekend = date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;
                NswPublicHoliday entry = Supported(date, snapshot) ? snapshot.Find(date) : null;
                string holiday = entry == null ? null : entry.Name;
                SetIsWeekend(button, weekend); SetIsHoliday(button, !String.IsNullOrEmpty(holiday));
                string label = date.ToString("dddd, d MMMM yyyy", CultureInfo.CurrentCulture);
                if (!String.IsNullOrEmpty(holiday)) label += " · " + holiday + " · NSW public holiday" + (entry.IsEdited ? " (edited)" : entry.IsOfficial ? "" : " (estimated)");
                if (weekend) label += " · Weekend";
                if (calendar.SelectedDate.HasValue && date.Date == calendar.SelectedDate.Value.Date) label += " · Your celebration";
                if (date.Date == DateTime.Today) label += " · Today";
                if (!Supported(date, snapshot)) label += " · NSW holiday dates unavailable";
                button.ToolTip = label;
                ToolTipService.SetInitialShowDelay(button, 350);
                ToolTipService.SetBetweenShowDelay(button, 0);
                AutomationProperties.SetName(button, label);
            }
        }
        private static bool Supported(DateTime date, PublicHolidaySnapshot snapshot) { return date.Year >= snapshot.FromYear && date.Year <= snapshot.ThroughYear; }

        private sealed class State
        {
            private readonly DatePicker picker;
            private Calendar calendar;
            private DispatcherOperation pending;
            internal State(DatePicker picker) { this.picker = picker; }
            internal void Use(Calendar next)
            {
                if (calendar == next) return;
                Detach(); calendar = next;
                calendar.Loaded += CalendarLoaded;
                calendar.DisplayDateChanged += DisplayDateChanged;
                calendar.DisplayModeChanged += DisplayModeChanged;
                calendar.SelectedDatesChanged += SelectionChanged;
            }
            private void Detach()
            {
                if (pending != null && pending.Status == DispatcherOperationStatus.Pending) pending.Abort();
                pending = null;
                if (calendar == null) return;
                calendar.Loaded -= CalendarLoaded;
                calendar.DisplayDateChanged -= DisplayDateChanged;
                calendar.DisplayModeChanged -= DisplayModeChanged;
                calendar.SelectedDatesChanged -= SelectionChanged;
                calendar = null;
            }
            internal void Queue()
            {
                if (calendar == null || pending != null || picker.Dispatcher.HasShutdownStarted) return;
                pending = picker.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(delegate
                { pending = null; if (calendar != null) Annotate(calendar); }));
            }
            internal void Opened(object sender, RoutedEventArgs args) { Refresh(picker); }
            internal void Loaded(object sender, RoutedEventArgs args) { Refresh(picker); }
            internal void Unloaded(object sender, RoutedEventArgs args) { Detach(); }
            private void CalendarLoaded(object sender, RoutedEventArgs args) { Queue(); }
            private void DisplayDateChanged(object sender, CalendarDateChangedEventArgs args) { Queue(); }
            private void DisplayModeChanged(object sender, CalendarModeChangedEventArgs args) { Queue(); }
            internal void SelectionChanged(object sender, SelectionChangedEventArgs args) { Queue(); }
        }
    }
}
