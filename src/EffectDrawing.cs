using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace HolidayTimer
{
    // The gallery's twelve authored painters, expressed as native vector drawing.
    // Paths contain at most 121 points; no pixel shaders, bitmaps or audio samples
    // are retained here. Frozen brushes and pens have bounded per-scene caches.
    internal sealed class EffectDrawing
    {
        private const double Tau = Math.PI * 2;
        private readonly bool light;
        private readonly Color[] palette;
        private readonly Dictionary<uint, Brush> solids = new Dictionary<uint, Brush>();
        private readonly Dictionary<uint, Brush> blooms = new Dictionary<uint, Brush>();
        private readonly Dictionary<long, Pen> pens = new Dictionary<long, Pen>();
        private DrawingContext drawing;
        private double energy, baseIntensity, level, low, mid, high, beat, transientBeat, lightGain = 1;
        private bool musicEnabled;
        private bool shockMusicMode;
        private double previousShockBeat, lastShockTime = Double.NegativeInfinity;
        private readonly List<double> shockStarts = new List<double>();
        internal int CapturedShockwaveCount { get { return shockStarts.Count; } }
        internal long CapturedShockwaveEmissions { get; private set; }

        internal EffectDrawing(HolidayTheme theme, bool light)
        {
            this.light = light;
            if (theme == HolidayTheme.Sunset)
                palette = new[] { Rgb(189, 163, 255), Rgb(240, 142, 203), Rgb(139, 110, 243) };
            else if (theme == HolidayTheme.Festive)
                palette = new[] { Rgb(255, 190, 123), Rgb(255, 133, 143), Rgb(232, 153, 99) };
            else palette = new[] { Rgb(128, 188, 255), Rgb(92, 225, 230), Rgb(113, 105, 244) };
        }

        internal static double Unit(double value)
        { return Double.IsNaN(value) || Double.IsInfinity(value) ? 0 : Math.Max(0, Math.Min(1, value)); }

        internal void Paint(DrawingContext context, OrbEffect effect, double time, double intensity,
            double musicIntensity, MusicMotion music, bool enabled)
        {
            drawing = context;
            baseIntensity = Unit(intensity);
            energy = .25 + .75 * baseIntensity;
            musicEnabled = enabled;
            level = enabled ? music.LevelDrive : 0;
            low = enabled ? music.BassDrive : 0;
            mid = enabled ? music.FlowDrive : 0;
            high = enabled ? music.HighDrive : 0;
            beat = enabled ? music.BeatDrive : 0;
            transientBeat = enabled ? music.Beat : 0;
            // A visible response belongs to the shell even in styles whose
            // geometry is intentionally quiet. Intensity changes the strength,
            // while real level/high/transient envelopes supply the variation.
            lightGain = 1 + level * 1.25 + high * .35 + beat * .4;
            energy = Math.Min(1.25, energy + (low + mid) * .16 + beat * .08);
            drawing.PushTransform(new TranslateTransform(172, 172));
            RingBase(time);
            switch (effect)
            {
                case OrbEffect.MusicPulse: Pulse(time); break;
                case OrbEffect.LiquidRibbons: Ribbons(time); break;
                case OrbEffect.OrbitalParticles: Particles(time); break;
                case OrbEffect.WaterRipples: Ripples(time); break;
                case OrbEffect.SolarCorona: Corona(time); break;
                case OrbEffect.NorthernLights: Aurora(time); break;
                case OrbEffect.PlasmaVortex: Vortex(time); break;
                case OrbEffect.MagneticWaves: Magnetic(time); break;
                case OrbEffect.LiquidMetal: Metal(time); break;
                case OrbEffect.WarpTunnel: Tunnel(time); break;
                case OrbEffect.ShockwavePulses: Shockwave(time); break;
                case OrbEffect.CometSwarm: Comet(time); break;
                default: Pulse(time); break;
            }
            drawing.Pop();
            drawing = null;
        }

        private void RingBase(double t)
        {
            for (int i = 0; i < 3; i++)
            {
                double a = t * .08 + i * Tau / 3;
                Bloom(Polar(a, 113 + low * 7), 57, palette[i], .22 + level * .18 + low * .08);
            }
            Circle(127, palette[0], 1.2, .2);
            Circle(123, palette[1], .6, .12);
        }

        private void Pulse(double t)
        {
            // Silence is a slow breath. Only captured bass/transients produce a pulse.
            double breath = musicEnabled ? 0 : (.5 + .5 * Math.Sin(t * .46)) * .08;
            double bass = low + beat * .35 + breath;
            double push = bass * (5 + energy * 16);
            for (int k = 0; k < 3; k++)
            {
                int band = k;
                Point[] points = Curve(96, delegate(double u)
                {
                    double a = u * Tau;
                    double r = Math.Min(159, 121 + push + Math.Sin(a * 3 + t * .43 + band) * (3 + mid * 5) +
                        Math.Sin(a * 7 - t * .6 + band) * energy * 2.6);
                    return Polar(a, r);
                });
                Line(points, palette[k], (k == 0 ? 2.8 : 1.1) + bass * 1.2, .35 + bass * .28, true);
            }
            for (int i = 0; i < 36; i++)
            {
                double a = i / 36.0 * Tau;
                double h = 3 + energy * (bass * 16 + high * 13) * (.45 + .55 * Math.Sin(i * 2.4 + t));
                Segment(Polar(a, 137), Polar(a, Math.Min(166, 137 + h)), palette[i % 3], 1.5, .18 + high * .35 + level * .18);
            }
            for (int k = 0; k < 3; k++) Bloom(Polar(t * .17 + k * Tau / 3, 119), 42 + push, palette[k], .23 + bass * .25);
        }

        private void Ribbons(double t)
        {
            double speed = .18 + baseIntensity * .18;
            for (int k = 0; k < 3; k++)
            {
                int band = k;
                double turn = t * speed + k * 1.06 + mid * .45 + beat * .12;
                Point[] points = Curve(96, delegate(double u)
                {
                    double a = u * Tau;
                    double x = Math.Cos(a) * (126 + Math.Sin(a * 3 + t * .26 + band) * (7 + low * 14));
                    double y = Math.Sin(a) * (83 + Math.Sin(a * 2 - t * .2 + band) * (14 + mid * 24));
                    return Rotate(x, y, turn);
                });
                Line(points, palette[k], 12 + level * 8, .11, true);
                Line(points, palette[k], 4.8 + mid * 5 + beat * 2.4, .3, true);
                Line(points, palette[k], 1.2, .55 + high * .18, true);
            }
        }

        private Point ParticlePoint(double angle, int orbit)
        { return Rotate(Math.Cos(angle) * (135 + low * 13), Math.Sin(angle) * (90 + orbit * 8 + mid * 17), orbit * 1.01 + .3 + mid * .16); }

        private void Particles(double t)
        {
            double speed = .22 + baseIntensity * .25;
            for (int k = 0; k < 3; k++)
            {
                int orbit = k;
                Line(Curve(64, delegate(double u) { return ParticlePoint(u * Tau, orbit); }), palette[k], .7, .2, true);
                int direction = k % 2 == 1 ? -1 : 1;
                for (int i = 0; i < 19; i++)
                {
                    double a = i / 19.0 * Tau + t * speed * direction;
                    Point head = ParticlePoint(a, k);
                    double size = 1.15 + (Math.Sin(i * 5.1) * .5 + .5) * 1.5 + high * 2.5 + level * 1.6;
                    for (int trail = 0; trail < 5; trail++)
                        Dot(ParticlePoint(a - trail * .023 * direction, k), size * (1 - trail * .12),
                            palette[k], (.42 - trail * .075) * (.6 + .4 * Math.Sin(a) * Math.Sin(a)));
                    if (i % 7 == 0) Bloom(head, 13 + high * 8 + level * 6, palette[k], .33 + beat * .18);
                }
            }
        }

        private void Ripples(double t)
        {
            double speed = .15 + baseIntensity * .13;
            for (int k = 0; k < 5; k++)
            {
                int band = k;
                double phase = Wrap(t * speed + k / 5.0), r = Math.Min(157, 82 + phase * 68 + low * 13 + beat * 5);
                double fade = Math.Sin(phase * Math.PI);
                Point[] points = Curve(96, delegate(double u)
                {
                    double a = u * Tau, w = Math.Sin(a * 5 - t * .3 + band) * (1.7 + mid * 6 + beat * 2) * fade;
                    return Polar(a, r + w);
                });
                Line(points, palette[k % 3], 5.5, fade * .08, true);
                Line(points, palette[k % 3], 1.25 + beat * 1.8 + level * .7, fade * (.44 + beat * .18), true);
            }
        }

        private void Corona(double t)
        {
            Circle(124 + low * 9 + beat * 3, palette[0], 7 + level * 4, .14);
            Circle(125 + low * 9 + beat * 3, palette[1], 2 + beat, .47);
            for (int i = 0; i < 82; i++)
            {
                double a = i / 82.0 * Tau + t * .025;
                double variation = .5 + .5 * Math.Sin(i * 2.23 + t * (.4 + energy * .4));
                double r = 125 + Math.Sin(i * 1.71 + t * .42) * 2 + low * 7;
                double length = 3 + variation * (8 + energy * 13) + high * 15 + level * 9 + beat * 5;
                Segment(Polar(a, r), Polar(a + .007, Math.Min(167, r + length)), palette[i % 3], .9 + level * .7, .12 + variation * .29);
            }
            for (int k = 0; k < 5; k++)
            {
                double a = k * 1.31 + Math.Sin(t * .16 + k) * .14;
                double flare = Math.Pow(.5 + .5 * Math.Sin(t * .5 + k * 1.9), 4), r = 125 + flare * 7 * energy;
                Bloom(Polar(a, r + low * 7), 21 + flare * 14 + beat * 9, palette[k % 3], .13 + flare * .3 + level * .15);
            }
        }

        private void Aurora(double t)
        {
            double speed = .14 + baseIntensity * .16;
            for (int band = 0; band < 3; band++)
            {
                int index = band;
                Point[] points = Curve(96, delegate(double u)
                {
                    double a = u * Tau, r = 113 + index * 9 + Math.Sin(a * 3 + t * speed + index) * (8 + mid * 12) +
                        Math.Sin(a * 7 - t * .13) * 3 + low * 6;
                    return Polar(a, Math.Min(158, r));
                });
                Line(points, palette[band], 17 + level * 5, .09, true);
                Line(points, palette[band], 6 + mid * 7 + beat * 2, .22, true);
                Line(points, palette[band], 1.2, .36 + high * .2, true);
            }
            for (int i = 0; i < 48; i++)
            {
                double a = i / 48.0 * Tau + t * .035, length = 5 + (.5 + .5 * Math.Sin(i * .43 - t * speed)) * (18 + mid * 15);
                Segment(Polar(a, 113 + low * 4), Polar(a + .02, 113 + low * 4 + length), palette[i % 3], 2, .1 + level * .12);
            }
        }

        private Point VortexPoint(double t, int arm, double u, int braid)
        {
            double radius = 91 + u * 63 + Math.Sin(u * 17 - t * 1.6 + arm) * energy * 3 + low * 11 * Math.Sin(u * Math.PI);
            double angle = arm * Tau / 5 + t * (.37 + energy * .19) + u * Math.PI * 1.34 +
                Math.Sin(u * 14 - t * 1.13 + arm * .71) * (.06 + energy * .075) +
                braid * Math.Sin(u * 21 - t * 2.15 + arm) * (.012 + energy * .025) + mid * .22 + beat * .1;
            return Polar(angle, radius);
        }

        private void Vortex(double t)
        {
            for (int arm = 0; arm < 5; arm++)
            {
                int index = arm;
                Color color = palette[arm % 3];
                Point[] main = Curve(60, delegate(double u) { return VortexPoint(t, index, u, 0); });
                Point[] braid = Curve(60, delegate(double u) { return VortexPoint(t, index, u, 1); });
                Line(main, color, 10 + energy * 4 + level * 7, .07);
                Line(main, color, 3.4 + energy * 2.2 + beat * 2, .22);
                Line(main, Mix(color, Rgb(235, 244, 255), .23), 1.05 + energy * .45, .51);
                Line(braid, palette[(arm + 1) % 3], .75, .35);
                for (int packet = 0; packet < 3; packet++)
                {
                    double phase = Wrap(t * (.18 + energy * .09) + packet / 3.0 + arm * .137);
                    double u = 1 - phase, fade = Math.Sin(phase * Math.PI);
                    for (int trail = 0; trail < 4; trail++)
                        Dot(VortexPoint(t, arm, Math.Min(1, u + trail * .018), 0),
                            1.1 + energy * .75 - trail * .14, color, fade * (.7 - trail * .13));
                    if (packet == 0) Bloom(VortexPoint(t, arm, u, 0), 17 + energy * 6, color, fade * (.23 + energy * .13));
                }
            }
            Circle(126, palette[1], .65, .18);
        }

        private Point FieldPoint(double t, int band, int sign, double u, double turn)
        {
            double arch = Math.Sin(u * Math.PI);
            double x = -119 + 238 * u + arch * Math.Sin(t * .39 + band * .8) * energy * 8;
            double y = sign * Math.Min(159, 102 + band * 17 + Math.Sin(t * .58 + band) * energy * 5 + low * 15 + beat * 6) * arch;
            return Rotate(x, y, turn);
        }

        private void Magnetic(double t)
        {
            double turn = Math.Sin(t * .16) * .38 + t * .075 + mid * .3;
            for (int band = 0; band < 3; band++)
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    int index = band, side = sign;
                    Color color = palette[(band + (sign > 0 ? 1 : 0)) % 3];
                    Point[] path = Curve(60, delegate(double u) { return FieldPoint(t, index, side, u, turn); });
                    Line(path, color, 7 + energy * 4 + level * 4, .075);
                    Line(path, color, 2 + energy * .6 + beat * 2, .28);
                    Line(path, Mix(color, Rgb(224, 244, 255), .18), .7, .62);
                    for (int charge = 0; charge < 3; charge++)
                    {
                        double phase = Wrap(t * (.19 + energy * .12) + charge / 3.0 + band * .11);
                        double u = sign > 0 ? phase : 1 - phase, fade = Math.Sin(phase * Math.PI);
                        for (int trail = 0; trail < 4; trail++)
                            Dot(FieldPoint(t, band, sign, Clamp(u - sign * trail * .017, 0, 1), turn),
                                1.25 + energy * .9 - trail * .17, color, fade * (.78 - trail * .16));
                    }
                }
            for (int sign = -1; sign <= 1; sign += 2)
            {
                Point pole = Rotate(sign * 119, 0, turn);
                Color color = palette[sign > 0 ? 0 : 2];
                double swell = .65 + .35 * Math.Sin(t * .91 + sign * .7);
                Bloom(pole, 23 + energy * swell * 8, color, .32 + energy * .12);
                Dot(pole, 3 + energy * swell, Mix(color, Rgb(247, 250, 255), .3), .85);
                drawing.DrawEllipse(null, Pen(color, .36, .75), pole, 7 + swell * energy * 3, 7 + swell * energy * 3);
            }
        }

        private Point MetalPoint(double t, double angle, int band)
        {
            double ripple = Math.Sin(angle * 3 + t * .64) * (4 + energy * 6 + mid * 8 + beat * 3) +
                Math.Sin(angle * 5 - t * .43 + 1.1) * (2 + energy * 3 + low * 4) + Math.Cos(angle * 2 - t * .29) * energy * 4;
            double radius = Math.Min(158, 124 + ripple + band * (2.5 + energy * .8) + low * 8);
            double flow = angle + Math.Sin(angle * 4 - t * .53) * energy * .018;
            return Polar(flow, radius);
        }

        private void Metal(double t)
        {
            Color neutral = light ? Rgb(88, 113, 140) : Rgb(205, 220, 238);
            Point[] shell = Curve(120, delegate(double u) { return MetalPoint(t, u * Tau, 0); });
            double shine = t * .31 + Math.Sin(t * .17) * .48 + high * .55 + level * .25;
            Point direction = Polar(shine, 149);
            LinearGradientBrush gradient = new LinearGradientBrush
            { MappingMode = BrushMappingMode.Absolute, StartPoint = new Point(-direction.X, -direction.Y), EndPoint = direction };
            gradient.GradientStops.Add(Stop(palette[2], .2, 0));
            gradient.GradientStops.Add(Stop(Mix(palette[0], neutral, .58), .61, .23));
            gradient.GradientStops.Add(Stop(palette[1], .22, .36));
            gradient.GradientStops.Add(Stop(neutral, .74, .52));
            gradient.GradientStops.Add(Stop(palette[2], .27, .61));
            gradient.GradientStops.Add(Stop(Mix(palette[1], neutral, .4), .61, .82));
            gradient.GradientStops.Add(Stop(palette[0], .24, 1));
            gradient.Freeze();
            Line(shell, palette[2], 24 + energy * 6, .08, true);
            Pen chrome = new Pen(gradient, 15 + energy * 3) { LineJoin = PenLineJoin.Round };
            chrome.Freeze();
            drawing.DrawGeometry(null, chrome, Geometry(shell, true));
            for (int band = -2; band <= 2; band++)
            {
                int index = band;
                Point[] contour = Curve(120, delegate(double u) { return MetalPoint(t, u * Tau, index); });
                Line(contour, Mix(palette[(band + 3) % 3], neutral, .32), .6 + (band == 2 ? .3 : 0), band == 2 ? .58 : .24, true);
            }
            for (int glint = 0; glint < 3; glint++)
            {
                double angle = shine + glint * Tau / 3;
                Bloom(MetalPoint(t, angle, glint == 1 ? -1 : 1), 22 + energy * 8, palette[glint], .25);
                Point[] highlight = Curve(20, delegate(double u) { return MetalPoint(t, angle - .16 + u * .32, 1); });
                Line(highlight, Mix(palette[glint], neutral, .65), 1.3, .67);
            }
        }

        private Point TunnelPoint(double t, double depth, double angle)
        {
            double radius = Math.Min(164, 70 + depth * depth * 90 + low * depth * 9), turn = t * .12 + depth * (1.15 + mid * .55);
            Point rotated = Rotate(Math.Cos(angle) * radius, Math.Sin(angle) * radius * (.65 + depth * .34), turn);
            return new Point(rotated.X + Math.Sin(t * .27) * (1 - depth) * 9, rotated.Y + Math.Cos(t * .21) * (1 - depth) * 6);
        }

        private void Tunnel(double t)
        {
            double speed = .19 + energy * .23;
            for (int ring = 0; ring < 9; ring++)
            {
                double depth = Wrap(t * speed + ring / 9.0), fade = Math.Sin(depth * Math.PI);
                Point[] points = Curve(64, delegate(double u)
                {
                    double angle = u * Tau;
                    Point q = TunnelPoint(t, depth, angle);
                    double wave = Math.Sin(angle * 3 - t * .38 + depth * 3) * (energy * 2.1 + level * 3);
                    return new Point(q.X + Math.Cos(angle) * wave, q.Y + Math.Sin(angle) * wave);
                });
                Line(points, palette[ring % 3], 4.2 + depth * 2.8, fade * .075, true);
                Line(points, palette[ring % 3], .65 + depth * 1.2, fade * (.27 + energy * .15), true);
            }
            for (int ray = 0; ray < 18; ray++)
            {
                double head = Wrap(t * speed + ray * .173), fade = Math.Sin(head * Math.PI);
                Point[] points = new Point[9];
                for (int i = 0; i < points.Length; i++)
                {
                    double depth = Clamp(head - i * (.009 + energy * .009), 0, 1);
                    points[i] = TunnelPoint(t, depth, ray / 18.0 * Tau - t * .16 + depth * 1.08);
                }
                Line(points, palette[ray % 3], 4.5, fade * .09);
                Line(points, palette[ray % 3], .8 + head * .8, fade * .49);
                if (ray % 3 == 0 && head > .6) Bloom(points[0], 9 + head * 4, palette[ray % 3], fade * .22);
            }
        }

        private void Shockwave(double t)
        {
            // Idle flow has its own rhythm. In music mode only a rising captured
            // transient emits a ring or sparks; silence has no simulated beats.
            if (shockMusicMode != musicEnabled)
            {
                shockMusicMode = musicEnabled;
                shockStarts.Clear(); previousShockBeat = 0; lastShockTime = Double.NegativeInfinity;
            }
            if (musicEnabled)
            {
                if (t < lastShockTime)
                { shockStarts.Clear(); lastShockTime = Double.NegativeInfinity; }
                if (transientBeat > .12 && transientBeat - previousShockBeat > .015 && t - lastShockTime > .24)
                {
                    // The first small age gives deterministic still previews a
                    // visible attack. Smooth fades keep the live emission gentle.
                    shockStarts.Add(t - .04); lastShockTime = t;
                    CapturedShockwaveEmissions++;
                    if (shockStarts.Count > 4) shockStarts.RemoveAt(0);
                }
                previousShockBeat = transientBeat;
                for (int i = shockStarts.Count - 1; i >= 0; i--)
                    if (t - shockStarts[i] >= 3.3) shockStarts.RemoveAt(i);
                for (int wave = 0; wave < shockStarts.Count; wave++)
                    ShockRing(t - shockStarts[wave], wave);
                if (shockStarts.Count != 0)
                    ShockSparks(Clamp((t - shockStarts[shockStarts.Count - 1]) / 1.2, 0, 1), shockStarts.Count);
                Circle(126 + low * 8, palette[1], 1 + beat * 1.3 + level, .2 + beat * .2 + level * .18);
                return;
            }
            double period = 1.8 - energy * .48, cycle = t / period;
            for (int wave = 0; wave < 4; wave++)
            {
                ShockRing(Wrap((cycle + wave) / 4) * 4, wave);
            }
            double sparkAge = Wrap(cycle);
            ShockSparks(sparkAge, Math.Floor(cycle));
            Circle(126 + Math.Sin(sparkAge * Math.PI) * energy * 2.4, palette[1], 1, .2);
        }

        private void ShockRing(double age, int wave)
        {
            double progress = Clamp(age / 3.3, 0, 1);
            double fade = Smooth(age / .14) * (1 - Smooth((age - 1.15) / 2.15));
            double expansion = 1 - Math.Pow(1 - progress, 2.2);
            double recoil = Math.Sin(age * 10) * Math.Exp(-age * 1.55) * (3 + energy * 6 + beat * 4);
            double radius = 91 + expansion * 66 + recoil + low * 2;
            Point[] points = Curve(64, delegate(double u)
            {
                double angle = u * Tau, wobble = Math.Sin(angle * 4 + age * 5 + wave) * Math.Exp(-age) * energy * 3;
                return Polar(angle, radius + wobble);
            });
            Line(points, palette[wave % 3], 8 - progress * 5, fade * .075, true);
            Line(points, palette[wave % 3], 1.5 - progress * .7, fade * .48, true);
        }

        private void ShockSparks(double sparkAge, double generation)
        {
            double sparkFade = Smooth(sparkAge / .12) * (1 - Smooth((sparkAge - .32) / .65));
            for (int spark = 0; spark < 28; spark++)
            {
                double angle = spark / 28.0 * Tau + generation * .137;
                double variation = .55 + .45 * Math.Sin(spark * 2.37) * Math.Sin(spark * 2.37);
                double radius = 104 + (1 - Math.Pow(1 - sparkAge, 2)) * (31 + energy * 24 + beat * 8) * variation;
                if (musicEnabled) radius = Math.Min(167, radius);
                double bend = angle + sparkAge * Math.Sin(spark * 3.3) * .13, length = (2 + energy * 5) * (1 - sparkAge);
                Point head = Polar(bend, radius);
                Segment(head, Polar(bend - .014, radius - length), palette[spark % 3], 1.1, sparkFade * .55);
                if (spark % 7 == 0) Bloom(head, 7 + energy * 4, palette[spark % 3], sparkFade * .23);
            }
        }

        private Point CometPoint(double angle, int orbit, out double z)
        {
            z = Math.Sin(angle) * (.44 + orbit % 3 * .06);
            double perspective = 1 + z * .16;
            return Rotate(Math.Cos(angle) * (128 + orbit % 3 * 5 + low * 8) * perspective,
                Math.Sin(angle) * (94 + orbit % 3 * 7 + mid * 13) * perspective, orbit * .89 + .17 + mid * .12);
        }

        private void Comet(double t)
        {
            for (int orbit = 0; orbit < 7; orbit++)
            {
                int index = orbit, direction = orbit % 2 == 1 ? -1 : 1;
                double speed = (.35 + energy * .56) * (1 + orbit % 3 * .16) * direction;
                double phase = t * speed + orbit * 2.13, z;
                Point head = CometPoint(phase, orbit, out z);
                double visibility = .54 + (z + .6) * .32;
                for (int segment = 0; segment < 4; segment++)
                {
                    int part = segment;
                    Point[] points = Curve(8, delegate(double u)
                    {
                        double depth;
                        return CometPoint(phase - direction * (part + u) * (.11 + energy * .065) * (1 + mid * .9 + beat * .35), index, out depth);
                    });
                    double taper = Math.Pow(1 - segment / 4.0, 1.6);
                    Line(points, palette[orbit % 3], (7 + z * 2) * taper, taper * visibility * .09);
                    Line(points, palette[orbit % 3], (2.5 + z * .8 + level * 1.7) * taper, taper * visibility * .48);
                }
                Bloom(head, 12 + (z + .6) * 6 + high * 6 + level * 7, palette[orbit % 3], visibility * .3);
                Dot(head, 1.7 + (z + .6) * 1.15 + high * 2 + level * 1.5, palette[orbit % 3], visibility * .85);
            }
        }

        private static Point[] Curve(int steps, Func<double, Point> sample)
        {
            Point[] points = new Point[steps + 1];
            for (int i = 0; i <= steps; i++) points[i] = sample(i / (double)steps);
            return points;
        }

        private static StreamGeometry Geometry(Point[] points, bool closed)
        {
            StreamGeometry path = new StreamGeometry();
            using (StreamGeometryContext context = path.Open())
            {
                context.BeginFigure(points[0], false, closed);
                for (int i = 1; i < points.Length; i++) context.LineTo(points[i], true, false);
            }
            path.Freeze();
            return path;
        }

        private void Line(Point[] points, Color color, double width, double alpha, bool closed = false)
        { drawing.DrawGeometry(null, Pen(color, alpha, width), Geometry(points, closed)); }

        private void Segment(Point start, Point end, Color color, double width, double alpha)
        { drawing.DrawLine(Pen(color, alpha, width), start, end); }

        private void Circle(double radius, Color color, double width, double alpha)
        { drawing.DrawEllipse(null, Pen(color, alpha, width), new Point(0, 0), radius, radius); }

        private void Dot(Point point, double radius, Color color, double alpha)
        { drawing.DrawEllipse(Solid(color, alpha), null, point, Math.Max(.1, radius), Math.Max(.1, radius)); }

        private void Bloom(Point point, double radius, Color color, double alpha)
        {
            if (musicEnabled)
            {
                // Strong peaks stay inside the gadget's clear outer gutter.
                // This changes only audio-driven glow extents, not idle artwork.
                double distance = Math.Sqrt(point.X * point.X + point.Y * point.Y);
                radius = Math.Min(radius, Math.Max(.1, 170 - distance));
            }
            color = Alpha(color, alpha);
            uint key = Key(color);
            Brush brush;
            if (!blooms.TryGetValue(key, out brush))
            {
                if (blooms.Count >= 512) blooms.Clear();
                RadialGradientBrush glow = new RadialGradientBrush();
                glow.GradientStops.Add(new GradientStop(color, 0));
                Color middle = color; middle.A = (byte)Math.Round(color.A * .44);
                glow.GradientStops.Add(new GradientStop(middle, .34));
                Color edge = color; edge.A = 0;
                glow.GradientStops.Add(new GradientStop(edge, 1));
                glow.Freeze();
                brush = glow;
                blooms.Add(key, brush);
            }
            drawing.DrawEllipse(brush, null, point, radius, radius);
        }

        private Brush Solid(Color color, double alpha)
        {
            color = Alpha(color, alpha);
            uint key = Key(color);
            Brush brush;
            if (!solids.TryGetValue(key, out brush))
            {
                if (solids.Count >= 512) solids.Clear();
                brush = new SolidColorBrush(color); brush.Freeze();
                solids.Add(key, brush);
            }
            return brush;
        }

        private Pen Pen(Color color, double alpha, double width)
        {
            int quantizedWidth = Math.Max(1, (int)Math.Round(width * 20));
            long key = ((long)Key(Alpha(color, alpha)) << 16) | (uint)quantizedWidth;
            Pen pen;
            if (!pens.TryGetValue(key, out pen))
            {
                if (pens.Count >= 1024) pens.Clear();
                pen = new Pen(Solid(color, alpha), quantizedWidth / 20.0)
                { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
                pen.Freeze(); pens.Add(key, pen);
            }
            return pen;
        }

        private GradientStop Stop(Color color, double alpha, double offset)
        { return new GradientStop(Alpha(color, alpha), offset); }

        private Color Alpha(Color color, double alpha)
        { color.A = (byte)Math.Round(255 * Unit(alpha * (light ? 1.25 : 1) * lightGain)); return color; }

        private static uint Key(Color color)
        { return ((uint)color.A << 24) | ((uint)color.R << 16) | ((uint)color.G << 8) | color.B; }

        private static Color Rgb(byte r, byte g, byte b) { return Color.FromRgb(r, g, b); }
        private static Color Mix(Color a, Color b, double f)
        { return Rgb((byte)Math.Round(a.R + (b.R - a.R) * f), (byte)Math.Round(a.G + (b.G - a.G) * f), (byte)Math.Round(a.B + (b.B - a.B) * f)); }
        private static Point Polar(double angle, double radius) { return new Point(Math.Cos(angle) * radius, Math.Sin(angle) * radius); }
        private static Point Rotate(double x, double y, double angle)
        { return new Point(x * Math.Cos(angle) - y * Math.Sin(angle), x * Math.Sin(angle) + y * Math.Cos(angle)); }
        private static double Clamp(double value, double min, double max) { return Math.Max(min, Math.Min(max, value)); }
        private static double Wrap(double value) { return value - Math.Floor(value); }
        private static double Smooth(double value) { double x = Unit(value); return x * x * (3 - 2 * x); }
    }
}
