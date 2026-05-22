# ArkDuckBot Desktop

A professional desktop companion application for **ARK: Survival Ascended** servers, providing real-time map tracking, AI-powered chat via DuckBot, and comprehensive server management.

## Overview

ArkDuckBot Desktop connects to ARK: Survival Ascended servers running the **DuckBot mod**. It serves as the primary interface for players and administrators to interact with the server through an intuitive desktop experience.

### Core Capabilities

- **Real-time Map Tracking** - Monitor players, dinosaurs, and events on ARK maps with live updates
- **AI Chat Integration** - Connect to DuckBot AI via MCP Bridge for natural language commands
- **Server Pairing** - Connect using `arkduckbot://` protocol or manual configuration
- **Player Tracking** - Monitor online players, tribe info, and server status in real-time
- **System Tray** - Background operation with tray icon, notifications, and quick actions
- **39+ Chat Commands** - Full access to DuckBot's command system for economy, teleportation, tribe management, and more

---

## Connection Architecture

```
┌──────────────────────────────────────────────────────────────────┐
│                    ArkDuckBot Desktop (WPF)                       │
│  ┌──────────────┐    ┌──────────────────┐    ┌────────────────┐  │
│  │ ArkApiClient │    │ McpBridgeClient  │    │   WebView2 UI  │  │
│  │ (ServerAPI)  │    │ (AI Bridge :8444)│    │   (React/JS)  │  │
│  └──────┬───────┘    └────────┬─────────┘    └────────────────┘  │
└─────────┼─────────────────────┼─────────────────────────────────┘
          │ Port 27020 (default)│ Port 8444 (desktop companion)
          ▼                     ▼
┌─────────────────────┐   ┌─────────────────────────────────────┐
│ ARK: Survival Ascended│   │  DuckBot MCP Bridge (Python)       │
│ Server              │   │  ┌───────────────────────────────┐  │
│ ┌─────────────────┐ │   │  │ DuckBot AI Agent Loop         │  │
│ │ServerAPI Plugin │ │   │  │ - Intent Recognition          │  │
│ │(C++)            │ │   │  │ - Tool Routing               │  │
│ │                 │ │   │  │ - LLM Orchestration           │  │
│ │• Game Events    │ │   │  └───────────────────────────────┘  │
│ │• Chat Commands  │ │   │                    │                  │
│ │• Player Data    │ │   │                    ▼                  │
│ │• Map Tracking   │ │   │  ┌───────────────────────────────┐  │
│ └─────────────────┘ │   │  │ LLM Provider (Configurable)  │  │
└─────────────────────┘   │  │ OpenRouter / Anthropic / OAI  │  │
                           │  └───────────────────────────────┘  │
                           └─────────────────────────────────────┘
```

### Dual Connection System

| Connection | Port | Purpose |
|------------|------|---------|
| **ARK ServerAPI** | 27020 | Game data, player tracking, map events |
| **MCP Bridge** | 8444 (desktop) / 8443 (in-game) | AI chat, natural language commands |

---

## DuckBot AI Features

Natural language interface powered by DuckBot AI:

```
Player: "Can you spawn me a Rex level 200?"
DuckBot: "Sure! Spawning level 200 Rex..."
Tool: spawn_dino → spcdino 2 200
Result: Rex spawned successfully!
```

**Capabilities:**
- Natural language command recognition (Query, Command, Action, Help, Chat intents)
- Context-aware conversation history with per-player sessions
- Permission-aware responses (Admin gets elevated access)
- 25+ ARK-specific tools organized by permission tier

### Permission Tiers

| Tier | Level | Capabilities |
|------|-------|-------------|
| **Player** | 0 | Basic commands, economy, teleport requests |
| **VIP** | 1 | Extended kits, faster cooldowns, priority queue |
| **Mod** | 2 | Kick, mute, warn, event management |
| **Admin** | 3 | Ban, unban, spawn commands, kit management |

### DuckBot Commands (39+)

| Category | Commands | Description |
|----------|----------|-------------|
| **Economy** | `/bal`, `/pay`, `/daily`, `/work`, `/coinflip` | Player economy system |
| **Teleport** | `/home`, `/sethome`, `/tpr`, `/tpaccept`, `/warp`, `/rtp` | Teleportation system |
| **Tribe** | `/tribe`, `/tdinos`, `/tribealert`, `/marker` | Tribe management |
| **Moderation** | `/kick`, `/ban`, `/unban`, `/mute`, `/slay`, `/warn` | Admin tools |
| **Kits** | `/kits`, `/kit`, `/kitcooldown` | Custom kit system |
| **Events** | `/events`, `/event`, `/drop`, `/announce` | Event management |
| **AI** | `/aibrain`, `/aireset`, `/aimode` | AI configuration |

