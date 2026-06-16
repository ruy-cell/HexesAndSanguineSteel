using System.Text.Json;
using BepInEx;
using ProjectM;
using Stunlock.Core;
using Unity.Entities;

namespace HexesAndSanguineSteel;

internal sealed class CustomWeaponInstanceFile
{
    public int ConfigVersion { get; set; } = 1;
    public Dictionary<string, CustomWeaponInstanceEntry> Instances { get; set; } = [];
}

internal sealed class CustomWeaponInstanceEntry
{
    public int SequenceGuidHash { get; set; }
    public string WeaponName { get; set; } = string.Empty;
    public int ItemWeapon { get; set; }
    public ulong LastKnownOwnerPlatformId { get; set; }
    public string LastKnownOwnerName { get; set; } = string.Empty;
    public string CreatedUtc { get; set; } = DateTime.UtcNow.ToString("O");
    public string LastSeenUtc { get; set; } = DateTime.UtcNow.ToString("O");
}

internal static class CustomWeaponInstanceStore
{
    const string DirectoryName = "HexesAndSanguineSteel";
    const string LegacyDirectoryName = "CustomWeaponMod";
    const string FileName = "weapon-instances.json";

    static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true
    };

    static CustomWeaponInstanceFile _current = new();

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

    internal static void Initialize()
    {
        Directory.CreateDirectory(ConfigDirectory);
        CopyLegacyConfigIfNeeded();

        if (!File.Exists(ConfigPath))
        {
            _current = new CustomWeaponInstanceFile();
            Save();
            Plugin.LogInstance.LogInfo($"Created custom weapon instance file: {ConfigPath}");
            return;
        }

        try
        {
            var json = File.ReadAllText(ConfigPath);
            _current = JsonSerializer.Deserialize<CustomWeaponInstanceFile>(json, JsonOptions) ?? new CustomWeaponInstanceFile();
            _current.Instances ??= [];
        }
        catch (Exception ex)
        {
            string backup = ConfigPath + ".broken";
            try
            {
                if (File.Exists(backup))
                    File.Delete(backup);
                File.Move(ConfigPath, backup);
            }
            catch (Exception backupEx)
            {
                Plugin.LogInstance.LogWarning($"Could not back up broken weapon instance file: {backupEx.Message}");
            }

            _current = new CustomWeaponInstanceFile();
            Save();
            Plugin.LogInstance.LogError($"Failed to load weapon instance file. Wrote a fresh one. Error: {ex}");
        }
    }

    internal static bool TryBindGeneratedItem(Entity itemEntity, CustomWeaponDef weapon, PlayerLookupResult receiver, out string message)
    {
        message = string.Empty;

        if (!itemEntity.ExistsSafe())
        {
            message = "New item entity did not exist.";
            return false;
        }

        int sequenceGuidHash = ItemInstanceUtility.GetOrCreateSequenceGuid(itemEntity);

        if (sequenceGuidHash == 0)
        {
            message = "Could not assign a SequenceGUID to the generated weapon.";
            return false;
        }

        _current.Instances[sequenceGuidHash.ToString()] = new CustomWeaponInstanceEntry
        {
            SequenceGuidHash = sequenceGuidHash,
            WeaponName = weapon.Name,
            ItemWeapon = weapon.ItemWeapon.GuidHash,
            LastKnownOwnerPlatformId = receiver.PlatformId,
            LastKnownOwnerName = receiver.Name,
            CreatedUtc = DateTime.UtcNow.ToString("O"),
            LastSeenUtc = DateTime.UtcNow.ToString("O")
        };

        Save();

        message = $"Bound generated item instance {sequenceGuidHash} to custom weapon '{weapon.Name}'.";
        return true;
    }


    internal static bool TryBindHeldItem(Entity itemEntity, CustomWeaponDef weapon, PlayerLookupResult holder, out string message)
    {
        message = string.Empty;

        if (!itemEntity.ExistsSafe())
        {
            message = "Held item entity did not exist.";
            return false;
        }

        int sequenceGuidHash = ItemInstanceUtility.GetOrCreateSequenceGuid(itemEntity);

        if (sequenceGuidHash == 0)
        {
            message = "Could not assign a SequenceGUID to the held weapon.";
            return false;
        }

        string key = sequenceGuidHash.ToString();
        string previous = string.Empty;

        if (_current.Instances.TryGetValue(key, out var existing) && !string.IsNullOrWhiteSpace(existing.WeaponName))
            previous = $" Previous binding was '{existing.WeaponName}'.";

        _current.Instances[key] = new CustomWeaponInstanceEntry
        {
            SequenceGuidHash = sequenceGuidHash,
            WeaponName = weapon.Name,
            ItemWeapon = weapon.ItemWeapon.GuidHash,
            LastKnownOwnerPlatformId = holder.PlatformId,
            LastKnownOwnerName = holder.Name,
            CreatedUtc = existing?.CreatedUtc ?? DateTime.UtcNow.ToString("O"),
            LastSeenUtc = DateTime.UtcNow.ToString("O")
        };

        Save();

        message = $"Bound held item instance {sequenceGuidHash} to custom weapon '{weapon.Name}'.{previous}";
        return true;
    }


    internal static bool TryGetHeldInstanceWeapon(Entity character, out CustomWeaponDef weapon, out CustomWeaponInstanceEntry instance)
    {
        weapon = default;
        instance = default!;

        if (!ItemInstanceUtility.TryGetHeldWeaponEntity(character, out var itemEntity))
            return false;

        if (!ItemInstanceUtility.TryGetSequenceGuid(itemEntity, out int sequenceGuidHash))
            return false;

        if (!_current.Instances.TryGetValue(sequenceGuidHash.ToString(), out instance!))
            return false;

        if (!CustomWeaponRegistry.TryGetCustomWeaponByName(instance.WeaponName, out weapon))
            return false;

        instance.LastSeenUtc = DateTime.UtcNow.ToString("O");
        return true;
    }

    internal static bool TryDescribeHeld(Entity character, out string message)
    {
        message = "No custom weapon instance is held.";

        if (!ItemInstanceUtility.TryGetHeldWeaponEntity(character, out var itemEntity))
        {
            message = "No weapon entity is currently held.";
            return false;
        }

        if (!ItemInstanceUtility.TryGetSequenceGuid(itemEntity, out int sequenceGuidHash))
        {
            message = "Held weapon has no SequenceGUID. It is not an instance-bound custom weapon.";
            return false;
        }

        if (!_current.Instances.TryGetValue(sequenceGuidHash.ToString(), out var instance))
        {
            message = $"Held weapon SequenceGUID {sequenceGuidHash} is not bound to a custom weapon.";
            return false;
        }

        message = $"Held instance {sequenceGuidHash} -> '{instance.WeaponName}' item:{instance.ItemWeapon} lastOwner:{instance.LastKnownOwnerName}/{instance.LastKnownOwnerPlatformId}";
        return true;
    }



    internal static string DescribeInstances(string filter = "")
    {
        if (_current.Instances.Count == 0)
            return "No instance-bound custom weapons are saved.";

        IEnumerable<CustomWeaponInstanceEntry> entries = _current.Instances.Values;

        if (!string.IsNullOrWhiteSpace(filter))
            entries = entries.Where(e =>
                e.WeaponName.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || e.SequenceGuidHash.ToString().Contains(filter, StringComparison.OrdinalIgnoreCase)
                || e.LastKnownOwnerName.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || e.LastKnownOwnerPlatformId.ToString().Contains(filter, StringComparison.OrdinalIgnoreCase));

        var lines = entries
            .OrderBy(e => e.WeaponName)
            .ThenBy(e => e.SequenceGuidHash)
            .Take(50)
            .Select(e => $"{e.SequenceGuidHash}: '{e.WeaponName}' item:{e.ItemWeapon} lastOwner:{e.LastKnownOwnerName}/{e.LastKnownOwnerPlatformId} lastSeen:{e.LastSeenUtc}")
            .ToList();

        if (lines.Count == 0)
            return "No saved instance weapons matched that filter.";

        if (_current.Instances.Count > lines.Count)
            lines.Add($"Showing {lines.Count} of {_current.Instances.Count}. Use a filter to narrow results.");

        return string.Join("\n", lines);
    }

    internal static int PruneInvalidWeaponNames()
    {
        var validNames = new HashSet<string>(CustomWeaponRegistry.CurrentWeapons.Select(w => w.Name), StringComparer.OrdinalIgnoreCase);
        var remove = _current.Instances
            .Where(kvp => string.IsNullOrWhiteSpace(kvp.Value.WeaponName) || !validNames.Contains(kvp.Value.WeaponName))
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in remove)
            _current.Instances.Remove(key);

        if (remove.Count > 0)
            Save();

        return remove.Count;
    }


    internal static int Count => _current.Instances.Count;

    static void Save()
    {
        Directory.CreateDirectory(ConfigDirectory);
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(_current, JsonOptions));
    }
}
