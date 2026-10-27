# Verification

The Windows build runs focused checks for countdowns, settings, work schedules, NSW holidays, music analysis and GitHub update validation, followed by WPF integration checks. The installer test uses an isolated directory, registry entry, startup name and application mutex.

Version 1.7.1 passed 243 core, 198 effect/settings, 250 working-calendar, 241 public-holiday, 99 audio-analysis and 34 update-validation checks. Its WPF integration suite passed 1,675 checks. Isolated upgrades from 1.6.0 and 1.7.0 each passed 103 installer checks, including update handoff and preservation of settings and calendar edits. The actual update client read the published 1.7.1 release, downloaded it to an isolated folder, and verified its digest and version against the tested installer without installing it.

The initial 1.7.0 release used an unsupported HTTP header assignment and could not check GitHub. Version 1.7.1 fixes the request and adds a regression check; 1.7.0 is marked as superseded.

Version 1.7.2 adds the MIT licence for the project and includes it alongside the existing Inter Display font licence in the installer. The application checks passed, and an isolated 1.7.1 upgrade, handoff and uninstall passed 109 checks. The public source and its commit history were reviewed for common credential and personal-path patterns; automated checks cannot guarantee the absence of every sensitive detail.

Version 1.8.0 passed 243 core, 221 settings, 250 work-calendar, 241 public-holiday, 99 audio-analysis, 34 update-validation and 1,767 WPF integration checks. Controlled frames cover all eight silhouettes, colour schemes, every orb effect, click/drag classification, short visits, audio response and persistence. An isolated upgrade from 1.7.2 passed 110 installer checks. The production update client also checked and downloaded the published 1.8.0 installer, verifying its version and digest without installing it.

Version 1.8.1 passed 243 core, 221 settings, 250 work-calendar, 241 public-holiday, 99 audio-analysis, 34 update-validation, 128 companion asset, 1,776 WPF integration and 1,796 render-preview checks. The tests cover silhouette crops, scheme colours, motion frames and wheel isolation. An isolated upgrade from 1.8.0 passed 110 installer checks. The test session had Windows animation effects disabled, so continuous motion and live audio response were not observed directly.

Run `build.ps1 -Test` for application checks. Run `build-setup.ps1 -Test` after installing the documented Inno Setup compiler for the installer checks. If a previous-version payload is present at `dist/companions-update`, the installer test includes that payload in its upgrade check; otherwise it exercises install, update handoff, and uninstall using the current payload.

The setup is unsigned. The GitHub release digest verifies download integrity, but does not replace code signing or protect against compromise of the publishing account. Reduced motion, High Contrast, hardware audio devices, multiple monitors and live Windows glass should also be checked in the target environment.
