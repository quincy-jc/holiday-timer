using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace HolidayTimer
{
    internal static class Themes
    {
        private static SolidColorBrush Brush(string hex, double opacity)
        {
            Color colour = (Color)ColorConverter.ConvertFromString(hex);
            colour.A = (byte)Math.Round(255 * opacity);
            SolidColorBrush brush = new SolidColorBrush(colour); brush.Freeze(); return brush;
        }
        private static DropShadowEffect TextShadow(bool light, double blur, double opacity)
        {
            // Replace the complete immutable effect when appearance changes. This
            // keeps its rendered shader parameters in sync with the theme.
            DropShadowEffect shadow = new DropShadowEffect
            {
                Color = light ? Colors.White : Color.FromRgb(3, 11, 23),
                BlurRadius = blur, ShadowDepth = 0, Opacity = opacity
            };
            shadow.Freeze();
            return shadow;
        }
        internal static void Apply(Window window, HolidayTheme theme, double opacity, bool light = false, string textColour = "")
        {
            string accent, secondary, highlight;
            if (theme == HolidayTheme.Sunset)
            { accent = "#BDA3FF"; secondary = "#F08ECB"; highlight = "#8B6EF3"; }
            else if (theme == HolidayTheme.Festive)
            { accent = "#FFBE7B"; secondary = "#FF858F"; highlight = "#E89963"; }
            else
            { accent = "#80BCFF"; secondary = "#5CE1E6"; highlight = "#7169F4"; }
            if (light)
            {
                accent = theme == HolidayTheme.Sunset ? "#7653B5" : theme == HolidayTheme.Festive ? "#9D4A31" : "#265EA8";
                secondary = theme == HolidayTheme.Sunset ? "#AC397D" : theme == HolidayTheme.Festive ? "#B2475C" : "#087D88";
            }
            window.Resources["LightAppearance"] = light;
            window.Resources["PageBackground"] = Brush(light ? "#F4F7FC" : "#101725", 1);
            window.Resources["PanelSurface"] = Brush(light ? "#F4F7FC" : "#101725", opacity);
            window.Resources["Heading"] = Brush(light ? "#101C30" : "#F4F7FC", 1);
            window.Resources["BodyText"] = Brush(light ? "#162235" : "#E1E8F3", 1);
            window.Resources["ErrorText"] = Brush(light ? "#A12D25" : "#FFB3AA", 1);
            window.Resources["Accent"] = Brush(accent, 1);
            window.Resources["MutedText"] = Brush(light ? "#25364D" : "#C4D2E7", 1);
            window.Resources["PanelTextShadowColour"] = (Color)ColorConverter.ConvertFromString(light ? "#FFFFFF" : "#030B17");
            window.Resources["PanelTextShadowBlur"] = light ? 1.0 : 3.0;
            window.Resources["PanelTextShadowEffect"] = TextShadow(light, light ? 1.0 : 3.0, 1);
            window.Resources["OrbPreviewTextShadowEffect"] = window.Resources["PanelTextShadowEffect"];
            window.Resources["Line"] = Brush(light ? "#8E9AA9" : "#344158", .9);
            window.Resources["Highlight"] = Brush(accent, 1);
            window.Resources["SecondaryAccent"] = Brush(secondary, 1);
            Color selectionBase = (Color)ColorConverter.ConvertFromString(light ? "#FFFFFF" : "#172132");
            Color selectionAccent = (Color)ColorConverter.ConvertFromString(accent);
            double mix = light ? .12 : .20;
            SolidColorBrush selection = new SolidColorBrush(Color.FromRgb(
                (byte)Math.Round(selectionBase.R * (1 - mix) + selectionAccent.R * mix),
                (byte)Math.Round(selectionBase.G * (1 - mix) + selectionAccent.G * mix),
                (byte)Math.Round(selectionBase.B * (1 - mix) + selectionAccent.B * mix)));
            selection.Freeze(); window.Resources["HoverSurface"] = selection;
            window.Resources["ControlSurface"] = Brush(light ? "#FFFFFF" : "#172132", .92);
            window.Resources["PopupSurface"] = Brush(light ? "#F7F9FD" : "#151E2D", 1);
            window.Resources["OnAccent"] = Brush(light ? "#FFFFFF" : "#08111F", 1);
            // The centre tint and its shading share one opacity owner. The coloured
            // hollow shell is a sibling, so changing translucency keeps it vivid.
            window.Resources["OrbMaterialOpacity"] = opacity;
            // A broad feather belongs only to the material. The count and
            // coloured animation remain independent, fully crisp siblings.
            RadialGradientBrush materialMask = new RadialGradientBrush
            { Center = new Point(.5, .5), GradientOrigin = new Point(.5, .5), RadiusX = .5, RadiusY = .5 };
            double[] featherOffsets = { 0, .48, .55, .62, .69, .77, .84, .91, .96, 1 };
            byte[] featherAlpha = { 255, 255, 249, 227, 181, 104, 39, 7, 0, 0 };
            for (int i = 0; i < featherOffsets.Length; i++)
                materialMask.GradientStops.Add(new GradientStop(Color.FromArgb(featherAlpha[i], 255, 255, 255), featherOffsets[i]));
            materialMask.Freeze(); window.Resources["OrbMaterialMask"] = materialMask;
            Color glowColour = (Color)ColorConverter.ConvertFromString(accent);
            RadialGradientBrush softGlow = new RadialGradientBrush
            { Center = new Point(.5, .5), GradientOrigin = new Point(.5, .5), RadiusX = .5, RadiusY = .5 };
            double[] glowOffsets = { 0, .48, .60, .73, .84, .94, 1 };
            byte[] glowAlpha = { 0, 0, 9, 23, 14, 2, 0 };
            for (int i = 0; i < glowOffsets.Length; i++)
                softGlow.GradientStops.Add(new GradientStop(Color.FromArgb(glowAlpha[i], glowColour.R, glowColour.G, glowColour.B), glowOffsets[i]));
            softGlow.Freeze(); window.Resources["OrbSoftGlow"] = softGlow;
            window.Resources["WidgetSurface"] = Brush(light ? "#F4F7FC" : "#101725", 1);
            window.Resources["UtilitySurface"] = Brush(light ? "#F4F7FC" : "#101725", Math.Max(.9, opacity));
            window.Resources["OrbText"] = Brush(light ? "#101C30" : "#EDF4FF", 1);
            window.Resources["OrbSecondaryText"] = Brush(light ? "#162235" : "#DBE6F8", 1);
            window.Resources["OrbMutedText"] = Brush(light ? "#24344A" : "#B9CAE3", 1);
            window.Resources["OrbShadowColour"] = (Color)ColorConverter.ConvertFromString(light ? "#FFFFFF" : "#030B17");
            window.Resources["OrbShadowBlur"] = light ? 1.25 : 5.0;
            window.Resources["OrbTextShadowEffect"] = TextShadow(light, light ? 1.25 : 5.0, .9);
            window.Resources["GreetingText"] = Brushes.White;
            window.Resources["GreetingNumberText"] = window.Resources["Highlight"];
            DropShadowEffect greetingShadow = new DropShadowEffect
            {
                Color = Color.FromRgb(7, 30, 39), BlurRadius = 12, ShadowDepth = 2, Opacity = .85
            };
            greetingShadow.Freeze();
            window.Resources["GreetingTextShadowEffect"] = greetingShadow;
            string customInk = AppSettings.NormalizeTextColour(textColour);
            if (customInk.Length != 0)
            {
                SolidColorBrush ink = Brush(customInk, 1);
                window.Resources["OrbText"] = ink;
                window.Resources["OrbSecondaryText"] = ink;
                window.Resources["OrbMutedText"] = ink;
                window.Resources["GreetingText"] = ink;
                window.Resources["GreetingNumberText"] = ink;
                window.Resources["OrbShadowColour"] = Colors.Transparent;
                window.Resources["OrbShadowBlur"] = 0.0;
                window.Resources["OrbTextShadowEffect"] = null;
                window.Resources["OrbPreviewTextShadowEffect"] = null;
                window.Resources["GreetingTextShadowEffect"] = null;
            }
            byte red = light ? (byte)247 : (byte)6, green = light ? (byte)249 : (byte)15, blue = light ? (byte)253 : (byte)32;
            RadialGradientBrush scrim = new RadialGradientBrush { Center = new Point(.5, .5), GradientOrigin = new Point(.5, .5), RadiusX = .5, RadiusY = .5 };
            scrim.GradientStops.Add(new GradientStop(Color.FromArgb(230, red, green, blue), 0));
            scrim.GradientStops.Add(new GradientStop(Color.FromArgb(217, red, green, blue), .62));
            scrim.GradientStops.Add(new GradientStop(Color.FromArgb(166, red, green, blue), .82));
            scrim.GradientStops.Add(new GradientStop(Color.FromArgb(0, red, green, blue), 1));
            scrim.Freeze(); window.Resources["CountdownScrim"] = scrim;
            window.Resources["WidgetTint"] = Brush(highlight, .12);
            window.Resources["CelebrationColours"] = new Color[] {
                (Color)ColorConverter.ConvertFromString(accent),
                (Color)ColorConverter.ConvertFromString(secondary),
                (Color)ColorConverter.ConvertFromString(highlight)
            };
        }
        internal static string Name(HolidayTheme theme)
        { return theme == HolidayTheme.Sunset ? "Violet" : theme == HolidayTheme.Festive ? "Ember" : "Aurora"; }
    }
}
