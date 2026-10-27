using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;

namespace HolidayTimer
{
    internal static class CelebrationDismissalTests
    {
        private static int checks;
        private static void Check(bool value, string message)
        { if (!value) throw new Exception("FAIL: " + message); checks++; }
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr handle, int message, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(IntPtr handle, int id, uint modifiers, uint key);
        [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr handle, int id);

        [STAThread]
        private static int Main()
        {
            Application app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            try
            {
                LegacyProbe();
#if !BEFORE
                CheckScopes();
#endif
                Console.WriteLine("PASS: " + checks + " celebration dismissal checks. Only owned offscreen helpers were used; no keyboard input was generated.");
                return 0;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
            finally { app.Shutdown(); }
        }
        private static Window Helper()
        {
            Window window = new Window { Width = 180, Height = 100, Left = -20000, Top = -20000, ShowActivated = false,
                ShowInTaskbar = false, WindowStyle = WindowStyle.None, AllowsTransparency = true, Background = System.Windows.Media.Brushes.Transparent };
            Button dismiss = new Button { Content = "Continue", Name = "GreetingDismiss" };
            window.Content = dismiss;
            NameScope.SetNameScope(window, new NameScope()); window.RegisterName("GreetingDismiss", dismiss);
            return window;
        }
        private static void LegacyProbe()
        {
            Window window = Helper(); int localEscapes = 0;
            window.PreviewKeyDown += delegate(object sender, KeyEventArgs e) { if (e.Key == Key.Escape) { localEscapes++; window.Close(); } };
            window.Show();
            IntPtr handle = new WindowInteropHelper(window).Handle;
            Check(!window.IsActive && GetForegroundWindow() != handle, "Legacy diagnostic helper has a real HWND without becoming the foreground window");
            KeyEventArgs handledEscape = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), 0, Key.Escape)
            { RoutedEvent = Keyboard.PreviewKeyDownEvent, Handled = true };
            window.RaiseEvent(handledEscape);
            Check(window.IsVisible && localEscapes == 0, "The incumbent ordinary local handler skips an already-handled Escape event");
            SendMessage(handle, 0x0312, new IntPtr(0x484F), new IntPtr(0x1B << 16));
            Check(window.IsVisible && localEscapes == 0, "The incumbent local handler has no HWND hotkey path while inactive");
            window.Close();
        }
#if !BEFORE
        private sealed class FakeRegistration : CelebrationDismissal.IEscapeRegistration
        {
            internal bool Succeed = true;
            internal int Registers, Unregisters;
            internal IntPtr LastHandle;
            internal int LastId;
            public int LastError { get { return Succeed ? 0 : 1409; } }
            public bool Register(IntPtr handle, int id) { Registers++; LastHandle = handle; LastId = id; return Succeed; }
            public void Unregister(IntPtr handle, int id) { Unregisters++; }
        }
        private static void HotKey(Window window, int id = CelebrationDismissal.HotKeyId, int key = 0x1B, int modifiers = 0)
        { SendMessage(new WindowInteropHelper(window).Handle, CelebrationDismissal.HotKeyMessage, new IntPtr(id), new IntPtr((key << 16) | modifiers)); }
        private static void DispatchKey(Window window, Key key, bool handled)
        {
            window.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), 0, key)
            { RoutedEvent = Keyboard.PreviewKeyDownEvent, Handled = handled });
        }
        private static void CheckScopes()
        {
            FakeRegistration registration = new FakeRegistration();
            Window window = Helper();
            CelebrationDismissal.Scope scope = CelebrationDismissal.Attach(window, registration);
            Check(registration.Registers == 0 && !scope.IsRegistered, "Constructing a hidden greeting does not reserve Escape");
            new WindowInteropHelper(window).EnsureHandle();
            Check(registration.Registers == 0, "An initialized but hidden HWND does not reserve Escape");
            window.Show();
            Check(scope.IsRegistered && registration.Registers == 1 && registration.LastHandle == new WindowInteropHelper(window).Handle && registration.LastId == CelebrationDismissal.HotKeyId,
                "The visible greeting owns exactly one registration on its actual HWND");
            Check(CelebrationDismissal.Attach(window, registration) == scope && registration.Registers == 1,
                "Repeated Attach reuses the same dismissal owner without stacking registrations");
            HotKey(window, CelebrationDismissal.HotKeyId + 1);
            HotKey(window, key: 0x41);
            HotKey(window, modifiers: 2);
            Check(window.IsVisible && scope.IsRegistered, "Other hotkey identifiers, other keys and modified chords are not intercepted");
            window.WindowState = WindowState.Minimized;
            Check(!scope.IsRegistered && registration.Unregisters == 1, "Minimizing releases the scoped Escape registration");
            HotKey(window);
            Check(!scope.IsDisposed, "A queued Escape hotkey does not close a minimized greeting");
            window.WindowState = WindowState.Normal;
            Check(scope.IsRegistered && registration.Registers == 2, "Restoring a visible greeting reacquires one Escape scope");
            window.Hide();
            Check(!scope.IsRegistered && registration.Unregisters == 2, "Hiding releases Escape immediately");
            HotKey(window);
            Check(!scope.IsDisposed && !window.IsVisible, "A queued Escape does not act on a hidden greeting");
            window.Show();
            Check(scope.IsRegistered && registration.Registers == 3 && !window.IsActive,
                "Showing the inactive helper reacquires Escape without becoming foreground");
            HotKey(window);
            Check(scope.IsDisposed && !scope.IsRegistered && !window.IsVisible && registration.Unregisters == 3,
                "Actual HWND WM_HOTKEY dispatch dismisses an inactive greeting and unregisters its scope");
            scope.Dispose();
            Check(registration.Unregisters == 3, "Repeated disposal does not unregister another owner's key");

            window = Helper(); registration = new FakeRegistration { Succeed = false };
            scope = CelebrationDismissal.Attach(window, registration); window.Show();
            Check(!scope.IsRegistered && scope.RegistrationError == 1409 && registration.Registers == 1,
                "A refused native registration remains nonfatal and records its platform error");
            DispatchKey(window, System.Windows.Input.Key.A, true);
            Check(window.IsVisible, "The fallback never closes a greeting for another key");
            DispatchKey(window, System.Windows.Input.Key.Escape, true);
            Check(!window.IsVisible && scope.IsDisposed && registration.Unregisters == 0,
                "Already-handled local Escape still dismisses when Windows cannot reserve it");

            window = Helper(); registration = new FakeRegistration();
            scope = CelebrationDismissal.Attach(window, registration); window.Show();
            DispatchKey(window, System.Windows.Input.Key.Escape, true);
            Check(!window.IsVisible && scope.IsDisposed && registration.Unregisters == 1,
                "Local Escape also releases a successful native scope");

            window = Helper(); window.Show(); registration = new FakeRegistration();
            scope = CelebrationDismissal.Attach(window, registration);
            Check(scope.IsRegistered && registration.Registers == 1, "Attach also works after an HWND is already shown");
            scope.Dispose();
            HotKey(window); DispatchKey(window, System.Windows.Input.Key.Escape, true);
            Check(window.IsVisible && registration.Unregisters == 1, "Explicit disposal removes both the HWND hook and local handler");
            window.Close();

            window = Helper(); scope = CelebrationDismissal.Attach(window, false); window.Show();
            Check(!scope.IsRegistered && !window.IsActive, "Isolated app fixtures can use the local path without a system-wide reservation");
            DispatchKey(window, System.Windows.Input.Key.Escape, true);
            Check(scope.IsDisposed && !window.IsVisible, "The non-native fixture path still accepts already-handled Escape");
            CheckShutdown();
            CheckNative();
        }
        private static void CheckShutdown()
        {
            FakeRegistration registration = new FakeRegistration();
            CelebrationDismissal.Scope scope = null;
            Exception failure = null;
            Thread thread = new Thread(delegate() {
                try
                {
                    Window window = Helper(); scope = CelebrationDismissal.Attach(window, registration); window.Show();
                    Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
                    Dispatcher.Run();
                }
                catch (Exception ex) { failure = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA); thread.IsBackground = true; thread.Start();
            Check(thread.Join(3000), "The isolated dismissal dispatcher exits within its deadline");
            if (failure != null) throw failure;
            Check(scope != null && scope.IsDisposed && !scope.IsRegistered && registration.Registers == 1 && registration.Unregisters == 1,
                "Dispatcher shutdown unregisters the visible scope and removes its handlers");
        }
        private static void CheckNative()
        {
            Window window = Helper();
            CelebrationDismissal.Scope scope = CelebrationDismissal.Attach(window, true);
            try
            {
                window.Show();
                Check(scope.IsRegistered && scope.RegistrationError == 0, "Windows accepts the scoped unmodified Escape registration with MOD_NOREPEAT");
                Check(!window.IsActive && GetForegroundWindow() != new WindowInteropHelper(window).Handle,
                    "The actual native Escape helper does not take foreground activation");
                HotKey(window);
                Check(!window.IsVisible && scope.IsDisposed && !scope.IsRegistered,
                    "Actual native registration and own-HWND message dispatch close the inactive helper");
            }
            finally { if (window.IsVisible) window.Close(); scope.Dispose(); }

            window = Helper(); window.Show();
            IntPtr handle = new WindowInteropHelper(window).Handle;
            bool reserved = false;
            try
            {
                reserved = RegisterHotKey(handle, CelebrationDismissal.HotKeyId, CelebrationDismissal.NoRepeat, CelebrationDismissal.EscapeVirtualKey);
                Check(reserved, "Escape can be reserved by a fresh owned HWND after celebration close, proving no leftover registration");
            }
            finally { if (reserved) UnregisterHotKey(handle, CelebrationDismissal.HotKeyId); window.Close(); }

            window = Helper(); scope = CelebrationDismissal.Attach(window, true); window.Show();
            try
            {
                Check(scope.IsRegistered, "A fresh visible native greeting reacquires Escape");
                window.Hide();
                Check(!scope.IsRegistered, "The native greeting releases Escape on hide");
                Window contender = Helper(); contender.Show();
                IntPtr contenderHandle = new WindowInteropHelper(contender).Handle;
                reserved = false;
                try
                {
                    reserved = RegisterHotKey(contenderHandle, CelebrationDismissal.HotKeyId, CelebrationDismissal.NoRepeat, CelebrationDismissal.EscapeVirtualKey);
                    Check(reserved, "Another owned HWND can reserve Escape while the greeting is hidden");
                }
                finally { if (reserved) UnregisterHotKey(contenderHandle, CelebrationDismissal.HotKeyId); contender.Close(); }
            }
            finally { window.Close(); scope.Dispose(); }
        }
#endif
    }
}
