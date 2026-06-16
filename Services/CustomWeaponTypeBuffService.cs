using ProjectM;
using Unity.Entities;

namespace HexesAndSanguineSteel;

internal static class CustomWeaponTypeBuffService
{
    internal static IReadOnlyList<WeaponTypeBuffDef> CurrentBuffs => CustomWeaponConfig.WeaponTypeBuffs;

    internal static bool TryGetMatchingStats(
        Entity character,
        bool customWeaponActive,
        out IReadOnlyList<CustomWeaponStatDef> stats,
        out string weaponType,
        out int matchedEntries)
    {
        stats = Array.Empty<CustomWeaponStatDef>();
        weaponType = string.Empty;
        matchedEntries = 0;

        if (!character.ExistsSafe())
            return false;

        var scope = WeaponScopeResolver.GetCurrentScope(character);

        if (scope.IsUnarmed || scope.WeaponPrefabGuid == 0)
            return false;

        if (!WeaponScopeResolver.TryGetWeaponTypeForPrefab(scope.WeaponPrefabGuid, out weaponType))
            return false;

        var merged = new List<CustomWeaponStatDef>();

        foreach (var candidate in CurrentBuffs)
        {
            if (customWeaponActive && !candidate.ApplyToCustomWeapons)
                continue;

            if (!string.Equals(candidate.WeaponType, weaponType, StringComparison.OrdinalIgnoreCase))
                continue;

            if (candidate.Stats.Count == 0)
            {
                RuntimeOptimization.Debug($"WeaponTypeBuff '{candidate.WeaponType}' matched {weaponType}, but it has no valid stats after config parsing.");
                continue;
            }

            matchedEntries++;
            merged.AddRange(candidate.Stats);
        }

        if (merged.Count == 0)
            return false;

        stats = merged;
        return true;
    }

    internal static int ApplyAllOnline(bool forceReconcile = false)
    {
        int applied = 0;

        foreach (var player in PlayerLookup.GetOnlinePlayers())
        {
            if (forceReconcile)
                CustomWeaponRuntimeOverrideService.ForceStatReconcile(player.CharacterEntity, "applytypebuffs");

            if (CustomWeaponRuntimeOverrideService.TryApplyForPlayer(player, out _))
                applied++;
        }

        return applied;
    }

    internal static string Describe()
    {
        var buffs = CurrentBuffs;

        if (buffs.Count == 0)
            return "No enabled WeaponTypeBuffs are loaded. Check that entries have \"Enabled\": true and run .csw reload.";

        return string.Join("\n", buffs.Select(b =>
            $"{b.WeaponType}: applyToCustom:{b.ApplyToCustomWeapons} validStats:{b.Stats.Count}"
        ));
    }

    internal static string DescribeCurrentScope(Entity character)
    {
        if (!character.ExistsSafe())
            return "Invalid character entity.";

        var scope = WeaponScopeResolver.GetCurrentScope(character);

        if (scope.IsUnarmed)
            return $"Current weapon scope is unarmed. detail:{scope.Detail}";

        if (!WeaponScopeResolver.TryGetWeaponTypeForPrefab(scope.WeaponPrefabGuid, out string weaponType))
            return $"Current weapon prefab {scope.WeaponPrefabGuid} is not in the WeaponScopeResolver type map. scope:{scope.ScopeKey} detail:{scope.Detail}";

        bool activeCustom = scope.IsCustomWeapon;
        if (!TryGetMatchingStats(character, activeCustom, out var stats, out _, out int entries))
            return $"Current weapon type '{weaponType}' has no matching enabled WeaponTypeBuffs. customWeapon:{activeCustom} scope:{scope.ScopeKey} detail:{scope.Detail}";

        return $"Current weapon type '{weaponType}' matches {entries} WeaponTypeBuff entr{(entries == 1 ? "y" : "ies")} with {stats.Count} valid stat row(s). customWeapon:{activeCustom} scope:{scope.ScopeKey} detail:{scope.Detail}";
    }
}
