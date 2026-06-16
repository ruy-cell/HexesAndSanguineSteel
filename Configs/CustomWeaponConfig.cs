using System.Text.Json;
using System.Text.Json.Serialization;
using BepInEx;
using ProjectM;

namespace HexesAndSanguineSteel;

internal static class CustomWeaponConfig
{
    internal const int DefaultRuntimeCarrierBuff = -1721561549; // EquipBuff_Weapon_Pollaxe_Ability03
    internal const int DefaultRuntimeStatCarrierBuff = -1122472005; // configured stat carrier candidate; validate on startup

    const string DirectoryName = "HexesAndSanguineSteel";
    const string LegacyDirectoryName = "CustomWeaponMod";
    const string FileName = "weapons.json";

    static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true
    };

    internal static string ConfigDirectory => Path.Combine(Paths.ConfigPath, DirectoryName);
    internal static string ConfigPath => Path.Combine(ConfigDirectory, FileName);
    static string LegacyConfigDirectory => Path.Combine(Paths.ConfigPath, LegacyDirectoryName);
    static string LegacyConfigPath => Path.Combine(LegacyConfigDirectory, FileName);

    static void CopyLegacyConfigIfNeeded()
    {
        if (File.Exists(ConfigPath) || !File.Exists(LegacyConfigPath))
            return;

        Directory.CreateDirectory(ConfigDirectory);
        File.Copy(LegacyConfigPath, ConfigPath, overwrite: false);
        Plugin.LogInstance.LogInfo($"Migrated legacy CustomWeaponMod config file to HexesAndSanguineSteel: {ConfigPath}");
    }

    static WeaponConfigFile? _currentFile;
    static IReadOnlyList<CustomWeaponDef> _currentDefinitions = Array.Empty<CustomWeaponDef>();
    static IReadOnlyList<WeaponTypeBuffDef> _currentTypeBuffDefinitions = Array.Empty<WeaponTypeBuffDef>();
    static IReadOnlyList<CustomWeaponCostDef> _globalUpgradeCost = Array.Empty<CustomWeaponCostDef>();

    internal static int CustomWeaponCarrierBuff
        => _currentFile?.DefaultCustomWeaponCarrierBuff ?? DefaultRuntimeCarrierBuff;

    internal static int CustomWeaponStatCarrierBuff
        => _currentFile?.DefaultCustomWeaponStatCarrierBuff ?? DefaultRuntimeStatCarrierBuff;

    internal static IReadOnlyList<WeaponTypeBuffDef> WeaponTypeBuffs
        => _currentTypeBuffDefinitions;

    internal static IReadOnlyList<CustomWeaponCostDef> GlobalUpgradeCost
        => _globalUpgradeCost;

    internal static bool DebugLogging
        => _currentFile?.DebugLogging ?? false;

    internal static double AdminRefreshCooldownSeconds
        => _currentFile?.AdminRefreshCooldownSeconds ?? 5d;

    internal static IReadOnlyList<CustomWeaponDef> LoadOrCreate()
    {
        Directory.CreateDirectory(ConfigDirectory);
        CopyLegacyConfigIfNeeded();

        if (!File.Exists(ConfigPath))
        {
            _currentFile = WeaponConfigFile.CreateDefault();
            _currentDefinitions = _currentFile.ToDefinitions();
            _currentTypeBuffDefinitions = _currentFile.ToWeaponTypeBuffDefinitions();
            _globalUpgradeCost = _currentFile.ToGlobalUpgradeCost();
            Save(_currentFile);
            Plugin.LogInstance.LogInfo($"Created custom weapon config: {ConfigPath}");
            return _currentDefinitions;
        }

        try
        {
            var json = File.ReadAllText(ConfigPath);
            var config = JsonSerializer.Deserialize<WeaponConfigFile>(json, JsonOptions);

            if (config is null)
            {
                config = WeaponConfigFile.CreateDefault();
                Save(config);
            }

            config.Weapons ??= [];
            config.WeaponTypeBuffs ??= [];
            config.GlobalUpgradeCost ??= [];

            bool changed = false;

            if (config.ConfigVersion < 6)
            {
                BackupPreMigrationConfig(config.ConfigVersion);
                config.ConfigVersion = 6;
                changed = true;

                if (config.DefaultCustomWeaponCarrierBuff == 0)
                    config.DefaultCustomWeaponCarrierBuff = DefaultRuntimeCarrierBuff;

                if (config.DefaultCustomWeaponStatCarrierBuff == 0)
                    config.DefaultCustomWeaponStatCarrierBuff = DefaultRuntimeStatCarrierBuff;

                Plugin.LogInstance.LogInfo("Upgraded weapons.json to schema v6. Custom weapon stats are now applied with runtime stat carrier buffs.");
            }

            if (config.ConfigVersion < 7)
            {
                BackupPreMigrationConfig(config.ConfigVersion);
                config.ConfigVersion = 7;
                config.WeaponTypeBuffs ??= [];
                changed = true;
                Plugin.LogInstance.LogInfo("Upgraded weapons.json to schema v7. Added WeaponTypeBuffs[] using the same runtime stat carrier.");
            }

            if (config.ConfigVersion < 8)
            {
                BackupPreMigrationConfig(config.ConfigVersion);
                config.ConfigVersion = 9;
                changed = true;
                Plugin.LogInstance.LogInfo("Upgraded weapons.json to schema v8. Custom weapons can now override R and C slots.");
            }

            if (config.ConfigVersion < 10)
            {
                BackupPreMigrationConfig(config.ConfigVersion);
                config.ConfigVersion = 10;
                changed = true;
                Plugin.LogInstance.LogInfo("Upgraded weapons.json to schema v10. Legacy PhysicalPower/SpellPower are ignored when Stats[] is non-empty; generated configs set them to 0 to avoid double-stat confusion.");
            }

            if (config.ConfigVersion < 11)
            {
                BackupPreMigrationConfig(config.ConfigVersion);
                config.ConfigVersion = 11;
                changed = true;
                Plugin.LogInstance.LogInfo("Upgraded weapons.json to schema v11. Added DebugLogging and AdminRefreshCooldownSeconds for lower-noise live operation.");
            }

            if (config.ConfigVersion < 12)
            {
                BackupPreMigrationConfig(config.ConfigVersion);
                config.ConfigVersion = 12;
                changed = true;
                Plugin.LogInstance.LogInfo("Upgraded weapons.json to schema v12. Added per-weapon UpgradeCost[] consumed by .csw upgrade.");
            }

            if (config.ConfigVersion < 13)
            {
                BackupPreMigrationConfig(config.ConfigVersion);
                config.ConfigVersion = 13;
                config.GlobalUpgradeCost ??= [];
                changed = true;
                Plugin.LogInstance.LogInfo("Upgraded weapons.json to schema v13. Added GlobalUpgradeCost[] used as the default cost for every .csw upgrade.");
            }

            if (config.ConfigVersion < 14)
            {
                BackupPreMigrationConfig(config.ConfigVersion);
                config.ConfigVersion = 14;
                changed = true;
                Plugin.LogInstance.LogInfo("Upgraded weapons.json to schema v14. Removed per-weapon UpgradeCost[]; .csw upgrade now uses GlobalUpgradeCost only.");
            }

            if (config.ConfigVersion < 15)
            {
                BackupPreMigrationConfig(config.ConfigVersion);
                config.ConfigVersion = 15;
                changed = true;
                Plugin.LogInstance.LogInfo("Upgraded weapons.json to schema v15. Added custom weapon OnHitEffects[] for spell-school/coating-style procs.");
            }

            if (config.ConfigVersion < 16)
            {
                BackupPreMigrationConfig(config.ConfigVersion);
                config.ConfigVersion = 16;
                changed = true;
                Plugin.LogInstance.LogInfo("Upgraded weapons.json to schema v16. Removed SecondaryBuff on-hit behavior; OnHitEffects now apply only the Bloodcraft class school debuffs.");
            }

            if (config.DefaultCustomWeaponCarrierBuff == 0)
            {
                config.DefaultCustomWeaponCarrierBuff = DefaultRuntimeCarrierBuff;
                changed = true;
            }

            if (config.DefaultCustomWeaponStatCarrierBuff == 0)
            {
                config.DefaultCustomWeaponStatCarrierBuff = DefaultRuntimeStatCarrierBuff;
                changed = true;
            }

            config.GlobalUpgradeCost ??= [];

            foreach (var weapon in config.Weapons)
            {
                if (weapon.SpellSlot1 == 0 && weapon.R != 0)
                {
                    weapon.SpellSlot1 = weapon.R;
                    weapon.SpellSlot1Cooldown = weapon.RCooldown;
                    weapon.R = 0;
                    weapon.RCooldown = 0f;
                    changed = true;
                }

                if (weapon.SpellSlot2 == 0 && weapon.C != 0)
                {
                    weapon.SpellSlot2 = weapon.C;
                    weapon.SpellSlot2Cooldown = weapon.CCooldown;
                    weapon.C = 0;
                    weapon.CCooldown = 0f;
                    changed = true;
                }

                weapon.Stats ??= [];
                weapon.OnHitEffects ??= [];

                if (weapon.Stats.Count == 0)
                {
                    if (weapon.PhysicalPower != 0f)
                    {
                        weapon.Stats.Add(new WeaponStatConfigEntry
                        {
                            StatType = "PhysicalPower",
                            ModificationType = "AddToBase",
                            AttributeCapType = "SoftCapped",
                            Value = weapon.PhysicalPower
                        });
                    }

                    if (weapon.SpellPower != 0f)
                    {
                        weapon.Stats.Add(new WeaponStatConfigEntry
                        {
                            StatType = "SpellPower",
                            ModificationType = "AddToBase",
                            AttributeCapType = "SoftCapped",
                            Value = weapon.SpellPower
                        });
                    }

                    if (weapon.Stats.Count > 0)
                        changed = true;
                }

                if (weapon.Stats.Count > 0 && (weapon.PhysicalPower != 0f || weapon.SpellPower != 0f))
                {
                    weapon.PhysicalPower = 0f;
                    weapon.SpellPower = 0f;
                    changed = true;
                }
            }

            if (changed)
                Save(config);

            _currentFile = config;
            _currentDefinitions = config.ToDefinitions();
            _currentTypeBuffDefinitions = config.ToWeaponTypeBuffDefinitions();
            _globalUpgradeCost = config.ToGlobalUpgradeCost();
            return _currentDefinitions;
        }
        catch (Exception ex)
        {
            var backup = ConfigPath + ".broken";
            try
            {
                if (File.Exists(backup)) File.Delete(backup);
                if (File.Exists(ConfigPath)) File.Move(ConfigPath, backup);
            }
            catch (Exception backupEx)
            {
                Plugin.LogInstance.LogWarning($"Could not back up broken config: {backupEx.Message}");
            }

            _currentFile = WeaponConfigFile.CreateDefault();
            _currentDefinitions = _currentFile.ToDefinitions();
            _currentTypeBuffDefinitions = _currentFile.ToWeaponTypeBuffDefinitions();
            _globalUpgradeCost = _currentFile.ToGlobalUpgradeCost();
            Save(_currentFile);

            Plugin.LogInstance.LogError($"Failed to read custom weapon config. Backed up broken file to '{backup}' and wrote defaults. Error: {ex}");
            return _currentDefinitions;
        }
    }

    static void BackupPreMigrationConfig(int oldVersion)
    {
        try
        {
            string backup = ConfigPath + $".v{oldVersion}.backup";
            if (File.Exists(backup))
                File.Delete(backup);

            if (File.Exists(ConfigPath))
                File.Copy(ConfigPath, backup);
        }
        catch (Exception ex)
        {
            Plugin.LogInstance.LogWarning($"Could not create weapons.json migration backup: {ex.Message}");
        }
    }

    static void Save(WeaponConfigFile config)
    {
        Directory.CreateDirectory(ConfigDirectory);
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(config, JsonOptions));
    }
}

