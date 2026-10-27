using System;
using System.IO;
using System.Globalization;
using System.Reflection;
using System.Security.Principal;
using System.Threading;
using System.Windows;
using System.Windows.Markup;
using Microsoft.Win32;

namespace HolidayTimer
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            try
            {
                FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement),
                    new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag)));
                if (Array.IndexOf(args, "--smoke-test") >= 0 || Array.IndexOf(args, "--render-preview") >= 0)
                    return UiChecks.Run(Array.IndexOf(args, "--render-preview") >= 0);

                bool startup = Array.IndexOf(args, "--startup") >= 0;
                string user = WindowsIdentity.GetCurrent().User.Value;
                using (EventWaitHandle activation = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\HolidayTimer.Show." + user))
                {
                    bool first;
                    using (Mutex mutex = new Mutex(true, @"Local\HolidayTimer.Instance." + user, out first))
                    {
                        if (!first) { if (!startup) activation.Set(); return 0; }
                        try
                        {
                            string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HolidayTimer");
                            HolidayApplication app = new HolidayApplication(new SettingsStore(directory), new StartupRegistration(), true);
                            RegisteredWaitHandle waiter = ThreadPool.RegisterWaitForSingleObject(activation,
                                delegate { if (!app.Dispatcher.HasShutdownStarted) app.Dispatcher.BeginInvoke(new Action(app.OpenSettings)); }, null, -1, false);
                            try { app.StartupFromLogin = startup; app.Run(); }
                            finally { waiter.Unregister(null); }
                        }
                        finally { mutex.ReleaseMutex(); }
                    }
                }
                return 0;
            }
            catch (Exception ex)
            {
                if (Array.IndexOf(args, "--smoke-test") >= 0 || Array.IndexOf(args, "--render-preview") >= 0)
                {
                    File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ui-checks-fatal.txt"), ex.ToString());
                    return 1;
                }
                MessageBox.Show("Holiday Timer could not open.\n\n" + ex.Message, "Holiday Timer", MessageBoxButton.OK, MessageBoxImage.Error);
                return 1;
            }
        }
    }
}
