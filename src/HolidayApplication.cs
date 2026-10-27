using System;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace HolidayTimer
{
    internal sealed partial class HolidayApplication : Application
    {
        internal AppSettings Settings;
        internal Window Widget;
        internal Window SettingsWindow;
        internal Window Greeting;
        internal Window GadgetPreview;
        internal Forms.NotifyIcon Tray;
        internal bool StartupFromLogin;
        internal bool SessionAvailable = true;
        internal Exception TestDispatcherError;
        private readonly SettingsStore store;
        private readonly IStartupRegistration startup;
        private readonly bool desktopIntegration;
        private DispatcherTimer timer;
        private System.Drawing.Icon icon;
        private Forms.ToolStripMenuItem showItem, pinItem;
        private Forms.ToolStripMenuItem[] units;
        private bool quitting, warningShown;
        private string startupWarning;
        private bool widgetMotionAttached;
        private AppSettings previewSettings;
        private DispatcherTimer previewUpdateTimer;
        private double? previewLeft, previewTop;

        internal HolidayApplication(SettingsStore store, IStartupRegistration startup, bool desktopIntegration)
        {
            this.store = store; this.startup = startup; this.desktopIntegration = desktopIntegration;
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Settings = store.Load();
            DispatcherUnhandledException += delegate(object sender, DispatcherUnhandledExceptionEventArgs e)
            {
                e.Handled = true;
                if (!this.desktopIntegration) { TestDispatcherError = e.Exception; return; }
                MessageBox.Show("The action could not finish. Your saved countdown is still available.\n\n" + e.Exception.Message,
                    "Holiday Timer", MessageBoxButton.OK, MessageBoxImage.Warning);
            };
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            Initialize();
            if (!Settings.IsConfigured)
            {
                if (Settings.ShowFloating && Settings.AutomaticPublicHoliday) ShowWidget();
                OpenSettings();
            }
            else
            {
                if (!StartupFromLogin) RepairStartupPath();
                if (Settings.ShowFloating) ShowWidget();
                ProcessCelebrations(DateTime.Now);
            }
        }

        internal void Initialize()
        {
            InitializePublicHolidayCalendar();
            Resources.MergedDictionaries.Add(Layouts.LoadControls());
            Widget = Layouts.Load("WidgetWindow");
            ApplyGadgetAppearance(Widget, Settings);
            Widget.Topmost = Settings.AlwaysOnTop;
            Layouts.Find<Button>(Widget, "WorkdayButton").Click += delegate { ToggleWorkdays(); };
            Layouts.Find<Button>(Widget, "EditButton").Click += delegate { OpenSettings(); };
            Layouts.Find<Button>(Widget, "HideButton").Click += delegate { HideWidget(); };
            Layouts.Find<Button>(Widget, "UnitButton").Click += delegate { CycleUnit(); };
            Layouts.Find<System.Windows.Controls.Border>(Widget, "DragArea").MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                if (e.ChangedButton != MouseButton.Left) return;
                Widget.DragMove(); ApplyGadgetAppearance(Widget, Settings); Placement.Clamp(Widget); RememberPosition(); SaveQuietly();
            };
            Widget.Closing += delegate(object sender, System.ComponentModel.CancelEventArgs e)
            { if (!quitting) { e.Cancel = true; HideWidget(); } };
            Widget.SourceInitialized += delegate { Placement.Restore(Widget, Settings, false); };
            Widget.DpiChanged += delegate { Dispatcher.BeginInvoke(new Action(delegate { if (Widget.IsVisible) { ApplyGadgetAppearance(Widget, Settings); Placement.Clamp(Widget); } })); };
            InitializeMusic();
            if (desktopIntegration)
            {
                CreateTray();
                InitializeUpdates();
                SystemEvents.SessionSwitch += OnSessionSwitch;
                SystemEvents.PowerModeChanged += OnPowerModeChanged;
                SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
            }
            timer = new DispatcherTimer(DispatcherPriority.Normal) { Interval = TimeSpan.FromSeconds(1) };
            timer.Tick += delegate { RefreshCountdown(); ProcessCelebrations(DateTime.Now); };
            timer.Start(); RefreshCountdown();
        }

        private void CreateTray()
        {
            icon = Layouts.CreateTrayIcon();
            Tray = new Forms.NotifyIcon { Icon = icon, Text = "Holiday Timer", Visible = true };
            Forms.ContextMenuStrip menu = new Forms.ContextMenuStrip();
            showItem = new Forms.ToolStripMenuItem("Show countdown", null, delegate { if (Widget.IsVisible) HideWidget(); else ShowWidget(); });
            menu.Items.Add(showItem);
            menu.Items.Add("Edit holiday and preferences", null, delegate { OpenSettings(); });
            pinItem = new Forms.ToolStripMenuItem("Always on top", null, delegate { TogglePin(); });
            menu.Items.Add(pinItem);
            Forms.ToolStripMenuItem unitMenu = new Forms.ToolStripMenuItem("Count down in");
            units = new Forms.ToolStripMenuItem[5];
            for (int i = 0; i < units.Length; i++)
            {
                CountdownUnit unit = (CountdownUnit)i;
                units[i] = new Forms.ToolStripMenuItem(unit.ToString(), null, delegate { Settings.Unit = unit; SaveQuietly(); RefreshCountdown(); });
                unitMenu.DropDownItems.Add(units[i]);
            }
            menu.Items.Add(unitMenu);
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add("Reset countdown position", null, delegate { ShowWidget(); Placement.Restore(Widget, Settings, true); RememberPosition(); SaveQuietly(); });
            menu.Items.Add("Preview morning message", null, delegate { if (Settings.IsConfigured) PreviewCelebration(Settings, DateTime.Now); else OpenSettings(); });
            menu.Items.Add("Check for updates…", null, delegate { ShowUpdates(); CheckForUpdates(true); });
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add("Exit Holiday Timer", null, delegate { ExitApp(); });
            Tray.ContextMenuStrip = menu;
            Tray.MouseClick += delegate(object sender, Forms.MouseEventArgs e)
            { if (e.Button == Forms.MouseButtons.Left) { if (Settings.IsConfigured || Settings.AutomaticPublicHoliday) ShowWidget(); else OpenSettings(); } };
        }

        internal void OpenSettings()
        {
            if (quitting) return;
            if (Widget == null) return;
            if (SettingsWindow != null) { SettingsWindow.Show(); SettingsWindow.WindowState = WindowState.Normal; SettingsWindow.Activate(); return; }
            SettingsWindow = Layouts.Load("SettingsWindow");
            FillForm(SettingsWindow, Settings);
            CalendarPicker.Attach(Layouts.Find<DatePicker>(SettingsWindow, "HolidayDate"));
            BindPublicHolidayForm(SettingsWindow);
            Themes.Apply(SettingsWindow, Settings.Theme, Settings.BackgroundOpacity, Settings.LightAppearance, Settings.CountdownTextColour);
            CalendarPicker.Refresh(Layouts.Find<DatePicker>(SettingsWindow, "HolidayDate"));
            FontChoices.Apply(SettingsWindow, Settings.DisplayFont);
            double height = Math.Max(280, SystemParameters.WorkArea.Height - 24), width = Math.Max(380, SystemParameters.WorkArea.Width - 24);
            SettingsWindow.MinHeight = Math.Min(450, height); SettingsWindow.Height = Math.Min(740, height);
            SettingsWindow.MinWidth = Math.Min(520, width); SettingsWindow.Width = Math.Min(980, width);
            GlassWindow.Apply(SettingsWindow);
            GlassWindow.ApplyAppearance(SettingsWindow, Settings.LightAppearance);
            Layouts.Find<TextBlock>(SettingsWindow, "FormError").Text = store.LoadWarning ?? startupWarning ?? publicHolidayWarning ?? "";
            Window form = SettingsWindow;
            previewLeft = previewTop = null;
            previewUpdateTimer = new DispatcherTimer(DispatcherPriority.Normal) { Interval = TimeSpan.FromMilliseconds(80) };
            previewUpdateTimer.Tick += delegate { previewUpdateTimer.Stop(); RefreshGadgetPreview(); };
            Layouts.Find<System.Windows.Controls.Border>(form, "SettingsTitleDrag").MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            { if (e.ChangedButton == MouseButton.Left) form.DragMove(); };
            Layouts.Find<Button>(form, "SettingsMinimize").Click += delegate { form.WindowState = WindowState.Minimized; };
            Layouts.Find<Button>(form, "SettingsClose").Click += delegate { form.Close(); };
            Layouts.Find<ComboBox>(form, "ColourScheme").SelectionChanged += delegate
            { SyncChoices(form); UpdateDraftAppearance(form); };
            Layouts.Find<ComboBox>(form, "CountdownUnit").SelectionChanged += delegate { SyncChoices(form); QueuePreviewUpdate(); };
            string[] unitNames = { "UnitDays", "UnitMinutes", "UnitSeconds", "UnitWeeks", "UnitHours" }, themeNames = { "ThemeBeach", "ThemeSunset", "ThemeFestive" };
            for (int i = 0; i < unitNames.Length; i++)
            {
                int index = i;
                Layouts.Find<RadioButton>(form, unitNames[i]).Checked += delegate { Layouts.Find<ComboBox>(form, "CountdownUnit").SelectedIndex = index; };
            }
            for (int i = 0; i < themeNames.Length; i++)
            {
                int index = i;
                Layouts.Find<RadioButton>(form, themeNames[i]).Checked += delegate { Layouts.Find<ComboBox>(form, "ColourScheme").SelectedIndex = index; };
            }
            Layouts.Find<ComboBox>(form, "DisplayFont").SelectionChanged += delegate
            { UpdateDraftAppearance(form); };
            Layouts.Find<Slider>(form, "BackgroundOpacity").ValueChanged += delegate
            { Layouts.Find<TextBlock>(form, "OpacityValue").Text = Layouts.Find<Slider>(form, "BackgroundOpacity").Value.ToString("0") + "%"; UpdateDraftAppearance(form); };
            Layouts.Find<Slider>(form, "WidgetScale").ValueChanged += delegate
            { Layouts.Find<TextBlock>(form, "ScaleValue").Text = Layouts.Find<Slider>(form, "WidgetScale").Value.ToString("0") + "%"; QueuePreviewUpdate(); };
            Layouts.Find<TextBox>(form, "HolidayName").TextChanged += delegate { QueuePreviewUpdate(); };
            Layouts.Find<TextBox>(form, "HolidayTime").TextChanged += delegate { QueuePreviewUpdate(); };
            Layouts.Find<DatePicker>(form, "HolidayDate").SelectedDateChanged += delegate { QueuePreviewUpdate(); };
            Layouts.Find<ComboBox>(form, "DisplayFont").AddHandler(TextBox.TextChangedEvent, new TextChangedEventHandler(delegate { UpdateDraftAppearance(form); }));
            Layouts.Find<CheckBox>(form, "AlwaysOnTop").Checked += delegate { QueuePreviewUpdate(); };
            Layouts.Find<CheckBox>(form, "AlwaysOnTop").Unchecked += delegate { QueuePreviewUpdate(); };
            Layouts.Find<Button>(form, "PreviewGadget").Click += delegate { ShowGadgetPreview(form); };
            BindMusicForm(form);
            BindAppearanceForm(form);
            BindWorkForm(form);
            BindCelebrationForm(form);
            BindUpdateForm(form);
            Layouts.Find<RadioButton>(form, "AppearanceDark").Checked += delegate { Layouts.Find<CheckBox>(form, "LightAppearance").IsChecked = false; };
            Layouts.Find<RadioButton>(form, "AppearanceLight").Checked += delegate { Layouts.Find<CheckBox>(form, "LightAppearance").IsChecked = true; };
            Layouts.Find<CheckBox>(form, "LightAppearance").Checked += delegate { SyncChoices(form); UpdateDraftAppearance(form); };
            Layouts.Find<CheckBox>(form, "LightAppearance").Unchecked += delegate { SyncChoices(form); UpdateDraftAppearance(form); };
            form.SizeChanged += delegate { ArrangeSettings(form); };
            form.Closed += delegate { if (previewUpdateTimer != null) previewUpdateTimer.Stop(); CloseGadgetPreview(); previewLeft = previewTop = null; SettingsWindow = null; previewSettings = null; SynchronizeMusic(); };
            Layouts.Find<Button>(form, "CancelButton").Click += delegate { form.Close(); };
            Layouts.Find<Button>(form, "SaveButton").Click += delegate { SaveForm(form); };
            Layouts.Find<Button>(form, "PreviewGreeting").Click += delegate
            {
                AppSettings draft;
                string error;
                if (ReadForm(form, false, out draft, out error)) { PreviewCelebration(draft, DateTime.Now); }
                else Layouts.Find<TextBlock>(form, "FormError").Text = error;
            };
            form.Show(); ArrangeSettings(form); RefreshGadgetPreview(); form.Activate();
            Motion.Attach(form, desktopIntegration);
            Layouts.Find<TextBox>(form, "HolidayName").Focus();
        }

        private static void ArrangeSettings(Window form)
        {
            SettingsLayout.Arrange(form);
        }

        internal static void FillForm(Window form, AppSettings settings)
        {
            DateTime target = CountdownCalculator.ParseTarget(settings.TargetLocal);
            Layouts.Find<TextBox>(form, "HolidayName").Text = settings.HolidayName;
            Layouts.Find<DatePicker>(form, "HolidayDate").SelectedDate = target.Date;
            Layouts.Find<TextBox>(form, "HolidayTime").Text = target.ToString("HH:mm", CultureInfo.InvariantCulture);
            Layouts.Find<ComboBox>(form, "CountdownUnit").SelectedIndex = (int)settings.Unit;
            Layouts.Find<CheckBox>(form, "ShowFloating").IsChecked = settings.ShowFloating;
            Layouts.Find<CheckBox>(form, "AlwaysOnTop").IsChecked = settings.AlwaysOnTop;
            Layouts.Find<CheckBox>(form, "StartAtLogin").IsChecked = settings.StartAtLogin;
            Layouts.Find<CheckBox>(form, "MorningGreeting").IsChecked = settings.MorningGreeting;
            Layouts.Find<CheckBox>(form, "LightAppearance").IsChecked = settings.LightAppearance;
            Layouts.Find<ComboBox>(form, "ColourScheme").SelectedIndex = (int)settings.Theme;
            Layouts.Find<Slider>(form, "BackgroundOpacity").Value = settings.BackgroundOpacity * 100;
            Layouts.Find<TextBlock>(form, "OpacityValue").Text = (settings.BackgroundOpacity * 100).ToString("0") + "%";
            Layouts.Find<Slider>(form, "WidgetScale").Value = settings.WidgetScale * 100;
            Layouts.Find<TextBlock>(form, "ScaleValue").Text = (settings.WidgetScale * 100).ToString("0") + "%";
            ComboBox fonts = Layouts.Find<ComboBox>(form, "DisplayFont");
            fonts.ItemsSource = FontChoices.Names;
            fonts.Text = FontChoices.Available(settings.DisplayFont) ? settings.DisplayFont : "Inter Display";
            FillMusicForm(form, settings);
            FillAppearanceForm(form, settings);
            FillWorkForm(form, settings);
            FillCelebrationForm(form, settings);
            SyncChoices(form);
        }
        private static void SyncChoices(Window form)
        {
            bool light = Layouts.Find<CheckBox>(form, "LightAppearance").IsChecked == true;
            Layouts.Find<RadioButton>(form, "AppearanceDark").IsChecked = !light;
            Layouts.Find<RadioButton>(form, "AppearanceLight").IsChecked = light;
            string[] unitNames = { "UnitDays", "UnitMinutes", "UnitSeconds", "UnitWeeks", "UnitHours" }, themeNames = { "ThemeBeach", "ThemeSunset", "ThemeFestive" };
            for (int i = 0; i < unitNames.Length; i++)
            {
                Layouts.Find<RadioButton>(form, unitNames[i]).IsChecked = Layouts.Find<ComboBox>(form, "CountdownUnit").SelectedIndex == i;
            }
            for (int i = 0; i < themeNames.Length; i++)
            {
                Layouts.Find<RadioButton>(form, themeNames[i]).IsChecked = Layouts.Find<ComboBox>(form, "ColourScheme").SelectedIndex == i;
            }
        }

        internal bool ReadForm(Window form, bool futureOnly, out AppSettings draft, out string error)
        {
            draft = null; error = null;
            string name = Layouts.Find<TextBox>(form, "HolidayName").Text.Trim();
            if (name.Length == 0) { error = "Give your holiday a name."; return false; }
            string targetText;
            if (!TryReadTarget(form, out targetText, out error)) return false;
            if (futureOnly && CountdownCalculator.ToLocalTarget(targetText) <= DateTimeOffset.Now &&
                (!Settings.IsConfigured || !String.Equals(targetText, Settings.TargetLocal, StringComparison.Ordinal)))
            { error = "Choose a holiday date and time in the future."; return false; }
            int unit = Layouts.Find<ComboBox>(form, "CountdownUnit").SelectedIndex;
            if (unit < 0 || unit > 4) { error = "Choose days, weeks, hours, minutes, or seconds."; return false; }
            draft = new AppSettings {
                HolidayName = name, TargetLocal = targetText, Unit = (CountdownUnit)unit,
                ShowFloating = Layouts.Find<CheckBox>(form, "ShowFloating").IsChecked == true,
                AlwaysOnTop = Layouts.Find<CheckBox>(form, "AlwaysOnTop").IsChecked == true,
                StartAtLogin = Layouts.Find<CheckBox>(form, "StartAtLogin").IsChecked == true,
                MorningGreeting = Layouts.Find<CheckBox>(form, "MorningGreeting").IsChecked == true,
                LightAppearance = Layouts.Find<CheckBox>(form, "LightAppearance").IsChecked == true,
                WidgetLeft = previewLeft ?? Settings.WidgetLeft, WidgetTop = previewTop ?? Settings.WidgetTop,
                LastGreetingDate = Settings.LastGreetingDate, LastArrivalTarget = Settings.LastArrivalTarget, IsConfigured = true,
                AutomaticPublicHoliday = Settings.AutomaticPublicHoliday && name == Settings.HolidayName && targetText == Settings.TargetLocal,
                PublicHolidayKey = PublicHolidayKeyForDraft(name, targetText)
            };
            draft.Theme = (HolidayTheme)Layouts.Find<ComboBox>(form, "ColourScheme").SelectedIndex;
            draft.BackgroundOpacity = Layouts.Find<Slider>(form, "BackgroundOpacity").Value / 100;
            draft.WidgetScale = Layouts.Find<Slider>(form, "WidgetScale").Value / 100;
            draft.DisplayFont = Layouts.Find<ComboBox>(form, "DisplayFont").Text.Trim();
            ReadMusicForm(form, draft);
            if (!ReadWorkForm(form, draft, out error)) { draft = null; return false; }
            if (!ReadCelebrationForm(form, draft, out error)) { draft = null; return false; }
            if (!ReadAppearanceForm(form, draft, out error)) { draft = null; return false; }
            if (!FontChoices.Available(draft.DisplayFont)) { error = "Choose a font from the list."; draft = null; return false; }
            return true;
        }
        private static bool TryReadTarget(Window form, out string targetText, out string error)
        {
            targetText = null; error = null;
            DatePicker datePicker = Layouts.Find<DatePicker>(form, "HolidayDate");
            DateTime parsedDate;
            if (!DateTime.TryParse(datePicker.Text, CultureInfo.CurrentCulture, DateTimeStyles.None, out parsedDate))
            { error = "Choose a valid holiday date."; return false; }
            DateTime time;
            if (!DateTime.TryParseExact(Layouts.Find<TextBox>(form, "HolidayTime").Text.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out time))
            { error = "Enter the time as HH:mm, for example 09:00 or 18:30."; return false; }
            DateTime target = DateTime.SpecifyKind(parsedDate.Date.AddHours(time.Hour).AddMinutes(time.Minute), DateTimeKind.Unspecified);
            if (TimeZoneInfo.Local.IsInvalidTime(target)) { error = "That time is skipped by daylight saving. Choose a different time."; return false; }
            targetText = target.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
            return true;
        }

        internal bool SaveForm(Window form)
        {
            bool automaticFallback = PreparePublicHolidayFallbackForm(form, DateTime.Now);
            AppSettings draft; string error;
            if (!ReadForm(form, true, out draft, out error)) { Layouts.Find<TextBlock>(form, "FormError").Text = error; return false; }
            if (automaticFallback)
            {
                draft.AutomaticPublicHoliday = true;
                NswPublicHoliday selected = NswPublicHolidays.Find(CountdownCalculator.ParseTarget(draft.TargetLocal));
                draft.PublicHolidayKey = selected == null ? null : selected.Key;
            }
            string originalCommand = null; bool registrationChanged = false;
            try
            {
                originalCommand = startup.CurrentCommand;
                string desired = draft.StartAtLogin ? StartupRegistration.CommandFor(Assembly.GetExecutingAssembly().Location) : null;
                if (!String.Equals(originalCommand, desired, StringComparison.OrdinalIgnoreCase))
                { startup.SetCommand(desired); registrationChanged = true; }
                store.Save(draft);
            }
            catch (Exception ex)
            {
                string rollback = "";
                if (registrationChanged)
                {
                    try { startup.SetCommand(originalCommand); }
                    catch { rollback = " Windows sign-in could not be restored. Check Startup apps in Windows Settings."; }
                }
                Layouts.Find<TextBlock>(form, "FormError").Text = "Your changes could not be saved. " + ex.Message + rollback;
                return false;
            }
            Settings = draft; startupWarning = null; warningShown = false;
            ApplyGadgetAppearance(Widget, Settings);
            Widget.Topmost = Settings.AlwaysOnTop;
            if (draft.ShowFloating) ShowWidget(); else { Widget.Hide(); if (new System.Windows.Interop.WindowInteropHelper(Widget).Handle != IntPtr.Zero) Placement.Restore(Widget, Settings, false); }
            RefreshCountdown(); form.Close();
            return true;
        }

        internal void ShowWidget()
        {
            if (!Settings.IsConfigured && !Settings.AutomaticPublicHoliday) { OpenSettings(); return; }
            Settings.ShowFloating = true;
            Widget.Topmost = Settings.AlwaysOnTop;
            Widget.Show(); Placement.Clamp(Widget); Widget.Activate();
            ApplyGadgetAppearance(Widget, Settings); Placement.Restore(Widget, Settings, false);
            if (!widgetMotionAttached) { Motion.Attach(Widget, desktopIntegration); widgetMotionAttached = true; }
            Motion.Reveal(Layouts.Find<Viewbox>(Widget, "WidgetView"), desktopIntegration);
            RememberPosition(); SaveQuietly(); RefreshCountdown();
        }
        internal void HideWidget()
        { Settings.ShowFloating = false; Widget.Hide(); SaveQuietly(); RefreshCountdown(); }
        internal void TogglePin()
        { Settings.AlwaysOnTop = !Settings.AlwaysOnTop; Widget.Topmost = Settings.AlwaysOnTop; SaveQuietly(); RefreshCountdown(); }
        internal void CycleUnit()
        { Settings.Unit = (CountdownUnit)(((int)Settings.Unit + 1) % 5); SaveQuietly(); RefreshCountdown(); }
        private void RememberPosition()
        { Placement.Remember(Widget, Settings); }

        internal void RefreshCountdown()
        {
            if (Widget == null) return;
            string tooltip = RenderCountdown(Widget, Settings, false);
            if (SettingsWindow != null && previewSettings != null) RenderSettingsPreview(SettingsWindow, previewSettings);
            if (GadgetPreview != null && previewSettings != null) RenderCountdown(GadgetPreview, previewSettings, true);
            if (Tray != null)
            {
                Tray.Text = tooltip.Length > 63 ? tooltip.Substring(0, 60) + "…" : tooltip;
                showItem.Text = Widget.IsVisible ? "Hide countdown in tray" : "Show countdown";
                pinItem.Checked = Settings.AlwaysOnTop;
                for (int i = 0; i < units.Length; i++) units[i].Checked = (int)Settings.Unit == i;
            }
            SynchronizeMusic();
        }
        private static string RenderCountdown(Window window, AppSettings settings, bool preview)
        {
            DateTimeOffset target = CountdownCalculator.ToLocalTarget(settings.TargetLocal);
            DateTimeOffset now = DateTimeOffset.Now;
            long remaining;
            bool available = TryRemaining(settings, now, settings.Unit, out remaining);
            string number = available ? remaining.ToString("N0", CultureInfo.CurrentCulture) : "—";
            bool arrived = target <= now;
            Layouts.Find<TextBlock>(window, "HolidayLabel").Text = settings.HolidayName;
            Layouts.Find<TextBlock>(window, "HolidayLabel").ToolTip = settings.HolidayName;
            TextBlock digits = Layouts.Find<TextBlock>(window, "RemainingNumber");
            digits.Text = number;
            FitCounter(digits, number);
            string unit = UnitLabel(settings.Unit, remaining, settings.WorkdayCountdown);
            Layouts.Find<TextBlock>(window, "RemainingUnit").Text = RemainingCaption(settings, remaining, arrived, available);
            Layouts.Find<TextBlock>(window, "TargetLabel").Text = CountdownCalculator.ParseTarget(settings.TargetLocal).ToString("d MMM yyyy · HH:mm", CultureInfo.CurrentCulture);
            UpdateWorkdayButton(window, settings);
            Layouts.Find<Button>(window, "UnitButton").Content = settings.Unit.ToString();
            window.Title = settings.HolidayName + (preview ? " — Preview" : " — Holiday Timer");
            return (arrived || !available || (settings.WorkdayCountdown && remaining == 0) ? RemainingCaption(settings, remaining, arrived, available) : number + " " + unit + " to go") + "\n" + settings.HolidayName;
        }
        internal void ApplyGadgetAppearance(Window window, AppSettings settings)
        {
            Themes.Apply(window, settings.Theme, settings.BackgroundOpacity, settings.LightAppearance, settings.CountdownTextColour); FontChoices.Apply(window, settings.DisplayFont);
            OrbScene.Apply(Layouts.Find<Canvas>(window, "OrbCanvas"), window, settings.Theme, settings.BackgroundOpacity, desktopIntegration, settings.LightAppearance, settings.Effect, settings.EffectIntensity);
            GadgetControls.Attach(window, desktopIntegration);
            double scale = Math.Min(settings.WidgetScale, Placement.MaximumWidgetScale(window));
            window.Width = 344 * scale; window.Height = 344 * scale; window.UpdateLayout();
            // Keep secondary information readable when the Viewbox is reduced.
            // The count retains its fixed row and independent width fitting.
            double labelScale = Math.Max(.01, scale);
            Grid face = Layouts.Find<Grid>(window, "GadgetHoverArea");
            if (face.ActualWidth > 0)
            {
                // Native window dimensions are rounded to pixels. Use the actual
                // transform so that rounding cannot shrink the promised minimum.
                GeneralTransform transform = face.TransformToAncestor((Visual)window.Content);
                Point origin = transform.Transform(new Point(0, 0)), unit = transform.Transform(new Point(0, 1));
                labelScale = Math.Max(.01, (unit - origin).Length);
            }
            Layouts.Find<TextBlock>(window, "HolidayLabel").FontSize = Math.Max(14, 12 / labelScale);
            Layouts.Find<TextBlock>(window, "RemainingUnit").FontSize = Math.Max(13, 11 / labelScale);
            Layouts.Find<TextBlock>(window, "TargetLabel").FontSize = Math.Max(11, 10 / labelScale);
            foreach (string name in new string[] { "UnitButton", "WorkdayButton", "EditButton", "HideButton" })
                Layouts.Find<Button>(window, name).FontSize = Math.Max(11, 10 / labelScale);
            window.UpdateLayout();
            if (window.IsVisible) Placement.Clamp(window);
        }
        private void UpdateDraftAppearance(Window form)
        {
            int theme = Layouts.Find<ComboBox>(form, "ColourScheme").SelectedIndex;
            if (theme < 0) return;
            bool light = Layouts.Find<CheckBox>(form, "LightAppearance").IsChecked == true;
            Themes.Apply(form, (HolidayTheme)theme, Layouts.Find<Slider>(form, "BackgroundOpacity").Value / 100, light, PreviewTextColour(form));
            GlassWindow.ApplyAppearance(form, light);
            GlassWindow.RefreshFallback(form);
            string font = Layouts.Find<ComboBox>(form, "DisplayFont").Text;
            if (FontChoices.Available(font)) FontChoices.Apply(form, font);
            QueuePreviewUpdate();
        }
        private void QueuePreviewUpdate()
        { if (previewUpdateTimer != null) { previewUpdateTimer.Stop(); previewUpdateTimer.Start(); } }
        private AppSettings PreviewDraft(Window form)
        {
            AppSettings draft; string error;
            if (ReadForm(form, false, out draft, out error)) return draft;
            AppSettings previous = previewSettings ?? Settings;
            string name = Layouts.Find<TextBox>(form, "HolidayName").Text.Trim();
            string font = Layouts.Find<ComboBox>(form, "DisplayFont").Text.Trim();
            string targetText;
            if (!TryReadTarget(form, out targetText, out error)) targetText = previous.TargetLocal;
            AppSettings fallback = new AppSettings {
                HolidayName = name.Length == 0 ? "My holiday" : name,
                TargetLocal = targetText, Unit = (CountdownUnit)Math.Max(0, Layouts.Find<ComboBox>(form, "CountdownUnit").SelectedIndex),
                Theme = (HolidayTheme)Math.Max(0, Layouts.Find<ComboBox>(form, "ColourScheme").SelectedIndex),
                DisplayFont = FontChoices.Available(font) ? font : previous.DisplayFont,
                BackgroundOpacity = Layouts.Find<Slider>(form, "BackgroundOpacity").Value / 100,
                LightAppearance = Layouts.Find<CheckBox>(form, "LightAppearance").IsChecked == true,
                WidgetScale = Layouts.Find<Slider>(form, "WidgetScale").Value / 100,
                AlwaysOnTop = Layouts.Find<CheckBox>(form, "AlwaysOnTop").IsChecked == true
            };
            ReadMusicForm(form, fallback);
            ReadWorkPreview(form, fallback, previous);
            fallback.CelebrationEffect = (CelebrationKind)Math.Max(0, Layouts.Find<ComboBox>(form, "CelebrationStyle").SelectedIndex);
            string appearanceError;
            ReadAppearanceForm(form, fallback, out appearanceError);
            fallback.CountdownTextColour = PreviewTextColour(form);
            return fallback;
        }
        internal void ShowGadgetPreview(Window form)
        {
            if (GadgetPreview != null) { RefreshGadgetPreview(); GadgetPreview.Activate(); return; }
            previewSettings = PreviewDraft(form);
            GadgetPreview = Layouts.Load("WidgetWindow");
            Window window = GadgetPreview;
            ApplyGadgetAppearance(window, previewSettings); window.Topmost = previewSettings.AlwaysOnTop;
            Button edit = Layouts.Find<Button>(window, "EditButton"); edit.Content = "Preview"; edit.IsEnabled = false;
            Button close = Layouts.Find<Button>(window, "HideButton"); close.Content = "Close"; close.ToolTip = "Close preview";
            close.Click += delegate { window.Close(); };
            Layouts.Find<Button>(window, "UnitButton").Click += delegate {
                ComboBox choice = Layouts.Find<ComboBox>(form, "CountdownUnit"); choice.SelectedIndex = (choice.SelectedIndex + 1) % 5; RefreshGadgetPreview();
            };
            Layouts.Find<Button>(window, "WorkdayButton").Click += delegate { CheckBox choice = Layouts.Find<CheckBox>(form, "WorkdayCountdown"); choice.IsChecked = choice.IsChecked != true; RefreshGadgetPreview(); };
            Layouts.Find<System.Windows.Controls.Border>(window, "DragArea").MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                if (e.ChangedButton != MouseButton.Left) return;
                window.DragMove(); ApplyGadgetAppearance(window, previewSettings); Placement.Clamp(window);
                AppSettings position = new AppSettings(); Placement.Remember(window, position); previewLeft = position.WidgetLeft; previewTop = position.WidgetTop;
            };
            window.PreviewKeyDown += delegate(object sender, KeyEventArgs e) { if (e.Key == Key.Escape) { e.Handled = true; window.Close(); } };
            window.DpiChanged += delegate { Dispatcher.BeginInvoke(new Action(delegate { if (GadgetPreview == window) RefreshGadgetPreview(); })); };
            window.Closed += delegate { GadgetPreview = null; SynchronizeMusic(); };
            WatchMusicVisibility(window);
            window.Show(); ApplyGadgetAppearance(window, previewSettings); Placement.PreviewBeside(window, form);
            RenderCountdown(window, previewSettings, true); Motion.Attach(window, desktopIntegration); window.Activate();
        }
        internal void RefreshGadgetPreview()
        {
            if (SettingsWindow == null) return;
            previewSettings = PreviewDraft(SettingsWindow);
            Themes.Apply(SettingsWindow, previewSettings.Theme, previewSettings.BackgroundOpacity, previewSettings.LightAppearance, previewSettings.CountdownTextColour);
            OrbScene.Apply(Layouts.Find<Canvas>(SettingsWindow, "SettingsOrbCanvas"), SettingsWindow, previewSettings.Theme, previewSettings.BackgroundOpacity, desktopIntegration, previewSettings.LightAppearance, previewSettings.Effect, previewSettings.EffectIntensity);
            SettingsLayout.Arrange(SettingsWindow);
            RenderSettingsPreview(SettingsWindow, previewSettings);
            SynchronizeMusic();
            if (GadgetPreview == null) return;
            ApplyGadgetAppearance(GadgetPreview, previewSettings);
            GadgetPreview.Topmost = previewSettings.AlwaysOnTop; RenderCountdown(GadgetPreview, previewSettings, true);
        }
        private static void RenderSettingsPreview(Window form, AppSettings settings)
        {
            DateTimeOffset now = DateTimeOffset.Now;
            long remaining;
            bool available = TryRemaining(settings, now, settings.Unit, out remaining);
            Layouts.Find<TextBlock>(form, "SettingsPreviewName").Text = settings.HolidayName;
            TextBlock digits = Layouts.Find<TextBlock>(form, "SettingsPreviewNumber");
            digits.Text = available ? remaining.ToString("N0", CultureInfo.CurrentCulture) : "—";
            FitCounter(digits, digits.Text);
            Layouts.Find<TextBlock>(form, "SettingsPreviewUnit").Text = RemainingCaption(settings, remaining, CountdownCalculator.ToLocalTarget(settings.TargetLocal) <= now, available);
        }
        private static void FitCounter(TextBlock digits, string number)
        {
            Typeface typeface = new Typeface(digits.FontFamily, digits.FontStyle, digits.FontWeight, digits.FontStretch);
            double dpi = VisualTreeHelper.GetDpi(digits).PixelsPerDip;
            char widest = '0'; double maximum = 0;
            for (char digit = '0'; digit <= '9'; digit++)
            {
                double width = new FormattedText(digit.ToString(), CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                    typeface, 64, Brushes.Black, dpi).WidthIncludingTrailingWhitespace;
                if (width > maximum) { maximum = width; widest = digit; }
            }
            char[] sample = number.ToCharArray();
            for (int i = 0; i < sample.Length; i++) if (Char.IsDigit(sample[i])) sample[i] = widest;
            double fitWidth = new FormattedText(new string(sample), CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                typeface, 64, Brushes.Black, dpi).WidthIncludingTrailingWhitespace;
            digits.FontSize = Math.Min(64, 64 * 218 / Math.Max(1, fitWidth));
        }
        private void CloseGadgetPreview()
        { if (GadgetPreview != null) GadgetPreview.Close(); previewSettings = null; }
        internal static string UnitLabel(CountdownUnit unit, long value, bool workday = false)
        {
            string label = unit == CountdownUnit.Days ? "day" : unit == CountdownUnit.Weeks ? "week" : unit == CountdownUnit.Hours ? "hour" : unit == CountdownUnit.Minutes ? "minute" : "second";
            if (workday) label = unit == CountdownUnit.Days ? "workday" : "working " + label;
            return value == 1 ? label : label + "s";
        }

        internal bool CheckMorning(DateTime now)
        {
            if (quitting || !SessionAvailable || !MorningPolicy.ShouldShow(Settings, now) || SettingsWindow != null || Greeting != null ||
                (desktopIntegration && !DesktopSession.IsUnlocked())) return false;
            // Mark before opening so an unlock and a timer tick cannot open duplicate greetings.
            Settings.LastGreetingDate = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            SaveQuietly(); ShowGreeting(Settings, now); return true;
        }
        private void SaveQuietly()
        {
            if (!Settings.IsConfigured) return;
            try { store.Save(Settings); }
            catch (Exception ex)
            {
                if (!warningShown && Tray != null)
                { warningShown = true; Tray.ShowBalloonTip(8000, "Holiday Timer", "This change could not be saved. " + ex.Message, Forms.ToolTipIcon.Warning); }
            }
        }
        private void RepairStartupPath()
        {
            if (!Settings.StartAtLogin) return;
            try
            {
                string desired = StartupRegistration.CommandFor(Assembly.GetExecutingAssembly().Location);
                if (!String.Equals(startup.CurrentCommand, desired, StringComparison.OrdinalIgnoreCase)) startup.SetCommand(desired);
            }
            catch (Exception ex) { startupWarning = "Start at sign-in needs attention: " + ex.Message; OpenSettings(); }
        }
        private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            if (quitting || Dispatcher.HasShutdownStarted) return;
            SessionSwitchReason reason = e.Reason;
            Dispatcher.BeginInvoke(new Action(delegate
            {
                if (reason == SessionSwitchReason.SessionLock || reason == SessionSwitchReason.RemoteDisconnect || reason == SessionSwitchReason.ConsoleDisconnect)
                    SessionAvailable = false;
                else if (reason == SessionSwitchReason.SessionUnlock || reason == SessionSwitchReason.SessionLogon || reason == SessionSwitchReason.RemoteConnect || reason == SessionSwitchReason.ConsoleConnect)
                    SessionAvailable = true;
                RefreshCountdown(); ProcessCelebrations(DateTime.Now);
            }));
        }
        private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        { if (e.Mode == PowerModes.Resume) QueueRefresh(); }
        private void QueueRefresh()
        {
            if (quitting || Dispatcher.HasShutdownStarted) return;
            Dispatcher.BeginInvoke(new Action(delegate { RefreshCountdown(); ProcessCelebrations(DateTime.Now); }));
        }
        private void OnDisplaySettingsChanged(object sender, EventArgs e)
        {
            if (quitting || Dispatcher.HasShutdownStarted) return;
            Dispatcher.BeginInvoke(new Action(delegate
            {
                if (Widget.IsVisible) { ApplyGadgetAppearance(Widget, Settings); Placement.Clamp(Widget); RememberPosition(); SaveQuietly(); }
                RefreshGadgetPreview();
                ReflowGreetings();
            }));
        }
        internal void ExitApp()
        {
            if (quitting) return;
            quitting = true;
            DisposeMusic();
            DisposeUpdates();
            if (timer != null) timer.Stop();
            if (previewUpdateTimer != null) previewUpdateTimer.Stop();
            CloseGadgetPreview();
            if (desktopIntegration)
            {
                SystemEvents.SessionSwitch -= OnSessionSwitch;
                SystemEvents.PowerModeChanged -= OnPowerModeChanged;
                SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            }
            if (Tray != null) { Tray.Visible = false; Tray.ContextMenuStrip.Dispose(); Tray.Dispose(); }
            if (icon != null) icon.Dispose();
            if (SettingsWindow != null) SettingsWindow.Close();
            CloseGreetings();
            if (Widget != null) Widget.Close();
            Shutdown();
        }
        protected override void OnExit(ExitEventArgs e)
        { if (!quitting) ExitApp(); base.OnExit(e); }
    }
}