internal sealed class WeaponConfigFile
{
    public int ConfigVersion { get; set; } = 16;

    // Keep runtime equip/swap logs quiet on live servers. Enable only while diagnosing a player.
    public bool DebugLogging { get; set; } = false;

    // Protects expensive all-online admin refresh commands from accidental spam.
    public double AdminRefreshCooldownSeconds { get; set; } = 5d;

    // Ability carrier: must be an EquipBuff_Weapon_* style buff that has ReplaceAbilityOnSlotBuff.
    public int DefaultCustomWeaponCarrierBuff { get; set; } = CustomWeaponConfig.DefaultRuntimeCarrierBuff;

    // Stat carrier: should be a reserved buff prefab with ModifyUnitStatBuff_DOTS buffer.
    // Prefer an unused SetBonus_* candidate. Do not share this with PerkShop/Bloodcraft/player spell carriers.
    public int DefaultCustomWeaponStatCarrierBuff { get; set; } = CustomWeaponConfig.DefaultRuntimeStatCarrierBuff;

    // Global cost consumed by every .csw upgrade. Per-weapon upgrade costs are no longer supported.
    public List<WeaponUpgradeCostConfigEntry> GlobalUpgradeCost { get; set; } = [];

    public List<WeaponConfigEntry> Weapons { get; set; } = [];

