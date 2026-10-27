using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace HolidayTimer
{
    internal static partial class UiChecks
    {
        private sealed class TestStartup : IStartupRegistration
        {
            public string CurrentCommand { get; private set; }
            public bool Fail;
            public int Writes;
            public void SetCommand(string command)
            { Writes++; if (Fail) throw new IOException("Test sign-in registration failure."); CurrentCommand = command; }
        }
        private static int checks;
        private static void Check(bool condition, string message)
        { if (!condition) throw new Exception("FAIL: " + message); checks++; }
        internal static int Run(bool render)
        {
            string root = AppDomain.CurrentDomain.BaseDirectory;
            string scratch = Path.Combine(root, "checks-" + Guid.NewGuid().ToString("N"));
            HolidayApplication app = null;
            string result;
            int exitCode = 0;
            bool nativeGlassEnabled = false;
            try
            {
                TestStartup startup = new TestStartup();
                SettingsStore store = new SettingsStore(scratch);
                app = new HolidayApplication(store, startup, false);
                // Let the real queued startup choose the widget before the
                // fixture keeps references or pumps preview events.
                DispatcherFrame startupFrame = new DispatcherFrame();
                app.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(delegate { startupFrame.Continue = false; }));
                Dispatcher.PushFrame(startupFrame);
                if (app.Widget == null) app.Initialize();
                app.OpenSettings();
                Window form = app.SettingsWindow;
                DatePicker celebrationDate = Layouts.Find<DatePicker>(form, "HolidayDate");
                Check(celebrationDate.CalendarStyle != null && celebrationDate.FirstDayOfWeek == DayOfWeek.Monday && CalendarPicker.GetCalendar(celebrationDate) != null,
                    "The actual settings date field uses the themed Monday-first celebration calendar");
                GlyphTypeface inter;
                Check(new Typeface(FontChoices.Family("Inter Display"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal).TryGetGlyphTypeface(out inter)
                    && System.Linq.Enumerable.Contains(inter.FamilyNames.Values, "Inter Display"), "Bundled Inter Display resolves to its real glyphs");
                Check(Layouts.Find<ComboBox>(form, "DisplayFont").Text == "Inter Display", "Inter Display is the initial font choice");
                CheckCompactSettingsLayout(form, root, render);
                CheckCompanionControls(app, form, store, root, render);
                CheckUpdateControls(app, form);
                CheckPublicHolidayControls(app, form, store, root, render);
                SettingsLayout.SelectSection(form, "MusicTab");
                CheckMusicControls(app, form, store, root, render);
                SettingsLayout.SelectSection(form, "AppearanceTab");
                CheckEffectsControls(app, form, store, root, render);
                CheckColourPickerControls(app, form, store, root, render);
                SettingsLayout.SelectSection(form, "CountdownTab");
                CheckWorkControls(app, form, store, root, render);
                SettingsLayout.SelectSection(form, "CelebrationTab");
                CheckCelebrationChoices(app, form, store);
                SettingsLayout.SelectSection(form, "CountdownTab");
                Check(Layouts.Find<RadioButton>(form, "AppearanceDark").IsChecked == true && Layouts.Find<CheckBox>(form, "LightAppearance").IsChecked == false, "Existing and new settings start with the dark appearance choice");
                Check(form.Height <= SystemParameters.WorkArea.Height && form.MinHeight <= form.Height, "Settings fits the actual scaled work area");
                Layouts.Find<RadioButton>(form, "UnitWeeks").IsChecked = true;
                AppSettings weeksDraft; string weeksError;
                Check(Layouts.Find<ComboBox>(form, "CountdownUnit").SelectedIndex == 3 && app.ReadForm(form, false, out weeksDraft, out weeksError) && weeksDraft.Unit == CountdownUnit.Weeks,
                    "The visible Weeks choice enters the saved form draft without changing existing unit identifiers");
                Check(HolidayApplication.UnitLabel(CountdownUnit.Weeks, 1) == "week" && HolidayApplication.UnitLabel(CountdownUnit.Weeks, 2) == "weeks", "Weeks uses singular and plural countdown labels");
                Layouts.Find<RadioButton>(form, "UnitMinutes").IsChecked = true;
                Check(Layouts.Find<ComboBox>(form, "CountdownUnit").SelectedIndex == 1, "Visible unit choices drive the selected unit");
                Layouts.Find<RadioButton>(form, "ThemeFestive").IsChecked = true;
                Check(Layouts.Find<ComboBox>(form, "ColourScheme").SelectedIndex == 2, "Visible mood choices drive the selected theme");
                Check(form != null && form.IsVisible, "First run opens configuration");
                Check(app.Widget.IsVisible && app.Settings.AutomaticPublicHoliday && !app.Settings.IsConfigured,
                    "First setup shows the next NSW holiday while keeping preferences unsaved");
                Check(app.ShutdownMode == ShutdownMode.OnExplicitShutdown, "Hiding windows keeps the tray process alive");
                nativeGlassEnabled = GlassWindow.GetEnabled(form);
                Check(!form.AllowsTransparency && form.WindowStyle == WindowStyle.None, "Settings uses a native window for the Acrylic backdrop");
                Check(nativeGlassEnabled || ((SolidColorBrush)Layouts.Find<Border>(form, "SettingsSurface").Background).Color.A == 255, "Unsupported native Acrylic keeps an opaque readable fallback");
                Canvas embeddedOrb = Layouts.Find<Canvas>(form, "SettingsOrbCanvas");
                Check(embeddedOrb.Children.Count == 8 && embeddedOrb.Opacity == 1 && Math.Abs(Layouts.Find<Grid>(form, "SettingsOrbMaterial").Opacity - .82) < .001, "Embedded preview keeps eight colour layers vivid while its centre uses the requested opacity");
                Check(Layouts.Find<TextBlock>(form, "SettingsPreviewName").Text == app.Settings.HolidayName, "Embedded preview starts with the current holiday name");
                double initialSettingsWidth = form.Width;
                form.Width = 520;
                Check(WaitFor(delegate { return Grid.GetRow(Layouts.Find<ScrollViewer>(form, "SettingsScroller")) == 1 && Grid.GetColumn(Layouts.Find<Grid>(form, "LivePreviewPanel")) == 0; }), "Narrow settings keep a compact live preview above the active options tab");
                Check(Layouts.Find<Grid>(form, "WorkspaceGrid").ColumnDefinitions[2].Width.Value == 0, "Narrow settings removes the empty side column");
                if (render)
                {
                    string earlyImages = Path.Combine(root, "previews"); Directory.CreateDirectory(earlyImages);
                    Render(form, Path.Combine(earlyImages, "settings-narrow.png"));
                }
                form.Width = initialSettingsWidth;
                Check(WaitFor(delegate { bool narrow = form.ActualWidth < 820; return Grid.GetRow(Layouts.Find<ScrollViewer>(form, "SettingsScroller")) == (narrow ? 1 : 0) && Grid.GetColumn(Layouts.Find<Grid>(form, "LivePreviewPanel")) == (narrow ? 0 : 2); }), "Settings restores the persistent side preview after widening");
                Layouts.Find<Slider>(form, "BackgroundOpacity").Value = 63;
                Check(((SolidColorBrush)Layouts.Find<Border>(form, "SettingsSurface").Background).Color.A == PanelAlpha(form, .63), "Slider updates the panel tint while respecting native-glass fallback");
                Check(form.Opacity == 1 && Layouts.Find<TextBox>(form, "HolidayName").Opacity == 1, "Panel translucency preserves opaque text and controls");
                Layouts.Find<Button>(form, "PreviewGadget").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(app.GadgetPreview != null && app.GadgetPreview.IsVisible, "Preview button opens the real floating layout");
                CheckGadgetControls(app.GadgetPreview, form, root, render);
                CountdownUnit savedPreviewUnit = app.Settings.Unit;
                int previewStartupWrites = startup.Writes;
                foreach (CountdownUnit draftUnit in new CountdownUnit[] { CountdownUnit.Seconds, CountdownUnit.Weeks, CountdownUnit.Hours, CountdownUnit.Days, CountdownUnit.Minutes })
                {
                    Layouts.Find<Button>(app.GadgetPreview, "UnitButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Check(Layouts.Find<ComboBox>(form, "CountdownUnit").SelectedIndex == (int)draftUnit && Layouts.Find<Button>(app.GadgetPreview, "UnitButton").Content.ToString() == draftUnit.ToString(), "The preview unit button switches the draft to " + draftUnit);
                    string draftLabel = draftUnit.ToString().ToLowerInvariant() + " to go";
                    Check(Layouts.Find<TextBlock>(app.GadgetPreview, "RemainingUnit").Text == draftLabel && Layouts.Find<TextBlock>(form, "SettingsPreviewUnit").Text == draftLabel, "Both draft countdowns show the preview button's " + draftUnit + " choice");
                    Check(app.Settings.Unit == savedPreviewUnit && !File.Exists(store.FilePath) && startup.Writes == previewStartupWrites, "Preview unit switching leaves the saved countdown and registrations untouched");
                    if (render && draftUnit == CountdownUnit.Weeks) Render(app.GadgetPreview, Path.Combine(root, "previews", "weeks-countdown.png"));
                }
                Color darkBody = ((SolidColorBrush)form.Resources["BodyText"]).Color;
                Layouts.Find<RadioButton>(form, "AppearanceLight").IsChecked = true;
                Check(WaitFor(delegate { return (bool)form.Resources["LightAppearance"] && (bool)app.GadgetPreview.Resources["LightAppearance"]; }), "Light appearance updates settings and floating preview within the draft response deadline");
                Check(Layouts.Find<CheckBox>(form, "LightAppearance").IsChecked == true, "The visible Light choice drives the saved appearance field");
                Check(((SolidColorBrush)form.Resources["BodyText"]).Color != darkBody && ((SolidColorBrush)form.Resources["PanelSurface"]).Color.R > 200, "Light appearance changes the panel and readable foreground together");
                Check(((SolidColorBrush)Layouts.Find<TextBlock>(app.GadgetPreview, "RemainingNumber").Foreground).Color == ((SolidColorBrush)app.GadgetPreview.Resources["OrbText"]).Color, "The light floating countdown uses its light foreground resource");
                Check(((SolidColorBrush)Layouts.Find<TextBlock>(form, "SettingsPreviewNumber").Foreground).Color == ((SolidColorBrush)form.Resources["OrbText"]).Color, "The embedded countdown uses the same light foreground resource");
                CheckGadgetTypography(app.GadgetPreview, 100);
                CheckMaterialTransparency(app, form, store, root, render);
                CheckAppearanceReapply(app.GadgetPreview, Layouts.Find<Canvas>(app.GadgetPreview, "OrbCanvas"));
                Layouts.Find<TextBox>(form, "HolidayName").Text = "Live preview holiday";
                Layouts.Find<RadioButton>(form, "UnitSeconds").IsChecked = true;
                Layouts.Find<RadioButton>(form, "ThemeSunset").IsChecked = true;
                Layouts.Find<Slider>(form, "WidgetScale").Value = 150;
                Check(WaitFor(delegate { return Layouts.Find<TextBlock>(app.GadgetPreview, "HolidayLabel").Text == "Live preview holiday"; }), "Preview follows unsaved name edits within its response deadline");
                Check(Layouts.Find<TextBlock>(app.GadgetPreview, "RemainingUnit").Text == "seconds to go", "Preview follows unsaved unit choice");
                Check(Layouts.Find<TextBlock>(form, "SettingsPreviewName").Text == "Live preview holiday" && Layouts.Find<TextBlock>(form, "SettingsPreviewUnit").Text == "seconds to go", "Embedded preview follows the same unsaved name and unit");
                Check(Int64.Parse(Layouts.Find<TextBlock>(form, "SettingsPreviewNumber").Text, NumberStyles.AllowThousands, CultureInfo.CurrentCulture) > 0, "Embedded preview shows a real positive countdown");
                Check(Math.Abs(app.GadgetPreview.Width - 516) < 1 && Math.Abs(app.GadgetPreview.Height - 516) < 1, "Size slider resizes the actual square preview window");
                Canvas floatingOrb = Layouts.Find<Canvas>(app.GadgetPreview, "OrbCanvas");
                Check(floatingOrb.Children.Count == 8 && floatingOrb.Opacity == 1 && embeddedOrb.Opacity == 1 && Math.Abs(Layouts.Find<Grid>(app.GadgetPreview, "OrbMaterial").Opacity - .63) < .001 && Math.Abs(Layouts.Find<Grid>(form, "SettingsOrbMaterial").Opacity - .63) < .001, "Embedded and floating orbs share vivid colour layers and the same fading centre opacity");
                Check(((SolidColorBrush)Layouts.Find<System.Windows.Controls.Border>(app.GadgetPreview, "WidgetSurface").Background).Color.A == 255 && Layouts.Find<Grid>(app.GadgetPreview, "OrbMaterial").Opacity == .63, "The opaque gadget base receives translucency only from its material group");
                string priorCount = Layouts.Find<TextBlock>(app.GadgetPreview, "RemainingNumber").Text;
                string priorEmbeddedCount = Layouts.Find<TextBlock>(form, "SettingsPreviewNumber").Text;
                Check(WaitFor(delegate { return Layouts.Find<TextBlock>(app.GadgetPreview, "RemainingNumber").Text != priorCount && Layouts.Find<TextBlock>(form, "SettingsPreviewNumber").Text != priorEmbeddedCount; }, 2500), "Embedded and floating seconds advance within one timer tick deadline");
                Layouts.Find<TextBox>(form, "HolidayTime").Text = "1";
                Layouts.Find<TextBox>(form, "HolidayName").Text = "";
                Layouts.Find<Slider>(form, "WidgetScale").Value = 60;
                Layouts.Find<Slider>(form, "BackgroundOpacity").Value = 35;
                Check(WaitFor(delegate { return Math.Abs(app.GadgetPreview.Width - 206.4) < 1; }), "Small preview size responds within deadline");
                Check(Math.Abs(app.GadgetPreview.Width - 206.4) < 1, "Small size still works with incomplete input");
                CheckGadgetTypography(app.GadgetPreview, 60);
                Check(Layouts.Find<TextBlock>(app.GadgetPreview, "HolidayLabel").Text == "My holiday", "Incomplete name uses a friendly preview placeholder");
                Check(Layouts.Find<TextBlock>(form, "SettingsPreviewName").Text == "My holiday", "Incomplete name also uses the embedded preview placeholder");
                Check(((SolidColorBrush)Layouts.Find<Border>(form, "SettingsSurface").Background).Color.A == PanelAlpha(form, .35), "Minimum opacity affects panel despite incomplete input");
                Layouts.Find<DatePicker>(form, "HolidayDate").SelectedDate = DateTime.Today.AddDays(50);
                Layouts.Find<TextBox>(form, "HolidayTime").Text = "10:15";
                Check(WaitFor(delegate { return Layouts.Find<TextBlock>(app.GadgetPreview, "TargetLabel").Text == DateTime.Today.AddDays(50).AddHours(10).AddMinutes(15).ToString("d MMM yyyy · HH:mm", CultureInfo.CurrentCulture); }), "Valid target edits update while the preview name is blank");
                Layouts.Find<ComboBox>(form, "DisplayFont").Text = "Inter D";
                Layouts.Find<DatePicker>(form, "HolidayDate").SelectedDate = DateTime.Today.AddDays(51);
                Check(WaitFor(delegate { return Layouts.Find<TextBlock>(app.GadgetPreview, "TargetLabel").Text == DateTime.Today.AddDays(51).AddHours(10).AddMinutes(15).ToString("d MMM yyyy · HH:mm", CultureInfo.CurrentCulture); }), "Valid target edits update while a font name is incomplete");
                Layouts.Find<ComboBox>(form, "DisplayFont").Text = "Inter Display";
                Layouts.Find<Slider>(form, "WidgetScale").Value = 180;
                Layouts.Find<Slider>(form, "BackgroundOpacity").Value = 100;
                Check(WaitFor(delegate { return Math.Abs(app.GadgetPreview.Width - 619.2) < 1; }), "Large preview size responds within deadline");
                Check(Math.Abs(app.GadgetPreview.Width - 619.2) < 1 && Math.Abs(app.GadgetPreview.Height - 619.2) < 1, "Large square preview size works");
                CheckGadgetTypography(app.GadgetPreview, 180);
                Check(Layouts.Find<Viewbox>(form, "SettingsPreviewView").ActualWidth > 0 && Layouts.Find<Viewbox>(form, "SettingsPreviewView").ActualWidth <= 260, "Embedded preview keeps a visible bounded size while the floating gadget grows");
                Check(((SolidColorBrush)Layouts.Find<System.Windows.Controls.Border>(form, "SettingsSurface").Background).Color.A == 255, "Maximum opacity makes panel solid");
                Check(!app.Settings.IsConfigured && !app.Settings.LightAppearance && app.Settings.WidgetScale == 1 && app.Settings.BackgroundOpacity == .82, "Preview does not change saved application preferences including appearance");
                Check(!File.Exists(store.FilePath) && startup.Writes == 0, "Preview causes no settings or sign-in writes");
                if (render)
                {
                    string previewImages = Path.Combine(root, "previews"); Directory.CreateDirectory(previewImages);
                    Layouts.Find<TextBox>(form, "HolidayName").Text = "Summer in Japan"; Layouts.Find<TextBox>(form, "HolidayTime").Text = "09:00";
                    Layouts.Find<RadioButton>(form, "UnitDays").IsChecked = true; Layouts.Find<RadioButton>(form, "ThemeBeach").IsChecked = true;
                    Layouts.Find<Slider>(form, "BackgroundOpacity").Value = 35;
                    Layouts.Find<Slider>(form, "WidgetScale").Value = 100;
                    Check(WaitFor(delegate { return Math.Abs(app.GadgetPreview.Width - 344) < 1 && Math.Abs(Layouts.Find<Grid>(app.GadgetPreview, "OrbMaterial").Opacity - .35) < .001; }), "Light-desktop fixture uses the 35% orb material at its normal size");
                    Render(app.GadgetPreview, Path.Combine(previewImages, "gadget-translucent-light.png"), true);
                    Layouts.Find<Slider>(form, "BackgroundOpacity").Value = 82;
                    Check(WaitFor(delegate { return Math.Abs(Layouts.Find<Grid>(app.GadgetPreview, "OrbMaterial").Opacity - .82) < .001; }), "Light appearance capture uses the normal orb size and material opacity");
                    Render(form, Path.Combine(previewImages, "light-settings.png"), true);
                    Render(app.GadgetPreview, Path.Combine(previewImages, "light-orb.png"), true);
                    Layouts.Find<Slider>(form, "BackgroundOpacity").Value = 63;
                    Layouts.Find<Slider>(form, "WidgetScale").Value = 60; Pump(150); Render(app.GadgetPreview, Path.Combine(previewImages, "gadget-small.png"));
                    Layouts.Find<Slider>(form, "WidgetScale").Value = 180; Pump(150); Render(app.GadgetPreview, Path.Combine(previewImages, "gadget-large.png"));
                    Render(form, Path.Combine(previewImages, "settings-translucent.png"));
                }
                Layouts.Find<Button>(app.GadgetPreview, "HideButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(app.GadgetPreview == null, "Preview close button closes only the preview");
                app.ShowGadgetPreview(form);
                Layouts.Find<Button>(form, "CancelButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(app.GadgetPreview == null && app.SettingsWindow == null && !File.Exists(store.FilePath) && startup.Writes == 0, "Cancel closes preview and discards every draft change");
                app.OpenSettings(); form = app.SettingsWindow;
                Check(Layouts.Find<Slider>(form, "WidgetScale").Value == 100 && Layouts.Find<Slider>(form, "BackgroundOpacity").Value == 82, "Reopening settings restores saved size and opacity");
                Check(Layouts.Find<RadioButton>(form, "AppearanceDark").IsChecked == true && !app.Settings.LightAppearance, "Cancel discards the Light appearance draft and restores Dark");
                Layouts.Find<TextBox>(form, "HolidayName").Text = "";
                Check(!app.SaveForm(form), "An empty holiday name cannot be saved");
                Check(!File.Exists(store.FilePath), "Invalid form does not write settings");
                Check(startup.CurrentCommand == null, "Invalid form does not enable sign-in");
                Layouts.Find<TextBox>(form, "HolidayName").Text = "Summer in Japan";
                Layouts.Find<TextBox>(form, "HolidayTime").Text = "25:00";
                Check(!app.SaveForm(form), "Invalid time rejected");
                Layouts.Find<TextBox>(form, "HolidayTime").Text = "09:30";
                Layouts.Find<DatePicker>(form, "HolidayDate").SelectedDate = DateTime.Today.AddDays(-1);
                startup.Fail = true;
                Check(!app.SaveForm(form) && Layouts.Find<TextBox>(form, "HolidayTime").Text == "00:00" &&
                    Layouts.Find<DatePicker>(form, "HolidayDate").SelectedDate == NswPublicHolidays.Next(DateTime.Now).Date,
                    "A past target loads the next NSW holiday draft while a later save failure preserves settings");
                startup.Fail = false;
                Layouts.Find<TextBox>(form, "HolidayName").Text = "Summer in Japan";
                Layouts.Find<TextBox>(form, "HolidayTime").Text = "09:30";
                Layouts.Find<DatePicker>(form, "HolidayDate").SelectedDate = DateTime.Today.AddDays(30);
                Layouts.Find<ComboBox>(form, "CountdownUnit").SelectedIndex = 2;
                startup.Fail = true;
                Check(!app.SaveForm(form), "Sign-in failure shown in the form");
                Check(!app.Settings.IsConfigured && !File.Exists(store.FilePath), "Sign-in failure preserves unconfigured state");
                startup.Fail = false;
                Layouts.Find<ComboBox>(form, "CelebrationStyle").SelectedIndex = (int)CelebrationKind.Flowers;
                Check(app.SaveForm(form), "Valid holiday saves");
                Check(app.SettingsWindow == null && app.Widget.IsVisible, "Save closes settings and reveals countdown");
                Check(app.Widget.Topmost, "Pinned window stays on top");
                Check(store.Load().HolidayName == "Summer in Japan", "Configuration persists");
                Check(store.Load().CelebrationEffect == CelebrationKind.Flowers, "The chosen wedding Flowers celebration saves through the actual form");
                Check(startup.CurrentCommand.StartsWith("\"") && startup.CurrentCommand.EndsWith("\" --startup"), "Sign-in path quoted");
                Check(Layouts.Find<TextBlock>(app.Widget, "RemainingUnit").Text == "seconds to go", "User seconds choice displayed");
                Check(Int64.Parse(Layouts.Find<TextBlock>(app.Widget, "RemainingNumber").Text, NumberStyles.AllowThousands, CultureInfo.CurrentCulture) > 0, "Live countdown is positive");
                CheckDirectUnitSwitches(app, store);
                CheckDirectWorkSwitches(app, store);
                CheckCounterStability(app);
                app.HideWidget();
                Check(!app.Widget.IsVisible && !store.Load().ShowFloating, "Hide switches to tray preference");
                app.ShowWidget();
                Check(app.Widget.IsVisible && store.Load().ShowFloating, "Tray restore keeps the saved floating preference");
                app.TogglePin();
                Check(!app.Widget.Topmost && !store.Load().AlwaysOnTop, "Unpin updates window and preference");
                app.Settings.WidgetLeft = -100000; app.Settings.WidgetTop = -100000;
                Placement.Restore(app.Widget, app.Settings, false);
                Check(app.Widget.Left >= SystemParameters.WorkArea.Left && app.Widget.Top >= SystemParameters.WorkArea.Top, "Missing-monitor position is recovered");
                app.OpenSettings(); form = app.SettingsWindow;
                Layouts.Find<TextBox>(form, "HolidayName").Text = "Changed holiday";
                Layouts.Find<CheckBox>(form, "StartAtLogin").IsChecked = false;
                string priorCommand = startup.CurrentCommand;
                using (FileStream locked = File.Open(store.FilePath, FileMode.Open, FileAccess.Read, FileShare.None))
                    Check(!app.SaveForm(form), "Settings write failure shown in form");
                Check(startup.CurrentCommand == priorCommand, "Failed save restores previous sign-in registration");
                Check(app.Settings.HolidayName == "Summer in Japan" && store.Load().HolidayName == "Summer in Japan", "Failed save preserves countdown and prior file");
                Check(app.SaveForm(form) && startup.CurrentCommand == null, "Sign-in can be disabled");
                app.OpenSettings(); form = app.SettingsWindow;
                Layouts.Find<ComboBox>(form, "ColourScheme").SelectedIndex = (int)HolidayTheme.Sunset;
                Layouts.Find<Slider>(form, "BackgroundOpacity").Value = 55;
                Layouts.Find<Slider>(form, "WidgetScale").Value = 125;
                Layouts.Find<ComboBox>(form, "DisplayFont").Text = "Trebuchet MS";
                Layouts.Find<RadioButton>(form, "AppearanceLight").IsChecked = true;
                Check(app.SaveForm(form), "Theme, Light appearance, font and opacity save through the actual form");
                Check(store.Load().Theme == HolidayTheme.Sunset && store.Load().LightAppearance && store.Load().BackgroundOpacity == .55 && store.Load().DisplayFont == "Trebuchet MS", "Style preferences and independent Light appearance survive reload");
                Check((bool)app.Widget.Resources["LightAppearance"] && ((SolidColorBrush)Layouts.Find<TextBlock>(app.Widget, "RemainingNumber").Foreground).Color == ((SolidColorBrush)app.Widget.Resources["OrbText"]).Color, "Saving Light applies the matching foreground to the real gadget");
                Check(((SolidColorBrush)Layouts.Find<Border>(app.Widget, "WidgetSurface").Background).Color.A == 255 && Math.Abs(Layouts.Find<Grid>(app.Widget, "OrbMaterial").Opacity - .55) < .001, "Saved opacity fades the centre without compounding its base alpha");
                Check(Layouts.Find<Canvas>(app.Widget, "OrbCanvas").Children.Count == 8 && Layouts.Find<Canvas>(app.Widget, "OrbCanvas").Opacity == 1 && Math.Abs(Layouts.Find<Grid>(app.Widget, "OrbMaterial").Opacity - .55) < .001, "Saved centre opacity leaves all eight independent colour layers vivid");
                Check(app.Widget.FontFamily.Source.Contains("Trebuchet MS"), "Selected font is applied to the gadget");
                Check(store.Load().WidgetScale == 1.25 && Math.Abs(app.Widget.Width - 430) < 1 && Math.Abs(app.Widget.Height - 430) < 1, "Save applies and persists real square gadget size");
                app.OpenSettings(); form = app.SettingsWindow;
                Check(Layouts.Find<RadioButton>(form, "AppearanceLight").IsChecked == true && Layouts.Find<CheckBox>(form, "LightAppearance").IsChecked == true, "Reopening settings restores the saved Light choice");
                Layouts.Find<RadioButton>(form, "AppearanceDark").IsChecked = true;
                Check(app.SaveForm(form) && !store.Load().LightAppearance && store.Load().Theme == HolidayTheme.Sunset, "Changing back to Dark saves without changing the selected palette");
                foreach (HolidayTheme theme in Enum.GetValues(typeof(HolidayTheme)))
                {
                    foreach (bool light in new bool[] { false, true })
                    {
                        Themes.Apply(app.Widget, theme, 1, light);
                        string appearance = theme + (light ? " Light" : " Dark");
                        Color panel = ((SolidColorBrush)app.Widget.Resources["PanelSurface"]).Color;
                        foreach (string resource in new string[] { "Heading", "BodyText", "MutedText", "Accent" })
                        {
                            Color foreground = ((SolidColorBrush)app.Widget.Resources[resource]).Color;
                            Check(foreground.A == 255 && Contrast(foreground, panel) >= 4.5, appearance + " " + resource + " stays opaque and readable against its base panel palette");
                        }
                        Color scrim = ((RadialGradientBrush)app.Widget.Resources["CountdownScrim"]).GradientStops[0].Color;
                        foreach (string resource in new string[] { "OrbText", "OrbSecondaryText", "OrbMutedText" })
                        {
                            Color foreground = ((SolidColorBrush)app.Widget.Resources[resource]).Color;
                            Check(foreground.A == 255 && Contrast(foreground, scrim) >= 4.5, appearance + " " + resource + " stays readable against its base countdown scrim");
                        }
                    }
                }
                Themes.Apply(app.Widget, app.Settings.Theme, app.Settings.BackgroundOpacity, app.Settings.LightAppearance);
                Rect primary = new Rect(0, 0, 1920, 1040), secondary = new Rect(1920, 0, 2560, 1400);
                Rect fitted = Placement.Fit(new Rect(3500, 100, 516, 516), new[] { primary, secondary }, primary);
                Check(fitted.Left == 3500 && fitted.Top == 100, "Physical position stays on secondary monitor at 150% DPI");
                fitted = Placement.Fit(new Rect(4400, 1300, 516, 516), new[] { primary, secondary }, primary);
                Check(fitted.Left == 3964 && fitted.Top == 884, "DPI-resized gadget is clamped to the monitor edges");
                fitted = Placement.Fit(new Rect(3500, 100, 516, 516), new[] { primary }, primary);
                Check(fitted.Left == 1404, "Disconnected secondary monitor restores to visible primary screen");
                Rect leftMonitor = new Rect(-1920, 0, 1920, 1040);
                fitted = Placement.Fit(new Rect(-1000, 200, 344, 344), new[] { leftMonitor, primary }, primary);
                Check(fitted.Left == -1000, "Monitor left of primary keeps its valid negative pixel coordinates");
                string future = app.Settings.TargetLocal;
                app.Settings.TargetLocal = DateTime.Today.AddDays(-1).ToString(CountdownCalculator.TargetFormat, CultureInfo.InvariantCulture);
                app.Settings.LastArrivalTarget = app.Settings.TargetLocal;
                app.OpenSettings(); form = app.SettingsWindow;
                Layouts.Find<TextBox>(form, "HolidayName").Text = "An arrived holiday";
                Check(app.SaveForm(form), "An arrived holiday still allows preference and name changes");
                app.Settings.TargetLocal = future;
                app.Settings.Unit = CountdownUnit.Days; app.Settings.HolidayName = "Summer in Japan";
                app.Settings.LastGreetingDate = null; app.RefreshCountdown();
                DateTime morning = DateTime.Today.AddHours(8);
                app.SessionAvailable = false;
                Check(!app.CheckMorning(morning) && app.Settings.LastGreetingDate == null, "Locked session waits without marking today's greeting");
                app.SessionAvailable = true;
                Check(app.CheckMorning(morning), "First morning opens a greeting");
                Check(app.Greeting != null && app.Greeting.IsVisible, "Large greeting is visible");
                Check(app.Greeting.AllowsTransparency && ((SolidColorBrush)app.Greeting.Background).Color.A == 0, "Fullscreen greeting has no background panel");
                Check(app.Greeting.WindowStyle == WindowStyle.None && app.Greeting.Topmost && app.Greeting.ActualHeight >= SystemParameters.WorkArea.Height, "Greeting fills the screen without window chrome");
                Check(Layouts.Find<Canvas>(app.Greeting, "CelebrationCanvas").Children.Count == 0, "Reduced/disabled motion has a calm greeting");
                Check(!app.CheckMorning(morning.AddMinutes(1)), "Repeated unlock does not duplicate greeting");
                Check(Layouts.Find<TextBlock>(app.Greeting, "GreetingUnit").Text.Contains("days"), "Greeting always reports days");
                string originalDays = Layouts.Find<TextBlock>(app.Greeting, "GreetingNumber").Text;
                app.UpdateGreeting(morning.AddDays(1));
                Check(Layouts.Find<TextBlock>(app.Greeting, "GreetingNumber").Text != originalDays, "Open greeting refreshes across overnight lock/resume");
                app.Greeting.Close();
                app.ShowGreeting(app.Settings, morning, false);
                Window escapeGreeting = app.Greeting;
                escapeGreeting.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(escapeGreeting), Environment.TickCount, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent, Handled = true });
                Check(WaitFor(delegate { return app.Greeting == null; }), "Already-handled Escape dismisses the actual greeting through its centralized handler");
                Check(!app.CheckMorning(morning.AddMinutes(2)), "Dismissed greeting stays dismissed today");
                Check(store.Load().LastGreetingDate == DateTime.Today.ToString("yyyy-MM-dd"), "Morning deduplication survives restart");
                CheckCelebrationPreviews(app, store);
                app.Settings.TargetLocal = DateTime.Today.AddDays(-1).ToString(CountdownCalculator.TargetFormat, CultureInfo.InvariantCulture);
                app.Settings.LastArrivalTarget = app.Settings.TargetLocal;
                app.RefreshCountdown();
                Check(Layouts.Find<TextBlock>(app.Widget, "RemainingNumber").Text == "0", "Arrived holiday stays at zero");
                Check(Layouts.Find<TextBlock>(app.Widget, "RemainingUnit").Text == "Your holiday is here!", "Arrived state has clear text");
                Check(!app.CheckMorning(DateTime.Today.AddDays(1).AddHours(8)), "Passed holiday stops morning greetings");
                CheckArrivalCelebrations(app, store, root, render);
                CheckPublicHolidayFallback(app, store);

                if (render)
                {
                    string images = Path.Combine(root, "previews"); Directory.CreateDirectory(images);
                    app.Settings.TargetLocal = DateTime.Today.AddDays(30).AddHours(9).ToString(CountdownCalculator.TargetFormat, CultureInfo.InvariantCulture);
                    app.Settings.Unit = CountdownUnit.Days; app.Settings.AlwaysOnTop = true;
                    app.Settings.DisplayFont = "Inter Display"; app.Settings.Theme = HolidayTheme.Beach; app.Settings.BackgroundOpacity = .82;
                    Themes.Apply(app.Widget, app.Settings.Theme, app.Settings.BackgroundOpacity); FontChoices.Apply(app.Widget, app.Settings.DisplayFont); OrbScene.Apply(Layouts.Find<Canvas>(app.Widget, "OrbCanvas"), app.Widget, app.Settings.Theme, app.Settings.BackgroundOpacity, false);
                    app.RefreshCountdown(); Render(app.Widget, Path.Combine(images, "countdown.png"));
                    foreach (HolidayTheme theme in Enum.GetValues(typeof(HolidayTheme)))
                    { Themes.Apply(app.Widget, theme, .82); OrbScene.Apply(Layouts.Find<Canvas>(app.Widget, "OrbCanvas"), app.Widget, theme, .82, false); Render(app.Widget, Path.Combine(images, ThemeName(theme) + ".png")); }
                    Themes.Apply(app.Widget, HolidayTheme.Beach, .82);
                    OrbScene.Apply(Layouts.Find<Canvas>(app.Widget, "OrbCanvas"), app.Widget, HolidayTheme.Beach, .82, false);
                    app.OpenSettings(); Render(app.SettingsWindow, Path.Combine(images, "settings.png"));
                    app.ShowGreeting(app.Settings, morning); Render(app.Greeting, Path.Combine(images, "morning.png"));
                    Canvas fireworks = Layouts.Find<Canvas>(app.Greeting, "CelebrationCanvas");
                    Celebration.Play(app.Greeting, true);
                    bool moving = SystemParameters.ClientAreaAnimation && !SystemParameters.HighContrast;
                    Check(fireworks.Children.Count == (moving ? 1 : 0), "The single celebration drawing scene obeys the system motion preference");
                    if (moving)
                    {
                        foreach (HolidayTheme theme in Enum.GetValues(typeof(HolidayTheme)))
                        {
                            Themes.Apply(app.Greeting, theme, .82); Celebration.Play(app.Greeting, true);
                            Check(CelebrationPaletteMatches(app.Greeting), ThemeName(theme) + " celebration scene uses its selected theme palette");
                        }
                        Themes.Apply(app.Greeting, app.Settings.Theme, app.Settings.BackgroundOpacity); Celebration.Play(app.Greeting, true);
                        Pump(950); Render(app.Greeting, Path.Combine(images, "morning-fireworks.png"));
                        long framesBeforeLoginDelay = Celebration.FrameCount(app.Greeting);
                        Pump(8100);
                        Check(fireworks.Children.Count == 1 && Celebration.RenderingRunCount == 1 &&
                            Celebration.FrameCount(app.Greeting) > framesBeforeLoginDelay,
                            "The actual greeting keeps animating beyond the former sign-in timeout");
                        Celebration.Play(app.Greeting, true); Celebration.Stop(app.Greeting);
                        Check(fireworks.Children.Count == 0 && Celebration.RenderingRunCount == 0, "Early dismiss clears the scene and its frame subscription");
                        Celebration.RenderAt(app.Greeting, .5, app.Settings.CelebrationEffect);
                        Check(Celebration.CurrentKind(app.Greeting) == CelebrationKind.Flowers,
                            "The saved wedding celebration renders its selected flower scene");
                        Render(app.Greeting, Path.Combine(images, "morning-flowers-launch.png"));
                        Celebration.RenderAt(app.Greeting, 2.6, app.Settings.CelebrationEffect);
                        Render(app.Greeting, Path.Combine(images, "morning-flowers-burst.png"));
                        Celebration.RenderAt(app.Greeting, 120.5, app.Settings.CelebrationEffect);
                        Check(fireworks.Children.Count == 1 && Celebration.CurrentKind(app.Greeting) == CelebrationKind.Flowers,
                            "The saved wedding scene remains populated at a late login time");
                        Render(app.Greeting, Path.Combine(images, "morning-flowers-late.png"));
                        Celebration.Stop(app.Greeting);
                        CheckLoopDismissal(app, morning);
                    }
                    app.Settings.HolidayName = new string('W', 80); app.Settings.Unit = CountdownUnit.Seconds;
                    app.Settings.TargetLocal = new DateTime(9999, 12, 31).ToString(CountdownCalculator.TargetFormat, CultureInfo.InvariantCulture);
                    app.RefreshCountdown(); Render(app.Widget, Path.Combine(images, "long-countdown.png"));
                    TextBlock digits = Layouts.Find<TextBlock>(app.Widget, "RemainingNumber");
                    double numberWidth = new FormattedText(digits.Text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                        new Typeface(digits.FontFamily, digits.FontStyle, digits.FontWeight, digits.FontStretch), digits.FontSize, Brushes.Black, VisualTreeHelper.GetDpi(digits).PixelsPerDip).WidthIncludingTrailingWhitespace;
                    Check(numberWidth <= digits.ActualWidth, "Long count fits the card without clipping");
                    app.UpdateGreeting(morning); app.Greeting.UpdateLayout();
                    FrameworkElement greetingContent = (FrameworkElement)app.Greeting.Content;
                    TextBlock targetLabel = Layouts.Find<TextBlock>(app.Greeting, "GreetingTarget");
                    Button dismiss = Layouts.Find<Button>(app.Greeting, "GreetingDismiss");
                    double targetBottom = targetLabel.TransformToAncestor(greetingContent).Transform(new Point(0, targetLabel.ActualHeight)).Y;
                    double dismissTop = dismiss.TransformToAncestor(greetingContent).Transform(new Point(0, 0)).Y;
                    Check(targetBottom < dismissTop, "Long holiday name stays above the greeting dismiss button");
                    Render(app.Greeting, Path.Combine(images, "long-morning.png"));
                }
                Check(app.TestDispatcherError == null, "UI fixtures completed without unhandled dispatcher errors");
                result = "PASS: " + checks + " UI/integration checks. No real sign-in registration or personal settings were changed.";
                result += "\r\nNative live-glass request: " + (nativeGlassEnabled ? "enabled" : "unavailable; opaque fallback active") + ".";
                if (render) result += "\r\nReal WPF layouts rendered to previews over mock desktops, including Light appearance. Default captures hide the action strip; gadget-hover.png captures controls genuinely revealed by the hover event. RenderTargetBitmap does not capture native desktop blur.";
            }
            catch (Exception ex) { result = ex.ToString(); if (app != null && app.TestDispatcherError != null) result += "\r\nDispatcher error: " + app.TestDispatcherError; exitCode = 1; }
            finally
            {
                if (app != null) app.ExitApp();
                if (Directory.Exists(scratch) && Path.GetFullPath(scratch).StartsWith(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase)) Directory.Delete(scratch, true);
            }
            File.WriteAllText(Path.Combine(root, "ui-checks.txt"), result, Encoding.UTF8);
            return exitCode;
        }
        private static void CheckMaterialTransparency(HolidayApplication app, Window form, SettingsStore store, string root, bool render)
        {
            Window widget = app.GadgetPreview;
            Canvas floatingCanvas = Layouts.Find<Canvas>(widget, "OrbCanvas"), embeddedCanvas = Layouts.Find<Canvas>(form, "SettingsOrbCanvas");
            Grid floatingMaterial = Layouts.Find<Grid>(widget, "OrbMaterial"), embeddedMaterial = Layouts.Find<Grid>(form, "SettingsOrbMaterial");
            HolidayTheme originalTheme = (HolidayTheme)Layouts.Find<ComboBox>(form, "ColourScheme").SelectedIndex;
            double originalOpacity = Layouts.Find<Slider>(form, "BackgroundOpacity").Value / 100;
            bool originalLight = Layouts.Find<CheckBox>(form, "LightAppearance").IsChecked == true;
            CountdownUnit savedUnit = app.Settings.Unit;
            string savedFile = File.Exists(store.FilePath) ? File.ReadAllText(store.FilePath) : null;
            DispatcherTimer draftTimer = (DispatcherTimer)typeof(HolidayApplication).GetField("previewUpdateTimer", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(app);
            bool draftRefreshPending = draftTimer.IsEnabled;
            draftTimer.Stop();
            System.Collections.Generic.Dictionary<Canvas, byte[]> colourReferences = new System.Collections.Generic.Dictionary<Canvas, byte[]>();
            try
            {
                foreach (HolidayTheme theme in Enum.GetValues(typeof(HolidayTheme)))
                foreach (bool light in new bool[] { false, true })
                foreach (double opacity in new double[] { .35, .38, .82, 1 })
                {
                    Themes.Apply(widget, theme, opacity, light); Themes.Apply(form, theme, opacity, light);
                    OrbScene.Apply(floatingCanvas, widget, theme, opacity, false, light);
                    OrbScene.Apply(embeddedCanvas, form, theme, opacity, false, light);
                    widget.UpdateLayout(); form.UpdateLayout();
                    Check(RenderFrameBarrier(), "Material fixture commits real composition frames before pixel verification");
                    string fixture = theme + (light ? " Light " : " Dark ") + (opacity * 100).ToString("0", CultureInfo.InvariantCulture) + "%";
                    CheckOrbShadow(widget, floatingMaterial, light, false, fixture);
                    CheckOrbShadow(form, embeddedMaterial, light, true, fixture);
                    if (light)
                    {
                        System.Windows.Media.Effects.DropShadowEffect panelShadow = Layouts.Find<TextBlock>(form, "OpacityValue").Effect as System.Windows.Media.Effects.DropShadowEffect;
                        Check(panelShadow != null && panelShadow.BlurRadius <= 1.5, fixture + " actual panel text resolves a crisp Light shadow");
                    }
                    foreach (Grid material in new Grid[] { floatingMaterial, embeddedMaterial })
                    {
                        bool embedded = material == embeddedMaterial;
                        TextBlock number = Layouts.Find<TextBlock>(embedded ? form : widget, embedded ? "SettingsPreviewNumber" : "RemainingNumber");
                        UIElement textHolder = embedded ? (UIElement)FindOrbTextHolder(material) : Layouts.Find<Border>(widget, "DragArea");
                        string scope = fixture + (embedded ? " embedded" : " floating");
                        Canvas colourCanvas = embedded ? embeddedCanvas : floatingCanvas;
                        Check(Math.Abs(material.Opacity - opacity) < .001 && colourCanvas.Opacity == 1 && !material.IsAncestorOf(colourCanvas), scope + " fades only the centre while the independent colour layer stays vivid");
                        RenderTargetBitmap materialPixels = RenderIsolated((Grid)material.Parent, material);
                        byte[] pixels = Pixels(materialPixels);
                        int requestedAlpha = (int)Math.Round(255 * opacity);
                        int maximumAlpha = MaximumAlpha(pixels), centerAlpha = AlphaAt(pixels, materialPixels.PixelWidth, 172, 172), leftAlpha = AlphaAt(pixels, materialPixels.PixelWidth, 110, 172), upperAlpha = AlphaAt(pixels, materialPixels.PixelWidth, 172, 120);
                        string alphaValues = " (requested=" + requestedAlpha + ", max=" + maximumAlpha + ", samples=" + centerAlpha + "/" + leftAlpha + "/" + upperAlpha + ", group=" + material.Opacity.ToString("0.00", CultureInfo.InvariantCulture) + ")";
                        Check(maximumAlpha <= requestedAlpha + 2, scope + " rendered centre and shading alpha never exceed the chosen opacity" + alphaValues);
                        Check(Math.Abs(centerAlpha - requestedAlpha) <= 2 && Math.Abs(leftAlpha - requestedAlpha) <= 2 && Math.Abs(upperAlpha - requestedAlpha) <= 2, scope + " interior pixels render at the chosen centre opacity" + alphaValues);
                        RenderTargetBitmap colourPixels = RenderIsolated((Grid)material.Parent, colourCanvas);
                        byte[] colour = Pixels(colourPixels);
                        if (opacity == .35) colourReferences[colourCanvas] = colour;
                        Check(MaximumAlpha(colour) > 0 && AlphaAt(colour, colourPixels.PixelWidth, 172, 172) == 0, scope + " renders a visible colour shell with a transparent core");
                        Check(System.Linq.Enumerable.SequenceEqual(colourReferences[colourCanvas], colour), scope + " every rendered colour-shell pixel stays unchanged when centre opacity moves");
                        RenderTargetBitmap textPixels = RenderIsolated((Grid)material.Parent, textHolder);
                        Check(!material.IsAncestorOf(number) && number.Opacity == 1 && ((SolidColorBrush)number.Foreground).Color.A == 255 && MaximumAlpha(Pixels(textPixels)) >= 250, scope + " renders opaque countdown glyphs outside the translucent material");
                    }
                    if (opacity <= .38) CheckVisibleBackdrop(widget, opacity, fixture);
                    if (render && theme == HolidayTheme.Beach && opacity == .35)
                    {
                        string images = Path.Combine(root, "previews"); Directory.CreateDirectory(images);
                        SaveBitmap(RenderWithBackdrop(widget, Color.FromRgb(17, 31, 49), true), Path.Combine(images, light ? "gadget-glass35-light-patterned.png" : "gadget-glass35-dark-patterned.png"));
                    }
                }
            }
            finally
            {
                Themes.Apply(form, originalTheme, originalOpacity, originalLight);
                app.RefreshGadgetPreview();
                if (draftRefreshPending) draftTimer.Start();
            }
            Check(app.Settings.Unit == savedUnit && (File.Exists(store.FilePath) ? File.ReadAllText(store.FilePath) : null) == savedFile, "Material rendering and patterned draft captures do not save preview changes");
        }
        private static bool RenderFrameBarrier()
        {
            bool completed = false;
            int frames = 0;
            EventHandler rendering = null;
            rendering = delegate
            {
                if (++frames < 2) return;
                CompositionTarget.Rendering -= rendering;
                Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(delegate { completed = true; }));
            };
            CompositionTarget.Rendering += rendering;
            try { return WaitFor(delegate { return completed; }, 2500); }
            finally { CompositionTarget.Rendering -= rendering; }
        }
        private static StackPanel FindOrbTextHolder(Grid material)
        {
            foreach (UIElement child in ((Grid)material.Parent).Children)
            {
                StackPanel text = child as StackPanel;
                if (text != null) return text;
            }
            throw new InvalidOperationException("The embedded orb text holder is missing.");
        }
        private static void CheckOrbShadow(Window window, Grid material, bool light, bool embedded, string fixture)
        {
            StackPanel text = embedded ? FindOrbTextHolder(material) : Layouts.Find<Border>(window, "DragArea").Child as StackPanel;
            System.Windows.Media.Effects.DropShadowEffect shadow = text == null ? null : text.Effect as System.Windows.Media.Effects.DropShadowEffect;
            Color expected = light ? Colors.White : Color.FromRgb(3, 11, 23);
            Check(shadow != null && shadow.Color == expected, fixture + (embedded ? " embedded" : " floating") + " resolves the actual appearance-specific text shadow");
            if (light) Check(shadow.BlurRadius <= 1.5, fixture + (embedded ? " embedded" : " floating") + " keeps the actual Light text shadow crisp");
        }
        private static void CheckGadgetTypography(Window window, int requestedScale)
        {
            window.UpdateLayout();
            string fixture = requestedScale + "% gadget";
            TextBlock name = Layouts.Find<TextBlock>(window, "HolidayLabel"), unit = Layouts.Find<TextBlock>(window, "RemainingUnit"), target = Layouts.Find<TextBlock>(window, "TargetLabel");
            foreach (TextBlock label in new TextBlock[] { name, unit, target })
            {
                double minimum = label == name ? 12 : label == unit ? 11 : 10;
                Check(EffectiveFontSize(label, label.FontSize, window) >= minimum - .01, fixture + " " + label.Name + " meets its minimum readable size in actual window DIPs");
                Check(FitsWindow(label, window), fixture + " " + label.Name + " stays inside the window");
                if (label != name) Check(MeasuredText(label.Text, label).WidthIncludingTrailingWhitespace <= label.ActualWidth + .01, fixture + " " + label.Name + " fits its row without clipping");
            }
            Border strip = Layouts.Find<Border>(window, "ActionStrip");
            Check(FitsWindow(strip, window), fixture + " controls fit inside the window");
            foreach (string buttonName in new string[] { "UnitButton", "WorkdayButton", "EditButton", "HideButton" })
            {
                Button button = Layouts.Find<Button>(window, buttonName);
                Check(EffectiveFontSize(button, button.FontSize, window) >= 10 - .01, fixture + " " + buttonName + " meets the minimum actual button text size");
                Check(FitsWindow(button, window) && MeasuredText(button.Content.ToString(), button).WidthIncludingTrailingWhitespace <= button.ActualWidth - button.Padding.Left - button.Padding.Right - button.BorderThickness.Left - button.BorderThickness.Right + 1, fixture + " " + buttonName + " label fits without window overflow or clipping");
            }
            TextBlock digits = Layouts.Find<TextBlock>(window, "RemainingNumber");
            Grid orb = Layouts.Find<Grid>(window, "GadgetHoverArea");
            string originalText = digits.Text;
            double originalFontSize = digits.FontSize;
            System.Reflection.MethodInfo fit = typeof(HolidayApplication).GetMethod("FitCounter", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            try
            {
                double? stableFont = null;
                double[] stablePositions = null;
                double? stableBaseline = null;
                foreach (long count in new long[] { 8888888, 7777777, 6666666, 5555555 })
                {
                    digits.Text = count.ToString("N0", CultureInfo.CurrentCulture); fit.Invoke(null, new object[] { digits, digits.Text }); window.UpdateLayout();
                    double[] positions = CounterPositions(orb, digits, unit, target);
                    double baseline = positions[0] + MeasuredText(digits.Text, digits).Baseline;
                    if (!stableFont.HasValue) { stableFont = digits.FontSize; stablePositions = positions; stableBaseline = baseline; }
                    Check(Math.Abs(digits.FontSize - stableFont.Value) < .001 && Math.Abs(baseline - stableBaseline.Value) < .01 && SamePositions(stablePositions, positions), fixture + " changing same-length digits retains the count baseline and secondary label positions");
                }
            }
            finally { digits.Text = originalText; digits.FontSize = originalFontSize; window.UpdateLayout(); }
        }
        private static double EffectiveFontSize(FrameworkElement element, double fontSize, Window window)
        {
            GeneralTransform transform = element.TransformToAncestor((Visual)window.Content);
            Point origin = transform.Transform(new Point(0, 0)), unit = transform.Transform(new Point(0, 1));
            return fontSize * Math.Sqrt((unit.X - origin.X) * (unit.X - origin.X) + (unit.Y - origin.Y) * (unit.Y - origin.Y));
        }
        private static bool FitsWindow(FrameworkElement element, Window window)
        {
            FrameworkElement content = (FrameworkElement)window.Content;
            Rect bounds = element.TransformToAncestor(content).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
            return bounds.Left >= -.01 && bounds.Top >= -.01 && bounds.Right <= content.ActualWidth + .01 && bounds.Bottom <= content.ActualHeight + .01;
        }
        private static FormattedText MeasuredText(string text, Control control)
        {
            return new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface(control.FontFamily, control.FontStyle, control.FontWeight, control.FontStretch), control.FontSize,
                Brushes.Black, VisualTreeHelper.GetDpi(control).PixelsPerDip);
        }
        private static FormattedText MeasuredText(string text, TextBlock block)
        {
            return new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface(block.FontFamily, block.FontStyle, block.FontWeight, block.FontStretch), block.FontSize,
                Brushes.Black, VisualTreeHelper.GetDpi(block).PixelsPerDip);
        }
        private static RenderTargetBitmap RenderIsolated(Grid parent, UIElement visibleChild)
        {
            System.Collections.Generic.Dictionary<UIElement, Visibility> visibility = new System.Collections.Generic.Dictionary<UIElement, Visibility>();
            try
            {
                foreach (UIElement child in parent.Children)
                {
                    visibility.Add(child, child.Visibility);
                    child.Visibility = child == visibleChild ? Visibility.Visible : Visibility.Hidden;
                }
                parent.UpdateLayout();
                if (!RenderFrameBarrier()) throw new InvalidOperationException("The isolated orb visibility changes did not reach a rendered frame.");
                DrawingVisual visual = new DrawingVisual();
                using (DrawingContext drawing = visual.RenderOpen())
                {
                    VisualBrush parentBrush = new VisualBrush(parent) { ViewboxUnits = BrushMappingMode.Absolute, Viewbox = new Rect(0, 0, 344, 344), Stretch = Stretch.Fill };
                    drawing.DrawRectangle(parentBrush, null, new Rect(0, 0, 344, 344));
                }
                RenderTargetBitmap bitmap = new RenderTargetBitmap(344, 344, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(visual);
                return bitmap;
            }
            finally
            {
                foreach (System.Collections.Generic.KeyValuePair<UIElement, Visibility> child in visibility) child.Key.Visibility = child.Value;
                parent.UpdateLayout();
                if (!RenderFrameBarrier()) throw new InvalidOperationException("The restored orb visibility did not reach a rendered frame.");
            }
        }
        private static byte[] Pixels(RenderTargetBitmap bitmap)
        {
            byte[] pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
            bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);
            return pixels;
        }
        private static int MaximumAlpha(byte[] pixels)
        {
            int maximum = 0;
            for (int i = 3; i < pixels.Length; i += 4) maximum = Math.Max(maximum, pixels[i]);
            return maximum;
        }
        private static int AlphaAt(byte[] pixels, int width, int x, int y)
        { return pixels[(y * width + x) * 4 + 3]; }
        private static RenderTargetBitmap RenderWithBackdrop(Window window, Color background, bool patterned)
        {
            Border actions = window.FindName("ActionStrip") as Border;
            Visibility visibility = actions == null ? Visibility.Visible : actions.Visibility;
            try
            {
                if (actions != null) actions.Visibility = Visibility.Hidden;
                window.UpdateLayout();
                if (!RenderFrameBarrier()) throw new InvalidOperationException("The backdrop fixture visibility changes did not reach a rendered frame.");
                int width = (int)Math.Ceiling(window.ActualWidth), height = (int)Math.Ceiling(window.ActualHeight);
                DrawingVisual visual = new DrawingVisual();
                using (DrawingContext drawing = visual.RenderOpen())
                {
                    drawing.DrawRectangle(new SolidColorBrush(background), null, new Rect(0, 0, width, height));
                    if (patterned)
                    {
                        SolidColorBrush alternate = new SolidColorBrush(Color.FromRgb(214, 228, 244));
                        for (int y = 0; y < height; y += 32)
                        for (int x = 0; x < width; x += 32)
                            if ((x / 32 + y / 32) % 2 == 0) drawing.DrawRectangle(alternate, null, new Rect(x, y, Math.Min(32, width - x), Math.Min(32, height - y)));
                    }
                    drawing.DrawRectangle(new VisualBrush(window), null, new Rect(0, 0, width, height));
                }
                RenderTargetBitmap bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual); return bitmap;
            }
            finally
            {
                if (actions != null) actions.Visibility = visibility;
                window.UpdateLayout();
                if (!RenderFrameBarrier()) throw new InvalidOperationException("The restored backdrop fixture visibility did not reach a rendered frame.");
            }
        }
        private static void CheckVisibleBackdrop(Window window, double opacity, string fixture)
        {
            Border text = Layouts.Find<Border>(window, "DragArea");
            Visibility textVisibility = text.Visibility;
            try
            {
                // Exclude independent glyphs and their shadow, while retaining all actual artwork layers.
                text.Visibility = Visibility.Hidden;
                window.UpdateLayout();
                if (!RenderFrameBarrier()) throw new InvalidOperationException("The core probe text isolation did not reach a rendered frame.");
                Color dark = Color.FromRgb(17, 31, 49), light = Color.FromRgb(232, 241, 251);
                RenderTargetBitmap darkBitmap = RenderWithBackdrop(window, dark, false), lightBitmap = RenderWithBackdrop(window, light, false);
                byte[] darkPixels = Pixels(darkBitmap), lightPixels = Pixels(lightBitmap);
                foreach (Point sample in new Point[] { new Point(235, 172), new Point(172, 235) })
                {
                    int x = (int)Math.Round(sample.X * darkBitmap.PixelWidth / 344), y = (int)Math.Round(sample.Y * darkBitmap.PixelHeight / 344);
                    int index = (y * darkBitmap.PixelWidth + x) * 4;
                    double minimumTransmission = 1 - opacity;
                    string values = " (B/G/R differences=" + (lightPixels[index] - darkPixels[index]) + "/" + (lightPixels[index + 1] - darkPixels[index + 1]) + "/" + (lightPixels[index + 2] - darkPixels[index + 2]) + ")";
                    Check(lightPixels[index] - darkPixels[index] + 2 >= minimumTransmission * (light.B - dark.B) && lightPixels[index + 1] - darkPixels[index + 1] + 2 >= minimumTransmission * (light.G - dark.G) && lightPixels[index + 2] - darkPixels[index + 2] + 2 >= minimumTransmission * (light.R - dark.R), fixture + " complete-widget core transmits the desktop according to centre opacity" + values);
                }
            }
            finally
            {
                text.Visibility = textVisibility;
                window.UpdateLayout();
                if (!RenderFrameBarrier()) throw new InvalidOperationException("The restored core probe text did not reach a rendered frame.");
            }
        }
        private static void SaveBitmap(RenderTargetBitmap bitmap, string path)
        {
            PngBitmapEncoder encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (FileStream stream = File.Create(path)) encoder.Save(stream);
        }
        private static void CheckAppearanceReapply(Window window, Canvas canvas)
        {
            System.Collections.IDictionary scenes = (System.Collections.IDictionary)typeof(OrbScene).GetField("Scenes", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).GetValue(null);
            object scene = scenes[canvas];
            System.ComponentModel.PropertyChangedEventHandler callback = (System.ComponentModel.PropertyChangedEventHandler)scene.GetType().GetField("SystemChanged", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(scene);
            Color before = ((RadialGradientBrush)((System.Windows.Shapes.Shape)canvas.Children[1]).Fill).GradientStops[0].Color;
            double opacity = Layouts.Find<Grid>(window, "OrbMaterial").Opacity;
            foreach (string preference in new string[] { "ClientAreaAnimation", "HighContrast" })
            {
                bool processed = false, keptAppearance = false;
                callback(null, new System.ComponentModel.PropertyChangedEventArgs(preference));
                canvas.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
                {
                    keptAppearance = ((RadialGradientBrush)((System.Windows.Shapes.Shape)canvas.Children[1]).Fill).GradientStops[0].Color == before && canvas.Opacity == 1 && Layouts.Find<Grid>(window, "OrbMaterial").Opacity == opacity;
                    processed = true;
                }));
                Check(WaitFor(delegate { return processed; }) && keptAppearance, preference + " scene reapply retains the actual Light palette and requested material opacity");
            }
        }
        private static void CheckDirectUnitSwitches(HolidayApplication app, SettingsStore store)
        {
            Button unitButton = Layouts.Find<Button>(app.Widget, "UnitButton");
            Check(app.Settings.Unit == CountdownUnit.Seconds && app.SettingsWindow == null, "Direct unit switching starts from the saved seconds gadget with settings closed");
            foreach (CountdownUnit unit in new CountdownUnit[] { CountdownUnit.Weeks, CountdownUnit.Hours, CountdownUnit.Days, CountdownUnit.Minutes, CountdownUnit.Seconds, CountdownUnit.Weeks, CountdownUnit.Hours, CountdownUnit.Days })
            {
                unitButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(app.SettingsWindow == null && app.Widget.IsVisible && app.Settings.Unit == unit, "The gadget button switches directly to " + unit + " while settings stay closed");
                Check(unitButton.Content.ToString() == unit.ToString() && Layouts.Find<TextBlock>(app.Widget, "RemainingUnit").Text == unit.ToString().ToLowerInvariant() + " to go", "The gadget button and countdown both display " + unit);
                Check(store.Load().Unit == unit && store.Load().HolidayName == app.Settings.HolidayName, "The direct " + unit + " choice survives reload without changing the holiday");
            }
            unitButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            unitButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(app.Settings.Unit == CountdownUnit.Seconds && store.Load().Unit == CountdownUnit.Seconds, "Direct switching can restore the originally saved seconds choice");
        }
        private static void CheckCounterStability(HolidayApplication app)
        {
            Window window = app.Widget;
            Grid orb = Layouts.Find<Grid>(window, "GadgetHoverArea");
            TextBlock digits = Layouts.Find<TextBlock>(window, "RemainingNumber");
            TextBlock unit = Layouts.Find<TextBlock>(window, "RemainingUnit");
            TextBlock target = Layouts.Find<TextBlock>(window, "TargetLabel");
            System.Reflection.MethodInfo fit = typeof(HolidayApplication).GetMethod("FitCounter", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Check(fit != null && app.Settings.Unit == CountdownUnit.Seconds && unit.Text == "seconds to go", "The stability fixture uses the real seconds countdown and fitting path");
            try
            {
                double? stableFontSize = null;
                double[] stablePositions = null;
                foreach (long value in new long[] { 8888888, 7777777, 6666666, 5555555 })
                {
                    string number = value.ToString("N0", CultureInfo.CurrentCulture);
                    digits.Text = number; fit.Invoke(null, new object[] { digits, number }); window.UpdateLayout();
                    double[] positions = CounterPositions(orb, digits, unit, target);
                    if (!stableFontSize.HasValue) { stableFontSize = digits.FontSize; stablePositions = positions; }
                    Check(Math.Abs(digits.FontSize - stableFontSize.Value) < .001, "Same-length seconds containing " + value + " keep an identical font size");
                    Check(SamePositions(stablePositions, positions), "Same-length seconds containing " + value + " keep the number, unit and target rows steady");
                    Check(CounterFits(digits), "Seconds containing " + value + " fit the logical 218px width and 84px number row");
                }
                string beforeGrowth = 999999L.ToString("N0", CultureInfo.CurrentCulture);
                digits.Text = beforeGrowth; fit.Invoke(null, new object[] { digits, beforeGrowth }); window.UpdateLayout();
                double beforeGrowthFont = digits.FontSize;
                foreach (long value in new long[] { 1000000, 999999999999 })
                {
                    string number = value.ToString("N0", CultureInfo.CurrentCulture);
                    digits.Text = number; fit.Invoke(null, new object[] { digits, number }); window.UpdateLayout();
                    Check(digits.FontSize < beforeGrowthFont && CounterFits(digits), "Growing the digit count to " + value + " still fits the 218px width and 84px row");
                    Check(SamePositions(stablePositions, CounterPositions(orb, digits, unit, target)), "Digit-count growth keeps the number, unit and target rows steady");
                }
            }
            finally { app.RefreshCountdown(); window.UpdateLayout(); }
        }
        private static double[] CounterPositions(Grid orb, TextBlock digits, TextBlock unit, TextBlock target)
        {
            return new double[] {
                digits.TransformToAncestor(orb).Transform(new Point(0, 0)).Y,
                unit.TransformToAncestor(orb).Transform(new Point(0, 0)).Y,
                target.TransformToAncestor(orb).Transform(new Point(0, 0)).Y
            };
        }
        private static bool SamePositions(double[] first, double[] second)
        {
            for (int i = 0; i < first.Length; i++) if (Math.Abs(first[i] - second[i]) > .01) return false;
            return true;
        }
        private static bool CounterFits(TextBlock digits)
        {
            FormattedText measured = new FormattedText(digits.Text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface(digits.FontFamily, digits.FontStyle, digits.FontWeight, digits.FontStretch), digits.FontSize,
                Brushes.Black, VisualTreeHelper.GetDpi(digits).PixelsPerDip);
            return measured.WidthIncludingTrailingWhitespace <= Math.Min(218, digits.ActualWidth) + .01
                && measured.Height <= 84 && Math.Abs(digits.ActualHeight - 84) < .01;
        }
        private static void CheckGadgetControls(Window window, Window form, string root, bool render)
        {
            Border actions = Layouts.Find<Border>(window, "ActionStrip");
            Grid hoverArea = Layouts.Find<Grid>(window, "GadgetHoverArea");
            Button pin = Layouts.Find<Button>(window, "WorkdayButton");
            double left = window.Left, top = window.Top;
            try
            {
                // Keep the physical pointer outside the fixture so synthetic leave events are deterministic.
                window.Left = SystemParameters.VirtualScreenLeft - window.Width - 100;
                window.Top = SystemParameters.VirtualScreenTop - window.Height - 100;
                hoverArea.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount) { RoutedEvent = Mouse.MouseLeaveEvent });
                Check(WaitFor(delegate { return actions.Opacity < .001 && !actions.IsHitTestVisible; }), "Idle orb controls become invisible and leave no pointer targets");
                Check(actions.Visibility == Visibility.Visible && pin.Focusable, "Idle orb controls remain available to keyboard navigation");
                string images = Path.Combine(root, "previews");
                if (render) { Directory.CreateDirectory(images); Render(window, Path.Combine(images, "gadget-idle.png")); }

                hoverArea.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount) { RoutedEvent = Mouse.MouseEnterEvent });
                Check(actions.Opacity == 1 && actions.IsHitTestVisible, "Hover reveals the actual orb controls and enables their pointer targets");
                if (render) Render(window, Path.Combine(images, "gadget-hover.png"), false, true);
                hoverArea.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount) { RoutedEvent = Mouse.MouseLeaveEvent });
                Check(actions.Opacity == 1 && actions.IsHitTestVisible, "Pointer departure schedules the hide without immediately removing orb controls");
                Check(WaitFor(delegate { return actions.Opacity < .001 && !actions.IsHitTestVisible; }, 900), "Leaving the orb hides controls after the short delay and disables pointer targets");

                window.Activate();
                window.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), Environment.TickCount, Key.Tab) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
                Check(pin.Focus() && actions.IsKeyboardFocusWithin, "Tab can focus the orb controls while they were hidden");
                Pump(240);
                Check(actions.Opacity == 1 && actions.IsHitTestVisible, "Keyboard focus keeps the orb controls visible beyond the pointer hide delay");
                form.Activate();
                Layouts.Find<TextBox>(form, "HolidayName").Focus();
                Check(WaitFor(delegate { return actions.Opacity < .001 && !actions.IsHitTestVisible; }), "Leaving keyboard focus hides the orb controls again");

                hoverArea.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount) { RoutedEvent = Mouse.MouseEnterEvent });
                window.Hide();
                hoverArea.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount) { RoutedEvent = Mouse.MouseEnterEvent });
                Check(actions.Opacity < .001 && !actions.IsHitTestVisible, "Hiding the orb resets controls and detaches hover handling");
                window.Show();
                hoverArea.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount) { RoutedEvent = Mouse.MouseEnterEvent });
                Check(actions.Opacity == 1 && actions.IsHitTestVisible, "Restoring the orb reconnects hover handling");
                hoverArea.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount) { RoutedEvent = Mouse.MouseLeaveEvent });
                Check(WaitFor(delegate { return actions.Opacity < .001 && !actions.IsHitTestVisible; }), "Restored orb controls still hide after pointer departure");
            }
            finally
            {
                window.Left = left; window.Top = top;
                if (!window.IsVisible) window.Show();
                form.Activate(); Layouts.Find<TextBox>(form, "HolidayName").Focus();
            }
        }
        private static void Render(Window window, string path, bool lightBackdrop = false, bool showControls = false)
        {
            Border actions = window.FindName("ActionStrip") as Border;
            Visibility actionVisibility = actions == null ? Visibility.Visible : actions.Visibility;
            if (actions != null && !showControls) actions.Visibility = Visibility.Hidden;
            try
            {
                window.UpdateLayout();
                FrameworkElement content = (FrameworkElement)window.Content;
                content.UpdateLayout();
                int width = (int)Math.Ceiling(window.ActualWidth), height = (int)Math.Ceiling(window.ActualHeight);
                if (width <= 0 || height <= 0) throw new Exception("The layout did not measure.");
                RenderTargetBitmap bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                DrawingVisual preview = new DrawingVisual();
                using (DrawingContext drawing = preview.RenderOpen())
                {
                    drawing.DrawRectangle(new SolidColorBrush(lightBackdrop ? Color.FromRgb(226, 233, 243) : Color.FromRgb(17, 22, 31)), null, new Rect(0, 0, width, height));
                    drawing.DrawRectangle(new VisualBrush(window), null, new Rect(0, 0, width, height));
                }
                bitmap.Render(preview);
                PngBitmapEncoder encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using (FileStream stream = File.Create(path)) encoder.Save(stream);
            }
            finally { if (actions != null) actions.Visibility = actionVisibility; }
        }
        private static void Pump(int milliseconds)
        {
            DispatcherFrame frame = new DispatcherFrame();
            DispatcherTimer stop = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
            stop.Tick += delegate { stop.Stop(); frame.Continue = false; };
            stop.Start(); Dispatcher.PushFrame(frame);
        }
        private static bool WaitFor(Func<bool> condition, int timeoutMilliseconds = 1500)
        {
            System.Diagnostics.Stopwatch deadline = System.Diagnostics.Stopwatch.StartNew();
            while (!condition() && deadline.ElapsedMilliseconds < timeoutMilliseconds) Pump(40);
            return condition();
        }
        private static byte PanelAlpha(Window form, double opacity)
        { return GlassWindow.GetEnabled(form) ? (byte)Math.Round(255 * opacity) : (byte)255; }
        private static double Contrast(Color first, Color second)
        {
            double firstLuminance = Luminance(first), secondLuminance = Luminance(second);
            return (Math.Max(firstLuminance, secondLuminance) + .05) / (Math.Min(firstLuminance, secondLuminance) + .05);
        }
        private static double Luminance(Color colour)
        { return .2126 * LinearChannel(colour.R) + .7152 * LinearChannel(colour.G) + .0722 * LinearChannel(colour.B); }
        private static double LinearChannel(byte value)
        { double channel = value / 255.0; return channel <= .04045 ? channel / 12.92 : Math.Pow((channel + .055) / 1.055, 2.4); }
        private static string ThemeName(HolidayTheme theme)
        { return theme == HolidayTheme.Sunset ? "violet" : theme == HolidayTheme.Festive ? "ember" : "aurora"; }
        private static bool CelebrationPaletteMatches(Window window)
        {
            Color[] expected = window.Resources["CelebrationColours"] as Color[];
            Color[] actual = Celebration.BaseColours(window);
            return expected != null && actual != null && System.Linq.Enumerable.SequenceEqual(expected, actual);
        }
    }
}
