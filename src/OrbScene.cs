using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace HolidayTimer
{
    internal static class OrbScene
    {
        private sealed class Palette
        {
            internal Color Light, Main, Deep, Glow, Accent, Core;
        }

        private sealed class Scene
        {
            internal Canvas Canvas;
            internal Window Host;
            internal HolidayTheme Theme;
            internal double Opacity;
            internal bool Animate;
            internal bool Light;
            internal bool Paused;
            internal bool MusicEnabled;
            internal double MusicIntensity = .5;
            internal double LastLevel, LastBass, LastMid, LastHigh, LastBeat;
            internal long LastSample, LastFrame;
            internal readonly MusicMotion Music = new MusicMotion();
            internal ScaleTransform MusicHalo, MusicRim, MusicBackScale, MusicFrontScale;
            internal RadialGradientBrush MusicGlow;
            internal Color[] IdleGlow;
            internal RotateTransform MusicBackAngle, MusicFrontAngle, MusicSheen, MusicDetail;
            internal EventHandler MusicRendering;
            internal bool RenderingAttached;
            internal readonly List<AnimationClock> Clocks = new List<AnimationClock>();
            internal readonly List<Action> Reset = new List<Action>();
            internal DependencyPropertyChangedEventHandler VisibilityChanged;
            internal EventHandler WindowStateChanged, Closed;
            internal PropertyChangedEventHandler SystemChanged;
        }

        private static readonly Dictionary<Canvas, Scene> Scenes = new Dictionary<Canvas, Scene>();

        internal static void Apply(Canvas canvas, Window host, HolidayTheme theme, double opacity, bool animate, bool light = false, OrbEffect effect = OrbEffect.Glass, double effectIntensity = .65)
        {
            if (canvas == null) throw new ArgumentNullException("canvas");
            if (host == null) throw new ArgumentNullException("host");
            canvas.VerifyAccess();
            if (effect != OrbEffect.Glass)
            {
                if (Scenes.ContainsKey(canvas)) StopGlass(canvas);
                NativeOrbEffects.Apply(canvas, host, theme, light, animate, effect, effectIntensity);
                return;
            }
            NativeOrbEffects.Stop(canvas);
            double alpha = Double.IsNaN(opacity) || Double.IsInfinity(opacity) ? 1 : Math.Max(0, Math.Min(1, opacity));
            Scene existing;
            if (Scenes.TryGetValue(canvas, out existing) && existing.Host == host && existing.Theme == theme &&
                existing.Animate == animate && existing.Light == light && canvas.Children.Count == 8 &&
                (existing.Clocks.Count != 0 || !animate || !SystemParameters.ClientAreaAnimation || SystemParameters.HighContrast))
            {
                existing.Opacity = alpha;
                SyncMotion(existing);
                return;
            }
            Stop(canvas);
            canvas.Width = canvas.Height = 344;
            canvas.Background = null;
            canvas.IsHitTestVisible = false;
            // The colour shell stays vivid independently of the centre tint.
            // A soft hollow mask leaves the centre clear rather than stacking
            // several decorative fills into an opaque black/white disc.
            canvas.Opacity = 1;
            RadialGradientBrush shellMask = new RadialGradientBrush
            { Center = new Point(.5, .5), GradientOrigin = new Point(.5, .5), RadiusX = .5, RadiusY = .5 };
            shellMask.GradientStops.Add(new GradientStop(Colors.Transparent, 0));
            shellMask.GradientStops.Add(new GradientStop(Colors.Transparent, .48));
            shellMask.GradientStops.Add(new GradientStop(Colors.White, .68));
            shellMask.GradientStops.Add(new GradientStop(Colors.White, 1));
            shellMask.Freeze();
            canvas.OpacityMask = shellMask;

            Palette palette = Colours(theme, light);
            Scene scene = new Scene { Canvas = canvas, Host = host, Theme = theme, Opacity = alpha, Animate = animate, Light = light };
            Scenes.Add(canvas, scene);

            // Eight geometric layers form the sphere, with an appearance-specific core behind the independent count.
            Ellipse halo = AddEllipse(canvas, 320, 12, Radial(new Point(.5, .5),
                Stop(palette.Glow, .19, 0), Stop(palette.Accent, .09, .53), Stop(palette.Glow, 0, 1)));
            scene.MusicGlow = ((RadialGradientBrush)halo.Fill).Clone();
            scene.IdleGlow = new Color[scene.MusicGlow.GradientStops.Count];
            for (int i = 0; i < scene.IdleGlow.Length; i++) scene.IdleGlow[i] = scene.MusicGlow.GradientStops[i].Color;
            halo.Fill = scene.MusicGlow;
            AddEllipse(canvas, 266, 39, Radial(new Point(.29, .24),
                Stop(palette.Light, .96, 0), Stop(palette.Main, .91, .38),
                Stop(palette.Accent, .86, .70), Stop(palette.Deep, .89, 1)));
            AddEllipse(canvas, 266, 39, Radial(new Point(.5, .5),
                Stop(palette.Core, .84, 0), Stop(palette.Core, .73, .40),
                Stop(palette.Deep, .20, .78), Stop(palette.Deep, 0, 1)));
            Ellipse sheen = AddEllipse(canvas, 266, 39, SpectralSheen(palette));

            Geometry sphereClip = new EllipseGeometry(new Point(172, 172), 133, 133);
            sphereClip.Freeze();
            Path backBand = AddRibbon(canvas, sphereClip,
                "M 34,139 C 85,73 185,60 278,112 C 297,123 301,139 302,150 " +
                "C 296,130 284,119 273,115 C 184,69 87,81 36,142 Z",
                Linear(new Point(0, 0), new Point(1, .35), Stop(palette.Glow, .03, 0),
                    Stop(palette.Glow, .30, .12), Stop(Colors.White, .96, .24), Stop(palette.Accent, .72, .33),
                    Stop(Colors.White, .13, .47), Stop(palette.Glow, .48, .62), Stop(Colors.White, .94, .76),
                    Stop(palette.Accent, .44, .87), Stop(Colors.White, .02, 1)));
            Path frontBand = AddRibbon(canvas, sphereClip,
                "M 39,205 C 78,252 166,265 240,225 C 277,205 293,179 297,153 " +
                "C 295,182 281,208 243,231 C 167,272 79,257 39,209 Z",
                Linear(new Point(0, .9), new Point(1, .12), Stop(Colors.White, .03, 0),
                    Stop(palette.Glow, .52, .19), Stop(Colors.White, .98, .31), Stop(palette.Accent, .78, .41),
                    Stop(Colors.White, .16, .56), Stop(palette.Glow, .86, .66), Stop(Colors.White, .96, .77),
                    Stop(palette.Light, .28, .90), Stop(Colors.White, .02, 1)));
            Path caustic = AddBand(canvas, sphereClip,
                "M 64,98 C 39,147 54,215 109,257 M 95,65 C 155,35 236,62 270,113 " +
                "M 52,132 C 48,145 48,156 49,167 M 226,53 C 244,60 260,73 272,90",
                Linear(new Point(0, 0), new Point(.8, 1), Stop(Colors.White, .91, 0),
                    Stop(palette.Glow, .82, .23), Stop(Colors.White, .12, .43), Stop(palette.Accent, .56, .63),
                    Stop(Colors.White, .71, .81), Stop(Colors.White, .03, 1)), 1.6);
            Ellipse rim = AddEllipse(canvas, 266, 39, null);
            rim.Stroke = Linear(new Point(.1, 0), new Point(.8, 1), Stop(Colors.White, .97, 0),
                Stop(palette.Glow, .88, .18), Stop(Colors.White, .09, .35), Stop(palette.Accent, .77, .54),
                Stop(Colors.White, .96, .68), Stop(palette.Deep, .06, .82), Stop(palette.Glow, .68, 1));
            rim.StrokeThickness = 1.3;

            // These transforms are separate from idle clocks. The fixed shell
            // mask and countdown live outside their reach.
            scene.MusicHalo = new ScaleTransform(1, 1);
            scene.MusicRim = new ScaleTransform(1, 1);
            halo.RenderTransformOrigin = rim.RenderTransformOrigin = new Point(.5, .5);
            halo.RenderTransform = scene.MusicHalo;
            rim.RenderTransform = scene.MusicRim;
            scene.MusicSheen = new RotateTransform();
            sheen.RenderTransformOrigin = new Point(.5, .5);
            sheen.RenderTransform = scene.MusicSheen;

            if (animate && SystemParameters.ClientAreaAnimation && !SystemParameters.HighContrast)
            {
                ScaleTransform breath = new ScaleTransform(1, 1);
                halo.RenderTransformOrigin = new Point(.5, .5);
                TransformGroup haloMotion = new TransformGroup();
                haloMotion.Children.Add(breath);
                haloMotion.Children.Add(scene.MusicHalo);
                halo.RenderTransform = haloMotion;
                Animate(scene, breath, ScaleTransform.ScaleXProperty, Cycle(.97, 1.035, 4, true));
                Animate(scene, breath, ScaleTransform.ScaleYProperty, Cycle(.97, 1.035, 4, true));
                Animate(scene, halo, UIElement.OpacityProperty, Cycle(.72, 1, 4, true));

                RotateTransform backRotation, frontRotation;
                ScaleTransform backFlow, frontFlow;
                Geometry backGeometry = backBand.Data.Clone();
                Geometry frontGeometry = frontBand.Data.Clone();
                // Animate the geometry inside a fixed circular clip, rather than deforming the clip itself.
                backGeometry.Transform = Flow(-16, out backRotation, out backFlow);
                frontGeometry.Transform = Flow(8, out frontRotation, out frontFlow);
                backBand.Data = backGeometry;
                frontBand.Data = frontGeometry;
                Animate(scene, backRotation, RotateTransform.AngleProperty, Cycle(-16, 344, 20, false));
                Animate(scene, frontRotation, RotateTransform.AngleProperty, Cycle(8, 368, 20, false));
                Animate(scene, backFlow, ScaleTransform.ScaleYProperty, Cycle(.98, 1.045, 5.5, true));
                Animate(scene, frontFlow, ScaleTransform.ScaleXProperty, Cycle(.985, 1.025, 6, true));
                Animate(scene, sheen, UIElement.OpacityProperty, Cycle(.83, 1, 4, true));
                RotateTransform detail = new RotateTransform(-5, 172, 172);
                Geometry detailGeometry = caustic.Data.Clone();
                detailGeometry.Transform = detail;
                caustic.Data = detailGeometry;
                Animate(scene, detail, RotateTransform.AngleProperty, Cycle(-5, 5, 5, true));
            }

            AddMusicFlow(backBand, out scene.MusicBackAngle, out scene.MusicBackScale);
            AddMusicFlow(frontBand, out scene.MusicFrontAngle, out scene.MusicFrontScale);
            Geometry musicDetailGeometry = caustic.Data.IsFrozen ? caustic.Data.Clone() : caustic.Data;
            TransformGroup detailMotion = new TransformGroup();
            if (musicDetailGeometry.Transform != null && !musicDetailGeometry.Transform.Value.IsIdentity)
                detailMotion.Children.Add(musicDetailGeometry.Transform);
            scene.MusicDetail = new RotateTransform(0, 172, 172);
            detailMotion.Children.Add(scene.MusicDetail);
            musicDetailGeometry.Transform = detailMotion;
            caustic.Data = musicDetailGeometry;
            scene.MusicRendering = delegate { RenderMusic(scene); };
            if (existing != null)
            {
                scene.MusicEnabled = existing.MusicEnabled;
                scene.MusicIntensity = existing.MusicIntensity;
                scene.LastLevel = existing.LastLevel; scene.LastBass = existing.LastBass;
                scene.LastMid = existing.LastMid; scene.LastHigh = existing.LastHigh; scene.LastBeat = existing.LastBeat;
                scene.LastSample = existing.LastSample;
                ReceiveMusic(scene);
            }

            scene.VisibilityChanged = delegate { SyncMotion(scene); };
            scene.WindowStateChanged = delegate { SyncMotion(scene); };
            scene.Closed = delegate { Stop(canvas); };
            scene.SystemChanged = delegate(object sender, PropertyChangedEventArgs e)
            {
                if (e.PropertyName != "ClientAreaAnimation" && e.PropertyName != "HighContrast") return;
                if (canvas.Dispatcher.HasShutdownStarted || canvas.Dispatcher.HasShutdownFinished) return;
                canvas.Dispatcher.BeginInvoke(new Action(delegate
                {
                    Scene current;
                    if (Scenes.TryGetValue(canvas, out current) && current == scene)
                        Apply(canvas, host, scene.Theme, scene.Opacity, scene.Animate, scene.Light);
                }));
            };
            canvas.IsVisibleChanged += scene.VisibilityChanged;
            host.IsVisibleChanged += scene.VisibilityChanged;
            host.StateChanged += scene.WindowStateChanged;
            host.Closed += scene.Closed;
            SystemParameters.StaticPropertyChanged += scene.SystemChanged;
            SyncMotion(scene);
        }

        internal static void Stop(Canvas canvas)
        {
            NativeOrbEffects.Stop(canvas);
            StopGlass(canvas);
        }

        private static void StopGlass(Canvas canvas)
        {
            if (canvas == null) return;
            canvas.VerifyAccess();
            Scene scene;
            if (Scenes.TryGetValue(canvas, out scene))
            {
                Scenes.Remove(canvas);
                canvas.IsVisibleChanged -= scene.VisibilityChanged;
                scene.Host.IsVisibleChanged -= scene.VisibilityChanged;
                scene.Host.StateChanged -= scene.WindowStateChanged;
                scene.Host.Closed -= scene.Closed;
                SystemParameters.StaticPropertyChanged -= scene.SystemChanged;
                DetachMusic(scene);
                foreach (AnimationClock clock in scene.Clocks) clock.Controller.Remove();
                foreach (Action reset in scene.Reset) reset();
                scene.Clocks.Clear();
                scene.Reset.Clear();
            }
            canvas.Children.Clear();
            canvas.OpacityMask = null;
        }

        private static void SyncMotion(Scene scene)
        {
            Scene current;
            if (!Scenes.TryGetValue(scene.Canvas, out current) || current != scene) return;
            SyncMusic(scene);
            if (scene.Clocks.Count == 0) return;
            bool pause = !scene.Canvas.IsVisible || !scene.Host.IsVisible || scene.Host.WindowState == WindowState.Minimized ||
                !SystemParameters.ClientAreaAnimation || SystemParameters.HighContrast;
            if (pause == scene.Paused) return;
            scene.Paused = pause;
            foreach (AnimationClock clock in scene.Clocks)
            {
                if (pause) clock.Controller.Pause(); else clock.Controller.Resume();
            }
        }

        internal static void SetMusic(Canvas canvas, AudioSignal signal, double intensity, bool enabled)
        {
            if (canvas == null) return;
            canvas.VerifyAccess();
            if (NativeOrbEffects.SetMusic(canvas, signal, intensity, enabled)) return;
            Scene scene;
            if (!Scenes.TryGetValue(canvas, out scene)) return;
            if (signal == null) signal = AudioSignal.Zero;
            scene.MusicEnabled = enabled;
            scene.MusicIntensity = intensity;
            scene.LastLevel = signal.Level; scene.LastBass = signal.Bass;
            scene.LastMid = signal.Mid; scene.LastHigh = signal.High; scene.LastBeat = signal.Beat;
            scene.LastSample = Stopwatch.GetTimestamp();
            ReceiveMusic(scene);
            SyncMusic(scene);
        }

        private static void ReceiveMusic(Scene scene)
        {
            if (scene.MusicEnabled)
                scene.Music.Receive(scene.LastLevel, scene.LastBass, scene.LastMid, scene.LastHigh, scene.LastBeat, scene.MusicIntensity);
            else scene.Music.Quiet();
        }

        private static bool MusicAllowed(Scene scene)
        {
            return SystemParameters.ClientAreaAnimation && !SystemParameters.HighContrast &&
                scene.Canvas.IsVisible && scene.Host.IsVisible && scene.Host.WindowState != WindowState.Minimized;
        }

        private static void SyncMusic(Scene scene)
        {
            if (!MusicAllowed(scene))
            {
                DetachMusic(scene);
                scene.Music.Reset();
                PaintMusic(scene);
                return;
            }
            if (!scene.Animate)
            {
                scene.Music.Snap();
                PaintMusic(scene);
                return;
            }
            if ((scene.Music.HasTarget || !scene.Music.AtRest) && !scene.RenderingAttached)
            {
                scene.LastFrame = Stopwatch.GetTimestamp();
                CompositionTarget.Rendering += scene.MusicRendering;
                scene.RenderingAttached = true;
            }
            else if (!scene.Music.HasTarget && scene.Music.AtRest) DetachMusic(scene);
        }

        private static void RenderMusic(Scene scene)
        {
            if (!MusicAllowed(scene)) { SyncMusic(scene); return; }
            long now = Stopwatch.GetTimestamp();
            // A stopped capture must never leave the shell held at its last beat.
            if ((now - scene.LastSample) / (double)Stopwatch.Frequency > .3) scene.Music.Quiet();
            scene.Music.Step((now - scene.LastFrame) / (double)Stopwatch.Frequency);
            scene.LastFrame = now;
            PaintMusic(scene);
            if (!scene.Music.HasTarget && scene.Music.AtRest)
            {
                scene.Music.Reset();
                PaintMusic(scene);
                DetachMusic(scene);
            }
        }

        private static void PaintMusic(Scene scene)
        {
            MusicMotion motion = scene.Music;
            // Include the largest idle breath when keeping the coloured glow
            // inside the transparent window. Brightness carries extra energy.
            scene.MusicHalo.ScaleX = scene.MusicHalo.ScaleY = Math.Min(motion.HaloScale, 344.0 / (320 * 1.035));
            for (int i = 0; i < scene.IdleGlow.Length; i++)
            {
                Color colour = scene.IdleGlow[i];
                colour.A = (byte)Math.Min(255, Math.Round(colour.A * (1 + motion.LevelDrive * 3.2 + motion.BeatDrive * .6)));
                scene.MusicGlow.GradientStops[i].Color = colour;
            }
            scene.MusicRim.ScaleX = scene.MusicRim.ScaleY = motion.RimScale;
            scene.MusicBackScale.ScaleX = scene.MusicFrontScale.ScaleX = motion.RibbonScaleX;
            scene.MusicBackScale.ScaleY = scene.MusicFrontScale.ScaleY = motion.RibbonScaleY;
            scene.MusicBackAngle.Angle = motion.BackRibbonAngle;
            scene.MusicFrontAngle.Angle = motion.FrontRibbonAngle;
            scene.MusicSheen.Angle = motion.SheenAngle;
            scene.MusicDetail.Angle = motion.DetailAngle;
        }

        private static void DetachMusic(Scene scene)
        {
            if (scene.RenderingAttached) CompositionTarget.Rendering -= scene.MusicRendering;
            scene.RenderingAttached = false;
            scene.LastFrame = 0;
        }

        private static void AddMusicFlow(Path ribbon, out RotateTransform rotation, out ScaleTransform scale)
        {
            Geometry geometry = ribbon.Data.IsFrozen ? ribbon.Data.Clone() : ribbon.Data;
            TransformGroup group = new TransformGroup();
            if (geometry.Transform != null && !geometry.Transform.Value.IsIdentity) group.Children.Add(geometry.Transform);
            scale = new ScaleTransform(1, 1, 172, 172);
            rotation = new RotateTransform(0, 172, 172);
            group.Children.Add(scale);
            group.Children.Add(rotation);
            geometry.Transform = group;
            ribbon.Data = geometry;
        }

        private static TransformGroup Flow(double angle, out RotateTransform rotation, out ScaleTransform scale)
        {
            rotation = new RotateTransform(angle, 172, 172);
            scale = new ScaleTransform(1, 1, 172, 172);
            TransformGroup group = new TransformGroup();
            group.Children.Add(scale);
            group.Children.Add(rotation);
            return group;
        }

        private static Ellipse AddEllipse(Canvas canvas, double diameter, double offset, Brush fill)
        {
            Ellipse ellipse = new Ellipse { Width = diameter, Height = diameter, Fill = fill, IsHitTestVisible = false };
            Canvas.SetLeft(ellipse, offset); Canvas.SetTop(ellipse, offset);
            canvas.Children.Add(ellipse);
            return ellipse;
        }

        private static Path AddBand(Canvas canvas, Geometry clip, string path, Brush stroke, double width)
        {
            Geometry geometry = Geometry.Parse(path); geometry.Freeze();
            Path band = new Path { Width = 344, Height = 344, Stretch = Stretch.None, Data = geometry, Stroke = stroke, StrokeThickness = width,
                StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round, Clip = clip, IsHitTestVisible = false };
            canvas.Children.Add(band);
            return band;
        }

        private static Path AddRibbon(Canvas canvas, Geometry clip, string path, Brush fill)
        {
            Path ribbon = AddBand(canvas, clip, path, fill, 0);
            ribbon.Stroke = null;
            ribbon.Fill = fill;
            return ribbon;
        }

        private static Brush SpectralSheen(Palette palette)
        {
            DrawingGroup material = new DrawingGroup();
            material.Children.Add(new GeometryDrawing(Radial(new Point(.23, .16),
                Stop(Colors.White, .78, 0), Stop(palette.Light, .15, .24),
                Stop(palette.Accent, .03, .48), Stop(Colors.White, 0, 1)), null,
                new EllipseGeometry(new Rect(0, 0, 266, 266))));
            AddGlint(material, palette.Glow, new Point(25, 98), 24, 61, -24, .77);
            AddGlint(material, palette.Accent, new Point(245, 140), 18, 57, 21, .69);
            AddGlint(material, palette.Glow, new Point(164, 248), 56, 14, -11, .66);
            AddGlint(material, Colors.White, new Point(78, 31), 23, 6, -24, .97);
            material.Freeze();
            DrawingBrush brush = new DrawingBrush(material) { ViewboxUnits = BrushMappingMode.Absolute,
                Viewbox = new Rect(0, 0, 266, 266), Stretch = Stretch.Fill };
            brush.Freeze();
            return brush;
        }

        private static void AddGlint(DrawingGroup material, Color colour, Point centre, double radiusX, double radiusY, double angle, double alpha)
        {
            EllipseGeometry lens = new EllipseGeometry(centre, radiusX, radiusY);
            lens.Transform = new RotateTransform(angle, centre.X, centre.Y);
            material.Children.Add(new GeometryDrawing(Radial(new Point(.5, .5),
                Stop(colour, alpha, 0), Stop(colour, alpha * .26, .42), Stop(colour, 0, 1)), null, lens));
        }

        private static GradientStop Stop(Color colour, double alpha, double offset)
        {
            colour.A = (byte)Math.Round(255 * alpha);
            return new GradientStop(colour, offset);
        }

        private static Brush Radial(Point origin, params GradientStop[] stops)
        {
            RadialGradientBrush brush = new RadialGradientBrush { GradientOrigin = origin,
                Center = new Point(.5, .5), RadiusX = .56, RadiusY = .56 };
            foreach (GradientStop stop in stops) brush.GradientStops.Add(stop);
            brush.Freeze();
            return brush;
        }

        private static Brush Linear(Point start, Point end, params GradientStop[] stops)
        {
            LinearGradientBrush brush = new LinearGradientBrush { StartPoint = start, EndPoint = end };
            foreach (GradientStop stop in stops) brush.GradientStops.Add(stop);
            brush.Freeze();
            return brush;
        }

        private static DoubleAnimation Cycle(double from, double to, double seconds, bool reverse)
        {
            DoubleAnimation animation = new DoubleAnimation(from, to, TimeSpan.FromSeconds(seconds));
            animation.AutoReverse = reverse;
            animation.RepeatBehavior = RepeatBehavior.Forever;
            if (reverse) animation.EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut };
            return animation;
        }

        private static void Animate(Scene scene, Animatable target, DependencyProperty property, DoubleAnimation animation)
        {
            AnimationClock clock = (AnimationClock)animation.CreateClock(true);
            target.ApplyAnimationClock(property, clock, HandoffBehavior.SnapshotAndReplace);
            scene.Clocks.Add(clock);
            scene.Reset.Add(delegate { target.ApplyAnimationClock(property, null); });
        }

        private static void Animate(Scene scene, UIElement target, DependencyProperty property, DoubleAnimation animation)
        {
            AnimationClock clock = (AnimationClock)animation.CreateClock(true);
            target.ApplyAnimationClock(property, clock, HandoffBehavior.SnapshotAndReplace);
            scene.Clocks.Add(clock);
            scene.Reset.Add(delegate { target.ApplyAnimationClock(property, null); });
        }

        private static Palette Colours(HolidayTheme theme, bool light)
        {
            if (light)
            {
                if (theme == HolidayTheme.Sunset)
                    return new Palette { Light = Rgb(255, 248, 255), Main = Rgb(213, 193, 244), Deep = Rgb(144, 102, 181),
                        Glow = Rgb(223, 175, 220), Accent = Rgb(159, 176, 237), Core = Rgb(250, 246, 253) };
                if (theme == HolidayTheme.Festive)
                    return new Palette { Light = Rgb(255, 251, 242), Main = Rgb(247, 217, 184), Deep = Rgb(186, 121, 138),
                        Glow = Rgb(255, 194, 140), Accent = Rgb(238, 159, 184), Core = Rgb(251, 245, 241) };
                return new Palette { Light = Rgb(253, 253, 255), Main = Rgb(168, 221, 246), Deep = Rgb(77, 137, 185),
                    Glow = Rgb(156, 230, 225), Accent = Rgb(176, 176, 242), Core = Rgb(240, 246, 253) };
            }
            if (theme == HolidayTheme.Sunset)
                return new Palette { Light = Rgb(255, 231, 249), Main = Rgb(194, 128, 231), Deep = Rgb(88, 44, 146),
                    Glow = Rgb(244, 143, 242), Accent = Rgb(133, 186, 255), Core = Rgb(50, 22, 78) };
            if (theme == HolidayTheme.Festive)
                return new Palette { Light = Rgb(255, 239, 210), Main = Rgb(249, 149, 103), Deep = Rgb(153, 59, 85),
                    Glow = Rgb(255, 188, 94), Accent = Rgb(255, 134, 180), Core = Rgb(73, 31, 48) };
            return new Palette { Light = Rgb(229, 247, 255), Main = Rgb(130, 179, 239), Deep = Rgb(31, 66, 139),
                Glow = Rgb(99, 215, 255), Accent = Rgb(210, 160, 255), Core = Rgb(17, 28, 50) };
        }

        private static Color Rgb(byte red, byte green, byte blue)
        { return Color.FromRgb(red, green, blue); }
    }
}
