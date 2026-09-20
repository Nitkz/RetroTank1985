/**
 * game-engine.js — 60 FPS NES Game Loop & Player Tank Controller
 * Authentic NES Battle City (1985) physics, grid snapping & rendering engine.
 */

window.GameEngine = (function () {
  const NES_PLAYFIELD_SIZE = 208; // 13 cells * 16px
  const SCALE = 2;                // 208 NES px -> 416 Canvas px
  const BORDER = 16 * SCALE;      // 32px Canvas border
  const TANK_SIZE = 16;           // 16x16 NES pixels
  const HALF_TANK = 8;
  const TANK_SPEED = 1.25;        // NES speed in px/frame (~75 px/sec at 60fps)
  const BULLET_SPEED = 3.0;       // NES bullet speed in px/frame

  // Directions: 0=UP, 1=LEFT, 2=DOWN, 3=RIGHT
  const DIR = {
    UP: 0,
    LEFT: 1,
    DOWN: 2,
    RIGHT: 3
  };

  // State
  let canvas = null;
  let ctx = null;
  let animFrameId = null;
  let isRunning = false;
  let isPaused = false;

  let currentStageGrid = null; // 13x13 grid array
  let currentStageNumber = 1;

  // Collision map: 26x26 grid of 8x8 sub-tiles
  // 0: pass, 1: solid brick/steel, 2: water, 3: eagle
  let subTileGrid = Array(26).fill(null).map(() => Array(26).fill(0));

  // Input states
  const keys = {
    up: false,
    down: false,
    left: false,
    right: false,
    fire: false
  };

  // Player Tank State (NES coordinates 0..208)
  const player = {
    x: 4 * 16,        // Col 4 (64 NES px)
    y: 12 * 16,       // Row 12 (192 NES px)
    dir: DIR.UP,
    moving: false,
    animFrame: 0,
    animCounter: 0,
    shield: true,
    shieldTimer: 180, // ~3 seconds of initial invincibility shield
    shieldFrame: 0,
    active: true
  };

  // Bullets
  const bullets = []; // { x, y, dir, active }

  // Audio engine state tracking
  let engineSoundPlaying = false;

  // Telemetry callback to Blazor
  let telemetryCallback = null;
  let lastFpsCalc = performance.now();
  let frameCount = 0;
  let currentFps = 60;

  // Key listeners
  function onKeyDown(e) {
    if (['ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight', 'Space'].includes(e.code)) {
      e.preventDefault();
    }
    switch (e.code) {
      case 'KeyW':
      case 'ArrowUp':
        keys.up = true;
        break;
      case 'KeyS':
      case 'ArrowDown':
        keys.down = true;
        break;
      case 'KeyA':
      case 'ArrowLeft':
        keys.left = true;
        break;
      case 'KeyD':
      case 'ArrowRight':
        keys.right = true;
        break;
      case 'Space':
      case 'KeyJ':
        if (!keys.fire) {
          keys.fire = true;
          firePlayerBullet();
        }
        break;
      case 'KeyP':
      case 'Escape':
        togglePause();
        break;
    }
  }

  function onKeyUp(e) {
    switch (e.code) {
      case 'KeyW':
      case 'ArrowUp':
        keys.up = false;
        break;
      case 'KeyS':
      case 'ArrowDown':
        keys.down = false;
        break;
      case 'KeyA':
      case 'ArrowLeft':
        keys.left = false;
        break;
      case 'KeyD':
      case 'ArrowRight':
        keys.right = false;
        break;
      case 'Space':
      case 'KeyJ':
        keys.fire = false;
        break;
    }
  }

  // Build 26x26 8x8 sub-tile collision map from 13x13 stage grid
  function buildCollisionMap(grid) {
    subTileGrid = Array(26).fill(null).map(() => Array(26).fill(0));
    if (!grid) return;

    for (let r = 0; r < 13; r++) {
      for (let c = 0; c < 13; c++) {
        const tileType = grid[r] ? grid[r][c] : 13;
        const subR = r * 2;
        const subC = c * 2;

        if (tileType === 4 || tileType === 9) {
          // Full Brick or Full Steel
          subTileGrid[subR][subC] = 1;
          subTileGrid[subR][subC + 1] = 1;
          subTileGrid[subR + 1][subC] = 1;
          subTileGrid[subR + 1][subC + 1] = 1;
        } else if (tileType === 0) {
          // Brick right col
          subTileGrid[subR][subC + 1] = 1;
          subTileGrid[subR + 1][subC + 1] = 1;
        } else if (tileType === 1) {
          // Brick bottom row
          subTileGrid[subR + 1][subC] = 1;
          subTileGrid[subR + 1][subC + 1] = 1;
        } else if (tileType === 2) {
          // Brick left col
          subTileGrid[subR][subC] = 1;
          subTileGrid[subR + 1][subC] = 1;
        } else if (tileType === 3) {
          // Brick top row
          subTileGrid[subR][subC] = 1;
          subTileGrid[subR][subC + 1] = 1;
        } else if (tileType === 5) {
          // Steel right col
          subTileGrid[subR][subC + 1] = 1;
          subTileGrid[subR + 1][subC + 1] = 1;
        } else if (tileType === 6) {
          // Steel bottom row
          subTileGrid[subR + 1][subC] = 1;
          subTileGrid[subR + 1][subC + 1] = 1;
        } else if (tileType === 7) {
          // Steel left col
          subTileGrid[subR][subC] = 1;
          subTileGrid[subR + 1][subC] = 1;
        } else if (tileType === 8) {
          // Steel top row
          subTileGrid[subR][subC] = 1;
          subTileGrid[subR][subC + 1] = 1;
        } else if (tileType === 10) {
          // Water (impassable for tank)
          subTileGrid[subR][subC] = 2;
          subTileGrid[subR][subC + 1] = 2;
          subTileGrid[subR + 1][subC] = 2;
          subTileGrid[subR + 1][subC + 1] = 2;
        }
      }
    }

    // Eagle Wall fortification in sub-tiles (ROM EAGLE_WALL)
    // Eagle base is at cell (12, 6) -> sub-cells (24, 12), (24, 13), (25, 12), (25, 13)
    subTileGrid[24][12] = 3;
    subTileGrid[24][13] = 3;
    subTileGrid[25][12] = 3;
    subTileGrid[25][13] = 3;

    // Π-shaped wall
    const eagleBricks = [
      { r: 23, c: 11 }, { r: 23, c: 12 }, { r: 23, c: 13 }, { r: 23, c: 14 },
      { r: 24, c: 11 }, { r: 25, c: 11 },
      { r: 24, c: 14 }, { r: 25, c: 14 }
    ];
    eagleBricks.forEach(b => {
      subTileGrid[b.r][b.c] = 1;
    });
  }

  // Famicom Grid Alignment & Tank Collision Check
  function canMoveTo(x, y) {
    // 1. Check Playfield Boundaries (0..208 - 16 = 192)
    if (x < 0 || x > NES_PLAYFIELD_SIZE - TANK_SIZE) return false;
    if (y < 0 || y > NES_PLAYFIELD_SIZE - TANK_SIZE) return false;

    // 2. Check 8x8 sub-tiles covered by 16x16 tank
    // A tank overlaps a range of sub-tiles
    const minSubC = Math.floor(x / 8);
    const maxSubC = Math.floor((x + TANK_SIZE - 0.01) / 8);
    const minSubR = Math.floor(y / 8);
    const maxSubR = Math.floor((y + TANK_SIZE - 0.01) / 8);

    for (let r = minSubR; r <= maxSubR; r++) {
      for (let c = minSubC; c <= maxSubC; c++) {
        if (r >= 0 && r < 26 && c >= 0 && c < 26) {
          const cell = subTileGrid[r][c];
          // 1: solid brick/steel, 2: water, 3: eagle
          if (cell === 1 || cell === 2 || cell === 3) {
            return false;
          }
        }
      }
    }

    return true;
  }

  // Authentic Famicom Grid Snapping when turning
  function updatePlayerPhysics() {
    let requestedDir = null;

    if (keys.up) requestedDir = DIR.UP;
    else if (keys.down) requestedDir = DIR.DOWN;
    else if (keys.left) requestedDir = DIR.LEFT;
    else if (keys.right) requestedDir = DIR.RIGHT;

    if (requestedDir !== null) {
      player.moving = true;

      // Handle Direction Change and Famicom Grid Alignment
      if (player.dir !== requestedDir) {
        player.dir = requestedDir;

        // When switching between horizontal and vertical, snap to nearest 8px/16px line
        if (requestedDir === DIR.UP || requestedDir === DIR.DOWN) {
          // Align X to nearest 8px grid (sub-tile alignment)
          const snappedX = Math.round(player.x / 8) * 8;
          if (Math.abs(player.x - snappedX) <= 6) {
            player.x = snappedX;
          }
        } else if (requestedDir === DIR.LEFT || requestedDir === DIR.RIGHT) {
          // Align Y to nearest 8px grid
          const snappedY = Math.round(player.y / 8) * 8;
          if (Math.abs(player.y - snappedY) <= 6) {
            player.y = snappedY;
          }
        }
      }

      // Calculate Target Position
      let nextX = player.x;
      let nextY = player.y;

      if (player.dir === DIR.UP) nextY -= TANK_SPEED;
      else if (player.dir === DIR.DOWN) nextY += TANK_SPEED;
      else if (player.dir === DIR.LEFT) nextX -= TANK_SPEED;
      else if (player.dir === DIR.RIGHT) nextX += TANK_SPEED;

      // Check collision
      if (canMoveTo(nextX, nextY)) {
        player.x = nextX;
        player.y = nextY;
      } else {
        // Try gentle slide / nudge if close to grid alignment
        if (player.dir === DIR.UP || player.dir === DIR.DOWN) {
          const snappedX = Math.round(player.x / 8) * 8;
          if (player.x !== snappedX && canMoveTo(snappedX, nextY)) {
            player.x = snappedX;
            player.y = nextY;
          }
        } else {
          const snappedY = Math.round(player.y / 8) * 8;
          if (player.y !== snappedY && canMoveTo(nextX, snappedY)) {
            player.x = nextX;
            player.y = snappedY;
          }
        }
      }

      // Tread animation (toggles every 4 frames while moving)
      player.animCounter++;
      if (player.animCounter >= 4) {
        player.animCounter = 0;
        player.animFrame = (player.animFrame + 1) % 2;
      }

      // Audio Engine Hum
      if (!engineSoundPlaying && window.nesSynth) {
        window.nesSynth.startEngine(true);
        engineSoundPlaying = true;
      }
    } else {
      player.moving = false;
      if (engineSoundPlaying && window.nesSynth) {
        window.nesSynth.stopEngine();
        engineSoundPlaying = false;
      }
    }

    // Shield animation timer
    if (player.shield) {
      player.shieldTimer--;
      player.shieldFrame = Math.floor(player.shieldTimer / 4) % 2;
      if (player.shieldTimer <= 0) {
        player.shield = false;
      }
    }
  }

  // Fire Player Bullet
  function firePlayerBullet() {
    if (!player.active || isPaused) return;

    // Battle City player 1 starts with 1 bullet capacity (upgrades to 2 with stars)
    const activeBullets = bullets.filter(b => b.active);
    if (activeBullets.length >= 1) return;

    let bx = player.x + 6; // Centered on 16x16 tank
    let by = player.y + 6;

    if (player.dir === DIR.UP) {
      by = player.y - 4;
    } else if (player.dir === DIR.DOWN) {
      by = player.y + 16;
    } else if (player.dir === DIR.LEFT) {
      bx = player.x - 4;
    } else if (player.dir === DIR.RIGHT) {
      bx = player.x + 16;
    }

    bullets.push({
      x: bx,
      y: by,
      dir: player.dir,
      active: true
    });

    if (window.nesSynth) {
      window.nesSynth.playShot();
    }
  }

  // Update Bullets
  function updateBullets() {
    for (let i = bullets.length - 1; i >= 0; i--) {
      const b = bullets[i];
      if (!b.active) {
        bullets.splice(i, 1);
        continue;
      }

      if (b.dir === DIR.UP) b.y -= BULLET_SPEED;
      else if (b.dir === DIR.DOWN) b.y += BULLET_SPEED;
      else if (b.dir === DIR.LEFT) b.x -= BULLET_SPEED;
      else if (b.dir === DIR.RIGHT) b.x += BULLET_SPEED;

      // Check boundary hit
      if (b.x < 0 || b.x > NES_PLAYFIELD_SIZE - 4 || b.y < 0 || b.y > NES_PLAYFIELD_SIZE - 4) {
        b.active = false;
        if (window.nesSynth) window.nesSynth.playHitSteel();
        continue;
      }

      // Check bullet against sub-tiles
      const subC = Math.floor((b.x + 2) / 8);
      const subR = Math.floor((b.y + 2) / 8);

      if (subR >= 0 && subR < 26 && subC >= 0 && subC < 26) {
        const cell = subTileGrid[subR][subC];
        if (cell === 1) { // Brick or Steel
          b.active = false;
          if (window.nesSynth) window.nesSynth.playHitBrick();
        } else if (cell === 3) { // Eagle
          b.active = false;
          if (window.nesSynth) window.nesSynth.playEagleHit();
        }
      }
    }
  }

  // Offscreen static stage canvas for 60fps blitting
  let stageCanvas = null;

  async function prepareStageBackground() {
    if (!currentStageGrid) return;
    stageCanvas = document.createElement('canvas');
    stageCanvas.id = 'temp_stage_buffer_' + Date.now();
    await window.StageRenderer.renderStage(stageCanvas, currentStageGrid, {
      showGrid: false,
      showSpawns: false,
      scale: SCALE
    });
  }

  // Render Loop
  function render() {
    if (!ctx || !canvas) return;

    // 1. Draw static stage background from pre-rendered buffer
    if (stageCanvas) {
      ctx.drawImage(stageCanvas, 0, 0);
    } else {
      ctx.fillStyle = '#636363';
      ctx.fillRect(0, 0, canvas.width, canvas.height);
      ctx.fillStyle = '#000000';
      ctx.fillRect(BORDER, BORDER, NES_PLAYFIELD_SIZE * SCALE, NES_PLAYFIELD_SIZE * SCALE);
    }

    // 2. Draw Bullets (4x4 px CHR Sprite)
    bullets.forEach(b => {
      if (!b.active) return;
      const canvasX = BORDER + b.x * SCALE;
      const canvasY = BORDER + b.y * SCALE;
      ctx.fillStyle = '#ffffff';
      ctx.fillRect(canvasX, canvasY, 3 * SCALE, 3 * SCALE);
    });

    // 3. Draw Player Tank (16x16 px)
    if (player.active) {
      const pCanvasX = BORDER + Math.round(player.x) * SCALE;
      const pCanvasY = BORDER + Math.round(player.y) * SCALE;

      // Base CHR tile for Player 1 (matches renderTankPreview exactly):
      // dir: 0=UP, 1=LEFT, 2=DOWN, 3=RIGHT
      // Frame 0: T = base + dir*8 + 0
      // Frame 1: T = base + dir*8 + 4
      const dirOffset = player.dir * 8;
      const frameOffset = (player.animFrame % 2) * 4;
      const T = 0x00 + dirOffset + frameOffset;
      // Authentic NES metasprite tile layout: [T, T+2, T+1, T+3]
      const tiles = [T, T + 2, T + 1, T + 3];

      // Draw Player Tank using StageRenderer.drawMetasprite (palIdx=4 SP0 Yellow, pt1=true)
      if (window.StageRenderer) {
        window.StageRenderer.drawMetasprite(ctx, tiles, 4, pCanvasX, pCanvasY, true, SCALE);
      }

      // 4. Draw Invincibility Shield (Metasprite 0x28 / 0x2C in Background Bank PT0)
      if (player.shield && window.StageRenderer) {
        // Authentic shield frames from ROM: frame 0: [0x28, 0x2A, 0x29, 0x2B], frame 1: [0x2C, 0x2E, 0x2D, 0x2F]
        const sTiles = player.shieldFrame === 0 
          ? [0x28, 0x2A, 0x29, 0x2B]
          : [0x2C, 0x2E, 0x2D, 0x2F];
        window.StageRenderer.drawMetasprite(ctx, sTiles, 6, pCanvasX, pCanvasY, false, SCALE);
      }
    }

    // 5. Draw Trees on Top Layer
    if (currentStageGrid) {
      for (let r = 0; r < 13; r++) {
        for (let c = 0; c < 13; c++) {
          if (currentStageGrid[r] && currentStageGrid[r][c] === 11) { // Trees
            const tx = BORDER + c * 16 * SCALE;
            const ty = BORDER + r * 16 * SCALE;
            [0x22, 0x22, 0x22, 0x22].forEach((t, i) => {
              const ox = tx + (i % 2) * 8 * SCALE;
              const oy = ty + Math.floor(i / 2) * 8 * SCALE;
              const cached = window.StageRenderer ? window.StageRenderer.getCachedTile(t, 2, true, SCALE) : null;
              if (cached) {
                ctx.drawImage(cached, ox, oy);
              }
            });
          }
        }
      }
    }

    // 6. Draw Pause Overlay
    if (isPaused) {
      ctx.fillStyle = 'rgba(0, 0, 0, 0.65)';
      ctx.fillRect(BORDER, BORDER, NES_PLAYFIELD_SIZE * SCALE, NES_PLAYFIELD_SIZE * SCALE);
      ctx.fillStyle = '#e74c3c';
      ctx.font = 'bold 20px "Press Start 2P", monospace';
      ctx.textAlign = 'center';
      ctx.fillText('PAUSE', BORDER + (NES_PLAYFIELD_SIZE * SCALE) / 2, BORDER + (NES_PLAYFIELD_SIZE * SCALE) / 2);
      ctx.textAlign = 'start';
    }
  }

  // Fixed 60 FPS NES Timestep Constants
  const TARGET_FPS = 60;
  const MS_PER_FRAME = 1000 / TARGET_FPS; // 16.6667 ms
  let lastLoopTime = 0;
  let physicsAccumulator = 0;

  // 60 FPS Game Loop with Fixed Timestep Accumulator
  function gameLoop(now) {
    if (!isRunning) return;

    if (!lastLoopTime) {
      lastLoopTime = now;
      lastFpsCalc = now;
    }

    const delta = now - lastLoopTime;
    lastLoopTime = now;

    // Prevent spiral of death if tab was inactive (clamp max delta to 100ms)
    physicsAccumulator += Math.min(delta, 100);

    // Calculate FPS based on 60Hz ticks / render cycles
    frameCount++;
    if (now - lastFpsCalc >= 500) {
      currentFps = Math.round((frameCount * 1000) / (now - lastFpsCalc));
      frameCount = 0;
      lastFpsCalc = now;
      if (dotNetRef) {
        const dirNames = ['UP', 'LEFT', 'DOWN', 'RIGHT'];
        dotNetRef.invokeMethodAsync('UpdateTelemetry', 
          currentFps, 
          Math.round(player.x), 
          Math.round(player.y), 
          dirNames[player.dir] || 'UP', 
          player.shield
        ).catch(() => {});
      }
    }

    // Fixed 60 Hz Physics & Logic Updates
    while (physicsAccumulator >= MS_PER_FRAME) {
      if (!isPaused) {
        updatePlayerPhysics();
        updateBullets();
      }
      physicsAccumulator -= MS_PER_FRAME;
    }

    render();

    animFrameId = requestAnimationFrame(gameLoop);
  }

  function togglePause() {
    isPaused = !isPaused;
    if (window.nesSynth) {
      window.nesSynth.playPause();
      if (isPaused) window.nesSynth.stopEngine();
    }
  }

  let dotNetRef = null;

  return {
    async init(canvasId, stageGrid, stageNum = 1) {
      canvas = document.getElementById(canvasId);
      if (!canvas) {
        console.warn(`[GameEngine] Canvas #${canvasId} not found`);
        return false;
      }

      ctx = canvas.getContext('2d', { alpha: false });
      ctx.imageSmoothingEnabled = false;

      currentStageGrid = stageGrid;
      currentStageNumber = stageNum;
      buildCollisionMap(stageGrid);

      if (window.StageRenderer) {
        await window.StageRenderer.init();
      }
      await prepareStageBackground();

      // Reset Player position to P1 standard spawn (Col 4, Row 12)
      player.x = 4 * 16;
      player.y = 12 * 16;
      player.dir = DIR.UP;
      player.moving = false;
      player.shield = true;
      player.shieldTimer = 180;
      bullets.length = 0;

      // Bind input events
      window.removeEventListener('keydown', onKeyDown);
      window.removeEventListener('keyup', onKeyUp);
      window.addEventListener('keydown', onKeyDown);
      window.addEventListener('keyup', onKeyUp);

      return true;
    },

    start() {
      if (isRunning) return;
      isRunning = true;
      isPaused = false;
      lastLoopTime = 0;
      physicsAccumulator = 0;
      lastFpsCalc = performance.now();
      frameCount = 0;
      animFrameId = requestAnimationFrame(gameLoop);
      if (window.nesSynth) {
        window.nesSynth.playIntroBGM();
      }
    },

    stop() {
      isRunning = false;
      if (animFrameId) {
        cancelAnimationFrame(animFrameId);
        animFrameId = null;
      }
      if (window.nesSynth) {
        window.nesSynth.stopEngine();
        window.nesSynth.stopBGM();
      }
      engineSoundPlaying = false;
    },

    async setStage(grid, stageNum) {
      currentStageGrid = grid;
      currentStageNumber = stageNum;
      buildCollisionMap(grid);
      await prepareStageBackground();
      this.resetPlayer();
    },

    resetPlayer() {
      player.x = 4 * 16;
      player.y = 12 * 16;
      player.dir = DIR.UP;
      player.moving = false;
      player.shield = true;
      player.shieldTimer = 180;
      bullets.length = 0;
    },

    // Virtual D-Pad / Touch controls
    setVirtualInput(direction, isPressed) {
      if (direction === 'up') keys.up = isPressed;
      if (direction === 'down') keys.down = isPressed;
      if (direction === 'left') keys.left = isPressed;
      if (direction === 'right') keys.right = isPressed;
      if (direction === 'fire' && isPressed) {
        firePlayerBullet();
      }
    },

    togglePause() {
      togglePause();
    },

    setTelemetryDotNetRef(ref) {
      dotNetRef = ref;
    }
  };
})();
