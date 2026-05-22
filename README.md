# ArkDuckBot Desktop

A professional desktop companion application for **ARK: Survival Ascended** servers, providing real-time map tracking, AI-powered chat integration via DuckBot, and comprehensive server management features.

## Overview

ArkDuckBot Desktop is a WPF desktop application that connects to ARK: Survival Ascended servers running the **DuckBot mod**. It serves as the primary interface for players and administrators to interact with the server through an intuitive desktop experience.

### Core Capabilities

- **Real-time Map Tracking** - Monitor players, dinosaurs, and events on ARK maps with live updates
- **AI Chat Integration** - Connect to DuckBot AI-powered chat via the MCP Bridge ("Sheldon AI")
- **Server Pairing** - Connect to servers using `arkduckbot://` protocol links or manual configuration
- **Player Tracking** - Monitor online players, tribe info, and server status in real-time
- **System Tray** - Background operation with tray icon, desktop notifications, and quick actions
- **39+ Chat Commands** - Full access to DuckBot's command system for economy, teleportation, tribe management, and more

---

## Connection Architecture

ArkDuckBot Desktop operates as a dual-client application, maintaining connections to both the game server and the AI bridge simultaneously.

```
┌──────────────────────────────────────────────────────────────────────────────┐
│                           ArkDuckBot Desktop (WPF)                           │
│  ┌─────────────────────┐    ┌─────────────────────┐    ┌──────────────────┐  │
│  │   ArkApiClient      │    │   McpBridgeClient   │    │   WebView2 UI    │  │
│  │   (WebSocket)       │    │   (WebSocket)       │    │   (React/JS)     │  │
│  └──────────┬──────────┘    └──────────┬──────────┘    └──────────────────┘  │
└─────────────┼──────────────────────────┼──────────────────────────────────────┘
              │                          │
              │ Port 27020 (default)     │ Port 8443 (default)
              │                          │
              ▼                          ▼
┌─────────────────────────┐    ┌─────────────────────────────────────────────┐
│  ARK Server             │    │  DuckBot MCP Bridge (Python)                │
│  ┌───────────────────┐  │    │  ┌─────────────────────────────────────────┐│
│  │ DuckBot ServerAPI │  │    │  │ Sheldon AI Agent Loop                    ││
│  │ Plugin (C++)      │  │    │  │ - Intent Recognition                     ││
│  │                   │  │    │  │ - Tool Routing                          ││
│  │ • Game Events      │  │    │  │ - LLM Orchestration                     ││
│  │ • Chat Commands    │  │    │  │ - Response Generation                   ││
│  │ • Player Data      │  │    │  └─────────────────────────────────────────┘│
│  │ • Map Tracking     │  │    │                      │                      │
│  └───────────────────┘  │    │                      ▼                      │
└─────────────────────────┘    │  ┌─────────────────────────────────────────┐│
                               │  │ LLM Provider (Configurable)              ││
                               │  │ • OpenRouter (default)                  ││
                               │  │ • Anthropic (Claude)                    ││
                               │  │ • OpenAI (GPT-4)                        ││
                               │  │ • Google (Gemini)                       ││
                               │  │ • LM Studio (local)                    ││
                               │  │ • Ollama (local)                        ││
                               │  └─────────────────────────────────────────┘│
                               └─────────────────────────────────────────────┘
```

### Component Details

| Component | Technology | Purpose |
|-----------|------------|---------|
| **DuckBot ServerAPI Plugin** | C++ (ARK AsaApi) | Server-side mod that hooks into game events, provides chat commands, player data, and map tracking |
| **MCP Bridge** | Python 3.10+ | AI brain that processes natural language, manages tool registry, and orchestrates LLM interactions |
| **ArkDuckBot Desktop** | .NET 8.0 WPF | Desktop client providing UI, notifications, and local caching |
| **LLM Providers** | External APIs | Natural language understanding and response generation |

---

## Features

### Real-time Map Tracking

The map panel provides live visualization of server activity:

- **Player Positions** - See all online players with name tags on the map
- **Dinosaur Tracking** - Monitor tamed dinos, their levels, and locations
- **Event Markers** - Visual indicators for supply drops, cave entrances, and custom markers
- ** Tribe Territories** - Grid-based tribe territory visualization
- **Interactive Controls** - Zoom, pan, and click-to-teleport functionality

### AI Chat via Sheldon AI

Natural language interface powered by the DuckBot MCP Bridge:

```
Player: "Can you spawn me a Rex level 200 with 500 health?"
Sheldon: "Sure! Spawning level 200 Rex with 500 extra health bonus..."
Tool Call: spawn_dino → spcdino 2 200 0 0 500
Result: Rex spawned successfully!
```

**Capabilities:**
- Natural language command execution
- Context-aware conversation history
- Permission-aware responses (admins get elevated access)
- Multi-turn dialog for complex requests

### DuckBot Commands (39+)

