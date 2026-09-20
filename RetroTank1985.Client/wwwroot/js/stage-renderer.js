/**
 * stage-renderer.js — Stage Arena & CHR Tile Inspector Renderer
 * Authentic NES Battle City (1985) rendering engine for Blazor WASM.
 */

window.StageRenderer = (function () {
  const SCALE = 2; // 8px tile rendered as 16px, 16px metatile rendered as 32px
  const CHR_CELL = 9; // 8px tile + 1px grid border in chr_all.png
  const CHR_COLS = 32;

  // Authentic NES Master Palette (NTSC)
  const NES_MASTER = [
    '#545454','#001E74','#081090','#300088','#440064','#5C0030','#540400','#3C1800',
    '#202A00','#083A00','#004000','#003C00','#00323C','#000000','#000000','#000000',
    '#989698','#084CC4','#3032EC','#5C1EE4','#8814B0','#A01464','#982220','#783C00',
    '#545A00','#287200','#087C00','#007628','#006678','#000000','#000000','#000000',
    '#ECECEC','#4C9AEC','#787CEC','#B062EC','#E454EC','#EC58B4','#EC6A64','#D48820',
    '#A0AA00','#74C400','#4CD020','#38CC6C','#38B4CC','#3C3C3C','#000000','#000000',
    '#ECECEC','#A8CCEC','#BCBCEC','#D4B2EC','#E4AAEC','#ECAAD4','#ECB4B0','#E4C490',
    '#CCD278','#B4DE78','#A8E290','#98E2B4','#A0D6E4','#A0A2A0','#000000','#000000',
  ];

  // ROM PaletteData ($D44A)
  const ROM_PAL_BYTES = [
    [0x0F, 0x17, 0x06, 0x00], // BG0 brick
    [0x0F, 0x3C, 0x10, 0x12], // BG1 water
    [0x0F, 0x29, 0x09, 0x0B], // BG2 trees
    [0x0F, 0x00, 0x10, 0x20], // BG3 steel/ice
    [0x0F, 0x18, 0x27, 0x38], // SP0 P1 yellow
    [0x0F, 0x0A, 0x1B, 0x3B], // SP1 P2 green
    [0x0F, 0x0C, 0x10, 0x20], // SP2 enemy grey / powerups
    [0x0F, 0x04, 0x16, 0x20], // SP3 special / eagle ($E386)
  ];

  const PALETTES = ROM_PAL_BYTES.map(slot => slot.map(c => NES_MASTER[c & 0x3F]));

  // ROM TileCHRTable ($DB79) [TL, TR, BL, BR]
  const TILE_CHR = {
    0:  [0x00, 0x0F, 0x00, 0x0F], // Brick right col
    1:  [0x00, 0x00, 0x0F, 0x0F], // Brick bottom row
    2:  [0x0F, 0x00, 0x0F, 0x00], // Brick left col
    3:  [0x0F, 0x0F, 0x00, 0x00], // Brick top row
    4:  [0x0F, 0x0F, 0x0F, 0x0F], // Full brick
    5:  [0x20, 0x10, 0x20, 0x10], // Steel right col
    6:  [0x20, 0x20, 0x10, 0x10], // Steel bottom row
    7:  [0x10, 0x20, 0x10, 0x20], // Steel left col
    8:  [0x10, 0x10, 0x20, 0x20], // Steel top row
    9:  [0x10, 0x10, 0x10, 0x10], // Full steel
    10: [0x12, 0x12, 0x12, 0x12], // Water
    11: [0x22, 0x22, 0x22, 0x22], // Trees
    12: [0x21, 0x21, 0x21, 0x21], // Ice
    13: [0x00, 0x00, 0x00, 0x00], // Empty
    14: [0x00, 0x00, 0x00, 0x00],
    15: [0x00, 0x00, 0x00, 0x00],
  };

  // ROM TileAttrTable ($DB69)
  const TILE_PAL_MAP = [0, 0, 0, 0, 0, 3, 3, 3, 3, 3, 1, 2, 3, 0, 0, 0];

  let chrImage = null;
  let chrLoadPromise = null;
  const tileCache = new Map(); // key: `${tileAbs}_${palIdx}_${transparent}` -> HTMLCanvasElement

  function grayToIdx(grayVal) {
    if (grayVal < 0x2B) return 0;
    if (grayVal < 0x7F) return 1;
    if (grayVal < 0xD5) return 2;
    return 3;
  }

  const CHR_BORDER = 1;

  function loadCHRImage() {
    if (chrImage) return Promise.resolve(chrImage);
    if (chrLoadPromise) return chrLoadPromise;

    chrLoadPromise = new Promise((resolve, reject) => {
      const img = new Image();
      // Try root-relative path to ensure it loads in both standalone and hosted mode
      img.src = '/assets/sprites/chr_all.png';
      img.onload = () => {
        console.log('chr_all.png loaded successfully (size:', img.width, 'x', img.height, ')');
        chrImage = img;
        resolve(img);
      };
      img.onerror = (e) => {
        // Fallback relative path
        const fallback = new Image();
        fallback.src = 'assets/sprites/chr_all.png';
        fallback.onload = () => {
          chrImage = fallback;
          resolve(fallback);
        };
        fallback.onerror = (err) => {
          console.error('Failed to load chr_all.png:', err);
          reject(err);
        };
      };
    });

    return chrLoadPromise;
  }

  function getCachedCHRTile(tileAbs, palIdx, transparent = false, scale = SCALE) {
    const key = `${tileAbs}_${palIdx}_${transparent}_${scale}`;
    if (tileCache.has(key)) return tileCache.get(key);
    if (!chrImage) return null;

    const col = tileAbs % CHR_COLS;
    const row = Math.floor(tileAbs / CHR_COLS);
    const sx = col * CHR_CELL + CHR_BORDER;
    const sy = row * CHR_CELL + CHR_BORDER;

    // Read pixel values from chrImage
    const tempCanvas = document.createElement('canvas');
    tempCanvas.width = 8;
    tempCanvas.height = 8;
    const tempCtx = tempCanvas.getContext('2d', { willReadFrequently: true });
    tempCtx.drawImage(chrImage, sx, sy, 8, 8, 0, 0, 8, 8);
    const imgData = tempCtx.getImageData(0, 0, 8, 8).data;

    // Create colored tile canvas
    const outCanvas = document.createElement('canvas');
    outCanvas.width = 8 * scale;
    outCanvas.height = 8 * scale;
    const outCtx = outCanvas.getContext('2d');
    outCtx.imageSmoothingEnabled = false;

    const pal = PALETTES[palIdx] || PALETTES[0];

    for (let y = 0; y < 8; y++) {
      for (let x = 0; x < 8; x++) {
        const offset = (y * 8 + x) * 4;
        const cidx = grayToIdx(imgData[offset]);
        if (transparent && cidx === 0) continue;
        outCtx.fillStyle = pal[cidx];
        outCtx.fillRect(x * scale, y * scale, scale, scale);
      }
    }

    tileCache.set(key, outCanvas);
    return outCanvas;
  }

  function drawCHRTile(ctx, tileAbs, palIdx, dx, dy, transparent = false, scale = SCALE) {
    const cached = getCachedCHRTile(tileAbs, palIdx, transparent, scale);
    if (cached) {
      ctx.drawImage(cached, dx, dy);
    }
  }

  // Draw 2x2 CHR Metasprite (16x16 px)
  function drawMetasprite(ctx, tiles, palIdx, dx, dy, pt1 = false, scale = SCALE) {
    tiles.forEach((t, i) => {
      const tileAbs = (pt1 ? 256 : 0) + t;
      const ox = dx + (i % 2) * 8 * scale;
      const oy = dy + Math.floor(i / 2) * 8 * scale;
      drawCHRTile(ctx, tileAbs, palIdx, ox, oy, true, scale);
    });
  }

  return {
    async init() {
      await loadCHRImage();
      return true;
    },

    async renderStage(canvasOrId, grid, options = {}) {
      await loadCHRImage();
      const canvas = typeof canvasOrId === 'string' ? document.getElementById(canvasOrId) : canvasOrId;
      if (!canvas) {
        console.warn(`[StageRenderer] canvas not found in DOM`);
        return;
      }
      if (!grid || !grid.length) {
        console.warn(`[StageRenderer] grid is empty or null`);
        return;
      }
      console.log(`[StageRenderer] rendering stage onto ${canvas.id || 'canvas'}, grid rows:`, grid.length);

      const {
        showGrid = false,
        showSpawns = true,
        showCoords = false,
        scale = 2 // 208x208 NES px scaled to 416x416
      } = options;

      const ctx = canvas.getContext('2d');
      ctx.imageSmoothingEnabled = false;

      const arenaSize = 13 * 16 * scale; // 416px
      const borderWidth = 16 * scale;     // 32px border for HUD/look
      const totalWidth = arenaSize + borderWidth * 2;
      const totalHeight = arenaSize + borderWidth * 2;

      canvas.width = totalWidth;
      canvas.height = totalHeight;

      // Dark background outside & arena
      ctx.fillStyle = '#636363'; // NES Border grey
      ctx.fillRect(0, 0, totalWidth, totalHeight);

      // Playfield arena black
      ctx.fillStyle = '#000000';
      ctx.fillRect(borderWidth, borderWidth, arenaSize, arenaSize);

      const treesTiles = [];

      // 1. Draw Ground / Bricks / Steel / Water / Ice
      for (let r = 0; r < 13; r++) {
        for (let c = 0; c < 13; c++) {
          const tileType = grid[r][c];
          if (tileType >= 13) continue; // Empty

          const dx = borderWidth + c * 16 * scale;
          const dy = borderWidth + r * 16 * scale;

          if (tileType === 11) {
            // Trees are drawn on top after everything else
            treesTiles.push({ c, r, dx, dy });
            continue;
          }

          const chrList = TILE_CHR[tileType] || TILE_CHR[4];
          const palIdx = TILE_PAL_MAP[tileType] || 0;

          chrList.forEach((t, i) => {
            if (t === 0x00) return; // Transparent sub-tile
            const subX = dx + (i % 2) * 8 * scale;
            const subY = dy + Math.floor(i / 2) * 8 * scale;
            drawCHRTile(ctx, t, palIdx, subX, subY, false, scale);
          });
        }
      }

      // 2. Draw Eagle Base at col 6, row 12 (bottom center)
      // Standard NES Battle City Eagle at bottom center
      // Eagle tiles: [0xC8, 0xCA, 0xC9, 0xCB] with BG0 palette
      const eagleX = borderWidth + 6 * 16 * scale;
      const eagleY = borderWidth + 12 * 16 * scale;
      drawMetasprite(ctx, [0xC8, 0xCA, 0xC9, 0xCB], 0, eagleX, eagleY, false, scale);

      // Authentic NES Battle City Eagle Fortification (ROM EAGLE_WALL)
      // The eagle is protected by a Π-shaped brick wall in 5 cells:
      // (11, 5): Bottom-Right sub-tile brick only
      // (11, 6): Bottom-Left + Bottom-Right sub-tile bricks (top horizontal beam)
      // (11, 7): Bottom-Left sub-tile brick only
      // (12, 5): Top-Right + Bottom-Right sub-tile bricks (left vertical column)
      // (12, 7): Top-Left + Bottom-Left sub-tile bricks (right vertical column)
      // Quadrants: index 0=TL, 1=TR, 2=BL, 3=BR
      const eagleWallCells = [
        { r: 11, c: 5, quads: [false, false, false, true] }, // BR only
        { r: 11, c: 6, quads: [false, false, true, true]  }, // BL + BR
        { r: 11, c: 7, quads: [false, false, true, false] }, // BL only
        { r: 12, c: 5, quads: [false, true, false, true]  }, // TR + BR
        { r: 12, c: 7, quads: [true, false, true, false]  }  // TL + BL
      ];

      eagleWallCells.forEach(cell => {
        // Only draw eagle wall if stage map didn't already override this cell with a non-empty tile
        if (!grid[cell.r] || grid[cell.r][cell.c] >= 13) {
          const cx = borderWidth + cell.c * 16 * scale;
          const cy = borderWidth + cell.r * 16 * scale;
          cell.quads.forEach((hasBrick, qIdx) => {
            if (!hasBrick) return;
            const subX = cx + (qIdx % 2) * 8 * scale;
            const subY = cy + Math.floor(qIdx / 2) * 8 * scale;
            // 0x0F = solid brick sub-tile in CHR-ROM
            drawCHRTile(ctx, 0x0F, 0, subX, subY, false, scale);
          });
        }
      });

      // 3. Draw Trees (Top Layer)
      treesTiles.forEach(item => {
        const chrList = TILE_CHR[11];
        chrList.forEach((t, i) => {
          const subX = item.dx + (i % 2) * 8 * scale;
          const subY = item.dy + Math.floor(i / 2) * 8 * scale;
          drawCHRTile(ctx, t, 2, subX, subY, true, scale);
        });
      });

      // 4. Draw Spawn Markers
      if (showSpawns) {
        // Enemy spawns (Top row: col 0, col 6, col 12)
        const enemySpawns = [
          { c: 0, r: 0, label: 'E1' },
          { c: 6, r: 0, label: 'E2' },
          { c: 12, r: 0, label: 'E3' }
        ];
        enemySpawns.forEach(s => {
          const sx = borderWidth + s.c * 16 * scale;
          const sy = borderWidth + s.r * 16 * scale;
          ctx.strokeStyle = '#e74c3c';
          ctx.lineWidth = 2;
          ctx.setLineDash([4, 2]);
          ctx.strokeRect(sx + 2, sy + 2, 16 * scale - 4, 16 * scale - 4);
          ctx.setLineDash([]);
          ctx.fillStyle = '#e74c3c';
          ctx.font = 'bold 10px monospace';
          ctx.fillText(s.label, sx + 5, sy + 14);
        });

        // Player spawns (Bottom row: P1 at col 4, row 12; P2 at col 8, row 12)
        const playerSpawns = [
          { c: 4, r: 12, label: 'P1', color: '#f1c40f' },
          { c: 8, r: 12, label: 'P2', color: '#2ecc71' }
        ];
        playerSpawns.forEach(s => {
          const sx = borderWidth + s.c * 16 * scale;
          const sy = borderWidth + s.r * 16 * scale;
          ctx.strokeStyle = s.color;
          ctx.lineWidth = 2;
          ctx.setLineDash([4, 2]);
          ctx.strokeRect(sx + 2, sy + 2, 16 * scale - 4, 16 * scale - 4);
          ctx.setLineDash([]);
          ctx.fillStyle = s.color;
          ctx.font = 'bold 10px monospace';
          ctx.fillText(s.label, sx + 5, sy + 14);
        });
      }

      // 5. Draw Grid lines
      if (showGrid) {
        ctx.strokeStyle = 'rgba(255, 255, 255, 0.15)';
        ctx.lineWidth = 1;
        for (let i = 0; i <= 13; i++) {
          const pos = borderWidth + i * 16 * scale;
          // Vertical
          ctx.beginPath();
          ctx.moveTo(pos, borderWidth);
          ctx.lineTo(pos, borderWidth + arenaSize);
          ctx.stroke();
          // Horizontal
          ctx.beginPath();
          ctx.moveTo(borderWidth, pos);
          ctx.lineTo(borderWidth + arenaSize, pos);
          ctx.stroke();
        }
      }

      // 6. Draw Coordinate Numbers
      if (showCoords) {
        ctx.fillStyle = '#f1c40f';
        ctx.font = '9px monospace';
        for (let i = 0; i < 13; i++) {
          const colX = borderWidth + i * 16 * scale + 4;
          ctx.fillText(`${i}`, colX, borderWidth - 4);
          const rowY = borderWidth + i * 16 * scale + 12;
          ctx.fillText(`${i}`, 4, rowY);
        }
      }
    },

    async renderCHRSheet(canvasId, palIdx = 0, scale = 2) {
      await loadCHRImage();
      const canvas = document.getElementById(canvasId);
      if (!canvas) return;

      const ctx = canvas.getContext('2d');
      ctx.imageSmoothingEnabled = false;

      const cols = 32;
      const rows = 16;
      const tileAreaSize = 8 * scale;
      const gap = 1;

      canvas.width = cols * (tileAreaSize + gap);
      canvas.height = rows * (tileAreaSize + gap);

      ctx.fillStyle = '#0c0d12';
      ctx.fillRect(0, 0, canvas.width, canvas.height);

      for (let t = 0; t < 512; t++) {
        const c = t % cols;
        const r = Math.floor(t / cols);
        const dx = c * (tileAreaSize + gap);
        const dy = r * (tileAreaSize + gap);

        const cached = getCachedCHRTile(t, palIdx, false, scale);
        if (cached) {
          ctx.drawImage(cached, dx, dy);
        }
      }
    },

    async renderTankPreview(canvasId, enemyType, dir = 0, animFrame = 0, scale = 3) {
      await loadCHRImage();
      const canvas = document.getElementById(canvasId);
      if (!canvas) return;

      const ctx = canvas.getContext('2d');
      ctx.imageSmoothingEnabled = false;
      canvas.width = 16 * scale;
      canvas.height = 16 * scale;
      ctx.clearRect(0, 0, canvas.width, canvas.height);

      // Base offset calculation:
      // Player: 0x00, Enemy T0: 0x80, T1: 0xA0, T2: 0xC0, T3: 0xE0
      let base = 0x00;
      let palIdx = 4; // Yellow

      if (enemyType === 0) { base = 0x80; palIdx = 6; } // Basic
      else if (enemyType === 1) { base = 0xA0; palIdx = 6; } // Fast
      else if (enemyType === 2) { base = 0xC0; palIdx = 6; } // Power
      else if (enemyType === 3) { base = 0xE0; palIdx = 6; } // Armor
      else if (enemyType === -1) { base = 0x00; palIdx = 4; } // Player 1
      else if (enemyType === -2) { base = 0x00; palIdx = 5; } // Player 2

      // dir: 0=UP, 1=LEFT, 2=DOWN, 3=RIGHT
      const dirOffset = dir * 8;
      const frameOffset = (animFrame % 2) * 4;
      const T = base + dirOffset + frameOffset;

      const tiles = [T, T + 2, T + 1, T + 3];
      drawMetasprite(ctx, tiles, palIdx, 0, 0, true, scale);
    },

    async renderMetaspriteCustom(canvasId, tiles, palIdx, pt1 = false, scale = 4) {
      await loadCHRImage();
      const canvas = document.getElementById(canvasId);
      if (!canvas) return;

      const ctx = canvas.getContext('2d');
      ctx.imageSmoothingEnabled = false;
      canvas.width = 16 * scale;
      canvas.height = 16 * scale;
      ctx.clearRect(0, 0, canvas.width, canvas.height);

      drawMetasprite(ctx, tiles, palIdx, 0, 0, pt1, scale);
    },

    drawMetasprite(ctx, tiles, palIdx, dx, dy, pt1 = false, scale = SCALE) {
      drawMetasprite(ctx, tiles, palIdx, dx, dy, pt1, scale);
    },

    getCachedTile(tileAbs, palIdx, transparent = false, scale = SCALE) {
      return getCachedCHRTile(tileAbs, palIdx, transparent, scale);
    }
  };
})();
