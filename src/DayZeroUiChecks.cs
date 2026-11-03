using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Rectangle = System.Drawing.Rectangle;

namespace HolidayTimer
{
    internal static partial class UiChecks
    {
        private static void CheckDayZeroPreview(HolidayApplication app, SettingsStore store)
        {
            string saved = File.ReadAllText(store.FilePath);
            Func<Rectangle[]> oldDisplays = app.GreetingScreenBoundsProvider;
            app.OpenSettings();
            Window form = app.SettingsWindow;
            try
            {
                app.GreetingScreenBoundsProvider = delegate { return new[] {
                    new Rectangle(0, 0, 680, 400), new Rectangle(-460, 0, 460, 350) }; };
                Layouts.Find<CheckBox>(form, "AlwaysAnimateCelebrations").IsChecked = true;
                Layouts.Find<CheckBox>(form, "DayZeroEnabled").IsChecked = false;
                AppSettings draft; string error;
                Check(app.ReadForm(form, false, out draft, out error) && !draft.DayZeroEnabled,
                    "The day-zero toggle can be disabled independently");
                var sampleDay = Layouts.Find<ComboBox>(form, "StarburstPreviewDay");
                Button play = Layouts.Find<Button>(form, "PreviewDayZero");
                Check(sampleDay.SelectedIndex == 3 && sampleDay.Items.Count == 4,
                    "The starburst preview offers three lead-in days and the event day");
                for (int i = 0; i < 3; i++)
                {
                    sampleDay.SelectedIndex = i;
                    play.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    int days = 3 - i;
                    string caption = DayZeroScene.StageCaption(days), message = DayZeroScene.StageMessage(days);
                    Check(app.Greeting.Title.Contains(caption) && app.Greeting.Title.Contains(message),
                        "Preview opens the selected countdown day and its message");
                    app.CloseGreetings();
                }
                sampleDay.SelectedIndex = 1;
                play.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(app.IsDayZeroOpen && app.GreetingWindows.Count == 2, "Day-zero preview fills both displays even when the automatic show is off");
                foreach (Window window in app.GreetingWindows)
                    Check(!window.AllowsTransparency && ((SolidColorBrush)window.Background).Color == Colors.Black,
                        "Each day-zero window has an opaque black backdrop");
                if (!SystemParameters.HighContrast)
                    Check(WaitFor(delegate { return app.DayZeroElapsed > .1; }, 3500),
                        "The starburst clock starts after both monitors present the ring");
                double elapsed = app.DayZeroElapsed;
                app.ReflowGreetings();
                Check(app.IsDayZeroOpen && app.DayZeroElapsed >= elapsed && app.Greeting.Title.Contains("2 days to go"),
                    "Display changes preserve the countdown day and reveal phase");
                Window second = app.GreetingWindows[1];
                second.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(second),
                    Environment.TickCount, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
                Check(!app.IsDayZeroOpen && app.Greeting == null && app.GreetingWindows.Count == 0, "Escape on either display ends the entire starburst");
                Check(File.ReadAllText(store.FilePath) == saved, "A manual day-zero preview leaves the saved countdown and unlock markers untouched");
                CheckDayZeroLetterDust();
                CheckDayZeroSkyTransition();
            }
            finally
            {
                app.CloseGreetings();
                app.GreetingScreenBoundsProvider = oldDisplays;
                form.Close();
            }
        }

        private static void CheckDayZeroLetterDust()
        {
            // Matching primary and secondary views isolate letter movement from the sky's own response.
            Size size = new Size(480, 270);
            DayZeroScene letters = new DayZeroScene(), sky = new DayZeroScene();
            foreach (DayZeroScene layer in new[] { letters, sky })
            {
                layer.Configure(layer == letters, 419, Colors.SkyBlue);
                layer.Measure(size); layer.Arrange(new Rect(size));
            }
            Point cursor = new Point(209, 128);
            byte[] plain = DayZeroFrame(letters, 24.8), plainSky = DayZeroFrame(sky, 24.8);
            DayZeroFrame(letters, 24); DayZeroFrame(sky, 24);
            letters.SetPointer(cursor); sky.SetPointer(cursor);
            for (int j = 1; j < 8; j++)
            { DayZeroFrame(letters, 24 + j * .1); DayZeroFrame(sky, 24 + j * .1); }
            byte[] pulled = DayZeroFrame(letters, 24.8), pulledSky = DayZeroFrame(sky, 24.8);
            int brightGrains = 0, vacated = 0;
            for (int y = 110; y <= 146; y++)
                for (int x = 191; x <= 227; x++)
                {
                    double radius = (new Point(x, y) - cursor).Length;
                    if (radius < 8 || radius > 18) continue;
                    int i = (y * 480 + x) * 4;
                    int ink = plain[i] + plain[i + 1] + plain[i + 2] - plainSky[i] - plainSky[i + 1] - plainSky[i + 2];
                    if (ink < 186) continue;
                    brightGrains++;
                    int remainingInk = pulled[i] + pulled[i + 1] + pulled[i + 2] - pulledSky[i] - pulledSky[i + 1] - pulledSky[i + 2];
                    if (remainingInk < ink * .20) vacated++;
                }
            Check(brightGrains > 50 && vacated > brightGrains / 3,
                "Brushing the message clears original grain positions without leaving a fixed copy behind");
            letters.SetPointer(null); sky.SetPointer(null);
            for (int i = 1; i <= 14; i++) DayZeroFrame(letters, 24.8 + i * .1);
            byte[] reset = DayZeroFrame(letters, 26.2);
            DayZeroFrame(letters, 22);
            byte[] resting = DayZeroFrame(letters, 26.2);
            Check(reset.SequenceEqual(resting),
                "Letter grains return to their original positions after the pointer leaves");
        }

        private static void CheckDayZeroSkyTransition()
        {
            // A secondary display includes the outgoing rays and the sky but no lettering.
            Size size = new Size(480, 270);
            var scene = new DayZeroScene();
            scene.Configure(false, 532, Colors.SkyBlue);
            scene.SetOrigin(new Point(-.5, .5));
            scene.Measure(size); scene.Arrange(new Rect(size));
            byte[] lastRays = DayZeroFrame(scene, 15.85);
            int darkPixels = 0;
            for (int i = 0; i < lastRays.Length; i += 4)
                if (lastRays[i] == 0 && lastRays[i + 1] == 0 && lastRays[i + 2] == 0) darkPixels++;
            Check(darkPixels > lastRays.Length / 4 * .95,
                "The sky stays dark until the outgoing starburst finishes");
            int early = FrameLight(DayZeroFrame(scene, 16.45));
            int middle = FrameLight(DayZeroFrame(scene, 17.35));
            int settled = FrameLight(DayZeroFrame(scene, 19.5));
            Check(early < middle && middle < settled && middle > settled * .1 && middle < settled * .9,
                "The galaxy backdrop emerges progressively after the burst instead of appearing in one frame");
        }

        private static int FrameLight(byte[] pixels)
        {
            int sum = 0;
            for (int i = 0; i < pixels.Length; i += 4) sum += pixels[i] + pixels[i + 1] + pixels[i + 2];
            return sum;
        }

        private static byte[] DayZeroFrame(DayZeroScene scene, double seconds)
        {
            scene.SetFrame(seconds, false); scene.UpdateLayout();
            var frame = new RenderTargetBitmap(480, 270, 96, 96, PixelFormats.Pbgra32);
            frame.Render(scene); return Pixels(frame);
        }
    }
}
