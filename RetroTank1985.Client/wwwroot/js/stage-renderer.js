/**
 * stage-renderer.js — Lean Fast In-Game 60 FPS Canvas Blitter
 * Consumes C# RenderFrameDto snapshot and blits via NesCHR with 0 heap garbage.
 */

window.StageRenderer = (function () {
  const SCALE = 2;
  const BORDER = 16 * SCALE;
  const PLAYFIELD = 208 * SCALE;

  let subTileBufferCanvas = null;
  let subTileBufferCtx = null;
  let lastSubTiles = null;
  let treeSubTiles = [];

  function updateSubTileBuffer(subTiles) {
    if (!subTileBufferCanvas) {
      const arena = 26 * 8 * SCALE;
      subTileBufferCanvas = document.createElement('canvas');
      subTileBufferCanvas.width = arena;
      subTileBufferCanvas.height = arena;
      subTileBufferCtx = subTileBufferCanvas.getContext('2d');
      subTileBufferCtx.imageSmoothingEnabled = false;
    }

    const ctx = subTileBufferCtx;
    ctx.fillStyle = '#000000';
    ctx.fillRect(0, 0, subTileBufferCanvas.width, subTileBufferCanvas.height);

    treeSubTiles = [];
    const s8 = 8 * SCALE;

    for (let r = 0; r < 26; r++) {
      for (let c = 0; c < 26; c++) {
        const type = subTiles[r * 26 + c];
        const dx = c * s8, dy = r * s8;

        if (type === 1) window.NesCHR.drawCHRTile(ctx, 0x0F, 0, dx, dy, false, SCALE);
        else if (type === 2) window.NesCHR.drawCHRTile(ctx, 0x10, 3, dx, dy, false, SCALE);
        else if (type === 3) window.NesCHR.drawCHRTile(ctx, 0x12, 1, dx, dy, false, SCALE);
        else if (type === 4) treeSubTiles.push({ dx, dy }); // Tree cached for foreground
        else if (type === 5) window.NesCHR.drawCHRTile(ctx, 0x21, 3, dx, dy, false, SCALE);
        else if (type === 6 && r === 24 && c === 12) window.NesCHR.drawMetasprite(ctx, [0xC8, 0xCA, 0xC9, 0xCB], 0, dx, dy, false, SCALE);
        else if (type === 7 && r === 24 && c === 12) window.NesCHR.drawMetasprite(ctx, [0xCC, 0xCE, 0xCD, 0xCF], 0, dx, dy, false, SCALE);
      }
    }
    lastSubTiles = subTiles;
  }

  return {
    async init() {
      if (window.NesCHR) {
        await window.NesCHR.init();
        window.NesCHR.prewarmTileCache();
      }
      return true;
    },

    async prepareStageBackground(grid, stageNumber) {
      if (window.NesCHR) {
        await window.NesCHR.init();
        window.NesCHR.prewarmTileCache();
      }
      const arena = 26 * 8 * SCALE;
      if (!subTileBufferCanvas) {
        subTileBufferCanvas = document.createElement('canvas');
      }
      subTileBufferCanvas.width = arena;
      subTileBufferCanvas.height = arena;
      subTileBufferCtx = subTileBufferCanvas.getContext('2d');
      subTileBufferCtx.imageSmoothingEnabled = false;
      lastSubTiles = null;
      treeSubTiles = [];
    },

    renderGameFrame(canvas, frameData) {
      if (!canvas || !frameData || !window.NesCHR) return;
      const ctx = canvas.getContext('2d', { alpha: false });
      ctx.imageSmoothingEnabled = false;

      // 1. Outer border & Playfield
      ctx.fillStyle = '#636363';
      ctx.fillRect(0, 0, canvas.width, canvas.height);

      if (frameData.subTiles) updateSubTileBuffer(frameData.subTiles);
      if (subTileBufferCanvas) ctx.drawImage(subTileBufferCanvas, BORDER, BORDER);
      else { ctx.fillStyle = '#000000'; ctx.fillRect(BORDER, BORDER, PLAYFIELD, PLAYFIELD); }

      // 2. Bullets
      if (frameData.bullets && frameData.bullets.length > 0) {
        ctx.fillStyle = '#ffffff';
        const bLen = frameData.bullets.length;
        const bSize = 3 * SCALE;
        for (let i = 0; i < bLen; i++) {
          const b = frameData.bullets[i];
          ctx.fillRect(BORDER + b.x * SCALE, BORDER + b.y * SCALE, bSize, bSize);
        }
      }

      // 3. Player Tank
      if (frameData.pActive) {
        const px = BORDER + Math.round(frameData.pX) * SCALE;
        const py = BORDER + Math.round(frameData.pY) * SCALE;
        const T = frameData.pDir * 8 + (frameData.pAnim % 2) * 4;
        window.NesCHR.drawTankMetasprite(ctx, T, 4, px, py, true, SCALE);

        if (frameData.pShield) {
          const sBase = frameData.pShieldFrame === 0 ? 0x28 : 0x2C;
          window.NesCHR.drawTankMetasprite(ctx, sBase, 6, px, py, false, SCALE);
        }
      }

      // 4. Explosions
      if (frameData.explosions && frameData.explosions.length > 0) {
        const explosionFrames = [0xA0, 0xA2, 0xA4];
        const exLen = frameData.explosions.length;
        for (let i = 0; i < exLen; i++) {
          const ex = frameData.explosions[i];
          const tile = explosionFrames[Math.min(ex.frame, 2)];
          window.NesCHR.drawCHRTile(ctx, tile, 0, BORDER + Math.round(ex.x) * SCALE, BORDER + Math.round(ex.y) * SCALE, true, SCALE);
        }
      }

      // 5. Fast Foreground Trees
      const treeCount = treeSubTiles.length;
      if (treeCount > 0) {
        for (let i = 0; i < treeCount; i++) {
          const t = treeSubTiles[i];
          window.NesCHR.drawCHRTile(ctx, 0x22, 2, BORDER + t.dx, BORDER + t.dy, true, SCALE);
        }
      }

      // 6. Overlays
      if (frameData.isPaused) {
        ctx.fillStyle = 'rgba(0, 0, 0, 0.65)';
        ctx.fillRect(BORDER, BORDER, PLAYFIELD, PLAYFIELD);
        ctx.fillStyle = '#e74c3c'; ctx.font = 'bold 20px "Press Start 2P", monospace';
        ctx.textAlign = 'center'; ctx.fillText('PAUSE', BORDER + PLAYFIELD / 2, BORDER + PLAYFIELD / 2);
        ctx.textAlign = 'start';
      } else if (frameData.eagleDestroyed) {
        ctx.fillStyle = 'rgba(0, 0, 0, 0.7)';
        ctx.fillRect(BORDER, BORDER, PLAYFIELD, PLAYFIELD);
        ctx.fillStyle = '#ef4444'; ctx.font = 'bold 22px "Press Start 2P", monospace';
        ctx.textAlign = 'center'; ctx.fillText('GAME OVER', BORDER + PLAYFIELD / 2, BORDER + PLAYFIELD / 2 - 10);
        ctx.fillStyle = '#f1c40f'; ctx.font = 'bold 12px "Press Start 2P", monospace';
        ctx.fillText('EAGLE DESTROYED', BORDER + PLAYFIELD / 2, BORDER + PLAYFIELD / 2 + 25);
        ctx.textAlign = 'start';
      }
    }
  };
})();
