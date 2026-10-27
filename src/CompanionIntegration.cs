using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace HolidayTimer
{
    internal sealed partial class HolidayApplication
    {
        private static readonly string[] CompanionNames = {
            "None", "Dog", "Cat", "Bunny", "Bird", "Car", "Bicycle", "Motorcycle", "Airplane"
        };
        private static readonly string[] CompanionDescriptions = {
            "Let the orb shine on its own.",
            "A dog walks, plays and naps. Click for a happy hop and silent bark.",
            "A cat walks, grooms and naps. Click for a playful hop.",
            "A bunny hops, plays and rests on the edge.",
            "A bird flies, flaps and occasionally perches.",
            "A car drives, stops and idles. Click for a playful bounce.",
            "A bicycle rolls and pauses on the ring.",
            "A motorcycle cruises and stops. Click for a quick lean.",
            "An airplane travels and banks around the orb."
        };

        private static void FillCompanionForm(Window form, AppSettings settings)
        {
            ComboBox choice = Layouts.Find<ComboBox>(form, "CompanionChoice");
            choice.ItemsSource = CompanionNames; choice.SelectedIndex = (int)settings.Companion;
            Layouts.Find<CheckBox>(form, "CompanionScenery").IsChecked = settings.CompanionScenery;
            UpdateCompanionControls(form);
        }

        private static bool ReadCompanionForm(Window form, AppSettings draft, out string error)
        {
            int index = Layouts.Find<ComboBox>(form, "CompanionChoice").SelectedIndex;
            if (index < 0 || index >= CompanionNames.Length) { error = "Choose a companion from the list."; return false; }
            draft.Companion = (CompanionKind)index;
            draft.CompanionScenery = Layouts.Find<CheckBox>(form, "CompanionScenery").IsChecked == true;
            error = null; return true;
        }

        private static void UpdateCompanionControls(Window form)
        {
            int index = Layouts.Find<ComboBox>(form, "CompanionChoice").SelectedIndex;
            index = Math.Max(0, Math.Min(CompanionDescriptions.Length - 1, index));
            bool motion = SystemParameters.ClientAreaAnimation && !SystemParameters.HighContrast;
            Layouts.Find<TextBlock>(form, "CompanionDescription").Text = CompanionDescriptions[index] +
                (index != 0 && !motion ? " Windows motion settings keep it still." : "");
            Layouts.Find<Button>(form, "TryCompanion").IsEnabled = index != 0 && motion;
            Layouts.Find<CheckBox>(form, "CompanionScenery").IsEnabled = index != 0;
        }

        private void BindCompanionForm(Window form)
        {
            Layouts.Find<ComboBox>(form, "CompanionChoice").SelectionChanged += delegate { UpdateCompanionControls(form); QueuePreviewUpdate(); };
            Layouts.Find<CheckBox>(form, "CompanionScenery").Checked += delegate { QueuePreviewUpdate(); };
            Layouts.Find<CheckBox>(form, "CompanionScenery").Unchecked += delegate { QueuePreviewUpdate(); };
            Layouts.Find<Button>(form, "TryCompanion").Click += delegate
            {
                CompanionScene.React(Layouts.Find<Canvas>(form, "SettingsCompanionCanvas"));
                if (GadgetPreview != null) CompanionScene.React(Layouts.Find<Canvas>(GadgetPreview, "CompanionCanvas"));
            };
            PropertyChangedEventHandler system = delegate(object sender, PropertyChangedEventArgs e)
            {
                if (e.PropertyName != "ClientAreaAnimation" && e.PropertyName != "HighContrast") return;
                if (!form.Dispatcher.HasShutdownStarted) form.Dispatcher.BeginInvoke(new Action(delegate { if (SettingsWindow == form) UpdateCompanionControls(form); }));
            };
            SystemParameters.StaticPropertyChanged += system;
            form.Closed += delegate { SystemParameters.StaticPropertyChanged -= system; };
        }
    }
}
