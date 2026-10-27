# Holiday Timer

A native Windows countdown with a live glass panel, animated orb and tray icon. Choose a named event and display days, weeks, hours, minutes or seconds using elapsed or scheduled working time.

## Install from one file

Share `dist/companion-refinement-setup/HolidayTimerSetup.exe` for version 1.8.1. Double-click and choose Install. It updates the existing app without duplicate Apps entries and preserves preferences. A new or expired countdown automatically loads the next NSW public holiday, and a settings picker can refresh dates from the NSW Government. Previous artifacts retain their directories.

The setup installs for the current Windows user at `%LOCALAPPDATA%\Programs\HolidayTimer`. On Windows 11 it needs no administrator password, restart, or internet connection. It adds a Start menu shortcut and **Holiday Timer** to **Settings → Apps → Installed apps**, where you can choose **Uninstall**.

Save any edits and choose **Exit Holiday Timer** from the tray before updating or uninstalling. The setup preserves your saved countdown; reinstalling restores it. The uninstall removes the installed app and its own sign-in entry, while keeping `%LOCALAPPDATA%\HolidayTimer\settings.json` and its backup.

The single setup file contains the app, configuration, software licence, and font licence. It creates the necessary installed files automatically, so recipients do not have to manage a portable folder. The setup is unsigned, so Windows may show an unknown-publisher or SmartScreen prompt.

## Open the portable build

The current portable app is `dist/companion-refinement/HolidayTimer.exe`. Keep its configuration and licences beside it. Prior builds remain in their existing folders.

If another copy is running, **save any edits you want to keep**, then choose **Exit Holiday Timer** from its system tray menu. Open `dist/companion-refinement/HolidayTimer.exe`. The app is single-instance: opening another copy while the old one is running activates the existing app.

Keep `HolidayTimer.exe.config`, `LICENSE.txt` and `Inter-LICENSE.txt` beside the executable. Inter Display is bundled, and the app uses the .NET Framework included with Windows 11.

Enter the holiday name, date, and 24-hour time. Choose a unit, adjust appearance, then select **Save countdown**. The in-panel orb preview updates as you edit. **Try it on my desktop** opens a live floating preview. Preview changes remain in memory until Save; Cancel discards them.

The date picker matches your Dark/Light scheme. Monday-first weeks keep weekend columns together; NSW public holidays are marked with named tooltips, and your selected celebration date stays distinct. Arrow keys and month/year navigation remain available. Select **NSW holidays…** beside the date field to choose an upcoming holiday, or refresh published dates from the NSW Government. Choosing a holiday fills the draft; **Save countdown** commits it.

If no countdown has been set, the gadget starts with the next NSW statewide public holiday. A saved event stays selected while its time is in the future. After it arrives, its celebration remains open until dismissed, then the gadget switches to the next public holiday. An event already past when the app starts is replaced by the next holiday. A public holiday countdown starts at midnight in your computer's local time zone. A blank date/time or a past date in the settings form also chooses the next public holiday when saved.

The built-in calendar covers 2024–2031. Published NSW dates are included through 2027; later dates are estimates based on recurring rules and are labelled accordingly. **Refresh holidays** checks the official NSW Government page only when clicked. The last successfully refreshed dates remain available offline if a later refresh fails. Bank Holiday and local-only events are excluded. Dates outside calendar coverage remain selectable for elapsed countdowns.

To correct a holiday, choose it under **NSW holidays…**, select **Edit holiday**, change its name or date and choose **Save edit**. The shared calendar updates both the selected public-holiday countdown and the workday exclusions. Your correction stays in place after a refresh or restart; **Reset to source** restores the published or built-in value. Calendar edits are saved when you choose **Save edit**; changes to an ordinary custom countdown are saved with **Save countdown**.

## Appearance and desktop controls

