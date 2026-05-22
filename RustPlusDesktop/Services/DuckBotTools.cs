using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace ArkDuckBot.Services;

/// <summary>
/// DuckBot Tools - ARK-specific tools for the AI agent.
/// Based on sheldon-ai-for-ark and openclaw tool patterns.
///
/// These tools are registered with the DuckBotAgent and can be called
/// by the AI when processing player requests.
/// </summary>
public class DuckBotTools : IDisposable
{
    private readonly DuckBotAgent _agent;
    private McpBridgeClient? _mcpBridge;

    public DuckBotTools(DuckBotAgent agent)
    {
        _agent = agent;
        RegisterTools();
    }

    public void SetMcpBridge(McpBridgeClient bridge)
    {
        _mcpBridge = bridge;
    }

    private void RegisterTools()
    {
        // Player lookup tools
        _agent.RegisterTool("player_info", "Get information about a player",
            new[] { "player_name" },
            async (args) => await PlayerInfoAsync(args));

        _agent.RegisterTool("player_tribe", "Get tribe information for a player",
            new[] { "player_name" },
            async (args) => await PlayerTribeAsync(args));

        _agent.RegisterTool("player_inventory", "Check if player has specific items",
            new[] { "player_name", "item_name" },
            async (args) => await PlayerInventoryAsync(args));

        // Dino tools
        _agent.RegisterTool("dino_stats", "Get stats for a dinosaur type",
            new[] { "dino_name" },
            async (args) => await DinoStatsAsync(args));

        _agent.RegisterTool("spawn_dino", "Spawn a dinosaur",
            new[] { "dino_type", "level", "gender", "location" },
            async (args) => await SpawnDinoAsync(args));

        _agent.RegisterTool("tame_info", "Get taming information for a dinosaur",
            new[] { "dino_name" },
            async (args) => await TameInfoAsync(args));

        // Admin tools
        _agent.RegisterTool("spawn_item", "Give items to a player",
            new[] { "player_name", "item_name", "quantity" },
            async (args) => await SpawnItemAsync(args));

        _agent.RegisterTool("teleport_player", "Teleport a player",
            new[] { "player_name", "x", "y", "z" },
            async (args) => await TeleportPlayerAsync(args));

        _agent.RegisterTool("broadcast", "Send a server-wide broadcast",
            new[] { "message" },
            async (args) => await BroadcastAsync(args));

        _agent.RegisterTool("kick_player", "Kick a player from the server",
            new[] { "player_name", "reason" },
            async (args) => await KickPlayerAsync(args));

        _agent.RegisterTool("ban_player", "Ban a player from the server",
            new[] { "player_name", "reason" },
            async (args) => await BanPlayerAsync(args));

        // Server tools
        _agent.RegisterTool("server_status", "Get current server status",
            Array.Empty<string>(),
            async (_) => await ServerStatusAsync());

        _agent.RegisterTool("event_info", "Get information about current in-game events",
            Array.Empty<string>(),
            async (_) => await EventInfoAsync());

        _agent.RegisterTool("time_weather", "Get current in-game time and weather",
            Array.Empty<string>(),
            async (_) => await TimeWeatherAsync());

        // Tribe tools
        _agent.RegisterTool("tribe_members", "List tribe members",
            new[] { "tribe_name" },
            async (args) => await TribeMembersAsync(args));

        _agent.RegisterTool("tribe_dinos", "List tribe dinosaurs",
            new[] { "tribe_name" },
            async (args) => await TribeDinosAsync(args));

        // Economy tools
        _agent.RegisterTool("player_balance", "Get player economy balance",
            new[] { "player_name" },
            async (args) => await PlayerBalanceAsync(args));

        // Map tools
        _agent.RegisterTool("marker_info", "Get information about map markers",
            new[] { "marker_type" },
            async (args) => await MarkerInfoAsync(args));

        _agent.RegisterTool("find_player", "Find player location on map",
            new[] { "player_name" },
            async (args) => await FindPlayerAsync(args));

        _agent.RegisterTool("find_dino", "Find dinosaur location on map",
            new[] { "dino_name" },
            async (args) => await FindDinoAsync(args));
    }

    #region Player Tools

    private async Task<object> PlayerInfoAsync(Dictionary<string, object> args)
    {
        var playerName = args.TryGetValue("player_name", out var p) ? p?.ToString() : "";
        if (string.IsNullOrEmpty(playerName))
            return new { error = "player_name required" };

        // Send via MCP bridge to query player info
        var response = await QueryMcpAsync("player_info", new { name = playerName });
        return new { player = playerName, info = response };
    }

