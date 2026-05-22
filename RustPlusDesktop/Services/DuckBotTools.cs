using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ArkDuckBot.Services;

/// <summary>
/// DuckBot Tools - ARK-specific tools with permission tiers.
/// Inspired by sheldon-ai-for-ark @tool decorator pattern.
///
/// Tools are organized by category and enforce permission tiers.
/// Admin-only tools (spawn, teleport, ban) require Admin tier.
/// Player tools (lookup, balance) require Player tier.
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
        // ========== KNOWLEDGE TOOLS (Player tier - read only) ==========

        // Player lookup tools
        _agent.RegisterTool(
            "player_info",
            "Get information about a player",
            DuckBotAgent.PermissionTier.Player,
            new[] { "player_name" },
            null,
            async (args) => await PlayerInfoAsync(args));

        _agent.RegisterTool(
            "player_tribe",
            "Get tribe information for a player",
            DuckBotAgent.PermissionTier.Player,
            new[] { "player_name" },
            null,
            async (args) => await PlayerTribeAsync(args));

        _agent.RegisterTool(
            "dino_stats",
            "Get stats for a dinosaur type",
            DuckBotAgent.PermissionTier.Player,
            new[] { "dino_name" },
            null,
            async (args) => await DinoStatsAsync(args));

        _agent.RegisterTool(
            "tame_info",
            "Get taming information for a dinosaur",
            DuckBotAgent.PermissionTier.Player,
            new[] { "dino_name" },
            null,
            async (args) => await TameInfoAsync(args));

        _agent.RegisterTool(
            "server_status",
            "Get current server status",
            DuckBotAgent.PermissionTier.Player,
            Array.Empty<string>(),
            null,
            async (_) => await ServerStatusAsync());

        _agent.RegisterTool(
            "event_info",
            "Get information about current in-game events",
            DuckBotAgent.PermissionTier.Player,
            Array.Empty<string>(),
            null,
            async (_) => await EventInfoAsync());

        _agent.RegisterTool(
            "player_balance",
            "Get player economy balance",
            DuckBotAgent.PermissionTier.Player,
            new[] { "player_name" },
            null,
            async (args) => await PlayerBalanceAsync(args));

        // ========== VIP TOOLS (Vip tier - some write access) ==========

        _agent.RegisterTool(
            "teleport_to_player",
            "Teleport to another player",
            DuckBotAgent.PermissionTier.Vip,
            new[] { "target_player" },
            null,
            async (args) => await TeleportToPlayerAsync(args));

        _agent.RegisterTool(
            "marker_info",
            "Get information about map markers",
            DuckBotAgent.PermissionTier.Vip,
            new[] { "marker_type" },
            null,
            async (args) => await MarkerInfoAsync(args));

        // ========== MOD TOOLS (Mod tier - moderation) ==========

        _agent.RegisterTool(
            "kick_player",
            "Kick a player from the server",
            DuckBotAgent.PermissionTier.Mod,
            new[] { "player_name", "reason" },
            new Dictionary<string, object> { { "rate_limit", "5 per 60s" } },
            async (args) => await KickPlayerAsync(args));

        _agent.RegisterTool(
            "mute_player",
            "Mute a player in chat",
            DuckBotAgent.PermissionTier.Mod,
            new[] { "player_name", "duration_minutes" },
            null,
            async (args) => await MutePlayerAsync(args));

        _agent.RegisterTool(
            "broadcast",
            "Send a server-wide broadcast message",
            DuckBotAgent.PermissionTier.Mod,
            new[] { "message" },
            new Dictionary<string, object> { { "max_length", 200 } },
            async (args) => await BroadcastAsync(args));

        // ========== ADMIN TOOLS (Admin tier - full access) ==========

        _agent.RegisterTool(
            "spawn_dino",
            "Spawn a dinosaur near a player",
            DuckBotAgent.PermissionTier.Admin,
            new[] { "dino_type", "level", "gender" },
            new Dictionary<string, object> { { "max_level", 500 }, { "rate_limit", "10 per 60s" } },
            async (args) => await SpawnDinoAsync(args));

        _agent.RegisterTool(
            "spawn_item",
            "Give items to a player",
            DuckBotAgent.PermissionTier.Admin,
            new[] { "player_name", "item_name", "quantity" },
            null,
            async (args) => await SpawnItemAsync(args));

        _agent.RegisterTool(
            "teleport_player",
            "Teleport a player to coordinates",
            DuckBotAgent.PermissionTier.Admin,
            new[] { "player_name", "x", "y", "z" },
            null,
            async (args) => await TeleportPlayerAsync(args));

        _agent.RegisterTool(
            "ban_player",
            "Ban a player from the server",
            DuckBotAgent.PermissionTier.Admin,
            new[] { "player_name", "reason" },
            new Dictionary<string, object> { { "rate_limit", "5 per 60s" } },
            async (args) => await BanPlayerAsync(args));

        _agent.RegisterTool(
            "unban_player",
            "Unban a player from the server",
            DuckBotAgent.PermissionTier.Admin,
            new[] { "player_name" },
            null,
            async (args) => await UnbanPlayerAsync(args));

        _agent.RegisterTool(
            "set_time",
            "Set in-game time (hour 0-23)",
            DuckBotAgent.PermissionTier.Admin,
            new[] { "hour" },
            new Dictionary<string, object> { { "min_hour", 0 }, { "max_hour", 23 } },
            async (args) => await SetTimeAsync(args));

        // ========== MAP TOOLS (Player tier) ==========

        _agent.RegisterTool(
            "find_player",
            "Find player location on map",
            DuckBotAgent.PermissionTier.Player,
            new[] { "player_name" },
            null,
            async (args) => await FindPlayerAsync(args));

        _agent.RegisterTool(
            "find_dino",
            "Find dinosaurs on map by name",
            DuckBotAgent.PermissionTier.Player,
            new[] { "dino_name" },
            null,
            async (args) => await FindDinoAsync(args));

        // ========== TRIBE TOOLS (Vip tier) ==========

        _agent.RegisterTool(
            "tribe_members",
            "List tribe members",
            DuckBotAgent.PermissionTier.Vip,
            new[] { "tribe_name" },
            null,
            async (args) => await TribeMembersAsync(args));

        _agent.RegisterTool(
            "tribe_dinos",
            "List tribe dinosaurs",
            DuckBotAgent.PermissionTier.Vip,
            new[] { "tribe_name" },
            null,
            async (args) => await TribeDinosAsync(args));
    }

    #region Knowledge Tools (Player tier)

    private async Task<object> PlayerInfoAsync(Dictionary<string, object> args)
    {
        var playerName = args.TryGetValue("player_name", out var p) ? p?.ToString() : "";
        if (string.IsNullOrEmpty(playerName))
            return new { error = "player_name required" };

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

    private async Task<object> DinoStatsAsync(Dictionary<string, object> args)
    {
        var dinoName = args.TryGetValue("dino_name", out var d) ? d?.ToString() : "";
        if (string.IsNullOrEmpty(dinoName))
            return new { error = "dino_name required" };

        var response = await QueryMcpAsync("dino_stats", new { dino = dinoName });
        return new { dino = dinoName, stats = response };
    }

    private async Task<object> TameInfoAsync(Dictionary<string, object> args)
    {
        var dinoName = args.TryGetValue("dino_name", out var d) ? d?.ToString() : "";
        if (string.IsNullOrEmpty(dinoName))
            return new { error = "dino_name required" };

        var response = await QueryMcpAsync("tame_info", new { dino = dinoName });
        return new { dino = dinoName, taming = response };
    }

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

    private async Task<object> PlayerBalanceAsync(Dictionary<string, object> args)
    {
        var playerName = args.TryGetValue("player_name", out var p) ? p?.ToString() : "";
        if (string.IsNullOrEmpty(playerName))
            return new { error = "player_name required" };

        var response = await QueryMcpAsync("balance", new { player = playerName });
        return new { player = playerName, balance = response };
    }

    #endregion

    #region VIP Tools

    private async Task<object> TeleportToPlayerAsync(Dictionary<string, object> args)
    {
        var targetPlayer = args.TryGetValue("target_player", out var t) ? t?.ToString() : "";
        if (string.IsNullOrEmpty(targetPlayer))
            return new { error = "target_player required" };

        var response = await QueryMcpAsync("teleport_to", new { target = targetPlayer });
        return new { success = true, action = $"teleport to {targetPlayer}", response };
    }

    private async Task<object> MarkerInfoAsync(Dictionary<string, object> args)
    {
        var markerType = args.TryGetValue("marker_type", out var m) ? m?.ToString() : "all";
        var response = await QueryMcpAsync("markers", new { type = markerType });
        return new { markers = response };
    }

    #endregion

    #region Mod Tools

    private async Task<object> KickPlayerAsync(Dictionary<string, object> args)
    {
        var playerName = args.TryGetValue("player_name", out var p) ? p?.ToString() : "";
        var reason = args.TryGetValue("reason", out var r) ? r?.ToString() : "Kicked by mod";

        if (string.IsNullOrEmpty(playerName))
            return new { error = "player_name required" };

        var response = await QueryMcpAsync("kick", new { player = playerName, reason });
        return new { success = true, player = playerName, reason };
    }

    private async Task<object> MutePlayerAsync(Dictionary<string, object> args)
    {
        var playerName = args.TryGetValue("player_name", out var p) ? p?.ToString() : "";
        var duration = args.TryGetValue("duration_minutes", out var d) ? d?.ToString() : "60";

        if (string.IsNullOrEmpty(playerName))
            return new { error = "player_name required" };

        var response = await QueryMcpAsync("mute", new { player = playerName, duration });
        return new { success = true, player = playerName, duration };
    }

    private async Task<object> BroadcastAsync(Dictionary<string, object> args)
    {
        var message = args.TryGetValue("message", out var m) ? m?.ToString() : "";
        if (string.IsNullOrEmpty(message))
            return new { error = "message required" };

        if (message.Length > 200)
            return new { error = "message exceeds 200 character limit" };

        var response = await QueryMcpAsync("broadcast", new { message });
        return new { success = true, message };
    }

    #endregion

    #region Admin Tools

    private async Task<object> SpawnDinoAsync(Dictionary<string, object> args)
    {
        var dinoType = args.TryGetValue("dino_type", out var d) ? d?.ToString() : "";
        var level = args.TryGetValue("level", out var l) ? Convert.ToInt32(l) : 120;
        var gender = args.TryGetValue("gender", out var g) ? g?.ToString() : "random";

        if (string.IsNullOrEmpty(dinoType))
            return new { error = "dino_type required" };

        if (level > 500)
            return new { error = "level exceeds maximum of 500" };

        var response = await QueryMcpAsync("spawn_dino", new { dino = dinoType, level, gender });
        return new { success = true, dino = dinoType, level, gender, message = response };
    }

    private async Task<object> SpawnItemAsync(Dictionary<string, object> args)
    {
        var playerName = args.TryGetValue("player_name", out var p) ? p?.ToString() : "";
        var itemName = args.TryGetValue("item_name", out var i) ? i?.ToString() : "";
        var quantity = args.TryGetValue("quantity", out var q) ? Convert.ToInt32(q) : 1;

        if (string.IsNullOrEmpty(playerName) || string.IsNullOrEmpty(itemName))
            return new { error = "player_name and item_name required" };

        var response = await QueryMcpAsync("give_item", new { player = playerName, item = itemName, quantity });
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

        var response = await QueryMcpAsync("teleport", new { player = playerName, position = new { x, y, z } });
        return new { success = true, player = playerName, position = new { x, y, z } };
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

    private async Task<object> UnbanPlayerAsync(Dictionary<string, object> args)
    {
        var playerName = args.TryGetValue("player_name", out var p) ? p?.ToString() : "";
        if (string.IsNullOrEmpty(playerName))
            return new { error = "player_name required" };

        var response = await QueryMcpAsync("unban", new { player = playerName });
        return new { success = true, player = playerName };
    }

    private async Task<object> SetTimeAsync(Dictionary<string, object> args)
    {
        var hour = args.TryGetValue("hour", out var h) ? Convert.ToInt32(h) : -1;
        if (hour < 0 || hour > 23)
            return new { error = "hour must be between 0 and 23" };

        var response = await QueryMcpAsync("set_time", new { hour });
        return new { success = true, hour };
    }

    #endregion

    #region Map Tools

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

    #region Helper Methods

    private async Task<string> QueryMcpAsync(string tool, object args)
    {
        if (_mcpBridge?.IsConnected != true)
            return "MCP Bridge not connected";

        try
        {
            var command = FormatToolAsCommand(tool, args);
            await _mcpBridge.SendChatMessageAsync(command);
            return $"Executed: {tool}";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    private string FormatToolAsCommand(string tool, Dictionary<string, object> args)
    {
        return tool switch
        {
            "spawn_dino" => $"spawn {args["dino_type"]} level {args["level"]}",
            "spawn_item" => $"give {args["player_name"]} {args["item_name"]} x{args["quantity"]}",
            "teleport" => $"teleport {args["player_name"]} to {args["x"]},{args["y"]},{args["z"]}",
            "broadcast" => $"broadcast {args["message"]}",
            "kick" => $"kick {args["player_name"]} {args["reason"]}",
            "ban" => $"ban {args["player_name"]} {args["reason"]}",
            "set_time" => $"time set {args["hour"]}",
            _ => $"/{tool} {string.Join(" ", args.Values)}"
        };
    }

    #endregion

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}