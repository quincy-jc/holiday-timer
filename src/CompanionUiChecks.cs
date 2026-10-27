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
            Check(choice.Items.Count == 9 && choice.SelectedIndex == 0, "All eight optional companions and None are available");
            Check(!String.IsNullOrEmpty(AutomationProperties.GetName(choice)) && !String.IsNullOrEmpty(AutomationProperties.GetName(Layouts.Find<Button>(form, "TryCompanion"))),
                "Companion controls have accessible names");
            Check(!CompanionScene.Contains(embedded), "The default orb has no companion scene");
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
            Check(!CompanionScene.ShouldVisit(17, 18, 3, Math.PI / 2) && CompanionScene.ShouldVisit(18, 18, 3, Math.PI / 2) &&
                !CompanionScene.ShouldVisit(18, 18, 3, 0) && !CompanionScene.ShouldVisit(18, 18, .5, Math.PI / 2),
                "Visits wait for their interval, start at a side, and do not repeat immediately");

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
                    Check(ColoursClose(colours), "Companion " + i + " uses a single-colour silhouette");
                    Check(digits.TransformToAncestor((Visual)preview.Content).Transform(new Point()) == digitsAt &&
                        unit.TransformToAncestor((Visual)preview.Content).Transform(new Point()) == unitAt,
                        "Companion " + i + " leaves the countdown's rows stable");
                }
                choice.SelectedIndex = (int)CompanionKind.Dog;
                Check(WaitFor(delegate { return CompanionScene.Selected(floating) == CompanionKind.Dog; }), "Dog preview is ready for motion checks");
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
                Check(CompanionScene.Angle(floating) > idleAdvance, "A click reaction briefly increases the companion's motion");
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

        private static System.Collections.Generic.HashSet<int> OpaqueColours(byte[] pixels)
        {
            System.Collections.Generic.HashSet<int> colours = new System.Collections.Generic.HashSet<int>();
            for (int i = 0; i + 3 < pixels.Length; i += 4)
                if (pixels[i + 3] == 255) colours.Add((pixels[i] << 16) | (pixels[i + 1] << 8) | pixels[i + 2]);
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
            // WPF's antialiasing can round overlapping translucent edge pixels
            // by a couple of channel values even with one silhouette brush.
            return maxR - minR <= 3 && maxG - minG <= 3 && maxB - minB <= 3;
        }

        private static int RepresentativeColour(System.Collections.Generic.HashSet<int> colours)
        {
            int r = 0, g = 0, b = 0;
            foreach (int colour in colours) { r += (colour >> 16) & 255; g += (colour >> 8) & 255; b += colour & 255; }
            return (r / colours.Count << 16) | (g / colours.Count << 8) | b / colours.Count;
        }
    }
}
