using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
namespace HolidayTimer
{
    internal static class Motion
    {
        internal static void Attach(DependencyObject parent, bool enabled)
        {
            if (!enabled) return;
            Button button = parent as Button;
            if (button != null)
            {
                ScaleTransform scale = new ScaleTransform(1, 1); button.RenderTransform = scale; button.RenderTransformOrigin = new Point(.5, .5);
                Action<double> change = delegate(double value)
                {
                    if (!SystemParameters.ClientAreaAnimation || SystemParameters.HighContrast) return;
                    DoubleAnimation animation = new DoubleAnimation(value, TimeSpan.FromMilliseconds(120)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                    scale.BeginAnimation(ScaleTransform.ScaleXProperty, animation); scale.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
                };
                button.PreviewMouseLeftButtonDown += delegate { change(.97); }; button.PreviewMouseLeftButtonUp += delegate { change(1); };
                button.LostMouseCapture += delegate { change(1); }; button.MouseLeave += delegate { change(1); };
            }
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) Attach(VisualTreeHelper.GetChild(parent, i), enabled);
        }
        internal static void Reveal(FrameworkElement element, bool enabled)
        {
            if (!enabled || !SystemParameters.ClientAreaAnimation || SystemParameters.HighContrast || InputManager.Current.MostRecentInputDevice is KeyboardDevice) return;
            ScaleTransform scale = new ScaleTransform(1, 1); element.RenderTransform = scale; element.RenderTransformOrigin = new Point(.5, .5);
            DoubleAnimation size = new DoubleAnimation(.96, 1, TimeSpan.FromMilliseconds(220)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop };
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, size); scale.BeginAnimation(ScaleTransform.ScaleYProperty, size);
            element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(.6, 1, TimeSpan.FromMilliseconds(180)) { FillBehavior = FillBehavior.Stop });
        }
    }
}
