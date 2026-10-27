using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace HolidayTimer
{
    internal static partial class UiChecks
    {
        private static void CheckUpdateControls(HolidayApplication app, Window form)
        {
            SettingsLayout.SelectSection(form, "UpdatesTab");
            Button check = Layouts.Find<Button>(form, "CheckForUpdates");
            Button install = Layouts.Find<Button>(form, "InstallUpdate");
            TextBlock status = Layouts.Find<TextBlock>(form, "UpdateStatus");
            app.UpdateInstalledOverride = true;
            app.UpdateCheckOverride = delegate { throw new IOException("offline"); };
            check.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(WaitFor(delegate { return status.Text.Contains("Couldn’t complete"); }), "An offline update check reports a retryable error");
            Check(check.IsEnabled && install.Visibility == Visibility.Collapsed, "An offline check does not offer an installer");

            Version current = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            AvailableUpdate equal = new AvailableUpdate { Version = new Version(current.Major, current.Minor, current.Build) };
            app.UpdateCheckOverride = delegate { return equal; };
            check.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(WaitFor(delegate { return status.Text.Contains("up to date"); }), "An equal version is not offered as an update");

            AvailableUpdate newer = new AvailableUpdate { Version = new Version(current.Major, current.Minor, current.Build + 1), Sha256 = "test" };
            app.UpdateCheckOverride = delegate { return newer; };
            check.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(WaitFor(delegate { return status.Text.Contains("is available"); }), "A newer release becomes visible in settings");
            Check(install.Visibility == Visibility.Visible, "An available release offers a download button");

            int downloads = 0, launches = 0;
            app.UpdateDownloadOverride = delegate(AvailableUpdate release, string directory) { downloads++; return "test-installer"; };
            app.UpdateLaunchOverride = delegate(string path) { launches++; };
            app.UpdateConfirmOverride = delegate { return false; };
            install.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(downloads == 0 && launches == 0, "Declining the update leaves the app running and does not download");

            app.UpdateConfirmOverride = delegate { return true; };
            app.UpdateVerifyOverride = delegate(string path, AvailableUpdate release) { throw new InvalidDataException("corrupt"); };
            install.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(WaitFor(delegate { return status.Text.Contains("Couldn’t complete"); }), "A failed installer verification is reported");
            Check(downloads == 1 && launches == 0, "An unverified installer is never launched");

            check.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(WaitFor(delegate { return status.Text.Contains("is available"); }), "The user can retry after an update failure");
            using (ManualResetEvent releaseDownload = new ManualResetEvent(false))
            {
                app.UpdateDownloadOverride = delegate(AvailableUpdate release, string directory) { releaseDownload.WaitOne(3000); return "test-installer"; };
                app.UpdateVerifyOverride = delegate(string path, AvailableUpdate release) { };
                install.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                form.Hide(); releaseDownload.Set();
                Check(WaitFor(delegate { return status.Text.Contains("ready to install"); }), "A completed download remains ready when settings is dismissed");
                Check(launches == 0, "Dismissing settings prevents an unexpected installer launch");
                form.Show();
                install.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(launches == 1, "The user can install the verified download after returning");
            }
            SettingsLayout.SelectSection(form, "CountdownTab");
        }
    }
}
