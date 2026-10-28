using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Forms = System.Windows.Forms;

namespace HolidayTimer
{
    // A greeting is one presentation group, with a separate transparent
    // window per physical display. Its message never crosses a monitor seam.
    internal sealed partial class HolidayApplication
    {
        private readonly List<Window> greetingWindows = new List<Window>();
        private readonly Dictionary<Window, Rectangle> greetingBounds = new Dictionary<Window, Rectangle>();
        private readonly HashSet<Window> closingGreetingWindows = new HashSet<Window>();
        private AppSettings greetingSettings;
        private DateTime greetingNow;
        private bool greetingAnimate, closingGreetings;

        // Tests can supply physical rectangles without changing display settings.
        // Like the production enumeration, the first rectangle is the primary.
        internal Func<Rectangle[]> GreetingScreenBoundsProvider;
        internal IReadOnlyList<Window> GreetingWindows { get { return greetingWindows.AsReadOnly(); } }

        internal void PreviewCelebration(AppSettings draft, DateTime now)
        {
            CloseGreetings();
            ShowGreeting(draft, now);
        }

        internal void ShowGreeting(AppSettings settings, DateTime now, bool animate = true)
        {
            if (settings == null) throw new ArgumentNullException("settings");
            if (Greeting != null) { Greeting.Activate(); return; }
            PresentGreetings(settings, now, animate);
        }

        private Rectangle[] GreetingDisplays()
        {
            Rectangle[] supplied;
            if (GreetingScreenBoundsProvider != null) supplied = GreetingScreenBoundsProvider();
            else
            {
                List<Rectangle> actual = new List<Rectangle>();
                Forms.Screen[] screens = Forms.Screen.AllScreens;
                foreach (Forms.Screen screen in screens) if (screen.Primary) actual.Add(screen.Bounds);
                foreach (Forms.Screen screen in screens) if (!screen.Primary) actual.Add(screen.Bounds);
                supplied = actual.ToArray();
            }
            if (supplied == null || supplied.Length == 0)
                throw new InvalidOperationException("No display is available for the celebration.");
            List<Rectangle> bounds = new List<Rectangle>();
            foreach (Rectangle value in supplied)
            {
                if (value.Width <= 0 || value.Height <= 0)
                    throw new ArgumentException("Celebration displays must have positive physical dimensions.");
                if (!bounds.Contains(value)) bounds.Add(value);
            }
            return bounds.ToArray();
        }

        private void PresentGreetings(AppSettings settings, DateTime now, bool animate)
        {
            Rectangle[] displays = GreetingDisplays();
            List<Window> prepared = new List<Window>();
            bool committed = false;
            try
            {
                // Prepare and position every member before publishing the group.
                // A failure cannot leave an orphaned window or a second hotkey.
                foreach (Rectangle bounds in displays)
                {
                    Window window = Layouts.Load("GreetingWindow");
                    prepared.Add(window);
                    window.ShowActivated = false;
                    window.Opacity = 0;
                    Themes.Apply(window, settings.Theme, settings.BackgroundOpacity, settings.LightAppearance, settings.CountdownTextColour);
                    FontChoices.Apply(window, settings.DisplayFont);
                    UpdateGreetingWindow(window, settings, now);
                    Layouts.Find<Button>(window, "GreetingDismiss").Click += delegate { CloseGreetings(); };
                    window.Closing += delegate { closingGreetingWindows.Add(window); };
                    window.Closed += GreetingClosed;
                    window.IsVisibleChanged += GreetingVisibilityChanged;
                    window.StateChanged += GreetingStateChanged;
                    window.DpiChanged += delegate { QueueGreetingSize(window); };
                    window.Show();
                    Celebration.FullScreen(window, bounds);
                    SizeGreeting(window);
                    window.UpdateLayout();
                }

                CloseGreetings();
                greetingSettings = settings; greetingNow = now; greetingAnimate = animate;
                greetingWindows.AddRange(prepared);
                for (int i = 0; i < prepared.Count; i++) greetingBounds.Add(prepared[i], displays[i]);
                Greeting = prepared[0];
                committed = true;
                for (int i = 0; i < prepared.Count; i++)
                {
                    Window window = prepared[i];
                    CelebrationDismissal.Attach(window, i == 0 && desktopIntegration);
                    window.Opacity = 1;
                    Celebration.Play(window, animate && desktopIntegration, settings.CelebrationEffect, settings.AlwaysAnimateCelebrations);
                    Motion.Attach(window, desktopIntegration);
                }
                Greeting.Activate();
            }
            catch
            {
                if (committed) CloseGreetings();
                else foreach (Window window in prepared) { Celebration.Stop(window); window.Close(); }
                throw;
            }
        }

