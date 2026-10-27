using System;

namespace HolidayTimer
{
    internal enum CompanionAction { Travel, Rest, Play, Sleep }

    // Distance is the common clock for feet, wheels and position. A stance
    // foot travels backwards under the body at exactly the speed of travel.
    internal sealed class CompanionMotion
    {
        internal const double Radius = 125, Stride = 7, StanceFraction = .6, ArtScale = .95;
        private static readonly CompanionAction[] Sequence = { CompanionAction.Travel, CompanionAction.Rest,
            CompanionAction.Travel, CompanionAction.Play, CompanionAction.Travel, CompanionAction.Sleep };
        private static readonly double[] Durations = { 4.6, 1.4, 4.2, 2.4, 5.0, 5.0 };
        private int index;
        private double age;
        private bool interaction;
        internal double Angle = .55, Distance;
        internal int Direction = 1;
        internal CompanionAction Action { get; private set; }
        internal double Age { get { return age; } }

        internal CompanionMotion() { Action = CompanionAction.Travel; }

        internal void Interact()
        {
            interaction = true; age = 0; Action = CompanionAction.Play;
        }

        internal void Step(double seconds, CompanionKind kind, double intensity, double audio, double effectPace)
        {
            if (Double.IsNaN(seconds) || seconds <= 0) return;
            double left = Math.Min(seconds, .25);
            while (left > 0)
            {
                double duration = interaction ? 1.8 : Durations[index];
                double dt = Math.Min(left, Math.Max(0, duration - age));
                if (Action == CompanionAction.Travel && dt > 0)
                {
                    double midpoint = age + dt * .5;
                    double ease = Smooth(midpoint / .4) * Smooth((duration - midpoint) / .55);
                    double speed = effectPace * (22 + 12 * Unit(intensity)) * (1 + Math.Min(1.2, Math.Max(0, audio)));
                    Move(speed * ease * dt);
                }
                age += dt; left -= dt;
                if (age + 1e-9 >= duration)
                {
                    age = 0;
                    if (interaction) { interaction = false; index = 0; }
                    else index = (index + 1) % Sequence.Length;
                    Action = Sequence[index];
                    // Vehicles park instead of sleeping; they resume travel afterwards.
                    if (Action == CompanionAction.Sleep && kind >= CompanionKind.Car) Action = CompanionAction.Rest;
                }
                else if (dt <= 0) break;
            }
        }

        private void Move(double distance)
        {
            Distance += distance;
            double remaining = distance / Radius;
            const double bound = .84;
            while (remaining > 0)
            {
                double available = Direction > 0 ? bound - Angle : Angle + bound;
                if (available < 1e-9) { Direction = -Direction; continue; }
                double step = Math.Min(remaining, available);
                Angle += Direction * step; remaining -= step;
                if (step + 1e-9 >= available) Direction = -Direction;
            }
        }

        internal static double StepPhase(double distance) { return Math.Max(0, distance) / (Stride / StanceFraction * ArtScale); }
        internal static double FootX(double phase)
        {
            phase = phase - Math.Floor(phase);
            return phase < StanceFraction ? Stride * (.5 - phase / StanceFraction) :
                Stride * (-.5 + Smooth((phase - StanceFraction) / (1 - StanceFraction)));
        }
        internal static double FootLift(double phase)
        {
            phase = phase - Math.Floor(phase);
            return phase < StanceFraction ? 0 : 2.3 * Math.Sin(Math.PI * (phase - StanceFraction) / (1 - StanceFraction));
        }
        internal static double Smooth(double value)
        { value = Unit(value); return value * value * (3 - 2 * value); }
        private static double Unit(double value)
        { return Double.IsNaN(value) || Double.IsInfinity(value) ? 0 : Math.Max(0, Math.Min(1, value)); }

        internal void SetForTest(double angle, double distance)
        { Angle = Math.Max(-.84, Math.Min(.84, angle)); Distance = Math.Max(0, distance); Direction = 1; index = 0; age = 0; interaction = false; Action = CompanionAction.Travel; }
    }
}
