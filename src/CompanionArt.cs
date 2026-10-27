using System;
using System.Windows;
using System.Windows.Media;

namespace HolidayTimer
{
    // Each companion is a single-colour vector silhouette. Movement comes from
    // its pose, tail, wings or wheels rather than facial details.
    internal sealed class CompanionArt
    {
        private readonly Brush silhouette, glow;
        private readonly Pen fine, frame, limb, wheel;

        internal CompanionArt(HolidayTheme theme, bool light)
        {
            Color colour = theme == HolidayTheme.Sunset
                ? (light ? Color.FromRgb(117, 64, 156) : Color.FromRgb(230, 180, 250))
                : theme == HolidayTheme.Festive
                ? (light ? Color.FromRgb(157, 74, 49) : Color.FromRgb(255, 205, 157))
                : (light ? Color.FromRgb(35, 96, 151) : Color.FromRgb(169, 226, 255));
            SolidColorBrush ink = new SolidColorBrush(colour); ink.Freeze(); silhouette = ink;
            RadialGradientBrush halo = new RadialGradientBrush();
            halo.GradientStops.Add(new GradientStop(Color.FromArgb(65, colour.R, colour.G, colour.B), 0));
            halo.GradientStops.Add(new GradientStop(Color.FromArgb(24, colour.R, colour.G, colour.B), .58));
            halo.GradientStops.Add(new GradientStop(Color.FromArgb(0, colour.R, colour.G, colour.B), 1));
            halo.Freeze(); glow = halo;
            fine = PenFor(silhouette, 1.2); frame = PenFor(silhouette, 2.7);
            limb = PenFor(silhouette, 3.1); wheel = PenFor(silhouette, 2.7);
        }

        private static Pen PenFor(Brush brush, double width)
        { Pen pen = new Pen(brush, width) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round }; pen.Freeze(); return pen; }
        private static Geometry Shape(string data)
        { Geometry geometry = Geometry.Parse(data); geometry.Freeze(); return geometry; }

        private static readonly Geometry DogBody = Shape("M 13,34 C 12,29 18,26 26,26 L 36,27 C 42,27 45,30 44,36 L 42,41 C 37,42 33,40 28,40 L 20,42 C 15,41 12,38 13,34 Z");
        private static readonly Geometry DogHead = Shape("M 39,28 C 41,24 46,22 50,24 L 53,27 L 59,29 C 61,30 60,32 58,33 L 53,34 C 52,38 48,39 43,37 L 40,36 Z");
        private static readonly Geometry DogEar = Shape("M 42,23 C 38,22 37,28 39,34 C 40,37 43,36 44,32 Z");
        private static readonly Geometry DogTail = Shape("M 16,32 C 10,31 7,28 4,23 C 4,30 8,36 16,38 Z");
        private static readonly Geometry CatBody = Shape("M 14,37 C 13,30 19,26 28,27 C 35,27 40,29 43,34 L 43,40 C 39,42 34,41 28,40 L 20,42 C 16,41 14,39 14,37 Z");
        private static readonly Geometry CatHead = Shape("M 39,31 L 41,21 L 46,25 C 49,24 52,24 54,25 L 58,21 L 58,31 C 60,37 56,40 50,40 C 43,40 40,37 39,31 Z");
        private static readonly Geometry CatTail = Shape("M 17,35 C 10,39 5,35 6,28 C 6,25 8,23 10,23 C 8,29 9,33 17,32 Z");
        private static readonly Geometry BunnyEar1 = Shape("M 39,29 C 36,22 36,10 39,9 C 43,8 45,21 45,29 Z");
        private static readonly Geometry BunnyEar2 = Shape("M 45,28 C 45,19 48,10 51,10 C 54,11 53,23 50,30 Z");
        private static readonly Geometry BirdBody = Shape("M 11,34 C 16,28 23,27 30,28 C 36,24 43,24 47,28 L 58,31 L 48,34 C 45,40 39,43 31,43 C 22,43 15,40 11,36 Z");
        private static readonly Geometry BirdTail = Shape("M 16,32 L 5,26 L 9,34 L 4,39 L 17,39 Z");
        private static readonly Geometry BirdWing = Shape("M 23,34 C 18,25 17,16 21,11 C 28,15 35,22 38,30 C 34,34 29,37 23,34 Z");
        private static readonly Geometry CarBody = Shape("F0 M 5,39 L 9,33 C 13,31 19,30 23,29 L 29,22 C 34,19 42,20 46,22 L 52,29 L 58,32 L 60,42 L 57,45 L 8,45 L 5,43 Z M 26,29 L 31,23 L 36,22 L 36,29 Z M 39,22 L 43,23 L 49,29 L 39,29 Z");
        private static readonly Geometry BicycleFrame = Shape("M 14,45 L 25,29 L 36,45 L 14,45 M 25,29 L 42,29 L 50,45 M 36,45 L 42,29");
        private static readonly Geometry BicycleHandle = Shape("M 42,29 L 45,24 L 50,24");
        private static readonly Geometry MotorFrame = Shape("M 14,46 L 24,34 L 34,44 L 14,46 M 34,44 L 43,30 L 50,46 M 43,30 L 46,24 L 51,24");
        private static readonly Geometry MotorBody = Shape("M 19,38 L 26,29 L 38,29 L 43,34 L 37,42 L 25,42 Z");
        private static readonly Geometry PlaneBody = Shape("M 5,30 C 14,28 38,27 53,28 C 62,29 63,32 56,34 C 43,37 18,36 7,34 Z");
        private static readonly Geometry PlaneWing = Shape("M 25,30 L 14,16 L 20,15 L 38,28 Z M 27,35 L 17,48 L 23,49 L 40,35 Z");
        private static readonly Geometry PlaneTail = Shape("M 11,30 L 5,22 L 10,22 L 18,29 Z M 12,34 L 5,41 L 10,41 L 19,35 Z");