    // Global weapon-type stat buffs. These use the same DefaultCustomWeaponStatCarrierBuff
    // as custom weapon stats. The active player's stat carrier instance is rebuilt with
    // custom weapon stats + matching weapon type stats merged together.
    public List<WeaponTypeBuffConfigEntry> WeaponTypeBuffs { get; set; } = [];

    public static WeaponConfigFile CreateDefault() => new()
    {
        ConfigVersion = 16,
        DefaultCustomWeaponCarrierBuff = CustomWeaponConfig.DefaultRuntimeCarrierBuff,
        DefaultCustomWeaponStatCarrierBuff = CustomWeaponConfig.DefaultRuntimeStatCarrierBuff,
        GlobalUpgradeCost =
        [
            // Global default cost for every .csw upgrade. Leave empty for free upgrades.
            // new() { Name = "Primal Blood Essence", Item = 862477668, Amount = 1 }
        ],
        WeaponTypeBuffs =
        [
            new()
            {
                Enabled = false,
                WeaponType = "Mace",
                ApplyToCustomWeapons = true,
                Stats =
                [
                    new() { StatType = "AttackSpeed", ModificationType = "AddToBase", AttributeCapType = "SoftCapped", Value = 0.10f }
                ]
            }
        ],
        Weapons =
        [
            new()
            {
                Enabled = true,
                Name = "Crimson Thorns",
                ItemWeapon = 1307774440,

                // Legacy only. This is no longer mutated as the custom ability package.
                EquipBuff = 673013659,

                WeaponLevel = 100f,
                PhysicalPower = 0f,
                SpellPower = 0f,
                Stats =
                [
                    new() { StatType = "PhysicalPower", ModificationType = "AddToBase", AttributeCapType = "SoftCapped", Value = 35f },
                    new() { StatType = "SpellPower", ModificationType = "AddToBase", AttributeCapType = "SoftCapped", Value = 10f },
                    new() { StatType = "PhysicalResistance", ModificationType = "AddToBase", AttributeCapType = "SoftCapped", Value = 6f }
                ],
                OnHitEffects = [],
                Attack = -208121356,
                AttackCooldown = 0f,
                Primary = 1826128809,
                PrimaryCooldown = 8f,
                Secondary = 1730729556,
                SecondaryCooldown = 10f,
                Dash = -1940289109,
                DashCooldown = 6f,
                Ultimate = -1730693034,
                UltimateCooldown = 60f,
                SpellSlot1 = 841757706,
                SpellSlot1Cooldown = 8f,
                SpellSlot2 = 1295370119,
                SpellSlot2Cooldown = 8f
            }
        ]
    };