- **Aurora:** ice blue, cyan, and indigo.
- **Violet:** lavender, pink, and violet.
- **Ember:** amber, coral, and warm orange.
- **Surface:** choose Dark or Light independently of the colour scheme. Light uses pale glass, darker text and accents, and a pearl orb.
- **Display font:** bundled Inter Display or an installed Windows family. Type a name in the picker to find it.
- **Orb effect:** Original glass plus Music pulse, Liquid ribbons, Orbital particles, Water ripples, Solar corona, Northern lights, Plasma vortex, Magnetic waves, Liquid metal, Warp tunnel, Shockwave pulses, and Comet swarm. All twelve gallery concepts are native app effects. **Motion intensity** adjusts the energy of the chosen effect independently of music response.
- **Text colour:** Default keeps the existing Dark/Light text colours and shadows. Custom offers a matching colour field, hue/saturation/brightness controls, twelve swatches and exact opaque `#RRGGBB` editing. Changes preview live. **Apply colour** keeps them in the settings draft; **Cancel** or Escape restores the previous colour and Default/Custom state. **Save countdown** remembers the result. Custom lettering has no added outline, halo or shadow; settings labels and celebration particles keep their scheme colours.
- **Glass opacity:** 35%–100%, shared by the settings tint and the orb's black or white centre. The orb backing fades gradually into its transparent exterior, with a subtle scheme-matched glow around it. The coloured shell, ribbons, halo, and soft glow stay independent of the centre opacity. The settings panel shows the blurred desktop through it, including when another window is active. Countdown text stays crisp and opaque; default lettering keeps its local readability shadow while custom lettering is plain. The utility strip retains at least 90% backing opacity.
- **Gadget size:** 60%–180%, remembered after saving and fitted to the available screen. Labels and buttons keep readable minimum sizes when the orb is small.
- **Workdays:** switch elapsed/workday mode on the gadget; on is accent highlighted and off is transparent. The real gadget saves immediately; a preview changes only its draft. Always-on-top is still available in settings and the tray.
- **Days/Weeks/Hours/Minutes/Seconds:** choose any unit in settings or tray. The gadget cycles Days → Minutes → Seconds → Weeks → Hours → Days and saves immediately. Elapsed mode counts total time; workday mode applies the selected work schedule to every unit and displays workdays or working weeks/hours/minutes/seconds. Counts retain their stable row.
- **Position:** drag the orb to move it. Its screen position is remembered and recovered when a monitor disappears.

The buttons appear when the pointer enters the orb and fade away shortly after it leaves. Move from the orb into the button strip to use it. Tab also reveals the buttons and keeps them visible while they have keyboard focus.

Live glass depends on Windows compositor support. If it is unavailable, the panel uses a readable solid surface in the selected appearance. Orb movement pauses while its window is hidden or minimized; reduced motion and High Contrast use a static orb.

Settings groups Countdown, Appearance, Companion, Music, Celebration and Updates into compact tabs. The orb stays visible beside the active controls; narrow windows move a compact preview above them. The active tab can scroll on smaller screens, with Save and validation messages kept in the footer.

## Companion

Choose None, Dog, Cat, Bunny, Bird, Car, Bicycle, Motorcycle or Airplane in **Companion**. One silhouette follows the orb's selected effect and colour scheme. Animals walk, hop or fly; the cat occasionally grooms and the dog barks silently. Vehicle wheels rotate, and the plane banks in flight. Click the countdown or focus it and press Space or Enter for a brief reaction; drag it to move as usual. A companion may briefly approach the countdown, which stays in front of the artwork. **Try a reaction** and **Try on my desktop** preview the unsaved choice. Save remembers it; Cancel restores the saved setting. Existing settings default to None.

When music response is enabled, the companion also responds to the selected audio output. Windows reduced-motion and High Contrast settings keep it still. Motion pauses while the orb is hidden or minimized.

## Updates

Version 1.8.1 checks the public `quincy-jc/holiday-timer` GitHub Releases page at startup and approximately every six hours. **Settings → Updates** or **Check for updates** in the tray lets you check immediately. A tray notification appears when a newer stable version is available. Offline or failed checks leave the existing countdown working.

