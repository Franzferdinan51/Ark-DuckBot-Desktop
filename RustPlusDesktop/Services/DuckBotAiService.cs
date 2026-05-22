using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ArkDuckBot.Services;

/// <summary>
/// DuckBot AI Chat Service - Natural language AI command processor
/// Routes all player input through AI for intent recognition and execution
/// </summary>
public class DuckBotAiService : IDisposable
{
    private readonly DuckBotAgent _agent;
    private readonly DuckBotTools _tools;
    private McpBridgeClient? _mcpBridge;

    // Natural language patterns for command recognition
    private readonly Dictionary<string, string> _intentPatterns = new();

    public event EventHandler<string>? AiResponse;
    public event EventHandler<string>? ToolExecuted;
    public event EventHandler<string>? ErrorOccurred;

    public DuckBotAiService(DuckBotAgent agent, DuckBotTools tools)
    {
        _agent = agent;
        _tools = tools;
        LoadIntentPatterns();
    }

    public void SetMcpBridge(McpBridgeClient bridge) => _mcpBridge = bridge;

    /// <summary>
    /// Process user input through AI - handles all commands naturally
    /// </summary>
    public async Task<string> ProcessInputAsync(string userInput, string playerName, string playerTier)
    {
        try
        {
            // Parse intent from natural language
            var intent = ParseIntent(userInput);
            var context = CreateContext(playerName, playerTier);

            // Route to appropriate handler based on intent
            return intent.Type switch
            {
                IntentType.Query => await HandleQueryAsync(intent, context),
                IntentType.Command => await HandleCommandAsync(intent, context),
                IntentType.Action => await HandleActionAsync(intent, context),
                IntentType.Help => await HandleHelpAsync(intent, context),
                IntentType.Chat => await HandleChatAsync(intent, context),
                _ => "I'm not sure how to help with that. Try asking in natural language!"
            };
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, ex.Message);
            return $"Oops! Something went wrong: {ex.Message}";
        }
    }

    /// <summary>
    /// Parse natural language into structured intent
    /// </summary>
    private AiIntent ParseIntent(string input)
    {
        var lower = input.ToLower().Trim();
        var intent = new AiIntent { RawInput = input };

        // Check for explicit commands first
        if (lower.StartsWith("/"))
        {
            intent.Type = IntentType.Command;
            intent.Command = lower.Split(' ')[0].TrimStart('/');
            intent.Arguments = lower.Contains(' ') ? lower[(lower.IndexOf(' ') + 1)..] : "";
            return intent;
        }

        // Natural language intent detection
        if (lower.Contains("spawn") || lower.Contains("create") || lower.Contains("give me"))
            intent.Type = IntentType.Action;
        else if (lower.Contains("what") || lower.Contains("how") || lower.Contains("show") || lower.Contains("tell me"))
            intent.Type = IntentType.Query;
        else if (lower.Contains("do") || lower.Contains("make") || lower.Contains("set") || lower.Contains("enable"))
            intent.Type = IntentType.Command;
        else if (lower.Contains("help") || lower.Contains("what can"))
            intent.Type = IntentType.Help;
        else
            intent.Type = IntentType.Chat;

        // Extract key entities
        intent.Entities = ExtractEntities(lower);

        return intent;
    }

    /// <summary>
    /// Handle query intents (what, how, show, tell me)
    /// </summary>
    private async Task<string> HandleQueryAsync(AiIntent intent, AiContext context)
    {
        var lower = intent.RawInput.ToLower();

        // Dino queries
        if (lower.Contains("dino") || lower.Contains("creature") || lower.Contains("rex") || lower.Contains("spinosaur"))
        {
            var dinoName = ExtractDinoName(intent.Entities);
            if (!string.IsNullOrEmpty(dinoName))
            {
                var stats = await _agent.InvokeToolAsync("dino_stats",
                    new Dictionary<string, object> { { "dino_name", dinoName } },
                    context.Tier);
                return FormatDinoResponse(dinoName, stats);
            }
        }

        // Player queries
        if (lower.Contains("player") || lower.Contains("who is"))
        {
            var playerName = ExtractPlayerName(intent.Entities);
            if (!string.IsNullOrEmpty(playerName))
            {
                var info = await _agent.InvokeToolAsync("player_info",
                    new Dictionary<string, object> { { "player_name", playerName } },
                    context.Tier);
                return FormatPlayerResponse(playerName, info);
            }
        }

        // Tribe queries
        if (lower.Contains("tribe") || lower.Contains("my tribe"))
        {
            var tribeName = context.TribeId ?? "Unknown";
            var members = await _agent.InvokeToolAsync("tribe_members",
                new Dictionary<string, object> { { "tribe_name", tribeName } },
                context.Tier);
            return $"Your tribe '{tribeName}' has these members:\n{members}";
        }

        // Balance queries
        if (lower.Contains("balance") || lower.Contains("money"))
        {
            var balance = await _agent.InvokeToolAsync("player_balance",
                new Dictionary<string, object> { { "player_name", context.PlayerName } },
                context.Tier);
            return $"Your balance: {balance}";
        }

        // Server status
        if (lower.Contains("server") || lower.Contains("status"))
        {
            var status = await _agent.InvokeToolAsync("server_status", new Dictionary<string, object>(), context.Tier);
            return $"Server Status:\n{status}";
        }

        return "I can help with dino stats, player info, tribe data, balances, and more!";
    }

    /// <summary>
    /// Handle command intents (do, make, set, enable)
    /// </summary>
    private async Task<string> HandleCommandAsync(AiIntent intent, AiContext context)
    {
        var lower = intent.RawInput.ToLower();

        // Teleport commands
        if (lower.Contains("teleport") || lower.Contains("go to") || lower.Contains("warp"))
        {
            var target = ExtractTarget(intent.Entities);
            var tpResult = await _agent.InvokeToolAsync("teleport_to_player",
                new Dictionary<string, object> { { "target_player", target } },
                context.Tier);
            return tpResult?.ToString() ?? "Teleport command sent.";
        }

        // Home commands
        if (lower.Contains("home") && (lower.Contains("set") || lower.Contains("save")))
            return "Setting your home location...";
        if (lower.Contains("home"))
            return "Teleporting to your home...";

        // Broadcast
        if (lower.Contains("broadcast") || lower.Contains("announce"))
        {
            var message = ExtractMessage(intent.Entities);
            var bcResult = await _agent.InvokeToolAsync("broadcast",
                new Dictionary<string, object> { { "message", message } },
                context.Tier);
            return bcResult?.ToString() ?? "Broadcast sent.";
        }

        return "I'm not sure what command you want me to run. Try being more specific!";
    }

    /// <summary>
    /// Handle action intents (spawn, create, give me)
    /// </summary>
    private async Task<string> HandleActionAsync(AiIntent intent, AiContext context)
    {
        var lower = intent.RawInput.ToLower();

        // Spawn dino
        if (lower.Contains("spawn") || lower.Contains("create") || lower.Contains("summon"))
        {
            var dino = ExtractDinoName(intent.Entities);
            var level = ExtractLevel(intent.Entities);
            return await SpawnDinoAsync(dino, level, context);
        }

        // Give item
        if (lower.Contains("give") || lower.Contains("spawn") && lower.Contains("item"))
        {
            var item = ExtractItemName(intent.Entities);
            var quantity = ExtractQuantity(intent.Entities);
            var itemResult = await _agent.InvokeToolAsync("spawn_item",
                new Dictionary<string, object> { { "player_name", context.PlayerName }, { "item_name", item }, { "quantity", quantity } },
                context.Tier);
            return itemResult?.ToString() ?? $"Gave {quantity}x {item}.";
        }

        return "I can spawn dinos and items for you! Just say 'spawn me a Rex level 150'";
    }

    /// <summary>
    /// Handle help intents
    /// </summary>
    private Task<string> HandleHelpAsync(AiIntent intent, AiContext context)
    {
        var help = @"DuckBot AI - Your AI Assistant for ARK

**Available Commands (just ask naturally!):**

📊 **Info**: 'what's my balance', 'show tribe members', 'dino stats for Rex'
🏠 **Teleport**: 'go home', 'teleport to [player]', 'set home'
🦖 **Spawn**: 'spawn me a Rex level 200', 'give me kibble'
🎉 **Events**: 'what events are active', 'start event'
📢 **Admin**: 'broadcast hello everyone', 'kick [player]'

**Examples:**
- 'Can you spawn me a max level Giganotosaurus?'
- 'What's my tribe's total dino count?'
- 'Enable wild dino alerts for my tribe'
- 'Teleport to player X'

Just talk to me naturally!";
        return Task.FromResult(help);
    }

    /// <summary>
    /// Handle casual chat
    /// </summary>
    private Task<string> HandleChatAsync(AiIntent intent, AiContext context)
    {
        var lower = intent.RawInput.ToLower();

        // AI status check
        if (lower.Contains("are you") || lower.Contains("how are you"))
            return Task.FromResult("I'm doing great! I'm DuckBot AI, here to help you survive in ARK. What can I do for you?");

        // Thanks
        if (lower.Contains("thank") || lower.Contains("thanks"))
            return Task.FromResult("You're welcome! Happy surviving! 🦖");

        // Capabilities
        if (lower.Contains("what can you do") || lower.Contains("help"))
            return HandleHelpAsync(intent, context);

        return Task.FromResult("I'm here to help! Try asking about dinos, players, tribes, or spawn something for you.");
    }

    private async Task<string> SpawnDinoAsync(string dinoType, int level, AiContext context)
    {
        if (string.IsNullOrEmpty(dinoType))
            return "What dino would you like me to spawn? Try 'spawn me a Rex'";

        if (level <= 0) level = 120; // Default level

        var result = await _agent.InvokeToolAsync("spawn_dino",
            new Dictionary<string, object> { { "dino_type", dinoType }, { "level", level }, { "gender", "random" } },
            context.Tier);

        return $"Spawning level {level} {dinoType}... {result}";
    }

    private void LoadIntentPatterns()
    {
        // Common patterns for natural language understanding
        var patterns = new[]
        {
            "spawn * level *", "create * level *", "summon * level *",
            "teleport to *", "go to *", "warp to *",
            "what's my balance", "my balance", "check balance",
            "show * stats", "* stats", "dino stats for *"
        };
    }

    private AiContext CreateContext(string playerName, string playerTier)
    {
        return new AiContext
        {
            PlayerName = playerName,
            PlayerTier = playerTier,
            SessionId = _agent.GetSession().PlayerId
        };
    }

    private Dictionary<string, string> ExtractEntities(string input)
    {
        var entities = new Dictionary<string, string>();
        var words = input.Split(' ');

        foreach (var word in words)
        {
            // Level detection
            if (int.TryParse(word.Replace("level", "").Replace("lvl", "").Trim(), out var level))
                entities["level"] = level.ToString();

            // Player name detection (capitalized words)
            if (char.IsUpper(word[0]) && word.Length > 2 && !IsCommand(word))
                entities["player"] = word;
        }

        return entities;
    }

    private bool IsCommand(string word) =>
        word.ToLower() switch {
            "spawn" or "create" or "give" or "teleport" or "go" or "set" or "what" or "how" or "show" or "tell" => true,
            _ => false
        };

    private string ExtractDinoName(Dictionary<string, string> entities)
    {
        // Common aliases
        var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            {"rex", "Rex"}, {"t-rex", "Rex"}, {"trex", "Rex"},
            {"giga", "Giganotosaurus"}, {"giganoto", "Giganotosaurus"},
            {"mega", "Megalodon"}, {"megalodon", "Megalodon"},
            {"argy", "Argentavis"}, {"argentavis", "Argentavis"},
            {"yuty", "Yutyrannus"}, {"yutyrannus", "Yutyrannus"},
            {"therizino", "Therizinosaurus"}, {"theriz", "Therizinosaurus"}
        };

        // Try to find dino name in entities
        if (entities.TryGetValue("dino", out var dino))
            return aliases.TryGetValue(dino, out var full) ? full : dino;

        return "";
    }

    private string ExtractPlayerName(Dictionary<string, string> entities) =>
        entities.TryGetValue("player", out var name) ? name : "";

    private string ExtractTarget(Dictionary<string, string> entities) =>
        ExtractPlayerName(entities);

    private string ExtractMessage(Dictionary<string, string> entities) =>
        entities.TryGetValue("message", out var msg) ? msg : "";

    private int ExtractLevel(Dictionary<string, string> entities) =>
        entities.TryGetValue("level", out var lvl) && int.TryParse(lvl, out var level) ? level : 0;

    private string ExtractItemName(Dictionary<string, string> entities) =>
        entities.TryGetValue("item", out var item) ? item : "";

    private int ExtractQuantity(Dictionary<string, string> entities) =>
        entities.TryGetValue("quantity", out var qty) && int.TryParse(qty, out var num) ? num : 1;

    private string FormatDinoResponse(string dinoName, object stats) =>
        $"**{dinoName} Stats:**\n{stats}";

    private string FormatPlayerResponse(string playerName, object info) =>
        $"**Player {playerName}:**\n{info}";

    public void Dispose() => GC.SuppressFinalize(this);
}

#region Supporting Types

public class AiIntent
{
    public IntentType Type { get; set; }
    public string RawInput { get; set; } = "";
    public string Command { get; set; } = "";
    public string Arguments { get; set; } = "";
    public Dictionary<string, string> Entities { get; set; } = new();
}

public enum IntentType
{
    Query,
    Command,
    Action,
    Help,
    Chat
}

public class AiContext
{
    public string PlayerName { get; set; } = "";
    public string PlayerTier { get; set; } = "player";
    public string? TribeId { get; set; }
    public string SessionId { get; set; } = "";
    public DuckBotAgent.PermissionTier Tier => PlayerTier.ToLower() switch
    {
        "admin" => DuckBotAgent.PermissionTier.Admin,
        "mod" => DuckBotAgent.PermissionTier.Mod,
        "vip" => DuckBotAgent.PermissionTier.Vip,
        _ => DuckBotAgent.PermissionTier.Player
    };
}

#endregion
