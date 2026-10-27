using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

internal static class CompanionAssetTests
{
    private static int checks;
    private static void Check(bool value, string message)
    { if (!value) throw new Exception(message); checks++; }

    [STAThread]
    private static void Main(string[] args)
    {
        Assembly app = Assembly.LoadFrom(Path.GetFullPath(args[0]));
        Type animals = app.GetType("HolidayTimer.CompanionSprites", true);
        RuntimeHelpers.RunClassConstructor(animals.TypeHandle);
        foreach (string name in new[] { "CatWalk", "DogWalk", "CatGroom", "DogBark", "BunnyHop", "BirdFly", "Resting" })
        {
            Array frames = (Array)animals.GetField(name, BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            Check(frames.Length == 4, name + " has four complete poses");
            for (int i = 0; i < frames.Length; i++) InspectFrame(frames.GetValue(i), name + " " + i);
        }
        Type vehicles = app.GetType("HolidayTimer.VehicleSprites", true);
        RuntimeHelpers.RunClassConstructor(vehicles.TypeHandle);
        foreach (string name in new[] { "Car", "Bicycle", "Motorcycle", "Airplane" })
        {
            object vehicle = vehicles.GetField(name, BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            InspectFrame(vehicle, name);
            Geometry body = (Geometry)Get(vehicle, "BodyClip");
            Point[] centres = (Point[])Get(vehicle, "WheelCentres");
            EllipseGeometry[] clips = (EllipseGeometry[])Get(vehicle, "WheelClips");
            Check(centres.Length == (name == "Airplane" ? 0 : 2) && clips.Length == centres.Length, name + " has the right wheels");
            for (int i = 0; i < centres.Length; i++)
                Check(clips[i].FillContains(centres[i]) && !body.FillContains(centres[i]), name + " isolates wheel " + i + " from the body");
        }
        Console.WriteLine("PASS: " + checks + " companion asset checks.");
    }

    private static object Get(object source, string name)
    { return source.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(source); }

    private static void InspectFrame(object frame, string name)
    {
        ImageBrush mask = (ImageBrush)Get(frame, "Mask");
        Rect destination = (Rect)Get(frame, "Destination");
        BitmapSource image = (BitmapSource)mask.ImageSource;
        Check(mask.IsFrozen && image.IsFrozen, name + " is safely cached");
        Check(destination.Left >= 0 && destination.Top >= 0 && destination.Right <= 64 && destination.Bottom <= 64,
            name + " fits inside the companion canvas");
        FormatConvertedBitmap converted = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
        int stride = converted.PixelWidth * 4;
        byte[] pixels = new byte[stride * converted.PixelHeight]; converted.CopyPixels(pixels, stride, 0);
        int solid = 0, border = 0;
        for (int y = 0; y < converted.PixelHeight; y++)
            for (int x = 0; x < converted.PixelWidth; x++)
                if (pixels[y * stride + x * 4 + 3] >= 128)
                {
                    solid++;
                    if (x == 0 || y == 0 || x == converted.PixelWidth - 1 || y == converted.PixelHeight - 1) border++;
                }
        Check(solid > 100, name + " contains visible silhouette pixels");
        Check(border <= 4, name + " does not cut off the silhouette at a crop edge");
    }
}
