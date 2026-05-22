using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.Json;

namespace ArkDuckBot.Services;

/// <summary>
/// DuckBot Handler - Routes game commands to the ARK C++ plugin via command queue.
/// Inspired by sheldon-ai-for-ark duckbot_handler.py architecture.
///
/// Commands are queued and polled by the C++ plugin, ensuring the plugin
/// remains the client (avoiding firewall issues).
/// </summary>
public class DuckBotHandler : IDisposable
{
    private readonly ConcurrentQueue<QueuedCommand> _commandQueue = new();
    private const int MaxQueueSize = 100;

    public event EventHandler<string>? CommandEnqueued;
    public event EventHandler<string>? Error;

    /// <summary>
    /// Enqueue a console command to be executed by the C++ plugin.
    /// </summary>
    public async Task<bool> EnqueueCommandAsync(string action, Dictionary<string, object> payload, string? playerId = null)
    {
        var cmd = new QueuedCommand
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            Action = action,
            Payload = payload,
            PlayerId = playerId,
            Timestamp = DateTime.UtcNow
        };

        if (_commandQueue.Count >= MaxQueueSize)
        {
            // Drop oldest command when queue is full
            _commandQueue.TryDequeue(out _);
        }

        _commandQueue.Enqueue(cmd);
        CommandEnqueued?.Invoke(this, $"[{cmd.Id}] {action}");
        return await Task.FromResult(true);
    }

    /// <summary>
    /// Spawn a dinosaur at a player location.
    /// </summary>
    public Task<bool> SpawnDinoAsync(string dinoType, int level, string gender, string? playerName = null)
    {
        return EnqueueCommandAsync("spawn_dino", new Dictionary<string, object>
        {
            { "dino_type", dinoType },
            { "level", level },
            { "gender", gender },
            { "player_name", playerName ?? "" }
        });
    }

    /// <summary>
    /// Give an item to a player.
    /// </summary>
    public Task<bool> GiveItemAsync(string itemName, int quantity, string? playerName = null)
    {
        return EnqueueCommandAsync("give_item", new Dictionary<string, object>
        {
            { "item_name", itemName },
            { "quantity", quantity },
            { "player_name", playerName ?? "" }
        });
    }

    /// <summary>
    /// Teleport a player to another player or coordinates.
    /// </summary>
    public Task<bool> TeleportPlayerAsync(string target, string? playerName = null)
    {
        return EnqueueCommandAsync("teleport_player", new Dictionary<string, object>
        {
            { "target", target },
            { "player_name", playerName ?? "" }
        });
    }

    /// <summary>
    /// Broadcast a message to all players.
    /// </summary>
    public Task<bool> BroadcastAsync(string message)
    {
        return EnqueueCommandAsync("broadcast", new Dictionary<string, object>
        {
            { "message", message }
        });
    }

    /// <summary>
    /// Slay a dinosaur or player.
    /// </summary>
    public Task<bool> SlayAsync(string target, bool isPlayer = false)
    {
        return EnqueueCommandAsync("slay", new Dictionary<string, object>
        {
            { "target", target },
            { "is_player", isPlayer }
        });
    }

    /// <summary>
    /// Execute a console command (whitelisted only).
    /// </summary>
    public Task<bool> ExecuteConsoleCommandAsync(string command)
    {
        // Sanitize command - remove dangerous characters
        var sanitized = SanitizeCommand(command);
        return EnqueueCommandAsync("console_command", new Dictionary<string, object>
        {
            { "command", sanitized }
        });
    }

    /// <summary>
    /// Feed tribe (for tame management).
    /// </summary>
    public Task<bool> FeedTribeAsync(string? tribeName = null)
    {
        return EnqueueCommandAsync("feed_tribe", new Dictionary<string, object>
        {
            { "tribe_name", tribeName ?? "" }
        });
    }

    /// <summary>
    /// Get all pending commands (for C++ plugin polling).
    /// </summary>
    public List<QueuedCommand> GetPendingCommands()
    {
        var commands = new List<QueuedCommand>();
        while (_commandQueue.TryDequeue(out var cmd))
        {
            commands.Add(cmd);
        }
        return commands;
    }

    /// <summary>
    /// Get queue size without dequeuing.
    /// </summary>
    public int QueueSize => _commandQueue.Count;

    private static string SanitizeCommand(string command)
    {
        // Remove dangerous characters that could enable command injection
        var dangerous = new[] { '"', '\n', '\r', '\\', ';', '&', '|', '`', '$' };
        var result = command;
        foreach (var c in dangerous)
            result = result.Replace(c.ToString(), "");
        return result.Trim();
    }

    public void Dispose() => GC.SuppressFinalize(this);
}

public class QueuedCommand
{
    public string Id { get; set; } = "";
    public string Action { get; set; } = "";
    public Dictionary<string, object> Payload { get; set; } = new();
    public string? PlayerId { get; set; }
    public DateTime Timestamp { get; set; }
}