Choose **Download and install**, save any settings edits, and confirm. The app verifies the download against the release's SHA-256 digest and version, closes, and the per-user installer updates the existing installation and reopens it. Updates are never installed without your action. If settings is closed while a download finishes, the app waits until you return and select **Install update**. An unsigned installer may still show a Windows security prompt; the digest checks download integrity and does not replace code signing.

An existing 1.7.0 or older installation needs one manual install of the current version to gain working update checks; the 1.7.0 updater could not contact GitHub. Exit the old app from its tray menu before running that installer. Version 1.7.1 and later can update from within the app. Portable copies open the release page instead of silently installing to a different location.

For a later release, update the assembly and installer version numbers in `src/AssemblyInfo.cs` and `installer/HolidayTimer.iss`, build and test the setup, and publish it as `HolidayTimerSetup.exe` on a non-prerelease GitHub release with a matching stable three-part tag such as `v1.8.1`. GitHub must expose the asset's SHA-256 digest before the app offers the release.

## Music response

Enable **Let the orb react to music** in settings, select **Default speakers or headphones** or a named audio output, and choose **Subtle → Energetic**. The embedded orb and floating preview respond while you edit. Save commits the choice; Cancel restores the saved response. The feature is off by default, including when upgrading an existing countdown.

Source and intensity controls appear when enabled. A small meter shows playback level, and the status explains whether the response is waiting, active, paused, or disconnected. Short pauses between words or notes keep a connected status. Choosing the default source follows changes to the Windows default output; choosing a named source keeps that device selected. Disconnected outputs retry automatically, and a finished capture worker is recovered without toggling the feature. Saving transfers the live source from preview to the gadget.

Playback visibly changes the chosen effect’s flow, glow, ribbons, particles or pulses. Quiet real output receives a stronger bounded response while silence stays quiet. The countdown and centre opacity remain steady.

It reads all playback from the selected output, including Spotify, browsers, and other players. Audio is analysed in memory and discarded. No account connection, microphone, audio recording, administrator permission, or internet is needed. Capture pauses when no enabled orb or settings/desktop preview is visible, while Windows is locked, or when reduced motion or High Contrast is active. A disconnected chosen output is retained for reconnection; choose another source or use **Refresh audio outputs** to check available devices.

`examples/effects.html` is an offline, interactive gallery of the twelve effects. **Calm** contains the original six; **Dynamic** adds plasma vortex, magnetic waves, liquid metal, warp tunnel, shockwave pulses and comet swarm. The gallery simulates music; the app uses real playback from your selected output. Each effect animates while idle and responds to playback when music response is enabled.

## Working time

Enable **Count only workdays** once to apply working time to all five units and the morning Days readout. Choose same-day **Work start** and **Work finish** in HH:mm; defaults are 09:00–17:00. Computer-local time is used. Nights, weekends and NSW statewide public holidays do not count.

A workday means the selected shift: six hours remaining in a six-hour shift displays **1 workday** or **6 working hours**; eight hours in an eight-hour shift displays **1 workday** or **8 working hours**. A working week is five shifts. Hours/minutes/seconds are total remaining working time, rounded up. Settings, the gadget, previews, tray and morning readout name the active basis. Turning workday mode off restores ordinary day/week/hour/minute/second labels. Outside the shift, working time does not tick down.

The gadget’s **Workdays** button toggles this basis directly and saves immediately; its accent highlight means on. In a desktop draft it changes only the unsaved preview. Always-on-top remains in settings and the tray.

The same NSW calendar supplies working-time exclusions and date-picker labels, including the additional Anzac Mondays and observed Christmas/Boxing Day dates. Published years and estimated years are distinguished in the picker. Overnight shifts are unsupported. A future target with zero work left does not trigger arrival early. Beyond calendar coverage, the timer remains saved and displays **Calendar update needed**; update the app or turn workday mode off. It never silently changes to elapsed time.

## System tray

Click the calendar tray icon to show the countdown. Right-click for preferences, units, pinning, show/hide, position reset, morning preview, and exit. Windows may initially place the icon in its hidden-icons area.

