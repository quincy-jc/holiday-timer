# Motion concepts

Open `effects.html` in a browser, or refresh an already-open copy. **Dynamic** selects six more energetic concepts, with plasma vortex shown first at 65% intensity. **Calm** retains the original six concepts. Appearance, palette, intensity, pause and reduced-motion handling apply to both groups; the countdown remains still.

The HTML is self-contained: Inter Display, its font licence, and all rendering code are embedded. `dynamic-a.js` and `dynamic-b.js` are the editable sources for the six new renderers; they are inlined into the HTML and are not companion files required to open it. Integrated checks live in `tests/effects-gallery-checks.js`.

These are simulated motion concepts, separate from the Windows app's live music response. The version 1.1 executable and installer remain unchanged.

Browser automation cannot inspect this local file because its URL protocol is blocked. No alternate browser, server or indirect route is used to bypass that restriction. Geometry and integrated controls are checked with a mocked DOM/canvas; final browser pixels remain a user-visible preview.

The integrated gallery passes 1,336 checks: all twelve styles across 144 palette/appearance/intensity paint scenarios and 288 factory scenarios, 1,761,240 finite numeric canvas values, mode selection memory, pause/visibility/reduced motion, embedded fonts/licence and offline resources. The animation scheduler painted 29 times over one simulated second, within its 30 fps ceiling. Source renderer stress checks additionally cover up to 180 seconds of geometry. These checks establish code behavior; they do not establish browser screenshots or measured layout.
