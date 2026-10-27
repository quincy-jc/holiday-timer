using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace HolidayTimer
{
    internal sealed partial class HolidayApplication
    {
        private static readonly string[] EffectNames = {
            "Original glass", "Music pulse", "Liquid ribbons", "Orbital particles",
            "Water ripples", "Solar corona", "Northern lights", "Plasma vortex",
            "Magnetic waves", "Liquid metal", "Warp tunnel", "Shockwave pulses", "Comet swarm"
        };
        private static readonly string[] EffectDescriptions = {
            "The original luminous glass orb.", "A luminous ring that breathes with the music.",
            "Soft ribbons weave around a clear centre.", "Light particles follow layered orbital paths.",
            "Gentle ripples travel across a glass ring.", "A radiant corona with drifting flares.",
            "Flowing curtains of spectral light.", "Braided light spirals into a glowing vortex.",
            "Charged particles trace shifting magnetic fields.", "A fluid metal shell with moving reflections.",
            "Twisted rings and light trails move through depth.", "Elastic waves expand into fading sparks.",
            "Comets sweep through tilted orbits with luminous tails."
        };

        private static void FillAppearanceForm(Window form, AppSettings settings)
        {
            ComboBox effects = Layouts.Find<ComboBox>(form, "OrbEffectCombo");
            effects.ItemsSource = EffectNames; effects.SelectedIndex = (int)settings.Effect;
            Layouts.Find<Slider>(form, "EffectIntensitySlider").Value = settings.EffectIntensity * 100;
            string colour = AppSettings.NormalizeTextColour(settings.CountdownTextColour);
            Layouts.Find<TextBox>(form, "TextColourHex").Text = colour;
            Layouts.Find<RadioButton>(form, "DefaultTextColour").IsChecked = colour.Length == 0;
            Layouts.Find<RadioButton>(form, "CustomTextColour").IsChecked = colour.Length != 0;
            UpdateAppearanceControls(form);
        }

        private static bool ReadAppearanceForm(Window form, AppSettings draft, out string error)
        {
            error = null;
            int index = Layouts.Find<ComboBox>(form, "OrbEffectCombo").SelectedIndex;
            if (index < 0 || index >= EffectNames.Length) { error = "Choose an orb effect from the list."; return false; }
            draft.Effect = (OrbEffect)index;
            draft.EffectIntensity = Layouts.Find<Slider>(form, "EffectIntensitySlider").Value / 100;
            string colour = Layouts.Find<TextBox>(form, "TextColourHex").Text.Trim();
            if (Layouts.Find<RadioButton>(form, "CustomTextColour").IsChecked == true)
            {
                if (String.IsNullOrEmpty(colour) || !AppSettings.IsValidTextColour(colour))
                { error = "Enter a text colour as #RRGGBB, for example #B4E5FF, or choose Default."; return false; }
                draft.CountdownTextColour = AppSettings.NormalizeTextColour(colour);
            }
            else draft.CountdownTextColour = "";
            return true;
        }

        private static string PreviewTextColour(Window form)
        {
            if (Layouts.Find<RadioButton>(form, "CustomTextColour").IsChecked != true) return "";
            string value = AppSettings.NormalizeTextColour(Layouts.Find<TextBox>(form, "TextColourHex").Text.Trim());
            if (value.Length != 0) return value;
            // Keep the last valid ink while the user edits an incomplete hex value.
            return form.Tag as string ?? "";
        }

        private static void UpdateAppearanceControls(Window form)
        {
            int index = Math.Max(0, Layouts.Find<ComboBox>(form, "OrbEffectCombo").SelectedIndex);
            Layouts.Find<TextBlock>(form, "EffectDescription").Text = EffectDescriptions[Math.Min(index, EffectDescriptions.Length - 1)];
            Layouts.Find<StackPanel>(form, "EffectIntensityDetails").Visibility = index == 0 ? Visibility.Collapsed : Visibility.Visible;
            Layouts.Find<TextBlock>(form, "EffectIntensityValue").Text = Layouts.Find<Slider>(form, "EffectIntensitySlider").Value.ToString("0", CultureInfo.CurrentCulture) + "%";
            bool custom = Layouts.Find<RadioButton>(form, "CustomTextColour").IsChecked == true;
            Layouts.Find<Grid>(form, "CustomTextColourDetails").Visibility = custom ? Visibility.Visible : Visibility.Collapsed;
            string value = AppSettings.NormalizeTextColour(Layouts.Find<TextBox>(form, "TextColourHex").Text.Trim());
            if (value.Length != 0) form.Tag = value;
            Brush ink = value.Length == 0 ? form.Resources["OrbText"] as Brush : new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
            Layouts.Find<Rectangle>(form, "TextColourSwatch").Fill = ink;
        }

        private void BindAppearanceForm(Window form)
        {
            Layouts.Find<ComboBox>(form, "OrbEffectCombo").SelectionChanged += delegate { UpdateAppearanceControls(form); QueuePreviewUpdate(); };
            Layouts.Find<Slider>(form, "EffectIntensitySlider").ValueChanged += delegate { UpdateAppearanceControls(form); QueuePreviewUpdate(); };
            Layouts.Find<RadioButton>(form, "DefaultTextColour").Checked += delegate { UpdateAppearanceControls(form); UpdateDraftAppearance(form); };
            Layouts.Find<RadioButton>(form, "CustomTextColour").Checked += delegate {
                TextBox field = Layouts.Find<TextBox>(form, "TextColourHex");
                if (AppSettings.NormalizeTextColour(field.Text.Trim()).Length == 0)
                {
                    SolidColorBrush ink = form.Resources["OrbText"] as SolidColorBrush;
                    field.Text = ink == null ? "#EDF4FF" : "#" + ink.Color.R.ToString("X2") + ink.Color.G.ToString("X2") + ink.Color.B.ToString("X2");
                }
                UpdateAppearanceControls(form); UpdateDraftAppearance(form);
            };
            Layouts.Find<TextBox>(form, "TextColourHex").TextChanged += delegate { UpdateAppearanceControls(form); UpdateDraftAppearance(form); };
            Layouts.Find<Button>(form, "ChooseTextColour").Click += delegate {
                CreateColourPicker(form).ShowDialog();
            };
        }

        internal ColourPicker CreateColourPicker(Window form)
        {
            TextBox field = Layouts.Find<TextBox>(form, "TextColourHex");
            RadioButton custom = Layouts.Find<RadioButton>(form, "CustomTextColour");
            RadioButton automatic = Layouts.Find<RadioButton>(form, "DefaultTextColour");
            string previousHex = field.Text;
            bool previousCustom = custom.IsChecked == true;
            object previousPreview = form.Tag;
            string current = PreviewTextColour(form);
            SolidColorBrush defaultInk = form.Resources["OrbText"] as SolidColorBrush;
            if (current.Length == 0) current = defaultInk == null ? "#EDF4FF" : ColourPicker.Hex(defaultInk.Color);
            ColourPicker picker = new ColourPicker(form, current, delegate(string value) {
                field.Text = value;
                custom.IsChecked = true;
                UpdateDraftAppearance(form);
            });
            picker.Closed += delegate {
                if (!picker.Accepted)
                {
                    form.Tag = previousPreview;
                    if (!previousCustom) automatic.IsChecked = true;
                    field.Text = previousHex;
                    if (previousCustom) custom.IsChecked = true;
                    form.Tag = previousPreview;
                    UpdateAppearanceControls(form);
                    UpdateDraftAppearance(form);
                }
                Layouts.Find<Button>(form, "ChooseTextColour").Focus();
            };
            return picker;
        }

        private static void SetPickedColour(Window form, System.Drawing.Color colour)
        {
            Layouts.Find<TextBox>(form, "TextColourHex").Text = "#" + colour.R.ToString("X2") + colour.G.ToString("X2") + colour.B.ToString("X2");
        }

    }
}
