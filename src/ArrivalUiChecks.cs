using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace HolidayTimer
{
    internal static partial class UiChecks
    {
        private static AppSettings ArrivalSettings(DateTime target)
        {
            return new AppSettings { IsConfigured = true, HolidayName = "The big moment",
                TargetLocal = target.ToString(CountdownCalculator.TargetFormat, CultureInfo.InvariantCulture),
                MorningGreeting = false, StartAtLogin = false, Unit = CountdownUnit.Seconds };
        }

        private static void CheckArrivalCelebrations(HolidayApplication app, SettingsStore store, string root, bool render)
        {
            AppSettings original = app.Settings;
            Func<System.Drawing.Rectangle[]> originalScreens = app.GreetingScreenBoundsProvider;
            DispatcherTimer mainTimer = (DispatcherTimer)typeof(HolidayApplication).GetField("timer", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(app);
            bool wasEnabled = mainTimer.IsEnabled;
            mainTimer.Stop(); app.CloseGreetings();
            try
            {
                app.GreetingScreenBoundsProvider = delegate { return new[] {
                    new System.Drawing.Rectangle(0, 0, 1024, 768),
                    new System.Drawing.Rectangle(-800, 0, 800, 600) }; };
                DateTime target = DateTime.Today.AddDays(3).AddHours(14);
                app.Settings = ArrivalSettings(target); store.Save(app.Settings);
                Check(!app.CheckArrival(target.AddSeconds(-1)) && app.GreetingWindows.Count == 0,
                    "The saved countdown does not celebrate before the actual target instant");
                app.SessionAvailable = false;
                Check(!app.CheckArrival(target) && app.Settings.LastArrivalTarget == null && store.Load().LastArrivalTarget == null,
                    "A locked session defers arrival without consuming its persistent marker");
                app.SessionAvailable = true;
                app.ProcessCelebrations(target);
                Check(app.GreetingWindows.Count == 2 && app.Greeting != null && app.Greeting.IsVisible,
                    "The scheduler automatically opens one arrival greeting on each supplied screen");
                foreach (Window window in app.GreetingWindows)
                    Check(Layouts.Find<TextBlock>(window, "GreetingNumber").Text == "0" &&
                        Layouts.Find<TextBlock>(window, "GreetingIntro").Text.Contains("is here"),
                        "Every screen displays the arrived state at the exact target");
                Check(app.Settings.LastArrivalTarget == app.Settings.TargetLocal && store.Load().LastArrivalTarget == app.Settings.TargetLocal,
                    "A delivered arrival persists the selected target marker independently of morning preference");
                Window first = app.Greeting;
                Check(!app.CheckArrival(target.AddSeconds(1)) && app.Greeting == first,
                    "Repeated ticks do not restart the same arrival group");

                if (render)
                {
                    string previews = Path.Combine(root, "previews"); Directory.CreateDirectory(previews);
                    for (int i = 0; i < app.GreetingWindows.Count; i++)
                    {
                        Celebration.RenderAt(app.GreetingWindows[i], 120.5, app.Settings.CelebrationEffect);
                        Render(app.GreetingWindows[i], Path.Combine(previews, "arrival-screen-" + i + ".png"));
                    }
                }
                string marker = app.Settings.LastArrivalTarget;
                app.GreetingScreenBoundsProvider = delegate { return new[] {
                    new System.Drawing.Rectangle(0, 0, 1024, 768),
                    new System.Drawing.Rectangle(-800, 0, 800, 600),
                    new System.Drawing.Rectangle(0, -640, 1024, 640) }; };
                app.ReflowGreetings();
                Check(app.GreetingWindows.Count == 3 && app.Greeting != first && !first.IsVisible && app.Settings.LastArrivalTarget == marker,
                    "Display reflow replaces the group without resetting arrival metadata");
                Layouts.Find<Button>(app.GreetingWindows[2], "GreetingDismiss").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(app.GreetingWindows.Count == 0 && app.Greeting == null && Celebration.ActiveRunCount == 0,
                    "Continue on a secondary screen dismisses the entire arrival group");
                app.Settings = store.Load();
                Check(!app.CheckArrival(target.AddMinutes(1)) && app.GreetingWindows.Count == 0,
                    "Reloading saved preferences does not repeat a delivered arrival");

                app.OpenSettings();
                Layouts.Find<TextBox>(app.SettingsWindow, "HolidayName").Text = "A renamed celebration";
                Check(Layouts.Find<TextBlock>(app.SettingsWindow, "ArrivalHint").Text.Contains("every screen"),
                    "Settings explain automatic arrival on every screen");
                Check(app.SaveForm(app.SettingsWindow) && app.Settings.LastArrivalTarget == marker && store.Load().LastArrivalTarget == marker,
                    "Saving appearance or name edits preserves the arrival marker");
                Check(!app.CheckArrival(target.AddMinutes(2)), "A metadata edit cannot replay the same target");

                string saved = File.ReadAllText(store.FilePath);
                AppSettings draft = ArrivalSettings(target.AddDays(2)); draft.HolidayName = "Unsaved preview";
                draft.CelebrationEffect = CelebrationKind.Flowers;
                app.PreviewCelebration(draft, target);
                Check(app.GreetingWindows.Count == 3 && File.ReadAllText(store.FilePath) == saved && draft.LastArrivalTarget == null,
                    "An all-screen draft preview neither persists nor consumes an arrival");
                Window escape = app.GreetingWindows[1];
                escape.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(escape), Environment.TickCount,
                    Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent, Handled = true });
                Check(app.GreetingWindows.Count == 0 && app.Greeting == null, "Escape on a secondary screen closes every preview");

                DateTime monday = DateTime.Today.AddDays(14);
                while (monday.DayOfWeek != DayOfWeek.Monday) monday = monday.AddDays(1);
                app.Settings = ArrivalSettings(monday.AddHours(9)); app.Settings.WorkdayCountdown = true;
                Check(!app.CheckArrival(monday.AddDays(-1).AddHours(12)) && app.Settings.LastArrivalTarget == null,
                    "A nonworking weekend before the target cannot dispatch an early arrival");

                app.Settings = ArrivalSettings(DateTime.Now.AddSeconds(2));
                store.Save(app.Settings); mainTimer.Start();
                Check(WaitFor(delegate { return app.GreetingWindows.Count == 3; }, 6500),
                    "The actual one-second timer opens all-screen celebration when its live countdown reaches zero");
                Check(Layouts.Find<TextBlock>(app.Widget, "RemainingNumber").Text == "0" &&
                    Layouts.Find<TextBlock>(app.Greeting, "GreetingIntro").Text.Contains("is here"),
                    "Live timer arrival and the gadget's zero state agree");
                string arrivedTarget = app.Settings.TargetLocal;
                app.CloseGreetings(); Pump(1200);
                Check(app.GreetingWindows.Count == 0 && store.Load().LastArrivalTarget == arrivedTarget &&
                    app.Settings.AutomaticPublicHoliday && store.Load().TargetLocal == app.Settings.TargetLocal &&
                    CountdownCalculator.ToLocalTarget(app.Settings.TargetLocal) > DateTimeOffset.Now,
                    "Dismissing a live arrival keeps its marker and advances to the next public holiday");
                mainTimer.Stop();

                app.Settings = ArrivalSettings(DateTime.Now.AddMinutes(-1));
                app.SessionAvailable = false;
                Check(!app.CheckArrival(DateTime.Now) && app.Settings.LastArrivalTarget == null,
                    "A missed target remains pending while locked");
                app.SessionAvailable = true; app.ProcessCelebrations(DateTime.Now);
                Check(app.GreetingWindows.Count == 3 && store.Load().LastArrivalTarget == app.Settings.TargetLocal,
                    "The first unlocked scheduling opportunity catches up a missed arrival once");
                app.CloseGreetings();
            }
            finally
            {
                mainTimer.Stop(); app.CloseGreetings();
                if (app.SettingsWindow != null) app.SettingsWindow.Close();
                app.Settings = original; app.GreetingScreenBoundsProvider = originalScreens; app.SessionAvailable = true;
                store.Save(original); app.RefreshCountdown();
                if (wasEnabled) mainTimer.Start();
            }
        }
    }
}
