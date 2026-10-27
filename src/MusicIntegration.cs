using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace HolidayTimer
{
    // One capture serves the visible draft or saved gadget. This owner keeps
    // audio lifetime, UI dispatch and save/cancel semantics together.
    internal sealed partial class HolidayApplication
    {
        private IMusicCapture musicCapture;
        private DispatcherTimer musicTimer;
        private string musicDevice;
        private PropertyChangedEventHandler musicSystemHandler;
        private int deviceRequest;
        private string lastMusicStatus;
        internal bool MusicCaptureRequested { get; private set; }

        private void InitializeMusic()
        {
            if (!desktopIntegration) return;
            musicCapture = new MusicCapture();
            musicTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(33) };
            musicTimer.Tick += delegate { UpdateMusicFrame(); };
            WatchMusicVisibility(Widget);
            musicSystemHandler = delegate(object sender, PropertyChangedEventArgs e)
            {
                if (e.PropertyName != "HighContrast" && e.PropertyName != "ClientAreaAnimation") return;
                if (!Dispatcher.HasShutdownStarted) Dispatcher.BeginInvoke(new Action(SynchronizeMusic));
            };
            SystemParameters.StaticPropertyChanged += musicSystemHandler;
        }

        private void WatchMusicVisibility(Window window)
        {
            window.IsVisibleChanged += delegate { SynchronizeMusic(); };
            window.StateChanged += delegate { SynchronizeMusic(); };
        }

        private static bool VisibleForMusic(Window window)
        { return window != null && window.IsVisible && window.WindowState != WindowState.Minimized; }

        private AppSettings MusicTarget()
        {
            if (quitting || !SessionAvailable || SystemParameters.HighContrast || !SystemParameters.ClientAreaAnimation) return null;
            if (previewSettings != null && previewSettings.MusicResponse &&
                (VisibleForMusic(SettingsWindow) || VisibleForMusic(GadgetPreview))) return previewSettings;
            return Settings.MusicResponse && VisibleForMusic(Widget) ? Settings : null;
        }

        internal void SynchronizeMusic()
        {
            AppSettings target = MusicTarget();
            MusicCaptureRequested = desktopIntegration && target != null;
            if (musicCapture == null) { UpdateMusicFrame(); return; }
            if (target == null)
            {
                if (musicDevice != null) musicCapture.Stop();
                musicDevice = null;
                musicTimer.Stop();
            }
            else
            {
                string device = target.AudioDeviceId ?? "";
                // Start is idempotent for a healthy run. Let the capture owner
                // recover a finished worker even when its source ID is unchanged.
                musicCapture.Start(device);
                musicDevice = device;
                if (!musicTimer.IsEnabled) musicTimer.Start();
            }
            UpdateMusicFrame();
        }

        private void UpdateMusicFrame()
        {
            if (quitting) return;
            AudioSignal signal = musicCapture == null ? null : musicCapture.Signal;
            ApplyMusic(Widget, "OrbCanvas", Settings, signal);
            ApplyMusic(SettingsWindow, "SettingsOrbCanvas", previewSettings, signal);
            ApplyMusic(GadgetPreview, "OrbCanvas", previewSettings, signal);
            if (SettingsWindow == null) return;
            bool enabled = previewSettings != null && previewSettings.MusicResponse;
            string status = !enabled ? "Off. Your orb keeps its calm, idle motion." :
                !SessionAvailable ? "Paused while Windows is locked." :
                SystemParameters.HighContrast || !SystemParameters.ClientAreaAnimation ? "Motion is paused by your Windows accessibility settings." :
                !desktopIntegration ? "Preview controls are ready. Play music in the installed app." :
                musicCapture == null ? "Music response is off." : musicCapture.StatusMessage;
            if (!String.Equals(lastMusicStatus, status, StringComparison.Ordinal))
            {
                Layouts.Find<TextBlock>(SettingsWindow, "MusicStatus").Text = status;
                lastMusicStatus = status;
            }
            ScaleTransform meter = (ScaleTransform)Layouts.Find<System.Windows.Shapes.Rectangle>(SettingsWindow, "MusicLevel").RenderTransform;
            meter.ScaleX = enabled && signal != null && MusicCaptureRequested ? Math.Max(0, Math.Min(1, signal.Level)) : 0;
        }

        private void ApplyMusic(Window window, string canvasName, AppSettings settings, AudioSignal signal)
        {
            if (window == null || settings == null) return;
            bool enabled = MusicCaptureRequested && settings.MusicResponse && VisibleForMusic(window) &&
                String.Equals(settings.AudioDeviceId ?? "", musicDevice ?? "", StringComparison.Ordinal);
            OrbScene.SetMusic(Layouts.Find<Canvas>(window, canvasName), signal, settings.MusicIntensity, enabled);
            string companionName = canvasName == "SettingsOrbCanvas" ? "SettingsCompanionCanvas" : "CompanionCanvas";
            CompanionScene.SetMusic(Layouts.Find<Canvas>(window, companionName), signal, settings.MusicIntensity, enabled);
        }

        private static void FillMusicForm(Window form, AppSettings settings)
        {
            Layouts.Find<CheckBox>(form, "MusicResponse").IsChecked = settings.MusicResponse;
            Layouts.Find<Slider>(form, "MusicIntensity").Value = settings.MusicIntensity * 100;
            ComboBox sources = Layouts.Find<ComboBox>(form, "AudioSource");
            sources.Items.Clear();
            sources.Items.Add(new ComboBoxItem { Content = "Default speakers or headphones", Tag = "" });
            if (!String.IsNullOrEmpty(settings.AudioDeviceId))
                sources.Items.Add(new ComboBoxItem { Content = "Saved output · checking availability", Tag = settings.AudioDeviceId });
            sources.SelectedIndex = String.IsNullOrEmpty(settings.AudioDeviceId) ? 0 : 1;
        }

        private static void ReadMusicForm(Window form, AppSettings draft)
        {
            draft.MusicResponse = Layouts.Find<CheckBox>(form, "MusicResponse").IsChecked == true;
            draft.MusicIntensity = Layouts.Find<Slider>(form, "MusicIntensity").Value / 100;
            ComboBoxItem source = Layouts.Find<ComboBox>(form, "AudioSource").SelectedItem as ComboBoxItem;
            draft.AudioDeviceId = source == null ? "" : source.Tag as string ?? "";
        }

        private void BindMusicForm(Window form)
        {
            lastMusicStatus = null;
            WatchMusicVisibility(form);
            Layouts.Find<CheckBox>(form, "MusicResponse").Checked += delegate
            {
                QueuePreviewUpdate();
            };
            Layouts.Find<CheckBox>(form, "MusicResponse").Unchecked += delegate { QueuePreviewUpdate(); };
            Layouts.Find<Slider>(form, "MusicIntensity").ValueChanged += delegate { QueuePreviewUpdate(); };
            Layouts.Find<ComboBox>(form, "AudioSource").SelectionChanged += delegate { QueuePreviewUpdate(); };
            Layouts.Find<Button>(form, "RefreshAudioSources").Click += delegate { RefreshAudioSources(form); };
            RefreshAudioSources(form);
        }

        private void RefreshAudioSources(Window form)
        {
            if (!desktopIntegration) return;
            int request = ++deviceRequest;
            Button refresh = Layouts.Find<Button>(form, "RefreshAudioSources");
            refresh.IsEnabled = false;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                System.Collections.Generic.IList<AudioOutputDevice> devices = null;
                try { devices = MusicCapture.GetOutputDevices(); } catch { }
                if (Dispatcher.HasShutdownStarted) return;
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    if (SettingsWindow != form || request != deviceRequest || quitting) return;
                    ComboBox combo = Layouts.Find<ComboBox>(form, "AudioSource");
                    ComboBoxItem previous = combo.SelectedItem as ComboBoxItem;
                    string selected = previous == null ? "" : previous.Tag as string ?? "";
                    combo.Items.Clear();
                    ComboBoxItem defaultSource = new ComboBoxItem { Content = "Default speakers or headphones", Tag = "" };
                    combo.Items.Add(defaultSource);
                    ComboBoxItem choice = String.IsNullOrEmpty(selected) ? defaultSource : null;
                    if (devices != null) foreach (AudioOutputDevice device in devices)
                    {
                        ComboBoxItem item = new ComboBoxItem { Content = device.Name, Tag = device.Id, ToolTip = device.Name };
                        combo.Items.Add(item);
                        if (String.Equals(selected, device.Id, StringComparison.Ordinal)) choice = item;
                    }
                    if (choice == null)
                    {
                        choice = new ComboBoxItem { Content = "Saved output · not connected", Tag = selected };
                        combo.Items.Add(choice);
                    }
                    combo.SelectedItem = choice;
                    refresh.IsEnabled = true;
                }));
            });
        }

        private void DisposeMusic()
        {
            MusicCaptureRequested = false;
            if (musicTimer != null) musicTimer.Stop();
            if (musicSystemHandler != null) SystemParameters.StaticPropertyChanged -= musicSystemHandler;
            if (musicCapture != null) { musicCapture.Dispose(); musicCapture = null; }
        }
    }
}