---

## Features in Detail

### Real-time Map Tracking

- **Player Positions** - See all online players with name tags
- **Dinosaur Tracking** - Monitor tamed dinos, levels, and locations
- **Event Markers** - Supply drops, cave entrances, custom markers
- **Tribe Territories** - Grid-based territory visualization
- **Interactive Controls** - Zoom, pan, click-to-teleport

### AI Chat via DuckBot

Natural language commands without typing `/`:

- "spawn me a Rex level 200" → spawns dino
- "what's my balance" → shows economy
- "teleport to player X" → sends tpr
- "enable wild dino alerts" → activates skill

### Skills System

Event-driven behaviors powered by AI:

| Skill | Tier | Trigger | Action |
|-------|------|---------|--------|
| **wild_dino_alert** | VIP | `wild_dino_alert` | Notifies when dangerous wild dino detected |
| **auto_slay_dangerous** | Admin | `high_level_dino_detected` | Auto-slays level 150+ dangerous dinos |
| **player_join_welcome** | Player | `player_joined` | Welcomes new players |

### Knowledge Base

Built-in encyclopedia for ARK: Survival Ascended:

- **Dinosaurs** - Rex, Giganotosaurus, Megalodon, Argentavis, Therizinosaurus, Yutyrannus, Spinosaurus
- **Items** - Kibble, Raw Meat, Cooked Meat, Metal Ingot, Gunpowder
- **Taming Data** - Methods, speeds, effectiveness
- **Fuzzy Search** - Nickname support (rex, giga, argy, yuty, etc.)

---

## Installation

### Prerequisites

- **Windows 10/11**
- **.NET 8.0 SDK** - [Download](https://dotnet.microsoft.com/download/dotnet/8.0)
- **ARK: Survival Ascended** server with DuckBot mod installed
- **DuckBot MCP Bridge** (Python 3.10+) running for AI features

### Build from Source

```bash
git clone https://github.com/Franzferdinan51/Ark-DuckBot-Desktop.git
cd Ark-DuckBot-Desktop/ArkDuckBotDesktop
dotnet restore
dotnet build
dotnet run
```

### Publish Release

```bash
dotnet publish -c Release --self-contained
```

### First Run Setup

1. Launch ArkDuckBot Desktop
2. Go to **Settings** → **Connection**
3. Configure:
   - **ARK Server IP**: Your server's IP address
   - **ARK Server Port**: Default `27020`
   - **MCP Bridge URL**: `ws://localhost:8444`
   - **MCP Secret**: Your configured shared secret
4. Click **Connect** to verify connections
5. Configure notification preferences in **Settings** → **Notifications**

---

## Configuration

Configuration stored in `%APPDATA%\ArkDuckBot\`:

```json
{
  "ark_host": "192.168.1.100",
  "ark_port": 27020,
  "mcp_host": "localhost",
  "mcp_port": 8443,
  "admin_port": 8444,
  "mcp_secret": "your-secret-key",
  "announce_player_join": true,
  "announce_drops": true,
  "minimize_to_tray": true,
  "theme": "dark"
}
```

---

## Tech Stack

| Component | Technology |
|-----------|------------|
| **Framework** | .NET 8.0 WPF |
| **UI Library** | WPF-UI (Fluent Design) |
| **WebView** | Microsoft WebView2 |
| **Architecture** | MVVM |
| **API Client** | WebSocket (System.Net.WebSockets) |
| **AI Bridge** | WebSocket (Python asyncio) |

---

## Related Projects

| Project | Description |
|---------|-------------|
| [DuckBot-For-ark](https://github.com/Franzferdinan51/DuckBot-For-ark) | ServerAPI C++ plugin + Python MCP Bridge for ARK: Survival Ascended |
| [WindowsGSM.ArkSAwithServerAPI](https://github.com/ohmcodes/WindowsGSM.ArkSAwithServerAPI) | WindowsGSM plugin for ARK SA with ServerAPI |
| [sheldon-ai-for-ark](https://github.com/ArkAscendedAI/sheldon-ai-for-ark) | AI integration framework inspiration |
| [rustplus-desktop](https://github.com/Pronwan/rustplus-desktop) | Original Rust+ companion app (inspiration) |

---

## License

GPL-3.0 License - See [LICENSE](LICENSE) file for details

## Authors

- **Franz Ferdinand** - [GitHub](https://github.com/Franzferdinan51)
- **OpenClaude** - Development assistance

## Acknowledgments

- Original project inspiration: [Pronwan/rustplus-desktop](https://github.com/Pronwan/rustplus-desktop)
- DuckBot architecture: [sheldon-mcp-bridge](https://github.com/nicholaslim99/sheldon-mcp-bridge)
- ARK AsaApi: Community ServerAPI project