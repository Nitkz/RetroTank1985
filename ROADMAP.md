# 🗺️ RetroTank 1985 — Development Roadmap

Project development roadmap for **RetroTank 1985**, an authentic Battle City (1985 NES) remake in **.NET 9 Blazor WebAssembly**.

---

## 📊 Phase Progress Summary

| Phase | Description | Status | Target Completion |
| :--- | :--- | :---: | :---: |
| **Phase 1** | Reverse Engineering & Asset Pipeline (Audio, Stages, CHR) | ✅ **Completed** | 2026-09-20 |
| **Phase 2** | C# 60 FPS NES Game Loop & Player Tank Controller | ✅ **Completed** | 2026-09-20 |
| **Phase 3** | Destructible Terrain & Bullet-World Collisions | ✅ **Completed** | 2026-09-20 |
| **Phase 4** | Enemy AI & Wave Spawning System | ✅ **Completed** | 2026-09-20 |
| **Phase 5** | Phoenix Eagle Base & Droppable Power-Up System | ⏳ **Planned** | Upcoming |

| **Phase 6** | Stage Transitions, Score Tally & Game Over Flow | ⏳ **Planned** | Upcoming |
| **Phase 7** | Two-Player Co-Op & High Score Persistence | ⏳ **Planned** | Future |
| **Phase 8** | 👑 Epic Boss Battles & Special Munition Crates | 💡 **New / Planned** | Future Expansion |

---

## 🎯 Detailed Phase Breakdown

### ✅ Phase 1: Reverse Engineering & Asset Pipeline (Done)
- [x] **NES 2A03 APU Synthesizer (`nes-synth.js`)**: Real-time Web Audio API synthesis (Pulse 1, Pulse 2, Triangle, Noise channels) for all SFX and music tracks.
- [x] **35 Stage ROM Decoders**: All 35 official stages reverse-engineered into modular JSON files (`data/stages/stage_01.json` - `stage_35.json`).
- [x] **CHR-ROM Sprite Pipeline**: Native 2bpp tile sheet (`chr_all.png`) with Master NES Palette (`$D44A`) and live preview inspection.
- [x] **Stage & Tile Inspector (`/stage-inspector`)**: Interactive tool for inspecting stage maps, enemy recon, CHR tiles, and power-up metasprites.
- [x] **Automated Deployment**: Standalone WASM publish script (`publish-wasm.bat`) optimized for Cloudflare Pages / GitHub Pages.

---

