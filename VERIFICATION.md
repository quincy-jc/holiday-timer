# Verification

The Windows build runs focused checks for countdowns, settings, work schedules, NSW holidays, music analysis and GitHub update validation, followed by WPF integration checks. The installer test uses an isolated directory, registry entry, startup name and application mutex.

Version 1.7.0 passed 243 core, 198 effect/settings, 250 working-calendar, 241 public-holiday, 99 audio-analysis and 33 update-validation checks. Its WPF integration suite passed 1,675 checks, with an additional rendered-layout run. An isolated upgrade from 1.6.0 passed 103 installer checks, including update handoff and preservation of settings and calendar edits.

Run `build.ps1 -Test` for application checks. Run `build-setup.ps1 -Test` after installing the documented Inno Setup compiler for the installer checks. If a previous-version payload is present at `dist/public-holiday-update`, the installer test includes that payload in its upgrade check; otherwise it exercises install, update handoff, and uninstall using the current payload.

The setup is unsigned. The GitHub release digest verifies download integrity, but does not replace code signing or protect against compromise of the publishing account. Reduced motion, High Contrast, hardware audio devices, multiple monitors and live Windows glass should also be checked in the target environment.
