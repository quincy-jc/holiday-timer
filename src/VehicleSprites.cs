using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace HolidayTimer
{
    // The vehicle and the separately rotated wheels share one original alpha
    // mask. Only the circular wheel areas rotate; the body stays stable.
    internal static class VehicleSprites
    {
        private sealed class Vehicle
        {
            internal ImageBrush Mask;
            internal Rect Destination;
            internal Geometry BodyClip;
            internal EllipseGeometry[] WheelClips;
            internal Point[] WheelCentres;
        }

        private static readonly BitmapFrame VehicleAtlas = Open("vehicle-silhouettes.png");
        private static readonly BitmapFrame PlaneAtlas = Open("airplane-silhouette.png");
        private static readonly Vehicle Car = Create(VehicleAtlas, new Int32Rect(20, 149, 786, 257), 412.5, 401, .069, 51,
            new double[,] { { 179, 338, 65 }, { 660, 338, 65 } });
        private static readonly Vehicle Bicycle = Create(VehicleAtlas, new Int32Rect(890, 90, 604, 353), 1191, 438, .081, 51,
            new double[,] { { 1012, 321, 118 }, { 1370, 321, 118 } });
        private static readonly Vehicle Motorcycle = Create(VehicleAtlas, new Int32Rect(60, 600, 667, 347), 393, 942, .078, 51,
            new double[,] { { 167, 842, 101 }, { 622, 843, 100 } });
        private static readonly Vehicle Airplane = Create(PlaneAtlas, new Int32Rect(38, 126, 1895, 477), 985, 365, .029, 34, null);

        private static BitmapFrame Open(string name)
        {
            using (Stream stream = typeof(VehicleSprites).Assembly.GetManifestResourceStream("HolidayTimer.Companions." + name))
            {
                if (stream == null) throw new InvalidOperationException("A companion silhouette resource is missing: " + name);
                BitmapFrame atlas = BitmapFrame.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                atlas.Freeze(); return atlas;
            }
        }

        private static Vehicle Create(BitmapFrame atlas, Int32Rect cropRect, double anchorX, double anchorY,
            double scale, double baseline, double[,] wheels)
        {
            CroppedBitmap crop = new CroppedBitmap(atlas, cropRect); crop.Freeze();
            ImageBrush mask = new ImageBrush(crop) { Stretch = Stretch.Fill }; mask.Freeze();
            Vehicle vehicle = new Vehicle { Mask = mask, Destination = new Rect(32 + (cropRect.X - anchorX) * scale,
                baseline + (cropRect.Y - anchorY) * scale, cropRect.Width * scale, cropRect.Height * scale) };
            int count = wheels == null ? 0 : wheels.GetLength(0);
            vehicle.WheelClips = new EllipseGeometry[count]; vehicle.WheelCentres = new Point[count];
            GeometryGroup body = new GeometryGroup { FillRule = FillRule.EvenOdd };
            body.Children.Add(new RectangleGeometry(new Rect(-10, -10, 84, 84)));
            for (int i = 0; i < count; i++)
            {
                Point centre = new Point(32 + (wheels[i, 0] - anchorX) * scale, baseline + (wheels[i, 1] - anchorY) * scale);
                double radius = wheels[i, 2] * scale;
                EllipseGeometry wheel = new EllipseGeometry(centre, radius, radius); wheel.Freeze();
                vehicle.WheelCentres[i] = centre; vehicle.WheelClips[i] = wheel; body.Children.Add(wheel);
            }
            body.Freeze(); vehicle.BodyClip = body; return vehicle;
        }

        internal static void Draw(DrawingContext dc, CompanionKind kind, double distance, Brush colour)
        {
            Vehicle vehicle = kind == CompanionKind.Car ? Car : kind == CompanionKind.Bicycle ? Bicycle :
                kind == CompanionKind.Motorcycle ? Motorcycle : Airplane;
            dc.PushClip(vehicle.BodyClip); Paint(dc, vehicle, colour); dc.Pop();
            for (int i = 0; i < vehicle.WheelClips.Length; i++)
            {
                dc.PushClip(vehicle.WheelClips[i]);
                dc.PushTransform(new RotateTransform(distance / (vehicle.WheelClips[i].RadiusX * CompanionMotion.ArtScale) * 180 / Math.PI,
                    vehicle.WheelCentres[i].X, vehicle.WheelCentres[i].Y));
                Paint(dc, vehicle, colour); dc.Pop(); dc.Pop();
            }
        }

        private static void Paint(DrawingContext dc, Vehicle vehicle, Brush colour)
        {
            dc.PushOpacityMask(vehicle.Mask); dc.DrawRectangle(colour, null, vehicle.Destination); dc.Pop();
        }
    }
}
