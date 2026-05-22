# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build Commands

```bash
dotnet build                          # Debug build
dotnet build -c Release               # Release build
dotnet run                            # Run debug
dotnet publish -c Release --self-contained  # Publish single-file exe
```

## Architecture Overview

ArkDuckBot Desktop is a WPF .NET 8.0 application that connects to **ARK: Survival Ascended** servers running the DuckBot mod. It maintains dual WebSocket connections: one to the game server (via ServerAPI) and one to the DuckBot MCP Bridge AI system.

```
┌─────────────────────────────────────────────────────────────┐
│                    MainWindow (WPF)                         │
├─────────────────────────────────────────────────────────────┤
│  MainViewModel  │  AiChatViewModel  │  Commands ViewModel   │
├─────────────────────────────────────────────────────────────┤
│              Services Layer                                  │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────────┐  │
│  │ ArkApiClient │  │McpBridgeClient│  │ TrackingService  │  │
│  │ (ServerAPI)  │  │ (AI Bridge)   │  │ (Settings)       │  │
│  └──────────────┘  └──────────────┘  └──────────────────┘  │
│                                                              │
│  ┌──────────────────────────────────────────────────────┐   │
│  │          DuckBotOrchestrator (AI Central)            │   │
│  │  ┌────────────┐ ┌────────────┐ ┌────────────────┐   │   │
│  │  │DuckBotAi   │ │DuckBotAgent│ │DuckBotTools    │   │   │
│  │  │Service     │ │(Tool Reg)  │ │(25+ ASA Tools) │   │   │
│  │  │Intent Route│ │Permissions │ │                 │   │   │
│  │  └────────────┘ └────────────┘ └────────────────┘   │   │
│  │  ┌────────────┐ ┌────────────┐ ┌────────────────┐   │   │
│  │  │DuckBotSkills│ │DuckBotHandler│ │DuckBotKnowledge│  │   │
│  │  │(Event Driv)│ │(Cmd Queue) │ │(Dino/Item DB)  │   │   │
│  │  └────────────┘ └────────────┘ └────────────────┘   │   │
│  └──────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
```

## Key Services

**McpBridgeClient** - WebSocket client connecting to DuckBot MCP Bridge on port 8444 (desktop) or 8443 (in-game). Handles authentication, AI message routing, and game events.

**DuckBotOrchestrator** - Central AI service coordinating all DuckBot components. Routes user input through intent recognition, manages player sessions with rate limiting per tier (player/vip/mod/admin).

**DuckBotAiService** - Natural language intent parser. Routes to Query/Command/Action/Help/Chat handlers based on keyword detection.

**DuckBotAgent** - Tool registry with permission tiers. All 25+ ARK: Survival Ascended tools organized by required tier level.

**DuckBotSkills** - Event-driven skill system. Built-in skills: wild_dino_alert (vip), auto_slay_dangerous (admin), player_join_welcome (player).

**DuckBotHandler** - Command queue for C++ plugin communication. Sanitized console commands queued for polling by ARK: Survival Ascended server plugin.

**DuckBotKnowledge** - Encyclopedia with fuzzy search for ARK: Survival Ascended dinos (rex, giga, megalodon, argy, etc.) and items (kibble, raw meat, etc.).

## Connection Flow

```
Connect → ArkApiClient (ServerAPI) + McpBridgeClient (port 8444)
           ↓                              ↓
     ARK: Survival Ascended          DuckBot AI Bridge
     Server (ServerAPI Plugin)       (Python MCP Bridge)
                                        ↓
                                   LLM Provider
                                   (OpenRouter/Anthropic/etc)
```

## Important Patterns

- **Permission Tiers**: player < vip < mod < admin. Tools are gated by tier in DuckBotTools.
- **Dual Port**: MCP Bridge uses port 8444 for desktop companion, 8443 for in-game connections.
- **Event-Driven Skills**: Skills trigger on game events (player_joined, high_level_dino_detected) via DuckBotSkills.TriggerByEventAsync()
- **Command Queue**: DuckBotHandler enqueues commands for the ServerAPI plugin to poll, avoiding firewall issues on incoming connections.
- **Namespace**: All code uses `ArkDuckBot` namespace (converted from RustPlusDesk).

## Related Repositories

- **DuckBot-For-ark**: https://github.com/Franzferdinan51/DuckBot-For-ark - C++ ServerAPI plugin + Python MCP Bridge for ARK: Survival Ascended
- **WindowsGSM.ArkSAwithServerAPI**: https://github.com/ohmcodes/WindowsGSM.ArkSAwithServerAPI - Server management

## Repository

Remote: https://github.com/FranzFERDINAN51/Ark-DuckBot-Desktop