### ✅ Phase 2: C# 60 FPS Hybrid Game Engine & Tank Controller (Done)
- [x] **Hybrid Architecture (C# Game Brain + JS Fast Canvas/Audio Muscle)**:
  - 100% of game state, physics, collisions, and sound dispatching running in .NET 9 C#.
  - Clean separation: `Engine/Core/`, `Engine/Models/`, `Engine/Enums/`, `Services/GameEngineService.cs`.
- [x] **Stage Arena Canvas (`/play`)**: Authentic 208×208 NES Playfield scaled 2x (416px) with arcade bezel border.
- [x] **Fixed 60Hz Timestep Game Loop (`BattleCityEngine.cs`)**: Physics accumulator loop ensuring consistent 60 FPS NES speed across high-refresh displays (120Hz/144Hz).
- [x] **P1 Tank Physics & Controller (`TankPhysics.cs`)**:
  - 4-directional movement (UP, LEFT, DOWN, RIGHT) at authentic ~75 px/sec speed.
  - Famicom 8px grid snapping on turns for smooth navigation through narrow tile corridors.
  - 2-frame authentic tread animation with CHR tile offset matching.
- [x] **Dual Input System**: Desktop keyboard (WASD / Arrow Keys + Space/J) & Mobile Virtual D-Pad + Fire button.
- [x] **Invincibility Shield**: Initial spawn Force Shield animation and timer.
- [x] **Telemetry HUD**: Real-time FPS, coordinate position, direction, and shield indicator (throttled for high-efficiency rendering).
- [x] **Modular Blazor UI (`Components/Play/`)**: Clean decomposition into `GameControlBar`, `TelemetryHud`, `MissionBriefingCard`, `VirtualDPad`, and `Play.razor.cs` code-behind.

---

### ✅ Phase 3: Destructible Terrain & Bullet Collisions (Done)
- [x] **Sub-tile Brick Wall Destruction (`DestructibleMap.cs`)**:
  - 26×26 grid (of 8×8 px sub-tiles) matching authentic NES Battle City ROM layout.
  - Multi-subtile leading edge collision test: cleanly carves full 16px slices on full hits and 8px slices on half-hits.
- [x] **Steel Wall Interaction**:
  - Bullet reflection / deflection metallic sound (`HitSteel`).
  - Bullet cancellation upon hitting impenetrable steel blocks.
- [x] **Bullet vs Bullet Collision**: Mutual cancellation when opposing bullets collide head-on.
- [x] **Small Explosion Animation**: 3-frame explosion sprite animation (`0xA0`, `0xA2`, `0xA4` from CHR-ROM) on bullet impact.
- [x] **Water & Ice Interactions**:
  - Water: Blocks tank movement, lets bullets pass through cleanly.
  - Ice: Traversible terrain with reduced friction.
- [x] **Phoenix Eagle Base Destruction**: Instant state transition to destroyed eagle sprite with game over sound dispatch.

---

### ✅ Phase 4: Enemy AI & Wave Spawning System (Done)
- [x] **3-Point Spawn System**: Top-left (0,0), Top-center (6,0), and Top-right (12,0) spawn positions.
- [x] **Obstruction-Safe Spawner**: Detects if active tank is occupying the spawn zone (< 14px) and delays/rotates spawner to prevent tanks from spawning on top of each other.
- [x] **Spawn Star Animation**: 4-frame rotating star sparkle animation (`0xAD, 0xA9, 0xA5, 0xA1`) before enemy emergence.
- [x] **Max 4 Active Enemies**: 20-enemy queue per stage dispatched sequentially from JSON ROM metadata.
- [x] **4 Distinct Enemy Tank Archetypes**:
  - ⚪ **Basic Tank (Type 0)**: Standard movement speed (60 px/s), standard fire (100 pts, 1 HP).
  - 🟡 **Fast Tank (Type 1)**: High movement speed (~150 px/s), rapid pathing (200 pts, 1 HP).
  - 🔴 **Power Tank (Type 2)**: High-velocity armor-piercing bullets (300 pts, 1 HP).
  - 🟢 **Armor Tank (Type 3)**: 4-hit durability with visual damage color shifting (Green `SP1` -> Yellow `SP0` -> Red `SP3` -> Grey `SP2`, 400 pts) and metallic armor hit SFX.
- [x] **Flashing Enemy Tanks**: Red-flashing variants cycling SP2 <-> SP3 palette every 8 frames.
- [x] **Enemy Pathfinding AI**: Directional choosing algorithm favoring downward/player/eagle paths + 8px grid snapping & obstacle avoidance.
- [x] **Mutual Tank-vs-Tank Collision Physics**:
  - Player vs Enemy & Enemy vs Enemy 14px AABB boundary blocking.
  - Anti-lock physics allowing tanks to freely move in directions that increase separation if initial overlap occurs.
  - Smart AI pathfinding (`PickOpenDirection`) finding open alternate headings when hitting obstacles or other tanks.
- [x] **Dedicated Debug & Sandbox Panel (`GameDebugSandboxPanel.razor`)**: Direct spawner lab for all 4 types + Flashing, Star Power lab (Tiers 0-3), Eagle steel fortify, and Nuke all (Bomb).


---

### ⏳ Phase 5: Phoenix Eagle Base & Droppable Power-Up System
- [ ] **Phoenix HQ (Eagle Base)**:
  - Intact Eagle state (`0xC8..0xCB`).
  - Destroyed Eagle state (`0xCC..0xCF`) triggering instant Stage Defeat.
- [ ] **Droppable Power-Ups (6 Classic Items)**:
  - 🌟 **Star**: Weapon upgrades (Level 1: Fast bullet &rarr; Level 2: Dual bullets &rarr; Level 3: Steel destruction).
  - 🛡️ **Helmet**: 10-second invulnerability force shield.
  - ⏱️ **Timer**: Freezes all enemies for ~10 seconds.
  - 💣 **Grenade**: Destroys all active enemies currently on screen.
  - 🔨 **Shovel**: Temporarily turns eagle fortress into solid steel.
  - 🚗 **Tank (1-Up)**: Grants an extra life.
- [ ] **Score Popup Metasprite**: Floating 500 PTS banner when collecting power-ups.

---

### ⏳ Phase 6: Stage Transitions, Score Tally & Game Over Flow
- [ ] **Stage Curtain Transition**: Classic NES grey sliding shutter animation before stage starts.
- [ ] **Score Tally Screen**:
  - End-of-stage summary screen counting destroyed tanks per type with authentic chime SFX.
  - Total score calculation and bonus stage progression.
- [ ] **Game Over Screen**: Classic "GAME OVER" banner and sound sequence.
- [ ] **Lives & Stage HUD Counter**: Right-side NES HUD with remaining enemy icons, player lives, and flag stage number.

---

### ⏳ Phase 7: Two-Player Co-Op & High Score Persistence
- [ ] **Player 2 Green Tank Support**: Local 2-player mode with split keyboard / dual controller support.
- [ ] **Local Storage Persistence**: Best stage progression, high scores, and audio preference saving.
- [ ] **Stage Editor**: Custom map creator and export tool.

---

### 💡 Phase 8: 👑 Epic Boss Battles & Special Munitions (New Expansion)
- [ ] **End-of-Stage Boss Encounters (บอสใหญ่ท้ายฉาก)**:
  - Giant multi-tile Boss Mech Tank appearing after standard 20-tank wave clearance or at landmark stages (Stage 05, 10, 15, 20, 25, 30, 35).
  - Multi-phase health bar with progressive armor destruction visuals.
- [ ] **Minion Deployment (Boss ปล่อยลูกน้อง)**:
  - Boss summons active support battalions (Basic/Fast/Flashing drones) to flank the player.
- [ ] **Dual-Arm Heavy Cannons (ยิงกระสุนจากแขนสองข้าง)**:
  - Simultaneous twin-cannon firing with spread and cross-fire trajectory patterns.
- [ ] **Multi-Tile Jump & Leap Ability (กระโดดข้ามสิ่งกีดขวางได้หลายช่อง)**:
  - Boss can leap airborne over brick, steel, and water obstacles to reposition or attempt ground-pound slam attacks.
  - Screen shake & shockwave effect upon landing.
- [ ] **Special Munition / Heavy Weapon Crates (หีบกระสุนแรงพิเศษ)**:
  - Droppable tactical weapon crates spawning during boss encounters.
  - Special ammo types:
    - ⚡ **Laser / Railgun**: Pierces through multiple walls in a straight beam.
    - 💥 **Heavy Artillery Plasma Bomb**: Area-of-effect blast destroying 4×4 sub-tiles.
    - 🎯 **Armor Piercing AP Shells**: Inflicts double damage on Boss armor plating.

---

*Last Updated: 2026-09-20 • RetroTank 1985 Core Team*
