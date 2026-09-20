# RetroTank 1985 (Battle City NES Remake)

> **Disclaimer & Fair Use Notice**:
> This project is a non-commercial, technical remake built purely for **educational, reverse-engineering, and archival purposes**.
> The original *Battle City* (1985) game intellectual property (IP), characters, original audio compositions, and brand belong to **Bandai Namco Entertainment Inc.** (formerly Namco).

---

## 🎯 Overview

**RetroTank 1985** is a modern, modular remake of the legendary 1985 NES arcade game *Battle City*, built using **.NET 9 Blazor WebAssembly (WASM)** and styled with **MudBlazor** and custom retro aesthetics.

The project demonstrates:
- **Dual-Mode Execution**: Runs as a full-stack **ASP.NET Core Blazor Web App** during local development, and deploys as a pure **Standalone WebAssembly SPA** for static cloud hosting (e.g. Cloudflare Pages, GitHub Pages).
- **NES APU Audio Synthesis**: Emulation of Ricoh 2A03 hardware (Pulse 1, Pulse 2, Triangle, and Noise channels) in Web Audio API without relying on pre-recorded audio files.
- **ROM-Accurate Assets**: Native extraction of 8-bit NES CHR tiles, palettes (`$D44A`), and authentic game sprites (including player tanks, enemies, eagle base, and powerups).
- **Automated Deployment**: One-click build script generating optimized WASM bundles, Cloudflare Pages headers (`_headers`, `_redirects`), and zip release packages.

---

## 🛠 Tech Stack

- **Framework**: .NET 9.0 (Blazor WebAssembly with AOT / Linking support)
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

Open your browser at the local URL (e.g., `https://localhost:7xxx` or `http://localhost:5xxx`).

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

### 2. NES Sound & BGM Synthesizer (`/sound-bgm`)
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
| **Game Over BGM** | Pulse 1 | Chromatic step-down game over sequence |

### 3. Stage, CHR & Item Inspector (`/stage-inspector`)
- **35 Stages Map Viewer**: Decoded from ROM `$F07A` and saved into modular JSON files (`data/stages/stage_01.json` ... `stage_35.json`).
- **Authentic Eagle Fortification**: Strict ROM `EAGLE_WALL` 8px sub-tile Π-wall geometry.
- **Enemy Intelligence Recon**: 20-tank spawn breakdown per stage across 4 tiers (Basic, Fast, Power, Armor) based on ROM `$E4EC` & `$E578`.
- **CHR Tile & Sprite Catalog**: Real-time rendering of all 512 8×8 tiles (`chr_all.png`) with palette switching (BG0–BG3, SP0–SP3) and 4-way metasprite tank previews.
- **Power-ups & Specials Gallery**: Interactive preview of all 6 classic droppable items (Helmet, Timer, Shovel, Star, Grenade, 1-UP) + Phoenix HQ status (Intact/Destroyed) and Force Shield with instant SFX testing.

### 4. Upcoming Modules (Phase 2-5)
- **Phase 2: 60 FPS Game Loop & Tank Controller (`/play`)**: Tank physics, WASM tick loop, and mobile touch D-Pad.
- **Phase 3: Collision & Destruction**: 4×4 sub-tile brick damage and steel ricochets.
- **Phase 4: Enemy AI & Spawning**: AI targeting, flashing tanks, and power-up drops.
- **Phase 5: Game Polish & Construction Mode**: Custom stage builder and score tally screen.

---

## 📁 Project Structure

```
RetroTank1985/
├── publish-wasm.bat             # Automated Release script (Cloudflare Pages + Zip)
├── RetroTank1985.slnx           # Modern .NET Solution File
│
├── RetroTank1985/               # Server host project (Blazor Web App for local dev)
│   ├── Components/
│   │   ├── App.razor            # Root HTML template for Server Host mode
│   │   └── Routes.razor
│   └── Program.cs               # Host configuration & MapStaticAssets
│
├── RetroTank1985.Client/        # Pure WebAssembly Client (Runs locally & in Cloudflare)
│   ├── Layout/
│   │   └── MainLayout.razor     # Retro arcade layout & MudBlazor theme
│   ├── Models/
│   │   └── StageModel.cs        # Stage, Tile, and Enemy data models
│   ├── Pages/
│   │   ├── Home.razor           # Navigation Hub
│   │   ├── SoundBgm.razor       # Sound & BGM test bench
│   │   └── StageInspector.razor # 35-stage map & CHR tile inspector
│   ├── Services/
│   │   └── StageService.cs      # Stage JSON loader with in-memory caching
│   ├── wwwroot/
│   │   ├── app.css              # Pixel font and CRT styling
│   │   ├── favicon.png          # 64x64 ROM-extracted retro tank icon
│   │   ├── assets/sprites/      # CHR tile sheets (chr_all.png)
│   │   ├── data/stages/         # 35 individual stage JSONs + manifest.json
│   │   ├── index.html           # Standalone entry point for Cloudflare Pages
│   │   └── js/
│   │       ├── interop.js       # Runtime environment detector
│   │       ├── nes-synth.js     # NES APU sound synthesizer engine
│   │       └── stage-renderer.js# Stage canvas & CHR sprite rendering engine
│   ├── Program.cs               # Dynamic client bootstrapper
│   └── RetroTank1985.Client.csproj
│
└── ../publish/Wasm/             # Output folder for Cloudflare release (and zip archives)
```

---

## 📜 License & Acknowledgements

- Built for educational study of 1980s video game architecture, audio synthesis, and WebAssembly integration.
- Battle City © 1985 Bandai Namco Entertainment Inc. All rights reserved.
