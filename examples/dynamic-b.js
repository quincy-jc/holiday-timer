/* Additional Orbital Glass concepts. Pure time-based painters; the gallery owns
   animation scheduling, reduced motion, palette selection and the clear centre. */
function createDynamicEffectsB(api) {
  'use strict';
  const { ctx, colors, intensity, TAU, line, circle, bloom, rgba, opacity } = api;
  const clamp = (value, min, max) => Math.max(min, Math.min(max, value));
  const wrap = value => ((value % 1) + 1) % 1;
  const strength = () => .25 + .75 * clamp(intensity(), 0, 1);
  const smooth = value => { const x = clamp(value, 0, 1); return x * x * (3 - 2 * x); };
  const dot = (x, y, radius, color, alpha) => {
    ctx.fillStyle = rgba(color, opacity(alpha));
    ctx.beginPath();
    ctx.arc(x, y, radius, 0, TAU);
    ctx.fill();
  };

  // Rings accelerate out of a shallow, twisting vanishing point. Streaks follow
  // the same perspective, so the motion reads as forward travel through space.
  function tunnel(t) {
    const p = colors(), energy = strength(), speed = .19 + energy * .23;
    const project = (depth, angle) => {
      const radius = 70 + depth * depth * 90;
      const turn = t * .12 + depth * 1.15;
      const x = Math.cos(angle) * radius;
      const y = Math.sin(angle) * radius * (.65 + depth * .34);
      return [
        x * Math.cos(turn) - y * Math.sin(turn) + Math.sin(t * .27) * (1 - depth) * 9,
        x * Math.sin(turn) + y * Math.cos(turn) + Math.cos(t * .21) * (1 - depth) * 6
      ];
    };
    for (let ring = 0; ring < 9; ring++) {
      const depth = wrap(t * speed + ring / 9);
      const fade = Math.sin(depth * Math.PI);
      const points = [];
      for (let i = 0; i <= 64; i++) {
        const angle = i / 64 * TAU;
        const q = project(depth, angle);
        const wave = Math.sin(angle * 3 - t * .38 + depth * 3) * energy * 2.1;
        points.push([q[0] + Math.cos(angle) * wave, q[1] + Math.sin(angle) * wave]);
      }
      line(points, p[ring % 3], 4.2 + depth * 2.8, fade * .075, true);
      line(points, p[ring % 3], .65 + depth * 1.2, fade * (.27 + energy * .15), true);
    }
    for (let ray = 0; ray < 18; ray++) {
      const head = wrap(t * speed + ray * .173);
      const fade = Math.sin(head * Math.PI);
      const points = [];
      for (let i = 0; i < 9; i++) {
        const depth = clamp(head - i * (.009 + energy * .009), 0, 1);
        points.push(project(depth, ray / 18 * TAU - t * .16 + depth * 1.08));
      }
      line(points, p[ray % 3], 4.5, fade * .09);
      line(points, p[ray % 3], .8 + head * .8, fade * .49);
      if (ray % 3 === 0 && head > .6) bloom(points[0][0], points[0][1], 9 + head * 4, p[ray % 3], fade * .22);
    }
  }

  // A soft impact releases an elastic membrane and a finite spray of sparks.
  // Every layer has a fade-in; no beat replaces the frame with a bright flash.
  function shockwave(t) {
    const p = colors(), energy = strength(), period = 1.8 - energy * .48;
    const beat = t / period;
    for (let wave = 0; wave < 4; wave++) {
      const age = wrap((beat + wave) / 4) * 4;
      const progress = clamp(age / 3.3, 0, 1);
      const fade = smooth(age / .14) * (1 - smooth((age - 1.15) / 2.15));
      const expansion = 1 - Math.pow(1 - progress, 2.2);
      const recoil = Math.sin(age * 10) * Math.exp(-age * 1.55) * (3 + energy * 6);
      const radius = 91 + expansion * 66 + recoil;
      const points = [];
      for (let i = 0; i <= 64; i++) {
        const angle = i / 64 * TAU;
        const wobble = Math.sin(angle * 4 + age * 5 + wave) * Math.exp(-age) * energy * 3;
        points.push([Math.cos(angle) * (radius + wobble), Math.sin(angle) * (radius + wobble)]);
      }
      line(points, p[wave % 3], 8 - progress * 5, fade * .075, true);
      line(points, p[wave % 3], 1.5 - progress * .7, fade * .48, true);
    }
    const age = wrap(beat);
    const sparkFade = smooth(age / .12) * (1 - smooth((age - .32) / .65));
    for (let spark = 0; spark < 28; spark++) {
      const angle = spark / 28 * TAU + Math.floor(beat) * .137;
      const variation = .55 + .45 * Math.sin(spark * 2.37) * Math.sin(spark * 2.37);
      const radius = 104 + (1 - Math.pow(1 - age, 2)) * (31 + energy * 24) * variation;
      const bend = angle + age * Math.sin(spark * 3.3) * .13;
      const x = Math.cos(bend) * radius, y = Math.sin(bend) * radius;
      const length = (2 + energy * 5) * (1 - age);
      line([[x, y], [Math.cos(bend - .014) * (radius - length), Math.sin(bend - .014) * (radius - length)]], p[spark % 3], 1.1, sparkFade * .55);
      if (spark % 7 === 0) bloom(x, y, 7 + energy * 4, p[spark % 3], sparkFade * .23);
    }
    // A restrained breathing rim connects the discrete bursts between beats.
    circle(126 + Math.sin(age * Math.PI) * energy * 2.4, p[1], 1, .2);
  }

  // Comets race along inclined 3D loops. Perspective changes head size, opacity
  // and trail width; long fading tails give this a different rhythm to particles.
  function comet(t) {
    const p = colors(), energy = strength();
    for (let orbit = 0; orbit < 7; orbit++) {
      const tilt = orbit * .89 + .17;
      const direction = orbit % 2 ? -1 : 1;
      const speed = (.35 + energy * .56) * (1 + (orbit % 3) * .16) * direction;
      const phase = t * speed + orbit * 2.13;
      const path = angle => {
        const z = Math.sin(angle) * (.44 + (orbit % 3) * .06);
        const perspective = 1 + z * .16;
        const x = Math.cos(angle) * (128 + orbit % 3 * 5) * perspective;
        const y = Math.sin(angle) * (94 + orbit % 3 * 7) * perspective;
        return [x * Math.cos(tilt) - y * Math.sin(tilt), x * Math.sin(tilt) + y * Math.cos(tilt), z];
      };
      const head = path(phase);
      const visibility = .54 + (head[2] + .6) * .32;
      // Separate trail segments taper smoothly instead of a uniformly bright ring.
      for (let segment = 0; segment < 4; segment++) {
        const points = [];
        for (let i = 0; i <= 8; i++) {
          const distance = (segment + i / 8) * (.11 + energy * .065);
          const q = path(phase - direction * distance);
          points.push([q[0], q[1]]);
        }
        const taper = Math.pow(1 - segment / 4, 1.6);
        line(points, p[orbit % 3], (7 + head[2] * 2) * taper, taper * visibility * .09);
        line(points, p[orbit % 3], (2.5 + head[2] * .8) * taper, taper * visibility * .48);
      }
      bloom(head[0], head[1], 12 + (head[2] + .6) * 6, p[orbit % 3], visibility * .3);
      dot(head[0], head[1], 1.7 + (head[2] + .6) * 1.15, p[orbit % 3], visibility * .85);
    }
  }

  return { tunnel, shockwave, comet };
}

if (typeof module !== 'undefined' && module.exports) module.exports = { createDynamicEffectsB };