    private async Task<object> PlayerTribeAsync(Dictionary<string, object> args)
    {
        var playerName = args.TryGetValue("player_name", out var p) ? p?.ToString() : "";
        if (string.IsNullOrEmpty(playerName))
            return new { error = "player_name required" };

        var response = await QueryMcpAsync("tribe_info", new { player = playerName });
        return new { player = playerName, tribe = response };
    }

    private async Task<object> PlayerInventoryAsync(Dictionary<string, object> args)
    {
        var playerName = args.TryGetValue("player_name", out var p) ? p?.ToString() : "";
        var itemName = args.TryGetValue("item_name", out var i) ? i?.ToString() : "";

        if (string.IsNullOrEmpty(playerName) || string.IsNullOrEmpty(itemName))
            return new { error = "player_name and item_name required" };

        var response = await QueryMcpAsync("check_inventory", new { player = playerName, item = itemName });
        return new { player = playerName, item = itemName, found = response };
    }

    #endregion

    #region Dino Tools

    private async Task<object> DinoStatsAsync(Dictionary<string, object> args)
    {
        var dinoName = args.TryGetValue("dino_name", out var d) ? d?.ToString() : "";
        if (string.IsNullOrEmpty(dinoName))
            return new { error = "dino_name required" };

        var response = await QueryMcpAsync("dino_stats", new { dino = dinoName });
        return new { dino = dinoName, stats = response };
    }

    private async Task<object> SpawnDinoAsync(Dictionary<string, object> args)
    {
        var dinoType = args.TryGetValue("dino_type", out var d) ? d?.ToString() : "";
        var level = args.TryGetValue("level", out var l) ? Convert.ToInt32(l) : 120;
        var gender = args.TryGetValue("gender", out var g) ? g?.ToString() : "random";
        var location = args.TryGetValue("location", out var loc) ? loc?.ToString() : "near";

        if (string.IsNullOrEmpty(dinoType))
            return new { error = "dino_type required" };

        var response = await QueryMcpAsync("spawn_dino", new
        {
            dino = dinoType,
            level,
            gender,
            location
        });

        return new { success = true, dino = dinoType, level, message = response };
    }

    private async Task<object> TameInfoAsync(Dictionary<string, object> args)
    {
        var dinoName = args.TryGetValue("dino_name", out var d) ? d?.ToString() : "";
        if (string.IsNullOrEmpty(dinoName))
            return new { error = "dino_name required" };

        var response = await QueryMcpAsync("tame_info", new { dino = dinoName });
        return new { dino = dinoName, taming = response };
    }

    #endregion

    #region Admin Tools

    private async Task<object> SpawnItemAsync(Dictionary<string, object> args)
    {
        var playerName = args.TryGetValue("player_name", out var p) ? p?.ToString() : "";
        var itemName = args.TryGetValue("item_name", out var i) ? i?.ToString() : "";
        var quantity = args.TryGetValue("quantity", out var q) ? Convert.ToInt32(q) : 1;

        if (string.IsNullOrEmpty(playerName) || string.IsNullOrEmpty(itemName))
            return new { error = "player_name and item_name required" };

        var response = await QueryMcpAsync("give_item", new
        {
            player = playerName,
            item = itemName,
            quantity
        });

        return new { success = true, player = playerName, item = itemName, quantity };
    }

    private async Task<object> TeleportPlayerAsync(Dictionary<string, object> args)
    {
        var playerName = args.TryGetValue("player_name", out var p) ? p?.ToString() : "";
        var x = args.TryGetValue("x", out var xv) ? Convert.ToDouble(xv) : 0.0;
        var y = args.TryGetValue("y", out var yv) ? Convert.ToDouble(yv) : 0.0;
        var z = args.TryGetValue("z", out var zv) ? Convert.ToDouble(zv) : 0.0;

        if (string.IsNullOrEmpty(playerName))
            return new { error = "player_name required" };

        var response = await QueryMcpAsync("teleport", new
        {
            player = playerName,
            position = new { x, y, z }
        });

        return new { success = true, player = playerName, position = new { x, y, z } };
    }

    private async Task<object> BroadcastAsync(Dictionary<string, object> args)
    {
        var message = args.TryGetValue("message", out var m) ? m?.ToString() : "";
        if (string.IsNullOrEmpty(message))
            return new { error = "message required" };

        var response = await QueryMcpAsync("broadcast", new { message });
        return new { success = true, message };
    }

    private async Task<object> KickPlayerAsync(Dictionary<string, object> args)
    {
        var playerName = args.TryGetValue("player_name", out var p) ? p?.ToString() : "";
        var reason = args.TryGetValue("reason", out var r) ? r?.ToString() : "Kicked by admin";

        if (string.IsNullOrEmpty(playerName))
            return new { error = "player_name required" };

        var response = await QueryMcpAsync("kick", new { player = playerName, reason });
        return new { success = true, player = playerName, reason };
    }

