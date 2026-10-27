using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace HolidayTimer
{
    internal static partial class UiChecks
    {
        private static void CheckEffectsControls(HolidayApplication app, Window form, SettingsStore store, string root, bool render)
        {
            ComboBox effects = Layouts.Find<ComboBox>(form, "OrbEffectCombo");
            Slider intensity = Layouts.Find<Slider>(form, "EffectIntensitySlider");
            RadioButton automatic = Layouts.Find<RadioButton>(form, "DefaultTextColour");
            RadioButton custom = Layouts.Find<RadioButton>(form, "CustomTextColour");
            TextBox hex = Layouts.Find<TextBox>(form, "TextColourHex");
            Check(effects.Items.Count == 13 && effects.SelectedIndex == (int)OrbEffect.Glass,
                "The effect picker exposes glass and all twelve new native effects");
            Check(intensity.Minimum == 0 && intensity.Maximum == 100 && intensity.Value == 65,
                "Effect motion intensity starts at its saved 65% default and includes both endpoints");
            Check(Layouts.Find<FrameworkElement>(form, "EffectIntensityDetails").Visibility == Visibility.Collapsed,
                "The unchanged glass effect leaves no unused motion controls");
            Check(automatic.IsChecked == true && Layouts.Find<FrameworkElement>(form, "CustomTextColourDetails").Visibility == Visibility.Collapsed,
                "Countdown text starts with the appearance default and no unused colour fields");
            Check(!String.IsNullOrEmpty(AutomationProperties.GetName(effects)) && !String.IsNullOrEmpty(AutomationProperties.GetName(hex)) &&
                !String.IsNullOrEmpty(AutomationProperties.GetName(Layouts.Find<Button>(form, "ChooseTextColour"))),
                "Effect and colour inputs retain accessible control names");

            Check(app.GadgetPreview == null, "Effect checks begin without changing an existing desktop preview");
            app.ShowGadgetPreview(form);
            Window preview = app.GadgetPreview;
            TextBlock digits = Layouts.Find<TextBlock>(preview, "RemainingNumber");
            TextBlock unit = Layouts.Find<TextBlock>(preview, "RemainingUnit");
            Visual content = (Visual)preview.Content;
            Point numberPosition = digits.TransformToAncestor(content).Transform(new Point());
            Point unitPosition = unit.TransformToAncestor(content).Transform(new Point());
            string number = digits.Text, unitText = unit.Text;
            double fontSize = digits.FontSize, materialOpacity = Layouts.Find<Grid>(preview, "OrbMaterial").Opacity;
            Color defaultText = Color.FromRgb(237, 244, 255);
            Color[] originalPalette = (Color[])((Color[])form.Resources["CelebrationColours"]).Clone();
            Check(EffectsBrushColour(digits.Foreground) == defaultText, "Default dark countdown ink uses the actual #EDF4FF appearance colour");
            try
            {
                foreach (OrbEffect effect in Enum.GetValues(typeof(OrbEffect)))
                {
                    Canvas currentCanvas = Layouts.Find<Canvas>(preview, "OrbCanvas");
                    object previousArtwork = currentCanvas.Children.Count == 0 ? null : currentCanvas.Children[0];
                    effects.SelectedIndex = (int)effect;
                    Check(WaitFor(delegate
                    {
                        Canvas canvas = Layouts.Find<Canvas>(preview, "OrbCanvas");
                        return effect == OrbEffect.Glass ? !NativeOrbEffects.Contains(canvas) && canvas.Children.Count == 8 :
                            NativeOrbEffects.Contains(canvas) && canvas.Children.Count > 0 && canvas.Children[0] != previousArtwork;
                    }), "The " + effect + " selection reaches the floating preview without saving");
                    AppSettings draft; string error;
                    Check(app.ReadForm(form, false, out draft, out error) && draft.Effect == effect,
                        "The form carries its " + effect + " effect into the draft");
                    Check(!String.IsNullOrWhiteSpace(Layouts.Find<TextBlock>(form, "EffectDescription").Text),
                        effect + " explains its preview behavior");
                    preview.UpdateLayout();
                    Check(digits.Text == number && unit.Text == unitText && digits.FontSize == fontSize &&
                        digits.TransformToAncestor(content).Transform(new Point()) == numberPosition &&
                        unit.TransformToAncestor(content).Transform(new Point()) == unitPosition,
                        effect + " leaves the real countdown value, unit and text geometry stable");
                    Check(preview.Opacity == 1 && digits.Opacity == 1 && Layouts.Find<Canvas>(preview, "OrbCanvas").Opacity == 1 &&
                        Math.Abs(Layouts.Find<Grid>(preview, "OrbMaterial").Opacity - materialOpacity) < .001,
                        effect + " preserves text and centre opacity independently of its artwork");
                    if (render && effect != OrbEffect.Glass)
                    {
                        Canvas canvas = Layouts.Find<Canvas>(preview, "OrbCanvas");
                        AudioSignal synthetic = new AudioSignal(.7, .9, .5, .4, .8);
                        NativeOrbEffects.SetMusic(canvas, synthetic, .65, effect == OrbEffect.MusicPulse);
                        NativeOrbEffects.RenderAt(canvas, 2.4, synthetic);
                        string images = Path.Combine(root, "previews"); Directory.CreateDirectory(images);
                        Render(preview, Path.Combine(images, "effect-" + ((int)effect).ToString("00", CultureInfo.InvariantCulture) + "-" + effect.ToString().ToLowerInvariant() + ".png"));
                    }
                }

                effects.SelectedIndex = (int)OrbEffect.SolarCorona;
                intensity.Value = 0;
                AppSettings quiet; string failure;
                Check(app.ReadForm(form, false, out quiet, out failure) && quiet.EffectIntensity == 0,
                    "The subtle motion endpoint is represented in the form draft");
                intensity.Value = 100;
                Check(app.ReadForm(form, false, out quiet, out failure) && quiet.EffectIntensity == 1 &&
                    Layouts.Find<FrameworkElement>(form, "EffectIntensityDetails").Visibility == Visibility.Visible && intensity.IsEnabled,
                    "A native effect exposes its energetic endpoint without changing the countdown");

                custom.IsChecked = true;
                hex.Text = "#a4d5ff";
                Color chosen = Color.FromRgb(164, 213, 255);
                Check(WaitFor(delegate
                {
                    return EffectsBrushColour(digits.Foreground) == chosen &&
                        EffectsBrushColour(Layouts.Find<TextBlock>(form, "SettingsPreviewNumber").Foreground) == chosen &&
                        EffectsBrushColour(Layouts.Find<Rectangle>(form, "TextColourSwatch").Fill) == chosen;
                }), "Valid custom RGB updates both real countdowns and the colour swatch live");
                Check(digits.Effect == null && ((UIElement)digits.Parent).Effect == null &&
                    Layouts.Find<TextBlock>(form, "SettingsPreviewNumber").Effect == null &&
                    ((UIElement)Layouts.Find<TextBlock>(form, "SettingsPreviewNumber").Parent).Effect == null,
                    "Custom text has no shadow or outline in the real gadget or embedded preview");
                Check(Layouts.Find<FrameworkElement>(form, "CustomTextColourDetails").Visibility == Visibility.Visible && hex.IsEnabled,
                    "Choosing Custom exposes the editable colour field");
                AppSettings coloured;
                Check(app.ReadForm(form, false, out coloured, out failure) && coloured.CountdownTextColour == "#A4D5FF",
                    "The custom colour enters the draft in canonical RGB form");
                CheckCustomGreetingColour(app, coloured, chosen, originalPalette, root, render);
                if (render) CaptureCustomColourControls(form, root);
                hex.Text = "#12345G";
                Check(!app.ReadForm(form, false, out coloured, out failure) && !String.IsNullOrWhiteSpace(failure),
                    "Invalid custom colour cannot enter a saveable form draft");
                Pump(160);
                Check(EffectsBrushColour(digits.Foreground) == chosen &&
                    EffectsBrushColour(Layouts.Find<TextBlock>(form, "SettingsPreviewNumber").Foreground) == chosen,
                    "An invalid in-progress hex edit retains the last valid ink after the preview timer refreshes");
                Check(app.Settings.Effect == OrbEffect.Glass && app.Settings.EffectIntensity == .65 && app.Settings.CountdownTextColour == "" && !File.Exists(store.FilePath),
                    "Effect, intensity and colour previews leave saved preferences and the settings file untouched");

                automatic.IsChecked = true;
                Check(WaitFor(delegate
                {
                    return app.ReadForm(form, false, out coloured, out failure) && coloured.CountdownTextColour == "" &&
                        EffectsBrushColour(digits.Foreground) == defaultText &&
                        EffectsBrushColour(Layouts.Find<TextBlock>(form, "SettingsPreviewNumber").Foreground) == defaultText;
                }), "Default text colour clears an invalid custom draft and restores appearance-based text");
                Check(Layouts.Find<FrameworkElement>(form, "CustomTextColourDetails").Visibility == Visibility.Collapsed,
                    "Default text colour removes the unused custom fields");
                Check(!app.MusicCaptureRequested, "Native effect UI checks never capture real playback");
            }
            finally
            {
                effects.SelectedIndex = (int)OrbEffect.Glass;
                intensity.Value = 65;
                automatic.IsChecked = true;
                hex.Text = "";
                Check(WaitFor(delegate { return !NativeOrbEffects.Contains(Layouts.Find<Canvas>(form, "SettingsOrbCanvas")); }),
                    "Effect checks restore the original embedded glass renderer");
                if (app.GadgetPreview == preview) preview.Close();
                Layouts.Find<ScrollViewer>(form, "SettingsScroller").ScrollToTop();
            }
        }

        private static Color EffectsBrushColour(Brush brush)
        { SolidColorBrush solid = brush as SolidColorBrush; return solid == null ? Colors.Transparent : solid.Color; }

        private static void CheckCustomGreetingColour(HolidayApplication app, AppSettings draft, Color chosen, Color[] expectedPalette, string root, bool render)
        {
            app.ShowGreeting(draft, DateTime.Now, false);
            Window greeting = app.Greeting;
            try
            {
                greeting.UpdateLayout();
                Check(Layouts.Find<StackPanel>(greeting, "GreetingMessage").Effect == null,
                    "The custom greeting has no text shadow or outline");
                foreach (string name in new string[] { "GreetingHeading", "GreetingIntro", "GreetingNumber", "GreetingUnit", "GreetingHoliday", "GreetingTarget" })
                    Check(EffectsBrushColour(Layouts.Find<TextBlock>(greeting, name).Foreground) == chosen,
                        "The real " + name + " uses the selected opaque custom RGB colour");
                Color[] actualPalette = greeting.Resources["CelebrationColours"] as Color[];
                bool samePalette = actualPalette != null && actualPalette.Length == expectedPalette.Length;
                if (samePalette)
                    for (int i = 0; i < expectedPalette.Length; i++) samePalette &= expectedPalette[i] == actualPalette[i];
                Check(samePalette, "Custom celebration text leaves the selected fireworks and confetti palette unchanged");
                if (render)
                {
                    string images = Path.Combine(root, "previews"); Directory.CreateDirectory(images);
                    Render(greeting, Path.Combine(images, "greeting-custom-colour.png"));
                }
            }
            finally { if (app.Greeting == greeting) greeting.Close(); }
        }

        private static void CaptureCustomColourControls(Window form, string root)
        {
            double width = form.Width, height = form.Height;
            bool wasLight = Layouts.Find<RadioButton>(form, "AppearanceLight").IsChecked == true;
            string images = Path.Combine(root, "previews"); Directory.CreateDirectory(images);
            try
            {
                Layouts.Find<RadioButton>(form, "AppearanceDark").IsChecked = true;
                Check(WaitFor(delegate { return !(bool)form.Resources["LightAppearance"]; }), "Custom text controls render in the dark appearance");
                RevealCustomColourControls(form);
                Render(form, Path.Combine(images, "settings-custom-dark.png"));
                Layouts.Find<RadioButton>(form, "AppearanceLight").IsChecked = true;
                Check(WaitFor(delegate { return (bool)form.Resources["LightAppearance"]; }), "Custom text controls render in the light appearance");
                RevealCustomColourControls(form);
                Render(form, Path.Combine(images, "settings-custom-light.png"), true);
                form.Width = 520; form.Height = 690;
                form.UpdateLayout();
                RevealCustomColourControls(form);
                Render(form, Path.Combine(images, "settings-custom-narrow.png"), true);
            }
            finally
            {
                form.Width = width; form.Height = height;
                Layouts.Find<RadioButton>(form, wasLight ? "AppearanceLight" : "AppearanceDark").IsChecked = true;
                form.UpdateLayout();
                Pump(160);
            }
        }

        private static void RevealCustomColourControls(Window form)
        {
            form.UpdateLayout();
            FrameworkElement details = Layouts.Find<FrameworkElement>(form, "CustomTextColourDetails");
            details.BringIntoView(new Rect(0, -32, details.ActualWidth, details.ActualHeight + 64));
            Layouts.Find<TextBox>(form, "TextColourHex").Focus();
            Pump(80);
            ScrollViewer viewport = Layouts.Find<ScrollViewer>(form, "SettingsScroller");
            Rect bounds = details.TransformToAncestor(viewport).TransformBounds(new Rect(0, 0, details.ActualWidth, details.ActualHeight));
            Check(bounds.Top >= -1 && bounds.Bottom <= viewport.ActualHeight + 1 && bounds.Left >= -1 && bounds.Right <= viewport.ActualWidth + 1,
                "Custom hex and colour picker are visibly reachable together in the settings viewport");
        }
    }
}