| Category | Commands | Description |
|----------|----------|-------------|
| **Economy** | `/bal`, `/pay`, `/daily`, `/work`, `/coinflip`, `/gamble`, `/rich` | Player economy system with balance tracking |
| **Teleport** | `/home`, `/sethome`, `/tpr`, `/tpaccept`, `/warp`, `/rtp`, `/配合` | Teleportation system with random teleport and配合 support |
| **Tribe** | `/tribe`, `/tdinos`, `/tribealert`, `/marker`, `/gridmap`, `/tribelog` | Tribe management and territory tools |
| **Moderation** | `/kick`, `/ban`, `/unban`, `/mute`, `/unmute`, `/slay`, `/warn` | Admin moderation tools |
| **Kits** | `/kits`, `/kit`, `/kitcooldown` | Custom kit system with cooldowns |
| **Events** | `/events`, `/event`, `/drop`, `/announce` | Event management and notifications |
| **AI** | `/aibrain`, `/aireset`, `/aimode` | AI chat configuration and reset |
| **Utility** | `/help`, `/ping`, `/stats`, `/top`, `/who` | General utility commands |

### Permission Tiers

The MCP Bridge enforces a role-based permission system:

| Tier | Level | Capabilities |
|------|-------|-------------|
| **Player** | 0 | Basic commands, economy, teleport requests |
| **VIP** | 1 | Extended kits, faster cooldowns, priority queue |
| **Mod** | 2 | Kick, mute, warn, event management |
| **Admin** | 3 | Ban, unban, spawn commands, kit management |
| **SuperAdmin** | 4 | Full access, config changes, bridge management |

### System Tray Integration

- **Minimize to Tray** - App runs in background when closed
- **Desktop Notifications** - Player join/leave, tribe alerts, drop notifications
- **Quick Actions** - Right-click menu for common operations
- **Status Indicator** - Connection status at a glance

---

## DuckBot MCP Bridge Integration

### Connection Settings

| Parameter | Default | Description |
|-----------|---------|-------------|
| **Host** | `localhost` | MCP Bridge server address |
| **Port** | `8443` | MCP Bridge WebSocket port |
| **Shared Secret** | (configured) | Authentication token for bridge access |

### Supported AI Providers

The MCP Bridge supports multiple LLM backends:

| Provider | Model Examples | Notes |
|----------|---------------|-------|
| **OpenRouter** | claude-3.5-sonnet, gpt-4o, gemini-pro | Default provider, aggregated access |
| **Anthropic** | claude-3-5-sonnet, claude-3-opus | Direct API access |
| **OpenAI** | gpt-4-turbo, gpt-3.5-turbo | Direct API access |
| **Google** | gemini-1.5-pro, gemini-1.5-flash | Direct API access |
| **LM Studio** | Any local GGUF model | Local inference |
| **Ollama** | llama3, mistral, codellama | Local inference |

### Tool Registry (25+ ARK-Specific Tools)

```
Player Tools:       get_player_info, get_player_inventory, get_player_stats
                    get_players_online, get_tribe_info, get_tribe_members

Teleport Tools:     teleport_player, teleport_to_player, teleport_to_coords
                    set_home, get_home, delete_home, random_teleport

Spawn Tools:        spawn_dino, spawn_item, spawn_group, spawn_egg
                    spawn_near_player, spawn_multiple

Admin Tools:        kick_player, ban_player, unban_player, mute_player
                    unmute_player, slay_player, announce

Tribe Tools:        create_marker, remove_marker, get_tribe_log
                    set_tribe_permissions, tribe_invite

Economy Tools:      get_balance, add_balance, remove_balance
                    transfer_coins, set_daily_reward

Event Tools:        create_drop, cancel_drop, list_drops
                    trigger_event, send_notification

AI Tools:           reset_conversation, set_ai_mode, get_ai_status
                    update_system_prompt
```

---

## Agent Features

Inspired by hermes-agent, OpenClaw, and sheldon-ai architectures, the MCP Bridge implements a professional agent system:

### Workspace System

The bridge uses a structured workspace for agent configuration:

```
workspace/
├── AGENTS.md       # Agent definitions, personalities, and behaviors
├── SOUL.md         # Core values, ethics, and response guidelines
├── TOOLS.md        # Tool registry documentation and usage patterns
├── SKILLS/         # Learned behaviors and custom commands
│   └── *.md        # Individual skill definitions
└── SESSIONS/       # Conversation history and context
```

### Skills System with Learning Loop

- **Skill Definition** - Markdown-based skill files with triggers, actions, and responses
- **Learning from Conversation** - Bot learns successful patterns from admin corrections
- **Skill Activation** - Context-aware skill matching based on conversation flow
- **Persistence** - Learned skills saved to disk and loaded on restart

### Session History

- **Full Conversation Logging** - Complete history with timestamps
- **Searchable Archive** - Query past conversations by keyword, user, or date
- **Context Window Management** - Smart summarization for long conversations
- **Export Functionality** - Export conversations for analysis or debugging

### Rate Limiting

Per-tier rate limits prevent abuse:

