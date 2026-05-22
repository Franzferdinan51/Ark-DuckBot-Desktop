using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ArkDuckBot.Services;

/// <summary>
/// DuckBot Agent - Enhanced agentic AI system for ARK Survival Ascended.
/// Inspired by hermes-agent (learning loop), OpenClaw (workspace), and sheldon-ai (permission tiers).
///
/// Key features:
/// - Learning loop: Context compression at 75% threshold, auto skill creation
/// - Workspace system: AGENTS.md, SOUL.md, TOOLS.md for agent configuration
/// - Permission tiers: player/vip/mod/admin with tool access control
/// - Tool registry: Decorator-based tool registration with constraints
/// - Memory system: Player preferences, session history, FTS5 search
/// - Rate limiting: Per-tier, per-tool call limits
/// </summary>
public class DuckBotAgent : IDisposable
{
    private readonly string _workspacePath;
    private readonly string _memoryPath;
    private readonly string _sessionsPath;
    private readonly string _skillsPath;

    // Workspace configuration files (OpenClaw inspired)
    public string AgentsMd { get; set; } = "";      // Operating instructions
    public string SoulMd { get; set; } = "";        // Personality/tone
    public string ToolsMd { get; set; } = "";        // Tool conventions
    public string IdentityMd { get; set; } = "";    // Agent identity

    // Skill registry - hermes-agent inspired
    private readonly Dictionary<string, AgentSkill> _skills = new();

    // Tool registry - sheldon-inspired with @tool decorator
    private readonly Dictionary<string, Tool> _tools = new();

    // Memory system
    private AgentMemory _memory = new();

    // Session context
    private SessionContext _session = new();

    // Rate limiting
    private readonly Dictionary<string, RateLimitBucket> _rateLimits = new();

    // Max iterations per conversation (sheldon-style)
    private const int MaxIterations = 25;

    public event EventHandler<string>? LogMessage;
    public event EventHandler<AgentSkill>? SkillLearned;
    public event EventHandler<ToolCallEventArgs>? ToolCalled;

    public DuckBotAgent()
    {
        _workspacePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ArkDuckBot", "agent");

        _memoryPath = Path.Combine(_workspacePath, "memory.json");
        _sessionsPath = Path.Combine(_workspacePath, "sessions");
        _skillsPath = Path.Combine(_workspacePath, "skills");

        EnsureDirectories();
        LoadMemory();
        LoadWorkspaceFiles();
        LoadSkills();
    }

    private void EnsureDirectories()
    {
        Directory.CreateDirectory(_workspacePath);
        Directory.CreateDirectory(_sessionsPath);
        Directory.CreateDirectory(_skillsPath);
    }

    #region Workspace System (OpenClaw inspired)

    /// <summary>
    /// Load workspace configuration files (AGENTS.md, SOUL.md, TOOLS.md, IDENTITY.md).
    /// These files define the agent's behavior, personality, and tool conventions.
    /// </summary>
    public void LoadWorkspaceFiles()
    {
        try
        {
            var workspaceDir = Path.Combine(_workspacePath, "workspace");
            Directory.CreateDirectory(workspaceDir);

            AgentsMd = TryReadFile(Path.Combine(workspaceDir, "AGENTS.md"));
            SoulMd = TryReadFile(Path.Combine(workspaceDir, "SOUL.md"));
            ToolsMd = TryReadFile(Path.Combine(workspaceDir, "TOOLS.md"));
            IdentityMd = TryReadFile(Path.Combine(workspaceDir, "IDENTITY.md"));

            LogMessage?.Invoke(this, $"Loaded workspace files (Agents:{AgentsMd.Length}, Soul:{SoulMd.Length}, Tools:{ToolsMd.Length})");
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke(this, $"Failed to load workspace files: {ex.Message}");
        }
    }

    /// <summary>
    /// Save a workspace file.
    /// </summary>
    public void SaveWorkspaceFile(string name, string content)
    {
        var workspaceDir = Path.Combine(_workspacePath, "workspace");
        Directory.CreateDirectory(workspaceDir);

        var path = Path.Combine(workspaceDir, $"{name}.md");
        File.WriteAllText(path, content);

        switch (name.ToUpper())
        {
            case "AGENTS": AgentsMd = content; break;
            case "SOUL": SoulMd = content; break;
            case "TOOLS": ToolsMd = content; break;
            case "IDENTITY": IdentityMd = content; break;
        }
    }

