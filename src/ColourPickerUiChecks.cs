using System;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace HolidayTimer
{
    internal static partial class UiChecks
    {
        private static void CheckColourPickerControls(HolidayApplication app, Window form, SettingsStore store, string root, bool render)
        {
            TextBox ownerHex = Layouts.Find<TextBox>(form, "TextColourHex");
            RadioButton custom = Layouts.Find<RadioButton>(form, "CustomTextColour");
            RadioButton automatic = Layouts.Find<RadioButton>(form, "DefaultTextColour");
            string oldHex = ownerHex.Text;
            object oldTag = form.Tag;
            bool oldCustom = custom.IsChecked == true;
            bool oldLight = Layouts.Find<RadioButton>(form, "AppearanceLight").IsChecked == true;
            int oldTheme = Layouts.Find<ComboBox>(form, "ColourScheme").SelectedIndex;
            ColourPicker picker = null;
            try
            {
                Check(ColourPicker.Hex(ColourPicker.FromHsv(0, 1, 1)) == "#FF0000" &&
                    ColourPicker.Hex(ColourPicker.FromHsv(360, 1, 1)) == "#FF0000" &&
                    ColourPicker.Hex(ColourPicker.FromHsv(120, 1, 1)) == "#00FF00" &&
                    ColourPicker.Hex(ColourPicker.FromHsv(240, 1, 1)) == "#0000FF",
                    "Colour picker hue includes all primary colours and wraps at 360 degrees");
                foreach (string rgb in new string[] { "#000000", "#FFFFFF", "#101C30", "#EDF4FF", "#80BCFF", "#5CE1E6", "#7653B5", "#FF858F", "#0102FE", "#A4D5FF" })
                {
                    Color original = (Color)ColorConverter.ConvertFromString(rgb);
                    double h, s, v; ColourPicker.ToHsv(original, out h, out s, out v);
                    Check(ColourPicker.FromHsv(h, s, v) == original, "Exact RGB " + rgb + " survives the colour field model without recolouring");
                }

                automatic.IsChecked = true; ownerHex.Text = "";
                object defaultTag = form.Tag;
                picker = app.CreateColourPicker(form); picker.Show(); Pump(60); picker.UpdateLayout();
                Check(picker.Owner == form && !picker.ShowInTaskbar && picker.WindowStyle == WindowStyle.None && picker.FontFamily == form.FontFamily,
                    "The native WPF colour picker is owned by settings, uses its font and has no legacy dialog chrome");
                CheckPickerTextEffects(picker);
                Check(picker.Width <= SystemParameters.WorkArea.Width && picker.Height <= SystemParameters.WorkArea.Height &&
                    !picker.HasAnimatedProperties, "The picker fits the scaled work area and uses immediate static controls for reduced motion");
                Check(automatic.IsChecked == true && ownerHex.Text == "" && Object.Equals(defaultTag, form.Tag),
                    "Opening the picker leaves the existing default draft and preview state unchanged");
                foreach (string name in new string[] { "ColourHue", "ColourSaturation", "ColourBrightness", "ColourHex", "ColourField", "ColourApply", "ColourCancel", "ColourClose", "ColourSwatch0", "ColourSwatch11" })
                    Check(!String.IsNullOrEmpty(AutomationProperties.GetName(Layouts.Find<DependencyObject>(picker, name))), name + " has an accessible name");
                Check(Layouts.Find<Button>(picker, "ColourApply").IsDefault &&
                    KeyboardNavigation.GetTabNavigation((DependencyObject)((Border)picker.Content).Child) == KeyboardNavigationMode.Cycle,
                    "Picker keyboard navigation stays within the protected colour transaction and Enter applies valid ink");

                picker.SetHex("#12abcd", true);
                Color chosen = Color.FromRgb(18, 171, 205);
                Check(WaitFor(delegate { return EffectsBrushColour(Layouts.Find<TextBlock>(form, "SettingsPreviewNumber").Foreground) == chosen; }) &&
                    custom.IsChecked == true && ownerHex.Text == "#12ABCD", "Valid picker changes update the unsaved real countdown preview immediately");
                string valid = picker.SelectedHex;
                TextBox exact = Layouts.Find<TextBox>(picker, "ColourHex"); exact.Text = "#12ABCZ";
                Check(!Layouts.Find<Button>(picker, "ColourApply").IsEnabled && picker.SelectedHex == valid &&
                    !String.IsNullOrWhiteSpace(Layouts.Find<TextBlock>(picker, "ColourError").Text) && ownerHex.Text == valid,
                    "An invalid HEX edit explains recovery, disables Apply and keeps the last valid ink preview");
                exact.Text = "#ABC123";
                Check(Layouts.Find<Button>(picker, "ColourApply").IsEnabled && picker.SelectedHex == "#ABC123" && ownerHex.Text == "#ABC123",
                    "A valid HEX edit recovers without requiring another picker opening");
                picker.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(picker), 0, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
                Check(!picker.IsVisible && !picker.Accepted && automatic.IsChecked == true && ownerHex.Text == "" && Object.Equals(form.Tag, defaultTag),
                    "Escape cancels the whole picker transaction and restores the exact prior default state");
                Check(WaitFor(delegate { return EffectsBrushColour(Layouts.Find<TextBlock>(form, "SettingsPreviewNumber").Foreground) == Color.FromRgb(237, 244, 255); }),
                    "Cancel also restores the theme-default countdown preview");
                picker = null;

                custom.IsChecked = true; ownerHex.Text = "#A4D5FF";
                object lastValidTag = form.Tag;
                ownerHex.Text = "#12345G";
                picker = app.CreateColourPicker(form); picker.Show(); Pump(30);
                picker.SetHex("#FF858F", true);
                Layouts.Find<Button>(picker, "ColourCancel").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(custom.IsChecked == true && ownerHex.Text == "#12345G" && Object.Equals(form.Tag, lastValidTag),
                    "Cancel restores an incomplete custom edit and its previous valid preview rather than committing the temporary choice");
                Check(WaitFor(delegate { return EffectsBrushColour(Layouts.Find<TextBlock>(form, "SettingsPreviewNumber").Foreground) == Color.FromRgb(164, 213, 255); }),
                    "An incomplete restored custom edit retains its original last-valid countdown ink");
                picker = null;

                ownerHex.Text = "#A4D5FF";
                picker = app.CreateColourPicker(form); picker.Show(); Pump(30);
                Layouts.Find<Button>(picker, "ColourSwatch8").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(picker.SelectedHex == "#FFBE7B" && ownerHex.Text == "#FFBE7B", "Curated swatches preview their exact opaque RGB values");
                Layouts.Find<Button>(picker, "ColourApply").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                AppSettings draft; string failure;
                Check(picker.Accepted && !picker.IsVisible && app.ReadForm(form, false, out draft, out failure) && draft.CountdownTextColour == "#FFBE7B",
                    "Apply keeps the chosen colour only in the settings draft");
                Check(app.Settings.CountdownTextColour == "" && !File.Exists(store.FilePath), "Opening, previewing and applying a colour never saves settings or changes the installed preferences");
                picker = null;

                foreach (bool light in new bool[] { false, true })
                    for (int theme = 0; theme < 3; theme++)
                    {
                        Layouts.Find<RadioButton>(form, light ? "AppearanceLight" : "AppearanceDark").IsChecked = true;
                        Layouts.Find<ComboBox>(form, "ColourScheme").SelectedIndex = theme;
                        Check(WaitFor(delegate { return (bool)form.Resources["LightAppearance"] == light; }), "Picker owner switches its requested appearance before capture");
                        picker = app.CreateColourPicker(form); picker.Show(); Pump(40); picker.UpdateLayout();
                        CheckPickerTextEffects(picker);
                        Check(EffectsBrushColour((Brush)picker.Resources["PopupSurface"]) == EffectsBrushColour((Brush)form.Resources["PopupSurface"]) &&
                            EffectsBrushColour((Brush)picker.Resources["Accent"]) == EffectsBrushColour((Brush)form.Resources["Accent"]),
                            "Picker surface and selection colours match " + (light ? "Light" : "Dark") + " scheme " + theme);
                        Border field = Layouts.Find<Border>(picker, "ColourField");
                        picker.SetHex("#80BCFF", false);
                        double before = Layouts.Find<Slider>(picker, "ColourSaturation").Value;
                        field.Focus(); field.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(picker), 0, Key.Left) { RoutedEvent = Keyboard.KeyDownEvent });
                        Check(Math.Abs(Layouts.Find<Slider>(picker, "ColourSaturation").Value - Math.Max(0, before - 1)) < .001,
                            "Colour field has precise keyboard adjustment in every theme");
                        Check(field.IsKeyboardFocused && field.BorderThickness.Left == 2, "Keyboard focus is visibly drawn on the colour field");
                        if (render)
                        {
                            string images = Path.Combine(root, "previews"); Directory.CreateDirectory(images);
                            Render(picker, Path.Combine(images, "colour-picker-" + (light ? "light-" : "dark-") + theme + ".png"));
                            if (light && theme == 1)
                            {
                                picker.Width = 320; picker.Height = 470; picker.UpdateLayout();
                                exact = Layouts.Find<TextBox>(picker, "ColourHex"); exact.Text = "#12G"; exact.BringIntoView(); Pump(30);
                                ScrollViewer viewport = Layouts.Find<ScrollViewer>(picker, "ColourScroller");
                                Rect rect = exact.TransformToAncestor(viewport).TransformBounds(new Rect(0, 0, exact.ActualWidth, exact.ActualHeight));
                                Check(rect.Left >= -1 && rect.Right <= viewport.ActualWidth + 1 && rect.Top >= -1 && rect.Bottom <= viewport.ActualHeight + 1,
                                    "Exact HEX remains reachable in a compact picker viewport");
                                Check(Layouts.Find<Button>(picker, "ColourCancel").IsVisible && Layouts.Find<Button>(picker, "ColourApply").IsVisible,
                                    "Compact picker retains its fixed Apply and Cancel actions");
                                Render(picker, Path.Combine(images, "colour-picker-light-compact-error.png"));
                            }
                        }
                        picker.Close(); picker = null;
                    }
            }
            finally
            {
                if (picker != null && picker.IsVisible) picker.Close();
                Layouts.Find<ComboBox>(form, "ColourScheme").SelectedIndex = oldTheme;
                Layouts.Find<RadioButton>(form, oldLight ? "AppearanceLight" : "AppearanceDark").IsChecked = true;
                if (!oldCustom) automatic.IsChecked = true;
                ownerHex.Text = oldHex; form.Tag = oldTag;
                if (oldCustom) custom.IsChecked = true;
                Layouts.Find<ScrollViewer>(form, "SettingsScroller").ScrollToTop(); Pump(120);
            }
        }

        private static void CheckPickerTextEffects(ColourPicker picker)
        {
            int labels = 0, textBlocks = 0;
            CheckPickerTextEffects((DependencyObject)picker.Content, ref labels, ref textBlocks);
            Check(labels >= 4 && textBlocks >= 4, "Picker shadow checks inspect actual rendered labels and generated text");
            foreach (string name in new string[] { "ColourApply", "ColourCancel" })
            {
                int buttonLabels = 0, buttonText = 0;
                CheckPickerTextEffects(Layouts.Find<Button>(picker, name), ref buttonLabels, ref buttonText);
                Check(buttonText > 0, name + " shadow checks inspect its actual generated button text");
            }
        }

        private static void CheckPickerTextEffects(DependencyObject visual, ref int labels, ref int textBlocks)
        {
            Label label = visual as Label;
            if (label != null)
            {
                labels++;
                Check(label.Effect == null, "Picker label has no inherited glass-panel text effect");
            }
            TextBlock text = visual as TextBlock;
            if (text != null)
            {
                textBlocks++;
                Check(text.Effect == null, "Picker rendered text has no added shadow or outline");
            }
            UIElement element = visual as UIElement;
            if (element != null && (element is Button || element is ContentPresenter || element is AccessText))
                Check(element.Effect == null, "Picker button text container has no added effect");
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(visual); i++)
                CheckPickerTextEffects(VisualTreeHelper.GetChild(visual, i), ref labels, ref textBlocks);
        }
    }
}
