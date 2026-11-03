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
                Layouts.Find<Button>(form, "PreviewDayZero").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(app.IsDayZeroOpen && app.GreetingWindows.Count == 2, "Day-zero preview fills both displays even when the automatic show is off");
                foreach (Window window in app.GreetingWindows)
                    Check(!window.AllowsTransparency && ((SolidColorBrush)window.Background).Color == Colors.Black,
                        "Each day-zero window has an opaque black backdrop");
                if (!SystemParameters.HighContrast)
                    Check(WaitFor(delegate { return app.DayZeroElapsed > .1; }, 3500),
                        "The starburst clock starts after both monitors present the ring");
                double elapsed = app.DayZeroElapsed;
                app.ReflowGreetings();
                Check(app.IsDayZeroOpen && app.DayZeroElapsed >= elapsed, "Display changes preserve the reveal phase");
                Window second = app.GreetingWindows[1];
                second.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(second),
                    Environment.TickCount, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
                Check(!app.IsDayZeroOpen && app.Greeting == null && app.GreetingWindows.Count == 0, "Escape on either display ends the entire starburst");
                Check(File.ReadAllText(store.FilePath) == saved, "A manual day-zero preview leaves the saved countdown and unlock markers untouched");
                CheckDayZeroLetterDust();
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
            Point cursor = new Point(201, 130);
            byte[] plain = DayZeroFrame(letters, 24.2), plainSky = DayZeroFrame(sky, 24.2);
            // Seek before sampling the same instant with the cursor in the lettering.
            DayZeroFrame(letters, 24.1);
            letters.SetPointer(cursor); sky.SetPointer(cursor);
            byte[] pulled = DayZeroFrame(letters, 24.2), pulledSky = DayZeroFrame(sky, 24.2);
            int changedInk = 0;
            for (int y = 115; y < 153; y++)
                for (int x = 170; x < 235; x++)
                {
                    int i = (y * 480 + x) * 4;
                    int lettering = plain[i] - plainSky[i];
                    if (lettering > 65 && Math.Abs((pulled[i] - pulledSky[i]) - lettering) > 12) changedInk++;
                }
            Check(changedInk > 25, "Nearby letter grains respond to the pointer independently of the surrounding stars");
            letters.SetPointer(null); sky.SetPointer(null);
            for (int i = 1; i <= 10; i++) DayZeroFrame(letters, 24.2 + i * .1);
            byte[] reset = DayZeroFrame(letters, 25.2);
            DayZeroFrame(letters, 22);
            byte[] resting = DayZeroFrame(letters, 25.2);
            Check(reset.SequenceEqual(resting),
                "Letter grains return to their original positions after the pointer leaves");
        }

        private static byte[] DayZeroFrame(DayZeroScene scene, double seconds)
        {
            scene.SetFrame(seconds, false); scene.UpdateLayout();
            var frame = new RenderTargetBitmap(480, 270, 96, 96, PixelFormats.Pbgra32);
            frame.Render(scene); return Pixels(frame);
        }
    }
}
