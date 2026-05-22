using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ArkDuckBot.Services;

/// <summary>
/// DuckBot Knowledge Base - ARK Encyclopedia with dino stats, taming info, and recipes.
/// Inspired by sheldon-ai-for-ark knowledge.py module.
///
/// Provides fuzzy search with nickname/alias support for dinosaurs and items.
/// </summary>
public class DuckBotKnowledge : IDisposable
{
    private readonly Dictionary<string, DinoData> _dinoDb = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ItemData> _itemDb = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _dinoAliases = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _itemAliases = new(StringComparer.OrdinalIgnoreCase);

    private readonly string _dataPath;

    public DuckBotKnowledge(string? dataPath = null)
    {
        _dataPath = dataPath ?? GetDefaultDataPath();
        Directory.CreateDirectory(_dataPath);
        LoadData();
    }

    private static string GetDefaultDataPath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ArkDuckBot", "data");

    /// <summary>
    /// Load dino and item data from JSON files.
    /// </summary>
    private void LoadData()
    {
        LoadDinoData();
        LoadItemData();
    }

    private void LoadDinoData()
    {
        var dinoFile = Path.Combine(_dataPath, "dinos.json");
        if (File.Exists(dinoFile))
        {
            try
            {
                var json = File.ReadAllText(dinoFile);
                var dinos = JsonSerializer.Deserialize<List<DinoData>>(json);
                if (dinos != null)
                {
                    foreach (var dino in dinos)
                    {
                        _dinoDb[dino.Name] = dino;
                        // Build aliases
                        foreach (var alias in dino.Aliases ?? Array.Empty<string>())
                            _dinoAliases[alias] = dino.Name;
                    }
                }
            }
            catch { /* Ignore corrupted data files */ }
        }
        else
        {
            // Initialize with common ARK dinos
            InitializeDefaultDinos();
        }
    }

    private void LoadItemData()
    {
        var itemFile = Path.Combine(_dataPath, "items.json");
        if (File.Exists(itemFile))
        {
            try
            {
                var json = File.ReadAllText(itemFile);
                var items = JsonSerializer.Deserialize<List<ItemData>>(json);
                if (items != null)
                {
                    foreach (var item in items)
                    {
                        _itemDb[item.Name] = item;
                        foreach (var alias in item.Aliases ?? Array.Empty<string>())
                            _itemAliases[alias] = item.Name;
                    }
                }
            }
            catch { /* Ignore corrupted data files */ }
        }
        else
        {
            InitializeDefaultItems();
        }
    }

    private void InitializeDefaultDinos()
    {
        var defaultDinos = new[]
        {
            new DinoData { Name = "Rex", Blueprint = "Blueprint'/Game/PrimalEarth/Dinos/Rex/Rex_Character_BP.Rex_Character_BP'", BaseHealth = 700, BaseAttack = 200, TamingMethod = "Kibble", TamingSpeed = 45.0 },
            new DinoData { Name = "Giganotosaurus", Blueprint = "Blueprint'/Game/PrimalEarth/Dinos/Giganotosaurus/Giganotosaurus_Character_BP.Giganotosaurus_Character_BP'", BaseHealth = 850, BaseAttack = 250, TamingMethod = "Kibble", TamingSpeed = 60.0 },
            new DinoData { Name = "Megalodon", Blueprint = "Blueprint'/Game/PrimalEarth/Dinos/Megalodon/Megalodon_Character_BP.Megalodon_Character_BP'", BaseHealth = 400, BaseAttack = 80, TamingMethod = "Raw Meat", TamingSpeed = 20.0 },
            new DinoData { Name = "Argentavis", Blueprint = "Blueprint'/Game/PrimalEarth/Dinos/Argentavis/Argentavis_Character_BP.Argies_Character_BP'", BaseHealth = 365, BaseAttack = 65, TamingMethod = "Kibble", TamingSpeed = 25.0 },
            new DinoData { Name = "Therizinosaurus", Blueprint = "Blueprint'/Game/PrimalEarth/Dinos/Therizinosaurus/Therizinosaurus_Character_BP.Therizinosaurus_Character_BP'", BaseHealth = 420, BaseAttack = 45, TamingMethod = "Kibble", TamingSpeed = 30.0 },
            new DinoData { Name = "Yutyrannus", Blueprint = "Blueprint'/Game/PrimalEarth/Dinos/Yutyrannus/Yutyrannus_Character_BP.Yutyrannus_Character_BP'", BaseHealth = 500, BaseAttack = 120, TamingMethod = "Kibble", TamingSpeed = 35.0 },
            new DinoData { Name = "Spinosaurus", Blueprint = "Blueprint'/Game/PrimalEarth/Dinos/Spino/Spino_Character_BP.Spino_Character_BP'", BaseHealth = 600, BaseAttack = 150, TamingMethod = "Kibble", TamingSpeed = 40.0 },
            new DinoData { Name = "Titanosaurs", Blueprint = "Blueprint'/Game/PrimalEarth/Dinos/Titanosaur/Titanosaur_Character_BP.Titanosaur_Character_BP'", BaseHealth = 3000, BaseAttack = 300, TamingMethod = "Kibble", TamingSpeed = 120.0 },
        };

        foreach (var dino in defaultDinos)
        {
            dino.Aliases = GetDefaultAliases(dino.Name);
            _dinoDb[dino.Name] = dino;
        }

        // Common aliases
        _dinoAliases["rex"] = "Rex";
        _dinoAliases["t-rex"] = "Rex";
        _dinoAliases["trex"] = "Rex";
        _dinoAliases["giga"] = "Giganotosaurus";
        _dinoAliases["giganoto"] = "Giganotosaurus";
        _dinoAliases["mega"] = "Megalodon";
        _dinoAliases["megalodon"] = "Megalodon";
        _dinoAliases["argy"] = "Argentavis";
        _dinoAliases["argentavis"] = "Argentavis";
        _dinoAliases["yuty"] = "Yutyrannus";
        _dinoAliases["yutyrannus"] = "Yutyrannus";
        _dinoAliases["therizino"] = "Therizinosaurus";
        _dinoAliases["theriz"] = "Therizinosaurus";
        _dinoAliases["spino"] = "Spinosaurus";
    }

