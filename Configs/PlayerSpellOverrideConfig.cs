using System.Text.Json;
using BepInEx;

namespace HexesAndSanguineSteel;

internal enum SpellSlotKind
{
    Q,
    E,
    Dash,
    R,
    C,
    T
}

internal sealed class PlayerSpellOverrideFile
{
    public int ConfigVersion { get; set; } = 8;

    // Must be a buff prefab that has ReplaceAbilityOnSlotBuff buffer.
    // Do not use any EquipBuff already used by weapons.json/custom weapons.
    // 673013659 was the old default and conflicts with the default custom Shadow Spear.
    public int DefaultCarrierBuff { get; set; } = PlayerSpellOverrideConfig.SafeDefaultCarrierBuff;

    public Dictionary<string, PlayerSpellOverrideEntry> Players { get; set; } = [];
}

internal sealed class PlayerSpellOverrideEntry
{
    public bool Enabled { get; set; } = true;
    public ulong PlatformId { get; set; }
    public string LastKnownName { get; set; } = string.Empty;

    // Optional per-player carrier. 0 means use DefaultCarrierBuff.
    public int CarrierBuff { get; set; }

    // Weapon-scoped spell profiles.
    // Keys are weapon type names such as Sword, Spear, Reaper, Unarmed.
    // Unknown weapons fall back to Item:<prefabGuidHash>.
    public Dictionary<string, PlayerSpellScopeEntry> WeaponScopes { get; set; } = [];

    // Legacy flat fields kept so old configs can still deserialize.
    // New commands no longer write these.
    public int Q { get; set; }
    public float QCooldown { get; set; }
    public int E { get; set; }
    public float ECooldown { get; set; }
    public int Dash { get; set; }
    public float DashCooldown { get; set; }
    public int R { get; set; }
    public float RCooldown { get; set; }
    public int C { get; set; }
    public float CCooldown { get; set; }
    public int T { get; set; }
    public float TCooldown { get; set; }
}

internal sealed class PlayerSpellScopeEntry
{
    public int Q { get; set; }
    public float QCooldown { get; set; }

    public int E { get; set; }
    public float ECooldown { get; set; }

    public int Dash { get; set; }
    public float DashCooldown { get; set; }

    public int R { get; set; }
    public float RCooldown { get; set; }

    public int C { get; set; }
    public float CCooldown { get; set; }

    public int T { get; set; }
    public float TCooldown { get; set; }

    public bool HasAnySpell()
        => Q != 0 || E != 0 || Dash != 0 || R != 0 || C != 0 || T != 0;
}

internal static class PlayerSpellOverrideConfig
{
    internal const int LegacyConflictingCarrierBuff = 673013659;
    internal const int SafeDefaultCarrierBuff = 1644894901;

    const string DirectoryName = "HexesAndSanguineSteel";
    const string LegacyDirectoryName = "CustomWeaponMod";
    const string FileName = "player-spells.json";

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

    static PlayerSpellOverrideFile? _cache;

    internal static PlayerSpellOverrideFile LoadOrCreate()
    {
        Directory.CreateDirectory(ConfigDirectory);
        CopyLegacyConfigIfNeeded();

        if (!File.Exists(ConfigPath))
        {
            _cache = new PlayerSpellOverrideFile();
            Save(_cache);
            Plugin.LogInstance.LogInfo($"Created player spell override config: {ConfigPath}");
            return _cache;
        }

        try
        {
            var json = File.ReadAllText(ConfigPath);
            _cache = JsonSerializer.Deserialize<PlayerSpellOverrideFile>(json, JsonOptions) ?? new PlayerSpellOverrideFile();
            _cache.Players ??= [];

            foreach (var entry in _cache.Players.Values)
                entry.WeaponScopes ??= [];

            bool changed = false;

            if (_cache.ConfigVersion < 3)
            {
                BackupPreMigrationConfig();
                _cache.ConfigVersion = 3;
                changed = true;
                Plugin.LogInstance.LogInfo("Upgraded player spell override config to weapon-scoped schema v3. Legacy flat Q/E/T fields were preserved but are no longer applied unless re-saved with commands.");
            }

            if (_cache.ConfigVersion < 4 || _cache.DefaultCarrierBuff == LegacyConflictingCarrierBuff)
            {
                BackupPreMigrationConfig();

                if (_cache.DefaultCarrierBuff == LegacyConflictingCarrierBuff)
                {
                    _cache.DefaultCarrierBuff = SafeDefaultCarrierBuff;
                    changed = true;
                    Plugin.LogInstance.LogWarning($"Migrated DefaultCarrierBuff away from {LegacyConflictingCarrierBuff} because it conflicts with the default custom spear equip buff. New default carrier: {SafeDefaultCarrierBuff}.");
                }

                foreach (var entry in _cache.Players.Values)
                {
                    if (entry.CarrierBuff == LegacyConflictingCarrierBuff)
                    {
                        entry.CarrierBuff = 0;
                        changed = true;
                    }
                }

                _cache.ConfigVersion = 4;
                changed = true;
            }

            if (_cache.ConfigVersion < 5)
            {
                BackupPreMigrationConfig();
                _cache.ConfigVersion = 5;
                changed = true;
                Plugin.LogInstance.LogInfo("Upgraded player spell override config to schema v5. Dash, R, and C weapon-scoped spell slots are now supported.");
            }

            if (_cache.ConfigVersion < 6)
            {
                BackupPreMigrationConfig();
                _cache.ConfigVersion = 6;
                changed = true;
                Plugin.LogInstance.LogInfo("Upgraded player spell override config to schema v6. SpellPool support was removed.");
            }
            if (_cache.ConfigVersion < 8)
            {
                BackupPreMigrationConfig();
                _cache.ConfigVersion = 8;
                changed = true;
                Plugin.LogInstance.LogInfo("Upgraded player spell override config to schema v8. Experimental spell charge fields were removed.");
            }

            if (changed)
                Save(_cache);

            return _cache;
        }
        catch (Exception ex)
        {
            var backup = ConfigPath + ".broken";

            try
            {
                if (File.Exists(backup))
                    File.Delete(backup);

                File.Move(ConfigPath, backup);
            }
            catch (Exception backupEx)
            {
                Plugin.LogInstance.LogWarning($"Could not back up broken player spell config: {backupEx.Message}");
            }

            _cache = new PlayerSpellOverrideFile();
            Save(_cache);
            Plugin.LogInstance.LogError($"Failed to read player spell config. Wrote defaults. Error: {ex}");
            return _cache;
        }
    }

    static void BackupPreMigrationConfig()
    {
        try
        {
            string backup = ConfigPath + ".backup";
            if (File.Exists(backup))
                File.Delete(backup);

            if (File.Exists(ConfigPath))
                File.Copy(ConfigPath, backup);
        }
        catch (Exception ex)
        {
            Plugin.LogInstance.LogWarning($"Could not create player spell config migration backup: {ex.Message}");
        }
    }
    internal static PlayerSpellOverrideFile Current => _cache ?? LoadOrCreate();

    internal static void Save()
    {
        Save(Current);
    }

    static void Save(PlayerSpellOverrideFile config)
    {
        Directory.CreateDirectory(ConfigDirectory);
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(config, JsonOptions));
    }
}
