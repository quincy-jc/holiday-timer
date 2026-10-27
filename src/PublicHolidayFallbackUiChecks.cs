using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace HolidayTimer
{
    internal static partial class UiChecks
    {
        private static void CheckPublicHolidayFallback(HolidayApplication app, SettingsStore store)
        {
            AppSettings original = app.Settings;
            PublicHolidaySnapshot snapshot = NswPublicHolidays.Current;
            Func<System.Drawing.Rectangle[]> screens = app.GreetingScreenBoundsProvider;
            DispatcherTimer timer = (DispatcherTimer)typeof(HolidayApplication).GetField("timer", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(app);
            bool running = timer.IsEnabled; timer.Stop(); app.CloseGreetings();
            DateTime now = DateTime.Now;
            NswPublicHoliday next = snapshot.Next(now);
            try
            {
                app.Settings = new AppSettings();
                Check(app.EnsurePublicHolidayFallback(now, true) && !app.Settings.IsConfigured && app.Settings.AutomaticPublicHoliday &&
                    app.Settings.HolidayName == next.Name && CountdownCalculator.ParseTarget(app.Settings.TargetLocal) == next.Date,
                    "An unset countdown loads the next NSW holiday at local midnight without requiring setup");

                AppSettings future = original.Copy();
                future.IsConfigured = true; future.AutomaticPublicHoliday = false; future.PublicHolidayKey = null; future.HolidayName = "A future custom event";
                future.TargetLocal = now.AddDays(22).ToString(CountdownCalculator.TargetFormat, CultureInfo.InvariantCulture);
                app.Settings = future; store.Save(future);
                string saved = File.ReadAllText(store.FilePath);
                Check(!app.EnsurePublicHolidayFallback(now, true) && app.Settings.HolidayName == "A future custom event",
                    "A future custom event survives startup fallback");
                app.PublicHolidaysRefreshed();
                Check(app.Settings.TargetLocal == future.TargetLocal && File.ReadAllText(store.FilePath) == saved,
                    "Refreshing published holidays leaves future custom events and saved preferences untouched");

                AppSettings expired = future.Copy();
                expired.HolidayName = "Past event"; expired.TargetLocal = now.AddDays(-2).ToString(CountdownCalculator.TargetFormat, CultureInfo.InvariantCulture);
                expired.LastArrivalTarget = expired.TargetLocal; expired.LightAppearance = true; expired.MusicResponse = true;
                app.Settings = expired; store.Save(expired);
                Check(app.EnsurePublicHolidayFallback(now, true) && app.Settings.HolidayName == next.Name &&
                    app.Settings.AutomaticPublicHoliday && app.Settings.LastArrivalTarget == expired.TargetLocal &&
                    app.Settings.LightAppearance && app.Settings.MusicResponse,
                    "An expired saved event advances at startup while preserving appearance, music and arrival metadata");
                Check(store.Load().AutomaticPublicHoliday && store.Load().TargetLocal == app.Settings.TargetLocal,
                    "The automatic holiday and its identity survive a settings reload");

                // An unpublished holiday can be moved by a later official table.
                // A changed draft name is user-owned and must not be overwritten.
                app.OpenSettings(); Window form = app.SettingsWindow;
                Layouts.Find<TextBox>(form, "HolidayName").Text = "Draft wedding";
                DateTime? draftDate = Layouts.Find<DatePicker>(form, "HolidayDate").SelectedDate;
                List<NswPublicHoliday> changed = new List<NswPublicHoliday>();
                foreach (NswPublicHoliday holiday in snapshot.Entries) if (holiday.Date != next.Date) changed.Add(holiday);
                NswPublicHolidays.ReplaceForTests(changed);
                NswPublicHoliday revised = NswPublicHolidays.Next(now);
                app.PublicHolidaysRefreshed();
                Check(app.Settings.HolidayName == revised.Name && app.Settings.TargetLocal == revised.Date.ToString(CountdownCalculator.TargetFormat, CultureInfo.InvariantCulture),
                    "A refreshed calendar moves an automatic countdown to the next valid holiday");
                Check(Layouts.Find<TextBox>(form, "HolidayName").Text == "Draft wedding" &&
                    Layouts.Find<DatePicker>(form, "HolidayDate").SelectedDate == draftDate,
                    "A refresh does not replace an edited event draft");
                form.Close(); NswPublicHolidays.RestoreForTests(snapshot); app.PublicHolidaysRefreshed();

                app.OpenSettings(); form = app.SettingsWindow;
                Layouts.Find<Slider>(form, "BackgroundOpacity").Value = 65;
                NswPublicHolidays.ReplaceForTests(changed); app.PublicHolidaysRefreshed();
                Check(Layouts.Find<DatePicker>(form, "HolidayDate").SelectedDate == revised.Date &&
                    Layouts.Find<TextBox>(form, "HolidayName").Text == revised.Name && Layouts.Find<Slider>(form, "BackgroundOpacity").Value == 65,
                    "An untouched event draft follows refresh while unrelated appearance edits remain");
                form.Close(); NswPublicHolidays.RestoreForTests(snapshot); app.PublicHolidaysRefreshed();

                app.Settings = future.Copy(); store.Save(app.Settings);
                app.OpenSettings(); form = app.SettingsWindow;
                Layouts.Find<Button>(form, "PublicHolidayPickerButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                ComboBox holidayChoices = Layouts.Find<ComboBox>(form, "UpcomingPublicHolidays");
                NswPublicHoliday chosen = snapshot.GetUpcoming(now.AddDays(7), 90)[1];
                for (int i = 0; i < holidayChoices.Items.Count; i++)
                    if (((NswPublicHoliday)((ComboBoxItem)holidayChoices.Items[i]).Tag).Key == chosen.Key) { holidayChoices.SelectedIndex = i; break; }
                Layouts.Find<Button>(form, "UsePublicHoliday").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Layouts.Find<TextBox>(form, "HolidayTime").Text = "12:34";
                Check(app.SaveForm(form) && app.Settings.PublicHolidayKey == chosen.Key && !app.Settings.AutomaticPublicHoliday && store.Load().PublicHolidayKey == chosen.Key,
                    "A chosen public holiday stays linked to its calendar entry when a custom time is saved");
                DateTime editedDate = chosen.Date.AddDays(-1);
                while (editedDate > DateTime.Today && (editedDate.Year != chosen.Date.Year || snapshot.Find(editedDate) != null)) editedDate = editedDate.AddDays(-1);
                Check(editedDate > DateTime.Today && editedDate.Year == chosen.Date.Year, "Fixture found a free date for the selected holiday");
                NswPublicHolidays.SaveCorrection(app.PublicHolidayCacheDirectory, chosen.Key, editedDate, "Revised selected holiday");
                app.PublicHolidaysRefreshed();
                Check(app.Settings.HolidayName == "Revised selected holiday" && CountdownCalculator.ParseTarget(app.Settings.TargetLocal).Date == editedDate &&
                    CountdownCalculator.ParseTarget(app.Settings.TargetLocal).TimeOfDay == new TimeSpan(12, 34, 0) && store.Load().PublicHolidayKey == chosen.Key,
                    "Editing a chosen calendar holiday updates the countdown and preserves the user's time across reload");
                NswPublicHolidays.SaveCorrection(app.PublicHolidayCacheDirectory, chosen.Key, null, null);
                app.PublicHolidaysRefreshed();
                Check(CountdownCalculator.ParseTarget(app.Settings.TargetLocal).Date == chosen.Date && CountdownCalculator.ParseTarget(app.Settings.TargetLocal).Hour == 12,
                    "Resetting a linked holiday returns its source date while retaining the chosen time");

                app.GreetingScreenBoundsProvider = delegate { return new[] { new System.Drawing.Rectangle(0, 0, 1024, 768) }; };
                app.ShowGreeting(app.Settings, now, false);
                string celebrationName = Layouts.Find<TextBlock>(app.Greeting, "GreetingHoliday").Text;
                NswPublicHolidays.SaveCorrection(app.PublicHolidayCacheDirectory, chosen.Key, editedDate, "Updated during celebration");
                app.PublicHolidaysRefreshed();
                Check(CountdownCalculator.ParseTarget(app.Settings.TargetLocal).Date == chosen.Date &&
                    Layouts.Find<TextBlock>(app.Greeting, "GreetingHoliday").Text == celebrationName,
                    "Calendar changes wait while a celebration is open, preserving its message");
                app.CloseGreetings(); app.ProcessCelebrations(now);
                Check(CountdownCalculator.ParseTarget(app.Settings.TargetLocal).Date == editedDate && app.Settings.HolidayName == "Updated during celebration",
                    "A queued calendar correction reaches the linked countdown after dismissal");
                NswPublicHolidays.SaveCorrection(app.PublicHolidayCacheDirectory, chosen.Key, null, null);
                app.PublicHolidaysRefreshed();

                app.SessionAvailable = false;
                NswPublicHolidays.SaveCorrection(app.PublicHolidayCacheDirectory, chosen.Key, editedDate, "Updated while locked");
                app.PublicHolidaysRefreshed();
                Check(CountdownCalculator.ParseTarget(app.Settings.TargetLocal).Date == chosen.Date,
                    "Calendar refresh defers countdown changes while the session is locked");
                app.SessionAvailable = true; app.ProcessCelebrations(now);
                Check(CountdownCalculator.ParseTarget(app.Settings.TargetLocal).Date == editedDate && app.Settings.HolidayName == "Updated while locked",
                    "The linked countdown receives a deferred calendar update after unlock");
                NswPublicHolidays.SaveCorrection(app.PublicHolidayCacheDirectory, chosen.Key, null, null);
                app.PublicHolidaysRefreshed();

                NswPublicHolidays.SaveCorrection(app.PublicHolidayCacheDirectory, chosen.Key, editedDate, "Recovered on startup");
                app.Settings = store.Load();
                typeof(HolidayApplication).GetMethod("InitializePublicHolidayCalendar", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(app, null);
                Check(CountdownCalculator.ParseTarget(app.Settings.TargetLocal).Date == editedDate && app.Settings.HolidayName == "Recovered on startup",
                    "Startup reconciles a saved calendar edit even if the linked countdown was last saved before it");
                NswPublicHolidays.SaveCorrection(app.PublicHolidayCacheDirectory, chosen.Key, null, null);
                app.PublicHolidaysRefreshed();

                app.Settings = future.Copy(); store.Save(app.Settings);
                app.OpenSettings(); form = app.SettingsWindow;
                Layouts.Find<TextBox>(form, "HolidayTime").Text = "";
                Check(app.SaveForm(form) && app.Settings.AutomaticPublicHoliday && app.Settings.HolidayName == next.Name &&
                    store.Load().TargetLocal == next.Date.ToString(CountdownCalculator.TargetFormat, CultureInfo.InvariantCulture),
                    "Saving without a time commits the next public holiday instead of an empty countdown");

                string beforeDefaultArrival = File.ReadAllText(store.FilePath);
                AppSettings firstRun = new AppSettings { IsConfigured = false, AutomaticPublicHoliday = true,
                    HolidayName = "Automatic holiday", TargetLocal = now.AddSeconds(-2).ToString(CountdownCalculator.TargetFormat, CultureInfo.InvariantCulture),
                    MorningGreeting = false, PublicHolidayKey = next.Key };
                app.Settings = firstRun;
                app.ProcessCelebrations(now);
                Check(app.Greeting != null && Layouts.Find<TextBlock>(app.Greeting, "GreetingHoliday").Text == "Automatic holiday" &&
                    firstRun.LastArrivalTarget == firstRun.TargetLocal && File.ReadAllText(store.FilePath) == beforeDefaultArrival,
                    "An unsaved automatic holiday celebrates arrival without writing preferences");
                app.CloseGreetings(); app.ProcessCelebrations(now.AddSeconds(1));
                Check(!app.Settings.IsConfigured && app.Settings.AutomaticPublicHoliday && app.Settings.HolidayName == next.Name &&
                    File.ReadAllText(store.FilePath) == beforeDefaultArrival,
                    "The next automatic holiday loads after dismissal without forcing first-run setup to be saved");

                AppSettings beyond = future.Copy(); beyond.TargetLocal = "2031-12-31T23:59:00";
                app.Settings = beyond;
                Check(!app.EnsurePublicHolidayFallback(new DateTime(2032, 1, 1), true) && Object.ReferenceEquals(app.Settings, beyond),
                    "An exhausted built-in horizon does not invent an unverified holiday");
                Check(app.TestDispatcherError == null, "Fallback and refresh produce no deferred UI errors");
            }
            finally
            {
                app.CloseGreetings(); if (app.SettingsWindow != null) app.SettingsWindow.Close();
                NswPublicHolidays.RestoreForTests(snapshot); app.Settings = original; app.GreetingScreenBoundsProvider = screens;
                app.SessionAvailable = true; store.Save(original); app.RefreshCountdown();
                if (running) timer.Start();
            }
        }
    }
}
