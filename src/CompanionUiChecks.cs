using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace HolidayTimer
{
    internal static partial class UiChecks
    {
        private static void CheckCompanionControls(HolidayApplication app, Window form, SettingsStore store, string root, bool render)
        {
            SettingsLayout.SelectSection(form, "CompanionTab");
            ComboBox choice = Layouts.Find<ComboBox>(form, "CompanionChoice");
            Canvas embedded = Layouts.Find<Canvas>(form, "SettingsCompanionCanvas");
            TextBlock description = Layouts.Find<TextBlock>(form, "CompanionDescription");
            CheckBox scenery = Layouts.Find<CheckBox>(form, "CompanionScenery");
            Check(choice.Items.Count == 9 && choice.SelectedIndex == 0, "All eight optional companions and None are available");
            Check(!String.IsNullOrEmpty(AutomationProperties.GetName(choice)) && !String.IsNullOrEmpty(AutomationProperties.GetName(Layouts.Find<Button>(form, "TryCompanion"))),
                "Companion controls have accessible names");
            Check(!CompanionScene.Contains(embedded), "The default orb has no companion scene");
            Check(scenery.IsChecked == true && !scenery.IsEnabled, "Scenery is ready but disabled until a companion is selected");
            int initialScenes = CompanionScene.ActiveSceneCount;
            Check(!CompanionInteraction.IsDrag(new Point(10, 10), new Point(10, 10)) &&
                CompanionInteraction.IsDrag(new Point(10, 10), new Point(10 + SystemParameters.MinimumHorizontalDragDistance + 1, 10)),
                "A click and a drag are distinguished using the Windows movement threshold");
            CompanionInteraction.Gesture gesture = new CompanionInteraction.Gesture();
            gesture.Begin(new Point(10, 10), true);
            Check(!gesture.StartDrag(new Point(10, 10)) && gesture.EndClick(), "A stationary press finishes as one interaction");
            gesture.Begin(new Point(10, 10), true);
            Check(gesture.StartDrag(new Point(10 + SystemParameters.MinimumHorizontalDragDistance + 1, 10)) && !gesture.EndClick(),
                "A drag never also triggers a click reaction");
            gesture.Begin(new Point(10, 10), true); gesture.Cancel();
            Check(!gesture.EndClick(), "Losing capture cancels an incomplete interaction");
            gesture.Begin(new Point(10, 10), false);
            Check(!gesture.StartDrag(new Point(50, 50)) && !gesture.EndClick(), "A failed capture cannot move or interact with the gadget");
            Check(CompanionScene.VisitOffset(0) == 0 && CompanionScene.VisitOffset(.525) > 30 && CompanionScene.VisitOffset(1.05) == 0,
                "An occasional approach returns to the ring in just over one second");
            CompanionArt studyArt = new CompanionArt(HolidayTheme.Beach, true);
            if (render)
            {
                RenderCompanionStudies(studyArt, Path.Combine(root, "previews", "companion-anatomy.png"));
                RenderWalkingStudies(studyArt, Path.Combine(root, "previews", "companion-walking.png"));
            }
            foreach (CompanionKind animated in new[] { CompanionKind.Dog, CompanionKind.Cat, CompanionKind.Bunny,
                CompanionKind.Bird, CompanionKind.Car, CompanionKind.Bicycle, CompanionKind.Motorcycle })
                Check(!System.Linq.Enumerable.SequenceEqual(ArtPixels(studyArt, animated, 1.2, 0), ArtPixels(studyArt, animated, 1.6, 0)),
                    animated + " has changing articulated artwork or wheels at a fixed position");
            Check(!System.Linq.Enumerable.SequenceEqual(ArtPixels(studyArt, CompanionKind.Cat, 1, 0, CompanionAction.Play), ArtPixels(studyArt, CompanionKind.Cat, 1, 0)) &&
                !System.Linq.Enumerable.SequenceEqual(ArtPixels(studyArt, CompanionKind.Dog, 1, 0, CompanionAction.Play), ArtPixels(studyArt, CompanionKind.Dog, 1, 0)),
                "The cat and dog have distinct grooming and barking poses");
            Check(!CompanionScene.ShouldVisit(17, 18, 3, Math.PI / 2) && CompanionScene.ShouldVisit(18, 18, 3, Math.PI / 2) &&
                !CompanionScene.ShouldVisit(18, 18, 3, 0) && !CompanionScene.ShouldVisit(18, 18, .5, Math.PI / 2),
                "Visits wait for their interval, start at a side, and do not repeat immediately");
            CheckLiveCompanionMotion();

            app.ShowGadgetPreview(form);
            Window preview = app.GadgetPreview;
            Canvas floating = Layouts.Find<Canvas>(preview, "CompanionCanvas");
            TextBlock digits = Layouts.Find<TextBlock>(preview, "RemainingNumber");
            TextBlock unit = Layouts.Find<TextBlock>(preview, "RemainingUnit");
            Point digitsAt = digits.TransformToAncestor((Visual)preview.Content).Transform(new Point());
            Point unitAt = unit.TransformToAncestor((Visual)preview.Content).Transform(new Point());
            try
            {
                for (int i = 1; i < choice.Items.Count; i++)
                {
                    choice.SelectedIndex = i;
                    Check(WaitFor(delegate { return CompanionScene.Selected(embedded) == (CompanionKind)i && CompanionScene.Selected(floating) == (CompanionKind)i; }),
                        "Companion " + i + " appears in both live previews");
                    AppSettings draft; string error;
                    Check(app.ReadForm(form, false, out draft, out error) && draft.Companion == (CompanionKind)i,
                        "Companion " + i + " enters the unsaved draft");
                    Check(!String.IsNullOrWhiteSpace(description.Text) && store.Load().Companion == CompanionKind.None,
                        "Companion " + i + " is described without saving the draft");
                    CompanionScene.RenderAt(embedded, 1.2, 0, .18, new AudioSignal(.6, .7, .5, .3, .8));
                    CompanionScene.RenderAt(floating, 1.2, 0, .18, new AudioSignal(.6, .7, .5, .3, .8));
                    Pump(40);
                    if (render)
                    {
                        string images = Path.Combine(root, "previews"); Directory.CreateDirectory(images);
                        Render(preview, Path.Combine(images, "companion-" + ((CompanionKind)i).ToString().ToLowerInvariant() + ".png"));
                        if (i == (int)CompanionKind.Dog) Render(form, Path.Combine(images, "companion-settings.png"));
                    }
                    Check(WaitFor(delegate { return MaximumAlpha(CompanionPixels(floating)) > 0 || app.TestDispatcherError != null; }, 2000) && app.TestDispatcherError == null,
                        "Companion " + i + " renders visible native artwork" + (app.TestDispatcherError == null ? "" : ": " + app.TestDispatcherError));
                    System.Collections.Generic.HashSet<int> colours = OpaqueColours(CompanionPixels(floating));
                    Check(ColoursClose(colours), "Companion " + i + " uses a single-colour silhouette (" + ColourRanges(colours) + ")");
                    Check(digits.TransformToAncestor((Visual)preview.Content).Transform(new Point()) == digitsAt &&
                        unit.TransformToAncestor((Visual)preview.Content).Transform(new Point()) == unitAt,
                        "Companion " + i + " leaves the countdown's rows stable");
                }
                choice.SelectedIndex = (int)CompanionKind.Dog;
                Check(WaitFor(delegate { return CompanionScene.Selected(floating) == CompanionKind.Dog; }), "Dog preview is ready for motion checks");
                scenery.IsChecked = false;
                Check(WaitFor(delegate { return !CompanionScene.Scenery(embedded) && !CompanionScene.Scenery(floating); }),
                    "Turning scenery off updates both unsaved previews");
                AppSettings sceneryDraft; string sceneryError;
                Check(app.ReadForm(form, false, out sceneryDraft, out sceneryError) && !sceneryDraft.CompanionScenery && store.Load().CompanionScenery,
                    "Scenery changes remain in the draft until saved");
                scenery.IsChecked = true;
                Check(WaitFor(delegate { return CompanionScene.Scenery(embedded) && CompanionScene.Scenery(floating); }),
                    "Turning scenery on restores both previews");
                CompanionScene.RenderAt(floating, 1.2, 0, 3, null); Pump(40); byte[] idle = CompanionPixels(floating);
                CompanionScene.RenderAt(floating, 1.2, 0, .18, null); Pump(40); byte[] reaction = CompanionPixels(floating);
                CompanionScene.RenderAt(floating, 1.2, 0, 3, new AudioSignal(.7, .8, .6, .4, .9)); Pump(40); byte[] music = CompanionPixels(floating);
                Check(!System.Linq.Enumerable.SequenceEqual(idle, reaction), "Interaction produces a visible companion reaction");
                Check(!System.Linq.Enumerable.SequenceEqual(idle, music), "Audio energy produces a visible companion response");
                CompanionScene.RenderAt(floating, 1.2, 0, 3, null); CompanionScene.AdvanceForTest(floating, .1, null);
                double idleAdvance = CompanionScene.Angle(floating);
                AudioSignal energetic = new AudioSignal(.7, .8, .6, .4, .9);
                CompanionScene.RenderAt(floating, 1.2, 0, 3, energetic); CompanionScene.AdvanceForTest(floating, .1, energetic);
                Check(CompanionScene.Angle(floating) > idleAdvance * 1.2, "Real audio envelopes make the companion move more energetically");
                CompanionScene.RenderAt(floating, 1.2, 0, 0, null); CompanionScene.AdvanceForTest(floating, .1, null);
                Check(CompanionScene.Action(floating) == CompanionAction.Play && Math.Abs(CompanionScene.Angle(floating)) < .00001,
                    "A click starts a stationary play reaction instead of sliding");
                ComboBox schemes = Layouts.Find<ComboBox>(form, "ColourScheme");
                int previousScheme = schemes.SelectedIndex;
                System.Collections.Generic.HashSet<int> schemeColours = new System.Collections.Generic.HashSet<int>();
                foreach (HolidayTheme scheme in Enum.GetValues(typeof(HolidayTheme)))
                {
                    schemes.SelectedIndex = (int)scheme;
                    Check(WaitFor(delegate { return CompanionScene.Scheme(floating) == scheme; }), "The companion follows the " + scheme + " scheme");
                    CompanionScene.RenderAt(floating, 1.2, 0, 3, null); Pump(40);
                    System.Collections.Generic.HashSet<int> colours = OpaqueColours(CompanionPixels(floating));
                    Check(ColoursClose(colours), "The " + scheme + " silhouette stays monochrome");
                    schemeColours.Add(RepresentativeColour(colours));
                }
                Check(schemeColours.Count == 3, "The three colour schemes produce distinct silhouette colours");
                schemes.SelectedIndex = previousScheme;
                Check(WaitFor(delegate { return CompanionScene.Scheme(floating) == (HolidayTheme)previousScheme; }), "The preview restores its prior scheme");
                Pump(120); // Let the debounced draft refresh settle before testing the visit frames.
                CompanionScene.RenderAt(floating, 1.2, Math.PI / 2, 3, null, 3); Pump(40); byte[] away = CompanionPixels(floating);
                CompanionScene.RenderAt(floating, 1.2, Math.PI / 2, 3, null, .525); Pump(40); byte[] near = CompanionPixels(floating);
                Check(!System.Linq.Enumerable.SequenceEqual(away, near), "The occasional approach visibly moves toward the countdown");
                if (render) Render(preview, Path.Combine(root, "previews", "companion-visit.png"));
                Grid parent = Layouts.Find<Grid>(preview, "GadgetHoverArea");
                Check(parent.Children.IndexOf(floating) < parent.Children.IndexOf(Layouts.Find<Border>(preview, "DragArea")),
                    "The countdown is drawn in front of the visiting companion");
                ComboBox effects = Layouts.Find<ComboBox>(form, "OrbEffectCombo");
                int previousEffect = effects.SelectedIndex;
                foreach (OrbEffect effect in Enum.GetValues(typeof(OrbEffect)))
                {
                    effects.SelectedIndex = (int)effect;
                    Check(WaitFor(delegate { return CompanionScene.Selected(floating) == CompanionKind.Dog && CompanionScene.Effect(floating) == effect; }),
                        "The companion remains paired with the " + effect + " effect");
                }
                effects.SelectedIndex = previousEffect;
                if (render)
                {
                    CheckBox light = Layouts.Find<CheckBox>(form, "LightAppearance");
                    bool? wasLight = light.IsChecked;
                    try
                    {
                        light.IsChecked = true; choice.SelectedIndex = (int)CompanionKind.Car;
                        Check(WaitFor(delegate { return CompanionScene.Selected(floating) == CompanionKind.Car; }), "Light companion preview is ready");
                        CompanionScene.RenderAt(floating, 1.2, 0, .18, null); Pump(40);
                        Render(preview, Path.Combine(root, "previews", "companion-car-light.png"), true);
                    }
                    finally { light.IsChecked = wasLight; }
                }
                choice.SelectedIndex = 0;
                Check(WaitFor(delegate { return !CompanionScene.Contains(embedded) && !CompanionScene.Contains(floating); }),
                    "Choosing None removes both companion scenes");
                Check(CompanionScene.ActiveSceneCount == initialScenes, "Removing companions releases their scene owners");
            }
            finally
            {
                choice.SelectedIndex = 0;
                preview.Close();
                SettingsLayout.SelectSection(form, "CountdownTab");
            }
        }

        private static byte[] CompanionPixels(Canvas canvas)
        {
            canvas.UpdateLayout();
            RenderTargetBitmap bitmap = new RenderTargetBitmap(344, 344, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(canvas);
            return Pixels(bitmap);
        }

        private static void CheckLiveCompanionMotion()
        {
            if (!SystemParameters.ClientAreaAnimation || SystemParameters.HighContrast) return;
            int baseline = CompanionScene.RenderingSceneCount;
            Window host = new Window { Width = 344, Height = 344, WindowStyle = WindowStyle.None,
                ShowInTaskbar = false, ShowActivated = false, Left = -10000, Top = -10000 };
            Grid root = new Grid(); Canvas orb = new Canvas(); Canvas companion = new Canvas { Name = "CompanionCanvas" };
            root.Children.Add(orb); root.Children.Add(companion); host.Content = root;
            NameScope.SetNameScope(host, new NameScope()); host.RegisterName("OrbCanvas", orb);
            try
            {
                host.Show(); Pump(80);
                CompanionScene.Apply(companion, host, CompanionKind.Dog, HolidayTheme.Beach, false, true, OrbEffect.Glass, .6, false);
                Check(WaitFor(delegate { return CompanionScene.RenderingSceneCount == baseline + 1; }), "A visible companion attaches its live animation loop");
                double before = CompanionScene.Angle(companion); long frames = CompanionScene.FrameCount(companion);
                byte[] beforePixels = CompanionPixels(companion);
                Pump(850);
                Check(CompanionScene.FrameCount(companion) > frames + 3 && Math.Abs(CompanionScene.Angle(companion) - before) > .01,
                    "The native live companion takes steps and travels over time");
                Check(!System.Linq.Enumerable.SequenceEqual(beforePixels, CompanionPixels(companion)),
                    "The live companion visibly changes across rendered frames");
                Check(CompanionScene.React(companion) && CompanionScene.Action(companion) == CompanionAction.Play,
                    "The live companion responds to interaction");
                host.Hide(); Pump(80);
                Check(CompanionScene.RenderingSceneCount == baseline, "Hiding the companion detaches its rendering loop");
            }
            finally { CompanionScene.Stop(companion); host.Close(); }
        }

        private static byte[] ArtPixels(CompanionArt art, CompanionKind kind, double phase, double reaction, CompanionAction action = CompanionAction.Travel)
        {
            DrawingVisual visual = new DrawingVisual();
            using (DrawingContext dc = visual.RenderOpen()) art.Draw(dc, kind, phase, reaction, 0, action, phase * 28, 1.1);
            RenderTargetBitmap bitmap = new RenderTargetBitmap(64, 64, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual); return Pixels(bitmap);
        }

        private static void RenderCompanionStudies(CompanionArt art, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            CompanionKind[] kinds = { CompanionKind.Dog, CompanionKind.Dog, CompanionKind.Dog,
                CompanionKind.Cat, CompanionKind.Cat, CompanionKind.Cat, CompanionKind.Bunny,
                CompanionKind.Bird, CompanionKind.Airplane, CompanionKind.Car, CompanionKind.Bicycle, CompanionKind.Motorcycle };
            string[] labels = { "Dog walk", "Dog step", "Dog bark", "Cat walk", "Cat step", "Cat groom",
                "Bunny", "Bird", "Airplane", "Car", "Bicycle", "Motorcycle" };
            double[] phases = { 1.2, 1.7, 11.1, 1.2, 1.7, 11.1, 1.2, 1.2, 1.2, 1.2, 1.2, 1.2 };
            DrawingVisual visual = new DrawingVisual();
            using (DrawingContext dc = visual.RenderOpen())
            {
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(244, 247, 249)), null, new Rect(0, 0, 600, 560));
                for (int i = 0; i < kinds.Length; i++)
                {
                    int col = i % 3, row = i / 3;
                    dc.PushTransform(new TranslateTransform(col * 200 + 31, row * 140 + 1));
                    dc.PushTransform(new ScaleTransform(2.15, 2.15));
                    art.Draw(dc, kinds[i], phases[i], 0, 0, i == 2 || i == 5 ? CompanionAction.Play : CompanionAction.Travel,
                        phases[i] * 28, 1.1); dc.Pop(); dc.Pop();
                    FormattedText label = new FormattedText(labels[i], CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                        new Typeface("Segoe UI"), 12, Brushes.DimGray, 1.0);
                    dc.DrawText(label, new Point(col * 200 + 12, row * 140 + 118));
                }
            }
            RenderTargetBitmap image = new RenderTargetBitmap(600, 560, 96, 96, PixelFormats.Pbgra32);
            image.Render(visual); PngBitmapEncoder encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
            using (FileStream file = File.Create(path)) encoder.Save(file);
        }

        private static void RenderWalkingStudies(CompanionArt art, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            DrawingVisual visual = new DrawingVisual();
            using (DrawingContext dc = visual.RenderOpen())
            {
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(244, 247, 249)), null, new Rect(0, 0, 864, 360));
                for (int row = 0; row < 2; row++)
                {
                    CompanionKind kind = row == 0 ? CompanionKind.Dog : CompanionKind.Cat;
                    for (int frame = 0; frame < 8; frame++)
                    {
                        dc.PushTransform(new TranslateTransform(frame * 108 + 10, row * 120 + 4));
                        dc.PushTransform(new ScaleTransform(1.4, 1.4));
                        art.Draw(dc, kind, 1, 0, 0, CompanionAction.Travel,
                            frame * (CompanionMotion.Stride / CompanionMotion.StanceFraction * CompanionMotion.ArtScale) / 8, 0);
                        dc.Pop(); dc.Pop();
                    }
                }
                CompanionKind[] sleeping = { CompanionKind.Dog, CompanionKind.Cat, CompanionKind.Bunny, CompanionKind.Bird };
                for (int i = 0; i < sleeping.Length; i++)
                {
                    dc.PushTransform(new TranslateTransform(i * 216 + 46, 244));
                    dc.PushTransform(new ScaleTransform(1.4, 1.4));
                    art.Draw(dc, sleeping[i], 1, 0, 0, CompanionAction.Sleep, 0, 1);
                    dc.Pop(); dc.Pop();
                }
            }
            RenderTargetBitmap image = new RenderTargetBitmap(864, 360, 96, 96, PixelFormats.Pbgra32);
            image.Render(visual); PngBitmapEncoder encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
            using (FileStream file = File.Create(path)) encoder.Save(file);
        }

        private static System.Collections.Generic.HashSet<int> OpaqueColours(byte[] pixels)
        {
            System.Collections.Generic.HashSet<int> colours = new System.Collections.Generic.HashSet<int>();
            for (int i = 0; i + 3 < pixels.Length; i += 4)
            {
                int alpha = pixels[i + 3];
                if (alpha < 128) continue;
                // Recover colour from partially transparent antialiased pixels
                // before comparing it with the selected scheme.
                int b = Math.Min(255, (pixels[i] * 255 + alpha / 2) / alpha);
                int g = Math.Min(255, (pixels[i + 1] * 255 + alpha / 2) / alpha);
                int r = Math.Min(255, (pixels[i + 2] * 255 + alpha / 2) / alpha);
                colours.Add((r << 16) | (g << 8) | b);
            }
            return colours;
        }

        private static bool ColoursClose(System.Collections.Generic.HashSet<int> colours)
        {
            if (colours.Count == 0) return false;
            int minR = 255, minG = 255, minB = 255, maxR = 0, maxG = 0, maxB = 0;
            foreach (int colour in colours)
            {
                int r = (colour >> 16) & 255, g = (colour >> 8) & 255, b = colour & 255;
                minR = Math.Min(minR, r); maxR = Math.Max(maxR, r);
                minG = Math.Min(minG, g); maxG = Math.Max(maxG, g);
                minB = Math.Min(minB, b); maxB = Math.Max(maxB, b);
            }
            // Premultiplied-alpha and antialiasing can round a colour by a
            // few channel values even when one brush supplies the silhouette.
            return maxR - minR <= 8 && maxG - minG <= 8 && maxB - minB <= 8;
        }

        private static string ColourRanges(System.Collections.Generic.HashSet<int> colours)
        {
            if (colours.Count == 0) return "no pixels above the opacity threshold";
            int minR = 255, minG = 255, minB = 255, maxR = 0, maxG = 0, maxB = 0;
            foreach (int colour in colours)
            {
                int r = (colour >> 16) & 255, g = (colour >> 8) & 255, b = colour & 255;
                minR = Math.Min(minR, r); maxR = Math.Max(maxR, r);
                minG = Math.Min(minG, g); maxG = Math.Max(maxG, g);
                minB = Math.Min(minB, b); maxB = Math.Max(maxB, b);
            }
            return String.Format(CultureInfo.InvariantCulture, "R {0}-{1}, G {2}-{3}, B {4}-{5}", minR, maxR, minG, maxG, minB, maxB);
        }

        private static int RepresentativeColour(System.Collections.Generic.HashSet<int> colours)
        {
            int r = 0, g = 0, b = 0;
            foreach (int colour in colours) { r += (colour >> 16) & 255; g += (colour >> 8) & 255; b += colour & 255; }
            return (r / colours.Count << 16) | (g / colours.Count << 8) | b / colours.Count;
        }
    }
}
