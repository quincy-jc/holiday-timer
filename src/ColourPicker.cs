using System;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;

namespace HolidayTimer
{
    // A colour choice is a draft transaction. The settings owner commits it only
    // with its own Save; this window never writes preferences or alters palettes.
    internal sealed class ColourPicker : Window
    {
        internal bool Accepted { get; private set; }
        internal string SelectedHex { get; private set; }
        private readonly Action<string> preview;
        private readonly Slider hue, saturation, brightness;
        private readonly TextBox hexField;
        private readonly Button apply;
        private readonly TextBlock error, sample, hueValue, saturationValue, brightnessValue;
        private readonly Border colourField, currentSwatch;
        private readonly Canvas selectionLayer;
        private readonly Ellipse selector;
        private readonly Button[] swatches;
        private bool updating;

        internal ColourPicker(Window owner, string initialHex, Action<string> onPreview)
        {
            Owner = owner;
            Title = "Holiday Timer — Text colour";
            Width = Math.Min(440, Math.Max(280, SystemParameters.WorkArea.Width - 24));
            Height = Math.Min(730, Math.Max(280, SystemParameters.WorkArea.Height - 24));
            MinWidth = Math.Min(320, Width); MinHeight = Math.Min(350, Height);
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true; Background = Brushes.Transparent;
            ShowInTaskbar = false; FontFamily = owner.FontFamily;
            UseLayoutRounding = true; SnapsToDevicePixels = true;
            preview = onPreview;
            Resources.MergedDictionaries.Add(Layouts.LoadControls());
            foreach (object key in owner.Resources.Keys)
            {
                // The settings panel's implicit text styles add shadows for
                // live glass. This opaque popup uses the shared control styles.
                if (Object.Equals(key, typeof(TextBlock)) || Object.Equals(key, typeof(Label))) continue;
                Resources[key] = owner.Resources[key];
            }
            if (SystemParameters.HighContrast)
            {
                Resources["PopupSurface"] = SystemColors.WindowBrush;
                Resources["ControlSurface"] = SystemColors.WindowBrush;
                Resources["Heading"] = Resources["BodyText"] = Resources["MutedText"] = SystemColors.WindowTextBrush;
                Resources["Line"] = SystemColors.WindowTextBrush;
                Resources["Accent"] = SystemColors.HighlightBrush;
                Resources["OnAccent"] = SystemColors.HighlightTextBrush;
            }
            SetResourceReference(ForegroundProperty, "BodyText");
            NameScope.SetNameScope(this, new NameScope());
            Grid layout = new Grid { Margin = new Thickness(24) };
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition());
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Border surface = new Border { CornerRadius = new CornerRadius(14), BorderThickness = new Thickness(1), Child = layout };
            surface.SetResourceReference(Border.BackgroundProperty, "PopupSurface");
            surface.SetResourceReference(Border.BorderBrushProperty, "Line");
            Content = surface;
            Grid header = new Grid { Margin = new Thickness(0, 0, 0, 22) };
            header.ColumnDefinitions.Add(new ColumnDefinition());
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            StackPanel title = new StackPanel();
            title.Children.Add(Text("Text colour", 24, "Heading", FontWeights.SemiBold));
            TextBlock subtitle = Text("Choose the ink. See it on your countdown.", 12, "MutedText", FontWeights.Normal);
            subtitle.Margin = new Thickness(0, 7, 0, 0); title.Children.Add(subtitle);
            title.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e) { if (e.ClickCount == 1) DragMove(); };
            header.Children.Add(title);
            Button close = Button("ColourClose", "", "Cancel colour selection");
            close.Padding = new Thickness(10); close.Background = Brushes.Transparent; close.BorderThickness = new Thickness(0);
            Path cross = new Path { Data = Geometry.Parse("M 0 0 L 9 9 M 9 0 L 0 9"), StrokeThickness = 1.4, Width = 9, Height = 9 };
            cross.SetResourceReference(Path.StrokeProperty, "MutedText"); close.Content = cross;
            close.VerticalAlignment = VerticalAlignment.Top; close.Click += delegate { Close(); };
            Grid.SetColumn(close, 1); header.Children.Add(close); layout.Children.Add(header);

