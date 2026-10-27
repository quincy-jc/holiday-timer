using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace HolidayTimer
{
    internal sealed partial class HolidayApplication
    {
        private enum UpdateState { Idle, Checking, Current, Available, Downloading, Ready, Failed }
        private readonly GitHubReleaseClient updateClient = new GitHubReleaseClient("quincy-jc", "holiday-timer");
        private DispatcherTimer updateTimer;
        private UpdateState updateState;
        private AvailableUpdate availableUpdate;
        private string downloadedUpdate;
        private int updateOperation;

        // Test seams never touch the installed app or make a network request.
        internal Func<AvailableUpdate> UpdateCheckOverride;
        internal Func<AvailableUpdate, string, string> UpdateDownloadOverride;
        internal Action<string, AvailableUpdate> UpdateVerifyOverride;
        internal Func<bool> UpdateConfirmOverride;
        internal Action<string> UpdateLaunchOverride;
        internal bool? UpdateInstalledOverride;

        private Version RunningVersion { get { return Assembly.GetExecutingAssembly().GetName().Version; } }

        private bool IsInstalledForUpdates()
        {
            if (UpdateInstalledOverride.HasValue) return UpdateInstalledOverride.Value;
            string expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "HolidayTimer", "HolidayTimer.exe");
            return String.Equals(Path.GetFullPath(Assembly.GetExecutingAssembly().Location), Path.GetFullPath(expected), StringComparison.OrdinalIgnoreCase);
        }

        private void InitializeUpdates()
        {
            updateTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromHours(6) };
            updateTimer.Tick += delegate { CheckForUpdates(false); };
            updateTimer.Start();
            if (Tray != null) Tray.BalloonTipClicked += delegate { if (availableUpdate != null) ShowUpdates(); };
            CheckForUpdates(false);
        }

        private void DisposeUpdates()
        {
            updateOperation++;
            if (updateTimer != null) updateTimer.Stop();
        }

        private void BindUpdateForm(Window form)
        {
            Layouts.Find<TextBlock>(form, "InstalledVersion").Text = "Version " + RunningVersion.ToString(3);
            Layouts.Find<Button>(form, "CheckForUpdates").Click += delegate { CheckForUpdates(true); };
            Layouts.Find<Button>(form, "InstallUpdate").Click += delegate { BeginUpdateInstall(form); };
            RenderUpdateState();
        }

        private void ShowUpdates()
        {
            OpenSettings();
            if (SettingsWindow != null) SettingsLayout.SelectSection(SettingsWindow, "UpdatesTab");
        }

        internal void CheckForUpdates(bool manual)
        {
            if (quitting || updateState == UpdateState.Checking || updateState == UpdateState.Downloading) return;
            updateState = UpdateState.Checking;
            RenderUpdateState();
            int operation = ++updateOperation;
            ThreadPool.QueueUserWorkItem(delegate
            {
                AvailableUpdate result = null; bool failed = false;
                try { result = UpdateCheckOverride != null ? UpdateCheckOverride() : updateClient.Check(); }
                catch (Exception) { failed = true; }
                if (Dispatcher.HasShutdownStarted) return;
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    if (quitting || operation != updateOperation) return;
                    if (failed || result == null) updateState = UpdateState.Failed;
                    else if (!GitHubReleaseClient.IsNewer(result.Version, RunningVersion))
                    { availableUpdate = null; downloadedUpdate = null; updateState = UpdateState.Current; }
                    else
                    {
                        bool changed = availableUpdate == null || availableUpdate.Version != result.Version || availableUpdate.Sha256 != result.Sha256;
                        availableUpdate = result;
                        if (changed) downloadedUpdate = null;
                        updateState = downloadedUpdate == null ? UpdateState.Available : UpdateState.Ready;
                        if (changed && !manual && Tray != null)
                            Tray.ShowBalloonTip(8000, "Holiday Timer update available", "Version " + result.Version.ToString(3) + " is ready. Open Updates to install it.", Forms.ToolTipIcon.Info);
                    }
                    RenderUpdateState();
                }));
            });
        }

        private void BeginUpdateInstall(Window form)
        {
            if (availableUpdate == null || (updateState != UpdateState.Available && updateState != UpdateState.Ready)) return;
            if (!IsInstalledForUpdates())
            {
                try { Process.Start(new ProcessStartInfo(availableUpdate.ReleaseUrl.AbsoluteUri) { UseShellExecute = true }); }
                catch (Exception) { updateState = UpdateState.Failed; RenderUpdateState(); }
                return;
            }
            bool confirmed = UpdateConfirmOverride != null ? UpdateConfirmOverride() :
                MessageBox.Show(form, "Save any changes you want to keep first. Holiday Timer will close, install the update, and reopen.\n\nContinue?",
                    "Update Holiday Timer", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
            if (!confirmed) return;
            if (downloadedUpdate != null) { FinishUpdateInstall(); return; }
            updateState = UpdateState.Downloading;
            RenderUpdateState();
            int operation = ++updateOperation;
            AvailableUpdate release = availableUpdate;
            string directory = Path.Combine(PublicHolidayCacheDirectory, "updates");
            ThreadPool.QueueUserWorkItem(delegate
            {
                string result = null; bool failed = false;
                try { result = UpdateDownloadOverride != null ? UpdateDownloadOverride(release, directory) : updateClient.Download(release, directory); }
                catch (Exception) { failed = true; }
                if (Dispatcher.HasShutdownStarted) return;
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    if (quitting || operation != updateOperation) return;
                    if (failed || String.IsNullOrEmpty(result)) { updateState = UpdateState.Failed; RenderUpdateState(); return; }
                    downloadedUpdate = result; updateState = UpdateState.Ready; RenderUpdateState();
                    // If the user has dismissed settings, don't close the app unexpectedly.
                    if (SettingsWindow == form && form.IsVisible) FinishUpdateInstall();
                }));
            });
        }

        private void FinishUpdateInstall()
        {
            try
            {
                if (UpdateVerifyOverride != null) UpdateVerifyOverride(downloadedUpdate, availableUpdate);
                else GitHubReleaseClient.VerifyStoredInstaller(downloadedUpdate, availableUpdate);
                if (UpdateLaunchOverride != null) { UpdateLaunchOverride(downloadedUpdate); return; }
                Process installer = Process.Start(new ProcessStartInfo(downloadedUpdate, "/AUTOUPDATE /SILENT /SP- /NORESTART /NOCLOSEAPPLICATIONS") { UseShellExecute = true });
                if (installer == null) throw new InvalidOperationException("The installer could not start.");
                ExitApp();
            }
            catch (Exception)
            {
                downloadedUpdate = null; updateState = UpdateState.Failed; RenderUpdateState();
            }
        }

        private void RenderUpdateState()
        {
            Window form = SettingsWindow;
            if (form == null) return;
            string status, detail;
            switch (updateState)
            {
                case UpdateState.Checking: status = "Checking for updates…"; detail = "Looking for the latest release on GitHub."; break;
                case UpdateState.Current: status = "You’re up to date"; detail = "You have the latest available version."; break;
                case UpdateState.Available: status = "Version " + availableUpdate.Version.ToString(3) + " is available"; detail = IsInstalledForUpdates() ? "Install when you’re ready. Holiday Timer will reopen after the update." : "This copy is running outside the installed location. Download the installer from GitHub."; break;
                case UpdateState.Downloading: status = "Downloading the update…"; detail = "Checking the installer before it runs."; break;
                case UpdateState.Ready: status = "Update ready to install"; detail = "The download is ready. Holiday Timer will reopen after installation."; break;
                case UpdateState.Failed: status = "Couldn’t complete the update"; detail = "Check your connection or try the download again. Your current countdown is unchanged."; break;
                default: status = "Check for a new version"; detail = "Holiday Timer checks GitHub for new versions."; break;
            }
            Layouts.Find<TextBlock>(form, "UpdateStatus").Text = status;
            Layouts.Find<TextBlock>(form, "UpdateDetail").Text = detail;
            Layouts.Find<Button>(form, "CheckForUpdates").IsEnabled = updateState != UpdateState.Checking && updateState != UpdateState.Downloading;
            Button install = Layouts.Find<Button>(form, "InstallUpdate");
            install.Visibility = updateState == UpdateState.Available || updateState == UpdateState.Ready ? Visibility.Visible : Visibility.Collapsed;
            install.Content = !IsInstalledForUpdates() ? "Open release page" : updateState == UpdateState.Ready ? "Install update" : "Download and install";
        }
    }
}
