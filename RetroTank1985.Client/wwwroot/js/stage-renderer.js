/**
 * stage-renderer.js — Lean Fast In-Game 60 FPS Canvas Blitter
 * Consumes C# RenderFrameDto snapshot and blits via NesCHR with 0 heap garbage.
 * Fully supports 1-Player and 2-Player Co-Op (SP1 Green Tank), Dual HUD & 2P Score Tally!
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

    // 2. Player 1 Lives Info (IP)
    const p1Y = hudY + 10 * (s8 + 2) + 12;
    ctx.fillStyle = '#000000';
    ctx.font = 'bold 9px "Press Start 2P", monospace';
    ctx.textAlign = 'left';
    ctx.fillText('IP', hudX, p1Y);

    const pTankX = hudX;
    const pTankY = p1Y + 5;
    ctx.fillStyle = '#000000';
    ctx.fillRect(pTankX, pTankY, s8, s8);
    ctx.fillStyle = '#f1c40f'; // Yellow player mini tank
    ctx.fillRect(pTankX + 6, pTankY + 2, 4, 4);
    ctx.fillRect(pTankX + 4, pTankY + 6, 8, 6);
    ctx.fillRect(pTankX + 2, pTankY + 4, 2, 10);
    ctx.fillRect(pTankX + 12, pTankY + 4, 2, 10);

    ctx.fillStyle = '#000000';
    ctx.font = 'bold 10px "Press Start 2P", monospace';
    ctx.fillText(`${Math.max(0, frameData.lives ?? 0)}`, hudX + s8 + 4, pTankY + 13);

    // 3. Player 2 Lives Info (IIP) if in 2-Player Co-Op mode
    let nextY = pTankY + s8 + 14;
    if (frameData.isTwoPlayer) {
      const p2Y = nextY;
      ctx.fillStyle = '#000000';
      ctx.font = 'bold 9px "Press Start 2P", monospace';
      ctx.fillText('IIP', hudX, p2Y);

      const p2TankX = hudX;
      const p2TankY = p2Y + 5;
      ctx.fillStyle = '#000000';
      ctx.fillRect(p2TankX, p2TankY, s8, s8);
      ctx.fillStyle = '#2ecc71'; // Green player 2 mini tank
      ctx.fillRect(p2TankX + 6, p2TankY + 2, 4, 4);
      ctx.fillRect(p2TankX + 4, p2TankY + 6, 8, 6);
      ctx.fillRect(p2TankX + 2, p2TankY + 4, 2, 10);
      ctx.fillRect(p2TankX + 12, p2TankY + 4, 2, 10);

      ctx.fillStyle = '#000000';
      ctx.font = 'bold 10px "Press Start 2P", monospace';
      ctx.fillText(`${Math.max(0, frameData.p2Lives ?? 0)}`, hudX + s8 + 4, p2TankY + 13);

      nextY = p2TankY + s8 + 14;
    }

    // 4. Stage Flag Icon & Stage Number
    const flagY = nextY;
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

    ctx.fillStyle = '#000000';
    ctx.font = 'bold 10px "Press Start 2P", monospace';
    ctx.fillText(`${frameData.stageNumber ?? 1}`, hudX + s8 + 4, flagY + 14);
  }

  function drawStageCurtain(ctx, frameData) {
    const progress = frameData.curtainProgress ?? 0;
    const totalHeight = PLAYFIELD;
    const currentClose = Math.round((1.0 - progress) * (totalHeight / 2));

    ctx.fillStyle = '#636363';
    ctx.fillRect(BORDER, BORDER, PLAYFIELD, currentClose);
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
    ctx.fillStyle = '#000000';
    ctx.fillRect(BORDER, BORDER, PLAYFIELD, PLAYFIELD);

    ctx.font = 'bold 10px "Press Start 2P", monospace';

    // Header: HI-SCORE & STAGE
    ctx.fillStyle = '#ef4444';
    ctx.fillText('HI-SCORE', BORDER + 30, BORDER + 30);
    ctx.fillStyle = '#f59e0b';
    ctx.fillText(`${frameData.highScore || 20000}`, BORDER + 150, BORDER + 30);

    ctx.fillStyle = '#ffffff';
    ctx.textAlign = 'center';
    ctx.fillText(`STAGE ${String(frameData.stageNumber || 1).padStart(2, '0')}`, BORDER + PLAYFIELD / 2, BORDER + 55);

    const is2P = frameData.isTwoPlayer;

    // Tank Rows Definition (Basic, Fast, Power, Armor)
    const tankRows = [
      { 
        type: 0, 
        pts: 100, 
        countP1: frameData.tallyCountBasic ?? 0, 
        countP2: frameData.tallyCountBasicP2 ?? 0, 
        baseTile: 0x80, 
        pal: 6 
      },
      { 
        type: 1, 
        pts: 200, 
        countP1: frameData.tallyCountFast ?? 0, 
        countP2: frameData.tallyCountFastP2 ?? 0, 
        baseTile: 0xA0, 
        pal: 6 
      },
      { 
        type: 2, 
        pts: 300, 
        countP1: frameData.tallyCountPower ?? 0, 
        countP2: frameData.tallyCountPowerP2 ?? 0, 
        baseTile: 0xC0, 
        pal: 6 
      },
      { 
        type: 3, 
        pts: 400, 
        countP1: frameData.tallyCountArmor ?? 0, 
        countP2: frameData.tallyCountArmorP2 ?? 0, 
        baseTile: 0xE0, 
        pal: 5 
      }
    ];

    let startY = BORDER + 125;
    const rowGap = 34;

    if (!is2P) {
      // 1-Player Tally Layout
      ctx.fillStyle = '#ef4444';
      ctx.textAlign = 'left';
      ctx.fillText('I-PLAYER', BORDER + 30, BORDER + 80);
      ctx.fillStyle = '#f59e0b';
      ctx.fillText(`${frameData.score || 0}`, BORDER + 30, BORDER + 98);

      for (let i = 0; i < 4; i++) {
        const row = tankRows[i];
        const ry = startY + i * rowGap;

        const rowPts = row.countP1 * row.pts;
        ctx.fillStyle = '#ffffff';
        ctx.textAlign = 'right';
        ctx.fillText(`${rowPts}`, BORDER + 90, ry + 12);
        ctx.fillText('PTS', BORDER + 130, ry + 12);
        ctx.fillText(`${row.countP1}`, BORDER + 180, ry + 12);
        ctx.fillText('◀', BORDER + 200, ry + 12);

        window.NesCHR.drawTankMetasprite(ctx, row.baseTile, row.pal, BORDER + 215, ry, true, SCALE);
      }

      ctx.strokeStyle = '#ffffff';
      ctx.lineWidth = 2;
      ctx.beginPath();
      ctx.moveTo(BORDER + 130, startY + 4 * rowGap - 5);
      ctx.lineTo(BORDER + 270, startY + 4 * rowGap - 5);
      ctx.stroke();

      const totalCountP1 = (frameData.tallyCountBasic || 0) + (frameData.tallyCountFast || 0) + (frameData.tallyCountPower || 0) + (frameData.tallyCountArmor || 0);
      ctx.fillStyle = '#ffffff';
      ctx.textAlign = 'left';
      ctx.fillText('TOTAL', BORDER + 70, startY + 4 * rowGap + 18);
      ctx.textAlign = 'right';
      ctx.fillText(`${totalCountP1}`, BORDER + 180, startY + 4 * rowGap + 18);
    } else {
      // Authentic 2-Player Dual Column Tally Layout!
      // I-PLAYER (Left)
      ctx.fillStyle = '#ef4444';
      ctx.textAlign = 'left';
      ctx.fillText('I-PLAYER', BORDER + 20, BORDER + 80);
      ctx.fillStyle = '#f59e0b';
      ctx.fillText(`${frameData.score || 0}`, BORDER + 20, BORDER + 98);

      // II-PLAYER (Right)
      ctx.fillStyle = '#2ecc71';
      ctx.textAlign = 'right';
      ctx.fillText('II-PLAYER', BORDER + PLAYFIELD - 20, BORDER + 80);
      ctx.fillStyle = '#f59e0b';
      ctx.fillText(`${frameData.p2Score || 0}`, BORDER + PLAYFIELD - 20, BORDER + 98);

      for (let i = 0; i < 4; i++) {
        const row = tankRows[i];
        const ry = startY + i * rowGap;

        // P1 Stats (Left)
        const rowPtsP1 = row.countP1 * row.pts;
        ctx.fillStyle = '#ffffff';
        ctx.textAlign = 'right';
        ctx.fillText(`${rowPtsP1}`, BORDER + 75, ry + 12);
        ctx.fillText('PTS', BORDER + 110, ry + 12);
        ctx.fillText(`${row.countP1}`, BORDER + 145, ry + 12);
        ctx.fillText('◀', BORDER + 165, ry + 12);

        // Center Tank Metasprite Icon
        const centerTankX = BORDER + PLAYFIELD / 2 - 16;
        window.NesCHR.drawTankMetasprite(ctx, row.baseTile, row.pal, centerTankX, ry, true, SCALE);

        // P2 Stats (Right)
        const rowPtsP2 = row.countP2 * row.pts;
        ctx.fillStyle = '#ffffff';
        ctx.textAlign = 'left';
        ctx.fillText('▶', BORDER + 235, ry + 12);
        ctx.fillText(`${row.countP2}`, BORDER + 255, ry + 12);
        ctx.textAlign = 'right';
        ctx.fillText(`${rowPtsP2}`, BORDER + 345, ry + 12);
        ctx.fillText('PTS', BORDER + 380, ry + 12);
      }

      // Divider Lines for both sides
      ctx.strokeStyle = '#ffffff';
      ctx.lineWidth = 2;
      ctx.beginPath();
      ctx.moveTo(BORDER + 40, startY + 4 * rowGap - 5);
      ctx.lineTo(BORDER + 175, startY + 4 * rowGap - 5);
      ctx.moveTo(BORDER + 240, startY + 4 * rowGap - 5);
      ctx.lineTo(BORDER + 375, startY + 4 * rowGap - 5);
      ctx.stroke();

      // Total Summaries
      const totalP1 = (frameData.tallyCountBasic || 0) + (frameData.tallyCountFast || 0) + (frameData.tallyCountPower || 0) + (frameData.tallyCountArmor || 0);
      const totalP2 = (frameData.tallyCountBasicP2 || 0) + (frameData.tallyCountFastP2 || 0) + (frameData.tallyCountPowerP2 || 0) + (frameData.tallyCountArmorP2 || 0);

      ctx.fillStyle = '#ffffff';
      ctx.textAlign = 'left';
      ctx.fillText('TOTAL', BORDER + 40, startY + 4 * rowGap + 18);
      ctx.textAlign = 'right';
      ctx.fillText(`${totalP1}`, BORDER + 145, startY + 4 * rowGap + 18);

      ctx.textAlign = 'left';
      ctx.fillText('TOTAL', BORDER + 255, startY + 4 * rowGap + 18);
      ctx.textAlign = 'right';
      ctx.fillText(`${totalP2}`, BORDER + 360, startY + 4 * rowGap + 18);
    }

    // Step 5+: Announce Winner & Show Advance Prompt
    if (frameData.tallyStep >= 5) {
      const bannerY = BORDER + startY + 4 * rowGap + 42;

      ctx.font = 'bold 11px "Press Start 2P", monospace';
      ctx.textAlign = 'center';

      if (is2P) {
        const p1Score = frameData.score || 0;
        const p2Score = frameData.p2Score || 0;

        if (p1Score > p2Score) {
          ctx.fillStyle = '#f1c40f'; // Yellow
          ctx.fillText('👑 I-PLAYER WINS! 👑', BORDER + PLAYFIELD / 2, bannerY);
        } else if (p2Score > p1Score) {
          ctx.fillStyle = '#2ecc71'; // Green
          ctx.fillText('👑 II-PLAYER WINS! 👑', BORDER + PLAYFIELD / 2, bannerY);
        } else {
          ctx.fillStyle = '#38bdf8'; // Cyan
          ctx.fillText('🤝 CO-OP DRAW / VICTORY! 🤝', BORDER + PLAYFIELD / 2, bannerY);
        }
      } else {
        ctx.fillStyle = '#f1c40f';
        ctx.fillText('⭐ STAGE CLEARED! ⭐', BORDER + PLAYFIELD / 2, bannerY);
      }

      // Blinking prompt to skip or let auto-delay advance
      const blink = Math.floor(performance.now() / 400) % 2 === 0;
      if (blink) {
        ctx.fillStyle = '#38bdf8';
        ctx.font = 'bold 9px "Press Start 2P", monospace';
        ctx.fillText('PRESS FIRE / SPACE TO ADVANCE', BORDER + PLAYFIELD / 2, BORDER + PLAYFIELD - 12);
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
        const eLen = frameData.enemies.length;
        const nowFrame = Math.floor(performance.now() / 130);

        for (let i = 0; i < eLen; i++) {
          const e = frameData.enemies[i];
          const ex = BORDER + Math.round(e.x) * SCALE;
          const ey = BORDER + Math.round(e.y) * SCALE;

          if (e.isSpawning) {
            const SPAWN_SEQ = [0xAC, 0xAC, 0xA8, 0xA8, 0xA4, 0xA4, 0xA0, 0xA0, 0xA0, 0xA4, 0xA4, 0xA8, 0xA8, 0xAC, 0xAC];
            const seqIdx = Math.min(14, Math.floor((30 - e.spawnTimer) / 2));
            const starBase = SPAWN_SEQ[seqIdx] || 0xAC;
            window.NesCHR.drawMetasprite(ctx, [starBase, starBase + 2, starBase + 1, starBase + 3], 7, ex, ey, false, SCALE);
          } else {
            let base = 0x80;
            let pal = 6; // Default Grey

            if (e.type === 0) { base = 0x80; pal = 6; }
            else if (e.type === 1) { base = 0xA0; pal = 6; }
            else if (e.type === 2) { base = 0xC0; pal = 6; }
            else if (e.type === 3) {
              base = 0xE0;
              if (e.hp >= 4) pal = 5;
              else if (e.hp === 3) pal = 4;
              else if (e.hp === 2) pal = 7;
              else pal = 6;
            }

            if (e.isFlashing && (nowFrame % 2 === 0)) {
              pal = 7;
            }

            const T = base + e.dir * 8 + (e.anim % 2) * 4;
            window.NesCHR.drawTankMetasprite(ctx, T, pal, ex, ey, true, SCALE);
          }
        }
      }

      // Helper function to render tech armor ring around player tank
      function drawPlayerArmorRing(tankX, tankY, hp, maxHp) {
        if (!hp || hp <= 1) return;
        ctx.save();
        const centerX = tankX + (8 * SCALE);
        const centerY = tankY + (8 * SCALE);
        const radius = (10 * SCALE);

        let mainColor = '#10b981';
        let segCount = 4;

        if (hp === 2) {
          mainColor = '#f59e0b'; // Amber Gold
          segCount = 3;
        } else if (hp === 3) {
          mainColor = '#10b981'; // Emerald Green
          segCount = 4;
        } else if (hp >= 4) {
          mainColor = '#06b6d4'; // Cyan Mega Armor
          segCount = 6;
        }

        ctx.strokeStyle = mainColor;
        ctx.lineWidth = 2 * SCALE;
        ctx.shadowColor = mainColor;
        ctx.shadowBlur = 6 * SCALE;

        // Draw segmented energy shield bracket arc
        for (let seg = 0; seg < segCount; seg++) {
          const startAngle = (seg * (Math.PI * 2 / segCount)) + 0.25;
          const endAngle = startAngle + (Math.PI * 2 / segCount) - 0.5;
          ctx.beginPath();
          ctx.arc(centerX, centerY, radius, startAngle, endAngle);
          ctx.stroke();
        }

        // Tech Pip corner nodes
        ctx.fillStyle = mainColor;
        for (let seg = 0; seg < segCount; seg++) {
          const angle = (seg * (Math.PI * 2 / segCount)) + 0.25;
          const px = centerX + Math.cos(angle) * radius;
          const py = centerY + Math.sin(angle) * radius;
          ctx.beginPath();
          ctx.arc(px, py, 2, 0, Math.PI * 2);
          ctx.fill();
        }
        ctx.restore();
      }

      const nowFlicker = Math.floor(performance.now() / 80) % 2 === 0;

      // 5. Player 1 Tank (SP0 Yellow palette 4)
      if (frameData.pActive) {
        const px = BORDER + Math.round(frameData.pX) * SCALE;
        const py = BORDER + Math.round(frameData.pY) * SCALE;

        // Draw Armor Ring if HP >= 2
        drawPlayerArmorRing(px, py, frameData.pHp, frameData.pMaxHp);

        // Invulnerability flicker after taking armor hit
        const shouldDrawP1 = !frameData.pInvuln || nowFlicker;
        if (shouldDrawP1) {
          const starTier = Math.min(3, Math.max(0, frameData.pStarPower || 0));
          const tierBase = starTier * 0x20;
          const T = tierBase + frameData.pDir * 8 + (frameData.pAnim % 2) * 4;
          window.NesCHR.drawTankMetasprite(ctx, T, 4, px, py, true, SCALE);
        }

        if (frameData.pShield) {
          const sBase = frameData.pShieldFrame === 0 ? 0x28 : 0x2C;
          window.NesCHR.drawTankMetasprite(ctx, sBase, 6, px, py, false, SCALE);
        }
      }

      // 5b. Player 2 Tank (SP1 Green palette 5)
      if (frameData.isTwoPlayer && frameData.p2Active) {
        const p2x = BORDER + Math.round(frameData.p2X) * SCALE;
        const p2y = BORDER + Math.round(frameData.p2Y) * SCALE;

        // Draw Armor Ring if HP >= 2
        drawPlayerArmorRing(p2x, p2y, frameData.p2Hp, frameData.p2MaxHp);

        const shouldDrawP2 = !frameData.p2Invuln || nowFlicker;
        if (shouldDrawP2) {
          const starTier2 = Math.min(3, Math.max(0, frameData.p2StarPower || 0));
          const tierBase2 = starTier2 * 0x20;
          const T2 = tierBase2 + frameData.p2Dir * 8 + (frameData.p2Anim % 2) * 4;
          window.NesCHR.drawTankMetasprite(ctx, T2, 5, p2x, p2y, true, SCALE);
        }

        if (frameData.p2Shield) {
          const sBase2 = frameData.p2ShieldFrame === 0 ? 0x28 : 0x2C;
          window.NesCHR.drawTankMetasprite(ctx, sBase2, 6, p2x, p2y, false, SCALE);
        }
      }

      // 6. Droppable Power-Up Items
      if (frameData.powerUps && frameData.powerUps.length > 0) {
        const pLen = frameData.powerUps.length;
        const powerUpBaseTiles = [0x80, 0x84, 0x88, 0x8C, 0x90, 0x94];

        for (let i = 0; i < pLen; i++) {
          const p = frameData.powerUps[i];
          if (!p.visible) continue;

          const px = BORDER + Math.round(p.x) * SCALE;
          const py = BORDER + Math.round(p.y) * SCALE;
          const baseTile = powerUpBaseTiles[Math.min(p.type, 5)] || 0x8C;

          window.NesCHR.drawMetasprite(ctx, [baseTile, baseTile + 2, baseTile + 1, baseTile + 3], 6, px, py, false, SCALE);
        }
      }

      // 7. Floating Score Popups
      if (frameData.scorePopups && frameData.scorePopups.length > 0) {
        const spLen = frameData.scorePopups.length;
        for (let i = 0; i < spLen; i++) {
          const sp = frameData.scorePopups[i];
          const spX = BORDER + Math.round(sp.x) * SCALE;
          const spY = BORDER + Math.round(sp.y) * SCALE;

          window.NesCHR.drawMetasprite(ctx, [0x3A, 0x3C, 0x3B, 0x3D], 6, spX, spY, false, SCALE);
        }
      }

      // 8. Explosions
      if (frameData.explosions && frameData.explosions.length > 0) {
        const exLen = frameData.explosions.length;
        for (let i = 0; i < exLen; i++) {
          const ex = frameData.explosions[i];
          const exX = BORDER + Math.round(ex.x) * SCALE;
          const exY = BORDER + Math.round(ex.y) * SCALE;

          if (ex.big) {
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

      // 10. Flow Overlays
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
