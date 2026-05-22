# ArkDuckBot Desktop

A desktop companion application for **ARK: Survival Ascended** servers, providing real-time map tracking, AI chat integration, and server management features.

## Overview

ArkDuckBot is a WPF desktop application that connects to ARK: Survival Ascended servers running the **DuckBot** mod. It provides:

- **Real-time Map Tracking** - Monitor players, dinosaurs, and events on ARK maps
- **AI Chat Integration** - Connect to Sheldon AI-powered chat via the DuckBot MCP bridge
- **Server Pairing** - Connect to servers using `arkduckbot://` protocol links
- **Player Tracking** - Monitor online players, tribe info, and server status
- **System Tray** - Background operation with tray icon and notifications

## Connection Architecture

```
┌──────────────────────────────────────────────────────────┐
│                   ArkDuckBot Desktop (WPF)               │
├──────────────────────────────────────────────────────────┤
│  ArkApiClient ←──────→ ARK Server (ServerAPI Plugin)    │
│  McpBridgeClient ←───→ DuckBot MCP Bridge (Python)       │
│                          ↓                               │
│                       LLM Provider (OpenRouter/Claude)   │
└──────────────────────────────────────────────────────────┘
```

## Requirements

- .NET 8.0 Runtime (Windows)
- ARK: Survival Ascended server with [DuckBot mod](https://github.com/Franzferdinan51/DuckBot-For-ark)
- WindowsGSM with [ArkSAwithServerAPI](https://github.com/ohmcodes/WindowsGSM.ArkSAwithServerAPI)

## Installation

1. Download the latest `ArkDuckBot-Setup.exe` from [Releases](https://github.com/Franzferdinan51/Ark-DuckBot-Desktop/releases)
2. Run the installer
3. Launch ArkDuckBot

## Server Setup

### 1. Install DuckBot Mod

Follow the instructions at [DuckBot-For-ark](https://github.com/Franzferdinan51/DuckBot-For-ark)

### 2. Install WindowsGSM + ArkSAwithServerAPI

Follow the instructions at [WindowsGSM.ArkSAwithServerAPI](https://github.com/ohmcodes/WindowsGSM.ArkSAwithServerAPI)

### 3. Pair with Server

Use the in-game pairing command or use an `arkduckbot://` link:
```
arkduckbot://SERVER_IP:SERVER_PORT
```

## Features

### Map Tracking
- Real-time player positions on ARK maps
- Dinosaur spawn tracking
- Supply drop and event notifications
- Tribe territory markers

### AI Chat (Sheldon AI)
- Natural language AI assistant
- Player lookups (dinosaurs, items, engrams)
- Admin commands (spawn, give items, teleport)
- Permission-tier-based tool access

### DuckBot Commands
The app integrates with DuckBot's 39+ chat commands:
- **Economy**: `/bal`, `/pay`, `/daily`, `/work`, `/coinflip`
- **Teleport**: `/home`, `/sethome`, `/tpr`, `/tpaccept`, `/warp`
- **Tribe**: `/tribe`, `/tdinos`, `/tribealert`, `/marker`
- **Moderation**: `/kick`, `/ban`, `/unban`, `/mute`, `/slay`
- **Kits**: `/kits`, `/kit`
- **AI**: `/aibrain`, `/aireset`

## Configuration

Settings are stored in `%APPDATA%\ArkDuckBot\`

### Key Settings
- `mcp_host` / `mcp_port` - DuckBot MCP Bridge connection
- `announce_*` - Notification preferences
- `auto_start` - Launch on Windows startup
- `minimize_to_tray` - Minimize to system tray

## Building from Source

```bash
# Clone the repository
git clone https://github.com/Franzferdinan51/Ark-DuckBot-Desktop.git

# Navigate to project
cd Ark-DuckBot-Desktop/ArkDuckBotDesktop

# Restore and build
dotnet restore
dotnet build

# Run
dotnet run
```

## Tech Stack

| Component | Technology |
|-----------|------------|
| Framework | .NET 8.0 WPF |
| UI Library | WPF-UI (Fluent Design) |
| WebView | Microsoft WebView2 |
| Architecture | MVVM |
| API Client | WebSocket (ArkApiClient) |
| AI Bridge | WebSocket (McpBridgeClient) |

## Related Projects

- [DuckBot-For-ark](https://github.com/Franzferdinan51/DuckBot-For-ark) - ARK mod + Python MCP bridge
- [sheldon-ai-for-ark](https://github.com/ArkAscendedAI/sheldon-ai-for-ark) - AI integration for ARK
- [WindowsGSM.ArkSAwithServerAPI](https://github.com/ohmcodes/WindowsGSM.ArkSAwithServerAPI) - Server management
- [rustplus-desktop](https://github.com/Pronwan/rustplus-desktop) - Original Rust+ desktop app (this project is based on)

## License

GPL-3.0 License - See LICENSE file for details

## Authors

- **Franz Ferdinand** - [GitHub](https://github.com/Franzferdinan51)
- **OpenClaude** - Development assistance

## Acknowledgments

- Original project: [Pronwan/rustplus-desktop](https://github.com/Pronwan/rustplus-desktop)
- DuckBot architecture: [sheldon-mcp-bridge](https://github.com/nicholaslim99/sheldon-mcp-bridge)
- Sheldon AI: [sheldon-ai-for-ark](https://github.com/ArkAscendedAI/sheldon-ai-for-ark)