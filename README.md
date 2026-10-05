# open.mp Launcher with ModSync Integration

Advanced multiplayer launcher for open.mp and SA-MP with automated client asset synchronization, content expansion pipelines, streaming memory optimization, and vehicle animation pool expansion.

## Overview

This repository is a specialized fork of the official open.mp launcher featuring native ModSync synchronization. It enables server operators to distribute high-capacity modpacks, custom vehicles, weapons, skins, audio, and scripts directly to connecting clients without overriding or corrupting base game files.

- Client Target: 0.4.0 - R1
- Server Compatibility: open.mp 1.5.9.0
- Development and Architecture: eLdarqO

---

## Core Capabilities

### 1. Dual Pipeline: Content Replacement & Pure Expansion
ModSync supports two operational models simultaneously:
- **Asset Replacement**: Drop-in replacements for vanilla vehicles, weapons, skins, textures, and map objects via ModLoader.
- **Content Expansion (No Vanilla Overwrite)**:
  - Integration with the open.mp artwork and custom model pipeline.
  - Adding brand-new standalone vehicles with custom handling and dummy hierarchies without replacing original GTA vehicles.
  - Adding brand-new custom weapons (models, textures, collision, sound effects) and props (e.g. flags, melee weapons) without ID conflicts or missing-model placeholder indicators.
  - Adding custom player skins and world objects through dedicated server model identifiers.

### 2. Universal CLEO & Script Support
- Seamless execution of classic CLEO scripts (`.cs`), CLEO Redux scripts (`.js`), CLEO plugins, configuration files (`.ini`), and audio assets (`.wav`, `.mp3`).
- Automatic dependency categorization and target directory routing.

### 3. Server-Specific Mod Isolation
- Client downloads are organized into server-specific sandboxes (`modloader/servers/<server_id>/`).
- Prevents cross-server file collisions, texture corruptions, and configuration overwrites when playing on multiple servers.
- Automatic cache validation using cryptographic SHA-256 hashes to ensure only missing or updated assets are downloaded.

### 4. Vehicle Animation Pool Expansion (8000 Nodes)
- Base GTA San Andreas allocates a fixed pool of only 900 nodes for the `CMatrixLinkList` array. High-polygon vehicle models with detailed animated dummies (doors, bonnets, boots, steering components, wheels) rapidly exhaust this buffer, causing animations to freeze or fail.
- `modsync_hook.asi` synchronously patches the matrix pool allocation to 8000 nodes inside `DllMain` before `CGame::Initialise` runs, ensuring smooth, non-freezing vehicle component animations.

### 5. 2048 MB Dynamic Streaming Memory Budget
- Eliminates model flickering, missing geometries, and invisible vehicle parts when loading HD asset packs.
- **Budget Address Fix**: Directly targets `0x008A5A80` (`CStreaming::ms_memoryAvailable`), correcting the legacy pitfall of writing to `0x008E4CB4` (which is the used memory accumulator, leading to false out-of-memory unloading loops).
- Configures `stream.ini` to `memory 2097152` (2048 MB) and patches `CStreaming::InitFileList` default limits (`0x005B8E6A` to 2047 MB and `0x005B8E74` to 96 vehicle models).

### 6. Large Address Aware (LAA 0x0020) In-Place Patching
- Inspects `gta_sa.exe` and sets the `IMAGE_FILE_LARGE_ADDRESS_AWARE` PE characteristic (`0x0020`) using pure managed byte manipulation.
- Allows the 32-bit game process to utilize the full 4 GB virtual address space on 64-bit Windows without memory allocation crashes.

### 7. Antivirus False-Positive Remediation
- **Lazy IPC Initialization**: Defers inter-process communication listener binding until game launch, preventing false-positive heuristics from flagging the application during idle operation.
- **Zero Suspicious Dropper Patterns**: Avoids aggressive runtime memory injectors, obfuscators, and temporary batch droppers. Uses standard Windows API calls, direct stream I/O for PE verification, and delegates ASI loading to the established game loader pipeline (`vorbisFile.dll` / ASI loader).
- **Self-Contained Single-File Execution**: The .NET client launcher is compiled as a self-contained, signed single-file binary with zero external runtime dependencies.

