using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Rectangle = System.Drawing.Rectangle;

namespace HolidayTimer
{
    internal sealed partial class HolidayApplication
    {
        private DayZeroPlayback dayZeroPlayback;
        internal string StandalonePreview;
        private void StartStandalonePreview()
        {
            ShutdownMode = ShutdownMode.OnLastWindowClose;
            Resources.MergedDictionaries.Add(Layouts.LoadControls());
            if (StandalonePreview == "--preview-day-zero") ShowDayZero(Settings);
            else
            {
                DateTime now = DateTime.Now;
                Settings.HolidayName = "Your holiday";
                Settings.TargetLocal = now.ToString(CountdownCalculator.TargetFormat, System.Globalization.CultureInfo.InvariantCulture);
                ShowGreeting(Settings, now);
            }
        }
        internal bool IsDayZeroOpen { get { return dayZeroPlayback != null; } }
        internal double DayZeroElapsed { get { return dayZeroPlayback == null ? 0 : dayZeroPlayback.Seconds; } }

        private void ShowDayZero(AppSettings settings, int daysRemaining = 0)
        {
            PresentGreetings(settings, DateTime.Now, true, daysRemaining);
        }
    }

    // One clock for all monitors, starting when each window has presented its opening frame.
    internal sealed class DayZeroPlayback
    {
        private readonly List<DayZeroScene> scenes = new List<DayZeroScene>();
        private readonly bool alwaysAnimate;
        private TextBlock staticMessage;
        private int presented;
        private bool active, rendering;
        private bool sentPointerFrame;
        private TimeSpan? previousFrame;
        internal int DaysRemaining { get; private set; }
        internal double Seconds { get; private set; }

        internal DayZeroPlayback(bool alwaysAnimate, int daysRemaining, double elapsed)
        { this.alwaysAnimate = alwaysAnimate; DaysRemaining = daysRemaining; Seconds = elapsed; }

        internal void Prepare(Window window, Rectangle main, Rectangle bounds, int index)
        {
            string message = DayZeroScene.StageMessage(DaysRemaining);
            string count = DayZeroScene.StageCaption(DaysRemaining);
            string description = count.Length == 0 ? message : count + " · " + message;
            window.Title = description + " — Holiday Timer";
            window.AllowsTransparency = false;
            window.Background = Brushes.Black;
            Grid root = (Grid)window.Content;
            Layouts.Find<StackPanel>(window, "GreetingMessage").Visibility = Visibility.Collapsed;
            Button dismiss = Layouts.Find<Button>(window, "GreetingDismiss");
            dismiss.FontSize = 12;
            dismiss.Margin = new Thickness(0, 0, 0, 16);
            DayZeroScene scene = new DayZeroScene();
            Color accent = ((SolidColorBrush)window.Resources["Accent"]).Color;
            scene.Configure(index == 0, 419 + index * 113, accent, DaysRemaining);
            scene.SetOrigin(new Point((main.Left + main.Width * .5 - bounds.Left) / bounds.Width,
                (main.Top + main.Height * .5 - bounds.Top) / bounds.Height));
            AutomationProperties.SetName(scene, description);
            root.Children.Insert(0, scene);
            scenes.Add(scene);
            if (index == 0)
            {
                staticMessage = new TextBlock { Text = count.Length == 0 ? message : count + "\n" + message, Foreground = Brushes.White,
                    FontSize = 42, FontWeight = FontWeights.SemiBold, TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap, HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center, Visibility = Visibility.Collapsed };
                root.Children.Insert(1, staticMessage);
            }
            window.MouseMove += delegate(object sender, System.Windows.Input.MouseEventArgs e)
            { if (rendering) scene.SetPointer(e.GetPosition(scene)); };
            window.MouseLeave += delegate { scene.SetPointer(null); };
            window.ContentRendered += delegate { presented++; };
            scene.SetFrame(Seconds, false);
        }

        internal void Start()
        {
            active = true;
            SystemParameters.StaticPropertyChanged += PreferenceChanged;
            ApplyMotionPreference();
        }

        private void PreferenceChanged(object sender, PropertyChangedEventArgs args)
        {
            if (!active || scenes.Count == 0) return;
            scenes[0].Dispatcher.BeginInvoke(new Action(delegate { if (active) ApplyMotionPreference(); }));
        }

        private void ApplyMotionPreference()
        {
            bool motion = Celebration.AnimationAllowed(SystemParameters.ClientAreaAnimation,
                SystemParameters.HighContrast, alwaysAnimate);
            if (motion != rendering)
            {
                rendering = motion;
                previousFrame = null;
                sentPointerFrame = false;
                if (motion) CompositionTarget.Rendering += Render;
                else CompositionTarget.Rendering -= Render;
            }
            staticMessage.Visibility = motion ? Visibility.Collapsed : Visibility.Visible;
            foreach (DayZeroScene scene in scenes)
            {
                scene.Visibility = motion ? Visibility.Visible : Visibility.Hidden;
                scene.SetFrame(Seconds, !motion);
            }
        }

        private void Render(object sender, EventArgs args)
        {
            if (presented < scenes.Count) return;
            TimeSpan now = ((RenderingEventArgs)args).RenderingTime;
            if (!previousFrame.HasValue || now < previousFrame.Value) { previousFrame = now; return; }
            double delta = (now - previousFrame.Value).TotalSeconds;
            if (delta < 1.0 / 32)
            {
                // Consume the latest mouse position at the next display frame.
                // Other monitors stay on the shared animation cadence.
                if (!sentPointerFrame && delta >= 1.0 / 65)
                    foreach (DayZeroScene scene in scenes)
                        if (scene.NeedsPointerFrame)
                        {
                            scene.SetFrame(Seconds + delta, false);
                            sentPointerFrame = true;
                        }
                return;
            }
            // Hold the sign-in opening through long stalls. Once the ring has
            // completed, ordinary slow frames should not stretch the burst.
            Seconds += Math.Min(delta, Seconds < 5 ? .1 : .25);
            previousFrame = now;
            sentPointerFrame = false;
            foreach (DayZeroScene scene in scenes) scene.SetFrame(Seconds, false);
        }

        internal void Stop()
        {
            active = false;
            SystemParameters.StaticPropertyChanged -= PreferenceChanged;
            if (rendering) CompositionTarget.Rendering -= Render;
            rendering = false;
        }
    }
}
