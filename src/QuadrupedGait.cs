using System;

namespace HolidayTimer
{
    // A walking leg has a planted stance and a lifted return, with fixed
    // bone lengths. The front elbow and hind stifle bend in opposite directions.
    internal static class QuadrupedGait
    {
        internal struct Joint
        {
            internal double X, Y;
            internal Joint(double x, double y) { X = x; Y = y; }
        }
        internal struct LegPose
        {
            internal Joint Root, Knee, Ankle, Paw;
            internal double UpperLength, LowerLength;
        }

        internal static LegPose Pose(bool cat, bool hind, bool far, double cycle, double bodyLift, double settle = 0)
        {
            double phase = cycle + (hind ? (far ? .5 : 0) : (far ? .25 : .75));
            phase -= Math.Floor(phase);
            double rootX = hind ? 20.3 : (cat ? 43.5 : 43.7);
            double rootY = hind ? (cat ? 36.5 : 32.5) : (cat ? 35.0 : 30.5);
            if (far) rootX -= 1.0;
            double radius = CompanionMotion.Radius / CompanionMotion.ArtScale;
            // Ground contacts are points on the actual rim, expressed in the
            // animal's rotating frame. As the body advances a planted paw's
            // angle retreats by precisely the same amount.
            double touchdown = Math.Asin((rootX - 32 + CompanionMotion.Stride * .5) / radius);
            double angle;
            double lift = 0;
            if (phase < CompanionMotion.StanceFraction)
                angle = touchdown - CompanionMotion.Stride * phase / CompanionMotion.StanceFraction / radius;
            else
            {
                double swing = (phase - CompanionMotion.StanceFraction) / (1 - CompanionMotion.StanceFraction);
                // Endpoint tangents follow travel at take-off and landing;
                // the paw sweeps forward through the middle of the return.
                double slope = -(1 - CompanionMotion.StanceFraction) / CompanionMotion.StanceFraction;
                double sweep = (-2 * swing * swing * swing + 3 * swing * swing) +
                    slope * (2 * swing * swing * swing - 3 * swing * swing + swing);
                angle = touchdown - CompanionMotion.Stride / radius + CompanionMotion.Stride / radius * sweep;
                lift = (cat ? 2.6 : 3.3) * Math.Pow(Math.Sin(Math.PI * swing), 2);
            }
            double standingAngle = Math.Asin((rootX - 32 + (far ? -1.8 : 1.8)) / radius);
            angle += (standingAngle - angle) * settle;
            lift *= 1 - settle;
            Joint paw = new Joint(32 + radius * Math.Sin(angle),
                51 + radius * (1 - Math.Cos(angle)) - lift);
            Joint root = new Joint(rootX, rootY - bodyLift);
            Joint ankle = new Joint(paw.X - (hind ? 1.1 : .35), paw.Y - (hind ? 3.4 : 1.8));
            double upper = hind ? (cat ? 6.4 : 7.9) : (cat ? 7.4 : 9.7);
            double lower = hind ? (cat ? 6.6 : 8.4) : (cat ? 7.2 : 9.4);
            // The shoulder/hip can settle slightly within the torso at full
            // extension. This preserves bone lengths without a permanently
            // crouched stance or snapping the knee through its straight limit.
            double reach = upper + lower - .12, dx = ankle.X - root.X;
            root.Y = Math.Max(root.Y, ankle.Y - Math.Sqrt(Math.Max(.01, reach * reach - dx * dx)));
            Joint knee = Bend(root, ankle, upper, lower, hind ? 1 : -1);
            return new LegPose { Root = root, Knee = knee, Ankle = ankle, Paw = paw,
                UpperLength = upper, LowerLength = lower };
        }

        private static Joint Bend(Joint root, Joint end, double upper, double lower, int direction)
        {
            double dx = end.X - root.X, dy = end.Y - root.Y;
            double distance = Math.Sqrt(dx * dx + dy * dy);
            double reach = Math.Max(.001, Math.Min(upper + lower - .001, distance));
            double along = (upper * upper - lower * lower + reach * reach) / (2 * reach);
            double offset = Math.Sqrt(Math.Max(0, upper * upper - along * along));
            return new Joint(root.X + dx / distance * along + direction * dy / distance * offset,
                root.Y + dy / distance * along - direction * dx / distance * offset);
        }
    }
}
