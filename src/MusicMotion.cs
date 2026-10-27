using System;

namespace HolidayTimer
{
    // Audio envelopes drive only the coloured shell. Their attack/release is
    // deliberately slower than individual samples, keeping the countdown calm.
    internal sealed class MusicMotion
    {
        internal double Level, Bass, Mid, High, Beat;
        private double targetLevel, targetBass, targetMid, targetHigh, targetBeat;
        private double intensity = .5;

        internal void Receive(double level, double bass, double mid, double high, double beat, double strength)
        {
            targetLevel = Response(level);
            targetBass = Response(bass);
            targetMid = Response(mid);
            targetHigh = Response(high);
            targetBeat = Response(beat);
            intensity = Unit(strength);
        }

        internal void Step(double seconds)
        {
            double elapsed = Math.Max(0, Math.Min(.1, Finite(seconds)));
            Level = Follow(Level, targetLevel, elapsed, .075, .45);
            Bass = Follow(Bass, targetBass, elapsed, .09, .42);
            Mid = Follow(Mid, targetMid, elapsed, .13, .5);
            High = Follow(High, targetHigh, elapsed, .18, .55);
            Beat = Follow(Beat, targetBeat, elapsed, .065, .3);
        }

        internal void Snap()
        {
            Level = targetLevel; Bass = targetBass; Mid = targetMid;
            High = targetHigh; Beat = targetBeat;
        }

        internal void Quiet()
        { targetLevel = targetBass = targetMid = targetHigh = targetBeat = 0; }

        internal void Reset()
        {
            Quiet();
            Snap();
        }

        internal bool AtRest
        { get { return Level < .001 && Bass < .001 && Mid < .001 && High < .001 && Beat < .001; } }

        internal bool HasTarget
        { get { return targetLevel >= .001 || targetBass >= .001 || targetMid >= .001 || targetHigh >= .001 || targetBeat >= .001; } }

        internal double Gain { get { return .2 + .8 * intensity; } }
        internal double LevelDrive { get { return Gain * Level; } }
        // Speech and quieter music may carry little bass. Broad-band energy
        // supplies a modest floor without inventing transients or idle beats.
        internal double BassDrive { get { return Gain * Math.Max(Bass, Level * .7); } }
        internal double FlowDrive { get { return Gain * Math.Max(Mid, Level * .5); } }
        internal double HighDrive { get { return Gain * High; } }
        internal double BeatDrive { get { return Gain * Beat; } }
        internal double HaloScale { get { return 1 + BassDrive * .05 + BeatDrive * .025; } }
        internal double RimScale { get { return 1 + BassDrive * .075 + LevelDrive * .02 + BeatDrive * .025; } }
        internal double RibbonScaleX { get { return 1 + FlowDrive * .12 + BassDrive * .06; } }
        internal double RibbonScaleY { get { return 1 + FlowDrive * .17 + BassDrive * .06; } }
        internal double BackRibbonAngle { get { return -(FlowDrive * 18 + BeatDrive * 6); } }
        internal double FrontRibbonAngle { get { return FlowDrive * 30 + BeatDrive * 8; } }
        internal double SheenAngle { get { return HighDrive * 22 + LevelDrive * 8; } }
        internal double DetailAngle { get { return -(HighDrive * 10 + FlowDrive * 6); } }

        // Compress audible energy rather than amplifying samples or inventing beats.
        // Quiet real output becomes visible, while silence stays exactly zero.
        internal static double Response(double value)
        {
            double bounded = Unit(value);
            return bounded == 0 ? 0 : 1.12 * bounded / (bounded + .12);
        }

        private static double Follow(double current, double target, double elapsed, double attack, double release)
        {
            double value = current + (target - current) * (1 - Math.Exp(-elapsed / (target > current ? attack : release)));
            return target == 0 && value < .0001 ? 0 : value;
        }

        private static double Unit(double value)
        { return Math.Max(0, Math.Min(1, Finite(value))); }

        private static double Finite(double value)
        { return Double.IsNaN(value) || Double.IsInfinity(value) ? 0 : value; }
    }
}
