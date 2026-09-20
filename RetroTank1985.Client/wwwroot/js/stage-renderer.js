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

      // 3. Enemy Tanks & Spawning Stars
      if (frameData.enemies && frameData.enemies.length > 0) {
        const SPAWN_STARS = [0xAD, 0xA9, 0xA5, 0xA1];
        const eLen = frameData.enemies.length;
        const nowFrame = Math.floor(performance.now() / 130);

        for (let i = 0; i < eLen; i++) {
          const e = frameData.enemies[i];
          const ex = BORDER + Math.round(e.x) * SCALE;
          const ey = BORDER + Math.round(e.y) * SCALE;

          if (e.isSpawning) {
            // 4-Frame Spawning Star Sparkle Metasprite (0xAD, 0xA9, 0xA5, 0xA1)
            const starPhase = Math.min(3, Math.floor((30 - e.spawnTimer) / 7.5));
            const starTile = SPAWN_STARS[starPhase];
            window.NesCHR.drawMetasprite(ctx, [starTile, starTile + 2, starTile + 1, starTile + 3], 7, ex, ey, false, SCALE);
          } else {
            // Determine Palette & CHR base for Archetype
            let base = 0x80;
            let pal = 6; // Default Grey

            if (e.type === 0) { base = 0x80; pal = 6; }
            else if (e.type === 1) { base = 0xA0; pal = 6; }
            else if (e.type === 2) { base = 0xC0; pal = 6; }
            else if (e.type === 3) {
              base = 0xE0;
              // Armor Tank color shifts based on HP: 4:Green (5), 3:Yellow (4), 2:Red/Orange (7), 1:Grey (6)
              if (e.hp >= 4) pal = 5;
              else if (e.hp === 3) pal = 4;
              else if (e.hp === 2) pal = 7;
              else pal = 6;
            }

            // Red Flashing Tank: alternate between base palette and SP3 (7) every few frames
            if (e.isFlashing && (nowFrame % 2 === 0)) {
              pal = 7;
            }

            const T = base + e.dir * 8 + (e.anim % 2) * 4;
            window.NesCHR.drawTankMetasprite(ctx, T, pal, ex, ey, true, SCALE);
          }
        }
      }

      // 4. Player Tank (Upgrades visual sprite based on StarPower 0=0x00, 1=0x20, 2=0x40, 3=0x60)
      if (frameData.pActive) {
        const px = BORDER + Math.round(frameData.pX) * SCALE;
        const py = BORDER + Math.round(frameData.pY) * SCALE;
        const starTier = Math.min(3, Math.max(0, frameData.pStarPower || 0));
        const tierBase = starTier * 0x20;
        const T = tierBase + frameData.pDir * 8 + (frameData.pAnim % 2) * 4;
        window.NesCHR.drawTankMetasprite(ctx, T, 4, px, py, true, SCALE);

        if (frameData.pShield) {
          const sBase = frameData.pShieldFrame === 0 ? 0x28 : 0x2C;
          window.NesCHR.drawTankMetasprite(ctx, sBase, 6, px, py, false, SCALE);
        }
      }

      // 5. Droppable Power-Up Items (0:Helmet=0x80, 1:Timer=0x84, 2:Shovel=0x88, 3:Star=0x8C, 4:Grenade=0x90, 5:TankLife=0x94)
      if (frameData.powerUps && frameData.powerUps.length > 0) {
        const pLen = frameData.powerUps.length;
        const powerUpBaseTiles = [0x80, 0x84, 0x88, 0x8C, 0x90, 0x94];

        for (let i = 0; i < pLen; i++) {
          const p = frameData.powerUps[i];
          if (!p.visible) continue;

          const px = BORDER + Math.round(p.x) * SCALE;
          const py = BORDER + Math.round(p.y) * SCALE;
          const baseTile = powerUpBaseTiles[Math.min(p.type, 5)] || 0x8C;

          // Power-up metasprites are arranged as [TL, TR, BL, BR] = [t, t+2, t+1, t+3]
          window.NesCHR.drawMetasprite(ctx, [baseTile, baseTile + 2, baseTile + 1, baseTile + 3], 6, px, py, false, SCALE);
        }
      }

      // 6. Floating Score Popups (e.g. +500 PTS 0x3A..0x3D)
      if (frameData.scorePopups && frameData.scorePopups.length > 0) {
        const spLen = frameData.scorePopups.length;
        for (let i = 0; i < spLen; i++) {
          const sp = frameData.scorePopups[i];
          const spX = BORDER + Math.round(sp.x) * SCALE;
          const spY = BORDER + Math.round(sp.y) * SCALE;

          // Metasprite 0x3A..0x3D for 500 PTS
          window.NesCHR.drawMetasprite(ctx, [0x3A, 0x3C, 0x3B, 0x3D], 6, spX, spY, false, SCALE);
        }
      }

      // 7. Explosions
      if (frameData.explosions && frameData.explosions.length > 0) {
        const explosionFrames = [0xA0, 0xA2, 0xA4];
        const exLen = frameData.explosions.length;
        for (let i = 0; i < exLen; i++) {
          const ex = frameData.explosions[i];
          const tile = explosionFrames[Math.min(ex.frame, 2)];
          window.NesCHR.drawCHRTile(ctx, tile, 0, BORDER + Math.round(ex.x) * SCALE, BORDER + Math.round(ex.y) * SCALE, true, SCALE);
        }
      }

      // 8. Fast Foreground Trees
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
        ctx.fillStyle = '#e74c3c'; ctx.font = 'bold 22px "Press Start 2P", monospace';
        ctx.textAlign = 'center'; 
        ctx.fillText('PAUSE', BORDER + PLAYFIELD / 2, BORDER + PLAYFIELD / 2 - 10);

        const blink = Math.floor(performance.now() / 400) % 2 === 0;
        if (blink) {
          ctx.fillStyle = '#38bdf8'; ctx.font = 'bold 9px "Press Start 2P", monospace';
          ctx.fillText('PRESS [P] OR ESC TO RESUME', BORDER + PLAYFIELD / 2, BORDER + PLAYFIELD / 2 + 25);
        }
        ctx.textAlign = 'start';
      } else if (frameData.isGameOver || frameData.eagleDestroyed) {
        ctx.fillStyle = 'rgba(0, 0, 0, 0.75)';
        ctx.fillRect(BORDER, BORDER, PLAYFIELD, PLAYFIELD);
        ctx.fillStyle = '#ef4444'; ctx.font = 'bold 22px "Press Start 2P", monospace';
        ctx.textAlign = 'center'; 
        ctx.fillText('GAME OVER', BORDER + PLAYFIELD / 2, BORDER + PLAYFIELD / 2 - 25);

        ctx.fillStyle = '#f1c40f'; ctx.font = 'bold 10px "Press Start 2P", monospace';
        if (frameData.eagleDestroyed) {
          ctx.fillText('EAGLE DESTROYED', BORDER + PLAYFIELD / 2, BORDER + PLAYFIELD / 2 + 5);
        } else {
          ctx.fillText('OUT OF LIVES', BORDER + PLAYFIELD / 2, BORDER + PLAYFIELD / 2 + 5);
        }

        // Blinking Press R or Click Restart prompt
        const blink = Math.floor(performance.now() / 400) % 2 === 0;
        if (blink) {
          ctx.fillStyle = '#38bdf8'; ctx.font = 'bold 10px "Press Start 2P", monospace';
          ctx.fillText('PRESS [R] OR RESTART', BORDER + PLAYFIELD / 2, BORDER + PLAYFIELD / 2 + 45);
        }
        ctx.textAlign = 'start';
      }
    }
  };
})();