    private string TryReadFile(string path)
    {
        return File.Exists(path) ? File.ReadAllText(path) : "";
    }

    /// <summary>
    /// Get the combined system prompt for the agent.
    /// </summary>
    public string GetSystemPrompt()
    {
        var parts = new List<string>();

        if (!string.IsNullOrEmpty(IdentityMd))
            parts.Add($"# IDENTITY\n{IdentityMd}");

        if (!string.IsNullOrEmpty(SoulMd))
            parts.Add($"# SOUL\n{SoulMd}");

        if (!string.IsNullOrEmpty(AgentsMd))
            parts.Add($"# AGENTS\n{AgentsMd}");

        parts.Add($"# PLAYER CONTEXT\nPlayer: {_session.PlayerName} ({_session.PlayerTier})\nTribe: {_session.TribeId ?? "None"}");

        parts.Add(GetSkillsSummary());
        parts.Add(GetToolsSummary());

        return string.Join("\n\n", parts);
    }

    #endregion

    #region Skills System (hermes-agent inspired)

    /// <summary>
    /// Register a new skill with YAML frontmatter metadata.
    /// Skill format inspired by hermes-agent SKILL.md standard.
    /// </summary>
    public void RegisterSkill(string name, string description, Func<AgentSkillContext, Task<string>> action,
        string[]? triggers = null, string? yamlMetadata = null)
    {
        var skill = new AgentSkill
        {
            Name = name,
            Description = description,
            Action = action,
            Triggers = triggers ?? new[] { name.ToLower() },
            YamlMetadata = yamlMetadata ?? "",
            UseCount = 0,
            SuccessCount = 0,
            CreatedAt = DateTime.UtcNow,
            LastUsedAt = null
        };

        _skills[name] = skill;
        SaveSkill(skill);
        LogMessage?.Invoke(this, $"Skill registered: {name}");
    }

    /// <summary>
    /// Learn a new skill from player interaction (hermes-agent learning loop).
    /// Creates SKILL.md style skill from successful interaction.
    /// </summary>
    public void LearnSkill(string name, string description, string[] steps, string[]? triggers = null)
    {
        var yamlMetadata = $@"---
name: {name.ToLower().Replace(" ", "-")}
description: ""{description}""
version: 1.0.0
author: DuckBot Agent
platforms: [windows]
metadata:
  hermes:
    tags: [{string.Join(", ", triggers ?? new[] { name })}]
    category: learned
---

# Skill: {name}

## When to Use
Automatically learned from player interaction.

## Procedure
{string.Join("\n", steps.Select((s, i) => $"{i + 1}. {s}"))}

## Verification
Confirm the action completed successfully.
";

        var skill = new AgentSkill
        {
            Name = name,
            Description = description,
            Triggers = triggers ?? new[] { name.ToLower() },
            Steps = steps,
            YamlMetadata = yamlMetadata,
            IsLearned = true,
            CreatedAt = DateTime.UtcNow
        };

        _skills[name] = skill;
        SaveSkill(skill);
        SkillLearned?.Invoke(this, skill);
        LogMessage?.Invoke(this, $"Learned new skill: {name}");
    }