    private string[] GetDefaultAliases(string name) => name.ToLower() switch
    {
        "Rex" => new[] { "rex", "t-rex", "trex" },
        "Giganotosaurus" => new[] { "giga", "giganoto" },
        "Megalodon" => new[] { "mega", "megalodon" },
        "Argentavis" => new[] { "argy", "argentavis" },
        _ => Array.Empty<string>()
    };

    private void InitializeDefaultItems()
    {
        var defaultItems = new[]
        {
            new ItemData { Name = "Kibble", Blueprint = "Blueprint'/Game/PrimalEarth/Test/Kibble_Base.Kibble_Base'", StackSize = 100, Weight = 0.5 },
            new ItemData { Name = "Raw Meat", Blueprint = "Blueprint'/Game/PrimalEarth/Test/RawMeat.RawMeat'", StackSize = 100, Weight = 0.1 },
            new ItemData { Name = "Cooked Meat", Blueprint = "Blueprint'/Game/PrimalEarth/Test/PrimeMeat.PrimeMeat'", StackSize = 100, Weight = 0.1 },
            new ItemData { Name = "Metal Ingot", Blueprint = "Blueprint'/Game/PrimalEarth/Resources/PrimalItemResource_MetalIngot.PrimalItemResource_MetalIngot'", StackSize = 100, Weight = 1.0 },
            new ItemData { Name = "Gunpowder", Blueprint = "Blueprint'/Game/PrimalEarth/Resources/PrimalItemResource_Gunpowder.PrimalItemResource_Gunpowder'", StackSize = 100, Weight = 0.5 },
        };

        foreach (var item in defaultItems)
        {
            _itemDb[item.Name] = item;
        }
    }

    /// <summary>
    /// Search for a dinosaur by name, alias, or partial match.
    /// </summary>
    public DinoData? LookupDino(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;

        // Exact match
        if (_dinoDb.TryGetValue(name, out var exact)) return exact;

        // Alias match
        if (_dinoAliases.TryGetValue(name, out var alias) && _dinoDb.TryGetValue(alias, out var aliased)) return aliased;

        // Fuzzy match (contains)
        var search = name.ToLower();
        foreach (var kvp in _dinoDb)
        {
            if (kvp.Key.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                kvp.Value.Description?.Contains(search, StringComparison.OrdinalIgnoreCase) == true)
                return kvp.Value;
        }

        return null;
    }

    /// <summary>
    /// Search for an item by name or alias.
    /// </summary>
    public ItemData? LookupItem(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;

        if (_itemDb.TryGetValue(name, out var exact)) return exact;

        if (_itemAliases.TryGetValue(name, out var alias) && _itemDb.TryGetValue(alias, out var aliased)) return aliased;

        var search = name.ToLower();
        foreach (var kvp in _itemDb)
        {
            if (kvp.Key.Contains(search, StringComparison.OrdinalIgnoreCase))
                return kvp.Value;
        }

        return null;
    }

    /// <summary>
    /// Get all dinosaur data.
    /// </summary>
    public IEnumerable<DinoData> GetAllDinos() => _dinoDb.Values;

    /// <summary>
    /// Get all item data.
    /// </summary>
    public IEnumerable<ItemData> GetAllItems() => _itemDb.Values;

    public void Dispose() => GC.SuppressFinalize(this);
}

public class DinoData
{
    public string Name { get; set; } = "";
    public string? Blueprint { get; set; }
    public string? Description { get; set; }
    public double BaseHealth { get; set; }
    public double BaseAttack { get; set; }
    public double BaseSpeed { get; set; }
    public double BaseFood { get; set; }
    public string? TamingMethod { get; set; }
    public double TamingSpeed { get; set; }
    public string[]? Aliases { get; set; }
}

public class ItemData
{
    public string Name { get; set; } = "";
    public string? Blueprint { get; set; }
    public string? Description { get; set; }
    public int StackSize { get; set; }
    public double Weight { get; set; }
    public string[]? Aliases { get; set; }
    public Dictionary<string, int>? CraftingRequirements { get; set; }
}