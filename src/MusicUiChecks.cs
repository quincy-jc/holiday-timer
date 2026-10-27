using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace HolidayTimer
{
    internal static partial class UiChecks
    {
        private static void CheckMusicControls(HolidayApplication app, Window form, SettingsStore store, string root, bool render)
        {
            CheckBox enabled = Layouts.Find<CheckBox>(form, "MusicResponse");
            Slider intensity = Layouts.Find<Slider>(form, "MusicIntensity");
            ComboBox source = Layouts.Find<ComboBox>(form, "AudioSource");
            Check(enabled.IsChecked == false, "Music response is visibly opt-in");
            Check(!intensity.IsEnabled && !source.IsEnabled, "Audio controls are disabled until music response is on");
            Check(Layouts.Find<Grid>(form, "MusicDetails").Visibility == Visibility.Collapsed, "Inactive music details leave no empty layout footprint");
            Check(source.SelectedIndex == 0 && intensity.Value == 35, "Music source and intensity reflect safe defaults");
            enabled.IsChecked = true;
            intensity.Value = 85;
            source.Items.Add(new ComboBoxItem { Content = "Saved test headphones", Tag = "test-output" });
            source.SelectedIndex = 1;
            Check(WaitFor(delegate {
                string status = Layouts.Find<TextBlock>(form, "MusicStatus").Text;
                bool paused = SystemParameters.HighContrast || !SystemParameters.ClientAreaAnimation;
                return intensity.IsEnabled && source.IsEnabled && (paused ? status.Contains("Windows accessibility settings") : status.Contains("Play music"));
            }),
                "Enabling music makes its controls available and updates preview status");
            AppSettings draft; string error;
            Check(app.ReadForm(form, false, out draft, out error) && draft.MusicResponse && draft.AudioDeviceId == "test-output" && Math.Abs(draft.MusicIntensity - .85) < .001,
                "The form carries enabled/source/intensity together into its draft");
            Check(!app.Settings.MusicResponse && app.Settings.AudioDeviceId == "" && app.Settings.MusicIntensity == .35 && !File.Exists(store.FilePath),
                "Editing music controls leaves the saved countdown untouched");
            Check(!app.MusicCaptureRequested, "Isolated UI checks never request real playback capture");
            CheckMusicViewport(form);
            if (render)
            {
                string images = Path.Combine(root, "previews"); Directory.CreateDirectory(images);
                Render(form, Path.Combine(images, "music-settings-dark.png"));
                Layouts.Find<RadioButton>(form, "AppearanceLight").IsChecked = true;
                Check(WaitFor(delegate { return (bool)form.Resources["LightAppearance"]; }), "Music controls use the current light appearance in their preview");
                Render(form, Path.Combine(images, "music-settings-light.png"), true);
                double width = form.Width, height = form.Height;
                form.Width = 520; form.Height = 690; form.UpdateLayout();
                Layouts.Find<Grid>(form, "MusicDetails").BringIntoView(); Pump(80);
                CheckMusicViewport(form);
                Render(form, Path.Combine(images, "music-settings-narrow.png"), true);
                form.Width = width; form.Height = height; form.UpdateLayout();
                Layouts.Find<RadioButton>(form, "AppearanceDark").IsChecked = true;
                Check(WaitFor(delegate { return !(bool)form.Resources["LightAppearance"]; }), "Music preview restores the original dark appearance");
            }
            string name = Layouts.Find<TextBox>(form, "HolidayName").Text;
            Layouts.Find<TextBox>(form, "HolidayName").Text = "";
            enabled.IsChecked = false;
            Check(WaitFor(delegate { return Layouts.Find<TextBlock>(form, "MusicStatus").Text.StartsWith("Off."); }),
                "Music can be turned off while another draft field is incomplete");
            Check(Layouts.Find<Grid>(form, "MusicDetails").Visibility == Visibility.Collapsed, "Disabling music collapses its fields immediately");
            Layouts.Find<TextBox>(form, "HolidayName").Text = name;
            source.SelectedIndex = 0;
            intensity.Value = 35;
            Layouts.Find<ScrollViewer>(form, "SettingsScroller").ScrollToTop();
            Check(WaitFor(delegate { AppSettings restored; string failure; return app.ReadForm(form, false, out restored, out failure) && !restored.MusicResponse && restored.AudioDeviceId == ""; }),
                "Returning to default music controls clears only the draft selection");
            CheckMusicArtwork(app, root, render);
        }

        private static void CheckMusicViewport(Window form)
        {
            SettingsLayout.SelectSection(form, "MusicTab");
            ScrollViewer viewport = Layouts.Find<ScrollViewer>(form, "SettingsScroller");
            Grid details = Layouts.Find<Grid>(form, "MusicDetails");
            Rect bounds = details.TransformToAncestor(viewport).TransformBounds(new Rect(0, 0, details.ActualWidth, details.ActualHeight));
            Check(bounds.Top >= -1 && bounds.Bottom <= viewport.ActualHeight + 1,
                "Enabled source, intensity and endpoints are fully visible together in the settings viewport");
            Check(Layouts.Find<Viewbox>(form, "SettingsPreviewView").IsVisible,
                "Music source controls keep the reacting orb preview visible");
        }

        private static void CheckMusicArtwork(HolidayApplication app, string root, bool render)
        {
            Window orb = Layouts.Load("WidgetWindow");
            try
            {
                AppSettings settings = new AppSettings { HolidayName = "Music preview", TargetLocal = "2026-12-25T09:00:00" };
                app.ApplyGadgetAppearance(orb, settings);
                orb.Show(); orb.UpdateLayout();
                Canvas canvas = Layouts.Find<Canvas>(orb, "OrbCanvas");
                TextBlock digits = Layouts.Find<TextBlock>(orb, "RemainingNumber");
                TextBlock unit = Layouts.Find<TextBlock>(orb, "RemainingUnit");
                Point numberPosition = digits.TransformToAncestor((Visual)orb.Content).Transform(new Point());
                Point unitPosition = unit.TransformToAncestor((Visual)orb.Content).Transform(new Point());
                double fontSize = digits.FontSize, numberHeight = digits.ActualHeight;
                Brush mask = canvas.OpacityMask;
                int layers = canvas.Children.Count;
                AudioSignal maximum = new AudioSignal(1, 1, 1, 1, 1);
                foreach (bool light in new bool[] { false, true })
                {
                    settings.LightAppearance = light;
                    settings.BackgroundOpacity = .35;
                    app.ApplyGadgetAppearance(orb, settings);
                    canvas = Layouts.Find<Canvas>(orb, "OrbCanvas");
                    mask = canvas.OpacityMask;
                    OrbScene.SetMusic(canvas, maximum, 1, true);
                    orb.UpdateLayout();
                    Check(canvas.Children.Count == layers && canvas.Opacity == 1 && Object.ReferenceEquals(canvas.OpacityMask, mask),
                        "Music preserves the coloured-layer structure and fixed centre mask in " + (light ? "Light" : "Dark"));
                    Check(Math.Abs(Layouts.Find<Grid>(orb, "OrbMaterial").Opacity - .35) < .001,
                        "Music leaves the translucent centre at the user's opacity");
                    Check(digits.TransformToAncestor((Visual)orb.Content).Transform(new Point()) == numberPosition &&
                        unit.TransformToAncestor((Visual)orb.Content).Transform(new Point()) == unitPosition &&
                        digits.FontSize == fontSize && digits.ActualHeight == numberHeight,
                        "Maximum music energy keeps the countdown and labels fixed");
                    if (render)
                    {
                        string images = Path.Combine(root, "previews"); Directory.CreateDirectory(images);
                        Render(orb, Path.Combine(images, light ? "music-orb-light.png" : "music-orb-dark.png"), light);
                    }
                    OrbScene.SetMusic(canvas, AudioSignal.Zero, 1, false);
                }
                Check(!app.MusicCaptureRequested && !app.Settings.MusicResponse, "Synthetic artwork checks leave playback and preferences off");
            }
            finally { OrbScene.Stop(Layouts.Find<Canvas>(orb, "OrbCanvas")); orb.Close(); }
        }
    }
}
