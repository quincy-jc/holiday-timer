using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace HolidayTimer
{
    // Time is supplied by the host after the desktop is visible. A monitor can
    // join at any frame, and an offscreen preview can seek without a live timer.
    internal sealed class DayZeroScene : FrameworkElement
    {
        private const double Tau = Math.PI * 2;
        private const double BurstTime = 8.45;
        private const double BurstDuration = 7.4;
        private const double BurstEnd = BurstTime + BurstDuration;
        private const double DustFadeDuration = 1.6;
        private const double TitleStart = 13.0;
        // Different diffraction spikes ignite at different times. During
        // release their tails lift free of the core and run off the frame.
        private static readonly double[] JetAngle = { -90, 0, 165, -55, 71, -147, 28, 113, -20 };
        private static readonly double[] JetDelay = { .03, .25, .46, .22, .69, .82, .60, .95, 1.04 };
        private static readonly double[] JetReach = { .80, .58, .74, .57, .36, .48, .70, .43, .51 };
        private static readonly double[] JetWidth = { 5, 4, 11, 3, 4, 3, 8, 3, 3 };
        private static readonly string[] Messages = { "Today is the day!", "One more sleep!", "Excited yet?", "Getting closer!" };
        private const int Shades = 20;
        private const int Tints = 4;
        private readonly Brush[,] inks = new Brush[Tints, Shades];
        private readonly Pen[,] emberPens = new Pen[Tints, Shades];
        private readonly Color[] palette = new Color[Tints];
        private const int ForegroundStars = 630;
        private const int FieldStarCount = 1450;
        private readonly Star[] stars = new Star[FieldStarCount];
        private readonly Vector[] fieldOffsets = new Vector[FieldStarCount];
        private WriteableBitmap fieldImage;
        private byte[] fieldPixels;
        private BitmapSource sizedSky;
        private readonly DrawingGroup fieldDrawing = new DrawingGroup();
        private readonly DrawingGroup titleDrawing = new DrawingGroup();
        private double lastFieldFrame = -1;
        private readonly Star[] rays = new Star[1400];
        private BitmapSource ringBody, ringDust, titleImage, ringNumber, stageCaption;
        private enum LetteringKind { Message, Numeral, Caption }
        private FlareStamp flareFan, jetStamp, warmGlow, whiteGlow;
        private BitmapSource deepSky;
        private readonly GalaxySprite[] galaxies = new GalaxySprite[4];
        private TitleTile[] titleTiles;
        private int[] incomingGrains;
        private Point[] grainOffsets;
        private const int TileWidth = 54;
        private double lastTitleFrame = -1;
        private WriteableBitmap rayLayer;
        private DrawingGroup burstDrawing;
        private byte[] rayPixels;
        private TitleGrain[] titleGrains;
        private bool primary = true, still, configured;
        private Point origin = new Point(.5, .5);
        private Point? pointer;
        private bool pointerChanged;
        private double seconds;

        private struct Star
        {
            internal double X, Y, Depth, Phase, Drift, Speed, Delay;
            internal int Tint;
        }

        private struct TitleGrain
        {
            internal double X, Y, Reach, Theta, Delay, Weight, Light;
            internal int Tint;
        }

        private struct GalaxySprite
        {
            internal BitmapSource Image;
            internal BitmapSource Scaled;
            internal Rect Bounds;
        }

        private sealed class TitleTile
        {
            internal int X, Width, Padding;
            internal readonly List<int> Grains = new List<int>();
            internal BitmapSource Rest;
            internal bool Displaced;
            internal double MotionScale;
            internal WriteableBitmap Motion;
            internal byte[] Pixels;
        }

        internal DayZeroScene()
        {
            ClipToBounds = true;
            RenderOptions.SetBitmapScalingMode(fieldDrawing, BitmapScalingMode.NearestNeighbor);
            RenderOptions.SetBitmapScalingMode(titleDrawing, BitmapScalingMode.NearestNeighbor);
            Focusable = false;
            AutomationProperties.SetName(this, StageMessage(0));
        }

        internal static string StageMessage(int daysRemaining)
        {
            if (daysRemaining < 0 || daysRemaining >= Messages.Length) throw new ArgumentOutOfRangeException("daysRemaining");
            return Messages[daysRemaining];
        }

        internal static string StageCaption(int daysRemaining)
        {
            if (daysRemaining < 0 || daysRemaining >= Messages.Length) throw new ArgumentOutOfRangeException("daysRemaining");
            if (daysRemaining == 0) return String.Empty;
            return daysRemaining.ToString(CultureInfo.CurrentCulture) + (daysRemaining == 1 ? " day to go" : " days to go");
        }

        internal void Configure(bool primary, int seed, Color accent)
        { Configure(primary, seed, accent, 0); }

        internal void Configure(bool primary, int seed, Color accent, int daysRemaining)
        {
            string message = StageMessage(daysRemaining), caption = StageCaption(daysRemaining);
            AutomationProperties.SetName(this, daysRemaining == 0 ? message : caption + ". " + message);
            this.primary = primary;
            palette[0] = Mix(Color.FromRgb(182, 206, 236), accent, .23);
            palette[1] = Mix(Color.FromRgb(255, 210, 150), accent, .22);
            palette[2] = Mix(Colors.White, accent, .41);
            palette[3] = Color.FromRgb(255, 251, 240);
            for (int k = 0; k < Tints; k++)
            {
                for (int a = 0; a < Shades; a++)
                {
                    Color c = palette[k]; c.A = (byte)(255 * a / (Shades - 1));
                    Brush brush = new SolidColorBrush(c); brush.Freeze(); inks[k, a] = brush;
                    Pen pen = new Pen(brush, .65);
                    pen.Freeze(); emberPens[k, a] = pen;
                }
            }
            Random random = new Random(seed);
            Random faintStarSeed = new Random(seed ^ 38731);
            for (int i = 0; i < stars.Length; i++)
            {
                Random source = i < ForegroundStars ? random : faintStarSeed;
                stars[i] = new Star { X = source.NextDouble(), Y = source.NextDouble(),
                    Depth = source.NextDouble() * (i < ForegroundStars ? 1 : .75), Phase = Tau * source.NextDouble(),
                    Drift = source.NextDouble(), Speed = .58 + source.NextDouble() * .97,
                    Delay = source.NextDouble(), Tint = source.Next(Tints) };
            }
            Array.Clear(fieldOffsets, 0, fieldOffsets.Length);
            lastFieldFrame = -1;
            Random raySeed = new Random(seed ^ 7319);
            for (int i = 0; i < rays.Length; i++)
            {
                double depth = raySeed.NextDouble();
                rays[i] = new Star { X = raySeed.NextDouble(), Y = raySeed.NextDouble(),
                    Depth = depth, Speed = (.17 + 1.84 * depth * depth) * (.82 + .36 * raySeed.NextDouble()),
                    Tint = raySeed.Next(Tints) };
            }
            deepSky = CreateDeepSky(seed);
            sizedSky = null;
            if (primary)
            {
                ringBody = CreateRing(random, false); ringDust = CreateRing(random, true);
                titleImage = CreateLettering(message, random, LetteringKind.Message);
                ringNumber = daysRemaining > 0 ? CreateLettering(daysRemaining.ToString(CultureInfo.InvariantCulture),
                    new Random(seed ^ 52743), LetteringKind.Numeral) : null;
                stageCaption = daysRemaining > 0 ? CreateLettering(caption, new Random(seed ^ 34019), LetteringKind.Caption) : null;
                flareFan = CreateFlareFan(); jetStamp = CreateJet();
                warmGlow = CreateFlareGlow(palette[1]); whiteGlow = CreateFlareGlow(palette[3]);
            }
            configured = true;
            InvalidateVisual();
        }

        internal void SetOrigin(Point normalizedOrigin)
        {
            origin = normalizedOrigin;
            InvalidateVisual();
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo change)
        {
            base.OnRenderSizeChanged(change);
            PrepareRaySurface(change.NewSize);
            // Prepare size-dependent textures before the opening is presented.
            if (!configured || ActualWidth < 1 || ActualHeight < 1) return;
            double scale = Math.Min(ActualWidth / 630, ActualHeight / 720);
            EnsureFieldRaster(ActualWidth / scale, ActualHeight / scale, scale);
        }

        private void PrepareRaySurface(Size size)
        {
            if (size.Width < 1 || size.Height < 1) return;
            double dpi = VisualTreeHelper.GetDpi(this).DpiScaleX;
            double ratio = Math.Min(dpi, 1920 / Math.Max(size.Width, size.Height));
            int width = Math.Max(1, (int)Math.Ceiling(size.Width * ratio));
            int height = Math.Max(1, (int)Math.Ceiling(size.Height * ratio));
            if (rayLayer == null || rayLayer.PixelWidth != width || rayLayer.PixelHeight != height)
            {
                rayPixels = new byte[4 * width * height];
                rayLayer = new WriteableBitmap(width, height, 96, 96, PixelFormats.Pbgra32, null);
            }
            double scale = Math.Min(size.Width / 630, size.Height / 720);
            burstDrawing = new DrawingGroup();
            // Line edges are already antialiased in the surface. Avoid a
            // second full-screen interpolation pass on software WPF.
            RenderOptions.SetBitmapScalingMode(burstDrawing, BitmapScalingMode.NearestNeighbor);
            burstDrawing.Children.Add(new ImageDrawing(rayLayer, new Rect(0, 0, size.Width / scale, size.Height / scale)));
        }

        internal void SetPointer(Point? localPoint)
        {
            if (pointer == localPoint) return;
            pointer = localPoint;
            pointerChanged = true;
        }

        internal bool NeedsPointerFrame { get { return pointerChanged && !still && seconds >= TitleStart; } }

        internal void SetFrame(double elapsedSeconds, bool reducedMotion)
        {
            seconds = Double.IsNaN(elapsedSeconds) || Double.IsInfinity(elapsedSeconds) ? 0 : Math.Max(0, elapsedSeconds);
            still = reducedMotion;
            pointerChanged = false;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            double actualW = ActualWidth, actualH = ActualHeight;
            if (actualW < 1 || actualH < 1) return;
            dc.DrawRectangle(Brushes.Black, null, new Rect(0, 0, actualW, actualH));
            if (!configured) Configure(true, 71, Color.FromRgb(143, 190, 255));
            double scale = Math.Min(actualW / 630, actualH / 720);
            double w = actualW / scale, h = actualH / scale;
            Point center = new Point(w * origin.X, h * origin.Y);
            dc.PushTransform(new ScaleTransform(scale, scale));
            double t = still ? 24 : seconds;
            if (primary && t < 6.75) DrawRing(dc, center, t);
            if (t > 6.05)
            {
                if (t > BurstEnd)
                {
                    using (DrawingContext field = fieldDrawing.Open()) DrawStarField(field, w, h, t, scale);
                    dc.DrawDrawing(fieldDrawing);
                }
                if (t < BurstEnd)
                {
                    DrawWarp(dc, w, h, center, t);
                }
                DrawCoolingEmbers(dc, w, h, center, t);
                if (primary && t > TitleStart)
                {
                    using (DrawingContext title = titleDrawing.Open()) DrawTitle(title, w, h, t);
                    dc.DrawDrawing(titleDrawing);
                }
            }
            dc.Pop();
        }

        private void DrawRing(DrawingContext dc, Point center, double t)
        {
            double phase = t % 1.46;
            double beat = Bell((phase - .23) / .115) + .50 * Bell((phase - .48) / .145);
            double contraction = Ease((t - 5) / 1.65);
            double size = 382 * (1 + beat * .085 * (1 - Ease((t - 4.85) / .15))) * (1 - contraction);
            if (size < .15) return;
            dc.PushTransform(new RotateTransform(Math.Sin(t * .4) * 1.4, center.X, center.Y));
            DrawCentered(dc, ringBody, center, size);
            dc.Pop();
            dc.PushOpacity(.68 + beat * .29);
            dc.PushTransform(new RotateTransform(Math.Sin(t * .35) * -3.3, center.X, center.Y));
            DrawCentered(dc, ringDust, center, size * (1.008 + beat * .039));
            dc.Pop(); dc.Pop();
            if (ringNumber != null && t < 6.35)
            {
                double height = size * .32, width = height * ringNumber.PixelWidth / ringNumber.PixelHeight;
                dc.PushOpacity(1 - Ease((t - 5.15) / 1.20));
                dc.DrawImage(ringNumber, new Rect(center.X - width * .5, center.Y - height * .5, width, height));
                dc.Pop();
            }
        }

        // Warm these premultiplied textures when the scene is configured, then
        // composite them directly onto the ray surface, including before ignition.
        private sealed class FlareStamp
        {
            internal readonly byte[] Pixels;
            internal readonly int Width, Height;
            internal FlareStamp(byte[] pixels, int width, int height, int density)
            {
                Width = width * density; Height = height * density;
                // Prefilter the smooth fan and halo once. The already-expanding
                // light can then use a single texture sample per output pixel.
                Pixels = new byte[Width * Height * 4];
                for (int y = 0; y < Height; y++)
                {
                    double sourceY = Math.Max(0, (y + .5) / density - .5);
                    int y0 = (int)sourceY, y1 = Math.Min(height - 1, y0 + 1);
                    int fy = (int)((sourceY - y0) * 256);
                    for (int x = 0; x < Width; x++)
                    {
                        double sourceX = Math.Max(0, (x + .5) / density - .5);
                        int x0 = (int)sourceX, x1 = Math.Min(width - 1, x0 + 1);
                        int fx = (int)((sourceX - x0) * 256);
                        int a = (y0 * width + x0) * 4, b = (y0 * width + x1) * 4;
                        int c = (y1 * width + x0) * 4, e = (y1 * width + x1) * 4;
                        int d = (y * Width + x) * 4;
                        for (int k = 0; k < 4; k++)
                            Pixels[d + k] = (byte)((((256 - fx) * pixels[a + k] + fx * pixels[b + k]) * (256 - fy) +
                                ((256 - fx) * pixels[c + k] + fx * pixels[e + k]) * fy + 32768) >> 16);
                    }
                }
            }
        }

        private void BlendFlare(byte[] dest, int width, int height, Point center, double w, double h, double t)
        {
            double build = Ease((t - 6.82) / (BurstTime - 6.82));
            double after = Math.Max(0, t - BurstTime);
            double light = Ease((t - 6.05) / .64) * (1 - Ease(after / 1.78));
            if (light < .002) return;
            double sx = width / w, sy = height / h, cx = center.X * sx, cy = center.Y * sy;
            double expansion = 1 + 1.65 * after, strength = build * build;
            double halo = (40 + build * 78) * expansion;
            Stamp(dest, width, height, warmGlow, cx, cy, sx * halo, sy * halo, 1, 0, light);
            if (strength > 0)
            {
                double radius = (180 + Math.Min(w * 1.25, h * 1.72) * strength) * expansion * .5;
                Stamp(dest, width, height, flareFan, cx, cy, sx * radius, sy * radius, 1, 0, strength * light);
                double span = Math.Max(w, h);
                for (int i = 0; i < JetAngle.Length; i++)
                {
                    double start = 6.82 + JetDelay[i];
                    double formed = Ease((t - start) / (BurstTime - start));
                    if (formed <= 0) continue;
                    double length = span * JetReach[i] * (formed + after * (.75 + JetReach[i]));
                    double tail = span * JetReach[i] * after * (.48 + JetReach[i]);
                    double halfWidth = JetWidth[i] * (.26 + .74 * formed) * (1 + after * .25);
                    double angle = JetAngle[i] * Math.PI / 180, cos = Math.Cos(angle), sin = Math.Sin(angle);
                    double middle = .5 * (tail + length);
                    Stamp(dest, width, height, jetStamp, cx + sx * middle * cos, cy + sy * middle * sin,
                        sx * .5 * (length - tail), sy * halfWidth, cos, sin, light);
                }
                Stamp(dest, width, height, whiteGlow, cx, cy, sx * (8 + 54 * strength) * expansion,
                    sy * (8 + 48 * strength) * expansion, 1, 0, light);
            }
            double core = (1.8 + 4.5 * build) * expansion;
            halo = (8 + 18 * build) * expansion;
            Stamp(dest, width, height, whiteGlow, cx, cy, sx * halo, sy * halo, 1, 0, light);
            Stamp(dest, width, height, whiteGlow, cx, cy, sx * core * 2, sy * core * 2, 1, 0, light);
        }

        // Scan just the visible strip of the rotated filament (or the bounds of a halo).
        private static void Stamp(byte[] dest, int width, int height, FlareStamp stamp, double cx, double cy,
            double rx, double ry, double cos, double sin, double light)
        {
            int opacity = (int)(light * 256 + .5);
            if (opacity == 0 || rx <= 0 || ry <= 0) return;
            double extentY = Math.Abs(rx * sin) + Math.Abs(ry * cos);
            int top = Math.Max(0, (int)Math.Floor(cy - extentY)), bottom = Math.Min(height, (int)Math.Ceiling(cy + extentY));
            double scaleX = .5 * stamp.Width / rx, scaleY = .5 * stamp.Height / ry;
            int stepU = (int)Math.Round(cos * scaleX * 65536), stepV = (int)Math.Round(-sin * scaleY * 65536);
            byte[] pixels = stamp.Pixels;
            for (int y = top; y < bottom; y++)
            {
                double dy = y + .5 - cy, left = -cx, right = width - cx;
                if (!ClipStampRow(cos, sin * dy, rx, ref left, ref right) ||
                    !ClipStampRow(-sin, cos * dy, ry, ref left, ref right)) continue;
                int first = Math.Max(0, (int)Math.Ceiling(cx + left - .5));
                int end = Math.Min(width, (int)Math.Ceiling(cx + right - .5));
                double dx = first + .5 - cx;
                int u = (int)(((cos * dx + sin * dy + rx) * scaleX) * 65536);
                int v = (int)(((-sin * dx + cos * dy + ry) * scaleY) * 65536);
                for (int x = first, d = (y * width + first) * 4; x < end; x++, d += 4, u += stepU, v += stepV)
                {
                    int tx = u >> 16, ty = v >> 16;
                    if ((uint)tx >= (uint)stamp.Width || (uint)ty >= (uint)stamp.Height) continue;
                    int p = (ty * stamp.Width + tx) * 4;
                    int alpha = (pixels[p + 3] * opacity + 128) >> 8;
                    if (alpha == 0) continue;
                    if (dest[d + 3] == 0)
                    {
                        dest[d] = (byte)((pixels[p] * opacity + 128) >> 8);
                        dest[d + 1] = (byte)((pixels[p + 1] * opacity + 128) >> 8);
                        dest[d + 2] = (byte)((pixels[p + 2] * opacity + 128) >> 8);
                        dest[d + 3] = (byte)alpha;
                        continue;
                    }
                    int keep = 255 - alpha;
                    dest[d] = (byte)(((pixels[p] * opacity + 128) >> 8) + (dest[d] * keep + 127) / 255);
                    dest[d + 1] = (byte)(((pixels[p + 1] * opacity + 128) >> 8) + (dest[d + 1] * keep + 127) / 255);
                    dest[d + 2] = (byte)(((pixels[p + 2] * opacity + 128) >> 8) + (dest[d + 2] * keep + 127) / 255);
                    dest[d + 3] = (byte)(alpha + (dest[d + 3] * keep + 127) / 255);
                }
            }
        }

        private static bool ClipStampRow(double coefficient, double offset, double half, ref double left, ref double right)
        {
            if (Math.Abs(coefficient) < 1e-8) return Math.Abs(offset) <= half;
            double a = (-half - offset) / coefficient, b = (half - offset) / coefficient;
            left = Math.Max(left, Math.Min(a, b)); right = Math.Min(right, Math.Max(a, b));
            return right > left;
        }

        private static FlareStamp CreateFlareGlow(Color color)
        {
            const int size = 96;
            byte[] pixels = new byte[size * size * 4];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                double dx = (2 * x + 1.0) / size - 1, dy = (2 * y + 1.0) / size - 1;
                double r = Math.Sqrt(dx * dx + dy * dy);
                double a = r < .13 ? 1 - .475 * r / .13 : r < .36 ? .525 - .356 * (r - .13) / .23 :
                    r < .7 ? .169 - .134 * (r - .36) / .34 : Math.Max(0, .035 * (1 - r) / .3);
                int p = (y * size + x) * 4;
                pixels[p] = (byte)(color.B * a); pixels[p + 1] = (byte)(color.G * a);
                pixels[p + 2] = (byte)(color.R * a); pixels[p + 3] = (byte)(255 * a);
            }
            return new FlareStamp(pixels, size, size, 4);
        }

        private void DrawWarp(DrawingContext dc, double w, double h, Point center, double t)
        {
            double age = t - BurstTime;
            if (rayLayer == null || (age <= 0 && !primary)) return;
            int width = rayLayer.PixelWidth, height = rayLayer.PixelHeight;
            double scale = width / w;
            double cx = center.X * scale, cy = center.Y * height / h;
            Array.Clear(rayPixels, 0, rayPixels.Length);
            double fade = 1 - Ease((age - 3.8) / (BurstDuration - 3.8));
            // Composite the entire shell once; a WPF opacity layer per ray is
            // particularly costly where the ray heads overlap at ignition.
            for (int i = 0; age > 0 && i < rays.Length; i++)
            {
                Star s = rays[i];
                double flight = age * s.Speed;
                double travel = flight * (.50 + .36 * flight);
                double dx = s.X * width - cx, dy = s.Y * height - cy;
                double x = cx + dx * travel, y = cy + dy * travel;
                if (x < -80 * scale || y < -80 * scale || x > width + 80 * scale || y > height + 80 * scale) continue;
                double tail = travel * (.08 + .22 * s.Depth) * Math.Min(1, flight * 3);
                double light = fade * (.32 + .68 * s.Depth) * Math.Min(1, flight * 4);
                int breadth = s.Depth > .89 ? 3 : s.Depth > .63 ? 2 : 1;
                double inkWidth = (.65 + breadth * breadth * .54) * scale;
                double coreWidth = (.65 + (breadth-1) * (breadth-1) * .54) * scale;
                RayStroke(width, height, x - dx * tail, y - dy * tail, x, y,
                    inkWidth, light * .65, palette[s.Tint]);
                RayStroke(width, height, x - dx * tail * .69, y - dy * tail * .69, x, y,
                    coreWidth, light, palette[3]);
                if (s.Depth > .91) RayGlow(width, height, x, y, (2.8 + s.Depth) * scale,
                    fade * Math.Min(1, flight * 4), palette[s.Tint]);
            }
            if (primary && t < BurstTime + 1.78) BlendFlare(rayPixels, width, height, center, w, h, t);
            rayLayer.WritePixels(new Int32Rect(0, 0, width, height), rayPixels, width * 4, 0);
            dc.DrawDrawing(burstDrawing);
        }

        // Scan across the major axis so steep, long trails touch only their
        // narrow strip of pixels. Fractional overlap antialiases both edges.
        private void RayStroke(int width, int height, double x0, double y0, double x1, double y1,
            double thickness, double light, Color color)
        {
            bool steep = Math.Abs(y1-y0) > Math.Abs(x1-x0);
            double u0 = steep ? y0 : x0, v0 = steep ? x0 : y0;
            double u1 = steep ? y1 : x1, v1 = steep ? x1 : y1;
            double length = u1 - u0;
            if (Math.Abs(length) < .01 || light < .003) return;
            double slope = (v1 - v0) / length;
            double half = thickness * .5 * Math.Sqrt(1 + slope * slope);
            int major = steep ? height : width, minor = steep ? width : height;
            double left = Math.Min(u0,u1), right = Math.Max(u0,u1);
            int first = Math.Max(0,(int)Math.Floor(left)), last = Math.Min(major-1,(int)Math.Floor(right));
            double v = v0 + (first + .5 - u0) * slope;
            for (int u = first; u <= last; u++, v += slope)
            {
                double along = Math.Min(u+1.0,right) - Math.Max(u,left);
                int lo = Math.Max(0,(int)Math.Floor(v-half)), hi = Math.Min(minor-1,(int)Math.Floor(v+half));
                for (int k = lo; k <= hi; k++)
                {
                    double across = Math.Min(k+1.0,v+half) - Math.Max(k,v-half);
                    int alpha = (int)(255 * light * along * across);
                    if (alpha > 0) BlendRay(4 * (steep ? u * width + k : k * width + u), color, alpha);
                }
            }
        }

        private void RayGlow(int width, int height, double x, double y, double radius, double light, Color color)
        {
            double inverse = 1 / (radius * radius);
            int left = Math.Max(0,(int)(x-radius)), right = Math.Min(width-1,(int)(x+radius));
            int top = Math.Max(0,(int)(y-radius)), bottom = Math.Min(height-1,(int)(y+radius));
            for (int row=top; row<=bottom; row++) for (int col=left;col<=right;col++)
            {
                double dx = col+.5-x, dy = row+.5-y;
                double falloff = 1 - (dx*dx + dy*dy) * inverse;
                if (falloff <= 0) continue;
                double squared = falloff * falloff;
                int alpha = (int)(255 * light * squared * squared * squared);
                if (alpha > 0) BlendRay(4*(row*width+col), color, alpha);
            }
        }

        private void BlendRay(int index, Color color, int alpha)
        {
            int behind = 255 - alpha;
            rayPixels[index] = (byte)((color.B * alpha + rayPixels[index] * behind + 127) / 255);
            rayPixels[index+1] = (byte)((color.G * alpha + rayPixels[index+1] * behind + 127) / 255);
            rayPixels[index+2] = (byte)((color.R * alpha + rayPixels[index+2] * behind + 127) / 255);
            rayPixels[index+3] = (byte)(alpha + (rayPixels[index+3] * behind + 127) / 255);
        }

        // Fired with the other rays, a few distant embers slow into the first
        // points of the new sky. Their heads never reset at the scene boundary.
        private void DrawCoolingEmbers(DrawingContext dc, double w, double h, Point center, double t)
        {
            double age = t - BurstTime, settled = t - BurstEnd;
            if (age <= 0 || settled >= DustFadeDuration) return;
            double progress = Clamp(age / BurstDuration);
            double radius = 1 - (1 - progress) * (1 - progress);
            double trail = .085 * progress * (1 - progress);
            double opacity = Ease(age / .65) * (1 - Ease(settled / DustFadeDuration));
            for (int i = 0; i < ForegroundStars; i += 11)
            {
                Star s = stars[i];
                Vector path = FieldStarPosition(s, w, h, age) - center;
                Point head = center + path * radius;
                if (head.X < -8 || head.Y < -8 || head.X > w + 8 || head.Y > h + 8) continue;
                double light = (.26 + .44 * s.Depth) * opacity;
                dc.DrawLine(emberPens[s.Tint, Shade(light * .6)], head - path * trail, head);
                double size = .6 + s.Depth * .78;
                dc.DrawEllipse(inks[s.Tint, Shade(light)], null, head, size, size);
            }
        }

        private static Point FieldStarPosition(Star s, double w, double h, double age)
        {
            return new Point(s.X * w + Math.Sin(s.Phase + age * .09) * (1 + s.Depth * 7),
                s.Y * h + Math.Cos(s.Phase + age * .073) * (1 + s.Depth * 6));
        }

        private void DrawStarField(DrawingContext dc, double w, double h, double t, double scale)
        {
            double settled = t - BurstEnd;
            double density = EnsureFieldRaster(w, h, scale);
            dc.PushOpacity(Ease((settled - .12) / 2.48));
            dc.DrawImage(sizedSky, new Rect(0, 0, w, h));
            dc.Pop();
            double drift = t - BurstTime;
            dc.PushOpacity(Ease((settled - .40) / 2.50));
            for (int i = 0; i < galaxies.Length; i++)
            {
                GalaxySprite galaxy = galaxies[i];
                Rect bounds = galaxy.Bounds;
                double x = (3.5 + i * .25) * Math.Sin(drift * (.076 + i * .012) + i * 1.71);
                double y = (2.4 + i * .16) * Math.Cos(drift * (.061 + i * .009) + i * 2.13);
                dc.DrawImage(galaxy.Scaled, new Rect(bounds.X * w + x / scale, bounds.Y * h + y / scale,
                    bounds.Width * w, bounds.Height * h));
            }
            dc.Pop();
            int pixelWidth = fieldImage.PixelWidth, pixelHeight = fieldImage.PixelHeight;
            Array.Clear(fieldPixels, 0, fieldPixels.Length);
            Point? mouse = still || !pointer.HasValue ? (Point?)null :
                new Point(pointer.Value.X / scale, pointer.Value.Y / scale);
            if (still || t < lastFieldFrame) Array.Clear(fieldOffsets, 0, fieldOffsets.Length);
            double delta = lastFieldFrame < 0 ? 1.0 / 30 : Math.Max(0, Math.Min(t - lastFieldFrame, .12));
            double catchup = 1 - Math.Exp(-delta * (mouse.HasValue ? 16 : 9));
            lastFieldFrame = t;
            const double reach = 157;
            for (int i = 0; i < stars.Length; i++)
            {
                Star s = stars[i];
                Point pos = FieldStarPosition(s, w, h, drift);
                Vector target = new Vector();
                if (mouse.HasValue)
                {
                    Vector toward = mouse.Value - pos;
                    if (toward.LengthSquared < reach * reach)
                        target = toward * (Ease(1 - toward.Length / reach) * (.36 + .47 * s.Depth));
                }
                Vector offset = fieldOffsets[i];
                if (target.LengthSquared > 0 || offset.LengthSquared > 0)
                {
                    offset += (target - offset) * catchup;
                    if (offset.LengthSquared < .01 && target.LengthSquared < .01) offset = new Vector();
                    fieldOffsets[i] = offset;
                    pos += offset;
                }
                double attracted = Math.Min(1, Math.Sqrt(offset.LengthSquared) / 30);
                // Each star has its own rise time and phase. Only a few show a
                // stronger glint, so the settled sky never pulses as a whole.
                double glimmer = .50 + .33 * Math.Sin(drift * (.51 + s.Drift * .72) + s.Phase)
                    + .17 * Math.Sin(drift * (.76 + s.Speed * .47) + 1.73 * s.Phase);
                bool scintillates = i % 13 == 0;
                double glint = scintillates ? glimmer * glimmer * glimmer : 0;
                double light = .12 + .44 * s.Depth + (.18 + .22 * s.Depth) * glimmer + .30 * glint + attracted * .4;
                double rad = .35 + s.Depth * s.Depth * 1.1;
                if (i >= ForegroundStars)
                {
                    // The deeper stars arrive gradually and stay fainter than the foreground.
                    double reveal = Ease((settled - s.Delay * 1.6) / 1.5);
                    light *= .80 * reveal;
                    glint *= reveal;
                    rad *= .90;
                }
                if (s.Depth > .967 || attracted > .28 || glint > .4)
                {
                    double halo = Clamp(.18 + .56 * glimmer + .34 * glint + .65 * attracted);
                    SplatFieldHalo(fieldPixels, pixelWidth, pixelHeight, pos.X * density, pos.Y * density,
                        rad * density * 4, palette[s.Tint], halo);
                }
                Splat(fieldPixels, pixelWidth, pixelHeight, pos.X * density, pos.Y * density,
                    rad * density, palette[s.Tint], Clamp(light));
            }
            fieldImage.WritePixels(new Int32Rect(0, 0, pixelWidth, pixelHeight), fieldPixels, pixelWidth * 4, 0);
            dc.PushOpacity(Ease(settled / DustFadeDuration));
            dc.DrawImage(fieldImage, new Rect(0, 0, w, h));
            dc.Pop();
        }

        private double EnsureFieldRaster(double w, double h, double scale)
        {
            double density = Math.Min(scale * VisualTreeHelper.GetDpi(this).DpiScaleX,
                Math.Min(1920 / w, 1080 / h));
            int width = Math.Max(1, (int)Math.Round(w * density)), height = Math.Max(1, (int)Math.Round(h * density));
            if (sizedSky != null && fieldImage.PixelWidth == width && fieldImage.PixelHeight == height) return density;
            fieldImage = new WriteableBitmap(width, height, 96, 96, PixelFormats.Pbgra32, null);
            fieldPixels = new byte[width * height * 4];
            sizedSky = ScaleFieldImage(deepSky, width, height);
            for (int i = 0; i < galaxies.Length; i++)
            {
                GalaxySprite galaxy = galaxies[i];
                galaxy.Scaled = ScaleFieldImage(galaxy.Image, Math.Max(1, (int)Math.Round(galaxy.Bounds.Width * width)),
                    Math.Max(1, (int)Math.Round(galaxy.Bounds.Height * height)));
                galaxies[i] = galaxy;
            }
            return density;
        }

        // Resize the static artwork once, with interpolation, so its diffuse edges stay smooth
        // without making the software compositor resample the full sky on every frame.
        private static BitmapSource ScaleFieldImage(BitmapSource source, int width, int height)
        {
            if (width == source.PixelWidth && height == source.PixelHeight) return source;
            var scaled = new TransformedBitmap(source, new ScaleTransform((double)width / source.PixelWidth,
                (double)height / source.PixelHeight));
            int stride = scaled.PixelWidth * 4;
            byte[] pixels = new byte[stride * scaled.PixelHeight];
            scaled.CopyPixels(pixels, stride, 0);
            BitmapSource copy = BitmapSource.Create(scaled.PixelWidth, scaled.PixelHeight, 96, 96, PixelFormats.Pbgra32, null, pixels, stride);
            copy.Freeze();
            return copy;
        }

        private static void SplatFieldHalo(byte[] pixels, int w, int h, double x, double y, double radius,
            Color colour, double light)
        {
            int left = Math.Max(0, (int)(x - radius)), right = Math.Min(w - 1, (int)(x + radius));
            int top = Math.Max(0, (int)(y - radius)), bottom = Math.Min(h - 1, (int)(y + radius));
            double invRadius = 1 / radius;
            for (int py = top; py <= bottom; py++) for (int px = left; px <= right; px++)
            {
                double dx = px + .5 - x, dy = py + .5 - y;
                double falloff = 1 - Math.Sqrt(dx * dx + dy * dy) * invRadius;
                if (falloff <= 0) continue;
                double squared = falloff * falloff, alpha = light * squared * squared;
                int n = 4 * (py * w + px);
                pixels[n] = (byte)(pixels[n] * (1 - alpha) + colour.B * alpha);
                pixels[n + 1] = (byte)(pixels[n + 1] * (1 - alpha) + colour.G * alpha);
                pixels[n + 2] = (byte)(pixels[n + 2] * (1 - alpha) + colour.R * alpha);
                pixels[n + 3] = (byte)(pixels[n + 3] * (1 - alpha) + 255 * alpha);
            }
        }

        // Bake the fixed dust lanes separately from the four translating galaxies.
        private BitmapSource CreateDeepSky(int seed)
        {
            const int cw = 320, ch = 200, width = 960, height = 600;
            byte[] cloud = new byte[(cw + 1) * (ch + 1) * 3];
            byte[][] galaxyPixels = new byte[galaxies.Length][];
            for (int i = 0; i < galaxyPixels.Length; i++) galaxyPixels[i] = new byte[(cw + 1) * (ch + 1) * 4];
            double phase = (seed % 97) * .028;
            for (int y = 0; y <= ch; y++) for (int x = 0; x <= cw; x++)
            {
                double u = x / (double)cw, v = y / (double)ch;
                double body = 2 * SkyNoise(u * 12, v * 10, seed) - 1;
                double detail = 2 * SkyNoise(u * 35, v * 33, seed + 3) - 1;
                double fleck = 2 * SkyNoise(u * 91, v * 68, seed + 9) - 1;
                double dust = .56 * body + .31 * detail + .13 * fleck;
                double spine = .84 - .73 * u + .047 * Math.Sin(u * 7 + phase);
                double d = (v - spine + .032 * body) / (.105 + .03 * Math.Sin(5 * u));
                double band = Math.Exp(-d * d * 1.7) * (.92 + .86 * dust);
                // Dark, branching absorption lanes cut across the bright central band.
                double lane = Math.Exp(-Math.Pow((d - .18 * Math.Sin(u * 17) - .21 * body - .12 * detail) * 5, 2));
                double branch = Math.Exp(-Math.Pow((d + .32 - .39 * Math.Sin(u * 12) + .18 * dust) * 8, 2));
                band *= (1 - .70 * lane) * (1 - .38 * branch);
                double warm = Math.Exp(-Math.Pow((u - .20) * 4, 2));
                double red = 2 + band * (30 + 23 * warm);
                double green = 3 + band * (32 + 11 * warm);
                double blue = 8 + band * (48 - 8 * warm);

                // Each sprite captures its own dust and colour at its original orientation.
                int pixel = 4 * (y * (cw + 1) + x);
                double gr = 0, gg = 0, gb = 0;
                double gx = (u - .185) * 1.6, gy = v - .22;
                AddDistantGalaxy((gx * .94 + gy * .34) / .173, (gy * .94 - gx * .34) / .10,
                    body, dust, .20, ref gr, ref gg, ref gb);
                SetGalaxyPixel(galaxyPixels[0], pixel, gr, gg, gb);
                gr = gg = gb = 0;
                gx = (u - .775) * 1.6; gy = v - .21;
                AddDistantGalaxy((gx * .91 - gy * .415) / .145, (gx * .415 + gy * .91) / .055,
                    body, dust, .82, ref gr, ref gg, ref gb);
                SetGalaxyPixel(galaxyPixels[1], pixel, gr, gg, gb);

                // An edge-on disk has an amber bulge bisected by a narrow dust lane.
                gx = (u - .24) * 1.6; gy = v - .825;
                double along = (gx * .97 + gy * .24) / .179;
                double across = (gy * .97 - gx * .24) / .030;
                double diskRadius = along * along + across * across;
                if (diskRadius < 9)
                {
                    double disk = Math.Exp(-1.18 * diskRadius);
                    double bulge = Math.Exp(-12 * along * along - .74 * across * across);
                    double laneDepth = across + .07 + body * .13;
                    double laneLight = 1 - .78 * Math.Exp(-laneDepth * laneDepth * 49);
                    double intensity = (25 * disk + 57 * bulge) * laneLight;
                    SetGalaxyPixel(galaxyPixels[2], pixel, intensity, intensity * .74, intensity * .60);
                }

                // Small, distant companion in the gap between the disk and the nebula.
                gx = (u - .568) * 1.6; gy = v - .869;
                double companionRadius = Math.Pow((gx * .81 - gy * .59) / .053, 2)
                    + Math.Pow((gx * .59 + gy * .81) / .026, 2);
                if (companionRadius < 7)
                {
                    double light = 25 * Math.Exp(-1.25 * companionRadius) + 32 * Math.Exp(-9 * companionRadius);
                    SetGalaxyPixel(galaxyPixels[3], pixel, light * .91, light * .81, light);
                }

                // A cloud of ionised gas, with a ragged illuminated rim around a dark cavity.
                double nx = (u - .83) / .165, ny = (v - .77) / .13;
                double nr = Math.Sqrt(nx * nx + ny * ny);
                if (nr < 2.1)
                {
                    double distort = .31 * body + .12 * detail + .20 * Math.Sin(3 * Math.Atan2(ny, nx));
                    double rim = Math.Exp(-Math.Pow((nr - .76 - distort) / .22, 2));
                    double gas = Math.Exp(-nr * nr * 1.1) * (.23 + .22 * dust) + .87 * rim * (.55 + .48 * dust);
                    double filament = rim * rim * (.34 + .25 * detail + .12 * fleck);
                    red += 45 * gas + 26 * filament;
                    green += 22 * gas + 15 * filament;
                    blue += 54 * gas + 29 * filament;
                }
                int n = (y * (cw + 1) + x) * 3;
                cloud[n] = (byte)Math.Min(255, blue); cloud[n + 1] = (byte)Math.Min(255, green);
                cloud[n + 2] = (byte)Math.Min(255, red);
            }
            for (int i = 0; i < galaxies.Length; i++) galaxies[i] = CropGalaxy(galaxyPixels[i], cw + 1, ch + 1);

            byte[] pixels = new byte[width * height * 4];
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                int p = (y * width + x) * 4, s = (y / 3 * (cw + 1) + x / 3) * 3;
                int fx = x % 3, fy = y % 3, row = (cw + 1) * 3;
                for (int c = 0; c < 3; c++, s++)
                    pixels[p + c] = (byte)(((3 - fx) * (3 - fy) * cloud[s] + fx * (3 - fy) * cloud[s + 3] +
                        (3 - fx) * fy * cloud[s + row] + fx * fy * cloud[s + row + 3]) / 9);
                pixels[p + 3] = 255;
            }
            Random random = new Random(seed ^ 87179);
            for (int i = 0; i < 1150; i++)
            {
                double u = random.NextDouble();
                double v = i % 3 == 0 ? .84 - .73 * u + .047 * Math.Sin(u * 7 + phase) + .06 * Gaussian(random) :
                    random.NextDouble();
                if (v <= 0 || v >= 1) continue;
                double brightness = .18 + .59 * random.NextDouble();
                double radius = .43 + .68 * random.NextDouble();
                Splat(pixels, width, height, u * width, v * height, radius, palette[random.Next(Tints)], brightness);
            }
            return Image(pixels, width, height);
        }

        private static void SetGalaxyPixel(byte[] pixels, int index, double red, double green, double blue)
        {
            pixels[index] = (byte)blue; pixels[index + 1] = (byte)green; pixels[index + 2] = (byte)red;
            pixels[index + 3] = Math.Max(pixels[index], Math.Max(pixels[index + 1], pixels[index + 2]));
        }

        private static GalaxySprite CropGalaxy(byte[] pixels, int width, int height)
        {
            int left = width, top = height, right = -1, bottom = -1;
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                if (pixels[4 * (y * width + x) + 3] > 0)
                {
                    left = Math.Min(left, x); right = Math.Max(right, x);
                    top = Math.Min(top, y); bottom = Math.Max(bottom, y);
                }
            left = Math.Max(0, left - 1); top = Math.Max(0, top - 1);
            right = Math.Min(width - 1, right + 1); bottom = Math.Min(height - 1, bottom + 1);
            Int32Rect tile = new Int32Rect(left, top, right - left + 1, bottom - top + 1);
            BitmapSource image = new CroppedBitmap(Image(pixels, width, height), tile);
            image.Freeze();
            return new GalaxySprite { Image = image,
                Bounds = new Rect((left - .5) / (width - 1), (top - .5) / (height - 1),
                    tile.Width / (double)(width - 1), tile.Height / (double)(height - 1)) };
        }

        private static void AddDistantGalaxy(double x, double y, double noise, double dust, double warmth,
            ref double red, ref double green, ref double blue)
        {
            double r2 = x * x + y * y;
            if (r2 > 6.5) return;
            double lane = y + .14 + .22 * x * x + .12 * noise;
            double disk = Math.Exp(-r2 * 1.42) * (1 + .40 * dust) * (1 - .32 * Math.Exp(-lane * lane * 28));
            double core = 47 * Math.Exp(-r2 * 15);
            red += (26 + 9 * warmth) * disk + core;
            green += 29 * disk + .89 * core;
            blue += (39 - 7 * warmth) * disk + .78 * core;
        }

        private static double SkyNoise(double x, double y, int seed)
        {
            int ix = (int)Math.Floor(x), iy = (int)Math.Floor(y);
            double fx = Ease(x - ix), fy = Ease(y - iy);
            double a = SkyHash(ix, iy, seed), b = SkyHash(ix + 1, iy, seed);
            double c = SkyHash(ix, iy + 1, seed), d = SkyHash(ix + 1, iy + 1, seed);
            return (a + (b - a) * fx) * (1 - fy) + (c + (d - c) * fx) * fy;
        }

        private static double SkyHash(int x, int y, int seed)
        {
            uint n = unchecked((uint)(x * 374761393 + y * 668265263 + seed));
            n = unchecked((n ^ (n >> 13)) * 1274126177u);
            return ((n ^ (n >> 16)) & 65535) / 65535.0;
        }

        private void DrawTitle(DrawingContext dc, double w, double h, double t)
        {
            double size = Math.Min(w * .72 / titleImage.PixelWidth, h * .123 / titleImage.PixelHeight);
            double left = (w - titleImage.PixelWidth * size) / 2;
            double top = (h - titleImage.PixelHeight * size) / 2;
            Rect bounds = new Rect(left, top, titleImage.PixelWidth * size, titleImage.PixelHeight * size);
            if (stageCaption != null)
            {
                double opacity = Ease((t - TitleStart - .55) / 1.4);
                if (opacity > 0)
                {
                    dc.PushOpacity(opacity);
                    double width = stageCaption.PixelWidth * .5, height = stageCaption.PixelHeight * .5;
                    dc.DrawImage(stageCaption, new Rect((w - width) * .5, top - height - 15, width, height));
                    dc.Pop();
                }
            }
            bool moving = UpdateTitleDust(w, bounds, size, t);
            double settled = Ease((t - TitleStart - 2.15) / 1.85);
            if (settled > 0)
            {
                dc.PushOpacity(settled);
                if (!moving) dc.DrawImage(titleImage, bounds);
                else foreach (TitleTile tile in titleTiles)
                {
                    BitmapSource image = tile.Displaced ? (BitmapSource)tile.Motion : tile.Rest;
                    int pad = tile.Displaced ? tile.Padding : 2;
                    dc.DrawImage(image, new Rect(left + (tile.X - pad) * size, top - pad * size,
                        image.PixelWidth * size, image.PixelHeight * size));
                }
                dc.Pop();
            }
            if (settled >= 1) return;
            dc.PushOpacity(1 - settled);
            foreach (int index in incomingGrains)
            {
                TitleGrain g = titleGrains[index];
                double arrival = Clamp((t - TitleStart - g.Delay * 1.16) / 2.35);
                if (arrival <= 0) continue;
                double spread = 1 - Ease(arrival);
                double angle = g.Theta + spread * .52;
                double distance = g.Reach * spread;
                Point offset = grainOffsets[index];
                Point pos = new Point(left + g.X * size + distance * Math.Cos(angle) + offset.X,
                                      top + g.Y * size + distance * Math.Sin(angle) + offset.Y);
                double r = Math.Max(.36, size * g.Weight);
                dc.DrawEllipse(inks[g.Tint, Shade((.24 + .73 * arrival) * Math.Min(1, arrival * 4))], null, pos, r, r);
            }
            dc.Pop();
        }

        private bool UpdateTitleDust(double w, Rect bounds, double size, double t)
        {
            if (still || t < lastTitleFrame)
            {
                Array.Clear(grainOffsets, 0, grainOffsets.Length);
                foreach (TitleTile tile in titleTiles) tile.Displaced = false;
            }
            double delta = lastTitleFrame < 0 ? 1.0 / 30 : Math.Min(.12, Math.Max(0, t - lastTitleFrame));
            lastTitleFrame = t;
            const double radius = 100;
            double catchup = 1 - Math.Exp(-delta * (pointer.HasValue ? 18 : 11));
            Point? mouse = !still && pointer.HasValue ? (Point?)new Point(pointer.Value.X * w / ActualWidth - bounds.Left,
                pointer.Value.Y * w / ActualWidth - bounds.Top) : null;
            bool anyMoving = false;
            foreach (TitleTile tile in titleTiles)
            {
                bool near = mouse.HasValue && mouse.Value.X >= tile.X * size - radius &&
                    mouse.Value.X <= (tile.X + tile.Width) * size + radius &&
                    mouse.Value.Y >= -radius && mouse.Value.Y <= bounds.Height + radius;
                if (!near && !tile.Displaced) continue;
                bool repaint = tile.Motion == null || tile.MotionScale != size;
                tile.Displaced = false;
                foreach (int index in tile.Grains)
                {
                    TitleGrain g = titleGrains[index];
                    double dx = 0, dy = 0;
                    if (near)
                    {
                        double mx = mouse.Value.X - g.X * size, my = mouse.Value.Y - g.Y * size;
                        double distance2 = mx * mx + my * my;
                        if (distance2 < radius * radius)
                        {
                            double pull = (1 - Ease((Math.Sqrt(distance2) / radius - .28) / .72)) * (.96 + g.Delay * .08);
                            dx = mx * pull;
                            dy = (my - 2 * g.Weight) * pull;
                        }
                    }
                    Point last = grainOffsets[index];
                    Point offset = new Point(last.X + (dx - last.X) * catchup, last.Y + (dy - last.Y) * catchup);
                    if (Math.Abs(offset.X - dx) + Math.Abs(offset.Y - dy) < .08)
                        offset = new Point(dx, dy);
                    if (Math.Abs(offset.X) + Math.Abs(offset.Y) <= .10) offset = new Point();
                    else tile.Displaced = true;
                    if (last != offset) repaint = true;
                    grainOffsets[index] = offset;
                }
                if (tile.Displaced)
                {
                    if (repaint) PaintDisplacedTile(tile, size, radius);
                    anyMoving = true;
                }
            }
            return anyMoving;
        }

        private void PaintDisplacedTile(TitleTile tile, double size, double radius)
        {
            tile.MotionScale = size;
            // The strongest pull is less than half the influence radius. Padding
            // lets grains leave their home tile and even the top/bottom of a letter.
            int pad = 2 + (int)Math.Ceiling(.5 * radius / size);
            if (tile.Motion == null || tile.Padding < pad)
            {
                tile.Padding = pad;
                tile.Motion = new WriteableBitmap(tile.Width + 2 * pad, titleImage.PixelHeight + 2 * pad,
                    96, 96, PixelFormats.Pbgra32, null);
                tile.Pixels = new byte[tile.Motion.PixelWidth * tile.Motion.PixelHeight * 4];
            }
            Array.Clear(tile.Pixels, 0, tile.Pixels.Length);
            foreach (int index in tile.Grains)
            {
                TitleGrain g = titleGrains[index]; Point offset = grainOffsets[index];
                Splat(tile.Pixels, tile.Motion.PixelWidth, tile.Motion.PixelHeight,
                    g.X - tile.X + tile.Padding + offset.X / size, g.Y + tile.Padding + offset.Y / size,
                    g.Weight, palette[g.Tint], g.Light);
            }
            tile.Motion.WritePixels(new Int32Rect(0, 0, tile.Motion.PixelWidth, tile.Motion.PixelHeight),
                tile.Pixels, tile.Motion.PixelWidth * 4, 0);
        }

        // Rasterise two seeded dust bands once. The heart beat moves the soft
        // outer dust slightly farther than the dense, sharply resolved core.
        private BitmapSource CreateRing(Random random, bool outer)
        {
            const int side = 660;
            byte[] pixels = new byte[side * side * 4];
            int count = outer ? 10500 : 23500;
            for (int i = 0; i < count; i++)
            {
                double angle = random.NextDouble() * Tau;
                double width = 22 + 5.7 * Math.Sin(3 * angle + .8) + 3.5 * Math.Cos(5 * angle);
                double noise = Gaussian(random);
                double ridge = 233 + 3 * Math.Sin(angle * 4 + .7) + 1.9 * Math.Cos(angle * 7);
                double r = ridge + noise * (outer ? width * 2.05 : width);
                double light = (outer ? .56 : .76) * (.34 + random.NextDouble() * .66);
                int tint = random.Next(8) < 6 ? 0 : (random.Next(2) == 0 ? 2 : 3);
                double dot = .51 + random.NextDouble() * .87;
                Splat(pixels, side, side, side * .5 + Math.Cos(angle) * r,
                    side * .5 + Math.Sin(angle) * r, dot, palette[tint], light);
            }
            return Image(pixels, side, side);
        }

        // Sample the outline once. Only the interactive message keeps individual
        // grains after rasterization; the caption and numeral reuse frozen images.
        private BitmapSource CreateLettering(string text, Random random, LetteringKind kind)
        {
            bool interactive = kind == LetteringKind.Message;
            bool numeral = kind == LetteringKind.Numeral;
            double rowSpacing = numeral ? 2.55 : 1.9;
            double columnSpacing = numeral ? 2.05 : 1.37;
            FormattedText label = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
                kind == LetteringKind.Caption ? 54 : 125, Brushes.White, 1);
            PathGeometry glyphs = label.BuildGeometry(new Point()).GetFlattenedPathGeometry(.23, ToleranceType.Absolute);
            Rect bounds = glyphs.Bounds;
            int width = (int)Math.Ceiling(bounds.Width) + 10, height = (int)Math.Ceiling(bounds.Height) + 10;
            byte[] pixels = new byte[width * height * 4];
            if (interactive)
            {
                titleTiles = new TitleTile[(width + TileWidth - 1) / TileWidth];
                for (int i = 0; i < titleTiles.Length; i++)
                    titleTiles[i] = new TitleTile { X = i * TileWidth, Width = Math.Min(TileWidth, width - i * TileWidth) };
            }
            List<Point[]> contours = new List<Point[]>();
            foreach (PathFigure figure in glyphs.Figures)
            {
                List<Point> vertices = new List<Point>(); vertices.Add(figure.StartPoint);
                foreach (PathSegment segment in figure.Segments)
                {
                    PolyLineSegment poly = segment as PolyLineSegment;
                    if (poly != null) vertices.AddRange(poly.Points);
                    else { LineSegment line = segment as LineSegment; if (line != null) vertices.Add(line.Point); }
                }
                if (vertices.Count > 2) contours.Add(vertices.ToArray());
            }
            List<TitleGrain> grains = new List<TitleGrain>();
            List<int> incoming = new List<int>();
            List<double> crossings = new List<double>();
            for (double y = bounds.Top + .6; y < bounds.Bottom; y += rowSpacing)
            {
                crossings.Clear();
                foreach (Point[] polygon in contours)
                {
                    Point previous = polygon[polygon.Length - 1];
                    foreach (Point p in polygon)
                    {
                        if ((p.Y > y) != (previous.Y > y))
                            crossings.Add(previous.X + (p.X - previous.X) * (y - previous.Y) / (p.Y - previous.Y));
                        previous = p;
                    }
                }
                crossings.Sort();
                for (int j = 1; j < crossings.Count; j += 2)
                    for (double x = crossings[j - 1] + random.NextDouble() * 1.2; x < crossings[j]; x += columnSpacing + random.NextDouble() * 1.25)
                    {
                        double tx = 5 + x - bounds.X, ty = 5 + y - bounds.Y + (random.NextDouble() - .5) * 1.35;
                        int tint = random.Next(4); double weight = .68 + random.NextDouble() * .60;
                        double light = .64 + random.NextDouble() * .36;
                        Splat(pixels, width, height, tx, ty, weight, palette[tint], light);
                        if (!interactive) continue;
                        if (random.NextDouble() < .41) incoming.Add(grains.Count);
                        titleTiles[(int)tx / TileWidth].Grains.Add(grains.Count);
                        grains.Add(new TitleGrain { X = tx, Y = ty, Theta = random.NextDouble() * Tau,
                            Reach = 35 + random.NextDouble() * 165, Delay = random.NextDouble(), Tint = tint, Weight = weight, Light = light });
                    }
            }
            BitmapSource image = Image(pixels, width, height);
            if (!interactive) return image;
            titleGrains = grains.ToArray();
            incomingGrains = incoming.ToArray();
            foreach (TitleTile tile in titleTiles)
            {
                int tileWidth = tile.Width + 4, tileHeight = height + 4;
                byte[] resting = new byte[tileWidth * tileHeight * 4];
                foreach (int index in tile.Grains)
                {
                    TitleGrain g = titleGrains[index];
                    Splat(resting, tileWidth, tileHeight, g.X - tile.X + 2, g.Y + 2, g.Weight, palette[g.Tint], g.Light);
                }
                tile.Rest = Image(resting, tileWidth, tileHeight);
            }
            grainOffsets = new Point[titleGrains.Length]; lastTitleFrame = -1;
            return image;
        }

        private static void Splat(byte[] pixels, int w, int h, double x, double y, double radius, Color color, double light)
        {
            double edge = radius + .53;
            int x0 = Math.Max(0, (int)(x - edge)), y0 = Math.Max(0, (int)(y - edge));
            int x1 = Math.Min(w - 1, (int)(x + edge)), y1 = Math.Min(h - 1, (int)(y + edge));
            for (int py = y0; py <= y1; py++) for (int px = x0; px <= x1; px++)
            {
                double dx = px + .5 - x, dy = py + .5 - y;
                double a = light * Clamp(edge - Math.Sqrt(dx * dx + dy * dy));
                int n = (py * w + px) * 4;
                pixels[n] = (byte)(pixels[n] * (1 - a) + color.B * a);
                pixels[n + 1] = (byte)(pixels[n + 1] * (1 - a) + color.G * a);
                pixels[n + 2] = (byte)(pixels[n + 2] * (1 - a) + color.R * a);
                pixels[n + 3] = (byte)(pixels[n + 3] * (1 - a) + 255 * a);
            }
        }

        private static BitmapSource Image(byte[] pixels, int w, int h)
        {
            BitmapSource bitmap = BitmapSource.Create(w, h, 96, 96, PixelFormats.Pbgra32, null, pixels, w * 4);
            bitmap.Freeze(); return bitmap;
        }

        // A baked, directional diffraction fan adds broad light between the
        // sharp axial streaks. Each ray has its own spread and spectral edge.
        private FlareStamp CreateFlareFan()
        {
            const int side = 440;
            double[] angles = { -158, -145, -119, -75, -39, -23, 18, 35, 57, 112, 140, 156 };
            int n = angles.Length;
            double[] cos = new double[n], sin = new double[n], widths = new double[n];
            for (int i = 0; i < n; i++)
            {
                double a = angles[i] * Math.PI / 180;
                cos[i] = Math.Cos(a); sin[i] = Math.Sin(a);
                widths[i] = .039 + ((i * 19) % 13) * .009;
            }
            byte[] pixels = new byte[side * side * 4];
            for (int y = 0; y < side; y++) for (int x = 0; x < side; x++)
            {
                double u = (2.0 * x + 1) / side - 1, v = (2.0 * y + 1) / side - 1;
                double radius = Math.Sqrt(u * u + v * v);
                if (radius >= 1) continue;
                double field = .37 * Math.Exp(-radius * 7) + .92 * Math.Exp(-radius * 32);
                double r = field, g = field * .80, b = field * .51;
                for (int i = 0; i < n; i++)
                {
                    double along = u * cos[i] + v * sin[i];
                    if (along <= 0) continue;
                    double cross = (v * cos[i] - u * sin[i]) / (.004 + along * widths[i]);
                    if (Math.Abs(cross) > 3) continue;
                    double beam = (.65 + .17 * (i % 3)) * Math.Exp(-along * (2.9 + .31 * (i % 5)));
                    double red = cross - along * .79, blue = cross + along * .92;
                    r += beam * Math.Exp(-red * red);
                    g += beam * .87 * Math.Exp(-cross * cross);
                    b += beam * .68 * Math.Exp(-blue * blue);
                }
                double edge = 255 * (1 - Ease((radius - .59) / .41));
                int p = (y * side + x) * 4;
                pixels[p] = (byte)(edge * (1 - Math.Exp(-b)));
                pixels[p + 1] = (byte)(edge * (1 - Math.Exp(-g)));
                pixels[p + 2] = (byte)(edge * (1 - Math.Exp(-r)));
                pixels[p + 3] = Math.Max(pixels[p], Math.Max(pixels[p + 1], pixels[p + 2]));
            }
            return new FlareStamp(pixels, side, side, 4);
        }

        // A one-sided optical filament with a white spine and a tapered warm halo.
        private FlareStamp CreateJet()
        {
            const int w = 256, h = 28;
            byte[] pixels = new byte[w * h * 4];
            for (int x = 0; x < w; x++)
            {
                double along = (x + .5) / w;
                double envelope = (1 - Ease((along - .38) / .62)) * Ease(along / .035);
                for (int y = 0; y < h; y++)
                {
                    double cross = (2 * (y + .5) / h - 1) / (1.03 - along * .80);
                    double spine = Math.Exp(-cross * cross * 70);
                    double light = envelope * (.33 * Math.Exp(-cross * cross * 5) + .67 * spine);
                    int p = (y * w + x) * 4;
                    pixels[p] = (byte)(light * (148 + 95 * spine));
                    pixels[p + 1] = (byte)(light * (198 + 52 * spine));
                    pixels[p + 2] = pixels[p + 3] = (byte)(255 * light);
                }
            }
            return new FlareStamp(pixels, w, h, 2);
        }

        private static void DrawCentered(DrawingContext dc, ImageSource image, Point p, double size)
        { dc.DrawImage(image, new Rect(p.X - size * .5, p.Y - size * .5, size, size)); }


        private static Color Mix(Color a, Color b, double amount)
        { return Color.FromRgb((byte)(a.R + (b.R - a.R) * amount), (byte)(a.G + (b.G - a.G) * amount), (byte)(a.B + (b.B - a.B) * amount)); }
        private static double Gaussian(Random r) { return Math.Sqrt(-2 * Math.Log(1 - r.NextDouble())) * Math.Cos(Tau * r.NextDouble()); }
        private static double Bell(double x) { return Math.Exp(-x * x); }
        private static double Clamp(double x) { return Math.Max(0, Math.Min(1, x)); }
        private static double Ease(double x) { x = Clamp(x); return x * x * (3 - 2 * x); }
        private static int Shade(double light) { return (int)(Clamp(light) * (Shades - 1) + .5); }
    }
}