| Tier | Global Limit | Tool Limit | Burst |
|------|-------------|------------|-------|
| **Player** | 10/min | 5/min per tool | 3 |
| **VIP** | 20/min | 10/min per tool | 5 |
| **Mod** | 50/min | 20/min per tool | 10 |
| **Admin** | 100/min | 50/min per tool | 20 |
| **SuperAdmin** | Unlimited | Unlimited | Unlimited |

---

## Installation

### Prerequisites

- **.NET 8.0 SDK** (Windows) - [Download](https://dotnet.microsoft.com/download/dotnet/8.0)
- **Python 3.10+** (for MCP Bridge)
- **ARK: Survival Ascended** server with DuckBot mod installed

### Build from Source

```bash
# Clone the repository
git clone https://github.com/Franzferdinan51/Ark-DuckBot-Desktop.git
cd Ark-DuckBot-Desktop/ArkDuckBotDesktop

# Restore dependencies
dotnet restore

# Build the application
dotnet build

# Run the application
dotnet run
```

### Server Requirements

The ARK server must have the **DuckBot mod** installed and configured. This includes:

1. **DuckBot ServerAPI Plugin** - Provides game data and command execution
2. **MCP Bridge** - Python service for AI processing

For server installation guides, see:
- [DuckBot-For-ark](https://github.com/Franzferdinan51/DuckBot-For-ark)
- [WindowsGSM.ArkSAwithServerAPI](https://github.com/ohmcodes/WindowsGSM.ArkSAwithServerAPI)

### First Run Setup

1. Launch ArkDuckBot Desktop
2. Go to **Settings** → **Connection**
3. Configure:
   - **ARK Server IP**: Your server's IP address
   - **ARK Server Port**: Default `27020`
   - **MCP Bridge URL**: `ws://localhost:8443`
   - **MCP Secret**: Your configured shared secret
4. Click **Connect** to verify connections
5. Configure notification preferences in **Settings** → **Notifications**

---

## Configuration

Configuration files are stored in `%APPDATA%\ArkDuckBot\`

### Connection Settings

```json
{
  "ark_host": "192.168.1.100",
  "ark_port": 27020,
  "mcp_host": "localhost",
  "mcp_port": 8443,
  "mcp_secret": "your-secret-key"
}
```

### Notification Settings

```json
{
  "announce_player_join": true,
  "announce_player_leave": true,
  "announce_drops": true,
  "announce_tribe_alerts": true,
  "announce_events": true,
  "minimize_to_tray": true,
  "auto_start": false
}
```

### Display Settings

```json
{
  "theme": "dark",
  "map_zoom_level": 1.0,
  "show_player_names": true,
  "show_dino_levels": true,
  "map_refresh_rate": 1000
}
```

---

## Tech Stack

| Component | Technology | Version |
|-----------|------------|---------|
| **Framework** | .NET 8.0 WPF | 8.0 |
| **UI Library** | WPF-UI (Fluent Design) | 3.x |
| **WebView** | Microsoft WebView2 | Latest |
| **Architecture** | MVVM | - |
| **API Client** | WebSocket (System.Net.WebSockets) | Built-in |
| **AI Bridge** | WebSocket (Python asyncio) | - |
| **JSON** | System.Text.Json | Built-in |

---

## Related Projects

| Project | Description |
|---------|-------------|
| [DuckBot-For-ark](https://github.com/Franzferdinan51/DuckBot-For-ark) | ARK mod (C++) + Python MCP bridge - the complete server-side solution |
| [sheldon-ai-for-ark](https://github.com/ArkAscendedAI/sheldon-ai-for-ark) | AI integration framework for ARK servers |
| [WindowsGSM.ArkSAwithServerAPI](https://github.com/ohmcodes/WindowsGSM.ArkSAwithServerAPI) | WindowsGSM plugin for ARK SA with ServerAPI support |
| [rustplus-desktop](https://github.com/Pronwan/rustplus-desktop) | Original Rust+ companion app (inspiration for this project) |

---

## Architecture Inspiration

ArkDuckBot Desktop and the DuckBot MCP Bridge draw inspiration from several open-source projects:

- **hermes-agent** - Agent framework architecture
- **OpenClaw** - Desktop companion app patterns
- **sheldon-mcp-bridge** - Original MCP Bridge implementation

---

## License

GPL-3.0 License - See [LICENSE](LICENSE) file for details

## Authors

- **Franz Ferdinand** - [GitHub](https://github.com/Franzferdinan51)
- **OpenClaude** - Development assistance

## Acknowledgments

- Original project inspiration: [Pronwan/rustplus-desktop](https://github.com/Pronwan/rustplus-desktop)
- DuckBot architecture: [sheldon-mcp-bridge](https://github.com/nicholaslim99/sheldon-mcp-bridge)
- Sheldon AI framework: [sheldon-ai-for-ark](https://github.com/ArkAscendedAI/sheldon-ai-for-ark)
- ARK AsaApi: Community ServerAPI project
