using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace HolidayTimer
{
    internal static partial class UiChecks
    {
        private static void CheckCompactSettingsLayout(Window form, string root, bool render)
        {
            double width = form.Width, height = form.Height;
            TabControl tabs = Layouts.Find<TabControl>(form, "SettingsTabs");
            ScrollViewer options = Layouts.Find<ScrollViewer>(form, "SettingsScroller");
            Viewbox preview = Layouts.Find<Viewbox>(form, "SettingsPreviewView");
            CheckBox work = Layouts.Find<CheckBox>(form, "WorkdayCountdown"), music = Layouts.Find<CheckBox>(form, "MusicResponse");
            ComboBox effect = Layouts.Find<ComboBox>(form, "OrbEffectCombo");
            RadioButton custom = Layouts.Find<RadioButton>(form, "CustomTextColour");
            TextBox hex = Layouts.Find<TextBox>(form, "TextColourHex");
            bool? wasWork = work.IsChecked, wasMusic = music.IsChecked;
            bool wasCustom = custom.IsChecked == true;
            int wasEffect = effect.SelectedIndex;
            string wasHex = hex.Text;
            object wasTag = form.Tag;
            Check(tabs.Items.Count == 5, "Settings groups all options into five native keyboard-accessible tabs");
            form.Width = 980; form.Height = 740; form.UpdateLayout(); Pump(80);
            try
            {
                string[] sections = { "CountdownTab", "AppearanceTab", "MusicTab", "CelebrationTab", "UpdatesTab" };
                Point previewPosition = preview.TransformToAncestor((Visual)form.Content).Transform(new Point());
                foreach (string section in sections)
                {
                    SettingsLayout.SelectSection(form, section); Pump(40);
                    Check(options.ScrollableHeight <= 1, section + " fits the compact default window without a scrollbar");
                    Check(preview.IsVisible && preview.ActualWidth >= 160 && InSettingsBounds(preview, form), section + " keeps the full live orb visibly beside its controls");
                    Point current = preview.TransformToAncestor((Visual)form.Content).Transform(new Point());
                    Check(Math.Abs(current.X - previewPosition.X) < 1 && Math.Abs(current.Y - previewPosition.Y) < 1,
                        section + " leaves the live preview anchored while switching settings");
                    Check(Layouts.Find<TabItem>(form, section).Focusable, section + " remains reachable by keyboard");
                    if (render)
                    {
                        string images = Path.Combine(root, "previews"); Directory.CreateDirectory(images);
                        Render(form, Path.Combine(images, "compact-" + section.Replace("Tab", "").ToLowerInvariant() + ".png"));
                    }
                }
                work.IsChecked = true;
                SettingsLayout.SelectSection(form, "CountdownTab"); Pump(100);
                Check(Layouts.Find<StackPanel>(form, "WorkdayScheduleDetails").IsVisible && options.ScrollableHeight <= 1,
                    "A full enabled work schedule fits the compact default window without scrolling (overflow: " + options.ScrollableHeight.ToString("0.0") + ")");
                Check(InSettingsBounds(Layouts.Find<TextBox>(form, "WorkStart"), form) && InSettingsBounds(Layouts.Find<TextBox>(form, "WorkFinish"), form),
                    "Both working-hour inputs remain visible with all countdown controls");
                if (render) Render(form, Path.Combine(root, "previews", "compact-working-hours.png"));
                work.IsChecked = wasWork;
                music.IsChecked = true;
                SettingsLayout.SelectSection(form, "MusicTab"); Pump(100);
                Check(Layouts.Find<Grid>(form, "MusicDetails").IsVisible && options.ScrollableHeight <= 1,
                    "Enabled audio source and intensity fit the compact default window without scrolling");
                Check(InSettingsBounds(Layouts.Find<ComboBox>(form, "AudioSource"), form) && InSettingsBounds(Layouts.Find<Slider>(form, "MusicIntensity"), form),
                    "Music source and intensity remain visible beside the live orb");
                music.IsChecked = wasMusic;
                effect.SelectedIndex = 1; custom.IsChecked = true;
                SettingsLayout.SelectSection(form, "AppearanceTab"); Pump(100);
                Check(Layouts.Find<StackPanel>(form, "EffectIntensityDetails").IsVisible && Layouts.Find<Grid>(form, "CustomTextColourDetails").IsVisible && options.ScrollableHeight <= 1,
                    "Expanded motion and custom colour controls fit the compact default window without scrolling");
                Check(InSettingsBounds(Layouts.Find<Button>(form, "ChooseTextColour"), form) && InSettingsBounds(Layouts.Find<Slider>(form, "WidgetScale"), form),
                    "Custom colour and gadget size remain visible together with the fixed preview");
                if (render) Render(form, Path.Combine(root, "previews", "compact-expanded-appearance.png"));
                form.Width = 520; form.Height = 450; form.UpdateLayout(); Pump(80);
                SettingsLayout.SelectSection(form, "AppearanceTab");
                Point beforeScroll = preview.TransformToAncestor((Visual)form.Content).Transform(new Point());
                options.ScrollToBottom(); Pump(40);
                Point afterScroll = preview.TransformToAncestor((Visual)form.Content).Transform(new Point());
                Check(options.ScrollableHeight > 0, "Small settings windows retain scrolling for all active-tab controls");
                Check(preview.IsVisible && InSettingsBounds(preview, form) && Math.Abs(beforeScroll.X - afterScroll.X) < 1 && Math.Abs(beforeScroll.Y - afterScroll.Y) < 1,
                    "Scrolling a small options pane cannot move or hide the live preview");
                Check(InSettingsBounds(Layouts.Find<Button>(form, "SaveButton"), form) && InSettingsBounds(Layouts.Find<Button>(form, "CancelButton"), form),
                    "Save and Cancel remain visible in the smallest settings window");
                if (render) Render(form, Path.Combine(root, "previews", "compact-small-scroll.png"));
            }
            finally
            {
                work.IsChecked = wasWork; music.IsChecked = wasMusic;
                effect.SelectedIndex = wasEffect;
                if (!wasCustom) Layouts.Find<RadioButton>(form, "DefaultTextColour").IsChecked = true;
                hex.Text = wasHex;
                if (wasCustom) custom.IsChecked = true;
                form.Tag = wasTag;
                form.Width = width; form.Height = height; form.UpdateLayout();
                SettingsLayout.SelectSection(form, "CountdownTab"); Pump(60);
            }
        }

        private static bool InSettingsBounds(FrameworkElement element, Window form)
        {
            FrameworkElement root = (FrameworkElement)form.Content;
            Rect rect = element.TransformToAncestor(root).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
            return rect.Width > 0 && rect.Height > 0 && rect.Top >= -1 && rect.Left >= -1 && rect.Bottom <= root.ActualHeight + 1 && rect.Right <= root.ActualWidth + 1;
        }
    }
}
