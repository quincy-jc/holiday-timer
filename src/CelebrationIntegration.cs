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
            Layouts.Find<CheckBox>(form, "AlwaysAnimateCelebrations").IsChecked = settings.AlwaysAnimateCelebrations;
            UpdateCelebrationDescription(form);
            UpdateCelebrationMotionDescription(form);
        }

        private static void BindCelebrationForm(Window form)
        {
            Layouts.Find<ComboBox>(form, "CelebrationStyle").SelectionChanged += delegate { UpdateCelebrationDescription(form); };
            CheckBox alwaysAnimate = Layouts.Find<CheckBox>(form, "AlwaysAnimateCelebrations");
            alwaysAnimate.Checked += delegate { UpdateCelebrationMotionDescription(form); };
            alwaysAnimate.Unchecked += delegate { UpdateCelebrationMotionDescription(form); };
            form.Activated += delegate { UpdateCelebrationMotionDescription(form); };
        }

        private static void UpdateCelebrationMotionDescription(Window form)
        {
            bool alwaysAnimate = Layouts.Find<CheckBox>(form, "AlwaysAnimateCelebrations").IsChecked == true;
            Layouts.Find<TextBlock>(form, "CelebrationMotionStatus").Text = SystemParameters.HighContrast
                ? "Windows High Contrast is on, so celebrations show text only."
                : alwaysAnimate ? "Fireworks, flowers and confetti play even when Windows animations are off."
                : !SystemParameters.ClientAreaAnimation ? "Windows animations are off, so only text is shown. Turn this on to play the celebration effects."
                : "Follows Windows animation effects. Turn on to allow celebration effects independently.";
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
            draft.AlwaysAnimateCelebrations = Layouts.Find<CheckBox>(form, "AlwaysAnimateCelebrations").IsChecked == true;
            return true;
        }
    }
}