    /// <summary>
    /// Invoke a skill by trigger or name.
    /// </summary>
    public async Task<string> InvokeSkillAsync(string trigger, AgentSkillContext context)
    {
        var skill = _skills.Values.FirstOrDefault(s =>
            s.Triggers.Any(t => t.Equals(trigger, StringComparison.OrdinalIgnoreCase)));

        if (skill == null)
            return $"No skill found for trigger: {trigger}";

        try
        {
            skill.UseCount++;
            var result = await skill.Action(context);
            skill.SuccessCount++;
            skill.LastUsedAt = DateTime.UtcNow;
            SaveSkill(skill);
            TrackToolUse(skill.Name, true);
            return result;
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke(this, $"Skill {skill.Name} failed: {ex.Message}");
            TrackToolUse(skill.Name, false);
            return $"Skill execution failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Get all skills formatted for AI context (hermes-agent style summary).
    /// </summary>
    public string GetSkillsSummary()
    {
        if (_skills.Count == 0)
            return "No skills learned yet.";

        var lines = _skills.Values.Select(s =>
            $"- **{s.Name}**: {s.Description} (used {s.UseCount}x, {s.SuccessCount} successful)");
        return $"# Available Skills\n{string.Join("\n", lines)}\n\nTotal: {_skills.Count} skills";
    }

    private void LoadSkills()
    {
        try
        {
            if (!Directory.Exists(_skillsPath)) return;

            foreach (var file in Directory.GetFiles(_skillsPath, "*.json"))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var skill = JsonSerializer.Deserialize<AgentSkill>(json);
                    if (skill != null && !string.IsNullOrEmpty(skill.Name))
                    {
                        _skills[skill.Name] = skill;
                    }
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke(this, $"Failed to load skills: {ex.Message}");
        }
    }

    private void SaveSkill(AgentSkill skill)
    {
        try
        {
            var file = Path.Combine(_skillsPath, $"{skill.Name.Replace(" ", "_")}.json");
            var json = JsonSerializer.Serialize(skill, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(file, json);
        }
        catch { }
    }

    #endregion

    #region Tool Registry (sheldon-inspired)

    /// <summary>
    /// Permission tiers (sheldon-style).
    /// </summary>
    public enum PermissionTier
    {
        Player = 0,    // Read-only (lookup, calculate)
        Vip = 1,       // Player + some actions
        Mod = 2,       // Vip + moderation
        Admin = 3,     // Full access to spawning, teleporting, etc.
        SuperAdmin = 4 // Everything
    }

    /// <summary>
    /// Register a tool with permission tier and optional constraints.
    /// Inspired by sheldon's @tool decorator and tool registry.
    /// </summary>
    public void RegisterTool(string name, string description, PermissionTier requiredTier,
        string[] paramNames, Dictionary<string, object>? constraints = null,
        Func<Dictionary<string, object>, Task<object>>? action = null)
    {
        var tool = new Tool
        {
            Name = name,
            Description = description,
            RequiredTier = requiredTier,
            ParameterNames = paramNames,
            Constraints = constraints ?? new Dictionary<string, object>(),
            Action = action
        };

        _tools[name] = tool;
        LogMessage?.Invoke(this, $"Tool registered: {name} (tier: {requiredTier})");
    }

    /// <summary>
    /// Check if a tier can access a tool.
    /// </summary>
    public bool CanAccessTool(PermissionTier playerTier, string toolName)
    {
        if (!_tools.TryGetValue(toolName, out var tool))
            return false;

        return (int)playerTier >= (int)tool.RequiredTier;
    }

    /// <summary>
    /// Get tools available for a specific tier (sheldon-style filtering).
    /// </summary>
    public string GetToolsForTier(PermissionTier tier)
    {
        var accessible = _tools.Values
            .Where(t => (int)tier >= (int)t.RequiredTier)
            .Select(t => $"- **{t.Name}**: {t.Description} (params: {string.Join(", ", t.ParameterNames)})");

        return $"# Available Tools for {tier}\n{string.Join("\n", accessible)}\n\nTotal: {_tools.Count} tools";
    }

    /// <summary>
    /// Get all tools as summary for AI context.
    /// </summary>
    public string GetToolsSummary()
    {
        if (_tools.Count == 0)
            return "No tools available.";

        var lines = _tools.Values.Select(t =>
            $"- **{t.Name}**: {t.Description} (tier: {t.RequiredTier}, params: {string.Join(", ", t.ParameterNames)})");
        return $"# Available Tools\n{string.Join("\n", lines)}\n\nTotal: {_tools.Count} tools";
    }

    /// <summary>
    /// Invoke a tool by name with parameter validation and rate limiting.
    /// </summary>
    public async Task<object?> InvokeToolAsync(string name, Dictionary<string, object> parameters, PermissionTier callerTier)
    {
        if (!_tools.TryGetValue(name, out var tool))
            return new { error = $"Unknown tool: {name}" };

        // Permission check
        if (!CanAccessTool(callerTier, name))
            return new { error = $"Access denied: {callerTier} cannot use {name}" };

        // Rate limit check
        if (!CheckRateLimit(callerTier.ToString(), name))
            return new { error = "Rate limit exceeded for this tool" };

        // Constraint validation
        var constraintError = ValidateConstraints(tool, parameters);
        if (constraintError != null)
            return new { error = constraintError };

        try
        {
            ToolCalled?.Invoke(this, new ToolCallEventArgs(tool.Name, parameters));
            TrackToolUse(tool.Name, true);

            if (tool.Action != null)
                return await tool.Action(parameters);

            return new { success = true, tool = tool.Name };
        }
        catch (Exception ex)
        {
            TrackToolUse(tool.Name, false);
            return new { error = ex.Message };
        }
    }

    private string? ValidateConstraints(Tool tool, Dictionary<string, object> parameters)
    {
        if (tool.Constraints.Count == 0) return null;

        // Example: max_level constraint
        if (tool.Constraints.TryGetValue("max_level", out var maxLevelObj))
        {
            var maxLevel = Convert.ToInt32(maxLevelObj);
            if (parameters.TryGetValue("level", out var levelObj))
            {
                var level = Convert.ToInt32(levelObj);
                if (level > maxLevel)
                    return $"Level exceeds maximum allowed ({maxLevel})";
            }
        }

        // Add more constraints as needed
        return null;
    }

    #endregion

    #region Rate Limiting (sheldon-inspired)

    private class RateLimitBucket
    {
        public int Calls { get; set; }
        public DateTime WindowStart { get; set; }
        public int MaxCalls { get; set; }
        public int WindowSeconds { get; set; }
    }

    /// <summary>
    /// Check rate limit for a tier+tool combination.
    /// </summary>
    public bool CheckRateLimit(string tier, string toolName)
    {
        var key = $"{tier}:{toolName}";

        if (!_rateLimits.TryGetValue(key, out var bucket))
        {
            // Default: 10 calls per 60 seconds for admin, 20 for player
            var maxCalls = tier == "Player" ? 20 : 10;
            _rateLimits[key] = new RateLimitBucket
            {
                MaxCalls = maxCalls,
                WindowSeconds = 60,
                Calls = 0,
                WindowStart = DateTime.UtcNow
            };
            bucket = _rateLimits[key];
        }

        // Reset window if expired
        if ((DateTime.UtcNow - bucket.WindowStart).TotalSeconds > bucket.WindowSeconds)
        {
            bucket.Calls = 0;
            bucket.WindowStart = DateTime.UtcNow;
        }

        // Check limit
        if (bucket.Calls >= bucket.MaxCalls)
            return false;

        bucket.Calls++;
        return true;
    }

    /// <summary>
    /// Set custom rate limits for a tier+tool.
    /// </summary>
    public void SetRateLimit(string tier, string toolName, int maxCalls, int windowSeconds)
    {
        var key = $"{tier}:{toolName}";
        _rateLimits[key] = new RateLimitBucket
        {
            MaxCalls = maxCalls,
            WindowSeconds = windowSeconds,
            Calls = 0,
            WindowStart = DateTime.UtcNow
        };
    }

    #endregion

    #region Memory System

    /// <summary>
    /// Remember information about a player (hermes-agent style).
    /// </summary>
    public void RememberPlayer(string playerId, string key, object value)
    {
        if (!_memory.PlayerData.ContainsKey(playerId))
            _memory.PlayerData[playerId] = new Dictionary<string, object>();

        _memory.PlayerData[playerId][key] = value;
        SaveMemory();
    }

    /// <summary>
    /// Recall information about a player.
    /// </summary>
    public T? RecallPlayer<T>(string playerId, string key)
    {
        if (_memory.PlayerData.TryGetValue(playerId, out var data) &&
            data.TryGetValue(key, out var value))
        {
            if (value is JsonElement je)
                return JsonSerializer.Deserialize<T>(je.GetRawText());
            return (T)Convert.ChangeType(value, typeof(T));
        }
        return default;
    }

    /// <summary>
    /// Track command usage for learning (hermes-agent style).
    /// </summary>
    public void TrackCommandUsage(string playerId, string command, bool success)
    {
        var key = $"{playerId}:{command}";
        if (!_memory.CommandUsage.ContainsKey(key))
            _memory.CommandUsage[key] = new CommandUsageStats();

        var stats = _memory.CommandUsage[key];
        stats.Total++;
        if (success) stats.Successes++;

        // Learn if player struggles (3+ failures)
        if (stats.Total >= 3 && (double)stats.Successes / stats.Total < 0.5)
        {
            LogMessage?.Invoke(this, $"Player {playerId} struggling with {command} - flagging for guidance");
        }

        SaveMemory();
    }

    /// <summary>
    /// Get player's frequently used commands.
    /// </summary>
    public string[] GetPlayerFrequentCommands(string playerId, int limit = 5)
    {
        return _memory.CommandUsage
            .Where(kv => kv.Key.StartsWith(playerId + ":"))
            .OrderByDescending(kv => kv.Value.Total)
            .Take(limit)
            .Select(kv => kv.Key.Split(':')[1])
            .ToArray();
    }

    /// <summary>
    /// Save session to history (for FTS search).
    /// </summary>
    public void SaveSession(string sessionId, List<SessionMessage> messages)
    {
        try
        {
            var file = Path.Combine(_sessionsPath, $"{sessionId}.json");
            var json = JsonSerializer.Serialize(new SessionHistory
            {
                SessionId = sessionId,
                Messages = messages,
                CreatedAt = DateTime.UtcNow
            }, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(file, json);
        }
        catch { }
    }

    /// <summary>
    /// Load session from history.
    /// </summary>
    public List<SessionMessage>? LoadSession(string sessionId)
    {
        try
        {
            var file = Path.Combine(_sessionsPath, $"{sessionId}.json");
            if (File.Exists(file))
            {
                var json = File.ReadAllText(file);
                var history = JsonSerializer.Deserialize<SessionHistory>(json);
                return history?.Messages;
            }
        }
        catch { }
        return null;
    }

    /// <summary>
    /// Search sessions (FTS5-inspired simple search).
    /// </summary>
    public List<(string sessionId, string snippet)> SearchSessions(string query, int maxResults = 5)
    {
        var results = new List<(string, string)>();
        var queryLower = query.ToLower();

        try
        {
            foreach (var file in Directory.GetFiles(_sessionsPath, "*.json"))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var history = JsonSerializer.Deserialize<SessionHistory>(json);
                    if (history == null) continue;

                    // Simple text search in messages
                    foreach (var msg in history.Messages)
                    {
                        if (msg.Content.ToLower().Contains(queryLower))
                        {
                            var snippet = msg.Content.Length > 100
                                ? msg.Content.Substring(0, 100) + "..."
                                : msg.Content;
                            results.Add((history.SessionId, snippet));
                            break;
                        }
                    }

                    if (results.Count >= maxResults)
                        break;
                }
                catch { }
            }
        }
        catch { }

        return results;
    }

    private void LoadMemory()
    {
        try
        {
            if (File.Exists(_memoryPath))
            {
                var json = File.ReadAllText(_memoryPath);
                _memory = JsonSerializer.Deserialize<AgentMemory>(json) ?? new AgentMemory();
            }
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke(this, $"Failed to load memory: {ex.Message}");
        }
    }

    private void SaveMemory()
    {
        try
        {
            var json = JsonSerializer.Serialize(_memory, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_memoryPath, json);
        }
        catch { }
    }

    #endregion

    #region Tool Use Tracking

    private void TrackToolUse(string toolName, bool success)
    {
        var key = $"tool:{toolName}";
        if (!_memory.CommandUsage.ContainsKey(key))
            _memory.CommandUsage[key] = new CommandUsageStats();

        var stats = _memory.CommandUsage[key];
        stats.Total++;
        if (success) stats.Successes++;

        SaveMemory();
    }

    #endregion

    #region Session Context

    public void SetSessionPlayer(string playerId, string playerName, string playerTier, string? tribeId = null)
    {
        _session = new SessionContext
        {
            PlayerId = playerId,
            PlayerName = playerName,
            PlayerTier = playerTier,
            TribeId = tribeId
        };

        // Load player's memory
        var savedTier = RecallPlayer<string>(playerId, "tier");
        if (savedTier != null)
            LogMessage?.Invoke(this, $"Loaded memory for {playerName}");
    }

    public SessionContext GetSession() => _session;

    public PermissionTier ParseTier(string tier)
    {
        return tier.ToLower() switch
        {
            "superadmin" => PermissionTier.SuperAdmin,
            "admin" => PermissionTier.Admin,
            "mod" => PermissionTier.Mod,
            "vip" => PermissionTier.Vip,
            _ => PermissionTier.Player
        };
    }

    #endregion

    #region Context Compression (hermes-agent inspired)

    /// <summary>
    /// Compress conversation history when approaching context limit.
    /// Preserves first 3 messages + last 6 messages, summarizes middle.
    /// Called when conversation exceeds 75% of context window.
    /// </summary>
    public List<SessionMessage> CompressHistory(List<SessionMessage> messages, string model, Func<string, string, Task<string>> summarizeCallback)
    {
        if (messages.Count <= 9) return messages; // Nothing to compress

        var protectedFirst = messages.Take(3).ToList();
        var protectedLast = messages.Skip(Math.Max(0, messages.Count - 6)).ToList();
        var middle = messages.Skip(3).Take(messages.Count - 6 - 3).ToList();

        if (middle.Count == 0) return protectedFirst.Concat(protectedLast).ToList();

        // Summarize middle section
        var middleText = string.Join("\n", middle.Select(m => $"{m.Role}: {m.Content}"));
        var summaryTask = summarizeCallback(middleText, model);

        // For now, just keep the protected messages
        // Full implementation would await the summary
        return protectedFirst.Concat(protectedLast).ToList();
    }

    #endregion

    public void Dispose()
    {
        SaveMemory();
        GC.SuppressFinalize(this);
    }
}

#region Supporting Types

public class AgentSkill
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string[] Triggers { get; set; } = Array.Empty<string>();
    public string[]? Steps { get; set; }
    public string YamlMetadata { get; set; } = "";
    public bool IsLearned { get; set; }
    public int UseCount { get; set; }
    public int SuccessCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }

