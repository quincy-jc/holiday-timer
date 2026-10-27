# Holiday Timer setup

`dist/gait-refinement-setup/HolidayTimerSetup.exe` is the one file to share for version 1.9.1. It includes app, configuration, documentation, MIT licence and font licence. Double-click and choose Install; prior installers retain their directories.

Current features include automatic arrival celebrations and optional morning greetings on every connected monitor, with Fireworks or Flowers plus confetti and shared Escape dismissal. The matching WPF calendar and colour picker, Original glass plus twelve additional effects, five countdown units, configurable working hours, direct Workdays switch, optional music response and plain custom lettering remain. Default colours/shadows are unchanged. The soft glass backing and coloured artwork stay independent of centre opacity.

The installed app lives in `%LOCALAPPDATA%\Programs\HolidayTimer`, with a Start menu shortcut. Installation is for the current Windows user and requires no administrator password, restart, or internet connection on Windows 11. Remove it through **Settings → Apps → Installed apps → Holiday Timer → Uninstall**.

Install 1.9.1 over an older version without uninstalling first. Stable product identity/location updates one Apps entry and preserves preferences. Missing working-time fields default to elapsed mode and 09:00–17:00; missing celebration style defaults to Fireworks. Older preferences have no recorded arrival target.

Before updating or removing the app, save any edits and choose **Exit Holiday Timer** from the tray. Setup does not force a running copy closed. Uninstall removes the installed app and its own sign-in registration, while preserving the saved countdown at `%LOCALAPPDATA%\HolidayTimer`. An existing saved **Launch when I sign in** choice is respected. Saved preferences can be removed separately after uninstall if they are no longer wanted.

The generated setup is unsigned, so Windows may show an unknown-publisher or SmartScreen prompt. The single-file requirement applies to distribution: setup manages the normal installed files automatically.

Version 1.5.0 celebrates once when the selected actual target time arrives, independently of the morning checkbox. If the moment is missed while locked, suspended or not running, it catches up at the next unlocked opportunity. A zero working-time count before the target never triggers arrival early. The saved LastArrivalTarget marker prevents repeat arrivals across restart, survives appearance saves and is never set by previews.

Arrival, morning and preview celebrations open one transparent fullscreen window per connected monitor, each with centred text and display-scaled bounds. Escape or Continue on any monitor closes the whole group through one shared temporary Escape reservation. Display changes reflow the open group without marking another arrival. Fireworks and Flowers plus confetti retain their loop-until-dismiss behaviour, selected palette/font/ink and calm reduced-motion/High Contrast presentation. There is no new loop setting. Morning remains an optional once-per-local-morning schedule before arrival; arrival takes priority over it and previews. Rendering and shortcuts are cleaned up with their visible group. No additional DLL or administrator permission is required.

## Rebuild and verify

Version 1.5.1 adds automatic recovery for failed audio workers and unavailable selected outputs, status stability across short quiet gaps, stronger sound response across the orb styles, and four compact settings tabs with a persistent live preview. Audio is still analysed only in memory on the selected playback output; no microphone, account connection or recording is added. Save keeps the selected response, and existing preferences need no migration.

Version 1.6.0 loads the next NSW statewide public holiday when no countdown exists or its chosen time has passed. The countdown time is local midnight; an on-screen arrival celebration remains until dismissed before the next holiday loads. The calendar includes published dates through 2027 and clearly marked rule-based estimates through 2031. The **NSW holidays…** picker can refresh the official NSW Government table on demand; the last valid calendar remains available offline. You can edit a holiday's name/date, save the correction for both countdowns and workdays, or reset it to the source. Future custom countdowns remain selected.

Version 1.7.1 adds working GitHub release checks and an opt-in updater. Its installer waits for the app to exit, updates the current-user installation, and reopens it. The update keeps saved countdowns, calendar edits and startup choices. Direct installation of 1.7.1 still requires the older app to be exited first; the initial 1.7.0 release had an issue preventing update checks.

Version 1.7.2 adds the MIT licence for Holiday Timer's source code and includes its licence notice in the installer. Inter Display retains its separate font licence.

Version 1.8.0 adds one optional animal or vehicle companion, with live previews, brief countdown visits and click reactions. The selected companion persists with the countdown and respects Windows reduced-motion settings.

Version 1.8.1 refines the silhouettes and adds more distinctive animal poses and vehicle movement.

Version 1.9.0 synchronizes steps and wheels with travel, adds rest, play and sleep, and offers optional scenery.

Version 1.9.1 refines dog and cat leg proportions, paw placement and walking stride.

Run from the project root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build-setup.ps1 -Test
```

The command builds the app into `dist/companion-lifecycles`, runs its checks, compiles `installer/HolidayTimer.iss`, writes `dist/companion-lifecycles-setup/HolidayTimerSetup.exe`, and tests setup and removal with isolated install paths and registry/startup names. It does not install over the user's running app or replace the prior setup. `dist/companion-lifecycles-setup` contains only the file to share; build metadata lives in `dist/companion-lifecycles-setup-verification/package-info.json`. Results and package identity are recorded in `VERIFICATION.md`.

The installer checks can also run separately with `powershell -NoProfile -ExecutionPolicy Bypass -File .\installer\test-setup.ps1 -CompilerPath .\tools\inno-6.7.3\ISCC.exe -PayloadDirectory dist\companion-lifecycles`.

## Build-time compiler

The setup uses Inno Setup 6.7.3, stored locally in `tools/inno-6.7.3`. The official compiler installer is [`innosetup-6.7.3.exe`](https://github.com/jrsoftware/issrc/releases/tag/is-6_7_3). Its SHA-256 is `9C73C3BAE7ED48D44112A0F48E66742C00090BDB5BEF71D9D3C056C66E97B732`; its Authenticode signature was verified as valid for Pyrsys B.V.

To prepare another build workspace, download that official installer, verify its signature and hash, and run it with `/PORTABLE=1 /CURRENTUSER /VERYSILENT /NORESTART /DIR="<absolute project path>\tools\inno-6.7.3"`. Alternatively, pass `-CompilerPath` to `build-setup.ps1` and `installer/test-setup.ps1` to select an existing `ISCC.exe`.

The compiler was extracted using its portable, current-user mode into this workspace, without adding tool shortcuts or an installed-app registration. Recipients do not need Inno Setup. The [official downloads page](https://jrsoftware.org/isdl.php) asks commercial users to purchase a licence; review that guidance and `tools/inno-6.7.3/license.txt` before commercial distribution.
