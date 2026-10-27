using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;

namespace HolidayTimer
{
    internal static class GlassWindow
    {
        private static readonly DependencyProperty AppliedProperty = DependencyProperty.RegisterAttached(
            "Applied", typeof(bool), typeof(GlassWindow), new PropertyMetadata(false));
        private static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
            "Enabled", typeof(bool), typeof(GlassWindow), new PropertyMetadata(false));
        private static readonly DependencyProperty LightProperty = DependencyProperty.RegisterAttached(
            "Light", typeof(bool), typeof(GlassWindow), new PropertyMetadata(false));
        private static readonly DependencyProperty RequestedSurfaceProperty = DependencyProperty.RegisterAttached(
            "RequestedSurface", typeof(SolidColorBrush), typeof(GlassWindow), new PropertyMetadata(null));
        private static readonly DependencyProperty FallbackSurfaceProperty = DependencyProperty.RegisterAttached(
            "FallbackSurface", typeof(SolidColorBrush), typeof(GlassWindow), new PropertyMetadata(null));

        internal static bool GetEnabled(Window window)
        { return (bool)window.GetValue(EnabledProperty); }

        internal static void Apply(Window window)
        {
            if (window == null) throw new ArgumentNullException("window");
            if ((bool)window.GetValue(AppliedProperty)) return;
            window.SetValue(AppliedProperty, true);
            window.AllowsTransparency = false;
            window.WindowStyle = WindowStyle.None;
            window.Background = Brushes.Transparent;
            WindowChrome.SetWindowChrome(window, new WindowChrome
            {
                GlassFrameThickness = new Thickness(0),
                ResizeBorderThickness = new Thickness(7),
                CaptionHeight = 0,
                UseAeroCaptionButtons = false,
                NonClientFrameEdges = NonClientFrameEdges.None
            });
            window.SourceInitialized += delegate { EnableBackdrop(window); };
            window.Loaded += delegate { EnableBackdrop(window); };
        }

        internal static void ApplyAppearance(Window window, bool light)
        {
            if (window == null) throw new ArgumentNullException("window");
            window.SetValue(LightProperty, light);
            IntPtr handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero) return;
            try
            {
                int dark = light ? 0 : 1;
                DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int));
            }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
            catch (BadImageFormatException) { }
        }

        private static void EnableBackdrop(Window window)
        {
            bool enabled = false;
            try
            {
                IntPtr handle = new WindowInteropHelper(window).Handle;
                HwndSource source = HwndSource.FromHwnd(handle);
                if (source != null && source.CompositionTarget != null)
                    source.CompositionTarget.BackgroundColor = Colors.Transparent;
                int dark = (bool)window.GetValue(LightProperty) ? 0 : 1, rounded = 2, none = 1;
                DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int));
                DwmSetWindowAttribute(handle, 33, ref rounded, sizeof(int));
                // System Acrylic deliberately becomes solid when inactive. It must not
                // compete with the compatibility blur, nor may extended frame glass.
                DwmSetWindowAttribute(handle, 38, ref none, sizeof(int));
                Margins margins = new Margins();
                enabled = DwmExtendFrameIntoClientArea(handle, ref margins) >= 0 &&
                    !SystemParameters.HighContrast && EnableCompatibilityBlur(handle);
            }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
            catch (BadImageFormatException) { }

            window.SetValue(EnabledProperty, enabled);
            RefreshFallback(window);
        }

        private static bool EnableCompatibilityBlur(IntPtr handle)
        {
            // WCA_ACCENT_POLICY and ACCENT_ENABLE_BLURBEHIND are undocumented.
            // This isolated Windows 10+ compatibility path is used because the
            // supported system Acrylic backdrop cannot stay translucent inactive.
            // Verified on Windows 11 build 26100 with changing windows behind it;
            // unavailable implementations keep the opaque PanelSurface fallback.
            if (Environment.OSVersion.Version.Major < 10) return false;
            IntPtr function = GetProcAddress(GetModuleHandle("user32.dll"), "SetWindowCompositionAttribute");
            if (function == IntPtr.Zero) return false;
            SetCompositionAttribute setAttribute = (SetCompositionAttribute)Marshal.GetDelegateForFunctionPointer(
                function, typeof(SetCompositionAttribute));
            AccentPolicy policy = new AccentPolicy { State = 3 };
            int size = Marshal.SizeOf(typeof(AccentPolicy));
            IntPtr memory = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(policy, memory, false);
                CompositionAttributeData data = new CompositionAttributeData
                { Attribute = 19, Data = memory, Size = new UIntPtr((uint)size) };
                return setAttribute(handle, ref data);
            }
            finally { Marshal.FreeHGlobal(memory); }
        }

        internal static void RefreshFallback(Window window)
        {
            if (new WindowInteropHelper(window).Handle == IntPtr.Zero) return;
            SolidColorBrush current = window.TryFindResource("PanelSurface") as SolidColorBrush;
            SolidColorBrush installed = window.GetValue(FallbackSurfaceProperty) as SolidColorBrush;
            if (GetEnabled(window))
            {
                // Source initialization can briefly fail before Loaded succeeds.
                // Restore only our own fallback, never a newer appearance brush.
                SolidColorBrush requested = window.GetValue(RequestedSurfaceProperty) as SolidColorBrush;
                if (installed != null && Object.ReferenceEquals(current, installed) && requested != null)
                    window.Resources["PanelSurface"] = requested;
                window.ClearValue(RequestedSurfaceProperty);
                window.ClearValue(FallbackSurfaceProperty);
                return;
            }
            if (installed != null && Object.ReferenceEquals(current, installed)) return;
            window.SetValue(RequestedSurfaceProperty, current);
            SolidColorBrush fallback = current == null
                ? new SolidColorBrush((bool)window.GetValue(LightProperty)
                    ? Color.FromRgb(244, 247, 252) : Color.FromRgb(16, 23, 37))
                : current.CloneCurrentValue();
            Color colour = fallback.Color;
            colour.A = 255;
            fallback.Color = colour;
            fallback.Opacity = 1;
            fallback.Freeze();
            window.SetValue(FallbackSurfaceProperty, fallback);
            window.Resources["PanelSurface"] = fallback;
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(
            IntPtr handle, int attribute, ref int value, int size);
        [StructLayout(LayoutKind.Sequential)]
        private struct Margins { public int Left, Right, Top, Bottom; }
        [StructLayout(LayoutKind.Sequential)]
        private struct AccentPolicy { public int State, Flags, Colour, Animation; }
        [StructLayout(LayoutKind.Sequential)]
        private struct CompositionAttributeData { public int Attribute; public IntPtr Data; public UIntPtr Size; }
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private delegate bool SetCompositionAttribute(IntPtr handle, ref CompositionAttributeData data);
        [DllImport("dwmapi.dll")]
        private static extern int DwmExtendFrameIntoClientArea(IntPtr handle, ref Margins margins);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandle(string module);
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
        private static extern IntPtr GetProcAddress(IntPtr module, string name);
    }
}