**Hide** switches the gadget to tray mode. Closing settings keeps the app running. To stop it, choose **Exit Holiday Timer** in the tray menu. The tray tooltip shows the live count and holiday name.

## Celebrations

When the actual target date and time arrives, the app automatically celebrates once for that selected target. This works independently of **Celebrate each morning**. If Windows is locked or suspended while the app is running, the celebration catches up after unlock or resume. When the app starts with an already-past target, it instead loads the next NSW public holiday. A future target with zero remaining working time does not trigger it early. Enable **Launch when I sign in** to let the app start automatically.

Choose **Fireworks + confetti** for layered launches and glowing bursts, or **Flowers + confetti** for bouquets that launch upward like rockets, burst, then scatter falling petals and confetti. Both follow the selected scheme, font and default/custom text colour, keep the message area clear, and repeat until dismissed. Fireworks remains the default for existing preferences. **Preview celebration** tries the unsaved draft without recording an arrival or a morning greeting.

Each connected monitor gets its own transparent fullscreen celebration with centred text, sized for that display and its scaling. **Continue · Esc to close** on any monitor or Escape dismisses the whole group; Escape also works after focus moves elsewhere. Connecting or disconnecting a monitor rearranges the visible celebration without recording a new arrival. Hiding, minimizing or closing stops rendering and releases the temporary Escape shortcut as appropriate; app shutdown removes it. Reduced motion and High Contrast show calm text on every display. There is no sound.

Optionally enable **Celebrate each morning** for a greeting once per local calendar day between 5 am and noon, after login/unlock or while the app stays open. It waits while Windows is locked and stops scheduling new morning greetings after the target arrives. The morning greeting shows Days using the selected elapsed/workday basis. Arrival takes priority over a morning greeting or preview. The once-per-morning schedule is separate from the animation, which keeps playing until dismissed.

Startup registration applies only to the current Windows user. Disable the option and Save to remove it. If you move the app folder, open it manually from the new location to refresh the sign-in path.

## Countdown and saved preferences

The target uses computer-local time. With workday mode off, days/weeks/hours/minutes/seconds are total elapsed units rounded up; a week is seven elapsed days. Skipped target times are rejected and repeated times use their later occurrence. Arrival is determined by the actual target instant.

At the target, the count reaches zero and shows **Your holiday is here!**, and the automatic arrival celebration opens on all connected monitors. The saved arrival marker prevents the same selected target from celebrating again after restart. Saving appearance changes preserves this marker; previewing never sets it. You can edit the timer or choose a new target.

Preferences are stored at `%LOCALAPPDATA%\HolidayTimer\settings.json`, with the previous file kept as `settings.json.bak`. Countdown and saved calendar data work offline; holiday refresh and update checks use the internet. No account is needed to use the app.

## Source and checks

Build and run isolated checks without replacing the preserved original:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Test -OutputDirectory dist\companion-refinement
```

`dist/companion-refinement/HolidayTimer.exe --render-preview` renders the actual WPF layouts into the updated build's `previews` folder without changing personal preferences or login registration. Layout captures use mock desktop backdrops; music examples use synthetic signals. Live Windows blur and actual audio capture have separate native evidence. See `VERIFICATION.md` for the current checks and evidence.

To build the single-file setup and run the application and isolated installation checks:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build-setup.ps1 -Test
```

See [installer/README.md](installer/README.md) for isolated installation checks, packaging details, and compiler licensing.

Holiday Timer's source code is distributed under the [MIT License](https://opensource.org/license/mit). The repository includes `LICENSE`; packaged builds include `LICENSE.txt`. The bundled Inter Display font comes from the official [Inter 4.1 release](https://github.com/rsms/inter/releases/tag/v4.1) and remains under its separate [SIL Open Font License](https://openfontlicense.org/open-font-license-official-text/), included as `Inter-LICENSE.txt` in packaged builds. The Inno Setup build tool has its own licence as described in the installer documentation.