---

## Architecture & Repository Structure

```
launcher-MODS-SYNC-/
├── client/
│   ├── ModSyncClient.slnx              # Visual Studio / .NET solution
│   ├── ModSyncLauncher/                # Standalone .NET 8 launcher & CLI sync engine
│   │   ├── src/
│   │   │   ├── Core/                   # Constants (0x008A5A80), manifests, models
│   │   │   ├── FileSystem/             # IMG synchronizer, LAA patcher, stream config
│   │   │   ├── Network/                # Async CDN downloader with SHA-256 checks
│   │   │   ├── Sync/                   # Sandbox isolation, CLEO routing, backup engine
│   │   │   ├── UI/                     # Professional zero-emoji console UI
│   │   │   └── Security/               # Cryptographic integrity verifier
│   │   ├── Program.cs                  # CLI entry point (--sync-only, --auto-launch, --clean)
│   │   └── ModSyncLauncher.csproj      # .NET 8 Single-File configuration
│   ├── ModSyncLauncher.Tests/          # Automated xUnit test suite (16 tests)
│   │   ├── ModSyncTests.cs             # Manifest, hashing, LAA, address validation
│   │   └── ModSyncLauncher.Tests.csproj
│   ├── hooks/                          # Native C++ GTA SA ASI Hook
│   │   ├── include/                    # Memory offsets and hook headers
│   │   ├── src/                        # Matrix pool expansion & streaming patches
│   │   ├── CMakeLists.txt              # CMake x86 C++20 build definition
│   │   ├── build.bat                   # One-click build script
│   │   └── modsync_hook.asi            # Compiled native hook binary
│   └── distribution/                   # Ready-to-deploy release package
│       ├── ModSyncLauncher.exe         # Self-contained single-file executable
│       ├── install_mod_sync.bat        # Automated one-click game installer
│       ├── play_openmp.bat             # Direct game launcher script
│       ├── launcher_config.json        # Client configuration
│       └── mod_framework/              # ModLoader & CLEO template with 2048MB stream.ini
├── src/                                # React / TypeScript frontend
│   ├── components/                     # Modular UI components
│   ├── containers/                     # Modals, join prompts, settings
│   ├── states/                         # Zustand state stores
│   └── utils/                          # Game launcher, query, and modsync hooks
├── src-tauri/                          # Rust backend
│   ├── src/
│   │   ├── modsync.rs                  # Native LAA PE patcher & stream.ini manager
│   │   ├── commands.rs                 # Exposed ensure_modsync command
│   │   ├── injector.rs                 # Automatic pre-launch optimization hook
│   │   └── main.rs                     # Tauri application initialization
│   └── extra/modsync/                  # Bundled modsync_hook.asi, stream.ini, configs
├── package.json                        # NPM build scripts
└── README.md                           # Documentation
```

---

## Build Instructions

### Standalone Client Engine (.NET 8)

Build the complete solution:
```bash
dotnet build client/ModSyncClient.slnx -c Release
```

Execute automated unit tests:
```bash
dotnet test client/ModSyncLauncher.Tests/ModSyncLauncher.Tests.csproj
# Result: 16 passed, 0 failed
```

Publish self-contained single-file binary:
```bash
dotnet publish client/ModSyncLauncher/ModSyncLauncher.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o client/distribution
```

### Native ASI Hook (C++20)

Build using CMake and Visual Studio (Win32 / x86):
```cmd
cd client\hooks
build.bat
```

### Desktop Launcher (Tauri + React)

Install dependencies:
```bash
npm install
```

Run in development mode:
```bash
npm run dev
```

Build release installer:
```bash
npm run release
```

---

## CLI Usage

The standalone engine can be executed directly or scripted via command-line arguments:

```bash
# Synchronize assets and automatically launch the game
ModSyncLauncher.exe --auto-launch

# Synchronize assets without launching
ModSyncLauncher.exe --sync-only

# Verify file integrity and restore vanilla gta3.img backup
ModSyncLauncher.exe --clean

# Deactivate synchronization and disable server mod packs
ModSyncLauncher.exe --deactivate
```