        internal void Draw(DrawingContext dc, CompanionKind kind, double phase, double reaction, double audio)
        {
            dc.DrawEllipse(glow, null, new Point(32, 33), 33 + 2 * audio, 26 + 2 * audio);
            switch (kind)
            {
                case CompanionKind.Dog: Dog(dc, phase, reaction); break;
                case CompanionKind.Cat: Cat(dc, phase, reaction); break;
                case CompanionKind.Bunny: Bunny(dc, phase, reaction); break;
                case CompanionKind.Bird: Bird(dc, phase, reaction); break;
                case CompanionKind.Car: Car(dc, phase, reaction); break;
                case CompanionKind.Bicycle: Bicycle(dc, phase, reaction); break;
                case CompanionKind.Motorcycle: Motorcycle(dc, phase, reaction); break;
                case CompanionKind.Airplane: Airplane(dc, phase, reaction); break;
            }
        }

        private void Dog(DrawingContext dc, double phase, double reaction)
        {
            dc.PushTransform(new RotateTransform(Math.Sin(phase * 5) * (8 + 14 * reaction), 16, 34));
            dc.DrawGeometry(silhouette, null, DogTail); dc.Pop();
            Legs(dc, phase, 19, 37, 39, 37);
            dc.DrawGeometry(silhouette, null, DogBody);
            dc.DrawGeometry(silhouette, null, DogHead);
            dc.DrawGeometry(silhouette, null, DogEar);
        }

        private void Cat(DrawingContext dc, double phase, double reaction)
        {
            dc.PushTransform(new RotateTransform(Math.Sin(phase * 2.5) * (6 + 10 * reaction), 17, 34));
            dc.DrawGeometry(silhouette, null, CatTail); dc.Pop();
            Legs(dc, phase, 19, 38, 39, 38);
            dc.DrawGeometry(silhouette, null, CatBody);
            dc.DrawGeometry(silhouette, null, CatHead);
        }

