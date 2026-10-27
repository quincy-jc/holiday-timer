using System;
using System.Windows;
using System.Windows.Media;

namespace HolidayTimer
{
    // Original, single-colour silhouettes. Animals use distinct poses and
    // vehicles use rotating wheels, rather than moving a static icon.
    internal sealed class CompanionArt
    {
        private readonly Brush silhouette, glow;
        internal Brush Ink { get { return silhouette; } }

        internal CompanionArt(HolidayTheme theme, bool light)
        {
            Color colour = theme == HolidayTheme.Sunset
                ? (light ? Color.FromRgb(117, 64, 156) : Color.FromRgb(230, 180, 250))
                : theme == HolidayTheme.Festive
                ? (light ? Color.FromRgb(157, 74, 49) : Color.FromRgb(255, 205, 157))
                : (light ? Color.FromRgb(35, 96, 151) : Color.FromRgb(169, 226, 255));
            SolidColorBrush ink = new SolidColorBrush(colour); ink.Freeze(); silhouette = ink;
            RadialGradientBrush halo = new RadialGradientBrush();
            halo.GradientStops.Add(new GradientStop(Color.FromArgb(56, colour.R, colour.G, colour.B), 0));
            halo.GradientStops.Add(new GradientStop(Color.FromArgb(19, colour.R, colour.G, colour.B), .58));
            halo.GradientStops.Add(new GradientStop(Color.FromArgb(0, colour.R, colour.G, colour.B), 1));
            halo.Freeze(); glow = halo;
        }

        internal void Draw(DrawingContext dc, CompanionKind kind, double phase, double reaction, double audio,
            CompanionAction action, double distance, double age)
        {
            dc.DrawEllipse(glow, null, new Point(32, 33), 33 + 2 * audio, 26 + 2 * audio);
            switch (kind)
            {
                case CompanionKind.Dog:
                case CompanionKind.Cat:
                case CompanionKind.Bunny:
                case CompanionKind.Bird: CompanionSprites.Draw(dc, kind, phase, reaction, silhouette, action, distance, age); break;
                case CompanionKind.Car:
                case CompanionKind.Bicycle:
                case CompanionKind.Motorcycle:
                case CompanionKind.Airplane: VehicleSprites.Draw(dc, kind, distance, silhouette); break;
            }
        }

    }
}
