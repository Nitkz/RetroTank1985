/**
 * nes-chr.js — Shared Core CHR-ROM & NES Master Palette Engine
 * Handles ROM tile decoding, NTSC palette lookup, and pixel-perfect tile caching.
 */

window.NesCHR = (function () {
  const SCALE = 2;
  const CHR_CELL = 9;
  const CHR_COLS = 32;

  // NES Master Palette (NTSC)
  const NES_MASTER = [
    '#545454','#001E74','#081090','#300088','#440064','#5C0030','#540400','#3C1800',
    '#202A00','#083A00','#004000','#003C00','#00323C','#000000','#000000','#000000',
    '#989698','#084CC4','#3032EC','#5C1EE4','#8814B0','#A01464','#982220','#783C00',
    '#545A00','#287200','#087C00','#007628','#006678','#000000','#000000','#000000',
    '#ECECEC','#4C9AEC','#787CEC','#B062EC','#E454EC','#EC58B4','#EC6A64','#D48820',
    '#A0AA00','#74C400','#4CD020','#38CC6C','#38B4CC','#3C3C3C','#000000','#000000',
    '#ECECEC','#A8CCEC','#BCBCEC','#D4B2EC','#E4AAEC','#ECAAD4','#ECB4B0','#E4C490',
    '#CCD278','#B4DE78','#A8E290','#98E2B4','#A0D6E4','#A0A2A0','#000000','#000000'
  ];

  // ROM Palettes ($D44A)
  const ROM_PAL_BYTES = [
    [0x0F, 0x17, 0x06, 0x00], // BG0 brick
    [0x0F, 0x3C, 0x10, 0x12], // BG1 water
    [0x0F, 0x29, 0x09, 0x0B], // BG2 trees
    [0x0F, 0x00, 0x10, 0x20], // BG3 steel/ice
    [0x0F, 0x18, 0x27, 0x38], // SP0 P1 yellow
    [0x0F, 0x0A, 0x1B, 0x3B], // SP1 P2 green
    [0x0F, 0x0C, 0x10, 0x20], // SP2 enemy grey / powerups
    [0x0F, 0x04, 0x16, 0x20]  // SP3 eagle/special
  ];

  const PALETTES = ROM_PAL_BYTES.map(slot => slot.map(c => NES_MASTER[c & 0x3F]));

  let chrImage = null;
  let chrLoadPromise = null;
  const tileCache = new Map();

  function grayToIdx(v) {
    return v < 0x2B ? 0 : v < 0x7F ? 1 : v < 0xD5 ? 2 : 3;
  }

  function loadCHRImage() {
    if (chrImage) return Promise.resolve(chrImage);
    if (chrLoadPromise) return chrLoadPromise;

    chrLoadPromise = new Promise((resolve, reject) => {
      const img = new Image();
      img.src = '/assets/sprites/chr_all.png';
      img.onload = () => { chrImage = img; resolve(img); };
      img.onerror = () => {
        const fallback = new Image();
        fallback.src = 'assets/sprites/chr_all.png';
        fallback.onload = () => { chrImage = fallback; resolve(fallback); };
        fallback.onerror = reject;
      };
    });
    return chrLoadPromise;
  }

  function getCachedCHRTile(tileAbs, palIdx, transparent = false, scale = SCALE) {
    const key = `${tileAbs}_${palIdx}_${transparent}_${scale}`;
    if (tileCache.has(key)) return tileCache.get(key);
    if (!chrImage) return null;

    const sx = (tileAbs % CHR_COLS) * CHR_CELL + 1;
    const sy = Math.floor(tileAbs / CHR_COLS) * CHR_CELL + 1;

    const tmp = document.createElement('canvas');
    tmp.width = 8; tmp.height = 8;
    const tCtx = tmp.getContext('2d', { willReadFrequently: true });
    tCtx.drawImage(chrImage, sx, sy, 8, 8, 0, 0, 8, 8);
    const data = tCtx.getImageData(0, 0, 8, 8).data;

    const out = document.createElement('canvas');
    out.width = 8 * scale; out.height = 8 * scale;
    const outCtx = out.getContext('2d');
    outCtx.imageSmoothingEnabled = false;

    const pal = PALETTES[palIdx] || PALETTES[0];
    for (let y = 0; y < 8; y++) {
      for (let x = 0; x < 8; x++) {
        const cidx = grayToIdx(data[(y * 8 + x) * 4]);
        if (transparent && cidx === 0) continue;
        outCtx.fillStyle = pal[cidx];
        outCtx.fillRect(x * scale, y * scale, scale, scale);
      }
    }

    tileCache.set(key, out);
    return out;
  }

  function drawCHRTile(ctx, tileAbs, palIdx, dx, dy, transparent = false, scale = SCALE) {
    const cached = getCachedCHRTile(tileAbs, palIdx, transparent, scale);
    if (cached) ctx.drawImage(cached, dx, dy);
  }

  function drawMetasprite(ctx, tiles, palIdx, dx, dy, pt1 = false, scale = SCALE) {
    const s8 = 8 * scale;
    const baseOffset = pt1 ? 256 : 0;
    for (let i = 0; i < tiles.length; i++) {
      const tileAbs = baseOffset + tiles[i];
      const ox = dx + (i % 2) * s8;
      const oy = dy + Math.floor(i / 2) * s8;
      drawCHRTile(ctx, tileAbs, palIdx, ox, oy, true, scale);
    }
  }

  function drawTankMetasprite(ctx, baseTile, palIdx, dx, dy, pt1 = true, scale = SCALE) {
    const s8 = 8 * scale;
    const baseOffset = pt1 ? 256 : 0;
    drawCHRTile(ctx, baseOffset + baseTile, palIdx, dx, dy, true, scale);
    drawCHRTile(ctx, baseOffset + baseTile + 2, palIdx, dx + s8, dy, true, scale);
    drawCHRTile(ctx, baseOffset + baseTile + 1, palIdx, dx, dy + s8, true, scale);
    drawCHRTile(ctx, baseOffset + baseTile + 3, palIdx, dx + s8, dy + s8, true, scale);
  }

  function prewarmTileCache() {
    const commonBgTiles = [0x0F, 0x10, 0x12, 0x21, 0x22, 0xC8, 0xC9, 0xCA, 0xCB, 0xCC, 0xCD, 0xCE, 0xCF];
    for (let i = 0; i < commonBgTiles.length; i++) {
      for (let pal = 0; pal < 4; pal++) {
        getCachedCHRTile(commonBgTiles[i], pal, false, SCALE);
        getCachedCHRTile(commonBgTiles[i], pal, true, SCALE);
      }
    }

    // Pre-warm Player Tiers (0x00, 0x20, 0x40, 0x60) & Enemy Tank Metasprites (Basic 0x80, Fast 0xA0, Power 0xC0, Armor 0xE0)
    const tankBases = [0x00, 0x20, 0x40, 0x60, 0x80, 0xA0, 0xC0, 0xE0];
    const tankPalettes = [4, 5, 6, 7]; // Yellow, Green, Grey, Red

    for (let b = 0; b < tankBases.length; b++) {
      const base = tankBases[b];
      for (let d = 0; d < 4; d++) {
        for (let a = 0; a < 2; a++) {
          const t = 256 + (base + d * 8 + a * 4);
          for (let p = 0; p < tankPalettes.length; p++) {
            const pal = tankPalettes[p];
            getCachedCHRTile(t, pal, true, SCALE);
            getCachedCHRTile(t + 1, pal, true, SCALE);
            getCachedCHRTile(t + 2, pal, true, SCALE);
            getCachedCHRTile(t + 3, pal, true, SCALE);
          }
        }
      }
    }

    // Spawn stars & Shield & Explosions
    const specialTiles = [0x28, 0x29, 0x2A, 0x2B, 0x2C, 0x2D, 0x2E, 0x2F, 0xA0, 0xA2, 0xA4, 0xA6, 0xAD, 0xA9, 0xA5, 0xA1];
    for (let i = 0; i < specialTiles.length; i++) {
      getCachedCHRTile(specialTiles[i], 0, true, SCALE);
      getCachedCHRTile(specialTiles[i], 6, true, SCALE);
      getCachedCHRTile(specialTiles[i], 7, true, SCALE);
    }
  }


  return {
    SCALE,
    PALETTES,
    init: loadCHRImage,
    prewarmTileCache,
    drawCHRTile,
    drawMetasprite,
    drawTankMetasprite,
    getCachedCHRTile
  };
})();