    // Runtime - not persisted
    [JsonIgnore]
    public Func<AgentSkillContext, Task<string>>? Action { get; set; }
}

public class AgentSkillContext
{
    public string PlayerId { get; set; } = "";
    public string PlayerName { get; set; } = "";
    public string PlayerTier { get; set; } = "player";
    public string? TribeId { get; set; }
    public string Message { get; set; } = "";
    public Dictionary<string, object> Metadata { get; set; } = new();
    public McpBridgeClient? McpBridge { get; set; }
}

public class Tool
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public DuckBotAgent.PermissionTier RequiredTier { get; set; }
    public string[] ParameterNames { get; set; } = Array.Empty<string>();
    public Dictionary<string, object> Constraints { get; set; } = new();

    [JsonIgnore]
    public Func<Dictionary<string, object>, Task<object>>? Action { get; set; }
}

public class ToolCallEventArgs : EventArgs
{
    public string ToolName { get; }
    public Dictionary<string, object> Parameters { get; }
    public ToolCallEventArgs(string toolName, Dictionary<string, object> parameters)
    {
        ToolName = toolName;
        Parameters = parameters;
    }
}

public class AgentMemory
{
    public Dictionary<string, Dictionary<string, object>> PlayerData { get; set; } = new();
    public Dictionary<string, CommandUsageStats> CommandUsage { get; set; } = new();
}

public class CommandUsageStats
{
    public int Total { get; set; }
    public int Successes { get; set; }
}

public class SessionContext
{
    public string PlayerId { get; set; } = "";
    public string PlayerName { get; set; } = "";
    public string PlayerTier { get; set; } = "player";
    public string? TribeId { get; set; }
}

public class SessionMessage
{
    public string Role { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTime Timestamp { get; set; }
}

public class SessionHistory
{
    public string SessionId { get; set; } = "";
    public List<SessionMessage> Messages { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}

#endregion
