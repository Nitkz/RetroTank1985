/**
 * stage-inspector.js — Dedicated ROM Stage & CHR Inspector Tools
 * Powers the /stage-inspector page with stage previews, CHR-ROM sheet, and tank directions.
 */

window.StageInspector = (function () {
  const TILE_CHR = {
    0:  [0x00, 0x0F, 0x00, 0x0F], 1:  [0x00, 0x00, 0x0F, 0x0F], 2:  [0x0F, 0x00, 0x0F, 0x00],
    3:  [0x0F, 0x0F, 0x00, 0x00], 4:  [0x0F, 0x0F, 0x0F, 0x0F], 5:  [0x20, 0x10, 0x20, 0x10],
    6:  [0x20, 0x20, 0x10, 0x10], 7:  [0x10, 0x20, 0x10, 0x20], 8:  [0x10, 0x10, 0x20, 0x20],
    9:  [0x10, 0x10, 0x10, 0x10], 10: [0x12, 0x12, 0x12, 0x12], 11: [0x22, 0x22, 0x22, 0x22],
    12: [0x21, 0x21, 0x21, 0x21], 13: [0x00, 0x00, 0x00, 0x00]
  };

  const TILE_PAL_MAP = [0, 0, 0, 0, 0, 3, 3, 3, 3, 3, 1, 2, 3, 0];

  async function waitForCanvas(canvasOrId) {
    if (typeof canvasOrId !== 'string') return canvasOrId;
    let canvas = document.getElementById(canvasOrId);
    for (let i = 0; !canvas && i < 6; i++) {
      await new Promise(r => setTimeout(r, 50));
      canvas = document.getElementById(canvasOrId);
    }
    return canvas;
  }

  return {
    async renderStage(canvasOrId, grid, options = {}) {
      if (!window.NesCHR) return;
      await window.NesCHR.init();
      const canvas = await waitForCanvas(canvasOrId);
      if (!canvas || !grid) return;

      const { showGrid = false, showSpawns = true, showCoords = false, scale = 2 } = options;
      const ctx = canvas.getContext('2d');
      ctx.imageSmoothingEnabled = false;

      const arenaSize = 13 * 16 * scale;
      const border = 16 * scale;
      const total = arenaSize + border * 2;
      canvas.width = total; canvas.height = total;

      ctx.fillStyle = '#636363'; ctx.fillRect(0, 0, total, total);
      ctx.fillStyle = '#000000'; ctx.fillRect(border, border, arenaSize, arenaSize);

      const trees = [];
      for (let r = 0; r < 13; r++) {
        for (let c = 0; c < 13; c++) {
          const type = grid[r][c];
          if (type >= 13) continue;
          const dx = border + c * 16 * scale;
          const dy = border + r * 16 * scale;

          if (type === 11) { trees.push({ dx, dy }); continue; }

          const chrList = TILE_CHR[type] || TILE_CHR[4];
          const palIdx = TILE_PAL_MAP[type] || 0;
          for (let i = 0; i < chrList.length; i++) {
            const t = chrList[i];
            if (t === 0) continue;
            window.NesCHR.drawCHRTile(ctx, t, palIdx, dx + (i % 2) * 8 * scale, dy + Math.floor(i / 2) * 8 * scale, false, scale);
          }
        }
      }

      // Eagle base & fortification
      window.NesCHR.drawMetasprite(ctx, [0xC8, 0xCA, 0xC9, 0xCB], 0, border + 6 * 16 * scale, border + 12 * 16 * scale, false, scale);
      const eagleWalls = [
        { r: 11, c: 5, q: [3] }, { r: 11, c: 6, q: [2, 3] }, { r: 11, c: 7, q: [2] },
        { r: 12, c: 5, q: [1, 3] }, { r: 12, c: 7, q: [0, 2] }
      ];
      for (let i = 0; i < eagleWalls.length; i++) {
        const cell = eagleWalls[i];
        if (!grid[cell.r] || grid[cell.r][cell.c] >= 13) {
          const cx = border + cell.c * 16 * scale;
          const cy = border + cell.r * 16 * scale;
          for (let qi = 0; qi < cell.q.length; qi++) {
            const qIdx = cell.q[qi];
            const qx = cx + (qIdx % 2) * 8 * scale;
            const qy = cy + Math.floor(qIdx / 2) * 8 * scale;
            window.NesCHR.drawCHRTile(ctx, 0x0F, 0, qx, qy, false, scale);
          }
        }
      }

      // Trees layer
      for (let i = 0; i < trees.length; i++) {
        const t = trees[i];
        for (let j = 0; j < 4; j++) {
          window.NesCHR.drawCHRTile(ctx, 0x22, 2, t.dx + (j % 2) * 8 * scale, t.dy + Math.floor(j / 2) * 8 * scale, true, scale);
        }
      }

      // Markers
      if (showSpawns) {
        const spawns = [{ c: 0, r: 0, l: 'E1' }, { c: 6, r: 0, l: 'E2' }, { c: 12, r: 0, l: 'E3' }];
        for (let i = 0; i < spawns.length; i++) {
          const s = spawns[i];
          const sx = border + s.c * 16 * scale, sy = border + s.r * 16 * scale;
          ctx.strokeStyle = '#e74c3c'; ctx.lineWidth = 2; ctx.strokeRect(sx + 2, sy + 2, 16 * scale - 4, 16 * scale - 4);
          ctx.fillStyle = '#e74c3c'; ctx.font = 'bold 10px monospace'; ctx.fillText(s.l, sx + 5, sy + 14);
        }
        const players = [{ c: 4, r: 12, l: 'P1', col: '#f1c40f' }, { c: 8, r: 12, l: 'P2', col: '#2ecc71' }];
        for (let i = 0; i < players.length; i++) {
          const s = players[i];
          const sx = border + s.c * 16 * scale, sy = border + s.r * 16 * scale;
          ctx.strokeStyle = s.col; ctx.lineWidth = 2; ctx.strokeRect(sx + 2, sy + 2, 16 * scale - 4, 16 * scale - 4);
          ctx.fillStyle = s.col; ctx.font = 'bold 10px monospace'; ctx.fillText(s.l, sx + 5, sy + 14);
        }
      }

      if (showGrid) {
        ctx.strokeStyle = 'rgba(255, 255, 255, 0.15)'; ctx.lineWidth = 1;
        for (let i = 0; i <= 13; i++) {
          const pos = border + i * 16 * scale;
          ctx.beginPath(); ctx.moveTo(pos, border); ctx.lineTo(pos, border + arenaSize); ctx.stroke();
          ctx.beginPath(); ctx.moveTo(border, pos); ctx.lineTo(border + arenaSize, pos); ctx.stroke();
        }
      }

      if (showCoords) {
        ctx.fillStyle = '#f1c40f'; ctx.font = '9px monospace';
        for (let i = 0; i < 13; i++) {
          ctx.fillText(`${i}`, border + i * 16 * scale + 4, border - 4);
          ctx.fillText(`${i}`, 4, border + i * 16 * scale + 12);
        }
      }
    },

    async renderCHRSheet(canvasId, palIdx = 0, scale = 2) {
      if (!window.NesCHR) return;
      await window.NesCHR.init();
      const canvas = await waitForCanvas(canvasId);
      if (!canvas) return;
      const ctx = canvas.getContext('2d');
      const size = 8 * scale;
      canvas.width = 32 * (size + 1); canvas.height = 16 * (size + 1);
      ctx.fillStyle = '#0c0d12'; ctx.fillRect(0, 0, canvas.width, canvas.height);
      for (let t = 0; t < 512; t++) {
        const cached = window.NesCHR.getCachedCHRTile(t, palIdx, false, scale);
        if (cached) ctx.drawImage(cached, (t % 32) * (size + 1), Math.floor(t / 32) * (size + 1));
      }
    },

    async renderTankPreview(canvasId, enemyType, dir = 0, animFrame = 0, scale = 3) {
      if (!window.NesCHR) return;
      await window.NesCHR.init();
      const canvas = await waitForCanvas(canvasId);
      if (!canvas) return;
      const ctx = canvas.getContext('2d');
      canvas.width = 16 * scale; canvas.height = 16 * scale;
      ctx.clearRect(0, 0, canvas.width, canvas.height);

      let base = 0x00, pal = 4;
      if (enemyType === 0) { base = 0x80; pal = 6; }
      else if (enemyType === 1) { base = 0xA0; pal = 6; }
      else if (enemyType === 2) { base = 0xC0; pal = 6; }
      else if (enemyType === 3) { base = 0xE0; pal = 6; }
      else if (enemyType === -2) { base = 0x00; pal = 5; }

      const T = base + dir * 8 + (animFrame % 2) * 4;
      window.NesCHR.drawTankMetasprite(ctx, T, pal, 0, 0, true, scale);
    },

    async renderMetaspriteCustom(canvasId, tiles, palIdx, pt1 = false, scale = 4) {
      if (!window.NesCHR) return;
      await window.NesCHR.init();
      const canvas = await waitForCanvas(canvasId);
      if (!canvas) return;
      const ctx = canvas.getContext('2d');
      canvas.width = 16 * scale; canvas.height = 16 * scale;
      ctx.clearRect(0, 0, canvas.width, canvas.height);
      window.NesCHR.drawMetasprite(ctx, tiles, palIdx, 0, 0, pt1, scale);
    },

    async renderSingleTileCustom(canvasId, tileId, palIdx, pt1 = false, scale = 4) {
      if (!window.NesCHR) return;
      await window.NesCHR.init();
      const canvas = await waitForCanvas(canvasId);
      if (!canvas) return;
      const ctx = canvas.getContext('2d');
      canvas.width = 16 * scale; canvas.height = 16 * scale;
      ctx.clearRect(0, 0, canvas.width, canvas.height);
      const baseOffset = pt1 ? 256 : 0;
      // Draw centered in 16x16 canvas
      window.NesCHR.drawCHRTile(ctx, baseOffset + tileId, palIdx, 4 * scale, 4 * scale, true, scale);
    },

    async renderExpandSpriteCustom(canvasId, base, palIdx, scale = 2) {
      if (!window.NesCHR) return;
      await window.NesCHR.init();
      const canvas = await waitForCanvas(canvasId);
      if (!canvas) return;
      const ctx = canvas.getContext('2d');
      canvas.width = 32 * scale; canvas.height = 32 * scale;
      ctx.clearRect(0, 0, canvas.width, canvas.height);
      window.NesCHR.drawExpandSprite(ctx, base, palIdx, 0, 0, scale);
    }
  };
})();
