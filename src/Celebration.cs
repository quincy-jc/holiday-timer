using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Forms = System.Windows.Forms;

namespace HolidayTimer
{
    internal static class Celebration
    {
        // Cohorts overlap: every authored tail ends before 8.2 seconds, which
        // is less than two cycles. Only the preceding and current cohorts are
        // ever needed. A populated phase is shown immediately after sign-in.
        internal const double CycleSeconds = 6.2;
        internal const double WarmStartSeconds = 2.6;
        private static readonly Dictionary<Window, RunState> Runs = new Dictionary<Window, RunState>();
        private static readonly Color[] Defaults = { Color.FromRgb(111, 195, 255), Color.FromRgb(102, 223, 235), Color.FromRgb(186, 224, 255) };
        internal static int ActiveRunCount { get { return Runs.Count; } }
        internal static int RenderingRunCount
        { get { int count = 0; foreach (RunState state in Runs.Values) if (state.Attached) count++; return count; } }
        internal static long FrameCount(Window window) { RunState state; return Runs.TryGetValue(window, out state) ? state.Frames : 0; }
        internal static CelebrationKind? CurrentKind(Window window) { RunState state; return Runs.TryGetValue(window, out state) ? (CelebrationKind?)state.Kind : null; }
        internal static Color[] BaseColours(Window window) { RunState state; return Runs.TryGetValue(window, out state) ? (Color[])state.Colours.Clone() : new Color[0]; }
        internal static Rect MessageClearance(Window window) { RunState state; return Runs.TryGetValue(window, out state) ? state.Clearance : Rect.Empty; }
        internal static int CachedResourceCount(Window window) { RunState state; return Runs.TryGetValue(window, out state) ? state.CachedResourceCount : 0; }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr handle, IntPtr after, int x, int y, int width, int height, uint flags);
        internal static void FullScreen(Window window)
        { FullScreen(window, Forms.Screen.PrimaryScreen.Bounds); }
        internal static void FullScreen(Window window, System.Drawing.Rectangle bounds)
        {
            if (window == null) throw new ArgumentNullException("window");
            if (bounds.Width <= 0 || bounds.Height <= 0) throw new ArgumentException("A celebration screen must have positive physical dimensions.", "bounds");
            window.WindowState = WindowState.Normal;
            IntPtr handle = new WindowInteropHelper(window).EnsureHandle();
            if (!SetWindowPos(handle, new IntPtr(-1), bounds.Left, bounds.Top, bounds.Width, bounds.Height, 0x0010 | 0x0040))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "The celebration could not fill the screen.");
            window.UpdateLayout();
            // PerMonitorV2 can update the HWND's DPI during its first move.
            // Reassert physical bounds after that synchronous size/layout pass;
            // WPF derives DIPs from the monitor instead of treating pixels as DIPs.
            if (!SetWindowPos(handle, new IntPtr(-1), bounds.Left, bounds.Top, bounds.Width, bounds.Height, 0x0010 | 0x0040))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "The celebration could not fill the screen.");
            window.UpdateLayout();
        }

        internal static void Play(Window window, bool animate, CelebrationKind kind = CelebrationKind.Fireworks)
        {
            if (window == null) throw new ArgumentNullException("window");
            Stop(window);
            if (!animate || !SystemParameters.ClientAreaAnimation || SystemParameters.HighContrast || !window.IsVisible || window.WindowState == WindowState.Minimized) return;
            RunState state = Create(window, kind);
            if (state == null) return;
            state.Start();
        }

        // Authored time for real WPF fixtures. This does not enable motion or
        // alter preferences: it holds one static frame until Stop/Close.
        internal static void RenderAt(Window window, double seconds, CelebrationKind kind = CelebrationKind.Fireworks)
        {
            if (window == null) throw new ArgumentNullException("window");
            RunState state;
            if (seconds < 0 || Double.IsNaN(seconds) || Double.IsInfinity(seconds)) { Stop(window); return; }
            if (!Runs.TryGetValue(window, out state) || state.Kind != kind || state.Attached || !PaletteMatches(window, state.Colours))
            { Stop(window); state = Create(window, kind); }
            if (state != null) state.Paint(seconds);
        }

        private static RunState Create(Window window, CelebrationKind kind)
        {
            Canvas canvas = window.FindName("CelebrationCanvas") as Canvas;
            if (canvas == null) return null;
            window.UpdateLayout();
            if (canvas.ActualWidth <= 0 || canvas.ActualHeight <= 0) return null;
            canvas.IsHitTestVisible = false; canvas.ClipToBounds = true;
            RunState state = new RunState(window, canvas, kind);
            Runs.Add(window, state);
            window.Closed += state.Closed;
            window.IsVisibleChanged += state.VisibilityChanged;
            window.StateChanged += state.StateChanged;
            state.Surface.Unloaded += state.Unloaded;
            canvas.Children.Add(state.Surface);
            return state;
        }
        private static bool PaletteMatches(Window window, Color[] colours)
        {
            Color[] current = window.TryFindResource("CelebrationColours") as Color[] ?? Defaults;
            if (current.Length != colours.Length) return false;
            for (int i = 0; i < current.Length; i++) if (current[i] != colours[i]) return false;
            return true;
        }

        internal static void Stop(Window window)
        {
            if (window == null) return;
            RunState state;
            if (Runs.TryGetValue(window, out state))
            {
                Runs.Remove(window); state.Detach();
                window.Closed -= state.Closed;
                window.IsVisibleChanged -= state.VisibilityChanged;
                window.StateChanged -= state.StateChanged;
                state.Surface.Unloaded -= state.Unloaded;
                state.Canvas.Children.Remove(state.Surface);
            }
            else
            { Canvas canvas = window.FindName("CelebrationCanvas") as Canvas; if (canvas != null) canvas.Children.Clear(); }
        }

        private sealed class Layer : FrameworkElement
        {
            private readonly DrawingVisual drawing = new DrawingVisual();
            internal Layer() { AddVisualChild(drawing); IsHitTestVisible = false; Focusable = false; }
            protected override int VisualChildrenCount { get { return 1; } }
            protected override Visual GetVisualChild(int index) { if (index != 0) throw new ArgumentOutOfRangeException("index"); return drawing; }
            internal DrawingContext Open() { return drawing.RenderOpen(); }
        }

        private sealed class RunState
        {
            internal readonly Window Window;
            internal readonly Canvas Canvas;
            internal readonly CelebrationKind Kind;
            internal readonly Color[] Colours;
            internal readonly Layer Surface = new Layer();
            internal bool Attached;
            internal long Frames;
            internal Rect Clearance;
            private readonly Stopwatch clock = new Stopwatch();
            private readonly Dictionary<int, Brush> inks = new Dictionary<int, Brush>();
            private readonly Dictionary<int, Pen> pens = new Dictionary<int, Pen>();
            private readonly Dictionary<int, Brush> glows = new Dictionary<int, Brush>();
            private readonly Brush[,] petalPaint = new Brush[3, 8];
            private readonly double[] random = new double[1600];
            private double lastFrame = -1;
            private double width, height, scale;
            private Geometry safeClip;
            internal int CachedResourceCount { get { return inks.Count + pens.Count + glows.Count + petalPaint.Length; } }
            internal RunState(Window window, Canvas canvas, CelebrationKind kind)
            {
                Window = window; Canvas = canvas; Kind = kind;
                Color[] source = window.TryFindResource("CelebrationColours") as Color[];
                Colours = source == null || source.Length == 0 ? (Color[])Defaults.Clone() : (Color[])source.Clone();
                Random generator = new Random(71241);
                for (int i = 0; i < random.Length; i++) random[i] = generator.NextDouble();
                for (int colour = 0; colour < 3; colour++) for (int light = 0; light < 8; light++)
                {
                    Color basis = Colours[colour % Colours.Length];
                    LinearGradientBrush brush = new LinearGradientBrush { StartPoint = new Point(.18, 0), EndPoint = new Point(.7, 1) };
                    brush.GradientStops.Add(new GradientStop(Mix(basis, Colors.White, .38 + light * .05), 0));
                    brush.GradientStops.Add(new GradientStop(Mix(basis, Colors.White, .08 + light * .018), .31));
                    brush.GradientStops.Add(new GradientStop(basis, .62));
                    brush.GradientStops.Add(new GradientStop(Mix(basis, Color.FromRgb(26, 19, 40), .52), 1));
                    brush.Freeze(); petalPaint[colour, light] = brush;
                }
            }
            internal void Start()
            {
                Paint(0); lastFrame = 0; clock.Start(); Attached = true;
                CompositionTarget.Rendering += Render;
                SystemParameters.StaticPropertyChanged += PreferenceChanged;
            }
            internal void Detach()
            {
                if (Attached)
                { CompositionTarget.Rendering -= Render; SystemParameters.StaticPropertyChanged -= PreferenceChanged; }
                Attached = false; clock.Stop();
            }
            private void Render(object sender, EventArgs args)
            {
                if (!Window.IsVisible || Window.WindowState == WindowState.Minimized || !SystemParameters.ClientAreaAnimation || SystemParameters.HighContrast) { Stop(Window); return; }
                double time = clock.Elapsed.TotalSeconds;
                if (lastFrame >= 0 && time - lastFrame < 1.0 / 30) return;
                lastFrame = time; Paint(time);
            }
            internal void Closed(object sender, EventArgs args) { Stop(Window); }
            internal void Unloaded(object sender, RoutedEventArgs args) { Stop(Window); }
            internal void VisibilityChanged(object sender, DependencyPropertyChangedEventArgs args) { if (!(bool)args.NewValue) Stop(Window); }
            internal void StateChanged(object sender, EventArgs args) { if (Window.WindowState == WindowState.Minimized) Stop(Window); }
            private void PreferenceChanged(object sender, PropertyChangedEventArgs args)
            { if (!SystemParameters.ClientAreaAnimation || SystemParameters.HighContrast) Stop(Window); }

            internal void Paint(double time)
            {
                double nextWidth = Canvas.ActualWidth, nextHeight = Canvas.ActualHeight;
                Rect nextClearance = ClearMessage(Window, Canvas, nextWidth, nextHeight);
                if (width != nextWidth || height != nextHeight || Clearance != nextClearance)
                {
                    width = nextWidth; height = nextHeight; Clearance = nextClearance;
                    scale = Math.Max(.6, Math.Min(2.2, Math.Min(width / 1440, height / 900)));
                    Surface.Width = width; Surface.Height = height;
                    GeometryGroup clip = new GeometryGroup { FillRule = FillRule.EvenOdd };
                    clip.Children.Add(new RectangleGeometry(new Rect(0, 0, width, height)));
                    if (!Clearance.IsEmpty) clip.Children.Add(new RectangleGeometry(Clearance));
                    clip.Freeze(); safeClip = clip;
                }
                using (DrawingContext draw = Surface.Open())
                {
                    draw.PushClip(safeClip);
                    double phase = (time % CycleSeconds + WarmStartSeconds) % CycleSeconds;
                    // Keep the older cohort behind the newer one. At a wrap,
                    // the old current cohort simply becomes the previous one;
                    // new arrivals have zero opacity or start below the frame.
                    Sequence(draw, phase + CycleSeconds);
                    Sequence(draw, phase);
                    draw.Pop();
                }
                Frames++;
            }
            private void Sequence(DrawingContext draw, double time)
            {
                if (Kind == CelebrationKind.Flowers) Flowers(draw, time);
                else Fireworks(draw, time);
                Confetti(draw, time);
            }

            private void Fireworks(DrawingContext draw, double time)
            {
                // Different planes open in a rhythm, rather than all at once.
                double[] xs = { .16, .84, .47, .08, .92, .28, .72, .50, .15, .86 };
                double[] ys = { .27, .25, .10, .57, .56, .13, .11, .08, .28, .27 };
                double[] births = { 1.0, 1.48, 2.1, 2.58, 2.94, 3.48, 3.95, 4.36, 4.78, 5.05 };
                for (int burst = 0; burst < births.Length; burst++)
                {
                    double birth = births[burst], age = time - birth;
                    Point center = new Point(width * xs[burst], height * ys[burst]);
                    int colour = burst % Colours.Length;
                    double radius = Math.Min(290 * scale, Math.Min(width, height) * (burst % 3 == 0 ? .225 : .18));
                    if (age < 0 && age > -.98) Launch(draw, center, time - birth + .98, colour, burst);
                    if (age < 0 || age > 2.55) continue;
                    double fade = Math.Pow(1 - Unit(age / 2.55), 1.45);
                    double core = Math.Exp(-age * 11);
                    Glow(draw, center, radius * (.34 + Unit(age) * .25), colour, .22 * Math.Exp(-age * 2.2));
                    Glow(draw, center, 26 * scale, colour + 3, core * .75);
                    draw.DrawEllipse(Ink(colour + 3, core), null, center, (2.5 + core * 6) * scale, (2.5 + core * 6) * scale);
                    const int sparks = 84;
                    for (int i = 0; i < sparks; i++)
                    {
                        double sparkAge = age - (i % 4) * .016;
                        if (sparkAge < 0) continue;
                        int seed = burst * 84 + i;
                        double depth = (1 - 2.0 * (i + .5) / sparks);
                        double plane = Math.Sqrt(1 - depth * depth);
                        double angle = i * 2.39996323 + burst * .73;
                        double reach = radius * (.70 + random[seed] * .35) * plane * (1 + depth * .12);
                        Point head = SparkPosition(center, angle, reach, sparkAge);
                        double opacity = fade * (.45 + (depth + 1) * .275) * Avoid(head, 9 * scale);
                        double size = (.7 + (depth + 1) * .75) * scale;
                        for (int tail = 5; tail >= 1; tail--)
                        {
                            double a = Math.Max(0, sparkAge - tail * .048), b = Math.Max(0, sparkAge - (tail - 1) * .048);
                            Point from = SparkPosition(center, angle, reach, a), to = SparkPosition(center, angle, reach, b);
                            draw.DrawLine(PenFor(colour, opacity * (1 - tail * .14), size * .72), from, to);
                        }
                        if (i % 3 == 0) Glow(draw, head, size * 4.8, colour, opacity * .5);
                        draw.DrawEllipse(Ink(colour + (i % 7 == 0 ? 3 : 0), opacity), null, head, size, size);
                    }
                }
            }
            private Point SparkPosition(Point center, double angle, double reach, double age)
            {
                double travel = 1 - Math.Exp(-age * 2.0);
                return new Point(center.X + Math.Cos(angle) * reach * travel,
                    center.Y + Math.Sin(angle) * reach * travel + 58 * scale * age * age);
            }
            private void Launch(DrawingContext draw, Point center, double age, int colour, int burst)
            {
                double progress = Unit(age / .98);
                Point start = new Point(center.X + (burst % 2 == 0 ? -1 : 1) * width * .075, height * 1.03);
                for (int i = 0; i < 10; i++)
                {
                    double p = Math.Max(0, progress - i * .024), q = Math.Max(0, progress - (i + 1) * .024);
                    Point head = Rocket(start, center, p), tail = Rocket(start, center, q);
                    draw.DrawLine(PenFor(colour + 3, (1 - i / 10.0) * .68 * Avoid(head, 15), 1.8 * scale), tail, head);
                }
                Point rocket = Rocket(start, center, progress);
                double opacity = Avoid(rocket, 20);
                Glow(draw, rocket, 14 * scale, colour, .8 * opacity);
                draw.DrawEllipse(Ink(colour + 3, opacity), null, rocket, 2.2 * scale, 3.3 * scale);
            }
            private static Point Rocket(Point start, Point finish, double progress)
            {
                double travel = 1 - Math.Pow(1 - progress, 1.55);
                return new Point(start.X + (finish.X - start.X) * travel + Math.Sin(progress * Math.PI) * 24,
                    start.Y + (finish.Y - start.Y) * travel);
            }

            private void Flowers(DrawingContext draw, double time)
            {
                // Thrown bouquets share the fireworks' rise/burst/fall rhythm.
                // Each launch is a small arrangement, not one large flower disc.
                double[] xs = { .15, .85, .29, .71, .10, .91, .50 };
                double[] ys = { .26, .24, .12, .11, .40, .37, .07 };
                double[] births = { 1.08, 1.63, 2.22, 2.85, 3.46, 4.08, 4.74 };
                for (int i = 0; i < xs.Length; i++)
                {
                    double age = time - births[i];
                    if (age < -1.06 || age > 2.90) continue;
                    Point center = new Point(width * xs[i], height * ys[i]);
                    int colour = i % 3;
                    if (age < 0)
                    {
                        double progress = Unit((age + 1.06) / 1.06);
                        Point start = new Point(width * (i % 2 == 0 ? .075 : .925), height * 1.08);
                        Point position = Rocket(start, center, progress);
                        double fade = Unit(progress * 4) * Avoid(position, 64 * scale);
                        Bouquet(draw, position, (.75 + .20 * progress) * scale, (i % 2 == 0 ? -1 : 1) * (65 - progress * 88), fade, i);
                        continue;
                    }
                    if (age < .23) Bouquet(draw, center, .95 * scale, (i % 2 == 0 ? -1 : 1) * (-23 + age * 80), Unit(1 - age / .23), i);
                    Glow(draw, center, 35 * scale, colour + 3, Math.Exp(-age * 8) * .34);
                    double reach = Math.Min(265 * scale, Math.Min(width, height) * .235);
                    const int petals = 96;
                    for (int petal = 0; petal < petals; petal++)
                    {
                        int seed = i * petals + petal;
                        double petalAge = age - (petal % 5) * .012;
                        double life = 2.35 + random[seed] * .49;
                        if (petalAge < 0 || petalAge > life) continue;
                        double depth = 1 - 2.0 * (petal + .5) / petals;
                        double angle = petal * 2.39996323 + i * .7;
                        double plane = Math.Sqrt(1 - depth * depth);
                        double travel = 1 - Math.Exp(-petalAge * 1.65);
                        double distance = reach * plane * (.64 + random[seed + 700] * .38);
                        Point position = new Point(center.X + Math.Cos(angle) * distance * travel + Math.Sin(petalAge * 3.3 + petal) * 10 * scale,
                            center.Y + Math.Sin(angle) * distance * travel + 42 * scale * petalAge * petalAge);
                        double size = (10 + (depth + 1) * 5.5) * scale;
                        double fade = Unit(petalAge / .08) * Unit((life - petalAge) / .66) * (.46 + (depth + 1) * .24) * Avoid(position, size * 1.3);
                        if (fade < .01) continue;
                        draw.PushOpacity(fade);
                        draw.PushTransform(new TranslateTransform(position.X, position.Y));
                        draw.PushTransform(new RotateTransform(angle * 180 / Math.PI + petalAge * (55 + random[seed + 800] * 85) + Math.Sin(petalAge * 4) * 12));
                        draw.PushTransform(new ScaleTransform(size * (.14 + .86 * Math.Abs(Math.Cos(petalAge * 1.7 + petal))), size * (.7 + .3 * Math.Abs(Math.Sin(petalAge * 1.1 + petal)))));
                        draw.PushTransform(new TranslateTransform(.10, .15));
                        draw.DrawGeometry(Ink((colour + petal % 3) % 3 + 6, .22), null, Petal); draw.Pop();
                        draw.DrawGeometry(petalPaint[(colour + petal % 3) % 3, petal % 8], null, Petal);
                        draw.DrawGeometry(null, PenFor((colour + petal % 3) % 3 + 3, .48, .045), Fold);
                        draw.Pop(); draw.Pop(); draw.Pop(); draw.Pop();
                    }
                }
            }
            private void Bouquet(DrawingContext draw, Point position, double size, double angle, double opacity, int seed)
            {
                if (opacity <= .01) return;
                draw.PushOpacity(opacity); draw.PushTransform(new TranslateTransform(position.X, position.Y));
                draw.PushTransform(new RotateTransform(angle)); draw.PushTransform(new ScaleTransform(size, size * .91));
                Point[] heads = { new Point(-22, -22), new Point(21, -20), new Point(0, -46), new Point(-30, 3), new Point(28, 4) };
                for (int i = 0; i < heads.Length; i++)
                {
                    StreamGeometry stem = Shape(delegate(StreamGeometryContext p)
                    { p.BeginFigure(heads[i], false, false); p.BezierTo(new Point(heads[i].X * .7, 12), new Point(7, 35), new Point(0, 54), true, false); });
                    draw.DrawGeometry(null, PenFor((seed + 1) % 3, .83, 2.2), stem);
                }
                for (int i = 0; i < heads.Length; i++) Bloom(draw, heads[i], i == 2 ? 24 : 21, 1, 1, seed + i, 1.5);
                draw.DrawGeometry(petalPaint[seed % 3, 6], null, BouquetWrap);
                draw.DrawGeometry(Ink(seed % 3 + 3, .35), null, WrapFold);
                draw.DrawGeometry(petalPaint[(seed + 1) % 3, 5], null, BowLeft);
                draw.DrawGeometry(petalPaint[(seed + 1) % 3, 3], null, BowRight);
                draw.DrawGeometry(petalPaint[(seed + 1) % 3, 4], null, BowTails);
                draw.DrawEllipse(Ink((seed + 1) % 3 + 3, .93), null, new Point(0, 39), 3.5, 4.5);
                draw.Pop(); draw.Pop(); draw.Pop(); draw.Pop();
            }
            private void Bloom(DrawingContext draw, Point center, double radius, double opening, double opacity, int seed, double age)
            {
                if (opacity <= .01) return;
                int colour = seed % 3;
                draw.PushOpacity(opacity);
                Glow(draw, new Point(center.X + 4 * scale, center.Y + 9 * scale), radius * .8, colour + 6, .35);
                // Fine curved stems and cupped leaves anchor a bloom in space.
                Point stemEnd = new Point(center.X + (seed % 2 == 0 ? -1 : 1) * radius * 1.5, center.Y + radius * .8);
                StreamGeometry stem = new StreamGeometry();
                using (StreamGeometryContext line = stem.Open())
                { line.BeginFigure(center, false, false); line.BezierTo(new Point(center.X + radius * .45, center.Y + radius * .25), new Point(stemEnd.X - radius * .5, stemEnd.Y + radius * .35), stemEnd, true, false); }
                stem.Freeze(); draw.DrawGeometry(null, PenFor((colour + 1) % 3, .45, 1.6 * scale), stem);
                draw.PushTransform(new TranslateTransform(stemEnd.X, stemEnd.Y));
                draw.PushTransform(new RotateTransform(seed % 2 == 0 ? -55 : 60));
                draw.PushTransform(new ScaleTransform(radius * .30, radius * .46));
                draw.DrawGeometry(petalPaint[(colour + 1) % 3, 1], null, Leaf);
                draw.Pop(); draw.Pop(); draw.Pop();
                draw.PushTransform(new TranslateTransform(center.X, center.Y));
                draw.PushTransform(new RotateTransform(seed * 31 + Math.Sin(age * .65 + seed) * 4));
                draw.PushTransform(new ScaleTransform(.62 + opening * .38, (.62 + opening * .38) * (seed % 3 == 0 ? .84 : .94)));
                int[] count = { 10, 9, 8, 7, 5, 4 };
                double[] rings = { 1, .80, .63, .47, .32, .19 };
                for (int layer = 0; layer < count.Length; layer++)
                    for (int petal = 0; petal < count[layer]; petal++)
                    {
                        double angle = petal * 360.0 / count[layer] + layer * 32 + seed * 17;
                        double size = radius * rings[layer] * (.78 + random[(seed * 53 + petal + layer * 13) % random.Length] * .22);
                        double unfurl = .72 + .28 * Ease(Unit((age - layer * .045 - petal * .012) / .85));
                        int light = (int)Math.Round(3.5 + 3.4 * Math.Cos((angle - 125) * Math.PI / 180));
                        draw.PushTransform(new RotateTransform(angle));
                        draw.PushTransform(new ScaleTransform(size * unfurl, size));
                        draw.DrawGeometry(petalPaint[colour, light], null, Petal);
                        draw.DrawGeometry(null, PenFor(colour + 3, .38, .010 + layer * .002), Fold);
                        draw.Pop(); draw.Pop();
                    }
                draw.DrawEllipse(Ink(colour + 6, .82), null, new Point(), radius * .074, radius * .05);
                draw.DrawGeometry(null, PenFor(colour + 3, .65, radius * .016), HeartFold(radius));
                draw.Pop(); draw.Pop(); draw.Pop(); draw.Pop();
            }

            private void Confetti(DrawingContext draw, double time)
            {
                for (int i = 0; i < 176; i++)
                {
                    double born = (i / 88) * 1.55 + (i % 22) * .018 + (Kind == CelebrationKind.Flowers ? .40 : .08);
                    double age = time - born, life = 4.4 + random[i + 300] * 1.45;
                    if (age < 0 || age > life) continue;
                    bool left = i % 2 == 0;
                    double depth = .36 + random[i + 500] * .92;
                    double sideways = (1 - Math.Exp(-age * .4)) / .4;
                    double x = width * (left ? .035 : .965) + (left ? 1 : -1) * width * (.11 + random[i + 600] * .16) * sideways;
                    double y = height * .96 - height * (.42 + random[i + 700] * .30) * age + height * .115 * age * age + Math.Sin(age * 2 + i) * 10 * depth;
                    Point position = new Point(x, y);
                    double size = (3.2 + depth * 5.8) * scale;
                    double opacity = Unit(age / .12) * Math.Pow(Unit((life - age) / .9), 1.2) * (.4 + depth * .47) * Avoid(position, size * 2);
                    if (opacity <= .01 || x < -25 || x > width + 25 || y < -25 || y > height + 25) continue;
                    draw.PushOpacity(opacity);
                    draw.PushTransform(new TranslateTransform(x, y));
                    draw.PushTransform(new RotateTransform(i * 137.5 + age * (80 + depth * 75) * (left ? 1 : -1)));
                    draw.PushTransform(new ScaleTransform(size * (.12 + .88 * Math.Abs(Math.Cos(age * (2 + depth) + i))), size));
                    draw.DrawRoundedRectangle(Ink(i % 3 + 6, .32), null, new Rect(-.39 + .15, -.65 + .18, .78, 1.3), .05, .05);
                    draw.DrawRoundedRectangle(petalPaint[i % 3, i % 8], null, new Rect(-.39, -.65, .78, 1.3), .05, .05);
                    draw.DrawLine(PenFor(i % 3 + 3, .70, .08), new Point(-.32, -.57), new Point(.28, -.57));
                    draw.Pop(); draw.Pop(); draw.Pop(); draw.Pop();
                }
            }

            private double Avoid(Point point, double extent)
            { return Clearance.IsEmpty ? 1 : Unit((Distance(point, Clearance) - extent) / (25 * scale)); }
            private Brush Ink(int colour, double opacity)
            {
                int alpha = (int)Math.Round(Unit(opacity) * 20), key = colour * 32 + alpha;
                Brush brush;
                if (!inks.TryGetValue(key, out brush))
                { Color value = Shade(colour); value.A = (byte)Math.Round(alpha / 20.0 * 255); SolidColorBrush solid = new SolidColorBrush(value); solid.Freeze(); brush = solid; inks.Add(key, brush); }
                return brush;
            }
            private Pen PenFor(int colour, double opacity, double thickness)
            {
                int alpha = (int)Math.Round(Unit(opacity) * 20), size = Math.Max(1, Math.Min(320, (int)Math.Round(thickness * 100)));
                int key = colour * 131072 + alpha * 512 + size;
                Pen pen;
                if (!pens.TryGetValue(key, out pen))
                { pen = new Pen(Ink(colour, alpha / 20.0), size / 100.0) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round }; pen.Freeze(); if (pens.Count < 4096) pens.Add(key, pen); }
                return pen;
            }
            private void Glow(DrawingContext draw, Point point, double radius, int colour, double opacity)
            {
                if (radius <= 0 || opacity <= .012) return;
                int alpha = (int)Math.Round(Unit(opacity) * 20), key = colour * 32 + alpha;
                Brush brush;
                if (!glows.TryGetValue(key, out brush))
                {
                    Color core = Shade(colour), mid = core, edge = core;
                    core.A = (byte)Math.Round(alpha / 20.0 * 200); mid.A = (byte)Math.Round(core.A * .24); edge.A = 0;
                    RadialGradientBrush glow = new RadialGradientBrush { Center = new Point(.5, .5), GradientOrigin = new Point(.5, .5), RadiusX = .5, RadiusY = .5 };
                    glow.GradientStops.Add(new GradientStop(core, 0)); glow.GradientStops.Add(new GradientStop(mid, .35)); glow.GradientStops.Add(new GradientStop(edge, 1)); glow.Freeze(); brush = glow; glows.Add(key, brush);
                }
                draw.DrawEllipse(brush, null, point, radius, radius);
            }
            private Color Shade(int value)
            {
                Color colour = Colours[value % 3 % Colours.Length];
                return value < 3 ? colour : value < 6 ? Mix(colour, Color.FromRgb(255, 244, 225), .72) : Mix(colour, Color.FromRgb(20, 14, 35), .72);
            }
        }

        private static Rect ClearMessage(Window window, Canvas canvas, double width, double height)
        {
            FrameworkElement message = window.FindName("GreetingMessage") as FrameworkElement;
            if (message != null && message.ActualWidth > 0 && message.ActualHeight > 0)
            {
                try { Rect result = new Rect(message.TranslatePoint(new Point(), canvas), message.RenderSize); result.Inflate(20, 16); return result; }
                catch (InvalidOperationException) { }
            }
            return new Rect(width * .30, height * .28, width * .40, height * .46);
        }
        private static double Distance(Point point, Rect rect)
        {
            if (rect.IsEmpty) return Double.PositiveInfinity;
            double dx = Math.Max(Math.Max(rect.Left - point.X, 0), point.X - rect.Right);
            double dy = Math.Max(Math.Max(rect.Top - point.Y, 0), point.Y - rect.Bottom);
            return Math.Sqrt(dx * dx + dy * dy);
        }
        private static double Unit(double value) { return Math.Max(0, Math.Min(1, value)); }
        private static double Ease(double value) { return 1 - Math.Pow(1 - Unit(value), 3); }
        private static Color Mix(Color a, Color b, double ratio)
        { return Color.FromRgb((byte)Math.Round(a.R + (b.R - a.R) * ratio), (byte)Math.Round(a.G + (b.G - a.G) * ratio), (byte)Math.Round(a.B + (b.B - a.B) * ratio)); }
        private static StreamGeometry Shape(Action<StreamGeometryContext> paint)
        { StreamGeometry shape = new StreamGeometry(); using (StreamGeometryContext context = shape.Open()) paint(context); shape.Freeze(); return shape; }
        private static readonly StreamGeometry Petal = Shape(delegate(StreamGeometryContext p)
        {
            p.BeginFigure(new Point(-.20, -.03), true, true);
            p.BezierTo(new Point(-.57, -.28), new Point(-.66, -.73), new Point(-.28, -.97), true, false);
            p.BezierTo(new Point(-.06, -1.12), new Point(.43, -1.08), new Point(.49, -.82), true, false);
            p.BezierTo(new Point(.26, -.88), new Point(.38, -.68), new Point(.22, -.58), true, false);
            p.BezierTo(new Point(.52, -.52), new Point(.43, -.23), new Point(.18, -.02), true, false);
        });
        private static readonly StreamGeometry Fold = Shape(delegate(StreamGeometryContext p)
        {
            p.BeginFigure(new Point(-.28, -.96), false, false);
            p.BezierTo(new Point(-.05, -1.08), new Point(.35, -1.03), new Point(.44, -.84), true, false);
            p.BezierTo(new Point(.20, -.9), new Point(.30, -.7), new Point(.18, -.61), true, false);
        });
        private static readonly StreamGeometry Leaf = Shape(delegate(StreamGeometryContext p)
        {
            p.BeginFigure(new Point(), true, true);
            p.BezierTo(new Point(-.7, -.45), new Point(-.32, -1), new Point(0, -1.2), true, false);
            p.BezierTo(new Point(.57, -.55), new Point(.52, -.15), new Point(), true, false);
        });
        private static readonly StreamGeometry BouquetWrap = Shape(delegate(StreamGeometryContext p)
        {
            p.BeginFigure(new Point(-32, 12), true, true);
            p.BezierTo(new Point(-20, 17), new Point(22, 18), new Point(32, 10), true, false);
            p.LineTo(new Point(5, 57), true, false); p.LineTo(new Point(-5, 57), true, false);
        });
        private static readonly StreamGeometry WrapFold = Shape(delegate(StreamGeometryContext p)
        { p.BeginFigure(new Point(-29, 14), true, true); p.LineTo(new Point(-7, 18), true, false); p.LineTo(new Point(3, 55), true, false); p.LineTo(new Point(-4, 55), true, false); });
        private static readonly StreamGeometry BowLeft = Shape(delegate(StreamGeometryContext p)
        { p.BeginFigure(new Point(), true, true); p.LineTo(new Point(0, 39), false, false); p.BezierTo(new Point(-26, 20), new Point(-35, 49), new Point(-15, 47), true, false); p.BezierTo(new Point(-5, 46), new Point(-3, 42), new Point(0, 39), true, false); });
        private static readonly StreamGeometry BowRight = Shape(delegate(StreamGeometryContext p)
        { p.BeginFigure(new Point(0, 39), true, true); p.BezierTo(new Point(23, 20), new Point(34, 48), new Point(15, 47), true, false); p.BezierTo(new Point(6, 47), new Point(3, 42), new Point(0, 39), true, false); });
        private static readonly StreamGeometry BowTails = Shape(delegate(StreamGeometryContext p)
        { p.BeginFigure(new Point(-2, 42), true, true); p.LineTo(new Point(-16, 70), true, false); p.LineTo(new Point(-6, 66), true, false); p.LineTo(new Point(-2, 74), true, false); p.LineTo(new Point(3, 43), true, false); p.BeginFigure(new Point(1, 42), true, true); p.LineTo(new Point(17, 65), true, false); p.LineTo(new Point(7, 64), true, false); p.LineTo(new Point(4, 71), true, false); p.LineTo(new Point(-3, 43), true, false); });
        private static StreamGeometry HeartFold(double radius)
        {
            return Shape(delegate(StreamGeometryContext p)
            { p.BeginFigure(new Point(-radius * .065, 0), false, false); p.BezierTo(new Point(-radius * .09, -radius * .09), new Point(radius * .07, -radius * .10), new Point(radius * .065, radius * .02), true, false); });
        }
    }
}
