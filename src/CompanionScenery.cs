using System;
using System.Windows;
using System.Windows.Media;

namespace HolidayTimer
{
    // Small background details sit on the upper rim, outside the countdown's
    // central text. The companion itself always remains above this artwork.
    internal static class CompanionScenery
    {
        private static readonly Geometry Tree = Shape("M -1,0 L -1,-7 C -5,-7 -8,-10 -8,-14 C -8,-18 -5,-21 -1,-21 C 1,-24 6,-21 7,-18 C 11,-15 9,-9 4,-8 L 2,-7 L 2,0 Z");
        private static readonly Geometry Rock = Shape("M -7,0 C -7,-3 -5,-6 -2,-6 L 1,-9 L 6,-7 L 8,-3 L 8,0 Z");
        private static readonly Geometry Buildings = Shape("M -10,0 L -10,-10 L -6,-10 L -6,-17 L -2,-17 L -2,-12 L 1,-12 L 1,-21 L 5,-21 L 5,-13 L 9,-13 L 9,0 Z");
        private static Geometry Shape(string data) { Geometry g = Geometry.Parse(data); g.Freeze(); return g; }

        internal static void Draw(DrawingContext dc, CompanionKind kind, Brush colour, double time,
            CompanionAction action, OrbEffect effect)
        {
            dc.PushOpacity(.24);
            if (kind == CompanionKind.Bird || kind == CompanionKind.Airplane)
            {
                Cloud(dc, colour, 91, 38, 1); Cloud(dc, colour, 244, 44, .8);
            }
            else
            {
                OnRim(dc, colour, -.69, Tree);
                OnRim(dc, colour, .72, kind >= CompanionKind.Car ? Buildings : Rock);
            }
            if (effect == OrbEffect.WaterRipples)
            {
                Pen rain = new Pen(colour, 1.1) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
                for (int i = 0; i < 4; i++)
                {
                    double x = i % 2 == 0 ? 72 + i * 5 : 254 + i * 4;
                    double y = 45 + (time * 13 + i * 7) % 20;
                    dc.DrawLine(rain, new Point(x, y), new Point(x - 1.5, y + 4));
                }
            }
            dc.Pop();
        }

        private static void OnRim(DrawingContext dc, Brush colour, double angle, Geometry shape)
        {
            double a = -Math.PI / 2 + angle;
            dc.PushTransform(new TranslateTransform(172 + 125 * Math.Cos(a), 172 + 125 * Math.Sin(a)));
            dc.PushTransform(new RotateTransform(a * 180 / Math.PI + 90));
            dc.DrawGeometry(colour, null, shape); dc.Pop(); dc.Pop();
        }

        private static void Cloud(DrawingContext dc, Brush colour, double x, double y, double scale)
        {
            dc.PushTransform(new TranslateTransform(x, y)); dc.PushTransform(new ScaleTransform(scale, scale));
            dc.DrawEllipse(colour, null, new Point(0, 0), 10, 4);
            dc.DrawEllipse(colour, null, new Point(-4, -2), 4, 4);
            dc.DrawEllipse(colour, null, new Point(2, -3), 5, 5);
            dc.Pop(); dc.Pop();
        }

        internal static void DrawPlay(DrawingContext dc, CompanionKind kind, Brush colour, double age)
        {
            dc.PushOpacity(.8);
            if (kind == CompanionKind.Dog || kind == CompanionKind.Cat || kind == CompanionKind.Bunny)
            {
                double x = 57 - 3 * Math.Sin(age * 2.5);
                double y = 48 - 3 * Math.Abs(Math.Sin(age * 4));
                dc.DrawEllipse(null, new Pen(colour, 1.6), new Point(x, y), 3.5, 3.5);
            }
            else if (kind == CompanionKind.Car || kind == CompanionKind.Motorcycle)
            {
                for (int i = 0; i < 3; i++)
                {
                    double t = (age * 12 + i * 4) % 12;
                    dc.DrawEllipse(colour, null, new Point(8 - t, 43 - t * .4), 1 + t * .13, 1 + t * .13);
                }
            }
            dc.Pop();
        }
    }
}