    public IReadOnlyList<CustomWeaponDef> ToDefinitions()
    {
        var result = new List<CustomWeaponDef>();

        foreach (var weapon in Weapons)
        {
            if (weapon.Enabled)
                result.Add(weapon.ToDefinition(DefaultCustomWeaponCarrierBuff, DefaultCustomWeaponStatCarrierBuff));
        }

        return result;
    }

    public IReadOnlyList<CustomWeaponCostDef> ToGlobalUpgradeCost()
    {
        GlobalUpgradeCost ??= [];
        return WeaponConfigEntry.BuildUpgradeCost("GlobalUpgradeCost", GlobalUpgradeCost);
    }

    public IReadOnlyList<WeaponTypeBuffDef> ToWeaponTypeBuffDefinitions()
    {
        var result = new List<WeaponTypeBuffDef>();

        foreach (var buff in WeaponTypeBuffs)
        {
            if (!buff.Enabled)
                continue;

            buff.Stats ??= [];

            result.Add(new WeaponTypeBuffDef(
                buff.WeaponType,
                buff.ApplyToCustomWeapons,
                StatConfigBuilder.BuildStats($"WeaponTypeBuff:{buff.WeaponType}", buff.Stats)
            ));
        }

        return result;
    }
}

internal sealed class WeaponConfigEntry
{
    public bool Enabled { get; set; } = true;
    public string Name { get; set; } = "Custom Weapon";