            ScrollViewer scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Margin = new Thickness(0, 0, -5, 0) };
            RegisterName("ColourScroller", scroll); Grid.SetRow(scroll, 1); layout.Children.Add(scroll);
            StackPanel body = new StackPanel { Margin = new Thickness(0, 0, 5, 0) }; scroll.Content = body;
            Grid fieldLayers = new Grid { ClipToBounds = true };
            fieldLayers.SizeChanged += delegate { fieldLayers.Clip = new RectangleGeometry(new Rect(0, 0, fieldLayers.ActualWidth, fieldLayers.ActualHeight), 10, 10); };
            LinearGradientBrush white = new LinearGradientBrush(Colors.White, Colors.Transparent, new Point(0, .5), new Point(1, .5));
            LinearGradientBrush black = new LinearGradientBrush(Colors.Transparent, Colors.Black, new Point(.5, 0), new Point(.5, 1));
            fieldLayers.Children.Add(new Rectangle { Fill = white });
            fieldLayers.Children.Add(new Rectangle { Fill = black });
            selectionLayer = new Canvas { IsHitTestVisible = false };
            selectionLayer.SizeChanged += delegate { PositionSelector(); };
            selector = new Ellipse { Width = 18, Height = 18, Fill = Brushes.Transparent, Stroke = Brushes.White, StrokeThickness = 2.5 };
            selector.Effect = null;
            selectionLayer.Children.Add(new Ellipse { Width = 21, Height = 21, Fill = Brushes.Transparent, Stroke = Brushes.Black, StrokeThickness = 1 });
            selectionLayer.Children.Add(selector); fieldLayers.Children.Add(selectionLayer);
            colourField = new Border { Height = 176, CornerRadius = new CornerRadius(10), Child = fieldLayers, Focusable = true, Cursor = Cursors.Cross, Margin = new Thickness(0, 0, 0, 12) };
            RegisterName("ColourField", colourField);
            AutomationProperties.SetName(colourField, "Saturation and brightness. Arrow keys adjust; Shift makes larger changes.");
            colourField.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e) { colourField.Focus(); colourField.CaptureMouse(); PickAt(e.GetPosition(colourField)); e.Handled = true; };
            colourField.MouseMove += delegate(object sender, MouseEventArgs e) { if (colourField.IsMouseCaptured && e.LeftButton == MouseButtonState.Pressed) PickAt(e.GetPosition(colourField)); };
            colourField.MouseLeftButtonUp += delegate { colourField.ReleaseMouseCapture(); };
            colourField.KeyDown += AdjustField;
            colourField.GotKeyboardFocus += delegate { colourField.BorderThickness = new Thickness(2); colourField.SetResourceReference(Border.BorderBrushProperty, "Accent"); };
            colourField.LostKeyboardFocus += delegate { colourField.BorderThickness = new Thickness(0); };
            colourField.SizeChanged += delegate { PositionSelector(); };
            body.Children.Add(colourField);

            hue = MakeSlider("ColourHue", "Hue", 360);
            hueValue = SliderHeading(body, "_Hue", hue, "0°");
            Grid hueStrip = new Grid { Margin = new Thickness(0, 0, 0, 12), Height = 26 };
            LinearGradientBrush spectrum = new LinearGradientBrush { StartPoint = new Point(0, .5), EndPoint = new Point(1, .5) };
            for (int i = 0; i <= 6; i++) spectrum.GradientStops.Add(new GradientStop(FromHsv(i * 60, 1, 1), i / 6.0));
            hueStrip.Children.Add(new Border { Height = 12, CornerRadius = new CornerRadius(6), Background = spectrum });
            hue.Template = (ControlTemplate)XamlReader.Parse("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Slider'><Grid><Track x:Name='PART_Track' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'><Track.DecreaseRepeatButton><RepeatButton Command='Slider.DecreaseLarge' Opacity='0' Focusable='False'/></Track.DecreaseRepeatButton><Track.IncreaseRepeatButton><RepeatButton Command='Slider.IncreaseLarge' Opacity='0' Focusable='False'/></Track.IncreaseRepeatButton><Track.Thumb><Thumb Width='18' Height='22'><Thumb.Template><ControlTemplate TargetType='Thumb'><Border CornerRadius='6' BorderBrush='Black' BorderThickness='1' Background='Transparent'><Border CornerRadius='5' BorderBrush='White' BorderThickness='2' Background='Transparent'/></Border></ControlTemplate></Thumb.Template></Thumb></Track.Thumb></Track><Border x:Name='Focus' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' BorderBrush='{DynamicResource Accent}' BorderThickness='1' CornerRadius='6' Margin='-3' Visibility='Collapsed' IsHitTestVisible='False'/></Grid><ControlTemplate.Triggers><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='Focus' Property='Visibility' Value='Visible'/></Trigger></ControlTemplate.Triggers></ControlTemplate>");
            hueStrip.Children.Add(hue); body.Children.Add(hueStrip);
            saturation = MakeSlider("ColourSaturation", "Saturation", 100);
            saturationValue = SliderHeading(body, "_Saturation", saturation, "0%"); body.Children.Add(saturation);
            brightness = MakeSlider("ColourBrightness", "Brightness", 100);
            brightnessValue = SliderHeading(body, "_Brightness", brightness, "0%"); body.Children.Add(brightness);
            saturation.Margin = brightness.Margin = new Thickness(0, 0, 0, 12);
            hue.ValueChanged += SliderChanged; saturation.ValueChanged += SliderChanged; brightness.ValueChanged += SliderChanged;

            TextBlock paletteLabel = Text("A few good colours", 12, "BodyText", FontWeights.Medium);
            paletteLabel.Margin = new Thickness(0, 8, 0, 8); body.Children.Add(paletteLabel);
            UniformGrid palette = new UniformGrid { Columns = 6, Rows = 2, Margin = new Thickness(-3, 0, -3, 16) };
            string[] options = { "#EDF4FF", "#101C30", "#80BCFF", "#5CE1E6", "#BDA3FF", "#F08ECB", "#FFFFFF", "#000000", "#FFBE7B", "#FF858F", "#7653B5", "#265EA8" };
            string[] names = { "Cloud", "Ink", "Aurora blue", "Lagoon", "Violet", "Rose", "White", "Black", "Ember", "Coral", "Deep violet", "Deep blue" };
            swatches = new Button[options.Length];
            for (int i = 0; i < options.Length; i++)
            {
                string value = options[i]; Button swatch = Button("ColourSwatch" + i.ToString(CultureInfo.InvariantCulture), "", names[i] + ", " + value);
                swatch.Padding = new Thickness(4); swatch.Margin = new Thickness(3); swatch.Height = 34; swatch.Tag = value;
                swatch.Content = new Border { Width = 24, Height = 18, CornerRadius = new CornerRadius(4), Background = new SolidColorBrush(Parse(value)) };
                swatch.ToolTip = names[i] + " · " + value;
                swatch.Click += delegate { SetHex(value, true); };
                swatches[i] = swatch; palette.Children.Add(swatch);
            }
            body.Children.Add(palette);
            Grid exact = new Grid(); exact.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); exact.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) }); exact.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            StackPanel entry = new StackPanel();
            hexField = new TextBox { MaxLength = 7, FontSize = 15, Padding = new Thickness(12, 9, 12, 9) };
            RegisterName("ColourHex", hexField); AutomationProperties.SetName(hexField, "Exact text colour, hexadecimal RGB");
            Label hexLabel = new Label { Content = "_HEX", Target = hexField }; entry.Children.Add(hexLabel); entry.Children.Add(hexField);
            exact.Children.Add(entry);
            StackPanel previewPanel = new StackPanel(); Grid.SetColumn(previewPanel, 2);
            previewPanel.Children.Add(Text("Preview", 12, "BodyText", FontWeights.Medium));
            sample = new TextBlock { Text = "Aa 72", FontSize = 18, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Effect = null };
            currentSwatch = new Border { CornerRadius = new CornerRadius(10), Height = 40, Margin = new Thickness(0, 7, 0, 0), Child = sample };
            currentSwatch.SetResourceReference(Border.BackgroundProperty, "PageBackground"); previewPanel.Children.Add(currentSwatch); exact.Children.Add(previewPanel); body.Children.Add(exact);
            error = Text("", 11, "BodyText", FontWeights.Normal); error.TextWrapping = TextWrapping.Wrap; error.Visibility = Visibility.Collapsed; error.Margin = new Thickness(0, 0, 0, 10); RegisterName("ColourError", error);
            hexField.TextChanged += HexChanged;

            Grid footer = new Grid { Margin = new Thickness(0, 16, 0, 0) };
            footer.ColumnDefinitions.Add(new ColumnDefinition()); footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            footer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); footer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetColumnSpan(error, 2); footer.Children.Add(error);
            Button cancel = Button("ColourCancel", "_Cancel", "Cancel colour selection"); cancel.HorizontalAlignment = HorizontalAlignment.Left; cancel.Background = Brushes.Transparent; cancel.BorderThickness = new Thickness(0); cancel.Click += delegate { Close(); }; Grid.SetRow(cancel, 1); footer.Children.Add(cancel);
            apply = Button("ColourApply", "_Apply colour", "Apply colour to the settings draft");
            apply.SetResourceReference(Control.BackgroundProperty, "Accent"); apply.SetResourceReference(Control.ForegroundProperty, "OnAccent"); apply.BorderThickness = new Thickness(0); apply.IsDefault = true;
            apply.Click += delegate { if (AppSettings.IsValidTextColour(hexField.Text.Trim())) { SetHex(hexField.Text.Trim(), true); Accepted = true; Close(); } };
            Grid.SetColumn(apply, 1); Grid.SetRow(apply, 1); footer.Children.Add(apply); Grid.SetRow(footer, 2); layout.Children.Add(footer);
            KeyboardNavigation.SetTabNavigation(layout, KeyboardNavigationMode.Cycle);
            PreviewKeyDown += delegate(object sender, KeyEventArgs e) { if (e.Key == Key.Escape) { e.Handled = true; Close(); } };
            SetHex(initialHex, false);
            Loaded += delegate { PositionSelector(); hexField.Focus(); hexField.SelectAll(); };
        }

        private static TextBlock Text(string value, double size, string brush, FontWeight weight)
        { TextBlock block = new TextBlock { Text = value, FontSize = size, FontWeight = weight, Effect = null }; block.SetResourceReference(TextBlock.ForegroundProperty, brush); return block; }
        private Button Button(string name, string content, string accessible)
        { Button button = new Button { Content = content }; RegisterName(name, button); AutomationProperties.SetName(button, accessible); return button; }
        private Slider MakeSlider(string name, string accessible, double maximum)
        { Slider slider = new Slider { Minimum = 0, Maximum = maximum, SmallChange = 1, LargeChange = 10, IsMoveToPointEnabled = true }; RegisterName(name, slider); AutomationProperties.SetName(slider, accessible); return slider; }
        private static TextBlock SliderHeading(StackPanel panel, string title, Slider target, string value)
        { Grid row = new Grid(); Label label = new Label { Content = title, Target = target }; row.Children.Add(label); TextBlock output = Text(value, 12, "MutedText", FontWeights.Normal); output.HorizontalAlignment = HorizontalAlignment.Right; row.Children.Add(output); panel.Children.Add(row); return output; }
        private void SliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        { if (!updating) SetFromSliders(true); }
        private void SetFromSliders(bool notify)
        {
            SelectedHex = Hex(FromHsv(hue.Value, saturation.Value / 100, brightness.Value / 100));
            updating = true; hexField.Text = SelectedHex; updating = false;
            ShowColour(notify);
        }
        private void HexChanged(object sender, TextChangedEventArgs e)
        {
            if (updating) return;
            string normalized = AppSettings.NormalizeTextColour(hexField.Text.Trim());
            if (normalized.Length == 0) { error.Text = "Use six colour digits, like #80BCFF."; error.Visibility = Visibility.Visible; apply.IsEnabled = false; return; }
            SetHex(normalized, true, false);
        }
        internal void SetHex(string value, bool notify)
        { SetHex(value, notify, true); }
        private void SetHex(string value, bool notify, bool updateField)
        {
            string normalized = AppSettings.NormalizeTextColour(value);
            if (normalized.Length == 0) return;
            SelectedHex = normalized;
            Color colour = Parse(normalized);
            double h, s, v; ToHsv(colour, out h, out s, out v);
            updating = true;
            // Achromatic ink keeps its hue, so saturation can bring colour back.
            if (s > .00001) hue.Value = h;
            saturation.Value = s * 100; brightness.Value = v * 100;
            if (updateField) hexField.Text = normalized;
            updating = false; ShowColour(notify);
        }
        private void ShowColour(bool notify)
        {
            colourField.Background = new SolidColorBrush(FromHsv(hue.Value, 1, 1));
            sample.Foreground = new SolidColorBrush(Parse(SelectedHex));
            hueValue.Text = hue.Value.ToString("0", CultureInfo.CurrentCulture) + "°";
            saturationValue.Text = saturation.Value.ToString("0", CultureInfo.CurrentCulture) + "%";
            brightnessValue.Text = brightness.Value.ToString("0", CultureInfo.CurrentCulture) + "%";
            error.Text = ""; error.Visibility = Visibility.Collapsed; apply.IsEnabled = true;
            foreach (Button swatch in swatches)
            {
                bool selected = (string)swatch.Tag == SelectedHex;
                swatch.BorderThickness = new Thickness(selected ? 2 : 1);
                swatch.SetResourceReference(Control.BorderBrushProperty, selected ? "Accent" : "Line");
                AutomationProperties.SetHelpText(swatch, selected ? "Selected colour" : "Choose this colour");
            }
            PositionSelector();
            if (notify && preview != null) preview(SelectedHex);
        }
        private void PickAt(Point point)
        {
            double width = Math.Max(1, colourField.ActualWidth - colourField.BorderThickness.Left - colourField.BorderThickness.Right);
            double height = Math.Max(1, colourField.ActualHeight - colourField.BorderThickness.Top - colourField.BorderThickness.Bottom);
            updating = true;
            saturation.Value = Math.Max(0, Math.Min(100, (point.X - colourField.BorderThickness.Left) / width * 100));
            brightness.Value = Math.Max(0, Math.Min(100, (1 - (point.Y - colourField.BorderThickness.Top) / height) * 100));
            updating = false; SetFromSliders(true);
        }
        private void AdjustField(object sender, KeyEventArgs e)
        {
            double step = (Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? 10 : 1;
            if (e.Key == Key.Left) saturation.Value -= step;
            else if (e.Key == Key.Right) saturation.Value += step;
            else if (e.Key == Key.Up) brightness.Value += step;
            else if (e.Key == Key.Down) brightness.Value -= step;
            else return;
            e.Handled = true;
        }
        private void PositionSelector()
        {
            if (saturation == null || brightness == null) return;
            double x = Math.Max(11, Math.Min(selectionLayer.ActualWidth - 11, saturation.Value / 100 * selectionLayer.ActualWidth));
            double y = Math.Max(11, Math.Min(selectionLayer.ActualHeight - 11, (1 - brightness.Value / 100) * selectionLayer.ActualHeight));
            Canvas.SetLeft(selector, x - 9); Canvas.SetTop(selector, y - 9);
            Canvas.SetLeft(selectionLayer.Children[0], x - 10.5); Canvas.SetTop(selectionLayer.Children[0], y - 10.5);
        }
        internal static string Hex(Color colour)
        { return "#" + colour.R.ToString("X2") + colour.G.ToString("X2") + colour.B.ToString("X2"); }
        private static Color Parse(string value)
        { return (Color)ColorConverter.ConvertFromString(value); }
        internal static Color FromHsv(double hue, double saturation, double value)
        {
            double h = ((hue % 360) + 360) % 360 / 60, chroma = value * saturation;
            double x = chroma * (1 - Math.Abs(h % 2 - 1)), m = value - chroma;
            double r = 0, g = 0, b = 0;
            if (h < 1) { r = chroma; g = x; } else if (h < 2) { r = x; g = chroma; }
            else if (h < 3) { g = chroma; b = x; } else if (h < 4) { g = x; b = chroma; }
            else if (h < 5) { r = x; b = chroma; } else { r = chroma; b = x; }
            return Color.FromRgb((byte)Math.Round((r + m) * 255), (byte)Math.Round((g + m) * 255), (byte)Math.Round((b + m) * 255));
        }
        internal static void ToHsv(Color colour, out double hue, out double saturation, out double value)
        {
            double r = colour.R / 255.0, g = colour.G / 255.0, b = colour.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), delta = max - min;
            value = max; saturation = max == 0 ? 0 : delta / max;
            hue = delta == 0 ? 0 : max == r ? 60 * (((g - b) / delta) % 6) : max == g ? 60 * ((b - r) / delta + 2) : 60 * ((r - g) / delta + 4);
            if (hue < 0) hue += 360;
        }
    }
}
