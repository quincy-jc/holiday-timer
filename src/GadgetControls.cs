using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace HolidayTimer
{
    internal static class GadgetControls
    {
        private static readonly Dictionary<Window, State> States = new Dictionary<Window, State>();

        internal static void Attach(Window window, bool animate)
        {
            if (window == null) throw new ArgumentNullException("window");
            window.VerifyAccess();
            State state;
            if (States.TryGetValue(window, out state))
            {
                state.Animate = animate;
                state.SyncLifecycle();
                if (!state.CanAnimate) state.StopAnimation();
                return;
            }
            state = new State(window, animate);
            States.Add(window, state);
            state.SyncLifecycle();
        }

        private sealed class State
        {
            private readonly Window window;
            private readonly FrameworkElement hoverArea;
            private readonly Border actionStrip;
            private readonly Border hoverBridge;
            private readonly DispatcherTimer hideTimer;
            private AnimationClock opacityClock;
            private EventHandler animationFinished;
            private DispatcherOperation focusCheck;
            private bool active, closed, keyboardMode;
            internal bool Animate, Shown;

            internal State(Window host, bool animate)
            {
                window = host;
                Animate = animate;
                hoverArea = host.FindName("GadgetHoverArea") as FrameworkElement;
                actionStrip = host.FindName("ActionStrip") as Border;
                hoverBridge = host.FindName("ActionHoverBridge") as Border;
                if (hoverArea == null || actionStrip == null || hoverBridge == null)
                    throw new InvalidOperationException("The countdown controls require the gadget hover area and action strip.");
                hideTimer = new DispatcherTimer(DispatcherPriority.Input, host.Dispatcher)
                    { Interval = TimeSpan.FromMilliseconds(180) };
                hideTimer.Tick += OnHideTimer;
                host.IsVisibleChanged += OnVisibilityChanged;
                host.StateChanged += OnStateChanged;
                host.Closed += OnClosed;
                SetShown(false, false);
            }

            internal bool CanAnimate
            { get { return Animate && SystemParameters.ClientAreaAnimation && !SystemParameters.HighContrast; } }

            private bool KeyboardHoldsControls
            { get { return keyboardMode && actionStrip.IsKeyboardFocusWithin; } }

            internal void SyncLifecycle()
            {
                if (closed) return;
                bool shouldBeActive = window.IsVisible && window.WindowState != WindowState.Minimized;
                if (active == shouldBeActive) return;
                active = shouldBeActive;
                keyboardMode = false;
                hideTimer.Stop();
                CancelFocusCheck();
                SetShown(false, false);
                if (active)
                {
                    hoverArea.MouseEnter += OnMouseEnter;
                    hoverArea.MouseLeave += OnMouseLeave;
                    window.PreviewKeyDown += OnPreviewKeyDown;
                    window.PreviewMouseDown += OnPreviewMouseDown;
                    window.Deactivated += OnDeactivated;
                    actionStrip.GotKeyboardFocus += OnGotKeyboardFocus;
                    actionStrip.LostKeyboardFocus += OnLostKeyboardFocus;
                    SystemParameters.StaticPropertyChanged += OnSystemChanged;
                    if (hoverArea.IsMouseOver) SetShown(true, false);
                }
                else UnhookInteraction();
            }

            private void UnhookInteraction()
            {
                hoverArea.MouseEnter -= OnMouseEnter;
                hoverArea.MouseLeave -= OnMouseLeave;
                window.PreviewKeyDown -= OnPreviewKeyDown;
                window.PreviewMouseDown -= OnPreviewMouseDown;
                window.Deactivated -= OnDeactivated;
                actionStrip.GotKeyboardFocus -= OnGotKeyboardFocus;
                actionStrip.LostKeyboardFocus -= OnLostKeyboardFocus;
                SystemParameters.StaticPropertyChanged -= OnSystemChanged;
            }

            private void OnMouseEnter(object sender, MouseEventArgs e)
            { if (active) { hideTimer.Stop(); SetShown(true, CanAnimate); } }

            private void OnMouseLeave(object sender, MouseEventArgs e)
            { ScheduleHide(); }

            private void ScheduleHide()
            {
                hideTimer.Stop();
                if (active && Shown && !KeyboardHoldsControls) hideTimer.Start();
            }

            private void OnHideTimer(object sender, EventArgs e)
            {
                hideTimer.Stop();
                if (active && !hoverArea.IsMouseOver && !KeyboardHoldsControls)
                    SetShown(false, CanAnimate);
            }

            private void OnPreviewKeyDown(object sender, KeyEventArgs e)
            {
                if (e.Key != Key.Tab && !actionStrip.IsKeyboardFocusWithin) return;
                keyboardMode = true;
                hideTimer.Stop();
                SetShown(true, false);
                CheckFocusAfterNavigation();
            }

            private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
            {
                // Mouse focus must not keep the controls visible after the pointer leaves.
                keyboardMode = false;
                CancelFocusCheck();
                if (!hoverArea.IsMouseOver) ScheduleHide();
            }

            private void OnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
            {
                // Only an actual key in this window establishes keyboard mode. The globally
                // remembered input device can still be a keyboard during automatic activation.
                if (!keyboardMode) return;
                hideTimer.Stop();
                SetShown(true, false);
            }

            private void OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
            { CheckFocusAfterNavigation(); }

            private void CheckFocusAfterNavigation()
            {
                CancelFocusCheck();
                focusCheck = window.Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(delegate
                {
                    focusCheck = null;
                    if (!active || !keyboardMode || actionStrip.IsKeyboardFocusWithin) return;
                    keyboardMode = false;
                    if (!hoverArea.IsMouseOver) SetShown(false, false);
                }));
            }

            private void CancelFocusCheck()
            {
                if (focusCheck == null) return;
                if (focusCheck.Status == DispatcherOperationStatus.Pending) focusCheck.Abort();
                focusCheck = null;
            }

            private void OnDeactivated(object sender, EventArgs e)
            { keyboardMode = false; CancelFocusCheck(); if (!hoverArea.IsMouseOver) ScheduleHide(); }

            internal void SetShown(bool shown, bool animate)
            {
                hideTimer.Stop();
                double from = actionStrip.Opacity;
                StopAnimation();
                Shown = shown;
                // Keep keyboard navigation available at zero opacity, while removing pointer targets.
                actionStrip.IsHitTestVisible = shown;
                hoverBridge.IsHitTestVisible = shown;
                double target = shown ? 1 : 0;
                actionStrip.Opacity = target;
                if (!active || !animate || !CanAnimate || Math.Abs(from - target) < .001) return;
                DoubleAnimation animation = new DoubleAnimation(from, target, TimeSpan.FromMilliseconds(150))
                    { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop };
                opacityClock = (AnimationClock)animation.CreateClock(true);
                AnimationClock current = opacityClock;
                animationFinished = delegate
                {
                    if (opacityClock == current) StopAnimation();
                };
                current.Completed += animationFinished;
                actionStrip.ApplyAnimationClock(UIElement.OpacityProperty, current, HandoffBehavior.SnapshotAndReplace);
            }

            internal void StopAnimation()
            {
                if (opacityClock == null) return;
                AnimationClock clock = opacityClock;
                opacityClock = null;
                clock.Completed -= animationFinished;
                animationFinished = null;
                actionStrip.ApplyAnimationClock(UIElement.OpacityProperty, null);
                clock.Controller.Remove();
            }

            private void OnSystemChanged(object sender, PropertyChangedEventArgs e)
            {
                if (e.PropertyName != "ClientAreaAnimation" && e.PropertyName != "HighContrast") return;
                if (window.Dispatcher.HasShutdownStarted || window.Dispatcher.HasShutdownFinished) return;
                window.Dispatcher.BeginInvoke(new Action(delegate
                {
                    if (active && !closed && !CanAnimate) StopAnimation();
                }));
            }

            private void OnVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
            { SyncLifecycle(); }

            private void OnStateChanged(object sender, EventArgs e)
            { SyncLifecycle(); }

            private void OnClosed(object sender, EventArgs e)
            {
                closed = true;
                active = false;
                keyboardMode = false;
                hideTimer.Stop();
                hideTimer.Tick -= OnHideTimer;
                CancelFocusCheck();
                SetShown(false, false);
                UnhookInteraction();
                window.IsVisibleChanged -= OnVisibilityChanged;
                window.StateChanged -= OnStateChanged;
                window.Closed -= OnClosed;
                States.Remove(window);
            }
        }
    }
}