    public int ItemWeapon { get; set; }

    // Legacy compatibility only. In schema v5+, custom weapons no longer need one unique EquipBuff per weapon.
    public int EquipBuff { get; set; }

    // Optional per-weapon runtime ability carrier. 0 means use DefaultCustomWeaponCarrierBuff.
    public int CarrierBuff { get; set; }

    // Optional per-weapon runtime stat carrier. 0 means use DefaultCustomWeaponStatCarrierBuff.
    public int StatCarrierBuff { get; set; }

    // Legacy/default fields. If Stats[] is empty, these are materialized into Stats[] during migration/load.
    public float WeaponLevel { get; set; } = 100f;
    public float PhysicalPower { get; set; } = 35f;
    public float SpellPower { get; set; } = 10f;

    public List<WeaponStatConfigEntry> Stats { get; set; } = [];

    public List<WeaponOnHitEffectConfigEntry> OnHitEffects { get; set; } = [];


    // Deprecated schema <=13 field. Ignored and omitted from saved config.
    // .csw upgrade now uses only GlobalUpgradeCost.
    [JsonIgnore]
    public List<WeaponUpgradeCostConfigEntry> UpgradeCost { get; set; } = [];

    public int Attack { get; set; }
    public float AttackCooldown { get; set; }

    public int Primary { get; set; }
    public float PrimaryCooldown { get; set; }

    public int Secondary { get; set; }
    public float SecondaryCooldown { get; set; }

    public int Dash { get; set; }
    public float DashCooldown { get; set; }

    public int Ultimate { get; set; }
    public float UltimateCooldown { get; set; }

    // Custom-weapon-only extra slots.
    // Spell Slot 1 maps to ReplaceAbilityOnSlotBuff slot 5.
    // Spell Slot 2 maps to ReplaceAbilityOnSlotBuff slot 6.
    [JsonPropertyName("Spell Slot 1")]
    public int SpellSlot1 { get; set; }

    [JsonPropertyName("Spell Slot 1 Cooldown")]
    public float SpellSlot1Cooldown { get; set; }

