using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Markup;
using Microsoft.Win32;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace HolidayTimer
{
    public interface IStartupRegistration
    {
        string CurrentCommand { get; }
        void SetCommand(string command);
    }

    public sealed class StartupRegistration : IStartupRegistration
    {
        private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        public const string ValueName = "HolidayTimer";
        public static string CommandFor(string executable)
        {
            string command = "\"" + Path.GetFullPath(executable) + "\" --startup";
            if (command.Length > 260)
                throw new InvalidOperationException("The app’s folder path is too long for Windows sign-in. Move Holiday Timer to a shorter folder path, then save again.");
            return command;
        }
        public string CurrentCommand
        {
            get
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(KeyPath))
                    return key == null ? null : key.GetValue(ValueName) as string;
            }
        }
        public void SetCommand(string command)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(KeyPath))
            {
                if (command == null) key.DeleteValue(ValueName, false);
                else key.SetValue(ValueName, command, RegistryValueKind.String);
            }
        }
    }

    internal static class Layouts
    {
        public static Window Load(string name)
        { return (Window)LoadResource(name); }
        public static ResourceDictionary LoadControls()
        { return (ResourceDictionary)LoadResource("Controls"); }
        private static object LoadResource(string name)
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("HolidayTimer." + name + ".xaml"))
            {
                if (stream == null) throw new InvalidOperationException("The app layout is missing: " + name);
                return XamlReader.Load(stream);
            }
        }
        public static T Find<T>(Window window, string name) where T : class
        {
            T control = window.FindName(name) as T;
            if (control == null) throw new InvalidOperationException("The app control is missing: " + name);
            return control;
        }
        public static Drawing.Icon CreateTrayIcon()
        {
            using (Drawing.Bitmap bitmap = new Drawing.Bitmap(32, 32))
            using (Drawing.Graphics g = Drawing.Graphics.FromImage(bitmap))
            using (Drawing.SolidBrush ocean = new Drawing.SolidBrush(Drawing.Color.FromArgb(33, 118, 160)))
            using (Drawing.SolidBrush white = new Drawing.SolidBrush(Drawing.Color.White))
            using (Drawing.SolidBrush peach = new Drawing.SolidBrush(Drawing.Color.FromArgb(241, 163, 130)))
            using (Drawing.Pen tick = new Drawing.Pen(Drawing.Color.FromArgb(23, 74, 103), 2))
            {
                g.SmoothingMode = Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.FillEllipse(ocean, 1, 1, 30, 30);
                g.FillRectangle(white, 8, 9, 16, 16);
                g.FillRectangle(peach, 8, 9, 16, 5);
                g.DrawLine(tick, 12, 6, 12, 11);
                g.DrawLine(tick, 20, 6, 20, 11);
                g.DrawLine(tick, 12, 19, 15, 22);
                g.DrawLine(tick, 15, 22, 21, 16);
                IntPtr handle = bitmap.GetHicon();
                try { using (Drawing.Icon icon = Drawing.Icon.FromHandle(handle)) return (Drawing.Icon)icon.Clone(); }
                finally { DestroyIcon(handle); }
            }
        }
        [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr handle);
    }

    internal static class Placement
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect { public int Left, Top, Right, Bottom; }
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr handle, out NativeRect bounds);
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr handle, IntPtr after, int x, int y, int width, int height, uint flags);
        private static Rect NativeBounds(Window window)
        {
            NativeRect bounds;
            if (!GetWindowRect(new WindowInteropHelper(window).Handle, out bounds))
                throw new InvalidOperationException("The countdown’s screen position could not be read.");
            return new Rect(bounds.Left, bounds.Top, bounds.Right - bounds.Left, bounds.Bottom - bounds.Top);
        }
        public static void Restore(Window window, AppSettings settings, bool reset)
        {
            Rect bounds = NativeBounds(window);
            Drawing.Rectangle primary = Forms.Screen.PrimaryScreen.WorkingArea;
            double left = !reset && settings.WidgetLeft.HasValue ? settings.WidgetLeft.Value : primary.Right - bounds.Width - 24;
            double top = !reset && settings.WidgetTop.HasValue ? settings.WidgetTop.Value : primary.Bottom - bounds.Height - 24;
            Move(window, Fit(new Rect(left, top, bounds.Width, bounds.Height), Areas(), ToRect(primary)));
            Clamp(window);
        }
        public static void Clamp(Window window)
        {
            Move(window, Fit(NativeBounds(window), Areas(), ToRect(Forms.Screen.PrimaryScreen.WorkingArea)));
        }
        public static void Remember(Window window, AppSettings settings)
        { Rect bounds = NativeBounds(window); settings.WidgetLeft = bounds.Left; settings.WidgetTop = bounds.Top; }
        public static void PreviewBeside(Window preview, Window owner)
        {
            Rect bounds = NativeBounds(preview), ownerBounds = NativeBounds(owner);
            Move(preview, Fit(new Rect(ownerBounds.Right + 18, ownerBounds.Top + 80, bounds.Width, bounds.Height), Areas(), ToRect(Forms.Screen.PrimaryScreen.WorkingArea)));
        }
        public static double MaximumWidgetScale(Window window)
        {
            IntPtr handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero) return Math.Min((SystemParameters.WorkArea.Width - 16) / 344, (SystemParameters.WorkArea.Height - 16) / 344);
            Drawing.Rectangle area = Forms.Screen.FromHandle(handle).WorkingArea;
            System.Windows.DpiScale dpi = System.Windows.Media.VisualTreeHelper.GetDpi(window);
            return Math.Max(.2, Math.Min((area.Width / dpi.DpiScaleX - 16) / 344, (area.Height / dpi.DpiScaleY - 16) / 344));
        }
        private static Rect ToRect(Drawing.Rectangle value)
        { return new Rect(value.Left, value.Top, value.Width, value.Height); }
        private static Rect[] Areas()
        {
            Forms.Screen[] screens = Forms.Screen.AllScreens;
            Rect[] result = new Rect[screens.Length];
            for (int i = 0; i < screens.Length; i++) result[i] = ToRect(screens[i].WorkingArea);
            return result;
        }
        // Saved positions and monitor rectangles are physical pixels, independent of DPI.
        internal static Rect Fit(Rect position, Rect[] areas, Rect primary)
        {
            Rect bestArea = primary;
            double bestOverlap = 0;
            foreach (Rect working in areas)
            {
                Rect intersection = Rect.Intersect(working, position);
                double overlap = intersection.IsEmpty ? 0 : intersection.Width * intersection.Height;
                if (overlap > bestOverlap) { bestOverlap = overlap; bestArea = working; }
            }
            return new Rect(Math.Max(bestArea.Left, Math.Min(position.Left, bestArea.Right - position.Width)),
                Math.Max(bestArea.Top, Math.Min(position.Top, bestArea.Bottom - position.Height)), position.Width, position.Height);
        }
        private static void Move(Window window, Rect position)
        {
            if (!SetWindowPos(new WindowInteropHelper(window).Handle, IntPtr.Zero, (int)Math.Round(position.Left), (int)Math.Round(position.Top), 0, 0, 0x15))
                throw new InvalidOperationException("The countdown could not be moved to that screen position.");
        }
    }

    internal static class DesktopSession
    {
        [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr OpenInputDesktop(uint flags, bool inherit, uint access);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool GetUserObjectInformation(IntPtr handle, int index, StringBuilder information, uint length, out uint needed);
        [DllImport("user32.dll")] private static extern bool CloseDesktop(IntPtr handle);
        internal static bool IsUnlocked()
        {
            IntPtr desktop = OpenInputDesktop(0, false, 1);
            if (desktop == IntPtr.Zero) return false;
            try
            {
                StringBuilder name = new StringBuilder(64); uint needed;
                return GetUserObjectInformation(desktop, 2, name, 128, out needed) &&
                    String.Equals(name.ToString(), "Default", StringComparison.OrdinalIgnoreCase);
            }
            finally { CloseDesktop(desktop); }
        }
    }
}
