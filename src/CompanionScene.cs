using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace HolidayTimer
{
    // Companion motion is a sibling of the orb and countdown. It never changes
    // the effect, the centre opacity, or the geometry of the countdown text.
    internal static class CompanionScene
    {
        private sealed class Surface : FrameworkElement
        {
            internal Scene Owner;
            internal Surface() { Width = Height = 344; IsHitTestVisible = false; }
            protected override void OnRender(DrawingContext dc)
            {
                base.OnRender(dc);
                Scene scene = Owner;
                if (scene == null) return;
                double reaction = scene.ReactionAge >= 2 ? 0 : Math.Exp(-scene.ReactionAge * 3.3);
                double audio = scene.MusicEnabled ? scene.Music.LevelDrive : 0;
                double pulse = scene.MusicEnabled ? Math.Max(scene.Music.BeatDrive, scene.Music.BassDrive * .45) : 0;
                double ringTime = NativeOrbEffects.Contains(scene.EffectCanvas) ? NativeOrbEffects.SceneTime(scene.EffectCanvas) : scene.Time;
                double ringWave = Math.Sin(ringTime * (scene.Effect == OrbEffect.Glass ? 1.57 : 2.4)) * scene.Intensity *
                    (scene.Effect == OrbEffect.ShockwavePulses || scene.Effect == OrbEffect.WaterRipples ? 4.0 : 2.5);
                bool walkingAnimal = scene.Kind == CompanionKind.Dog || scene.Kind == CompanionKind.Cat;
                double bounce = scene.Motion.Action != CompanionAction.Travel || walkingAnimal ? 0 : scene.Kind == CompanionKind.Bunny
                    ? Math.Pow(Math.Max(0, Math.Sin(CompanionMotion.SpritePhase(scene.Motion.Distance) * Math.PI * 2)), 2) * 4.5
                    : Math.Sin(CompanionMotion.SpritePhase(scene.Motion.Distance) * Math.PI * 4) * .7;
                double jump = reaction * Math.Sin(Math.Min(1, scene.ReactionAge / .5) * Math.PI) *
                    (scene.Kind == CompanionKind.Bunny ? 12 : 7);
                // Ground companions have their feet at y=51 in a 64-unit
                // sprite, 19 units below its centre. Put those feet on the
                // 125-unit rim so angular travel matches the gait distance.
                double groundAnchor = scene.Kind == CompanionKind.Bird || scene.Kind == CompanionKind.Airplane
                    ? 0 : 19 * CompanionMotion.ArtScale;
                double radius = Math.Min(149, CompanionMotion.Radius + groundAnchor + bounce + jump +
                    (walkingAnimal ? 0 : ringWave + pulse * (5 + 4 * scene.Intensity)) - VisitOffset(scene.VisitAge));
                double angle = -Math.PI / 2 + scene.Motion.Angle;
                double x = 172 + Math.Cos(angle) * radius;
                double y = 172 + Math.Sin(angle) * radius;
                double tilt = angle * 180 / Math.PI + 90;
                if (scene.Kind == CompanionKind.Airplane || scene.Kind == CompanionKind.Bird)
                    tilt += Math.Sin(scene.Time * 2.5) * 4 + reaction * 9;
                else if (scene.Kind == CompanionKind.Car || scene.Kind == CompanionKind.Bicycle || scene.Kind == CompanionKind.Motorcycle)
                    tilt += reaction * Math.Sin(scene.ReactionAge * 12) * 6;
                if (scene.Scenery) CompanionScenery.Draw(dc, scene.Kind, scene.Art.Ink, scene.Time, scene.Motion.Action, scene.Effect);
                dc.PushTransform(new TranslateTransform(x, y));
                dc.PushTransform(new RotateTransform(tilt));
                dc.PushTransform(new ScaleTransform(CompanionMotion.ArtScale, CompanionMotion.ArtScale));
                dc.PushTransform(new ScaleTransform(scene.Motion.Direction, 1));
                dc.PushTransform(new TranslateTransform(-32, -32));
                scene.Art.Draw(dc, scene.Kind, scene.Time, reaction, audio, scene.Motion.Action, scene.Motion.Distance, scene.Motion.Age);
                if (scene.Motion.Action == CompanionAction.Play) CompanionScenery.DrawPlay(dc, scene.Kind, scene.Art.Ink, scene.Motion.Age);
                dc.Pop(); dc.Pop(); dc.Pop(); dc.Pop(); dc.Pop();
            }
        }

        private sealed class Scene
        {
            internal Canvas Canvas, EffectCanvas;
            internal Window Host;
            internal Surface Surface;
            internal CompanionArt Art;
            internal CompanionKind Kind;
            internal HolidayTheme Theme;
            internal OrbEffect Effect;
            internal bool Light, Animate, Loaded, Attached, MusicEnabled, Scenery;
            internal double Intensity, Time = .7, ReactionAge = 3, MusicIntensity = .5;
            internal double Lifetime, NextVisit = 18, VisitAge = 3;
            internal CompanionMotion Motion = new CompanionMotion();
            internal long LastFrame, LastSample, Frames;
            internal AudioSignal Signal = AudioSignal.Zero;
            internal readonly MusicMotion Music = new MusicMotion();
            internal EventHandler Rendering, StateChanged, Closed;
            internal DependencyPropertyChangedEventHandler VisibilityChanged;
            internal RoutedEventHandler LoadedHandler, UnloadedHandler;
            internal PropertyChangedEventHandler SystemChanged;
        }

        private static readonly Dictionary<Canvas, Scene> Scenes = new Dictionary<Canvas, Scene>();
        internal static int ActiveSceneCount { get { return Scenes.Count; } }
        internal static int RenderingSceneCount
        { get { int count = 0; foreach (Scene scene in Scenes.Values) if (scene.Attached) count++; return count; } }

        internal static void Apply(Canvas canvas, Window host, CompanionKind kind, HolidayTheme theme,
            bool light, bool animate, OrbEffect effect, double intensity, bool scenery = true)
        {
            if (canvas == null || host == null) throw new ArgumentNullException();
            canvas.VerifyAccess();
            if (!Enum.IsDefined(typeof(CompanionKind), kind) || kind == CompanionKind.None) { Stop(canvas); return; }
            Scene scene;
            if (Scenes.TryGetValue(canvas, out scene))
            {
                if (scene.Host != host) { Stop(canvas); scene = null; }
                else
                {
                    if (scene.Theme != theme || scene.Light != light) scene.Art = new CompanionArt(theme, light);
                    if (scene.Kind != kind) scene.Motion = new CompanionMotion();
                    scene.Kind = kind; scene.Theme = theme; scene.Light = light; scene.Scenery = scenery;
                    scene.Animate = animate; scene.Effect = effect; scene.Intensity = Unit(intensity);
                    Paint(scene); Sync(scene); return;
                }
            }
            scene = new Scene { Canvas = canvas, Host = host, Kind = kind, Theme = theme, Light = light,
                Animate = animate, Effect = effect, Intensity = Unit(intensity), Loaded = canvas.IsLoaded, Scenery = scenery,
                Art = new CompanionArt(theme, light), Surface = new Surface() };
            scene.EffectCanvas = Layouts.Find<Canvas>(host, canvas.Name == "SettingsCompanionCanvas" ? "SettingsOrbCanvas" : "OrbCanvas");
            scene.Surface.Owner = scene;
            Scenes.Add(canvas, scene);
            canvas.Width = canvas.Height = 344; canvas.IsHitTestVisible = false;
            canvas.Children.Add(scene.Surface);
            scene.Rendering = delegate { Render(scene); };
            scene.VisibilityChanged = delegate { Sync(scene); };
            scene.StateChanged = delegate { Sync(scene); };
            scene.Closed = delegate { Stop(canvas); };
            scene.LoadedHandler = delegate { scene.Loaded = true; Sync(scene); };
            scene.UnloadedHandler = delegate { scene.Loaded = false; Sync(scene); };
            scene.SystemChanged = delegate(object sender, PropertyChangedEventArgs args)
            {
                if (args.PropertyName != "ClientAreaAnimation" && args.PropertyName != "HighContrast") return;
                if (!canvas.Dispatcher.HasShutdownStarted && !canvas.Dispatcher.HasShutdownFinished)
                    canvas.Dispatcher.BeginInvoke(new Action(delegate { Scene current; if (Scenes.TryGetValue(canvas, out current) && current == scene) Sync(scene); }));
            };
            canvas.IsVisibleChanged += scene.VisibilityChanged;
            host.IsVisibleChanged += scene.VisibilityChanged;
            host.StateChanged += scene.StateChanged;
            host.Closed += scene.Closed;
            canvas.Loaded += scene.LoadedHandler;
            canvas.Unloaded += scene.UnloadedHandler;
            SystemParameters.StaticPropertyChanged += scene.SystemChanged;
            Paint(scene); Sync(scene);
        }

        internal static void SetMusic(Canvas canvas, AudioSignal signal, double intensity, bool enabled)
        {
            Scene scene;
            if (canvas == null || !Scenes.TryGetValue(canvas, out scene)) return;
            scene.Signal = signal ?? AudioSignal.Zero; scene.MusicEnabled = enabled;
            scene.MusicIntensity = Unit(intensity); scene.LastSample = Stopwatch.GetTimestamp();
            if (enabled) scene.Music.Receive(scene.Signal.Level, scene.Signal.Bass, scene.Signal.Mid,
                scene.Signal.High, scene.Signal.Beat, scene.MusicIntensity);
            else scene.Music.Quiet();
        }

        internal static bool React(Canvas canvas)
        {
            Scene scene;
            if (canvas == null || !Scenes.TryGetValue(canvas, out scene)) return false;
            // Reduced motion stays still; a test or manual preview can still
            // render an explicit reaction without changing Windows settings.
            if (!MotionAllowed(scene)) return false;
            scene.ReactionAge = 0; scene.Motion.Interact(); Paint(scene); return true;
        }

        private static bool MotionAllowed(Scene scene)
        {
            return scene.Animate && scene.Loaded && scene.Canvas.IsVisible && scene.Host.IsVisible &&
                scene.Host.WindowState != WindowState.Minimized && SystemParameters.ClientAreaAnimation && !SystemParameters.HighContrast;
        }

        private static void Sync(Scene scene)
        {
            Scene current;
            if (!Scenes.TryGetValue(scene.Canvas, out current) || current != scene) return;
            if (!MotionAllowed(scene))
            {
                Detach(scene); scene.Music.Reset(); scene.ReactionAge = 3; scene.VisitAge = 3; Paint(scene); return;
            }
            if (!scene.Attached)
            { scene.LastFrame = Stopwatch.GetTimestamp(); CompositionTarget.Rendering += scene.Rendering; scene.Attached = true; }
        }

        private static void Render(Scene scene)
        {
            if (!MotionAllowed(scene)) { Sync(scene); return; }
            long now = Stopwatch.GetTimestamp();
            double elapsed = (now - scene.LastFrame) / (double)Stopwatch.Frequency;
            if (elapsed < 1.0 / 30) return;
            scene.LastFrame = now; elapsed = Math.Min(.1, elapsed);
            if ((now - scene.LastSample) / (double)Stopwatch.Frequency > .3) scene.Music.Quiet();
            Advance(scene, elapsed);
            Paint(scene);
        }

        private static void Advance(Scene scene, double elapsed)
        {
            scene.Music.Step(elapsed);
            double audio = scene.MusicEnabled ? scene.Music.LevelDrive * 1.1 + scene.Music.BeatDrive * .6 : 0;
            double effectPace = scene.Effect == OrbEffect.ShockwavePulses || scene.Effect == OrbEffect.CometSwarm ? 1.16 :
                scene.Effect == OrbEffect.WaterRipples || scene.Effect == OrbEffect.LiquidMetal ? .88 : 1;
            double reactionBoost = scene.ReactionAge < 2 ? .6 * Math.Exp(-scene.ReactionAge * 3.3) : 0;
            scene.Motion.Step(elapsed, scene.Kind, scene.Intensity, audio + reactionBoost * .35, effectPace);
            scene.Time += elapsed * (1 + Math.Min(1.5, audio) + reactionBoost);
            scene.ReactionAge = Math.Min(3, scene.ReactionAge + elapsed);
            scene.Lifetime += elapsed;
            scene.VisitAge = Math.Min(3, scene.VisitAge + elapsed);
            // Briefly approach from a side, behind the digits, and return.
            // A visit lasts just over one second and never lingers on the text.
            if (scene.Motion.Action == CompanionAction.Travel && ShouldVisit(scene.Lifetime, scene.NextVisit, scene.VisitAge, scene.Motion.Angle))
            { scene.VisitAge = 0; scene.NextVisit = scene.Lifetime + 32; }
        }

        internal static double VisitOffset(double age)
        {
            const double duration = 1.05;
            if (Double.IsNaN(age) || age <= 0 || age >= duration) return 0;
            double progress = Math.Sin(Math.PI * age / duration);
            return 32 * progress * progress;
        }

        internal static bool ShouldVisit(double lifetime, double nextVisit, double previousAge, double angle)
        { return lifetime >= nextVisit && previousAge >= 2 && Math.Abs(Math.Sin(-Math.PI / 2 + angle)) < .8; }

        private static void Paint(Scene scene)
        { scene.Surface.InvalidateVisual(); scene.Frames++; }

        private static double Unit(double value)
        { return Double.IsNaN(value) || Double.IsInfinity(value) ? 0 : Math.Max(0, Math.Min(1, value)); }

        private static void Detach(Scene scene)
        {
            if (scene.Attached) CompositionTarget.Rendering -= scene.Rendering;
            scene.Attached = false; scene.LastFrame = 0;
        }

        internal static void Stop(Canvas canvas)
        {
            if (canvas == null) return;
            canvas.VerifyAccess(); Scene scene;
            if (!Scenes.TryGetValue(canvas, out scene)) return;
            Scenes.Remove(canvas); Detach(scene);
            canvas.IsVisibleChanged -= scene.VisibilityChanged;
            scene.Host.IsVisibleChanged -= scene.VisibilityChanged;
            scene.Host.StateChanged -= scene.StateChanged;
            scene.Host.Closed -= scene.Closed;
            canvas.Loaded -= scene.LoadedHandler;
            canvas.Unloaded -= scene.UnloadedHandler;
            SystemParameters.StaticPropertyChanged -= scene.SystemChanged;
            scene.Surface.Owner = null; canvas.Children.Clear();
        }

        internal static bool Contains(Canvas canvas) { return canvas != null && Scenes.ContainsKey(canvas); }
        internal static CompanionKind Selected(Canvas canvas)
        { Scene scene; return canvas != null && Scenes.TryGetValue(canvas, out scene) ? scene.Kind : CompanionKind.None; }
        internal static OrbEffect Effect(Canvas canvas)
        { Scene scene; return canvas != null && Scenes.TryGetValue(canvas, out scene) ? scene.Effect : OrbEffect.Glass; }
        internal static HolidayTheme Scheme(Canvas canvas)
        { Scene scene; return canvas != null && Scenes.TryGetValue(canvas, out scene) ? scene.Theme : HolidayTheme.Beach; }
        internal static bool Scenery(Canvas canvas)
        { Scene scene; return canvas != null && Scenes.TryGetValue(canvas, out scene) && scene.Scenery; }
        internal static CompanionAction Action(Canvas canvas)
        { Scene scene; return canvas != null && Scenes.TryGetValue(canvas, out scene) ? scene.Motion.Action : CompanionAction.Rest; }
        internal static double Angle(Canvas canvas)
        { Scene scene; return canvas != null && Scenes.TryGetValue(canvas, out scene) ? scene.Motion.Angle : 0; }
        internal static long FrameCount(Canvas canvas) { Scene scene; return canvas != null && Scenes.TryGetValue(canvas, out scene) ? scene.Frames : 0; }
        internal static void RenderAt(Canvas canvas, double time, double angle, double reactionAge, AudioSignal signal, double visitAge = 3)
        {
            Scene scene;
            if (canvas == null || !Scenes.TryGetValue(canvas, out scene)) throw new InvalidOperationException("No companion is selected.");
            scene.Time = Math.Max(0, time); scene.Motion.SetForTest(angle, time * 28); scene.ReactionAge = Math.Max(0, reactionAge);
            if (reactionAge < 2) scene.Motion.Interact();
            scene.VisitAge = Math.Max(0, visitAge);
            scene.Signal = signal ?? AudioSignal.Zero; scene.MusicEnabled = signal != null;
            if (scene.MusicEnabled) scene.Music.Receive(signal.Level, signal.Bass, signal.Mid, signal.High, signal.Beat, 1);
            else scene.Music.Quiet();
            scene.Music.Snap(); Paint(scene);
        }

        internal static void AdvanceForTest(Canvas canvas, double elapsed, AudioSignal signal)
        {
            Scene scene;
            if (canvas == null || !Scenes.TryGetValue(canvas, out scene)) throw new InvalidOperationException("No companion is selected.");
            scene.MusicEnabled = signal != null;
            if (signal == null) scene.Music.Reset();
            else { scene.Music.Receive(signal.Level, signal.Bass, signal.Mid, signal.High, signal.Beat, 1); scene.Music.Snap(); }
            Advance(scene, Math.Max(0, Math.Min(.1, elapsed))); Paint(scene);
        }
    }
}