    [JsonPropertyName("Spell Slot 2")]
    public int SpellSlot2 { get; set; }

    [JsonPropertyName("Spell Slot 2 Cooldown")]
    public float SpellSlot2Cooldown { get; set; }

    // Legacy v8 field names. These are read for migration, then cleared so they do not keep appearing in generated config.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int R { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public float RCooldown { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int C { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public float CCooldown { get; set; }

    public CustomWeaponDef ToDefinition(int defaultCarrierBuff, int defaultStatCarrierBuff) => new(
        Name: Name,
        ItemWeapon: Guid(ItemWeapon),
        EquipBuff: Guid(EquipBuff),
        CarrierBuff: Guid(CarrierBuff != 0 ? CarrierBuff : defaultCarrierBuff),
        StatCarrierBuff: Guid(StatCarrierBuff != 0 ? StatCarrierBuff : defaultStatCarrierBuff),
        UpgradeCost: Array.Empty<CustomWeaponCostDef>(),
        Power: new WeaponPowerDef(WeaponLevel, PhysicalPower, SpellPower),
        Stats: StatConfigBuilder.BuildStats(Name, Stats),
        OnHitEffects: BuildOnHitEffects(Name, OnHitEffects),
        Attack: Slot(Attack, AttackCooldown),
        Primary: Slot(Primary, PrimaryCooldown),
        Secondary: Slot(Secondary, SecondaryCooldown),
        Dash: Slot(Dash, DashCooldown),
        Ultimate: Slot(Ultimate, UltimateCooldown),
        SpellSlot1: Slot(SpellSlot1, SpellSlot1Cooldown),
        SpellSlot2: Slot(SpellSlot2, SpellSlot2Cooldown)
    );

    static IReadOnlyList<CustomWeaponStatDef> BuildStats(string weaponName, List<WeaponStatConfigEntry> stats)
    {
        var result = new List<CustomWeaponStatDef>();

        foreach (var stat in stats)
        {
            if (!Enum.TryParse(stat.StatType, ignoreCase: true, out UnitStatType statType))
            {
                RuntimeOptimization.WarnOnce($"stat-unknown:{weaponName}:{stat.StatType}", $"{weaponName}: unknown UnitStatType '{stat.StatType}'. Stat entry skipped.");
                continue;
            }

            if (!Enum.TryParse(stat.ModificationType, ignoreCase: true, out ModificationType modificationType))
            {
                RuntimeOptimization.WarnOnce($"modtype-unknown:{weaponName}:{stat.ModificationType}", $"{weaponName}: unknown ModificationType '{stat.ModificationType}'. Stat entry skipped.");
                continue;
            }

            if (!Enum.TryParse(stat.AttributeCapType, ignoreCase: true, out AttributeCapType attributeCapType))
            {
                RuntimeOptimization.WarnOnce($"captype-unknown:{weaponName}:{stat.AttributeCapType}", $"{weaponName}: unknown AttributeCapType '{stat.AttributeCapType}'. Stat entry skipped.");
                continue;
            }

            result.Add(new CustomWeaponStatDef(
                statType,
                modificationType,
                attributeCapType,
                stat.Value,
                1f
            ));
        }

        return result;
    }



    static IReadOnlyList<CustomWeaponOnHitEffectDef> BuildOnHitEffects(string weaponName, List<WeaponOnHitEffectConfigEntry> effects)
    {
        var result = new List<CustomWeaponOnHitEffectDef>();
        effects ??= [];

        foreach (var effect in effects)
        {
            if (!effect.Enabled)
                continue;

            if (effect.Chance <= 0f)
                continue;

            if (effect.TargetBuff == 0)
            {
                RuntimeOptimization.WarnOnce($"onhit-target0:{weaponName}:{effect.Name}", $"{weaponName}: on-hit effect '{effect.Name}' has TargetBuff 0. Effect skipped.");
                continue;
            }

            result.Add(new CustomWeaponOnHitEffectDef(
                string.IsNullOrWhiteSpace(effect.Name) ? $"OnHit:{effect.TargetBuff}" : effect.Name,
                effect.School ?? string.Empty,
                Math.Clamp(effect.Chance, 0f, 1f),
                Guid(effect.TargetBuff),
                effect.AffectPlayers,
                effect.AffectNonPlayers
            ));
        }

        return result;
    }


