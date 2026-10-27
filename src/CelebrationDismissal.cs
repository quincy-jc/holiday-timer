using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;

namespace HolidayTimer
{
    // The celebration owns one Escape scope. It does not monitor keyboard input
    // or retain a shortcut after its visible, non-minimized lifetime.
    internal static class CelebrationDismissal
    {
        internal const int HotKeyId = 0x484F;
        internal const int HotKeyMessage = 0x0312;
        internal const uint EscapeVirtualKey = 0x1B;
        internal const uint NoRepeat = 0x4000;
        private static readonly DependencyProperty ScopeProperty = DependencyProperty.RegisterAttached(
            "Scope", typeof(Scope), typeof(CelebrationDismissal), new PropertyMetadata(null));

        internal interface IEscapeRegistration
        {
            bool Register(IntPtr handle, int id);
            void Unregister(IntPtr handle, int id);
            int LastError { get; }
        }
        private sealed class NativeRegistration : IEscapeRegistration
        {
            public int LastError { get; private set; }
            public bool Register(IntPtr handle, int id)
            {
                bool success = RegisterHotKey(handle, id, NoRepeat, EscapeVirtualKey);
                LastError = success ? 0 : Marshal.GetLastWin32Error();
                return success;
            }
            public void Unregister(IntPtr handle, int id) { UnregisterHotKey(handle, id); }
            [DllImport("user32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool RegisterHotKey(IntPtr handle, int id, uint modifiers, uint key);
            [DllImport("user32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool UnregisterHotKey(IntPtr handle, int id);
        }

        internal static Scope Attach(Window window, bool nativeEscape = true)
        { return Attach(window, nativeEscape ? new NativeRegistration() : null); }
        internal static Scope Attach(Window window, IEscapeRegistration registration)
        {
            if (window == null) throw new ArgumentNullException("window");
            window.Dispatcher.VerifyAccess();
            Scope current = window.GetValue(ScopeProperty) as Scope;
            if (current != null) return current;
            Scope scope = new Scope(window, registration);
            window.SetValue(ScopeProperty, scope);
            return scope;
        }

        internal sealed class Scope : IDisposable
        {
            private readonly Window window;
            private readonly IEscapeRegistration registration;
            private readonly KeyEventHandler localKey;
            private HwndSource source;
            private IntPtr handle;
            private bool attemptedWhileVisible;
            internal bool IsRegistered { get; private set; }
            internal bool IsDisposed { get; private set; }
            internal int RegistrationError { get; private set; }

            internal Scope(Window owner, IEscapeRegistration native)
            {
                window = owner; registration = native; localKey = OnLocalKey;
                window.AddHandler(Keyboard.PreviewKeyDownEvent, localKey, true);
                window.SourceInitialized += OnSourceReady;
                window.IsVisibleChanged += OnVisibilityChanged;
                window.StateChanged += OnStateChanged;
                window.Loaded += OnLoaded;
                window.Activated += OnActivated;
                window.Closed += OnClosed;
                window.Dispatcher.ShutdownStarted += OnShutdown;
                Synchronize();
            }
            private bool IsShown
            { get { return !IsDisposed && window.IsVisible && window.WindowState != WindowState.Minimized; } }
            private void Synchronize()
            {
                if (!IsShown)
                {
                    Release(); attemptedWhileVisible = false;
                    return;
                }
                if (registration == null || IsRegistered || attemptedWhileVisible) return;
                if (source == null)
                {
                    handle = new WindowInteropHelper(window).Handle;
                    if (handle == IntPtr.Zero) return;
                    source = HwndSource.FromHwnd(handle);
                    if (source == null || source.IsDisposed) { source = null; return; }
                    source.AddHook(OnMessage); source.Disposed += OnSourceDisposed;
                }
                attemptedWhileVisible = true;
                IsRegistered = registration.Register(handle, HotKeyId);
                RegistrationError = IsRegistered ? 0 : registration.LastError;
            }
            private IntPtr OnMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
            {
                long chord = lParam.ToInt64();
                if (message == HotKeyMessage && wParam.ToInt64() == HotKeyId &&
                    ((chord >> 16) & 0xFFFF) == EscapeVirtualKey && (chord & 0xF) == 0 && IsRegistered && IsShown)
                { handled = true; window.Close(); }
                return IntPtr.Zero;
            }
            private void OnLocalKey(object sender, KeyEventArgs e)
            {
                if (e.Key != Key.Escape || !IsShown) return;
                e.Handled = true; window.Close();
            }
            private void QueueDismissFocus()
            {
                window.Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(delegate {
                    // Focus inside an already-active greeting; never force another
                    // app out of the foreground merely to deliver this shortcut.
                    if (!IsShown || !window.IsActive) return;
                    Button dismiss = window.FindName("GreetingDismiss") as Button;
                    if (dismiss != null) dismiss.Focus();
                }));
            }
            private void OnSourceReady(object sender, EventArgs e) { Synchronize(); }
            private void OnVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e) { Synchronize(); }
            private void OnStateChanged(object sender, EventArgs e) { Synchronize(); }
            private void OnLoaded(object sender, RoutedEventArgs e) { Synchronize(); QueueDismissFocus(); }
            private void OnActivated(object sender, EventArgs e) { QueueDismissFocus(); }
            private void OnClosed(object sender, EventArgs e) { Dispose(); }
            private void OnShutdown(object sender, EventArgs e) { Dispose(); }
            private void OnSourceDisposed(object sender, EventArgs e) { Dispose(); }
            private void Release()
            {
                if (!IsRegistered) return;
                IsRegistered = false;
                registration.Unregister(handle, HotKeyId);
            }
            public void Dispose()
            {
                if (IsDisposed) return;
                window.Dispatcher.VerifyAccess();
                IsDisposed = true; Release();
                window.RemoveHandler(Keyboard.PreviewKeyDownEvent, localKey);
                window.SourceInitialized -= OnSourceReady;
                window.IsVisibleChanged -= OnVisibilityChanged;
                window.StateChanged -= OnStateChanged;
                window.Loaded -= OnLoaded;
                window.Activated -= OnActivated;
                window.Closed -= OnClosed;
                window.Dispatcher.ShutdownStarted -= OnShutdown;
                if (source != null)
                {
                    source.Disposed -= OnSourceDisposed;
                    if (!source.IsDisposed) source.RemoveHook(OnMessage);
                    source = null;
                }
                window.ClearValue(ScopeProperty);
            }
        }
    }
}
