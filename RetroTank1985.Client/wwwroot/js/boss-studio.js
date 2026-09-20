/**
 * boss-studio.js — Interactive Boss Mech Sprite Studio & Animation Engine
 * Transforms the pixel art concept "ปลา" (Demon Manta Ray Boss) & "คนดี โหมดแรง" (Hyper Tank)
 * into NES-authentic multi-tile metasprites and real-time battle animations.
 */

window.BossStudio = (function () {
  // Palettes for "ปลา" (Manta Ray Boss)
  // Index: 0=Transparent, 1=Dark Emerald, 2=Olive Fringes/Tusks, 3=Gold Yellow/Eyes, 4=Crimson Hood, 5=Black Outline, 6=Mask Core
  const BOSS_PALETTES = {
    manta: {
      name: '🐟 มัจฉาปีศาจ / ปลา (Emerald Manta Official)',
      bg: '#0c0d12',
      colors: ['transparent', '#165b33', '#6c9338', '#facc15', '#991b1b', '#000000', '#064e3b'],
      glow: '#facc15',
      eye: '#facc15',
      core: '#991b1b',
      laser: '#facc15',
      star: '#6c9338'
    },
    crimson: {
      name: '🔴 Blood Leviathan ($0F, $16, $27, $30)',
      bg: '#0c0d12',
      colors: ['transparent', '#540400', '#982220', '#facc15', '#300088', '#000000', '#3C1800'],
      glow: '#EC6A64',
      eye: '#facc15',
      core: '#EC6A64',
      laser: '#FF3366',
      star: '#FFE600'
    },
    steel: {
      name: '⚪ Cyber Manta Steel ($0F, $00, $10, $20)',
      bg: '#0c0d12',
      colors: ['transparent', '#1e293b', '#64748b', '#00e5ff', '#e11d48', '#000000', '#0f172a'],
      glow: '#00e5ff',
      eye: '#00e5ff',
      core: '#e11d48',
      laser: '#00e5ff',
      star: '#ffffff'
    },
    void: {
      name: '🟣 Void Ray Abomination ($0F, $14, $24, $38)',
      bg: '#0c0d12',
      colors: ['transparent', '#3b0764', '#7e22ce', '#38cc6c', '#db2777', '#000000', '#1e1b4b'],
      glow: '#38cc6c',
      eye: '#38cc6c',
      core: '#db2777',
      laser: '#38cc6c',
      star: '#e454ec'
    }
  };

  // 64x48 Master Matrix for "ปลา" (Demon Manta Ray Boss) — 100% Perfectly Symmetrical (Bilateral Mirror)
  // 0: Transparent, 1: Emerald Green, 2: Olive/Tusks, 3: Yellow/Eyes/Lightning/Flower, 4: Crimson Hood, 5: Black Outline, 6: Mask Beak
  const BOSS_HALF_MATRIX = [
    // 00-05: Top Horns
    "00000000000000000000000005150000",
    "00000000000000000000000051150000",
    "00000000000000000000000511150000",
    "00000000000000000000005111150000",
    "00000000000000000000051111155555",
    "00000000000000000000511111111111",
    // 06-10: 4 Glowing Eyes [● ● ● ●] & Upper Mantlet Spread
    "00000000000005555555111111111111",
    "00000000555551111111115335115335",
    "00005555111111111111115335115335",
    "00551111111111111111111551111551",
    "05111111111111111111111111155555",
    // 11-15: Wings Expanding & Crimson Hood / 4 Curved Claws
    "55111111111111111115511111544444",
    "51115511111111111152251115444444",
    "51152255111111111522251154446666",
    "51522222511111115222251544465566",
    "55222222511111152222251544655566",
    // 16-20: Claw Tips, Mask Beak & Yellow Lightning Zigzags
    "05552222551111522222511544666666",
    "00055222115511522225111544666655",
    "00005111111153352251111154466655",
    "00051111111533335511111115446655",
    "00511111153335533511111111544655",
    // 21-25: Lower Claws, Beak Nostrils & Wing Underside Fringes
    "05111115333550053351111111154455",
    "51111533355000005335111111115544",
    "51153335500000000533511111111551",
    "55533550000000000053351111111511",
    "05550000000000000005335511111511",
    // 26-30: Sinuous Center Tail & Lower Jagged Cape
    "00000000000000000000533351111511",
    "00000000000000000000053335111511",
    "00000000000000000000522533511511",
    "00000000000000000005222253351511",
    "00000000000000000052222225335511",
    // 31-35: Cape Fringes & Tail Descending
    "00000000000000000522222222533511",
    "00000000000000005222222222253511",
    "00000000000000052225522222255511",
    "00000000000000522250052222250511",
    "00000000000000522500005222500511",
    // 36-40: Spiked Cape Tips & Lower Tail
    "00000000000000525000000522500051",
    "00000000000000550000000055000051",
    "00000000000000000000000000000051",
    "00000000000000000000000000000051",
    "00000000000000000000000000000051",
    // 41-47: 6-Petal Yellow Flower Stinger with Green Core
    "00000000000000000000000000005335",
    "00000000000000000000000000533335",
    "00000000000000000000000053333333",
    "00000000000000000000000055333113",
    "00000000000000000000000533333113",
    "00000000000000000000000053335555",
    "00000000000000000000000005550000"
  ];

  // Dynamically generate mathematically guaranteed 100% bilateral mirror symmetrical 64x48 matrix
  const BOSS_MATRIX_64 = BOSS_HALF_MATRIX.map(halfRow => {
    const reversed = halfRow.split('').reverse().join('');
    return halfRow + reversed;
  });


  // Dedicated Palette for "คนดี โหมดแรง" (Heavy Combat Battle Tank)
  // 0: Transparent, 1: Tread Charcoal, 2: Gold Armor, 3: Highlight Armor, 4: Cyan Optics, 5: Black Outline, 6: Steel Tread Cleats
  const PLAYER_TANK_PALETTE = {
    name: 'Heavy Titan Player Tank',
    colors: ['transparent', '#1e293b', '#f59e0b', '#fde047', '#00f0ff', '#000000', '#94a3b8']
  };

  // Dedicated Palette for "คนร้าย" (Villain Assault Tank)
  // 0: Transparent, 1: Crimson Red, 2: Dark Charcoal Armor, 3: Rust Orange, 4: Glowing Purple Eye, 5: Black Outline, 6: Big Road Wheels
  const VILLAIN_TANK_PALETTE = {
    name: 'Villain Assault Tank',
    colors: ['transparent', '#dc2626', '#1e1e24', '#f97316', '#a855f7', '#000000', '#64748b']
  };

  // 24x24 Master Matrix for "คนดี โหมดแรง" (Heavy Combat Battle Tank with Long Twin Cannons & Caterpillar Treads)
  const PLAYER_HYPER_24 = [
    "000000555500005555000000", // Row 00: Twin Muzzle Brakes
    "000000533500005335000000", // Row 01: Twin Extended Cannons
    "000000533500005335000000", // Row 02: Twin Extended Cannons
    "000000533500005335000000", // Row 03: Twin Extended Cannons
    "000000533500005335000000", // Row 04: Twin Extended Cannons
    "000005533555555335500000", // Row 05: Heavy Gun Mantlet Mount
    "555555533333333335555555", // Row 06: Front Angled Armor / Wedge
    "511522333333333333225115", // Row 07: Left Tread Front | Glacis Plate | Right Tread Front
    "516522222244442222225165", // Row 08: Front Headlights & Sloped Armor
    "511522222222222222225115", // Row 09: Front Hull
    "561555555555555555555615", // Row 10: Turret Ring Seam
    "511533333333333333335115", // Row 11: Heavy Turret Front Armor
    "561533222222222222335615", // Row 12: Turret Center
    "511532244222222442235115", // Row 13: Commander Cupola & Gunner Periscope
    "561532244222222442235615", // Row 14: Cupola Hatches
    "511533222222222222335115", // Row 15: Turret Rear Armor Plate
    "561555555555555555555615", // Row 16: Turret Base Ring
    "511522222222222222225115", // Row 17: Rear Engine Deck
    "561522111111111111225615", // Row 18: Engine Grille Louvers
    "511522111111111111225115", // Row 19: Engine Grille Louvers
    "561522222222222222225615", // Row 20: Rear Mudguard Plates
    "511555555555555555555115", // Row 21: Rear Tow Hooks
    "561500000000000000005615", // Row 22: Rear Drive Sprocket Rollers
    "555500000000000000005555"  // Row 23: Track Rear Base
  ];

  // 24x24 Master Matrix for "คนร้าย" (Villain Assault Tank: High High-Angled Cannon + 2 Big Road Wheels + Pointed Wedge)
  const VILLAIN_TANK_24 = [
    "000000000000005555000000", // Row 00: High Angled Cannon Muzzle
    "000000000000051115000000", // Row 01: Long Elevated Barrel
    "000000000000511150000000", // Row 02: Elevated Barrel
    "000000000005111500000000", // Row 03: Elevated Barrel
    "000000000051115000000000", // Row 04: Cannon Breech
    "000000555551155555500000", // Row 05: Turret Mantlet & Heavy Spikes
    "000055111115511111550000", // Row 06: Pointed Turret Cap
    "000511111111111111115000", // Row 07: Sloped Heavy Armor
    "005111444411114444111500", // Row 08: Menacing Purple Optics
    "051111455411114554111150", // Row 09: Armored Slit Visors
    "551111111111111111111155", // Row 10: Heavy Glacis Plate
    "522222222222222222222225", // Row 11: Front Blade Wedge
    "555555555555555555555555", // Row 12: Hull Seam
    "511111111111111111111115", // Row 13: Mid Hull Deck
    "511555551111115555511115", // Row 14: Top of 2 Giant Road Wheels
    "515666665111156666651115", // Row 15: Giant Wheel Outer Hub
    "556655666511566556665115", // Row 16: Massive Wheel Sprockets
    "556555566511565555665115", // Row 17: Wheel Axles
    "556655666511566556665115", // Row 18: Massive Wheel Spokes
    "515666665111156666651115", // Row 19: Bottom of Giant Road Wheels
    "511555551111115555511115", // Row 20: Ground Cleats
    "533333333333333333333335", // Row 21: Heavy Reinforced Undercarriage
    "533555553333335555533335", // Row 22: Wheel Base Runners
    "055500055555555000555550"  // Row 23: Track Ground Base
  ];

  let currentPaletteKey = 'manta';
  let currentTerrainTheme = 'cave'; // 'cave' (สีน้ำเงินจากถ้ำ) | 'ground' (สีน้ำตาลจากพื้นดิน)
  let isSimulating = false;
  let simAnimId = null;

  // Mini Arena Simulation State
  let simState = {
    canvas: null,
    ctx: null,
    scale: 2,
    tick: 0,
    terrainTheme: 'cave', // 'cave' or 'ground'
    boss: {
      x: 104 - 32,
      y: 15,
      w: 64,
      h: 48,
      specialHp: 10,  // "มีเลือดพิเศษ 10 เลือด"
      maxSpecialHp: 10,
      coreHp: 20,     // "ของเลือด 20" (4 blocks of 5 hp each = "ถ้าเลือดละ 5 ประชัน")
      maxCoreHp: 20,
      phase: 1,       // 1: Special Armor, 2: Core HP (>3), 3: Rage Mode (<=3) ("จากพิเศษเลือดเหลือ 3")
      state: 'hover', // hover, fire, jump, slam, stunned
      targetX: 72,
      targetY: 15,
      dir: 2,
      jumpHeight: 0,
      jumpT: 0,
      cooldown: 50,
      minions: []
    },
    player: {
      x: 104 - 12,
      y: 165,
      w: 24,
      h: 24,
      hp: 100,
      maxHp: 100,
      ammo: 20,        // "กระสุนปืน 20"
      maxAmmo: 20,
      dir: 0,
      weapon: 'ap',
      cooldown: 0,
      invuln: 0
    },
    bullets: [],
    bossProjectiles: [],
    explosions: [],
    shockwaves: [],
    crates: [],
    shakeAmount: 0
  };

  function drawPixelMatrix(ctx, matrix, x, y, pixelScale, pal, options = {}) {
    const {
      layerHorns = true,
      layerEyes = true,
      layerWings = true,
      layerCannons = true,
      layerCore = true,
      layerTracks = true,
      phase = 1,
      glowPulse = 1,
      flashWhite = false
    } = options;

    const rows = matrix.length;
    const cols = matrix[0].length;

    for (let r = 0; r < rows; r++) {
      for (let c = 0; c < cols; c++) {
        const char = matrix[r][c];
        const val = parseInt(char, 10);
        if (val === 0) continue;

        // Layer toggles
        if (!layerHorns && r < 6) continue;
        if (!layerEyes && r >= 6 && r <= 10 && val === 3) continue;
        if (!layerWings && (c < 20 || c > 43) && r >= 8 && r <= 25) continue;
        if (!layerCannons && ((c >= 17 && c <= 27) || (c >= 36 && c <= 46)) && r >= 11 && r <= 22 && val === 2) continue;
        if (!layerCore && r >= 11 && r <= 23 && c >= 26 && c <= 37) continue;
        if (!layerTracks && r >= 40) continue; // Tail / Stinger

        let color = '#ffffff';
        if (flashWhite) {
          color = '#FFFFFF';
        } else if (val === 5) {
          color = pal.colors[5]; // Outline
        } else if (val === 3) {
          // Yellow: Eyes, Lightning, Stinger Flower
          color = pal.colors[3];
        } else if (val === 4) {
          // Crimson Hood Collar
          color = pal.colors[4];
        } else if (val === 6) {
          // Inner Mask Beak
          color = pal.colors[6] || pal.colors[1];
        } else {
          // 1: Emerald Body, 2: Olive Fringes/Tusks
          color = pal.colors[val] || pal.colors[1];
          // Phase damage degradation
          if (phase >= 2 && (r + c) % 8 === 0 && val !== 5) {
            color = '#333333'; // Cracked armor
          }
          if (phase === 3 && (r * c) % 6 === 0 && val !== 5) {
            color = (Math.random() > 0.5) ? '#FFCC00' : '#FF3300'; // Exposed sparks
          }
        }

        ctx.fillStyle = color;
        ctx.fillRect(x + c * pixelScale, y + r * pixelScale, pixelScale, pixelScale);
      }
    }
  }

  return {
    setPalette(key) {
      if (BOSS_PALETTES[key]) {
        currentPaletteKey = key;
      }
    },

    getPalettesList() {
      return Object.keys(BOSS_PALETTES).map(k => ({
        key: k,
        name: BOSS_PALETTES[k].name,
        color: BOSS_PALETTES[k].colors[1]
      }));
    },

    // Render single high-res boss preview
    renderBossPreview(canvasId, options = {}) {
      const canvas = document.getElementById(canvasId);
      if (!canvas) return;
      const ctx = canvas.getContext('2d');
      ctx.imageSmoothingEnabled = false;

      const {
        scale = 6,
        showGrid = false,
        showTileGrid = false,
        showCoords = false,
        animState = 'idle',
        paletteKey = currentPaletteKey,
        layerHorns = true,
        layerEyes = true,
        layerWings = true,
        layerCannons = true,
        layerCore = true,
        layerTracks = true,
        phase = 1
      } = options;

      const pal = BOSS_PALETTES[paletteKey] || BOSS_PALETTES.manta;
      const width = 64 * scale;
      const height = 48 * scale;
      const padding = 20;
      canvas.width = width + padding * 2;
      canvas.height = height + padding * 2;

      // Dark background
      ctx.fillStyle = '#0a0c10';
      ctx.fillRect(0, 0, canvas.width, canvas.height);

      // Ambient radial glow behind manta boss
      const gradient = ctx.createRadialGradient(
        canvas.width / 2, canvas.height / 2, 15,
        canvas.width / 2, canvas.height / 2, width / 1.8
      );
      gradient.addColorStop(0, `${pal.glow}25`);
      gradient.addColorStop(1, 'transparent');
      ctx.fillStyle = gradient;
      ctx.fillRect(0, 0, canvas.width, canvas.height);

      const bx = padding;
      const by = padding;

      // Draw boss sprite
      drawPixelMatrix(ctx, BOSS_MATRIX_64, bx, by, scale, pal, {
        layerHorns, layerEyes, layerWings, layerCannons, layerCore, layerTracks,
        phase: animState === 'damaged' ? 3 : phase,
        glowPulse: 1
      });

      // Special animation overlays
      if (animState === 'fire') {
        // Dual laser beams & flower stinger burst
        ctx.fillStyle = pal.laser;
        ctx.shadowColor = pal.laser;
        ctx.shadowBlur = 10;
        // Left claw laser
        ctx.fillRect(bx + 20 * scale, by + 22 * scale, 3 * scale, 22 * scale);
        // Right claw laser
        ctx.fillRect(bx + 41 * scale, by + 22 * scale, 3 * scale, 22 * scale);

        // Flower Stinger Energy Pulse
        ctx.fillStyle = pal.eye;
        const starX = bx + 32 * scale;
        const starY = by + 45 * scale;
        ctx.beginPath();
        ctx.arc(starX, starY, 7 * scale, 0, Math.PI * 2);
        ctx.fill();
        ctx.shadowBlur = 0;
      }

      // Optional Pixel Grid (every 1px)
      if (showGrid && scale >= 4) {
        ctx.strokeStyle = 'rgba(255, 255, 255, 0.07)';
        ctx.lineWidth = 1;
        for (let x = 0; x <= 64; x++) {
          ctx.beginPath();
          ctx.moveTo(bx + x * scale, by);
          ctx.lineTo(bx + x * scale, by + height);
          ctx.stroke();
        }
        for (let y = 0; y <= 48; y++) {
          ctx.beginPath();
          ctx.moveTo(bx, by + y * scale);
          ctx.lineTo(bx + width, by + y * scale);
          ctx.stroke();
        }
      }

      // Optional 8x8 NES Tile Matrix Grid (8x6 = 48 tiles)
      if (showTileGrid) {
        ctx.strokeStyle = 'rgba(250, 204, 21, 0.45)';
        ctx.lineWidth = 1.5;
        for (let tx = 0; tx <= 8; tx++) {
          ctx.beginPath();
          ctx.moveTo(bx + tx * 8 * scale, by);
          ctx.lineTo(bx + tx * 8 * scale, by + height);
          ctx.stroke();
        }
        for (let ty = 0; ty <= 6; ty++) {
          ctx.beginPath();
          ctx.moveTo(bx, by + ty * 8 * scale);
          ctx.lineTo(bx + width, by + ty * 8 * scale);
          ctx.stroke();
        }

        // Tile Index Labels
        ctx.fillStyle = '#facc15';
        ctx.font = 'bold 8px monospace';
        for (let ty = 0; ty < 6; ty++) {
          for (let tx = 0; tx < 8; tx++) {
            const tileIdx = ty * 8 + tx;
            ctx.fillText(`T${tileIdx.toString(16).toUpperCase().padStart(2, '0')}`, bx + tx * 8 * scale + 2, by + ty * 8 * scale + 9);
          }
        }
      }

      if (showCoords) {
        ctx.fillStyle = '#94a3b8';
        ctx.font = '9px monospace';
        ctx.fillText('0,0', bx - 14, by - 4);
        ctx.fillText('64,48', bx + width + 2, by + height + 12);
      }
    },

    // Render Hyper Player Tank Preview ("คนดี โหมดแรง")
    renderPlayerHyperPreview(canvasId, scale = 6) {
      const canvas = document.getElementById(canvasId);
      if (!canvas) return;
      const ctx = canvas.getContext('2d');
      ctx.imageSmoothingEnabled = false;

      const size = 24 * scale;
      canvas.width = size + 16;
      canvas.height = size + 16;

      ctx.fillStyle = '#0a0c10';
      ctx.fillRect(0, 0, canvas.width, canvas.height);

      drawPixelMatrix(ctx, PLAYER_HYPER_24, 8, 8, scale, PLAYER_TANK_PALETTE, {});
    },

    // Render Villain Assault Tank Preview ("คนร้าย")
    renderVillainPreview(canvasId, scale = 6) {
      const canvas = document.getElementById(canvasId);
      if (!canvas) return;
      const ctx = canvas.getContext('2d');
      ctx.imageSmoothingEnabled = false;

      const size = 24 * scale;
      canvas.width = size + 16;
      canvas.height = size + 16;

      ctx.fillStyle = '#0a0c10';
      ctx.fillRect(0, 0, canvas.width, canvas.height);

      drawPixelMatrix(ctx, VILLAIN_TANK_24, 8, 8, scale, VILLAIN_TANK_PALETTE, {});
    },

    setTerrainTheme(theme) {
      currentTerrainTheme = theme;
      simState.terrainTheme = theme;
    },

    // Start Real-Time Interactive Mini-Arena Simulator
    startMiniArena(canvasId, options = {}) {
      const canvas = document.getElementById(canvasId);
      if (!canvas) return;
      this.stopMiniArena();

      isSimulating = true;
      const ctx = canvas.getContext('2d');
      ctx.imageSmoothingEnabled = false;

      const scale = options.scale || 2;
      canvas.width = 208 * scale;
      canvas.height = 208 * scale;

      simState.canvas = canvas;
      simState.ctx = ctx;
      simState.scale = scale;
      simState.tick = 0;
      simState.terrainTheme = currentTerrainTheme;
      
      // Multi-phase Boss HP: 10 Special Armor + 20 Core HP (4 blocks of 5)
      simState.boss.specialHp = 10;
      simState.boss.maxSpecialHp = 10;
      simState.boss.coreHp = 20;
      simState.boss.maxCoreHp = 20;
      simState.boss.phase = 1;
      simState.boss.state = 'hover';
      simState.boss.x = 104 - 32;
      simState.boss.y = 20;
      simState.boss.w = 64;
      simState.boss.h = 48;
      simState.boss.minions = [];

      // Player AP Tank: 100 HP, 20 Ammo
      simState.player.hp = 100;
      simState.player.ammo = 20;
      simState.player.x = 104 - 12;
      simState.player.y = 152;
      simState.bullets = [];
      simState.bossProjectiles = [];
      simState.explosions = [];
      simState.shockwaves = [];
      simState.crates = [
        { x: 30, y: 130, type: 'ammo', label: '📦 +10', life: 1000 },
        { x: 160, y: 130, type: 'ap', label: '🎯 AP', life: 1000 }
      ];

      // Battle City Essential Core: Eagle Base & Destructible Brick Cover
      simState.eagle = { x: 104 - 8, y: 184, alive: true };
      simState.bricks = [
        // Eagle Protection Bunker
        { x: 88, y: 184, w: 8, h: 16, alive: true },
        { x: 96, y: 176, w: 16, h: 8, alive: true },
        { x: 112, y: 184, w: 8, h: 16, alive: true },
        // Midfield Strategic Brick Covers
        { x: 40, y: 110, w: 16, h: 12, alive: true },
        { x: 152, y: 110, w: 16, h: 12, alive: true },
        { x: 96, y: 90, w: 16, h: 12, alive: true }
      ];

      const pal = BOSS_PALETTES[currentPaletteKey] || BOSS_PALETTES.manta;

      function loop() {
        if (!isSimulating) return;
        simState.tick++;

        // 1. Boss AI Logic ("ปลา" Manta Leviathan)
        const b = simState.boss;
        const p = simState.player;

        // Periodic ammo resupply crate drop
        if (simState.tick % 300 === 0 && simState.crates.length < 3) {
          const rx = Math.random() * (160 - 40) + 40;
          const ry = Math.random() * (150 - 100) + 100;
          simState.crates.push({ x: rx, y: ry, type: 'ammo', label: '📦 +10', life: 800 });
        }

        if (b.state === 'hover') {
          // Floating undulating hover movement
          b.x += Math.sin(simState.tick * 0.04) * (b.phase === 3 ? 2.5 : 1.5);
          b.x = Math.max(8, Math.min(208 - 72, b.x));
          b.y = 20 + Math.sin(simState.tick * 0.08) * 4;

          b.cooldown--;
          if (b.cooldown <= 0) {
            const rand = Math.random();
            if (rand < 0.45) {
              b.state = 'fire';
              b.cooldown = b.phase === 3 ? 25 : 40;
              // Spawn Twin Claw Energy + Flower Stinger Blast
              simState.bossProjectiles.push(
                { x: b.x + 20, y: b.y + 24, vx: -0.6, vy: 3.2, type: 'laser' },
                { x: b.x + 41, y: b.y + 24, vx: 0.6, vy: 3.2, type: 'laser' },
                { x: b.x + 32, y: b.y + 46, vx: 0, vy: 2.8, type: 'flower' }
              );
              try { window.nesSynth?.playShoot(2); } catch (_) {}
            } else if (rand < 0.75) {
              b.state = 'jump';
              b.jumpT = 0;
              b.targetX = Math.random() * (208 - 74) + 8;
              b.targetY = Math.random() * 40 + 15;
              b.cooldown = 50;
            } else {
              // Spawn "คนร้าย" Villain Assault Minion Tank!
              if (b.minions.length < 2) {
                b.minions.push({
                  x: Math.random() > 0.5 ? 24 : 160,
                  y: 110,
                  hp: 3,
                  w: 16,
                  h: 16,
                  vx: (Math.random() - 0.5) * 1.5,
                  shootCooldown: 60
                });
              }
              b.cooldown = 70;
            }
          }
        } else if (b.state === 'fire') {
          b.cooldown--;
          if (b.cooldown <= 0) {
            b.state = 'hover';
            b.cooldown = b.phase === 3 ? 25 : 45;
          }
        } else if (b.state === 'jump') {
          b.jumpT += 0.035;
          b.x += (b.targetX - b.x) * 0.06;
          b.y += (b.targetY - b.y) * 0.06;
          b.jumpHeight = Math.sin(b.jumpT * Math.PI) * 40;

          if (b.jumpT >= 1) {
            b.state = 'slam';
            b.jumpHeight = 0;
            b.cooldown = 30;
            simState.shakeAmount = 14;
            simState.shockwaves.push({ x: b.x + 32, y: b.y + 24, r: 6, maxR: 60, life: 30 });
            try { window.nesSynth?.playExplosion(true); } catch (_) {}
          }
        } else if (b.state === 'slam') {
          b.cooldown--;
          if (b.cooldown <= 0) {
            b.state = 'hover';
            b.cooldown = 40;
          }
        }

        // Update "คนร้าย" Villain Minions
        for (let i = b.minions.length - 1; i >= 0; i--) {
          const m = b.minions[i];
          m.x += m.vx;
          if (m.x < 20 || m.x > 170) m.vx *= -1;
          m.shootCooldown--;
          if (m.shootCooldown <= 0) {
            m.shootCooldown = 80;
            simState.bossProjectiles.push({
              x: m.x + 8,
              y: m.y + 16,
              vx: (p.x - m.x) * 0.015,
              vy: 2.2,
              type: 'villain'
            });
          }
        }

        // 2. Update Bullets & Projectiles
        for (let i = simState.bullets.length - 1; i >= 0; i--) {
          const bul = simState.bullets[i];
          bul.y += bul.vy;
          bul.x += bul.vx;

          // Check hit Villain Minions
          let hitMinion = false;
          for (let mIdx = b.minions.length - 1; mIdx >= 0; mIdx--) {
            const m = b.minions[mIdx];
            if (bul.x >= m.x && bul.x <= m.x + m.w && bul.y >= m.y && bul.y <= m.y + m.h) {
              m.hp -= bul.dmg;
              simState.explosions.push({ x: bul.x, y: bul.y, r: 10, life: 12 });
              simState.bullets.splice(i, 1);
              hitMinion = true;
              if (m.hp <= 0) {
                b.minions.splice(mIdx, 1);
                simState.explosions.push({ x: m.x + 8, y: m.y + 8, r: 18, life: 25 });
                try { window.nesSynth?.playExplosion(false); } catch (_) {}
              }
              break;
            }
          }
          if (hitMinion) continue;

          // Check hit Boss ("ปลา")
          if (bul.y <= b.y + b.h && bul.y >= b.y && bul.x >= b.x && bul.x <= b.x + b.w) {
            const totalDmg = bul.dmg;
            if (b.specialHp > 0) {
              b.specialHp = Math.max(0, b.specialHp - Math.ceil(totalDmg / 5));
              if (b.specialHp === 0) {
                b.phase = 2; // Broke Special Armor
                simState.shakeAmount = 12;
                simState.shockwaves.push({ x: b.x + 32, y: b.y + 24, r: 10, maxR: 70, life: 30 });
              }
            } else {
              b.coreHp = Math.max(0, b.coreHp - Math.ceil(totalDmg / 6));
              // Phase 3: "จากพิเศษเลือดเหลือ 3" (Rage Mode when Core HP <= 3)
              if (b.coreHp <= 3 && b.phase < 3) {
                b.phase = 3;
                simState.shakeAmount = 18;
              }
            }

            simState.explosions.push({ x: bul.x, y: bul.y, r: 8, life: 10 });
            simState.bullets.splice(i, 1);

            if (b.coreHp <= 0 && b.specialHp <= 0) {
              simState.shakeAmount = 25;
              simState.explosions.push(
                { x: b.x + 15, y: b.y + 10, r: 24, life: 40 },
                { x: b.x + 45, y: b.y + 15, r: 20, life: 40 },
                { x: b.x + 32, y: b.y + 35, r: 30, life: 50 }
              );
              try { window.nesSynth?.playExplosion(true); } catch (_) {}
            }
            continue;
          }

          // Check hit Bricks
          for (const brk of simState.bricks) {
            if (brk.alive && bul.x >= brk.x && bul.x <= brk.x + brk.w && bul.y >= brk.y && bul.y <= brk.y + brk.h) {
              brk.alive = false;
              simState.explosions.push({ x: bul.x, y: bul.y, r: 8, life: 10 });
              simState.bullets.splice(i, 1);
              try { window.nesSynth?.playExplosion(false); } catch (_) {}
              break;
            }
          }
          if (!simState.bullets[i]) continue;

          // Screen bounds
          if (bul.y < 0 || bul.y > 208 || bul.x < 0 || bul.x > 208) {
            simState.bullets.splice(i, 1);
          }
        }

        // Check Crate pickups
        for (let i = simState.crates.length - 1; i >= 0; i--) {
          const c = simState.crates[i];
          if (p.x + p.w >= c.x && p.x <= c.x + 14 && p.y + p.h >= c.y && p.y <= c.y + 14) {
            if (c.type === 'ammo') {
              p.ammo = Math.min(p.maxAmmo, p.ammo + 10);
            } else if (c.type === 'ap') {
              p.weapon = 'ap';
              p.ammo = Math.min(p.maxAmmo, p.ammo + 5);
            }
            simState.explosions.push({ x: c.x + 6, y: c.y + 6, r: 14, life: 12 });
            simState.crates.splice(i, 1);
            try { window.nesSynth?.playPowerup(); } catch (_) {}
          }
        }

        for (let i = simState.bossProjectiles.length - 1; i >= 0; i--) {
          const bp = simState.bossProjectiles[i];
          bp.x += bp.vx;
          bp.y += bp.vy;

          // Check hit Player
          if (bp.x >= p.x && bp.x <= p.x + p.w && bp.y >= p.y && bp.y <= p.y + p.h) {
            p.hp = Math.max(0, p.hp - 12);
            simState.explosions.push({ x: bp.x, y: bp.y, r: 12, life: 15 });
            simState.bossProjectiles.splice(i, 1);
            try { window.nesSynth?.playExplosion(false); } catch (_) {}
            continue;
          }

          // Check hit Bricks
          let hitBrk = false;
          for (const brk of simState.bricks) {
            if (brk.alive && bp.x >= brk.x && bp.x <= brk.x + brk.w && bp.y >= brk.y && bp.y <= brk.y + brk.h) {
              brk.alive = false;
              simState.explosions.push({ x: bp.x, y: bp.y, r: 10, life: 12 });
              simState.bossProjectiles.splice(i, 1);
              hitBrk = true;
              try { window.nesSynth?.playExplosion(false); } catch (_) {}
              break;
            }
          }
          if (hitBrk) continue;

          // Check hit Eagle HQ
          if (simState.eagle.alive && bp.x >= simState.eagle.x && bp.x <= simState.eagle.x + 16 && bp.y >= simState.eagle.y && bp.y <= simState.eagle.y + 16) {
            simState.eagle.alive = false;
            simState.explosions.push({ x: simState.eagle.x + 8, y: simState.eagle.y + 8, r: 24, life: 35 });
            simState.bossProjectiles.splice(i, 1);
            try { window.nesSynth?.playExplosion(true); } catch (_) {}
            continue;
          }

          if (bp.y > 208 || bp.x < 0 || bp.x > 208) {
            simState.bossProjectiles.splice(i, 1);
          }
        }

        // 3. Render Arena with Cave / Ground Theme Palette
        ctx.save();
        if (simState.shakeAmount > 0) {
          const sx = (Math.random() - 0.5) * simState.shakeAmount;
          const sy = (Math.random() - 0.5) * simState.shakeAmount;
          ctx.translate(sx, sy);
          simState.shakeAmount *= 0.85;
          if (simState.shakeAmount < 0.5) simState.shakeAmount = 0;
        }

        const isCave = (simState.terrainTheme === 'cave');

        // Arena Background: "สีน้ำเงินจากถ้ำ" vs "สีน้ำตาลจากพื้นดิน"
        ctx.fillStyle = isCave ? '#0b1120' : '#1c130c';
        ctx.fillRect(0, 0, canvas.width, canvas.height);

        // Backdrop Details: "ติดภูเขา" on Left, "บันได" on Right
        if (isCave) {
          // Cave Stalactites & Blue Stone Cliffs
          ctx.fillStyle = '#1e293b';
          // Left Mountain Cliff ("ติดภูเขา")
          ctx.beginPath();
          ctx.moveTo(0, 0);
          ctx.lineTo(24 * scale, 60 * scale);
          ctx.lineTo(16 * scale, 120 * scale);
          ctx.lineTo(26 * scale, 180 * scale);
          ctx.lineTo(0, 208 * scale);
          ctx.fill();

          // Right Staircase ("บันได")
          ctx.fillStyle = '#334155';
          for (let step = 0; step < 7; step++) {
            ctx.fillRect((208 - 8 - step * 4) * scale, (80 + step * 16) * scale, 30 * scale, 14 * scale);
          }
        } else {
          // Brown Ground & Rocky Cliffs ("สีน้ำตาลจากพื้นดิน")
          ctx.fillStyle = '#451a03';
          // Left Mountain Cliff
          ctx.beginPath();
          ctx.moveTo(0, 0);
          ctx.lineTo(24 * scale, 60 * scale);
          ctx.lineTo(16 * scale, 120 * scale);
          ctx.lineTo(26 * scale, 180 * scale);
          ctx.lineTo(0, 208 * scale);
          ctx.fill();

          // Right Staircase Steps
          ctx.fillStyle = '#78350f';
          for (let step = 0; step < 7; step++) {
            ctx.fillRect((208 - 8 - step * 4) * scale, (80 + step * 16) * scale, 30 * scale, 14 * scale);
          }
        }

        // Outer Steel Arena Borders
        ctx.fillStyle = isCave ? '#1e3a8a' : '#854d0e';
        ctx.fillRect(0, 0, canvas.width, 8 * scale);
        ctx.fillRect(0, canvas.height - 8 * scale, canvas.width, 8 * scale);
        ctx.fillRect(0, 0, 8 * scale, canvas.height);
        ctx.fillRect(canvas.width - 8 * scale, 0, 8 * scale, canvas.height);

        // Draw Destructible Brick Walls (Classic Battle City Bricks)
        for (const brk of simState.bricks) {
          if (!brk.alive) continue;
          ctx.fillStyle = '#b91c1c'; // Brick Red
          ctx.fillRect(brk.x * scale, brk.y * scale, brk.w * scale, brk.h * scale);
          ctx.fillStyle = '#fca5a5';
          for (let bx = 0; bx < brk.w; bx += 4) {
            for (let by = 0; by < brk.h; by += 4) {
              ctx.fillRect((brk.x + bx) * scale, (brk.y + by) * scale, 3 * scale, 1 * scale);
            }
          }
        }

        // Draw Eagle HQ (Classic Battle City Phoenix / Eagle Base)
        const eX = simState.eagle.x * scale;
        const eY = simState.eagle.y * scale;
        if (simState.eagle.alive) {
          ctx.fillStyle = '#f59e0b'; // Golden Eagle
          ctx.fillRect(eX, eY, 16 * scale, 16 * scale);
          ctx.fillStyle = '#1e293b';
          ctx.fillRect(eX + 4 * scale, eY + 4 * scale, 8 * scale, 8 * scale);
          ctx.fillStyle = '#fde047';
          ctx.font = 'bold 9px monospace';
          ctx.fillText('🦅', eX + 1 * scale, eY + 12 * scale);
        } else {
          ctx.fillStyle = '#475569'; // Destroyed Base
          ctx.fillRect(eX, eY, 16 * scale, 16 * scale);
          ctx.fillStyle = '#ef4444';
          ctx.font = 'bold 9px monospace';
          ctx.fillText('💀', eX + 1 * scale, eY + 12 * scale);
        }

        // Shockwaves
        for (let i = simState.shockwaves.length - 1; i >= 0; i--) {
          const sw = simState.shockwaves[i];
          sw.r += 2.5;
          sw.life--;
          ctx.strokeStyle = `rgba(250, 204, 21, ${sw.life / 30})`;
          ctx.lineWidth = 3 * scale;
          ctx.beginPath();
          ctx.arc(sw.x * scale, sw.y * scale, sw.r * scale, 0, Math.PI * 2);
          ctx.stroke();
          if (sw.life <= 0) simState.shockwaves.splice(i, 1);
        }

        // Draw Crates (Resupply: Ammo & AP Munitions)
        for (const c of simState.crates) {
          ctx.fillStyle = c.type === 'ammo' ? '#16a34a' : '#f59e0b';
          ctx.fillRect(c.x * scale, c.y * scale, 14 * scale, 14 * scale);
          ctx.strokeStyle = '#FFFFFF';
          ctx.lineWidth = 1;
          ctx.strokeRect(c.x * scale, c.y * scale, 14 * scale, 14 * scale);
          ctx.fillStyle = '#FFF';
          ctx.font = 'bold 7px monospace';
          ctx.fillText(c.label, (c.x + 1) * scale, (c.y + 10) * scale);
        }

        // Draw "คนร้าย" Villain Minions
        for (const m of b.minions) {
          drawPixelMatrix(ctx, VILLAIN_TANK_24, m.x * scale, m.y * scale, scale * 0.7, VILLAIN_TANK_PALETTE, {});
        }

        // Draw Player "คนดี โหมดแรง" (Heavy Combat Battle Tank)
        if (p.hp > 0) {
          drawPixelMatrix(ctx, PLAYER_HYPER_24, p.x * scale, p.y * scale, scale, PLAYER_TANK_PALETTE, {});
        }

        // Draw Boss "ปลา" (with shadow when jumping)
        if (b.coreHp > 0 || b.specialHp > 0) {
          if (b.jumpHeight > 0) {
            ctx.fillStyle = 'rgba(0,0,0,0.5)';
            ctx.beginPath();
            ctx.ellipse((b.x + 32) * scale, (b.y + 40) * scale, 28 * scale, 10 * scale, 0, 0, Math.PI * 2);
            ctx.fill();
          }

          const curPal = BOSS_PALETTES[currentPaletteKey] || BOSS_PALETTES.manta;
          drawPixelMatrix(
            ctx,
            BOSS_MATRIX_64,
            b.x * scale,
            (b.y - b.jumpHeight) * scale,
            scale,
            curPal,
            {
              phase: b.phase,
              animState: b.state,
              flashWhite: (simState.tick % 4 === 0 && (b.coreHp < 20 || b.specialHp < 10))
            }
          );
        }

        // Draw Bullets & Projectiles
        ctx.fillStyle = '#00E5FF';
        for (const bul of simState.bullets) {
          ctx.fillRect(bul.x * scale, bul.y * scale, 3 * scale, 6 * scale);
        }

        for (const bp of simState.bossProjectiles) {
          ctx.fillStyle = bp.type === 'flower' ? '#facc15' : bp.type === 'villain' ? '#ef4444' : pal.laser;
          ctx.beginPath();
          ctx.arc(bp.x * scale, bp.y * scale, (bp.type === 'flower' ? 5 : 3.5) * scale, 0, Math.PI * 2);
          ctx.fill();
        }

        // Draw Explosions
        for (let i = simState.explosions.length - 1; i >= 0; i--) {
          const ex = simState.explosions[i];
          ex.life--;
          ctx.fillStyle = (ex.life % 2 === 0) ? '#FFE600' : '#FF3300';
          ctx.beginPath();
          ctx.arc(ex.x * scale, ex.y * scale, ex.r * scale * (1 - ex.life / 20), 0, Math.PI * 2);
          ctx.fill();
          if (ex.life <= 0) simState.explosions.splice(i, 1);
        }

        // 4. Draw HUD (Boss Dual HP Bars & Player Ammo/Health)
        // Boss Special HP (10 blocks) & Core HP (20 blocks = 4 segments of 5)
        const hudX = 14 * scale;
        const hudY = 10 * scale;

        // Label
        ctx.fillStyle = '#FFFFFF';
        ctx.font = 'bold 8px monospace';
        const phaseName = b.phase === 1 ? '🛡️ SPECIAL ARMOR' : b.phase === 2 ? '⚡ CORE HP' : '🔥 RAGE MODE';
        ctx.fillText(`👑 ปลา [${phaseName}]`, hudX, hudY - 2 * scale);

        // Layer 1: Special Armor HP Bar ("มีเลือดพิเศษ 10 เลือด")
        if (b.specialHp > 0) {
          ctx.fillStyle = '#4c1d95';
          ctx.fillRect(hudX, hudY, 180 * scale, 5 * scale);
          ctx.fillStyle = '#a855f7';
          ctx.fillRect(hudX, hudY, (180 * (b.specialHp / b.maxSpecialHp)) * scale, 5 * scale);
          ctx.strokeStyle = '#c084fc';
          ctx.lineWidth = 1;
          ctx.strokeRect(hudX, hudY, 180 * scale, 5 * scale);
        }

        // Layer 2: Core HP Bar (20 HP / 4 blocks of 5 = "ถ้าเลือดละ 5 ประชัน")
        const coreBarY = b.specialHp > 0 ? hudY + 7 * scale : hudY;
        ctx.fillStyle = '#7f1d1d';
        ctx.fillRect(hudX, coreBarY, 180 * scale, 5 * scale);
        ctx.fillStyle = b.phase === 3 ? '#ef4444' : '#22c55e';
        ctx.fillRect(hudX, coreBarY, (180 * (b.coreHp / b.maxCoreHp)) * scale, 5 * scale);
        ctx.strokeStyle = '#FFFFFF';
        ctx.lineWidth = 1;
        ctx.strokeRect(hudX, coreBarY, 180 * scale, 5 * scale);
        // Draw 4 segment ticks (every 5 HP)
        ctx.strokeStyle = 'rgba(0,0,0,0.8)';
        for (let seg = 1; seg <= 3; seg++) {
          ctx.beginPath();
          ctx.moveTo(hudX + seg * (180 / 4) * scale, coreBarY);
          ctx.lineTo(hudX + seg * (180 / 4) * scale, coreBarY + 5 * scale);
          ctx.stroke();
        }

        // Player HUD: "คนดี โหมดแรง" + Ammo ("กระสุนปืน 20")
        ctx.fillStyle = '#38bdf8';
        ctx.font = 'bold 8px monospace';
        ctx.fillText(`🛡️ คนดี โหมดแรง HP: ${Math.ceil(p.hp)}%`, 12 * scale, 196 * scale);

        // Ammo Display
        const ammoColor = p.ammo > 5 ? '#facc15' : '#ef4444';
        ctx.fillStyle = ammoColor;
        ctx.fillText(`🎯 กระสุนปืน: ${p.ammo}/${p.maxAmmo} ${p.ammo === 0 ? '[RELOAD OUT!]' : ''}`, 12 * scale, 204 * scale);

        ctx.restore();

        simAnimId = requestAnimationFrame(loop);
      }

      simAnimId = requestAnimationFrame(loop);
    },

    stopMiniArena() {
      isSimulating = false;
      if (simAnimId) {
        cancelAnimationFrame(simAnimId);
        simAnimId = null;
      }
    },

    // Player action triggers
    playerFire(weaponType = 'ap') {
      if (!isSimulating || simState.player.hp <= 0) return;
      const p = simState.player;

      // Check Ammo Count ("กระสุนปืน 20")
      if (p.ammo <= 0) {
        try { window.nesSynth?.playShoot(3); } catch (_) {}
        return;
      }

      p.ammo--;
      p.weapon = weaponType;

      if (weaponType === 'railgun') {
        simState.bullets.push({
          x: p.x + 10,
          y: p.y - 4,
          vx: 0,
          vy: -6,
          dmg: 10
        });
      } else if (weaponType === 'plasma') {
        simState.bullets.push(
          { x: p.x + 6, y: p.y - 4, vx: -1, vy: -5, dmg: 6 },
          { x: p.x + 16, y: p.y - 4, vx: 1, vy: -5, dmg: 6 }
        );
      } else if (weaponType === 'ap') {
        simState.bullets.push({
          x: p.x + 11,
          y: p.y - 4,
          vx: 0,
          vy: -7,
          dmg: 15
        });
      }

      try { window.nesSynth?.playShoot(0); } catch (_) {}
    },

    playerReload() {
      if (!isSimulating || simState.player.hp <= 0) return;
      simState.player.ammo = simState.player.maxAmmo;
      try { window.nesSynth?.playPowerup(); } catch (_) {}
    },

    playerMove(dx) {
      if (!isSimulating || simState.player.hp <= 0) return;
      simState.player.x = Math.max(12, Math.min(208 - 36, simState.player.x + dx * 12));
    },

    bossTriggerJump() {
      if (!isSimulating || (simState.boss.coreHp <= 0 && simState.boss.specialHp <= 0)) return;
      simState.boss.state = 'jump';
      simState.boss.jumpT = 0;
      simState.boss.targetX = Math.random() * (208 - 74) + 8;
      simState.boss.targetY = Math.random() * 40 + 15;
    },

    bossTriggerFire() {
      if (!isSimulating || (simState.boss.coreHp <= 0 && simState.boss.specialHp <= 0)) return;
      const b = simState.boss;
      b.state = 'fire';
      b.cooldown = 40;
      simState.bossProjectiles.push(
        { x: b.x + 20, y: b.y + 24, vx: -0.6, vy: 3.2, type: 'laser' },
        { x: b.x + 41, y: b.y + 24, vx: 0.6, vy: 3.2, type: 'laser' },
        { x: b.x + 32, y: b.y + 46, vx: 0, vy: 2.8, type: 'flower' }
      );
      try { window.nesSynth?.playShoot(2); } catch (_) {}
    },

    // Export C# and JSON Byte Matrix for Phase 8 Boss Engine
    exportSpriteData() {
      return {
        matrix64: BOSS_MATRIX_64,
        playerHyper24: PLAYER_HYPER_24,
        villainTank24: VILLAIN_TANK_24,
        palettes: BOSS_PALETTES,
        csharpCode: `// Generated Boss Metasprite Definition: "ปลา" (Demon Manta Ray) & Vehicles
public static class BossSpriteData
{
    public const int Width = 64;
    public const int Height = 48;
    public const int SubTilesX = 8;
    public const int SubTilesY = 6;
    
    // Boss Matrix 64x48
    public static readonly string[] BossMatrix64 = new string[]
    {
${BOSS_MATRIX_64.map(r => `        "${r}",`).join('\n')}
    };

    // "คนดี โหมดแรง" Player Hyper AP Tank 24x24
    public static readonly string[] PlayerHyper24 = new string[]
    {
${PLAYER_HYPER_24.map(r => `        "${r}",`).join('\n')}
    };

    // "คนร้าย" Villain Assault Tank 24x24
    public static readonly string[] VillainTank24 = new string[]
    {
${VILLAIN_TANK_24.map(r => `        "${r}",`).join('\n')}
    };
}`
      };
    }
  };
})();
