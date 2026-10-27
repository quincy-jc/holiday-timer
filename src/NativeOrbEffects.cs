using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace HolidayTimer
{
    // A scene owns only the coloured shell. The centre material, number and
    // controls remain siblings, so audio can never move or dim the countdown.
    internal static class NativeOrbEffects
    {
        private sealed class Surface : FrameworkElement
        {
            private readonly DrawingVisual visual = new DrawingVisual();
            internal Surface()
            {
                Width = Height = 344;
                IsHitTestVisible = false;
                AddVisualChild(visual);
            }
            protected override int VisualChildrenCount { get { return 1; } }
            protected override Visual GetVisualChild(int index)
            {
                if (index != 0) throw new ArgumentOutOfRangeException("index");
                return visual;
            }
            internal void Paint(EffectDrawing painter, OrbEffect effect, double seconds, double intensity,
                double musicIntensity, MusicMotion music, bool musicEnabled)
            {
                using (DrawingContext drawing = visual.RenderOpen())
                    painter.Paint(drawing, effect, seconds, intensity, musicIntensity, music, musicEnabled);
            }
        }

        private sealed class Scene
        {
            internal Canvas Canvas;
            internal Window Host;
            internal Surface Surface;
            internal EffectDrawing Painter;
            internal HolidayTheme Theme;
            internal OrbEffect Effect;
            internal bool Light, Animate, Attached, Loaded;
            internal bool MusicEnabled;
            internal double Intensity, MusicIntensity = .5, Time = 2.4;
            internal long LastFrame, LastSample, Frames;
            internal AudioSignal Signal = AudioSignal.Zero;
            internal readonly MusicMotion Music = new MusicMotion();
            internal EventHandler Rendering, StateChanged, Closed;
            internal DependencyPropertyChangedEventHandler VisibilityChanged;
            internal RoutedEventHandler OnLoaded, OnUnloaded;
            internal PropertyChangedEventHandler SystemChanged;
        }

        private static readonly Dictionary<Canvas, Scene> Scenes = new Dictionary<Canvas, Scene>();
        internal static int ActiveSceneCount { get { return Scenes.Count; } }
        internal static int RenderingSceneCount
        {
            get { int count = 0; foreach (Scene scene in Scenes.Values) if (scene.Attached) count++; return count; }
        }

        internal static bool Contains(Canvas canvas) { return canvas != null && Scenes.ContainsKey(canvas); }

        internal static void Apply(Canvas canvas, Window host, HolidayTheme theme, bool light, bool animate,
            OrbEffect effect, double intensity)
        {
            if (canvas == null) throw new ArgumentNullException("canvas");
            if (host == null) throw new ArgumentNullException("host");
            canvas.VerifyAccess();
            if (effect == OrbEffect.Glass) throw new ArgumentException("The glass scene has its own renderer.", "effect");
            if (!Enum.IsDefined(typeof(OrbEffect), effect)) effect = OrbEffect.MusicPulse;
            intensity = EffectDrawing.Unit(intensity);
            Scene previous;
            if (Scenes.TryGetValue(canvas, out previous) && previous.Host == host && previous.Theme == theme &&
                previous.Light == light && previous.Effect == effect && previous.Animate == animate &&
                canvas.Children.Count == 1 && canvas.Children[0] == previous.Surface)
            {
                if (previous.Intensity != intensity) { previous.Intensity = intensity; Paint(previous); }
                Sync(previous);
                return;
            }
            Stop(canvas);
            canvas.Width = canvas.Height = 344;
            canvas.Background = null;
            canvas.IsHitTestVisible = false;
            canvas.Opacity = 1;
            RadialGradientBrush clearCentre = new RadialGradientBrush
            { Center = new Point(.5, .5), GradientOrigin = new Point(.5, .5), RadiusX = .5, RadiusY = .5 };
            clearCentre.GradientStops.Add(new GradientStop(Colors.Transparent, 0));
            clearCentre.GradientStops.Add(new GradientStop(Colors.Transparent, 78.0 / 172));
            clearCentre.GradientStops.Add(new GradientStop(Colors.White, 105.0 / 172));
            clearCentre.GradientStops.Add(new GradientStop(Colors.White, 1));
            clearCentre.Freeze();
            canvas.OpacityMask = clearCentre;
            Scene scene = new Scene { Canvas = canvas, Host = host, Surface = new Surface(),
                Painter = new EffectDrawing(theme, light), Theme = theme, Light = light, Animate = animate,
                Effect = effect, Intensity = intensity, Loaded = canvas.IsLoaded };
            if (previous != null)
            {
                scene.Time = previous.Time;
                scene.MusicEnabled = previous.MusicEnabled;
                scene.MusicIntensity = previous.MusicIntensity;
                scene.Signal = previous.Signal;
                scene.LastSample = previous.LastSample;
                Receive(scene);
                scene.Music.Snap();
            }
            Scenes.Add(canvas, scene);
            canvas.Children.Add(scene.Surface);
            scene.Rendering = delegate { Render(scene); };
            scene.VisibilityChanged = delegate { Sync(scene); };
            scene.StateChanged = delegate { Sync(scene); };
            scene.Closed = delegate { Stop(canvas); };
            scene.OnLoaded = delegate { scene.Loaded = true; Sync(scene); };
            scene.OnUnloaded = delegate { scene.Loaded = false; Sync(scene); };
            scene.SystemChanged = delegate(object sender, PropertyChangedEventArgs args)
            {
                if (args.PropertyName != "ClientAreaAnimation" && args.PropertyName != "HighContrast") return;
                if (canvas.Dispatcher.HasShutdownStarted || canvas.Dispatcher.HasShutdownFinished) return;
                canvas.Dispatcher.BeginInvoke(new Action(delegate
                {
                    Scene current;
                    if (Scenes.TryGetValue(canvas, out current) && current == scene) Sync(scene);
                }));
            };
            canvas.IsVisibleChanged += scene.VisibilityChanged;
            host.IsVisibleChanged += scene.VisibilityChanged;
            host.StateChanged += scene.StateChanged;
            host.Closed += scene.Closed;
            canvas.Loaded += scene.OnLoaded;
            canvas.Unloaded += scene.OnUnloaded;
            SystemParameters.StaticPropertyChanged += scene.SystemChanged;
            Paint(scene);
            Sync(scene);
        }

        internal static bool SetMusic(Canvas canvas, AudioSignal signal, double musicIntensity, bool enabled)
        {
            if (canvas == null) return false;
            canvas.VerifyAccess();
            Scene scene;
            if (!Scenes.TryGetValue(canvas, out scene)) return false;
            scene.Signal = signal ?? AudioSignal.Zero;
            scene.MusicEnabled = enabled;
            scene.MusicIntensity = EffectDrawing.Unit(musicIntensity);
            scene.LastSample = Stopwatch.GetTimestamp();
            Receive(scene);
            if (!scene.Animate)
            {
                scene.Music.Snap();
                Paint(scene);
            }
            Sync(scene);
            return true;
        }

        private static void Receive(Scene scene)
        {
            if (scene.MusicEnabled)
                scene.Music.Receive(scene.Signal.Level, scene.Signal.Bass, scene.Signal.Mid, scene.Signal.High,
                    scene.Signal.Beat, scene.MusicIntensity);
            else scene.Music.Quiet();
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
                bool repaint = scene.Attached || !scene.Music.AtRest;
                Detach(scene);
                // Captured energy does not remain frozen when the window is hidden.
                if (scene.Animate) scene.Music.Reset();
                if (repaint) Paint(scene);
                return;
            }
            if (!scene.Attached)
            {
                scene.LastFrame = Stopwatch.GetTimestamp();
                CompositionTarget.Rendering += scene.Rendering;
                scene.Attached = true;
            }
        }

        private static void Render(Scene scene)
        {
            if (!MotionAllowed(scene)) { Sync(scene); return; }
            long now = Stopwatch.GetTimestamp();
            double elapsed = (now - scene.LastFrame) / (double)Stopwatch.Frequency;
            if (elapsed < 1.0 / 30) return;
            scene.LastFrame = now;
            elapsed = Math.Min(.1, elapsed);
            if ((now - scene.LastSample) / (double)Stopwatch.Frequency > .3) scene.Music.Quiet();
            scene.Music.Step(elapsed);
            // The audio modifies flow speed and shell geometry, never a sibling.
            double audioSpeed = scene.MusicEnabled ? Math.Min(2.4,
                scene.Music.LevelDrive * 3.2 + scene.Music.FlowDrive * .9 + scene.Music.BeatDrive * .6) : 0;
            scene.Time += elapsed * (1 + audioSpeed);
            Paint(scene);
        }

        private static void Paint(Scene scene)
        {
            scene.Surface.Paint(scene.Painter, scene.Effect, scene.Time, scene.Intensity, scene.MusicIntensity,
                scene.Music, scene.MusicEnabled);
            scene.Frames++;
        }

        internal static void RenderAt(Canvas canvas, double seconds, AudioSignal signal)
        {
            if (canvas == null) throw new ArgumentNullException("canvas");
            canvas.VerifyAccess();
            Scene scene;
            if (!Scenes.TryGetValue(canvas, out scene)) throw new InvalidOperationException("There is no native effect scene.");
            // Deterministic readout for render verification, without a fake beat generator.
            scene.Time = Double.IsNaN(seconds) || Double.IsInfinity(seconds) ? 0 : Math.Max(0, seconds);
            scene.Signal = signal ?? AudioSignal.Zero;
            Receive(scene);
            scene.Music.Snap();
            Paint(scene);
        }

        internal static long FrameCount(Canvas canvas)
        { Scene scene; return canvas != null && Scenes.TryGetValue(canvas, out scene) ? scene.Frames : 0; }

        internal static double SceneTime(Canvas canvas)
        { Scene scene; return canvas != null && Scenes.TryGetValue(canvas, out scene) ? scene.Time : 0; }

        internal static long CapturedShockwaveEmissions(Canvas canvas)
        { Scene scene; return canvas != null && Scenes.TryGetValue(canvas, out scene) ? scene.Painter.CapturedShockwaveEmissions : 0; }

        internal static int CapturedShockwaveCount(Canvas canvas)
        { Scene scene; return canvas != null && Scenes.TryGetValue(canvas, out scene) ? scene.Painter.CapturedShockwaveCount : 0; }

        private static void Detach(Scene scene)
        {
            if (scene.Attached) CompositionTarget.Rendering -= scene.Rendering;
            scene.Attached = false;
            scene.LastFrame = 0;
        }

        internal static void Stop(Canvas canvas)
        {
            if (canvas == null) return;
            canvas.VerifyAccess();
            Scene scene;
            if (!Scenes.TryGetValue(canvas, out scene)) return;
            Scenes.Remove(canvas);
            Detach(scene);
            canvas.IsVisibleChanged -= scene.VisibilityChanged;
            scene.Host.IsVisibleChanged -= scene.VisibilityChanged;
            scene.Host.StateChanged -= scene.StateChanged;
            scene.Host.Closed -= scene.Closed;
            canvas.Loaded -= scene.OnLoaded;
            canvas.Unloaded -= scene.OnUnloaded;
            SystemParameters.StaticPropertyChanged -= scene.SystemChanged;
            canvas.Children.Clear();
            canvas.OpacityMask = null;
        }
    }
}