    internal static IReadOnlyList<CustomWeaponCostDef> BuildUpgradeCost(string weaponName, List<WeaponUpgradeCostConfigEntry> costs)
    {
        var result = new List<CustomWeaponCostDef>();

        costs ??= [];

        foreach (var cost in costs)
        {
            if (cost.Amount <= 0)
            {
                Plugin.LogInstance.LogWarning($"{weaponName}: upgrade cost '{cost.Name}' has non-positive Amount {cost.Amount}. Cost entry skipped.");
                continue;
            }

            if (cost.Item == 0)
            {
                Plugin.LogInstance.LogWarning($"{weaponName}: upgrade cost '{cost.Name}' has Item 0. Cost entry skipped.");
                continue;
            }

            result.Add(new CustomWeaponCostDef(
                string.IsNullOrWhiteSpace(cost.Name) ? cost.Item.ToString() : cost.Name,
                Guid(cost.Item),
                cost.Amount
            ));
        }

        return result;
    }

    static Stunlock.Core.PrefabGUID Guid(int hash) => new(hash);
    static AbilitySlotDef Slot(int guidHash, float cooldown) => new(Guid(guidHash), cooldown);
}



internal sealed class WeaponOnHitEffectConfigEntry
{
    public bool Enabled { get; set; } = true;

    // Friendly label for logs/admin readability.
    public string Name { get; set; } = string.Empty;

    // Optional readability label. Supported/proven Bloodcraft class school effects:
    // Blood=Leech, Storm=Static, Frost=Chill, Chaos=Ignite, Illusion=Weaken, Unholy=Condemn.
    public string School { get; set; } = string.Empty;

    // 0.15 = 15% proc chance.
    public float Chance { get; set; } = 0.15f;

    // School buff/debuff applied to the damaged target when the proc succeeds.
    // Use the proven Bloodcraft class primary school buffs, not ability group GUIDs.
    public int TargetBuff { get; set; }

    // Defaults are PvE-safe. Enable AffectPlayers only if you want PvP procs.
    public bool AffectPlayers { get; set; } = false;
    public bool AffectNonPlayers { get; set; } = true;
}


internal sealed class WeaponUpgradeCostConfigEntry
{
    // Optional friendly label for admin readability.
    public string Name { get; set; } = string.Empty;

    // Item prefab GUID hash to consume from the upgrader's inventory.
    public int Item { get; set; }

    public int Amount { get; set; }
}

internal sealed class WeaponTypeBuffConfigEntry
{
    public bool Enabled { get; set; } = true;
    public string WeaponType { get; set; } = "Mace";
    public bool ApplyToCustomWeapons { get; set; } = true;
    public List<WeaponStatConfigEntry> Stats { get; set; } = [];

}

internal static class StatConfigBuilder
{
    // Friendly config aliases. The game enum uses PrimaryAttackSpeed, while older/example
    // weapon configs used AttackSpeed. Keep accepting the old name so WeaponTypeBuffs and
    // per-weapon Stats[] validate the same way.
    static readonly Dictionary<string, string> UnitStatAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AS"] = "PrimaryAttackSpeed",
        ["AttackSpeed"] = "PrimaryAttackSpeed",
        ["PrimaryAttackSpeed"] = "PrimaryAttackSpeed",

        ["AAS"] = "AbilityAttackSpeed",
        ["AbilitySpeed"] = "AbilityAttackSpeed",
        ["AbilityAttackSpeed"] = "AbilityAttackSpeed",

