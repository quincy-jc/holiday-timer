/* Offline gallery checks. These exercise the integrated script and its painters
   with a strict DOM/canvas model; they are not browser pixel or layout tests. */
'use strict';

const fs = require('fs');
const path = require('path');
const vm = require('vm');
const crypto = require('crypto');

function runEffectsGalleryChecks(workspace) {
  const root = path.resolve(workspace || path.join(__dirname, '..'));
  const html = fs.readFileSync(path.join(root, 'examples', 'effects.html'), 'utf8');
  const scripts = [...html.matchAll(/<script\b([^>]*)>([\s\S]*?)<\/script>/gi)];
  let assertions = 0, validatedNumbers = 0, paintScenarios = 0, factoryScenarios = 0;
  const check = (condition, label) => { assertions++; if (!condition) throw new Error(label); };
  const finite = values => values.forEach(value => {
    if (typeof value === 'number') {
      validatedNumbers++;
      if (!Number.isFinite(value)) throw new Error('Non-finite canvas geometry');
    }
  });
  check(scripts.length === 1, 'The offline gallery must contain one integrated script');
  check(!/\bsrc\s*=/.test(scripts[0][1]), 'The gallery script must be embedded');
  const source = scripts[0][2];
  new vm.Script(source, { filename: 'effects.html:script' });

  function makeCanvas() {
    let operations = [], paints = 0, gradientNumber = 0;
    const gradient = (name, values) => {
      finite(values); operations.push([name, ...values]);
      return { gradient: ++gradientNumber, addColorStop(offset, color) {
        if (!Number.isFinite(offset) || offset < 0 || offset > 1) throw new Error('Invalid gradient stop');
        validateColor(color); operations.push(['colorStop', offset, color]);
      } };
    };
    const context = {
      createRadialGradient(...values) { return gradient('radial', values); },
      createLinearGradient(...values) { return gradient('linear', values); }
    };
    for (const name of ['beginPath', 'arc', 'stroke', 'fill', 'moveTo', 'lineTo', 'closePath', 'setTransform', 'clearRect']) {
      context[name] = (...values) => {
        finite(values);
        if (name === 'arc' && values[2] < 0) throw new Error('Negative arc radius');
        if (name === 'clearRect') paints++;
        operations.push([name, ...values]);
      };
    }
    for (const property of ['strokeStyle', 'fillStyle', 'lineWidth', 'lineCap', 'lineJoin', 'globalCompositeOperation']) {
      Object.defineProperty(context, property, { set(value) {
        if (property === 'lineWidth' && (!(value > 0) || !Number.isFinite(value))) throw new Error('Invalid stroke width');
        if ((property === 'strokeStyle' || property === 'fillStyle') && typeof value === 'string') validateColor(value);
        operations.push([property, typeof value === 'object' ? 'gradient' : value]);
      } });
    }
    return {
      context,
      clearTrace() { operations = []; gradientNumber = 0; },
      hash() { return crypto.createHash('sha256').update(JSON.stringify(operations)).digest('hex'); },
      operations() { return operations; },
      paints() { return paints; }
    };
  }
  function validateColor(color) {
    if (typeof color !== 'string') throw new Error('Canvas colour must be text');
    const match = /^rgba\(([\d.]+),([\d.]+),([\d.]+),([\d.e+-]+)\)$/.exec(color);
    if (!match) throw new Error('Unexpected canvas colour: ' + color);
    const numbers = match.slice(1).map(Number); finite(numbers);
    if (numbers.slice(0, 3).some(value => value < 0 || value > 255) || numbers[3] < 0 || numbers[3] > 1) throw new Error('Canvas colour outside bounds');
  }
  function attributes(text) {
    const values = {};
    for (const match of text.matchAll(/([\w-]+)\s*=\s*"([^"]*)"/g)) values[match[1]] = match[2];
    return values;
  }
  class Element {
    constructor(tag, text) {
      this.tagName = tag.toUpperCase(); this.attributes = attributes(text); this.dataset = {};
      for (const [key, value] of Object.entries(this.attributes)) if (key.startsWith('data-')) this.dataset[key.slice(5)] = value;
      this.value = this.attributes.value || ''; this.hidden = /(?:^|\s)hidden(?:\s|$)/.test(text);
      this.disabled = /(?:^|\s)disabled(?:\s|$)/.test(text); this.listeners = {}; this.textContent = ''; this.innerHTML = '';
    }
    setAttribute(key, value) { this.attributes[key] = value; }
    addEventListener(name, callback) { this.listeners[name] = callback; }
    click() { if (!this.disabled && this.listeners.click) this.listeners.click({ target: this }); }
    input(value) { this.value = String(value); if (this.listeners.input) this.listeners.input({ target: this }); }
  }
  function boot(options) {
    options = Object.assign({ reduced: false, dpr: 3, size: 344 }, options);
    const markup = html.replace(/<style>[\s\S]*?<\/style>/g, '').replace(/<script\b[^>]*>[\s\S]*?<\/script>/g, '');
    const elements = [...markup.matchAll(/<([a-z][a-z0-9]*)\b([^>]*)>/gi)].map(match => new Element(match[1], match[2]));
    const ids = Object.fromEntries(elements.filter(element => element.attributes.id).map(element => [element.attributes.id, element]));
    const buttons = elements.filter(element => element.tagName === 'BUTTON');
    const documentListeners = {}, windowListeners = {}, frames = new Map();
    let nextFrame = 1, resizeCallback;
    const canvas = ids['effects-canvas'], rendering = makeCanvas();
    canvas.parentElement = {}; canvas.getContext = kind => { if (kind !== '2d') throw new Error('Unexpected context'); return rendering.context; };
    const reduced = { matches: options.reduced, addEventListener(name, callback) { this.callback = callback; } };
    const document = {
      documentElement: elements.find(element => element.tagName === 'HTML'), hidden: false,
      getElementById(id) { if (!ids[id]) throw new Error('Missing element: ' + id); return ids[id]; },
      querySelectorAll(selector) {
        const match = /^(button)?\[data-([\w-]+)\]$/.exec(selector);
        if (!match) throw new Error('Unhandled selector: ' + selector);
        return elements.filter(element => (!match[1] || element.tagName === 'BUTTON') && Object.hasOwn(element.dataset, match[2]));
      },
      addEventListener(name, callback) { documentListeners[name] = callback; }
    };
    const window = {}, sandbox = {
      window, document, matchMedia: query => { check(query === '(prefers-reduced-motion: reduce)', 'Only reduced-motion preference is queried'); return reduced; },
      devicePixelRatio: options.dpr,
      requestAnimationFrame(callback) { const id = nextFrame++; frames.set(id, callback); return id; },
      cancelAnimationFrame(id) { frames.delete(id); },
      ResizeObserver: class { constructor(callback) { resizeCallback = callback; } observe() { resizeCallback([{ contentRect: { width: options.size } }]); } },
      addEventListener(name, callback) { windowListeners[name] = callback; }
    };
    vm.runInNewContext(source, sandbox, { filename: 'effects.html:script', timeout: 5000 });
    return {
      ids, buttons, document, reduced, frames, rendering, canvas, sandbox,
      state: () => window.holidayMotionPreview.state(),
      select(key, value) { const button = buttons.find(item => item.dataset[key] === value); check(!!button, 'Control exists: ' + key + '/' + value); button.click(); return button; },
      tick(now) { const entry = frames.entries().next().value; check(!!entry, 'Animation callback is scheduled'); frames.delete(entry[0]); entry[1](now); },
      hidden(value) { document.hidden = value; documentListeners.visibilitychange(); },
      reducedMotion(value) { reduced.matches = value; reduced.callback(); },
      resize(width) { resizeCallback([{ contentRect: { width } }]); },
      pagehide() { windowListeners.pagehide(); }
    };
  }

  const gallery = boot();
  const calm = ['pulse', 'ribbons', 'particles', 'ripples', 'corona', 'aurora'];
  const dynamic = ['vortex', 'magnetic', 'metal', 'tunnel', 'shockwave', 'comet'];
  check(gallery.state().effect === 'vortex' && gallery.state().family === 'dynamic' && gallery.state().intensity === .65, 'Default is the dynamic vortex at 65%');
  check(gallery.ids.intensity.value === '65', 'Slider matches initial intensity');
  check(gallery.canvas.width === 688, 'Canvas DPR is capped at 2');
  check(gallery.frames.size === 1, 'One animation loop starts');
  check(gallery.ids['calm-effects'].hidden && !gallery.ids['dynamic-effects'].hidden, 'Only dynamic family is initially visible');
  check(/\.effects\[hidden\]\s*\{\s*display:\s*none/.test(html), 'Hidden family overrides flex/grid CSS');
  const unique = {};
  for (const [family, effects] of [['calm', calm], ['dynamic', dynamic]]) {
    gallery.select('family', family);
    check(gallery.state().family === family, 'Family selection updates state');
    check(!gallery.ids[family + '-effects'].hidden && gallery.ids[(family === 'calm' ? 'dynamic' : 'calm') + '-effects'].hidden, 'Family visibility is exclusive');
    for (const effect of effects) {
      const button = gallery.select('effect', effect);
      check(gallery.state().effect === effect && button.attributes['aria-pressed'] === 'true', 'Effect selection is accessible');
      check(gallery.buttons.filter(item => item.dataset.effect && item.attributes['aria-pressed'] === 'true').length === 1, 'One effect remains selected across both families');
      check(gallery.ids['effect-title'].textContent && gallery.ids['effect-description'].textContent, 'Selected effect has a caption and description');
      for (const scheme of ['aurora', 'violet', 'ember']) for (const appearance of ['dark', 'light']) for (const strength of [0, 100]) {
        gallery.select('scheme', scheme); gallery.select('appearance', appearance);
        gallery.rendering.clearTrace(); gallery.ids.intensity.input(strength); paintScenarios++;
        const state = gallery.state();
        check(state.scheme === scheme && state.light === (appearance === 'light') && state.intensity === strength / 100, 'Palette, appearance and intensity reach the painter');
        check(gallery.ids['intensity-value'].value === strength + '%', 'Intensity output updates');
        const trace = gallery.rendering.operations();
        check(trace.some(operation => operation[0] === 'globalCompositeOperation' && operation[1] === 'destination-out'), 'Colour shell preserves the clear countdown centre');
        if (scheme === 'aurora' && appearance === 'dark' && strength === 100) unique[effect] = gallery.rendering.hash();
      }
    }
  }
  check(new Set(Object.values(unique)).size === 12, 'All twelve effects have distinct paint paths');
  gallery.select('family', 'calm'); gallery.select('effect', 'ripples');
  gallery.select('family', 'dynamic'); gallery.select('effect', 'comet');
  gallery.select('family', 'calm'); check(gallery.state().effect === 'ripples', 'Calm family remembers its selection');
  gallery.select('family', 'dynamic'); check(gallery.state().effect === 'comet', 'Dynamic family remembers its selection');
  const beforeFrames = gallery.rendering.paints();
  for (let timestamp = 1000; timestamp < 2000; timestamp += 5) gallery.tick(timestamp);
  const framePaints = gallery.rendering.paints() - beforeFrames;
  check(framePaints <= 30 && framePaints >= 20, 'Scheduled paint frequency stays within the 30 fps budget');
  check(gallery.frames.size === 1, 'Controls do not duplicate the animation loop');
  gallery.ids.pause.click(); check(gallery.state().paused && gallery.frames.size === 0, 'Pause cancels motion');
  gallery.ids.pause.click(); check(!gallery.state().paused && gallery.frames.size === 1, 'Resume schedules one loop');
  gallery.hidden(true); check(gallery.frames.size === 0, 'Hidden tab cancels motion');
  gallery.hidden(false); check(gallery.frames.size === 1, 'Visible tab resumes one loop');
  gallery.reducedMotion(true); check(gallery.state().paused && gallery.frames.size === 0 && gallery.ids.pause.disabled, 'Reduced motion stops movement and disables resume');
  gallery.ids.pause.click(); check(gallery.frames.size === 0, 'Reduced-motion preference cannot be overridden by the pause button');
  gallery.select('family', 'calm'); gallery.select('effect', 'ribbons'); check(gallery.frames.size === 0, 'Changing effects preserves reduced motion');
  gallery.reducedMotion(false); check(gallery.frames.size === 1 && !gallery.ids.pause.disabled, 'Changed system preference restores motion');
  gallery.resize(213); check(gallery.canvas.width === 426 && gallery.state().cssSize === 213, 'Canvas backing size follows its actual display size');
  gallery.pagehide(); check(gallery.frames.size === 0, 'Leaving the page cancels motion');
  for (const dpr of [1, 1.5, 2, 3]) { const sample = boot({ dpr, size: 280, reduced: true }); check(sample.canvas.width === Math.round(280 * Math.min(dpr, 2)), 'DPR is bounded at different scales'); check(sample.frames.size === 0 && sample.ids.pause.disabled, 'Initial reduced-motion render is static'); }

  // Check independent factories at later and boundary times, beyond gallery defaults.
  const palettes = [[[128, 188, 255], [92, 225, 230], [113, 105, 244]], [[255, 190, 123], [255, 133, 143], [232, 153, 99]]];
  for (const factoryName of ['a', 'b']) {
    const factorySource = fs.readFileSync(path.join(root, 'examples', 'dynamic-' + factoryName + '.js'), 'utf8');
    check(source.includes(factorySource.trim()), 'Integrated HTML contains the current dynamic factory ' + factoryName);
    const factorySandbox = { module: { exports: {} } }; vm.runInNewContext(factorySource, factorySandbox);
    const factory = factoryName === 'a' ? factorySandbox.module.exports : factorySandbox.module.exports.createDynamicEffectsB;
    check(typeof factory === 'function', 'Factory export is callable');
    for (const palette of palettes) for (const light of [false, true]) for (const strength of [0, 1]) {
      const rendering = makeCanvas(), ctx = rendering.context;
      const rgba = (color, alpha) => 'rgba(' + color.join(',') + ',' + alpha + ')';
      const opacity = alpha => Math.min(1, alpha * (light ? 1.25 : 1));
      const api = {
        ctx, colors: () => palette, intensity: () => strength, light: () => light, TAU: Math.PI * 2, rgba, opacity,
        line(points, color, width, alpha, close) { ctx.beginPath(); points.forEach((point, index) => index ? ctx.lineTo(...point) : ctx.moveTo(...point)); if (close) ctx.closePath(); ctx.strokeStyle = rgba(color, opacity(alpha)); ctx.lineWidth = width; ctx.stroke(); },
        circle(radius, color, width, alpha) { ctx.beginPath(); ctx.arc(0, 0, radius, 0, Math.PI * 2); ctx.strokeStyle = rgba(color, opacity(alpha)); ctx.lineWidth = width; ctx.stroke(); },
        bloom(x, y, radius, color, alpha) { ctx.createRadialGradient(x, y, 0, x, y, radius).addColorStop(0, rgba(color, opacity(alpha))); ctx.beginPath(); ctx.arc(x, y, radius, 0, Math.PI * 2); ctx.fill(); }
      };
      const painters = factory(api);
      check(Object.keys(painters).length === 3, 'Each factory provides three effects');
      for (const painter of Object.values(painters)) for (const time of [0, .0001, 2.4, 20, 500, 3600]) { rendering.clearTrace(); painter(time); factoryScenarios++; check(rendering.operations().length > 20, 'Factory produces a nonempty finite scene'); }
    }
  }

  const fonts = [...html.matchAll(/base64,([A-Za-z0-9+/=]+)/g)];
  check(fonts.length === 2, 'Both Inter Display faces are embedded');
  for (let index = 0; index < fonts.length; index++) {
    const original = fs.readFileSync(path.join(root, 'assets', 'fonts', 'InterDisplay-' + ['Regular', 'SemiBold'][index] + '.ttf'));
    check(Buffer.compare(Buffer.from(fonts[index][1], 'base64'), original) === 0, 'Embedded font is complete and byte-identical');
  }
  const originalLicense = fs.readFileSync(path.join(root, 'assets', 'fonts', 'LICENSE.txt'), 'utf8').trim().replace(/\r\n/g, '\n');
  const embeddedLicense = html.match(/<pre id="font-license">([\s\S]*?)<\/pre>/)[1].replace(/&lt;/g, '<').replace(/&gt;/g, '>').replace(/&amp;/g, '&').replace(/\r\n/g, '\n');
  check(originalLicense === embeddedLicense, 'Embedded font licence is exact');
  check(!/(?:src|href)\s*=\s*["'](?:https?:|file:|\/|\.\.?\/)/i.test(html), 'No external file or network resources are required');
  check(!/getUserMedia|AudioContext|mediaDevices|fetch\(|WebSocket|XMLHttpRequest/.test(source), 'The concept gallery accesses no audio or network services');
  check(/line-height:\s*84px;\s*height:\s*84px/.test(html), 'Countdown occupies a fixed-height number row');
  check(/Concept preview\s*·\s*Simulated motion/.test(html), 'Motion is clearly labelled as simulated');
  check(/@media\s*\(max-width:\s*860px\)/.test(html) && /@media\s*\(max-width:\s*420px\)/.test(html), 'Tablet and narrow-screen layout rules exist');
  check(!/transition:\s*all/.test(html), 'Control transitions target explicit properties');
  return {
    result: 'PASS', assertions, effects: Object.keys(unique), paintScenarios, factoryScenarios, validatedNumbers,
    scheduledPaintsInOneSecond: framePaints, embeddedFonts: fonts.length,
    verification: 'Integrated JavaScript with a strict DOM/canvas model; browser pixels and computed layout remain unverified.'
  };
}

module.exports = runEffectsGalleryChecks;
if (require.main === module) console.log(JSON.stringify(runEffectsGalleryChecks(process.argv[2]), null, 2));
