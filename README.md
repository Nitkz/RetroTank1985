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

## 📚 Documentation Index

For modularity and ease of reference, all detailed technical specifications and roadmaps are organized in the [`docs/`](file:///d:/OtherProject/NitkSoft/BattleCity/RetroTank1985/docs) directory:

| Document | Description | Target Audience |
| :--- | :--- | :--- |
| 📖 [README.md](file:///d:/OtherProject/NitkSoft/BattleCity/RetroTank1985/README.md) | Project Overview, Architecture, Tech Stack, Setup, Code Structure & Deployment | All Developers & Contributors |
| 🗺️ [ROADMAP.md](file:///d:/OtherProject/NitkSoft/BattleCity/RetroTank1985/docs/ROADMAP.md) | 10-Phase Development Roadmap, Milestones, and Historical Changelog | Project Tracking & Planning |
| 🌐 [ONLINE_COOP_DESIGN_SPEC.md](file:///d:/OtherProject/NitkSoft/BattleCity/RetroTank1985/docs/ONLINE_COOP_DESIGN_SPEC.md) | Real-Time Online 2-Player Co-Op, WebRTC P2P/SignalR, Lobby & Borrow Life Spec | Multiplayer & Network Devs |
| 🛡️ [GAME_MECHANICS_SPEC.md](file:///d:/OtherProject/NitkSoft/BattleCity/RetroTank1985/docs/GAME_MECHANICS_SPEC.md) | Game Pace Tuning, Kid-Friendly Presets, and Dual-Layer Defense / Armor System | Game Designers & Engine Devs |
| 👑 [BOSS_DESIGN_SPEC.md](file:///d:/OtherProject/NitkSoft/BattleCity/RetroTank1985/docs/BOSS_DESIGN_SPEC.md) | Giant Boss Mech Battle Mechanics, Attack Patterns, and Special Munitions | Expansion Feature Devs |

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

### 2. Server Configuration (Docker/Env)
If you are running the backend in a container or production environment, you can configure the maximum number of concurrent Co-Op rooms (default is 10) by passing the `MAX_ROOMS` environment variable:
```bash
# Example Docker run command
docker run -e MAX_ROOMS=20 -p 5093:80 retrotank1985-server
```
Alternatively, configure `"MaxRooms": 20` inside the `"GameSettings"` block in `appsettings.json`.

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

### 1. Game Launch Hub (`/`)
- **Frictionless Onboarding**: Central launchpad designed to get players into the action immediately.
- **1-Player Single Campaign**:
  - Direct stage selector (Stages 1–35) with quick-pick grid and stepper buttons.
  - Quick game options dialog (Difficulty presets: Kids Friendly, Classic 1985, Veteran; Starting Lives & Armor).
  - One-click `START MISSION` button launching directly into `/arcade?stage=XX`.
- **2-Player Online Co-Op**:
  - Instant link to multiplayer lobby with live room codes and QR pairing.
- **Dev Tools Quick Access**: Direct links to technical sandboxes, audio synthesizer, and ROM inspector.

### 2. Arcade Mode (`/arcade`) — Focused 1P & Co-Op Arena
- **Pure Gameplay Arena**:
  - Streamlined HUD focused strictly on gameplay — stage skip cheats and mid-game restart buttons removed to maintain retro immersion and prevent accidental resets.
  - Live HUD displays active Stage number (`STAGE XX`), Score, High Score, Player Lives, and remaining enemy wave counter.
  - Safe exit options: `EXIT [ESC]` back to Home Hub or `LEAVE ROOM` in co-op sessions.
- **Cross-Platform Controls**:
  - **Desktop**: `WASD` / `Arrow Keys` (Move with Famicom 8px grid snapping), `Enter` / `Space` / `J` (Fire), `P` / `Esc` (Pause), `F` (Fullscreen).
  - **Mobile MOBA Touch Controller**: Floating dynamic joystick base, cardinal glow arrows, dedicated fire button, and safe-area fullscreen overlay.
  - Single-player mode automatically hides Player 2 switcher and emote buttons for a clean interface.

### 3. Online Co-Op Lobby & Arena (`/coop`)
- **Real-Time Multiplayer**: Dual-mode networking via WebRTC DataChannels (P2P for static WASM) and SignalR WebSockets Hub.
- **Pre-Game Lobby Customization**:
  - Host can configure Game Options (Difficulty, Starting Lives, Armor, Base Defense) before launching.
  - Real-time difficulty preset badge visible to both Host and Guest.
- **Multiplayer Mechanics**:
  - Borrow Life mechanic (`FIRE` on death to borrow from teammate).
  - Tactical Emote Wheel (radial quick chat).
  - Mutual bullet cancellation clink (friendly fire safe).
  - Synchronized dual-column end-of-stage tally screen with MVP medals.

### 4. Developer Suite & Labs (`/dev`)
Centralized technical portal providing access to diagnostic and testing environments:
- **Engine Sandbox & Physics Lab (`/sandbox`, `/dev/sandbox`, `/play`)**:
  - Interactive testbed with cheat drawer: live enemy spawner, droppable power-up laboratory, invincibility and Star Power upgrades.
  - Real-time telemetry: FPS, coordinates, heading, and wave queue breakdown.
  - Dual-tank controller testing (P1 Yellow / P2 Green).
- **NES APU Sound Synthesizer (`/sound-bgm`)**:
  - Real-time Web Audio API Ricoh 2A03 hardware emulation (Pulse 1, Pulse 2, Triangle, and Noise channels).
  - Interactive soundboard and ROM-accurate BGM player (Stage Start, Clear, Victory Fanfare, Game Over).
- **Stage & CHR Inspector (`/stage-inspector`)**:
  - Decoded 35 NES stage maps from ROM `$F07A` with enemy battalion recon.
  - 512 8×8 CHR-ROM tile catalog with master palette switching (`$D44A`).
  - Metasprite studio and Boss Mech preview.

---

## 📁 Project Structure

```
RetroTank1985/
├── docs/                        # Modular Technical Specifications & Roadmaps
│   ├── ROADMAP.md               # Detailed development roadmap (Phases 1 - 10)
│   ├── ONLINE_COOP_DESIGN_SPEC.md # Real-Time Online 2-Player Co-Op Specification
│   ├── GAME_MECHANICS_SPEC.md   # Game Pace, Kid-Friendly, & Dual-Layer Defense Spec
│   └── BOSS_DESIGN_SPEC.md      # Giant Boss Mech battle mechanics & munitions
├── publish-wasm.bat             # Automated Release script (Cloudflare Pages + Zip)
├── RetroTank1985.slnx           # Modern .NET Solution File (Shared + Client + Server)
├── README.md                    # Project overview & documentation index
│
├── RetroTank1985.Shared/        # Shared Contracts & Domain Class Library (.NET 9)
│   ├── Contracts/               # SignalR Hub & Client contracts (ICoopLobbyContracts.cs)
│   ├── Enums/                   # Direction, SubTileType, PowerUpType, GameEnums, CoopEnums
│   └── Models/                  # StageModel, GameSettings, and Network DTOs
│       └── Network/             # CoopRoomModels, WebRtcSignaling, CoopGamePackets, Tally, LifeBorrow
│
├── RetroTank1985/               # Server host project (Blazor Web App & SignalR Server)
│   ├── Components/
│   │   ├── App.razor            # Root HTML template for Server Host mode
│   │   └── Routes.razor
│   └── Program.cs               # Host configuration & MapStaticAssets
│
├── RetroTank1985.Client/        # Pure WebAssembly Client (Runs locally & in Cloudflare)
│   ├── Components/
│   │   ├── Arcade/              # Dedicated Arcade Mode Components
│   │   │   ├── ArcadeControlBar.razor   # Streamlined HUD & safe exit controls
│   │   │   ├── ArcadeHeader.razor       # Live score, high score, lives, armor HUD
│   │   │   └── ArcadeDisconnectOverlay.razor # Network reconnect overlay
│   │   ├── Coop/                # Online Co-Op Lobby & Gameplay Components
│   │   │   ├── CoopLobbyCard.razor      # Room pairing, options dialog & ready state
│   │   │   └── EmoteWheel.razor         # 8-direction radial quick chat
│   │   ├── Controls/            # Cross-Mode Input & Touch Controllers
│   │   │   ├── MobaTouchController.razor # Ergonomic floating joystick controller
│   │   │   └── VirtualDPad.razor         # Classic on-screen directional pad
│   │   ├── Sandbox/             # Engine Sandbox Sub-Components
│   │   │   ├── GameControlBar.razor      # Stage stepper, restart & mute bar
│   │   │   ├── GameDebugSandboxPanel.razor # Live spawner, power-up lab & cheats
│   │   │   ├── MissionBriefingCard.razor # Enemy battalion breakdown & key guide
│   │   │   └── TelemetryHud.razor        # Real-time engine telemetry strip
│   │   └── Inspector/           # Stage & CHR Inspector Sub-Components
│   │       ├── StageArenaTab.razor         # 35-Stage map canvas & Recon UI
│   │       ├── ChrTileCatalogTab.razor     # 512 CHR Sheet & Tank metasprites UI
│   │       └── PowerUpsSpecialsTab.razor   # Power-ups & Specials metasprites UI
│   ├── Helpers/
│   │   └── GameUiHelper.cs      # Reusable UI styling for difficulty & armor badges
│   ├── Engine/                  # C# Game Core Layer (Brain)
│   │   ├── Core/                # Physics, DestructibleMap, EnemySystem, PowerUpSystem, Bullets, Engine
│   │   └── Models/              # PlayerTank, EnemyTank, PowerUp, GameEntities, RenderFrameDto, InputState
│   ├── Layout/
│   │   └── MainLayout.razor     # Retro arcade layout, navbar & Dev Tools menu
│   ├── Pages/
│   │   ├── Home.razor           # Game Launch Hub (1P stage picker + 2P Co-op)
│   │   ├── Arcade.razor         # Focused Single-Player & Co-Op Arena
│   │   ├── Coop.razor           # Online Co-Op Matchmaking & Lobby
│   │   ├── DevPortal.razor      # Central Developer Suite (/dev)
│   │   ├── Sandbox.razor        # Engine Sandbox & Physics Lab (/sandbox)
│   │   ├── SoundBgm.razor       # Sound & BGM Synthesizer
│   │   └── StageInspector.razor # Stage & CHR Inspector
│   ├── Services/
│   │   ├── GameEngineService.cs # Blazor JS Interop & Session lifecycle service
│   │   ├── GameStorageService.cs# LocalStorage persistence service (HI-Score, Max Stage, Audio)
│   │   └── StageService.cs      # Stage JSON loader with in-memory caching
│   ├── wwwroot/
│   │   ├── app.css              # Pixel font, responsive layout & retro styling
│   │   ├── assets/sprites/      # CHR tile sheets (chr_all.png)
│   │   ├── data/stages/         # 35 individual stage JSONs + manifest.json
│   │   └── js/
│   │       ├── game-bundle.js   # Single Source of Truth JS module bundler
│   │       ├── nes-chr.js       # Core NES CHR tile & palette decoding engine
│   │       ├── nes-synth.js     # NES APU sound synthesizer engine
│   │       ├── stage-renderer.js# Fast 60 FPS In-Game Canvas 2D Blitter
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
