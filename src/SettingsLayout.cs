using System;
using System.Windows;
using System.Windows.Controls;

namespace HolidayTimer
{
    internal static class SettingsLayout
    {
        internal static void Arrange(Window form)
        {
            bool narrow = form.ActualWidth < 820;
            Grid workspace = Layouts.Find<Grid>(form, "WorkspaceGrid");
            ScrollViewer options = Layouts.Find<ScrollViewer>(form, "SettingsScroller");
            Grid preview = Layouts.Find<Grid>(form, "LivePreviewPanel");
            FrameworkElement caption = Layouts.Find<FrameworkElement>(form, "LivePreviewCaption");
            FrameworkElement actions = Layouts.Find<FrameworkElement>(form, "LivePreviewActions");
            Viewbox orb = Layouts.Find<Viewbox>(form, "SettingsPreviewView");

            workspace.ColumnDefinitions[1].Width = new GridLength(narrow ? 0 : 28);
            workspace.ColumnDefinitions[2].Width = new GridLength(narrow ? 0 : 260);
            workspace.RowDefinitions[0].Height = narrow ? GridLength.Auto : new GridLength(1, GridUnitType.Star);
            workspace.RowDefinitions[1].Height = narrow ? new GridLength(1, GridUnitType.Star) : GridLength.Auto;
            Grid.SetRow(options, narrow ? 1 : 0);
            options.Margin = new Thickness(0, narrow ? 10 : 0, 0, 0);
            Grid.SetColumn(preview, narrow ? 0 : 2);
            double size = narrow ? Math.Min(168, Math.Max(100, (form.ActualHeight - 240) * .4)) : Math.Min(260, Math.Max(140, form.ActualHeight - 350));
            double scale = Layouts.Find<Slider>(form, "WidgetScale").Value / 100;
            orb.Width = orb.Height = Math.Min(size, (narrow ? size * .78 : 212) * Math.Sqrt(scale));
            preview.Height = narrow ? size : Double.NaN;
            preview.Margin = new Thickness(0, narrow ? 0 : 10, 0, 0);
            preview.ColumnDefinitions[0].Width = narrow ? new GridLength(size) : new GridLength(1, GridUnitType.Star);
            preview.ColumnDefinitions[1].Width = new GridLength(narrow ? 16 : 0);
            preview.ColumnDefinitions[2].Width = narrow ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
            preview.RowDefinitions[1].Height = narrow ? new GridLength(1, GridUnitType.Star) : new GridLength(size);
            preview.RowDefinitions[2].Height = narrow ? new GridLength(0) : GridLength.Auto;
            Grid.SetColumn(caption, narrow ? 2 : 0);
            Grid.SetRow(orb, narrow ? 0 : 1);
            Grid.SetRowSpan(orb, narrow ? 2 : 1);
            Grid.SetColumn(actions, narrow ? 2 : 0);
            Grid.SetRow(actions, narrow ? 1 : 2);
            actions.VerticalAlignment = narrow ? VerticalAlignment.Center : VerticalAlignment.Top;
        }

        internal static void SelectSection(Window form, string tabName)
        {
            TabItem section = Layouts.Find<TabItem>(form, tabName);
            section.IsSelected = true;
            Layouts.Find<ScrollViewer>(form, "SettingsScroller").ScrollToTop();
            form.UpdateLayout();
        }
    }
}
