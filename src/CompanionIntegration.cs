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
            "A dog runs the orbit. Click for a happy hop and tail wag.",
            "A cat pads along the edge. Click for a curious bounce.",
            "A bunny hops around the ring. Click for a bigger hop.",
            "A bird flies around the ring. Click for a flap.",
            "A car drives the loop. Click for a quick bounce.",
            "A bicycle rolls around the ring. Click to spin its wheels.",
            "A motorcycle cruises the orbit. Click for a quick lean.",
            "An airplane circles the orb. Click for a quick bank."
        };

        private static void FillCompanionForm(Window form, AppSettings settings)
        {
            ComboBox choice = Layouts.Find<ComboBox>(form, "CompanionChoice");
            choice.ItemsSource = CompanionNames; choice.SelectedIndex = (int)settings.Companion;
            UpdateCompanionControls(form);
        }

        private static bool ReadCompanionForm(Window form, AppSettings draft, out string error)
        {
            int index = Layouts.Find<ComboBox>(form, "CompanionChoice").SelectedIndex;
            if (index < 0 || index >= CompanionNames.Length) { error = "Choose a companion from the list."; return false; }
            draft.Companion = (CompanionKind)index; error = null; return true;
        }

        private static void UpdateCompanionControls(Window form)
        {
            int index = Layouts.Find<ComboBox>(form, "CompanionChoice").SelectedIndex;
            index = Math.Max(0, Math.Min(CompanionDescriptions.Length - 1, index));
            bool motion = SystemParameters.ClientAreaAnimation && !SystemParameters.HighContrast;
            Layouts.Find<TextBlock>(form, "CompanionDescription").Text = CompanionDescriptions[index] +
                (index != 0 && !motion ? " Windows motion settings keep it still." : "");
            Layouts.Find<Button>(form, "TryCompanion").IsEnabled = index != 0 && motion;
        }

        private void BindCompanionForm(Window form)
        {
            Layouts.Find<ComboBox>(form, "CompanionChoice").SelectionChanged += delegate { UpdateCompanionControls(form); QueuePreviewUpdate(); };
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
