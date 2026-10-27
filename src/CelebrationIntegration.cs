using System;
using System.Windows;
using System.Windows.Controls;

namespace HolidayTimer
{
    internal sealed partial class HolidayApplication
    {
        private static void FillCelebrationForm(Window form, AppSettings settings)
        {
            Layouts.Find<ComboBox>(form, "CelebrationStyle").SelectedIndex = (int)settings.CelebrationEffect;
            UpdateCelebrationDescription(form);
        }

        private static void BindCelebrationForm(Window form)
        {
            Layouts.Find<ComboBox>(form, "CelebrationStyle").SelectionChanged += delegate { UpdateCelebrationDescription(form); };
        }

        private static void UpdateCelebrationDescription(Window form)
        {
            bool flowers = Layouts.Find<ComboBox>(form, "CelebrationStyle").SelectedIndex == (int)CelebrationKind.Flowers;
            Layouts.Find<TextBlock>(form, "CelebrationDescription").Text = (flowers
                ? "Bouquets rise and burst into drifting petals and confetti. Made for wedding celebrations."
                : "Layered fireworks with glowing trails and falling confetti.") + " Plays until you dismiss it.";
        }

        private static bool ReadCelebrationForm(Window form, AppSettings draft, out string error)
        {
            int value = Layouts.Find<ComboBox>(form, "CelebrationStyle").SelectedIndex;
            error = null;
            if (value < 0 || value > 1) { error = "Choose fireworks or flowers for your celebration."; return false; }
            draft.CelebrationEffect = (CelebrationKind)value;
            return true;
        }
    }
}
