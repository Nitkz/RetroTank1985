# 🗺️ RetroTank 1985 — Development Roadmap

Project development roadmap for **RetroTank 1985**, an authentic Battle City (1985 NES) remake in **.NET 9 Blazor WebAssembly**.

---

## 📊 Phase Progress Summary

| Phase | Description | Status | Target Completion |
| :--- | :--- | :---: | :---: |
| **Phase 1** | Reverse Engineering & Asset Pipeline (Audio, Stages, CHR) | ✅ **Completed** | 2026-09-20 |
| **Phase 2** | 60 FPS NES Game Loop & Player Tank Controller | ✅ **Completed** | 2026-09-20 |
| **Phase 3** | Destructible Terrain & Bullet-World Collisions | 🟡 **In Progress** | Next |
| **Phase 4** | Enemy AI & Wave Spawning System | ⏳ **Planned** | Upcoming |
| **Phase 5** | Phoenix Eagle Base & Droppable Power-Up System | ⏳ **Planned** | Upcoming |
| **Phase 6** | Stage Transitions, Score Tally & Game Over Flow | ⏳ **Planned** | Upcoming |
| **Phase 7** | Two-Player Co-Op & High Score Persistence | ⏳ **Planned** | Future |

---

## 🎯 Detailed Phase Breakdown

### ✅ Phase 1: Reverse Engineering & Asset Pipeline (Done)
- [x] **NES 2A03 APU Synthesizer (`nes-synth.js`)**: Real-time Web Audio API synthesis (Pulse 1, Pulse 2, Triangle, Noise channels) for all SFX and music tracks.
- [x] **35 Stage ROM Decoders**: All 35 official stages reverse-engineered into modular JSON files (`data/stages/stage_01.json` - `stage_35.json`).
- [x] **CHR-ROM Sprite Pipeline**: Native 2bpp tile sheet (`chr_all.png`) with Master NES Palette (`$D44A`) and live preview inspection.
- [x] **Stage & Tile Inspector (`/stage-inspector`)**: Interactive tool for inspecting stage maps, enemy recon, CHR tiles, and power-up metasprites.
- [x] **Automated Deployment**: Standalone WASM publish script (`publish-wasm.bat`) optimized for Cloudflare Pages / GitHub Pages.

---

### ✅ Phase 2: 60 FPS Game Loop & Player Tank Controller (Done)
- [x] **Stage Arena Canvas (`/play`)**: Authentic 208×208 NES Playfield scaled 2x (416px) with authentic arcade bezel border.
- [x] **Fixed 60Hz Timestep Game Loop (`game-engine.js`)**: Physics accumulator loop ensuring consistent 60 FPS NES speed across high-refresh displays (120Hz/144Hz).
- [x] **P1 Tank Controller**:
  - 4-directional movement (UP, LEFT, DOWN, RIGHT) at authentic ~75 px/sec speed.
  - Famicom 8px grid snapping on turns for smooth navigation through narrow tile corridors.
  - 2-frame authentic tread animation with CHR tile offset matching.
- [x] **Bullet Physics**: Initial bullet firing system with NES APU fire sound, speed regulation, and playfield boundary checks.
- [x] **Dual Input System**: Desktop keyboard (WASD / Arrow Keys + Space/J) & Mobile Virtual D-Pad + Fire button.
- [x] **Invincibility Shield**: Initial spawn Force Shield animation and timer.
- [x] **Telemetry HUD**: Real-time FPS, coordinate position, direction, and shield indicator.

---

### 🟡 Phase 3: Destructible Terrain & Bullet Collisions (In Progress / Next)
- [ ] **Sub-tile Brick Wall Destruction**:
  - Sub-grid (26×26 of 8×8 px) destruction model matching NES Battle City.
  - Directional brick chipping (destroying 2 sub-tiles per bullet impact).
- [ ] **Steel Wall Interaction**:
  - Bullet reflection / deflection sound (`nesSynth.playClink`).
  - Bullet cancellation upon hitting steel.
- [ ] **Bullet vs Bullet Collision**: Mutual cancellation when player and enemy bullets collide head-on.
- [ ] **Small Explosion Effect**: 3-frame explosion sprite animation (`0xA0..0xA4` from CHR-ROM) on bullet impact.
- [ ] **Water & Ice Interactions**:
  - Water: Blocks tank movement, lets bullets pass through.
  - Ice: Reduces friction / sliding effect when moving over ice tiles.

---

### ⏳ Phase 4: Enemy AI & Wave Spawning System
- [ ] **3-Point Spawn System**: Top-left, Top-center, and Top-right spawn positions.
- [ ] **Spawn Star Animation**: 4-frame rotating star sparkle animation before enemy emergence.
- [ ] **Max 4 Active Enemies**: 20-enemy queue per stage dispatched sequentially.
- [ ] **4 Distinct Enemy Tank Archetypes**:
  - ⚪ **Basic Tank**: Slow movement, slow fire (100 pts).
  - 🟡 **Fast Tank**: High movement speed, aggressive pathfinding (200 pts).
  - 🔴 **Power Tank**: High-velocity armor-piercing bullets (300 pts).
  - 🟢 **Armor Tank**: 4-hit durability with visual damage color shifting (400 pts).
- [ ] **Flashing Enemy Tanks**: Red-flashing variants carrying power-ups.
- [ ] **Enemy Pathfinding AI**: Directional choosing algorithm favoring downward/player/eagle paths.

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

*Last Updated: 2026-09-20 • RetroTank 1985 Core Team*
