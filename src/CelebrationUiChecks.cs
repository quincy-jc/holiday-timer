using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace HolidayTimer
{
    internal static partial class UiChecks
    {
        private static void CheckCelebrationChoices(HolidayApplication app, Window form, SettingsStore store)
        {
            ComboBox choice = Layouts.Find<ComboBox>(form, "CelebrationStyle");
            int previous = choice.SelectedIndex;
            string saved = File.Exists(store.FilePath) ? File.ReadAllText(store.FilePath) : null;
            try
            {
                Check(choice.Items.Count == 2 && choice.SelectedIndex == (int)app.Settings.CelebrationEffect,
                    "Celebration settings expose Fireworks and Flowers with the saved default selected");
                foreach (CelebrationKind kind in Enum.GetValues(typeof(CelebrationKind)))
                {
                    choice.SelectedIndex = (int)kind;
                    AppSettings draft; string error;
                    Check(app.ReadForm(form, false, out draft, out error) && draft.CelebrationEffect == kind,
                        kind + " celebration enters the actual form draft");
                    Check(Layouts.Find<TextBlock>(form, "CelebrationDescription").Text.Contains(kind == CelebrationKind.Flowers ? "wedding" : "fireworks"),
                        kind + " choice explains its intended effect");
                    Check(Layouts.Find<TextBlock>(form, "CelebrationDescription").Text.Contains("until you dismiss"),
                        kind + " choice explains that the celebration remains under user control");
                }
                choice.SelectedIndex = -1;
                AppSettings invalid; string invalidError;
                Check(!app.ReadForm(form, false, out invalid, out invalidError) && invalidError.Contains("flowers"),
                    "A missing celebration choice cannot be saved");
            }
            finally { choice.SelectedIndex = previous; }
            Check((File.Exists(store.FilePath) ? File.ReadAllText(store.FilePath) : null) == saved && app.Settings.CelebrationEffect == CelebrationKind.Fireworks,
                "Changing celebration styles in the form leaves the saved countdown untouched");
        }

        private static void CheckCelebrationPreviews(HolidayApplication app, SettingsStore store)
        {
            app.OpenSettings();
            Window form = app.SettingsWindow;
            ComboBox choice = Layouts.Find<ComboBox>(form, "CelebrationStyle");
            TextBox name = Layouts.Find<TextBox>(form, "HolidayName");
            int previousChoice = choice.SelectedIndex;
            string previousName = name.Text;
            string saved = File.ReadAllText(store.FilePath);
            Window previousGreeting = null;
            try
            {
                foreach (CelebrationKind kind in Enum.GetValues(typeof(CelebrationKind)))
                {
                    choice.SelectedIndex = (int)kind;
                    name.Text = kind + " preview";
                    AppSettings draft; string error;
                    Check(app.ReadForm(form, false, out draft, out error) && draft.CelebrationEffect == kind,
                        kind + " preview reads the selected unsaved style");
                    Layouts.Find<Button>(form, "PreviewGreeting").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Check(app.Greeting != null && app.Greeting.IsVisible && app.Greeting != previousGreeting &&
                        Layouts.Find<TextBlock>(app.Greeting, "GreetingHoliday").Text == name.Text,
                        kind + " explicit preview displays a fresh greeting from its current draft");
                    Check(previousGreeting == null || !previousGreeting.IsVisible,
                        "Replacing a preview closes the previous celebration");
                    previousGreeting = app.Greeting;
                }
                Check(File.ReadAllText(store.FilePath) == saved && app.Settings.HolidayName != name.Text,
                    "Repeated celebration previews do not save the draft or morning deduplication");
            }
            finally
            {
                if (app.Greeting != null) app.Greeting.Close();
                name.Text = previousName; choice.SelectedIndex = previousChoice;
                form.Close();
            }
        }

        private static void CheckLoopDismissal(HolidayApplication app, DateTime now)
        {
            if (app.Greeting != null) app.Greeting.Close();
            foreach (CelebrationKind kind in Enum.GetValues(typeof(CelebrationKind)))
            {
                app.ShowGreeting(app.Settings, now, false);
                Window greeting = app.Greeting;
                Celebration.Play(greeting, true, kind);
                Check(Celebration.RenderingRunCount == 1 && Celebration.CurrentKind(greeting) == kind,
                    kind + " celebration is live before user dismissal");
                if (kind == CelebrationKind.Fireworks)
                    Layouts.Find<Button>(greeting, "GreetingDismiss").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                else
                    greeting.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(greeting),
                        Environment.TickCount, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent, Handled = true });
                Check(app.Greeting == null && Celebration.ActiveRunCount == 0 && Celebration.RenderingRunCount == 0 &&
                    Layouts.Find<Canvas>(greeting, "CelebrationCanvas").Children.Count == 0,
                    kind + " user dismissal removes the loop, artwork and frame subscription");
            }
            app.ShowGreeting(app.Settings, now, false);
        }
    }
}