    private async Task<object> BanPlayerAsync(Dictionary<string, object> args)
    {
        var playerName = args.TryGetValue("player_name", out var p) ? p?.ToString() : "";
        var reason = args.TryGetValue("reason", out var r) ? r?.ToString() : "Banned by admin";

        if (string.IsNullOrEmpty(playerName))
            return new { error = "player_name required" };

        var response = await QueryMcpAsync("ban", new { player = playerName, reason });
        return new { success = true, player = playerName, reason };
    }

    #endregion

    #region Server Tools

    private async Task<object> ServerStatusAsync()
    {
        var response = await QueryMcpAsync("server_status", new { });
        return response;
    }

    private async Task<object> EventInfoAsync()
    {
        var response = await QueryMcpAsync("events", new { });
        return response;
    }

    private async Task<object> TimeWeatherAsync()
    {
        var response = await QueryMcpAsync("time_weather", new { });
        return response;
    }

    #endregion

    #region Tribe Tools

    private async Task<object> TribeMembersAsync(Dictionary<string, object> args)
    {
        var tribeName = args.TryGetValue("tribe_name", out var t) ? t?.ToString() : "";
        if (string.IsNullOrEmpty(tribeName))
            return new { error = "tribe_name required" };

        var response = await QueryMcpAsync("tribe_members", new { tribe = tribeName });
        return new { tribe = tribeName, members = response };
    }

    private async Task<object> TribeDinosAsync(Dictionary<string, object> args)
    {
        var tribeName = args.TryGetValue("tribe_name", out var t) ? t?.ToString() : "";
        if (string.IsNullOrEmpty(tribeName))
            return new { error = "tribe_name required" };

        var response = await QueryMcpAsync("tribe_dinos", new { tribe = tribeName });
        return new { tribe = tribeName, dinos = response };
    }

    #endregion

    #region Economy Tools

    private async Task<object> PlayerBalanceAsync(Dictionary<string, object> args)
    {
        var playerName = args.TryGetValue("player_name", out var p) ? p?.ToString() : "";
        if (string.IsNullOrEmpty(playerName))
            return new { error = "player_name required" };

        var response = await QueryMcpAsync("balance", new { player = playerName });
        return new { player = playerName, balance = response };
    }

    #endregion

    #region Map Tools

    private async Task<object> MarkerInfoAsync(Dictionary<string, object> args)
    {
        var markerType = args.TryGetValue("marker_type", out var m) ? m?.ToString() : "all";
        var response = await QueryMcpAsync("markers", new { type = markerType });
        return new { markers = response };
    }

    private async Task<object> FindPlayerAsync(Dictionary<string, object> args)
    {
        var playerName = args.TryGetValue("player_name", out var p) ? p?.ToString() : "";
        if (string.IsNullOrEmpty(playerName))
            return new { error = "player_name required" };

        var response = await QueryMcpAsync("find_player", new { player = playerName });
        return new { player = playerName, location = response };
    }

    private async Task<object> FindDinoAsync(Dictionary<string, object> args)
    {
        var dinoName = args.TryGetValue("dino_name", out var d) ? d?.ToString() : "";
        if (string.IsNullOrEmpty(dinoName))
            return new { error = "dino_name required" };

        var response = await QueryMcpAsync("find_dino", new { dino = dinoName });
        return new { dino = dinoName, locations = response };
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Send a query to the MCP bridge and get response.
    /// </summary>
    private async Task<string> QueryMcpAsync(string tool, object args)
    {
        if (_mcpBridge?.IsConnected != true)
            return "MCP Bridge not connected";

        try
        {
            // Format as natural command to send through bridge
            var command = FormatToolAsCommand(tool, args);
            await _mcpBridge.SendChatMessageAsync(command);
            return $"Executed: {tool}";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    /// <summary>
    /// Format tool call as natural language command.
    /// </summary>
    private string FormatToolAsCommand(string tool, object args)
    {
        var dict = args as Dictionary<string, object> ?? new Dictionary<string, object>();
        return tool switch
        {
            "spawn_dino" => $"spawn {dict["dino_type"]} level {dict["level"]}",
            "spawn_item" => $"give {dict["player_name"]} {dict["item_name"]} x{dict["quantity"]}",
            "teleport" => $"teleport {dict["player_name"]} to {dict["x"]},{dict["y"]},{dict["z"]}",
            "broadcast" => $"broadcast {dict["message"]}",
            "kick" => $"kick {dict["player_name"]} {dict["reason"]}",
            "ban" => $"ban {dict["player_name"]} {dict["reason"]}",
            _ => $"/{tool} {string.Join(" ", dict.Values)}"
        };
    }

    #endregion

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}