using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace HolidayTimer
{
    internal static class FontChoices
    {
        private static readonly string[] names = new[] { "Inter Display" }.Concat(
            Fonts.SystemFontFamilies.Select(f => f.Source).OrderBy(s => s, StringComparer.CurrentCultureIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        internal static string[] Names { get { return names; } }
        internal static bool Available(string name) { return names.Contains(name, StringComparer.OrdinalIgnoreCase); }
        internal static FontFamily Family(string name)
        {
            return String.Equals(name, "Inter Display", StringComparison.OrdinalIgnoreCase)
                ? new FontFamily(new Uri("pack://application:,,,/"), "./fonts/#Inter Display")
                : new FontFamily(name + ", Segoe UI");
        }
        internal static void Apply(Window window, string name)
        { FontFamily family = Family(name); window.FontFamily = family; window.Resources["CountdownFont"] = family; }
    }
}
