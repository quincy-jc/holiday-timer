using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace HolidayTimer
{
    internal static class CompanionInteraction
    {
        internal sealed class Gesture
        {
            private Point start;
            internal bool Armed { get; private set; }
            internal void Begin(Point position, bool captured) { start = position; Armed = captured; }
            internal bool StartDrag(Point position)
            {
                if (!Armed || !IsDrag(start, position)) return false;
                Armed = false; return true;
            }
            internal bool EndClick() { bool click = Armed; Armed = false; return click; }
            internal void Cancel() { Armed = false; }
        }

        // Distinguish a tap from a drag before calling DragMove, so clicking the
        // countdown can interact without changing its remembered position.
        internal static void Attach(Window window, Action moved)
        {
            Border area = Layouts.Find<Border>(window, "DragArea");
            Canvas companion = Layouts.Find<Canvas>(window, "CompanionCanvas");
            Gesture gesture = new Gesture();
            area.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                if (e.ChangedButton != MouseButton.Left || e.ClickCount != 1) return;
                Point start = e.GetPosition(window);
                gesture.Begin(start, area.CaptureMouse()); area.Focus(); e.Handled = true;
            };
            area.MouseMove += delegate(object sender, MouseEventArgs e)
            {
                if (!gesture.Armed || e.LeftButton != MouseButtonState.Pressed) return;
                Point current = e.GetPosition(window);
                if (!gesture.StartDrag(current)) return;
                area.ReleaseMouseCapture();
                try { window.DragMove(); }
                catch (InvalidOperationException) { return; } // The mouse may have been released during capture handoff.
                if (moved != null) moved();
                e.Handled = true;
            };
            area.MouseLeftButtonUp += delegate(object sender, MouseButtonEventArgs e)
            {
                if (!gesture.EndClick()) return;
                area.ReleaseMouseCapture(); CompanionScene.React(companion); e.Handled = true;
            };
            area.LostMouseCapture += delegate { gesture.Cancel(); };
            area.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key != Key.Space && e.Key != Key.Enter) return;
                CompanionScene.React(companion); e.Handled = true;
            };
        }

        internal static bool IsDrag(Point start, Point current)
        { return Math.Abs(current.X - start.X) >= SystemParameters.MinimumHorizontalDragDistance ||
            Math.Abs(current.Y - start.Y) >= SystemParameters.MinimumVerticalDragDistance; }
    }
}