        ["MS"] = "MovementSpeed",
        ["MoveSpeed"] = "MovementSpeed",
        ["MovementSpeed"] = "MovementSpeed"
    };

    static readonly Dictionary<string, string> AttributeCapAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["UnCapped"] = "Uncapped",
        ["Uncapped"] = "Uncapped",
        ["NoCap"] = "Uncapped",
        ["NoCaps"] = "Uncapped",
        ["IgnoreCap"] = "Uncapped",
        ["IgnoreCaps"] = "Uncapped",
        ["None"] = "Uncapped"
    };

    internal static IReadOnlyList<CustomWeaponStatDef> BuildStats(string ownerName, List<WeaponStatConfigEntry> stats)
    {
        var result = new List<CustomWeaponStatDef>();

        foreach (var stat in stats)
        {
            if (!TryParseUnitStatType(stat.StatType, out UnitStatType statType, out string resolvedStatName))
            {
                RuntimeOptimization.WarnOnce($"stat-unknown:{ownerName}:{stat.StatType}", $"{ownerName}: unknown UnitStatType '{stat.StatType}'. Stat entry skipped. Run .csw stattypes or use aliases like AttackSpeed/AS -> PrimaryAttackSpeed.");
                continue;
            }

            if (!Enum.TryParse(stat.ModificationType, ignoreCase: true, out ModificationType modificationType))
            {
                RuntimeOptimization.WarnOnce($"modtype-unknown:{ownerName}:{stat.ModificationType}", $"{ownerName}: unknown ModificationType '{stat.ModificationType}'. Stat entry skipped.");
                continue;
            }

            if (!TryParseAttributeCapType(stat.AttributeCapType, out AttributeCapType attributeCapType, out string resolvedCapName))
            {
                RuntimeOptimization.WarnOnce($"captype-unknown:{ownerName}:{stat.AttributeCapType}", $"{ownerName}: unknown AttributeCapType '{stat.AttributeCapType}'. Stat entry skipped.");
                continue;
            }

            if (!string.Equals(stat.StatType, resolvedStatName, StringComparison.OrdinalIgnoreCase))
                RuntimeOptimization.Debug($"{ownerName}: mapped UnitStatType alias '{stat.StatType}' -> '{resolvedStatName}'.");

            if (!string.Equals(stat.AttributeCapType, resolvedCapName, StringComparison.OrdinalIgnoreCase))
                RuntimeOptimization.Debug($"{ownerName}: mapped AttributeCapType alias '{stat.AttributeCapType}' -> '{resolvedCapName}'.");

            result.Add(new CustomWeaponStatDef(
                statType,
                modificationType,
                attributeCapType,
                stat.Value,
                1f
            ));
        }

        return result;
    }

    static bool TryParseUnitStatType(string rawName, out UnitStatType value, out string resolvedName)
    {
        resolvedName = rawName?.Trim() ?? string.Empty;

        if (Enum.TryParse(resolvedName, ignoreCase: true, out value))
            return true;

        if (UnitStatAliases.TryGetValue(resolvedName, out string alias)
            && Enum.TryParse(alias, ignoreCase: true, out value))
        {
            resolvedName = alias;
            return true;
        }

        value = default;
        return false;
    }

    static bool TryParseAttributeCapType(string rawName, out AttributeCapType value, out string resolvedName)
    {
        resolvedName = rawName?.Trim() ?? string.Empty;

        if (Enum.TryParse(resolvedName, ignoreCase: true, out value))
            return true;

        if (AttributeCapAliases.TryGetValue(resolvedName, out string alias)
            && Enum.TryParse(alias, ignoreCase: true, out value))
        {
            resolvedName = alias;
            return true;
        }

        value = default;
        return false;
    }
}

internal sealed class WeaponStatConfigEntry
{
    public string StatType { get; set; } = "PhysicalPower";
    public string ModificationType { get; set; } = "AddToBase";
    public string AttributeCapType { get; set; } = "SoftCapped";
    public float Value { get; set; }
}
