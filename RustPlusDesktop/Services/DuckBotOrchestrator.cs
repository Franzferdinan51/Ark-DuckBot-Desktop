using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ArkDuckBot.Services;

/// <summary>
/// DuckBot AI Orchestrator - Central AI system that coordinates all DuckBot services.
/// Inspired by sheldon-ai-for-ark agent.py architecture.
///
/// Orchestrates:
/// - Intent recognition (DuckBotAiService)
/// - Tool execution (DuckBotTools via DuckBotAgent)
/// - Skill handling (DuckBotSkills)
/// - Knowledge lookup (DuckBotKnowledge)
/// - Command routing (DuckBotHandler)
///
/// Features:
/// - Conversation context management with token budgeting
/// - Per-player session isolation
/// - Rate limiting per tier
/// - Guardrail checks on responses
/// - Self-improvement from past sessions
/// </summary>
public class DuckBotOrchestrator : IDisposable
{
    private readonly DuckBotAiService _aiService;
    private readonly DuckBotAgent _agent;
    private readonly DuckBotTools _tools;
    private readonly DuckBotSkills _skills;
    private readonly DuckBotHandler _handler;
    private readonly DuckBotKnowledge _knowledge;

    private readonly Dictionary<string, AiPlayerSession> _sessions = new();
    private McpBridgeClient? _mcpBridge;

    public event EventHandler<string>? ResponseGenerated;
    public event EventHandler<string>? ToolExecuted;
    public event EventHandler<string>? ErrorOccurred;

    public DuckBotOrchestrator()
    {
        _tools = new DuckBotTools();
        _agent = new DuckBotAgent(_tools);
        _aiService = new DuckBotAiService(_agent, _tools);
        _skills = new DuckBotSkills();
        _handler = new DuckBotHandler();
        _knowledge = new DuckBotKnowledge();

        _skills.SetHandler(_handler);
        _aiService.SetMcpBridge(_mcpBridge!);

        InitializeBuiltinSkills();
    }

    public void SetMcpBridge(McpBridgeClient client)
    {
        _mcpBridge = client;
        _aiService.SetMcpBridge(client);
    }

    /// <summary>
    /// Initialize built-in skills that respond to game events.
    /// </summary>
    private void InitializeBuiltinSkills()
    {
        // Wild Dino Alert skill - monitors dangerous wild dinos
        _skills.RegisterBuiltin("wild_dino_alert", async ctx =>
        {
            var eventData = ctx.EventData;
            var species = eventData.TryGetValue("species", out var s) ? s?.ToString() ?? "Unknown" : "Unknown";
            var level = eventData.TryGetValue("level", out var l) && l is JsonElement je ? je.GetInt32() : 0;
            var distance = eventData.TryGetValue("distance", out var d) && d is JsonElement jd ? jd.GetDouble() : 0;

            if (level < 30) return new SkillResult
            {
                Success = true,
                Message = $"Wild {species} (level {level}) detected but below alert threshold."
            };

            var dangerous = new[] { "Giganotosaurus", "Titanosaur", "Megalodon", "Rex", "Spino" };
            var isDangerous = dangerous.Any(d => species.Contains(d, StringComparison.OrdinalIgnoreCase));

            var message = $"DANGER: Wild {species} (level {level}) detected at {distance:F0}m";
            if (isDangerous) message += " - HIGH PRIORITY";

            return new SkillResult
            {
                Success = true,
                Message = message,
                Data = new Dictionary<string, object>
                {
                    { "species", species },
                    { "level", level },
                    { "distance", distance },
                    { "dangerous", isDangerous }
                }
            };
        }, "vip", "wild_dino_alert");

        // Player Join welcome skill
        _skills.RegisterBuiltin("player_join_welcome", async ctx =>
        {
            return new SkillResult
            {
                Success = true,
                Message = $"Welcome {ctx.PlayerName}! Type /help for commands."
            };
        }, "player", "player_joined");

        // Auto-slay dangerous dino skill (admin only)
        _skills.RegisterBuiltin("auto_slay_dangerous", async ctx =>
        {
            var species = ctx.EventData.TryGetValue("species", out var s) ? s?.ToString() ?? "" : "";
            var level = ctx.EventData.TryGetValue("level", out var l) && l is JsonElement je ? je.GetInt32() : 0;

            if (level >= 150 && new[] { "Rex", "Giganotosaurus", "Spino" }.Any(d => species.Contains(d)))
            {
                var target = ctx.EventData.TryGetValue("location", out var loc) ? loc?.ToString() ?? "" : "";
                await _handler.SlayAsync(target);
                return new SkillResult
                {
                    Success = true,
                    Message = $"Auto-slay triggered for {species} level {level}"
                };
            }

            return new SkillResult { Success = false, Message = "Criteria not met for auto-slay" };
        }, "admin", "high_level_dino_detected");
    }

