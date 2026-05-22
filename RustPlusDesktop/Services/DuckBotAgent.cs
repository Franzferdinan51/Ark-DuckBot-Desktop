using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ArkDuckBot.Services;

/// <summary>
/// DuckBot Agent - An agentic AI system inspired by hermes-agent and sheldon-ai-for-ark.
/// Provides learning capabilities, skills system, and tool calling for ARK.
///
/// Key features:
/// - Learning loop: Creates skills from experience, self-improves during use
/// - Skills system: Persisted skills that can be triggered by player commands
/// - Memory system: Tracks player preferences, tribe data, learned behaviors
/// - Tool registry: Registered tools that the AI can call
/// </summary>
public class DuckBotAgent : IDisposable
{
    private readonly string _workspacePath;
    private readonly string _skillsPath;
    private readonly string _memoryPath;
    private readonly string _toolsPath;

    // Skill registry - learned behaviors persisted to disk
    private readonly Dictionary<string, Skill> _skills = new();

    // Tool registry - available AI tools
    private readonly Dictionary<string, Tool> _tools = new();

    // Memory - player preferences, tribe data, learned behaviors
    private AgentMemory _memory = new();

    // Current session context
    private SessionContext _session = new();

    public event EventHandler<string>? LogMessage;
    public event EventHandler<Skill>? SkillLearned;
    public event EventHandler<string>? ToolCalled;

    public DuckBotAgent()
    {
        _workspacePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ArkDuckBot", "agent");

        _skillsPath = Path.Combine(_workspacePath, "skills");
        _memoryPath = Path.Combine(_workspacePath, "memory.json");
        _toolsPath = Path.Combine(_workspacePath, "tools.json");

        EnsureDirectories();
        LoadMemory();
        LoadSkills();
    }

    private void EnsureDirectories()
    {
        Directory.CreateDirectory(_workspacePath);
        Directory.CreateDirectory(_skillsPath);
    }

    #region Skills System (hermes-agent inspired)

