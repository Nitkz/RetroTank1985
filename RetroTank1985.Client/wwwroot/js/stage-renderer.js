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

  function drawSideHud(ctx, frameData, canvasWidth, canvasHeight) {
    // HUD is placed in the right border area (width = 512px, Playfield ends at 448px, Margin = 64px)
    const hudX = BORDER + PLAYFIELD + 14;
    const hudY = BORDER + 6;
    const s8 = 8 * SCALE; // 16px

    // 1. Remaining Enemy Tank Icons (2 columns of 10)
    const enemiesRemaining = frameData.enemiesRemaining ?? 20;

    for (let i = 0; i < 20; i++) {
      const col = i % 2;
      const row = Math.floor(i / 2);
      const ex = hudX + col * (s8 + 2);
      const ey = hudY + row * (s8 + 2);

      if (i < enemiesRemaining) {
        // Draw crisp mini enemy tank icon
        ctx.fillStyle = '#000000';
        ctx.fillRect(ex, ey, s8, s8);
        ctx.fillStyle = '#ffffff';
        // 8x8 px mini tank pixel graphic scaled 2x
        ctx.fillRect(ex + 6, ey + 2, 4, 4); // Turret/barrel
        ctx.fillRect(ex + 4, ey + 6, 8, 6); // Tank body
        ctx.fillRect(ex + 2, ey + 4, 2, 10); // Left tread
        ctx.fillRect(ex + 12, ey + 4, 2, 10); // Right tread
      } else {
        // Destroyed / empty placeholder slot
        ctx.fillStyle = '#555555';
        ctx.fillRect(ex, ey, s8, s8);
        ctx.strokeStyle = '#444444';
        ctx.lineWidth = 1;
        ctx.strokeRect(ex, ey, s8, s8);
      }
    }

    // 2. Player 1 Lives Info
    const p1Y = hudY + 10 * (s8 + 2) + 14;
    ctx.fillStyle = '#000000';
    ctx.font = 'bold 10px "Press Start 2P", monospace';
    ctx.textAlign = 'left';
    ctx.fillText('IP', hudX, p1Y);

    // Player Mini Tank Icon
    const pTankX = hudX;
    const pTankY = p1Y + 4;
    ctx.fillStyle = '#000000';
    ctx.fillRect(pTankX, pTankY, s8, s8);
    ctx.fillStyle = '#f1c40f'; // Yellow player mini tank
    ctx.fillRect(pTankX + 6, pTankY + 2, 4, 4); // Turret
    ctx.fillRect(pTankX + 4, pTankY + 6, 8, 6); // Body
    ctx.fillRect(pTankX + 2, pTankY + 4, 2, 10); // Left tread
    ctx.fillRect(pTankX + 12, pTankY + 4, 2, 10); // Right tread

    // Player Lives Count
    ctx.fillStyle = '#000000';
    ctx.font = 'bold 11px "Press Start 2P", monospace';
    ctx.fillText(`${Math.max(0, frameData.lives ?? 0)}`, hudX + s8 + 4, pTankY + 12);

    // 3. Stage Flag Icon & Stage Number
    const flagY = pTankY + s8 + 18;
    const fx = hudX;
    const fy = flagY;
    ctx.fillStyle = '#000000';
    ctx.fillRect(fx + 2, fy, 2, 18); // Flag pole
    ctx.fillStyle = '#ef4444'; // Red flag banner
    ctx.beginPath();
    ctx.moveTo(fx + 4, fy);
    ctx.lineTo(fx + 16, fy + 5);
    ctx.lineTo(fx + 4, fy + 10);
    ctx.fill();

    // Stage Number
    ctx.fillStyle = '#000000';
    ctx.font = 'bold 11px "Press Start 2P", monospace';
    ctx.fillText(`${frameData.stageNumber ?? 1}`, hudX + s8 + 4, flagY + 14);
  }

  function drawStageCurtain(ctx, frameData) {
    const progress = frameData.curtainProgress ?? 0;
    // Shutter wipe: grey curtains close or open vertically
    const totalHeight = PLAYFIELD;
    const currentClose = Math.round((1.0 - progress) * (totalHeight / 2));

    ctx.fillStyle = '#636363';
    // Top curtain
    ctx.fillRect(BORDER, BORDER, PLAYFIELD, currentClose);
    // Bottom curtain
    ctx.fillRect(BORDER, BORDER + PLAYFIELD - currentClose, PLAYFIELD, currentClose);

    if (progress < 0.95) {
      // Center Stage Banner
      ctx.fillStyle = '#000000';
      ctx.fillRect(BORDER + 40, BORDER + PLAYFIELD / 2 - 25, PLAYFIELD - 80, 50);
      ctx.strokeStyle = '#ef4444';
      ctx.lineWidth = 2;
      ctx.strokeRect(BORDER + 40, BORDER + PLAYFIELD / 2 - 25, PLAYFIELD - 80, 50);

      ctx.fillStyle = '#ffffff';
      ctx.font = 'bold 14px "Press Start 2P", monospace';
      ctx.textAlign = 'center';
      ctx.fillText(`STAGE ${String(frameData.stageNumber || 1).padStart(2, '0')}`, BORDER + PLAYFIELD / 2, BORDER + PLAYFIELD / 2 + 6);
      ctx.textAlign = 'left';
    }
  }

  function drawScoreTallyScreen(ctx, frameData) {
    // Fill entire playfield with NES Black
    ctx.fillStyle = '#000000';
    ctx.fillRect(BORDER, BORDER, PLAYFIELD, PLAYFIELD);

    ctx.font = 'bold 10px "Press Start 2P", monospace';

    // Header: HI-SCORE & STAGE
    ctx.fillStyle = '#ef4444';
    ctx.fillText('HI-SCORE', BORDER + 30, BORDER + 30);
    ctx.fillStyle = '#f59e0b';
    ctx.fillText('20000', BORDER + 150, BORDER + 30);

    ctx.fillStyle = '#ffffff';
    ctx.textAlign = 'center';
    ctx.fillText(`STAGE ${String(frameData.stageNumber || 1).padStart(2, '0')}`, BORDER + PLAYFIELD / 2, BORDER + 55);

    // Player 1 Header
    ctx.fillStyle = '#ef4444';
    ctx.textAlign = 'left';
    ctx.fillText('I-PLAYER', BORDER + 30, BORDER + 80);
    ctx.fillStyle = '#f59e0b';
    ctx.fillText(`${frameData.score || 0}`, BORDER + 30, BORDER + 98);

    // Tank Rows Breakdown (Basic, Fast, Power, Armor)
    const tankRows = [
      { type: 0, pts: 100, count: frameData.tallyCountBasic ?? 0, kills: frameData.killsBasic ?? 0, baseTile: 0x80, pal: 6 },
      { type: 1, pts: 200, count: frameData.tallyCountFast ?? 0, kills: frameData.killsFast ?? 0, baseTile: 0xA0, pal: 6 },
      { type: 2, pts: 300, count: frameData.tallyCountPower ?? 0, kills: frameData.killsPower ?? 0, baseTile: 0xC0, pal: 6 },
      { type: 3, pts: 400, count: frameData.tallyCountArmor ?? 0, kills: frameData.killsArmor ?? 0, baseTile: 0xE0, pal: 5 }
    ];

    let startY = BORDER + 125;
    const rowGap = 34;

    for (let i = 0; i < 4; i++) {
      const row = tankRows[i];
      const ry = startY + i * rowGap;

      // PTS calculation
      const rowPts = row.count * row.pts;
      ctx.fillStyle = '#ffffff';
      ctx.textAlign = 'right';
      ctx.fillText(`${rowPts}`, BORDER + 90, ry + 12);
      ctx.fillText('PTS', BORDER + 130, ry + 12);

      // Arrow indicator & Count
      ctx.fillText(`${row.count}`, BORDER + 180, ry + 12);
      ctx.fillText('◀', BORDER + 200, ry + 12);

      // Tank Sprite Metasprite Icon
      window.NesCHR.drawTankMetasprite(ctx, row.baseTile, row.pal, BORDER + 215, ry, true, SCALE);
    }

    // Divider Line
    ctx.strokeStyle = '#ffffff';
    ctx.lineWidth = 2;
    ctx.beginPath();
    ctx.moveTo(BORDER + 130, startY + 4 * rowGap - 5);
    ctx.lineTo(BORDER + 270, startY + 4 * rowGap - 5);
    ctx.stroke();

    // Total Summary
    const totalCount = (frameData.tallyCountBasic || 0) + (frameData.tallyCountFast || 0) + (frameData.tallyCountPower || 0) + (frameData.tallyCountArmor || 0);
    ctx.fillStyle = '#ffffff';
    ctx.textAlign = 'left';
    ctx.fillText('TOTAL', BORDER + 70, startY + 4 * rowGap + 18);
    ctx.textAlign = 'right';
    ctx.fillText(`${totalCount}`, BORDER + 180, startY + 4 * rowGap + 18);

    if (frameData.tallyStep >= 5) {
      const blink = Math.floor(performance.now() / 400) % 2 === 0;
      if (blink) {
        ctx.fillStyle = '#38bdf8';
        ctx.font = 'bold 9px "Press Start 2P", monospace';
        ctx.textAlign = 'center';
        ctx.fillText('PRESS FIRE / SPACE TO CONTINUE', BORDER + PLAYFIELD / 2, BORDER + PLAYFIELD - 15);
      }
    }
    ctx.textAlign = 'left';
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

      // 1. Outer border & Side HUD
      ctx.fillStyle = '#636363';
      ctx.fillRect(0, 0, canvas.width, canvas.height);

      // Draw Side NES HUD
      drawSideHud(ctx, frameData, canvas.width, canvas.height);

      // 2. Playfield Background
      if (frameData.subTiles) updateSubTileBuffer(frameData.subTiles);
      if (subTileBufferCanvas) ctx.drawImage(subTileBufferCanvas, BORDER, BORDER);
      else { ctx.fillStyle = '#000000'; ctx.fillRect(BORDER, BORDER, PLAYFIELD, PLAYFIELD); }

      // 3. Bullets
      if (frameData.bullets && frameData.bullets.length > 0) {
        ctx.fillStyle = '#ffffff';
        const bLen = frameData.bullets.length;
        const bSize = 3 * SCALE;
        for (let i = 0; i < bLen; i++) {
          const b = frameData.bullets[i];
          ctx.fillRect(BORDER + b.x * SCALE, BORDER + b.y * SCALE, bSize, bSize);
        }
      }

      // 4. Enemy Tanks & Spawning Stars
      if (frameData.enemies && frameData.enemies.length > 0) {
        const SPAWN_STARS = [0xAD, 0xA9, 0xA5, 0xA1];
        const eLen = frameData.enemies.length;
        const nowFrame = Math.floor(performance.now() / 130);

        for (let i = 0; i < eLen; i++) {
          const e = frameData.enemies[i];
          const ex = BORDER + Math.round(e.x) * SCALE;
          const ey = BORDER + Math.round(e.y) * SCALE;

          if (e.isSpawning) {
            // Authentic 15-step triangle wave spawning star (ROM $E0BF):
            // Giant ($AC) -> Large ($A8) -> Medium ($A4) -> Small ($A0) -> Medium ($A4) -> Large ($A8) -> Giant ($AC)
            const SPAWN_SEQ = [0xAC, 0xAC, 0xA8, 0xA8, 0xA4, 0xA4, 0xA0, 0xA0, 0xA0, 0xA4, 0xA4, 0xA8, 0xA8, 0xAC, 0xAC];
            const seqIdx = Math.min(14, Math.floor((30 - e.spawnTimer) / 2));
            const starBase = SPAWN_SEQ[seqIdx] || 0xAC;
            window.NesCHR.drawMetasprite(ctx, [starBase, starBase + 2, starBase + 1, starBase + 3], 7, ex, ey, false, SCALE);
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

      // 5. Player Tank (Upgrades visual sprite based on StarPower 0=0x00, 1=0x20, 2=0x40, 3=0x60)
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

      // 6. Droppable Power-Up Items (0:Helmet=0x80, 1:Timer=0x84, 2:Shovel=0x88, 3:Star=0x8C, 4:Grenade=0x90, 5:TankLife=0x94)
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

      // 7. Floating Score Popups (e.g. +500 PTS 0x3A..0x3D)
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

      // 8. Explosions (Small Bullet Impact = 3 frames 16x16, Big Tank/Eagle Explosion = 5 phases up to 32x32)
      if (frameData.explosions && frameData.explosions.length > 0) {
        const exLen = frameData.explosions.length;
        for (let i = 0; i < exLen; i++) {
          const ex = frameData.explosions[i];
          const exX = BORDER + Math.round(ex.x) * SCALE;
          const exY = BORDER + Math.round(ex.y) * SCALE;

          if (ex.big) {
            // Authentic Big Tank/Eagle Explosion (5 phases from PT0 with SP3 / palIdx 7):
            // Phase 0 (Small 16x16): [0xF0, 0xF2, 0xF1, 0xF3]
            // Phase 1 (Medium 16x16): [0xF4, 0xF6, 0xF5, 0xF7]
            // Phase 2 (Large 16x16): [0xF8, 0xFA, 0xF9, 0xFB]
            // Phase 3 (Giant 32x32 mushroom wave): 16 tiles Base 0xD0
            // Phase 4 (Giant 32x32 smoke plume): 16 tiles Base 0xE0
            if (ex.frame === 0) {
              window.NesCHR.drawMetasprite(ctx, [0xF0, 0xF2, 0xF1, 0xF3], 7, exX, exY, false, SCALE);
            } else if (ex.frame === 1) {
              window.NesCHR.drawMetasprite(ctx, [0xF4, 0xF6, 0xF5, 0xF7], 7, exX, exY, false, SCALE);
            } else if (ex.frame === 2) {
              window.NesCHR.drawMetasprite(ctx, [0xF8, 0xFA, 0xF9, 0xFB], 7, exX, exY, false, SCALE);
            } else if (ex.frame === 3) {
              window.NesCHR.drawExpandSprite(ctx, 0xD0, 7, exX - 8 * SCALE, exY - 8 * SCALE, SCALE);
            } else {
              window.NesCHR.drawExpandSprite(ctx, 0xE0, 7, exX - 8 * SCALE, exY - 8 * SCALE, SCALE);
            }
          } else {
            // Small Bullet Impact Explosion (3 frames 16x16 centered at impact point)
            const cx = exX - 8 * SCALE;
            const cy = exY - 8 * SCALE;
            if (ex.frame === 0) {
              window.NesCHR.drawMetasprite(ctx, [0xF0, 0xF2, 0xF1, 0xF3], 7, cx, cy, false, SCALE);
            } else if (ex.frame === 1) {
              window.NesCHR.drawMetasprite(ctx, [0xF4, 0xF6, 0xF5, 0xF7], 7, cx, cy, false, SCALE);
            } else {
              window.NesCHR.drawMetasprite(ctx, [0xF8, 0xFA, 0xF9, 0xFB], 7, cx, cy, false, SCALE);
            }
          }
        }
      }

      // 9. Fast Foreground Trees
      const treeCount = treeSubTiles.length;
      if (treeCount > 0) {
        for (let i = 0; i < treeCount; i++) {
          const t = treeSubTiles[i];
          window.NesCHR.drawCHRTile(ctx, 0x22, 2, BORDER + t.dx, BORDER + t.dy, true, SCALE);
        }
      }

      // 10. Flow Overlays (Stage Curtain, Score Tally, Pause, Game Over)
      if (frameData.gameState === 1) { // StageCurtain
        drawStageCurtain(ctx, frameData);
      } else if (frameData.gameState === 4) { // StageTally
        drawScoreTallyScreen(ctx, frameData);
      } else if (frameData.isPaused) {
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
      } else if (frameData.isGameOver || frameData.gameState === 3) {
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

