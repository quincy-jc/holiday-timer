using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace HolidayTimer
{
    internal static class ColourPickerPreview
    {
        [STAThread]
        private static int Main(string[] args)
        {
            string directory = Path.GetFullPath(args[0]); Directory.CreateDirectory(directory);
            Application app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            Window owner = new Window { Width = 700, Height = 700, ShowInTaskbar = false, WindowStyle = WindowStyle.None, Left = -3000, Top = -3000 };
            FontChoices.Apply(owner, "Inter Display"); owner.Show();
            try
            {
                foreach (bool light in new bool[] { false, true })
                    foreach (HolidayTheme theme in Enum.GetValues(typeof(HolidayTheme)))
                    {
                        Themes.Apply(owner, theme, .82, light);
                        ColourPicker picker = new ColourPicker(owner, "#80BCFF", delegate { }); picker.Show(); picker.UpdateLayout();
                        Save(picker, Path.Combine(directory, "picker-" + (light ? "light-" : "dark-") + theme + ".png"));
                        if (light && theme == HolidayTheme.Sunset)
                        {
                            picker.Width = 320; picker.Height = 470;
                            Layouts.Find<TextBox>(picker, "ColourHex").Text = "#12G";
                            Layouts.Find<TextBox>(picker, "ColourHex").BringIntoView(); picker.UpdateLayout();
                            Save(picker, Path.Combine(directory, "picker-compact-error.png"));
                        }
                        Console.WriteLine((light ? "Light " : "Dark ") + theme + ": " + picker.SelectedHex + "; content extent=" + Layouts.Find<ScrollViewer>(picker, "ColourScroller").ExtentHeight + "; viewport=" + Layouts.Find<ScrollViewer>(picker, "ColourScroller").ViewportHeight);
                        picker.Close();
                    }
                return 0;
            }
            catch (Exception e) { Console.Error.WriteLine(e); return 1; }
            finally { owner.Close(); app.Shutdown(); }
        }
        private static void Save(Window window, string path)
        {
            window.UpdateLayout();
            RenderTargetBitmap image = new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth), (int)Math.Ceiling(window.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            image.Render((Visual)window.Content); PngBitmapEncoder encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
            using (FileStream stream = File.Create(path)) encoder.Save(stream);
        }
    }
}
