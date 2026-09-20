# RetroTank 1985 (Battle City NES Remake)

> **Disclaimer & Fair Use Notice**:
> This project is a non-commercial, technical remake built purely for **educational, reverse-engineering, and archival purposes**.
> The original *Battle City* (1985) game intellectual property (IP), characters, original audio compositions, and brand belong to **Bandai Namco Entertainment Inc.** (formerly Namco).

---

## 🎯 Overview

**RetroTank 1985** is a modern, modular remake of the legendary 1985 NES arcade game *Battle City*, built using **.NET 9 Blazor WebAssembly (WASM)**, **C# Hybrid Game Engine Architecture**, and styled with **MudBlazor** and custom retro aesthetics.

The project demonstrates:
- **Hybrid Architecture (C# Game Brain + JS Fast Canvas/Audio Muscle)**:
  - **100% C# Game Core**: Game state machine, 60Hz fixed timestep accumulator, Famicom 8px grid snapping physics, 26×26 sub-tile destructible terrain, bullet collision simulation, power-up systems, and audio event queuing.
  - **Zero-Allocation 60 FPS Loop**: Pre-allocated entity object pools (`Bullet[16]`, `Explosion[16]`, `EnemyTank[6]`), DTO snapshot recycling (`RenderFrameDto`), double-buffered audio event queue, and cached HUD telemetry preventing Mono WASM GC stutters.
  - **Modular High-Speed JS Muscle**: Lightweight 60 FPS HTML5 Canvas 2D Metasprite blitting (`stage-renderer.js`, `nes-chr.js`, `game-bridge.js`) and Web Audio API 2A03 APU Synthesizer (`nes-synth.js`).
- **Single Source of Truth Bundler (`game-bundle.js`)**: ES6 module loader unifying scripts across both **ASP.NET Core Server Dev** (`App.razor`) and **Cloudflare Pages Standalone WASM** (`index.html`).
- **Dual-Mode Execution**: Runs as a full-stack **ASP.NET Core Blazor Web App** during local development, and deploys as a pure **Standalone WebAssembly SPA** for static cloud hosting (e.g. Cloudflare Pages, GitHub Pages).
- **NES APU Audio Synthesis**: Emulation of Ricoh 2A03 hardware (Pulse 1, Pulse 2, Triangle, and Noise channels) in Web Audio API without relying on pre-recorded audio files.
- **ROM-Accurate Assets**: Native extraction of 8-bit NES CHR tiles, palettes (`$D44A`), and authentic game sprites (including player tanks, enemies, eagle base, and powerups).
- **Modular Component & Code-Behind Architecture**: Clean separation of concerns with sub-components (`Components/Play`, `Components/Inspector`) and isolated `.razor` (markup) and `.razor.cs` (logic) files.
- **Automated Deployment**: One-click build script generating optimized WASM bundles, Cloudflare Pages headers (`_headers`, `_redirects`), and zip release packages.

---

## 🛠 Tech Stack

- **Framework**: .NET 9.0 (Blazor WebAssembly with AOT / Linking support)
- **Architecture**: C# Domain Game Engine + Canvas 2D Interop
- **UI Library**: [MudBlazor](https://mudblazor.com/) (v9.x)
- **Audio Engine**: Custom NES 6502 APU Sound Driver Synthesizer in Web Audio API
- **Asset Pipeline**: Python-assisted CHR-ROM extraction & 2bpp tile rendering
- **Styling**: Vanilla CSS + Google Fonts (`Press Start 2P`, `Rajdhani`)
- **Target Hosting**: Cloudflare Pages / Static CDN or ASP.NET Core Server

---

## 🚀 Getting Started

### Prerequisites
- [.NET 9.0+ SDK](https://dotnet.microsoft.com/download)
- Optional: 7-Zip for faster release compression (`C:\Program Files\7-Zip\7z.exe`)

### 1. Local Development (with Hot Reload)
From the repository root (`RetroTank1985`):

```bash
dotnet watch --project RetroTank1985/RetroTank1985.csproj
```

Or navigate to the server host folder:
```bash
cd RetroTank1985
dotnet watch
```

Open your browser at the local URL (e.g., `http://localhost:5093`).

---

## 📦 Build & Publish for Cloudflare Pages

To produce a production-ready, standalone WebAssembly bundle for **Cloudflare Pages**, run:

```cmd
publish-wasm.bat
```

### What `publish-wasm.bat` does:
1. Cleans previous builds and restores dependencies.
2. Compiles `RetroTank1985.Client` in `Release` mode with **WASM Linking & Trimming**.
3. Emits output to `..\publish\Wasm` (one level up from solution).
4. Generates static hosting configs:
   - `_redirects`: Single Page Application (SPA) routing fallback (`/* /index.html 200`).
   - `_headers`: Pre-compressed Brotli (`.wasm.br`) and Gzip (`.wasm.gz`) content encodings and MIME types for fast CDN delivery.
   - `version.json`: Release timestamp metadata.
5. Automatically archives `wwwroot` into `..\publish\Wasm\RetroTank1985_Wasm_<Timestamp>.zip` ready for one-click upload.

---

## 🕹 Features & Architecture

### 1. Retro Hub (Main Dashboard)
- Central access point for all reverse-engineered game modules.
- Responsive arcade cabinet aesthetic with authentic 1985 color grading.

### 2. Stage Arena (`/play`) — C# Hybrid Game Engine
- **60 FPS Fixed Timestep**: Deterministic physics simulation decoupled from display refresh rates.
- **Zero-Allocation 60 FPS Pipeline**:
  - Reused `RenderFrameDto` snapshot instance and pre-allocated Bullet/Explosion/Enemy pools.
  - Double-buffered audio queue (`AudioEventQueue`) eliminating GC allocations.
  - Offscreen Canvas sub-tile terrain buffering with pre-filtered foreground tree list (`treeSubTiles`).
- **Famicom 8px Grid Snapping & Mutual Tank Collision**:
  - Authentic turning mechanics allowing smooth navigation into 1-tile corridors.
  - Mutual Tank-vs-Tank collision (Player vs Enemy & Enemy vs Enemy) preventing tanks from passing through one another, coupled with anti-lock un-stick physics.
- **26×26 Sub-Tile Destructible Terrain**: Multi-subtile leading-edge bounding box detection carving 16px slices on direct hits and 8px slices on half-hits.
- **20-Tank Enemy Wave & 3-Point Spawner**:
  - 4 archetypes (Basic, Fast, Power, and 4-HP Armor tank with 4-tier NES palette color shifts & metallic hit SFX).
  - Obstruction-safe spawner preventing tanks from spawning on top of occupied points.
  - Red flashing carrier tanks dropping droppable power-ups upon defeat.
- **Full Droppable Power-Up System (6 Classic Items)**:
  - 🌟 **Star**: 3-tier weapon upgrades (fast projectile &rarr; dual concurrent shells &rarr; steel destruction).
  - 🛡️ **Helmet**: 10-second forcefield barrier.
  - ⏱️ **Timer**: 10-second universal enemy freeze.
  - 💣 **Grenade**: Instant screen-wide enemy demolition with full point awards.
  - 🔨 **Shovel**: 20-second Eagle base steel fortification with 3s pre-expiration warning blinking.
  - 🚗 **1-UP Tank**: Extra player life award with authentic NES life chime.
  - Floating `+500 PTS` score popup upon item pickup.
- **Phoenix Eagle HQ Base & Game Over Flow**:
  - Intact vs Destroyed sprite states (`0xC8..0xCB` &rarr; `0xCC..0xCF`), `EagleHit` SFX, and authentic 120-frame (~2.0s) post-game delay allowing explosions and destruction to finish before arcade Game Over overlay.
- **Authentic 5-Phase Explosion Sequence**:
  - Small Bullet Impact: 3-frame 16×16 spark animation (`0xF0..0xFB` from PT0 palette SP3).
  - Tank & Eagle Explosion: 5-phase expanding blast (16×16 Spark, Burst, Blast &rarr; 32×32 Giant Wave Base `$D0` &rarr; 32×32 Smoke Plume Base `$E0`).
- **15-Step Triangle Wave Spawn Star Animation**: Authentic Famicom sparkle sequence cycling `$A0, $A4, $A8, $AC` metasprites before tank emergence.
- **Stage Transitions & Score Tally Screen (Phase 6)**:
  - Classic NES grey shutter curtain wipe with Stage banner.
  - End-of-stage tank tally counting destroyed Basic, Fast, Power, and Armor tanks with authentic audio chimes.
  - Right-side NES HUD tracking 20 remaining enemy tank icons, player lives, and stage flag number.
- **Dedicated Modular Debug & Sandbox Panel**: Live spawner lab, Star Power tier upgrades (0-3), Eagle fortification, Nuke all, and instant 6-item Power-Up lab.
- **Dual Controller Support**: Full keyboard (WASD / Arrows + Space/J) and on-screen Touch D-Pad with Fire button.
- **Live Telemetry HUD**: Throttled 500ms status monitor reporting real-time FPS, coordinate position, direction, remaining wave enemies, and active field enemies.

### 3. NES Sound & BGM Synthesizer (`/sound-bgm`)
Authentic real-time 8-bit sound generation replicating Ricoh 2A03 hardware behavior:

| Effect / Music | NES APU Channel | Technique |
| :--- | :--- | :--- |
| **Tank Fire** | Pulse 1 (Square) | Fast downward pitch drop (980Hz &rarr; 110Hz, 0.12s decay) |
| **Hit Brick** | Noise | Bandpass filtered pseudo-random noise burst (0.08s) |
| **Steel Ricochet** | Pulse 2 (Square) | Crisp dual-pitch metallic square wave (1480Hz & 1760Hz) |
| **Explosion** | Noise | Lowpass swept decaying noise (Normal & Large variations) |
| **Bonus Pickup** | Pulse 1/2 | Rapid rising 6-note arpeggio (C5 to G6) |
| **Tank Engine** | Pulse + Gain Modulation | Looping engine hum with dynamic Idle (55Hz) vs Moving (95Hz) states |
| **Stage Start BGM** | Pulse 1 + Triangle | 2-track lead and bass transcription calibrated from ROM `$ED36` |
| **Stage Clear Jingle** | Pulse 1 + Pulse 2 + Tri | 3-track jingle decoded from ROM `$EEC1` |
| **Victory Fanfare** | Pulse 1 + Pulse 2 + Tri | Full victory fanfare sequence decoded from ROM `$EF3C` |
| **Game Over BGM** | Pulse 1 | Chromatic step-down game over sequence |

### 4. Stage, CHR & Item Inspector (`/stage-inspector`)
- **Stage Arena Viewer (`StageArenaTab`)**: Decoded 35 stages from ROM `$F07A` with live canvas rendering, display toggles (Grid, Spawns, Coords), Enemy Recon ($E4EC / $E578), sequential spawn queue, terrain distribution stats, and JSON viewer.
- **CHR-ROM Tile Catalog (`ChrTileCatalogTab`)**: 512 8×8 tilemap viewer with full NES palette switching (BG0–BG3, SP0–SP3) and interactive 4-direction Tank Metasprite live inspector.
- **Power-ups & Specials Gallery (`PowerUpsSpecialsTab`)**: Interactive 16×16 metasprite catalog for 6 classic droppable items (Helmet, Timer, Shovel, Star, Grenade, 1-UP) with instant bonus SFX testing, along with Phoenix HQ intact/destroyed and Force Shield badges.

### 5. Upcoming: 👑 Epic Boss Battles & Tactical Munitions (Phase 8)
- **Mega Boss Tank Encounters**: Giant multi-tile armored Boss Mechs with multi-phase HP bars.
- **Minion Swarm Deployment**: Boss actively summons support tank drones.
- **Dual Arm Artillery**: Simultaneous twin-cannon firing with spread/cross-fire projectile mechanics.
- **Multi-Tile Jump Maneuver**: Boss leaps airborne across brick, steel, and water obstacles.
- **Tactical Weapon Crates**: Crates dropping Laser Rails, AOE Plasma Bombs, and Heavy AP Shells.

---

## 📁 Project Structure

```
RetroTank1985/
├── publish-wasm.bat             # Automated Release script (Cloudflare Pages + Zip)
├── RetroTank1985.slnx           # Modern .NET Solution File
├── ROADMAP.md                   # Detailed development roadmap (Phases 1 - 8)
├── README.md                    # Project documentation
│
├── RetroTank1985/               # Server host project (Blazor Web App for local dev)
│   ├── Components/
│   │   ├── App.razor            # Root HTML template for Server Host mode
│   │   └── Routes.razor
│   └── Program.cs               # Host configuration & MapStaticAssets
│
├── RetroTank1985.Client/        # Pure WebAssembly Client (Runs locally & in Cloudflare)
│   ├── Components/
│   │   ├── Play/                # Play Arena Sub-Components
│   │   │   ├── GameControlBar.razor       # Top stage picker & action controls
│   │   │   ├── GameDebugSandboxPanel.razor# Live debug & entity sandbox panel
│   │   │   ├── MissionBriefingCard.razor  # Enemy battalion breakdown & key guide
│   │   │   ├── TelemetryHud.razor         # Real-time HUD status strip
│   │   │   └── VirtualDPad.razor          # Mobile on-screen touch controller
│   │   └── Inspector/           # Stage & CHR Inspector Sub-Components
│   │       ├── StageArenaTab.razor        # 35-Stage map canvas & Recon UI
│   │       ├── StageArenaTab.razor.cs     # StageArenaTab Code-Behind
│   │       ├── ChrTileCatalogTab.razor    # 512 CHR Sheet & Tank metasprites UI
│   │       ├── ChrTileCatalogTab.razor.cs # ChrTileCatalogTab Code-Behind
│   │       ├── PowerUpsSpecialsTab.razor  # Power-ups & Specials metasprites UI
│   │       └── PowerUpsSpecialsTab.razor.cs # PowerUpsSpecialsTab Code-Behind
│   ├── Engine/                  # C# Game Core Layer (Brain)
│   │   ├── Core/                # Physics, DestructibleMap, EnemySystem, PowerUpSystem, Bullets, Engine
│   │   ├── Enums/               # Direction, SubTileType, PowerUpType, GameEnums
│   │   └── Models/              # PlayerTank, EnemyTank, PowerUp, GameEntities, RenderFrameDto, InputState
│   ├── Layout/
│   │   └── MainLayout.razor     # Retro arcade layout & MudBlazor theme
│   ├── Models/
│   │   └── StageModel.cs        # Stage, Tile, and Enemy data models
│   ├── Pages/
│   │   ├── Home.razor           # Navigation Hub
│   │   ├── Play.razor           # Stage Arena Razor Template
│   │   ├── Play.razor.cs        # Stage Arena Code-Behind
│   │   ├── SoundBgm.razor       # Sound & BGM Synthesizer Razor Template
│   │   ├── SoundBgm.razor.cs    # Sound & BGM Synthesizer Code-Behind
│   │   ├── StageInspector.razor # Stage & CHR Inspector Razor Template
│   │   └── StageInspector.razor.cs # Stage & CHR Inspector Code-Behind
│   ├── Services/
│   │   ├── GameEngineService.cs # Blazor JS Interop & Session lifecycle service
│   │   └── StageService.cs      # Stage JSON loader with in-memory caching
│   ├── wwwroot/
│   │   ├── app.css              # Pixel font and CRT styling
│   │   ├── favicon.png          # 64x64 ROM-extracted retro tank icon
│   │   ├── assets/sprites/      # CHR tile sheets (chr_all.png)
│   │   ├── data/stages/         # 35 individual stage JSONs + manifest.json
│   │   ├── index.html           # Standalone entry point for Cloudflare Pages
│   │   └── js/
│   │       ├── game-bundle.js   # Single Source of Truth JS module bundler
│   │       ├── nes-chr.js       # Core NES CHR tile & palette decoding engine
│   │       ├── nes-synth.js     # NES APU sound synthesizer engine
│   │       ├── stage-renderer.js# Fast 60 FPS In-Game Canvas 2D Blitter
│   │       ├── stage-inspector.js# DevTools for /stage-inspector page
│   │       └── game-bridge.js   # Fast JS RAF ticker, input listener & audio dispatcher
│   ├── Program.cs               # Dynamic client bootstrapper
│   └── RetroTank1985.Client.csproj
│
└── ../publish/Wasm/             # Output folder for Cloudflare release (and zip archives)
```

---

## 📜 License & Acknowledgements

- Built for educational study of 1980s video game architecture, audio synthesis, and WebAssembly integration.
- Battle City © 1985 Bandai Namco Entertainment Inc. All rights reserved.