    /// <summary>
    /// Process user input through the full AI pipeline.
    /// </summary>
    public async Task<string> ProcessInputAsync(string input, string playerName, string playerTier, string? tribeId = null)
    {
        try
        {
            // Get or create player session
            var sessionKey = $"{playerName}:{tribeId ?? "none"}";
            if (!_sessions.TryGetValue(sessionKey, out var session))
            {
                session = new AiPlayerSession
                {
                    PlayerId = sessionKey,
                    PlayerName = playerName,
                    PlayerTier = playerTier,
                    TribeId = tribeId
                };
                _sessions[sessionKey] = session;
            }

            // Check rate limits
            if (!CheckRateLimit(session))
            {
                return "Rate limit exceeded. Please wait before sending another message.";
            }

            // Update session
            session.LastActivity = DateTime.UtcNow;
            session.MessageCount++;

            // Process through AI service (intent routing)
            var response = await _aiService.ProcessInputAsync(input, playerName, playerTier);

            // Track conversation
            session.ConversationHistory.Add(new ChatMessage
            {
                Role = "user",
                Content = input,
                Timestamp = DateTime.UtcNow
            });
            session.ConversationHistory.Add(new ChatMessage
            {
                Role = "assistant",
                Content = response,
                Timestamp = DateTime.UtcNow
            });

            // Trigger skills based on intents
            await TriggerSkillsForIntent(input, playerName, playerTier, tribeId);

            ResponseGenerated?.Invoke(this, response);
            return response;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, ex.Message);
            return $"Error processing request: {ex.Message}";
        }
    }

    /// <summary>
    /// Handle game events through skill system.
    /// </summary>
    public async Task HandleGameEventAsync(string eventType, Dictionary<string, object> eventData, string playerName, string playerTier)
    {
        var ctx = new SkillContext
        {
            PlayerId = $"{playerName}:{(eventData.TryGetValue("tribe_id", out var tid) ? tid : "none")}",
            PlayerName = playerName,
            PlayerTier = playerTier,
            TribeId = eventData.TryGetValue("tribe_id", out var t) ? t?.ToString() : null,
            EventData = eventData,
            Handler = _handler
        };

        await _skills.TriggerByEventAsync(eventType, ctx);
    }

    /// <summary>
    /// Trigger relevant skills based on message content.
    /// </summary>
    private async Task TriggerSkillsForIntent(string input, string playerName, string playerTier, string? tribeId)
    {
        var lower = input.ToLower();

        if (lower.Contains("wild") && lower.Contains("alert"))
        {
            var ctx = new SkillContext
            {
                PlayerId = $"{playerName}:{tribeId ?? "none"}",
                PlayerName = playerName,
                PlayerTier = playerTier,
                TribeId = tribeId,
                Handler = _handler
            };
            await _skills.TriggerAsync("wild_dino_alert", ctx);
        }
    }

    /// <summary>
    /// Check rate limit for player session.
    /// </summary>
    private bool CheckRateLimit(AiPlayerSession session)
    {
        var (limit, window) = session.PlayerTier.ToLower() switch
        {
            "admin" => (100, 60),
            "mod" => (50, 60),
            "vip" => (20, 60),
            _ => (10, 60)
        };

        var now = DateTime.UtcNow;
        var recentMessages = session.ConversationHistory
            .Count(m => m.Timestamp > now.AddSeconds(-window));

        return recentMessages < limit;
    }

    /// <summary>
    /// Get server status with AI overview.
    /// </summary>
    public string GetServerOverview()
    {
        var handlerStatus = _handler.QueueSize > 0
            ? $"Commands queued: {_handler.QueueSize}"
            : "Command queue empty";

        var sessionCount = _sessions.Count;
        var totalMessages = _sessions.Values.Sum(s => s.MessageCount);

        return $"DuckBot AI Server Status:\n" +
               $"- Active sessions: {sessionCount}\n" +
               $"- Total messages: {totalMessages}\n" +
               $"- {handlerStatus}\n" +
               $"- Knowledge base: {_knowledge.GetAllDinos().Count()} dinos, {_knowledge.GetAllItems().Count()} items";
    }

    /// <summary>
    /// Lookup dino information from knowledge base.
    /// </summary>
    public string LookupDinoInfo(string dinoName)
    {
        var dino = _knowledge.LookupDino(dinoName);
        if (dino == null) return $"Dino '{dinoName}' not found in knowledge base.";

        return $"**{dino.Name}**\n" +
               $"Health: {dino.BaseHealth}\n" +
               $"Attack: {dino.BaseAttack}\n" +
               $"Taming: {dino.TamingMethod} (speed: {dino.TamingSpeed})";
    }

    /// <summary>
    /// Get skill registry for UI display.
    /// </summary>
    public string GetSkillList(string tier)
    {
        var skills = _skills.GetAll();
        var filtered = skills.Where(s => HasTierAccess(tier, s.RequiredTier));

        return "Available Skills:\n" +
               string.Join("\n", filtered.Select(s => $"- {s.Name}: {s.Description}"));
    }

    private bool HasTierAccess(string playerTier, string requiredTier)
    {
        var tiers = new[] { "player", "vip", "mod", "admin" };
        var playerLevel = Array.IndexOf(tiers, playerTier.ToLower());
        var requiredLevel = Array.IndexOf(tiers, requiredTier.ToLower());
        return playerLevel >= requiredLevel && playerLevel >= 0;
    }

    /// <summary>
    /// Truncate conversation history to stay within token budget.
    /// </summary>
    public void TruncateConversation(string sessionKey, int maxMessages = 50)
    {
        if (_sessions.TryGetValue(sessionKey, out var session))
        {
            if (session.ConversationHistory.Count > maxMessages)
            {
                var toRemove = session.ConversationHistory.Count - maxMessages;
                session.ConversationHistory.RemoveRange(0, toRemove);
            }
        }
    }

    public void Dispose()
    {
        _tools.Dispose();
        _agent.Dispose();
        _skills.Dispose();
        _handler.Dispose();
        _knowledge.Dispose();
        GC.SuppressFinalize(this);
    }
}

public class AiPlayerSession
{
    public string PlayerId { get; set; } = "";
    public string PlayerName { get; set; } = "";
    public string PlayerTier { get; set; } = "player";
    public string? TribeId { get; set; }
    public DateTime LastActivity { get; set; } = DateTime.UtcNow;
    public int MessageCount { get; set; }
    public List<ChatMessage> ConversationHistory { get; set; } = new();
}

public class ChatMessage
{
    public string Role { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTime Timestamp { get; set; }
}