        private void Bunny(DrawingContext dc, double phase, double reaction)
        {
            dc.DrawEllipse(silhouette, null, new Point(15, 38), 3.5, 3.5);
            Legs(dc, phase * .8, 23, 40, 40, 40);
            dc.DrawEllipse(silhouette, null, new Point(29, 37), 14, 10);
            dc.PushTransform(new RotateTransform(Math.Sin(phase * 3) * 4 + reaction * 7, 46, 30));
            dc.DrawGeometry(silhouette, null, BunnyEar1); dc.DrawGeometry(silhouette, null, BunnyEar2); dc.Pop();
            dc.DrawEllipse(silhouette, null, new Point(47, 34), 8, 7.5);
            dc.DrawEllipse(silhouette, null, new Point(54, 37), 2.5, 2);
        }

        private void Bird(DrawingContext dc, double phase, double reaction)
        {
            dc.DrawGeometry(silhouette, null, BirdTail);
            dc.DrawGeometry(silhouette, null, BirdBody);
            dc.PushTransform(new RotateTransform(-8 + Math.Sin(phase * 5) * 23 - reaction * 26, 26, 34));
            dc.DrawGeometry(silhouette, null, BirdWing); dc.Pop();
            dc.DrawLine(fine, new Point(29, 42), new Point(27, 46));
            dc.DrawLine(fine, new Point(35, 42), new Point(33, 46));
        }

        private void Car(DrawingContext dc, double phase, double reaction)
        {
            dc.DrawGeometry(silhouette, null, CarBody);
            Wheel(dc, 17, 45, 6.2, phase * 7); Wheel(dc, 48, 45, 6.2, phase * 7);
        }

        private void Bicycle(DrawingContext dc, double phase, double reaction)
        {
            Wheel(dc, 14, 45, 9, phase * 5); Wheel(dc, 50, 45, 9, phase * 5);
            dc.DrawGeometry(null, frame, BicycleFrame);
            dc.DrawLine(frame, new Point(21, 27), new Point(29, 27));
            dc.DrawGeometry(null, frame, BicycleHandle);
            dc.DrawEllipse(silhouette, null, new Point(35, 45), 2, 2);
        }

        private void Motorcycle(DrawingContext dc, double phase, double reaction)
        {
            Wheel(dc, 14, 46, 8, phase * 7); Wheel(dc, 50, 46, 8, phase * 7);
            dc.DrawGeometry(null, frame, MotorFrame);
            dc.DrawGeometry(silhouette, null, MotorBody);
            dc.DrawLine(frame, new Point(20, 29), new Point(27, 29));
        }

        private void Airplane(DrawingContext dc, double phase, double reaction)
        {
            dc.DrawGeometry(silhouette, null, PlaneWing);
            dc.DrawGeometry(silhouette, null, PlaneBody);
            dc.DrawGeometry(silhouette, null, PlaneTail);
        }

        private void Legs(DrawingContext dc, double phase, double x1, double y1, double x2, double y2)
        {
            double step = Math.Sin(phase * 6) * 4;
            Leg(dc, x1, y1, step); Leg(dc, x1 + 3, y1, -step);
            Leg(dc, x2 - 3, y2, -step); Leg(dc, x2, y2, step);
        }

        private void Leg(DrawingContext dc, double x, double y, double stride)
        {
            Point knee = new Point(x + stride * .38, y + 6);
            Point foot = new Point(x + stride, 50);
            dc.DrawLine(limb, new Point(x, y), knee);
            dc.DrawLine(limb, knee, foot);
            dc.DrawLine(fine, foot, new Point(foot.X + 2.5, foot.Y));
        }

        private void Wheel(DrawingContext dc, double x, double y, double r, double angle)
        {
            dc.DrawEllipse(null, wheel, new Point(x, y), r, r);
            dc.PushTransform(new RotateTransform(angle * 180 / Math.PI, x, y));
            dc.DrawLine(fine, new Point(x - r * .55, y), new Point(x + r * .55, y));
            dc.DrawLine(fine, new Point(x, y - r * .55), new Point(x, y + r * .55));
            dc.Pop();
        }
    }
}
