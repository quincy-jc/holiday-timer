/* Offline orb concepts. All geometry uses logical 344 × 344 coordinates. */
function createDynamicEffectsA(api) {
  'use strict';
  const { ctx, colors, intensity, light, TAU, line, circle, bloom, rgba, opacity } = api;
  const gain = () => .25 + .75 * Math.max(0, Math.min(1, intensity()));
  const polar = (angle, radius) => [Math.cos(angle) * radius, Math.sin(angle) * radius];
  const rotate = (x, y, turn) => [x * Math.cos(turn) - y * Math.sin(turn), x * Math.sin(turn) + y * Math.cos(turn)];
  const mix = (a, b, fraction) => a.map((value, index) => Math.round(value + (b[index] - value) * fraction));
  function dot(point, radius, color, alpha) {
    ctx.fillStyle = rgba(color, opacity(alpha));
    ctx.beginPath();
    ctx.arc(point[0], point[1], radius, 0, TAU);
    ctx.fill();
  }

  function vortex(t) {
    const palette = colors(), energy = gain();
    const speed = .37 + energy * .19;
    const point = (arm, u, braid) => {
      const radius = 91 + u * 63 + Math.sin(u * 17 - t * 1.6 + arm) * energy * 3;
      const angle = arm * TAU / 5 + t * speed + u * Math.PI * 1.34 +
        Math.sin(u * 14 - t * 1.13 + arm * .71) * (.06 + energy * .075) +
        braid * Math.sin(u * 21 - t * 2.15 + arm) * (.012 + energy * .025);
      return polar(angle, radius);
    };
    for (let arm = 0; arm < 5; arm++) {
      const main = [], braid = [], color = palette[arm % 3];
      for (let i = 0; i <= 60; i++) {
        const u = i / 60;
        main.push(point(arm, u, 0));
        braid.push(point(arm, u, 1));
      }
      line(main, color, 10 + energy * 4, .07);
      line(main, color, 3.4 + energy * 2.2, .22);
      line(main, mix(color, [235, 244, 255], .23), 1.05 + energy * .45, .51);
      line(braid, palette[(arm + 1) % 3], .75, .35);

      // Packets travel inward along the spiral; their ends fade before wrapping.
      for (let packet = 0; packet < 3; packet++) {
        const phase = ((t * (.18 + energy * .09) + packet / 3 + arm * .137) % 1 + 1) % 1;
        const u = 1 - phase, fade = Math.sin(phase * Math.PI);
        for (let trail = 0; trail < 4; trail++) {
          const tailU = Math.min(1, u + trail * .018);
          dot(point(arm, tailU, 0), 1.1 + energy * .75 - trail * .14, color, fade * (.7 - trail * .13));
        }
        if (packet === 0) {
          const head = point(arm, u, 0);
          bloom(head[0], head[1], 17 + energy * 6, color, fade * (.23 + energy * .13));
        }
      }
    }
    circle(126, palette[1], .65, .18);
  }

  function magnetic(t) {
    const palette = colors(), energy = gain();
    const turn = Math.sin(t * .16) * .38 + t * .075;
    const field = (band, sign, u) => {
      const arch = Math.sin(u * Math.PI);
      const x = -119 + 238 * u + arch * Math.sin(t * .39 + band * .8) * energy * 8;
      const y = sign * (102 + band * 17 + Math.sin(t * .58 + band) * energy * 5) * arch;
      return rotate(x, y, turn);
    };
    for (let band = 0; band < 3; band++) {
      for (let sign = -1; sign <= 1; sign += 2) {
        const path = [], color = palette[(band + (sign > 0 ? 1 : 0)) % 3];
        for (let i = 0; i <= 60; i++) path.push(field(band, sign, i / 60));
        line(path, color, 7 + energy * 4, .075);
        line(path, color, 2 + energy * .6, .28);
        line(path, mix(color, [224, 244, 255], .18), .7, .62);

        for (let charge = 0; charge < 3; charge++) {
          const phase = ((t * (.19 + energy * .12) + charge / 3 + band * .11) % 1 + 1) % 1;
          const u = sign > 0 ? phase : 1 - phase;
          const fade = Math.sin(phase * Math.PI);
          for (let trail = 0; trail < 4; trail++) {
            const tailU = Math.max(0, Math.min(1, u - sign * trail * .017));
            dot(field(band, sign, tailU), 1.25 + energy * .9 - trail * .17, color, fade * (.78 - trail * .16));
          }
        }
      }
    }
    for (let sign = -1; sign <= 1; sign += 2) {
      const pole = rotate(sign * 119, 0, turn), color = palette[sign > 0 ? 0 : 2];
      const swell = .65 + .35 * Math.sin(t * .91 + sign * .7);
      bloom(pole[0], pole[1], 23 + energy * swell * 8, color, .32 + energy * .12);
      dot(pole, 3 + energy * swell, mix(color, [247, 250, 255], .3), .85);
      ctx.beginPath();
      ctx.arc(pole[0], pole[1], 7 + swell * energy * 3, 0, TAU);
      ctx.strokeStyle = rgba(color, opacity(.36));
      ctx.lineWidth = .75;
      ctx.stroke();
    }
  }

  function metal(t) {
    const palette = colors(), energy = gain();
    const neutral = light() ? [88, 113, 140] : [205, 220, 238];
    const profile = (angle, band) => {
      const ripple = Math.sin(angle * 3 + t * .64) * (4 + energy * 6) +
        Math.sin(angle * 5 - t * .43 + 1.1) * (2 + energy * 3) +
        Math.cos(angle * 2 - t * .29) * energy * 4;
      const radius = 124 + ripple + band * (2.5 + energy * .8);
      const flow = angle + Math.sin(angle * 4 - t * .53) * energy * .018;
      return polar(flow, radius);
    };
    const shell = [];
    for (let i = 0; i <= 120; i++) shell.push(profile(i / 120 * TAU, 0));

    // A moving specular gradient gives the shell its chrome character without
    // a white centre fill or a full-frame flash.
    const shine = t * .31 + Math.sin(t * .17) * .48;
    const direction = polar(shine, 149);
    const gradient = ctx.createLinearGradient(-direction[0], -direction[1], direction[0], direction[1]);
    gradient.addColorStop(0, rgba(palette[2], opacity(.2)));
    gradient.addColorStop(.23, rgba(mix(palette[0], neutral, .58), opacity(.61)));
    gradient.addColorStop(.36, rgba(palette[1], opacity(.22)));
    gradient.addColorStop(.52, rgba(neutral, opacity(.74)));
    gradient.addColorStop(.61, rgba(palette[2], opacity(.27)));
    gradient.addColorStop(.82, rgba(mix(palette[1], neutral, .4), opacity(.61)));
    gradient.addColorStop(1, rgba(palette[0], opacity(.24)));
    line(shell, palette[2], 24 + energy * 6, .08, true);
    ctx.beginPath();
    shell.forEach((point, index) => index ? ctx.lineTo(point[0], point[1]) : ctx.moveTo(point[0], point[1]));
    ctx.closePath();
    ctx.strokeStyle = gradient;
    ctx.lineWidth = 15 + energy * 3;
    ctx.lineJoin = 'round';
    ctx.stroke();

    // Fine parallel contours slide around the same deforming surface.
    for (let band = -2; band <= 2; band++) {
      const contour = [];
      for (let i = 0; i <= 120; i++) contour.push(profile(i / 120 * TAU, band));
      line(contour, mix(palette[(band + 3) % 3], neutral, .32), .6 + (band === 2 ? .3 : 0), band === 2 ? .58 : .24, true);
    }
    for (let glint = 0; glint < 3; glint++) {
      const angle = shine + glint * TAU / 3;
      const point = profile(angle, glint === 1 ? -1 : 1);
      bloom(point[0], point[1], 22 + energy * 8, palette[glint], .25);
      const highlight = [];
      for (let i = 0; i <= 20; i++) highlight.push(profile(angle - .16 + i * .016, 1));
      line(highlight, mix(palette[glint], neutral, .65), 1.3, .67);
    }
  }

  return { vortex, magnetic, metal };
}
if (typeof module !== 'undefined' && module.exports) module.exports = createDynamicEffectsA;
