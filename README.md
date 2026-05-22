# ArkDuckBot Desktop

A desktop companion application for **ARK: Survival Ascended** servers, providing real-time map tracking, AI chat integration via DuckBot, and server management features.

## Overview

ArkDuckBot Desktop is a WPF desktop application that connects to ARK: Survival Ascended servers running the **DuckBot mod**. It provides:

- **Real-time Map Tracking** - Monitor players, dinosaurs, and events on ARK maps
- **AI Chat Integration** - Connect to DuckBot AI-powered chat via the MCP Bridge
- **Server Pairing** - Connect to servers using `arkduckbot://` protocol links
- **Player Tracking** - Monitor online players, tribe info, and server status
- **System Tray** - Background operation with tray icon and notifications

## Connection Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                ArkDuckBot Desktop (WPF App)                     │
├─────────────────────────────────────────────────────────────────┤
│  ArkApiClient ←──────→ ARK Server (DuckBot ServerAPI Plugin)     │
│  McpBridgeClient ←──→ DuckBot MCP Bridge (Python)               │
│                          ↓                                      │
│                       LLM Provider (OpenRouter/Claude/Gemini)   │
└─────────────────────────────────────────────────────────────────┘
```

### How It Works

1. **DuckBot ServerAPI Plugin** (C++): Runs on the ARK server, hooks into game events, provides chat commands
2. **MCP Bridge** (Python): AI brain that processes natural language, runs the agentic loop with LLM providers
3. **ArkDuckBot Desktop**: Desktop client that connects to both the ServerAPI (for game data) and MCP Bridge (for AI chat)

## Requirements

- .NET 8.0 Runtime (Windows)
- ARK: Survival Ascended server with DuckBot mod installed
- DuckBot MCP Bridge running (for AI features)

## Installation

### 1. Server Setup: Install DuckBot Mod

Follow the guide at [DuckBot-For-ark](https://github.com/Franzferdinan51/DuckBot-For-ark/tree/main/mod):

```bash
# 1. Install Visual Studio 2022 with C++ desktop workload
# 2. Clone AsaApi as a sibling directory
# 3. Build DuckBot.sln (Release x64)
# 4. Copy Binaries/Release/DuckBot.dll to ArkApi/Plugins/DuckBot/
```

### 2. Server Setup: Install MCP Bridge

Follow the guide at [DuckBot-For-ark/mcp-bridge](https://github.com/Franzferdinan51/DuckBot-For-ark/tree/main/mcp-bridge):

```bash
cd mcp-bridge
pip install -e .
sheldon-bridge run
```

The bridge default port is `8443`. Configure your LLM provider in `config.json`.

### 3. Desktop App Setup

Download `ArkDuckBot-Setup.exe` from [Releases](https://github.com/Franzferdinan51/Ark-DuckBot-Desktop/releases) or build from source:

```bash
git clone https://github.com/Franzferdinan51/Ark-DuckBot-Desktop.git
cd Ark-DuckBot-Desktop/ArkDuckBotDesktop
dotnet restore
dotnet build
dotnet run
```

### 4. Connect to Server

Use the in-game pairing command or use an `arkduckbot://` link:
```
arkduckbot://SERVER_IP:SERVER_PORT
```

In the desktop app settings, configure:
- **ARK Server API**: Host/port of your ARK server (default: 27020)
- **MCP Bridge**: Host/port of your MCP Bridge (default: localhost:8443)
- **Shared Secret**: The auth token configured in your MCP Bridge

## DuckBot Commands

The app integrates with DuckBot's 39+ chat commands:

| Category | Commands | Description |
|----------|----------|-------------|
| **Economy** | `/bal`, `/pay`, `/daily`, `/work`, `/coinflip` | Player economy system |
| **Teleport** | `/home`, `/sethome`, `/tpr`, `/tpaccept`, `/warp` | Teleportation commands |
| **Tribe** | `/tribe`, `/tdinos`, `/tribealert`, `/marker`, `/gridmap` | Tribe management |
| **Moderation** | `/kick`, `/ban`, `/unban`, `/mute`, `/unmute`, `/slay` | Admin tools |
| **Kits** | `/kits`, `/kit` | Kit system |
| **Events** | `/events`, `/event`, `/drop` | Event notifications |
| **AI** | `/aibrain`, `/aireset` | AI chat integration |

## AI Chat (Sheldon AI)

The AI chat panel connects to your configured MCP Bridge and LLM provider. It supports:

- **Natural Language Commands**: "Spawn me a Rex level 150"
- **Permission Tiers**: user/vip/mod/admin with different access levels
- **Tool Access**: Spawn dinos, teleport, give items, manage tribe
- **Multi-LLM**: OpenRouter, Claude, GPT-4, Gemini, and local options

## Configuration

Settings are stored in `%APPDATA%\ArkDuckBot\`

### Key Settings
- `mcp_host` / `mcp_port` - DuckBot MCP Bridge connection
- `mcp_secret` - Shared secret for MCP Bridge authentication
- `ark_host` / `ark_port` - ARK Server API connection
- `announce_*` - Notification preferences
- `auto_start` - Launch on Windows startup
- `minimize_to_tray` - Minimize to system tray

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