        internal void CloseGreetings()
        {
            if (closingGreetings) return;
            closingGreetings = true;
            Window[] closing = greetingWindows.ToArray();
            greetingWindows.Clear(); greetingBounds.Clear();
            Greeting = null; greetingSettings = null;
            try
            {
                foreach (Window window in closing) { Celebration.Stop(window); window.Close(); }
            }
            finally { closingGreetings = false; }
        }

        internal void ReflowGreetings()
        {
            if (Greeting == null || greetingSettings == null) return;
            // Rebuild only the presentation; neither daily nor arrival markers
            // belong here, so hot-plug/DPI changes cannot consume either event.
            PresentGreetings(greetingSettings, greetingNow, greetingAnimate);
        }

        private void GreetingClosed(object sender, EventArgs args)
        {
            Window window = (Window)sender;
            Celebration.Stop(window);
            closingGreetingWindows.Remove(window);
            if (!closingGreetings && greetingWindows.Remove(window))
            { greetingBounds.Remove(window); CloseGreetings(); }
        }
        private void GreetingVisibilityChanged(object sender, DependencyPropertyChangedEventArgs args)
        {
            Window window = (Window)sender;
            if (!window.IsVisible && !closingGreetingWindows.Contains(window) && greetingWindows.Contains(window)) CloseGreetings();
        }
        private void GreetingStateChanged(object sender, EventArgs args)
        {
            Window window = (Window)sender;
            if (window.WindowState == WindowState.Minimized && greetingWindows.Contains(window)) CloseGreetings();
        }
        private void QueueGreetingSize(Window window)
        {
            if (Dispatcher.HasShutdownStarted) return;
            Dispatcher.BeginInvoke(new Action(delegate
            {
                Rectangle bounds;
                if (!greetingBounds.TryGetValue(window, out bounds)) return;
                Celebration.FullScreen(window, bounds);
                SizeGreeting(window); window.UpdateLayout();
            }));
        }

        private static void SizeGreeting(Window window)
        {
            double scale = Math.Max(.35, Math.Min(2.4, Math.Min(window.ActualWidth / 1440, window.ActualHeight / 900)));
            Layouts.Find<StackPanel>(window, "GreetingMessage").MaxWidth = Math.Max(1, Math.Min(window.ActualWidth - 72, 850 * scale));
            Layouts.Find<TextBlock>(window, "GreetingHeading").FontSize = 48 * scale;
            Layouts.Find<TextBlock>(window, "GreetingIntro").FontSize = 20 * scale;
            Layouts.Find<TextBlock>(window, "GreetingNumber").FontSize = 96 * scale;
            Layouts.Find<TextBlock>(window, "GreetingUnit").FontSize = 20 * scale;
            Layouts.Find<TextBlock>(window, "GreetingHoliday").FontSize = 30 * scale;
            Layouts.Find<TextBlock>(window, "GreetingHoliday").MaxHeight = 80 * scale;
            Layouts.Find<TextBlock>(window, "GreetingTarget").FontSize = 14 * scale;
            Layouts.Find<Button>(window, "GreetingDismiss").FontSize = Math.Max(10, Math.Min(20, 14 * scale));
        }

        internal void UpdateGreeting(DateTime now)
        {
            if (Greeting == null || greetingSettings == null) return;
            greetingNow = now;
            foreach (Window window in greetingWindows) UpdateGreetingWindow(window, greetingSettings, now);
        }
        private static void UpdateGreetingWindow(Window window, AppSettings settings, DateTime now)
        {
            long days;
            bool available = TryRemaining(settings, new DateTimeOffset(now), CountdownUnit.Days, out days);
            bool arrived = CountdownCalculator.ToLocalTarget(settings.TargetLocal) <= new DateTimeOffset(now);
            Layouts.Find<TextBlock>(window, "GreetingNumber").Text = available ? days.ToString("N0", CultureInfo.CurrentCulture) : "—";
            Layouts.Find<TextBlock>(window, "GreetingIntro").Text = arrived ? "Your holiday is here. Enjoy every moment." : !available ? "Your NSW calendar needs an update." : settings.WorkdayCountdown && days == 0 ? "No work time left before your holiday." : "Your holiday is getting closer.";
            Layouts.Find<TextBlock>(window, "GreetingUnit").Text = arrived ? "Today is the day for" : !available ? "Countdown paused for" : UnitLabel(CountdownUnit.Days, days, settings.WorkdayCountdown) + " to go until";
            Layouts.Find<TextBlock>(window, "GreetingHoliday").Text = settings.HolidayName;
            Layouts.Find<TextBlock>(window, "GreetingHoliday").ToolTip = settings.HolidayName;
            Layouts.Find<TextBlock>(window, "GreetingTarget").Text = CountdownCalculator.ParseTarget(settings.TargetLocal).ToString("dddd, d MMMM yyyy", CultureInfo.CurrentCulture);
        }
    }
}
