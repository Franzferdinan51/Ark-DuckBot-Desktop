using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ArkDuckBot.Services;

/// <summary>
/// DuckBot Skills System - Inspired by sheldon-ai-for-ark skills architecture.
/// Skills are event-driven behaviors that can be triggered by game events or AI requests.
/// </summary>
public class DuckBotSkills : IDisposable
{
    private readonly Dictionary<string, Skill> _skills = new();
    private readonly string _skillsPath;
    private DuckBotHandler? _handler;

    public event EventHandler<SkillEventArgs>? SkillTriggered;
    public event EventHandler<string>? SkillError;

    public DuckBotSkills(string? workspacePath = null)
    {
        _skillsPath = Path.Combine(workspacePath ?? GetDefaultWorkspace(), "skills");
        Directory.CreateDirectory(_skillsPath);
        LoadSkills();
    }

    public void SetHandler(DuckBotHandler handler) => _handler = handler;

    private static string GetDefaultWorkspace() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ArkDuckBot", "workspace");

    /// <summary>
    /// Load all skills from the skills directory.
    /// </summary>
    private void LoadSkills()
    {
        if (!Directory.Exists(_skillsPath)) return;

        foreach (var dir in Directory.GetDirectories(_skillsPath))
        {
            var skillName = Path.GetFileName(dir);
            var skillMd = Path.Combine(dir, "SKILL.md");

            if (File.Exists(skillMd))
            {
                var skill = ParseSkillDefinition(skillName, skillMd);
                _skills[skillName] = skill;
            }
        }
    }

    private Skill ParseSkillDefinition(string name, string path)
    {
        var content = File.ReadAllText(path);
        var skill = new Skill { Name = name, Description = "" };

        // Parse frontmatter-like headers
        var lines = content.Split('\n');
        foreach (var line in lines)
        {
            if (line.StartsWith("## "))
                skill.Description = line[3..].Trim();
            else if (line.StartsWith("- **Tier**:"))
                skill.RequiredTier = ParseTier(line);
            else if (line.StartsWith("- **Event**:"))
                skill.TriggerEvent = line[12..].Trim().ToLower();
            else if (line.StartsWith("- **Cooldown**:"))
                skill.CooldownSeconds = ParseCooldown(line);
        }

        return skill;
    }

    private string ParseTier(string line)
    {
        if (line.Contains("VIP")) return "vip";
        if (line.Contains("Mod")) return "mod";
        if (line.Contains("Admin")) return "admin";
        return "player";
    }

    private int ParseCooldown(string line)
    {
        var parts = line.Split(' ');
        foreach (var p in parts)
            if (int.TryParse(p.TrimEnd('s'), out var secs))
                return secs;
        return 60;
    }

    /// <summary>
    /// Register a built-in skill (code-based, not file-based).
    /// </summary>
    public void RegisterBuiltin(string name, Func<SkillContext, Task<SkillResult>> handler, string tier = "player", string? triggerEvent = null)
    {
        _skills[name] = new Skill
        {
            Name = name,
            Description = $"Built-in skill: {name}",
            RequiredTier = tier,
            TriggerEvent = triggerEvent,
            Handler = handler
        };
    }

    /// <summary>
    /// Trigger a skill by name with context.
    /// </summary>
    public async Task<SkillResult> TriggerAsync(string skillName, SkillContext ctx)
    {
        if (!_skills.TryGetValue(skillName, out var skill))
            return new SkillResult { Success = false, Message = $"Skill '{skillName}' not found" };

        if (!HasTierAccess(ctx.PlayerTier, skill.RequiredTier))
            return new SkillResult { Success = false, Message = $"Insufficient tier for '{skillName}'" };

        if (skill.CooldownSeconds > 0 && IsOnCooldown(skillName, ctx.PlayerId))
            return new SkillResult { Success = false, Message = $"Skill '{skillName}' on cooldown" };

        try
        {
            SkillTriggered?.Invoke(this, new SkillEventArgs(skillName, ctx));
            var result = await skill.Handler(ctx);
            SetCooldown(skillName, ctx.PlayerId);
            return result;
        }
        catch (Exception ex)
        {
            SkillError?.Invoke(this, $"Skill '{skillName}' error: {ex.Message}");
            return new SkillResult { Success = false, Message = ex.Message };
        }
    }

    /// <summary>
    /// Trigger skills by event (e.g., "wild_dino_detected", "player_joined").
    /// </summary>
    public async Task TriggerByEventAsync(string eventType, SkillContext ctx)
    {
        var triggered = _skills.Values
            .Where(s => s.TriggerEvent == eventType.ToLower() && HasTierAccess(ctx.PlayerTier, s.RequiredTier));

        foreach (var skill in triggered)
        {
            await TriggerAsync(skill.Name, ctx);
        }
    }

    /// <summary>
    /// Get all registered skills.
    /// </summary>
    public IEnumerable<Skill> GetAll() => _skills.Values;

    private bool HasTierAccess(string playerTier, string requiredTier)
    {
        var tiers = new[] { "player", "vip", "mod", "admin" };
        var playerLevel = Array.IndexOf(tiers, playerTier.ToLower());
        var requiredLevel = Array.IndexOf(tiers, requiredTier.ToLower());
        return playerLevel >= requiredLevel;
    }

    private readonly Dictionary<string, Dictionary<string, DateTime>> _cooldowns = new();

    private bool IsOnCooldown(string skillName, string playerId)
    {
        if (!_cooldowns.TryGetValue(skillName, out var playerCds))
            return false;
        if (!playerCds.TryGetValue(playerId, out var lastUsed))
            return false;
        return (DateTime.UtcNow - lastUsed).TotalSeconds < _skills[skillName].CooldownSeconds;
    }

    private void SetCooldown(string skillName, string playerId)
    {
        if (!_cooldowns.ContainsKey(skillName))
            _cooldowns[skillName] = new Dictionary<string, DateTime>();
        _cooldowns[skillName][playerId] = DateTime.UtcNow;
    }

    public void Dispose() => GC.SuppressFinalize(this);
}

public class Skill
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string RequiredTier { get; set; } = "player";
    public string? TriggerEvent { get; set; }
    public int CooldownSeconds { get; set; } = 60;
    public Func<SkillContext, Task<SkillResult>>? Handler { get; set; }
}

public class SkillContext
{
    public string PlayerId { get; set; } = "";
    public string PlayerName { get; set; } = "";
    public string PlayerTier { get; set; } = "player";
    public string? TribeId { get; set; }
    public Dictionary<string, object> EventData { get; set; } = new();
    public DuckBotHandler? Handler { get; set; }
}

public class SkillResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public Dictionary<string, object> Data { get; set; } = new();
}

public class SkillEventArgs : EventArgs
{
    public string SkillName { get; }
    public SkillContext Context { get; }
    public SkillEventArgs(string skillName, SkillContext context)
    {
        SkillName = skillName;
        Context = context;
    }
}