    /// <summary>
    /// Register a new skill or update existing one.
    /// Skills are triggered by player commands and can chain into complex behaviors.
    /// </summary>
    public void RegisterSkill(string name, string description, Func<SkillContext, Task<string>> action, string[]? triggers = null)
    {
        var skill = new Skill
        {
            Name = name,
            Description = description,
            Action = action,
            Triggers = triggers ?? new[] { name },
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
    /// Try to invoke a skill by name or trigger.
    /// </summary>
    public async Task<string> InvokeSkillAsync(string trigger, SkillContext context)
    {
        var skill = _skills.Values.FirstOrDefault(s =>
            s.Triggers.Contains(trigger, StringComparer.OrdinalIgnoreCase));

        if (skill == null)
            return $"No skill found for trigger: {trigger}";

        try
        {
            skill.UseCount++;
            var result = await skill.Action(context);
            skill.SuccessCount++;
            skill.LastUsedAt = DateTime.UtcNow;
            SaveSkill(skill);
            ToolCalled?.Invoke(this, skill.Name);
            return result;
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke(this, $"Skill {skill.Name} failed: {ex.Message}");
            return $"Skill execution failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Learn a new skill from player interaction (hermes-agent learning loop).
    /// Called when a complex task is successfully completed.
    /// </summary>
    public void LearnSkill(string name, string description, string[] steps, string[]? triggers = null)
    {
        var skill = new Skill
        {
            Name = name,
            Description = description,
            Triggers = triggers ?? new[] { name.ToLower() },
            Steps = steps,
            IsLearned = true,
            CreatedAt = DateTime.UtcNow
        };

        _skills[name] = skill;
        SaveSkill(skill);
        SkillLearned?.Invoke(this, skill);
        LogMessage?.Invoke(this, $"Learned new skill: {name}");
    }

    /// <summary>
    /// Get all skills as summary for AI context.
    /// </summary>
    public string GetSkillsSummary()
    {
        if (_skills.Count == 0)
            return "No skills learned yet.";

        var lines = _skills.Values.Select(s =>
            $"- {s.Name}: {s.Description} (used {s.UseCount}x, success {s.SuccessCount}x)");
        return $"Available skills:\n{string.Join("\n", lines)}";
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
                    var skill = JsonSerializer.Deserialize<Skill>(json);
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

    private void SaveSkill(Skill skill)
    {
        try
        {
            var file = Path.Combine(_skillsPath, $"{skill.Name}.json");
            var json = JsonSerializer.Serialize(skill, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(file, json);
        }
        catch { }
    }

    #endregion

    #region Memory System

    /// <summary>
    /// Remember information about a player.
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
            return (T)value;
        }
        return default;
    }

    /// <summary>
    /// Track a player's command usage for learning.
    /// </summary>
    public void TrackCommandUsage(string playerId, string command, bool success)
    {
        var key = $"{playerId}:{command}";
        if (!_memory.CommandUsage.ContainsKey(key))
            _memory.CommandUsage[key] = new CommandUsageStats();

        var stats = _memory.CommandUsage[key];
        stats.Total++;
        if (success) stats.Successes++;

        // Learn if player struggles with a command after 3+ failures
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

    #region Tool Registry

    /// <summary>
    /// Register a tool that the AI can call.
    /// Tools are functions the agent can invoke to interact with ARK.
    /// </summary>
    public void RegisterTool(string name, string description, string[] paramNames, Func<Dictionary<string, object>, Task<object>> action)
    {
        _tools[name] = new Tool
        {
            Name = name,
            Description = description,
            ParameterNames = paramNames,
            Action = action
        };
        SaveTools();
    }

    /// <summary>
    /// Get all available tools as summary for AI context.
    /// </summary>
    public string GetToolsSummary()
    {
        if (_tools.Count == 0)
            return "No tools available.";

        var lines = _tools.Values.Select(t =>
            $"- {t.Name}: {t.Description} (params: {string.Join(", ", t.ParameterNames)})");
        return $"Available tools:\n{string.Join("\n", lines)}";
    }

    /// <summary>
    /// Invoke a tool by name.
    /// </summary>
    public async Task<object?> InvokeToolAsync(string name, Dictionary<string, object> parameters)
    {
        if (!_tools.TryGetValue(name, out var tool))
            return new { error = $"Unknown tool: {name}" };

        try
        {
            ToolCalled?.Invoke(this, tool.Name);
            return await tool.Action(parameters);
        }
        catch (Exception ex)
        {
            return new { error = ex.Message };
        }
    }

    private void SaveTools()
    {
        try
        {
            var serializable = _tools.ToDictionary(
                kvp => kvp.Key,
                kvp => new { kvp.Value.Name, kvp.Value.Description, kvp.Value.ParameterNames });
            var json = JsonSerializer.Serialize(serializable, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_toolsPath, json);
        }
        catch { }
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
    }

    public SessionContext GetSession() => _session;

    #endregion

    public void Dispose()
    {
        SaveMemory();
        GC.SuppressFinalize(this);
    }
}

#region Supporting Types

public class Skill
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string[] Triggers { get; set; } = Array.Empty<string>();
    public string[]? Steps { get; set; }
    public bool IsLearned { get; set; }
    public int UseCount { get; set; }
    public int SuccessCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }

    // Runtime - not persisted
    [JsonIgnore]
    public Func<SkillContext, Task<string>>? Action { get; set; }
}

public class SkillContext
{
    public string PlayerId { get; set; } = "";
    public string PlayerName { get; set; } = "";
    public string PlayerTier { get; set; } = "user";
    public string? TribeId { get; set; }
    public string Message { get; set; } = "";
    public Dictionary<string, object> Metadata { get; set; } = new();
    public McpBridgeClient? McpBridge { get; set; }
}

public class Tool
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string[] ParameterNames { get; set; } = Array.Empty<string>();

    [JsonIgnore]
    public Func<Dictionary<string, object>, Task<object>>? Action { get; set; }
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
    public string PlayerTier { get; set; } = "user";
    public string? TribeId { get; set; }
}

#endregion