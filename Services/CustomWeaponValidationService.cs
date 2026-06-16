using ProjectM;
using Stunlock.Core;

namespace HexesAndSanguineSteel;

internal static class CustomWeaponValidationService
{
    internal static string ValidateCarriers(bool log = true)
    {
        var lines = new List<string>();

        int spellOverrideCarrier = PlayerSpellOverrideConfig.Current.DefaultCarrierBuff;
        int customAbilityCarrier = CustomWeaponConfig.CustomWeaponCarrierBuff;
        int customStatCarrier = CustomWeaponConfig.CustomWeaponStatCarrierBuff;

        AddCarrier(lines, "player spell override ability carrier", spellOverrideCarrier, requiresAbilityBuffer: true, requiresStatBuffer: false);
        AddCarrier(lines, "custom weapon ability carrier", customAbilityCarrier, requiresAbilityBuffer: true, requiresStatBuffer: false);
        AddCarrier(lines, "custom weapon/stat/typebuff carrier", customStatCarrier, requiresAbilityBuffer: false, requiresStatBuffer: true);

        var rolesByHash = new Dictionary<int, Dictionary<string, List<string>>>();

        void Mark(int hash, string role, string owner)
        {
            if (hash == 0)
                return;

            if (!rolesByHash.TryGetValue(hash, out var roles))
            {
                roles = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                rolesByHash[hash] = roles;
            }

            if (!roles.TryGetValue(role, out var owners))
            {
                owners = [];
                roles[role] = owners;
            }

            owners.Add(owner);
        }

        Mark(spellOverrideCarrier, "PlayerSpellAbilityCarrier", "player-spells.json DefaultCarrierBuff");
        Mark(customAbilityCarrier, "CustomWeaponAbilityCarrier", "weapons.json DefaultCustomWeaponCarrierBuff");
        Mark(customStatCarrier, "CustomWeaponStatCarrier", "weapons.json DefaultCustomWeaponStatCarrierBuff");

        foreach (var weapon in CustomWeaponRegistry.CurrentWeapons)
        {
            Mark(weapon.CarrierBuff.GuidHash, "CustomWeaponAbilityCarrier", $"weapon ability carrier: {weapon.Name}");
            Mark(weapon.StatCarrierBuff.GuidHash, "CustomWeaponStatCarrier", $"weapon stat carrier: {weapon.Name}");

            if (weapon.EquipBuff.HasValue())
                Mark(weapon.EquipBuff.GuidHash, "LegacyEquipBuffField", $"legacy EquipBuff field: {weapon.Name}");
        }

        foreach (var kvp in rolesByHash.OrderBy(k => k.Key))
        {
            var roles = kvp.Value.Keys.OrderBy(k => k).ToList();

            // Sharing within the same role is intentional: runtime carriers are reused and instance buffers are rebuilt per player.
            if (roles.Count <= 1)
                continue;

            bool dangerous =
                roles.Contains("PlayerSpellAbilityCarrier") ||
                roles.Contains("LegacyEquipBuffField") ||
                (roles.Contains("CustomWeaponAbilityCarrier") && roles.Contains("CustomWeaponStatCarrier"));

            if (!dangerous)
                continue;

            var ownerText = kvp.Value
                .Select(role => $"{role.Key}: {string.Join(", ", role.Value.Distinct().Take(6))}")
                .ToList();

            lines.Add($"WARNING carrier/buff GUID {kvp.Key} is shared across different carrier roles: {string.Join(" | ", ownerText)}");
        }

        string result = string.Join("\n", lines);

        if (log)
        {
            foreach (var line in lines)
            {
                if (line.StartsWith("WARNING", StringComparison.OrdinalIgnoreCase)
                    || line.Contains("missing", StringComparison.OrdinalIgnoreCase)
                    || line.Contains("does not have", StringComparison.OrdinalIgnoreCase))
                    RuntimeOptimization.WarnOnce($"carrier-validation:{line}", line);
                else
                    RuntimeOptimization.Debug(line);
            }
        }

        return string.IsNullOrWhiteSpace(result) ? "No carrier validation output." : result;
    }

    static void AddCarrier(List<string> lines, string label, int hash, bool requiresAbilityBuffer, bool requiresStatBuffer)
    {
        if (hash == 0)
        {
            lines.Add($"WARNING {label}: 0 / not configured.");
            return;
        }

        var guid = new PrefabGUID(hash);
        if (!guid.TryGetPrefabEntity(out var entity))
        {
            lines.Add($"WARNING {label}: {hash} missing prefab.");
            return;
        }

        bool hasAbilityBuffer = entity.TryGetBuffer<ReplaceAbilityOnSlotBuff>(out _);
        bool hasStatBuffer = entity.TryGetBuffer<ModifyUnitStatBuff_DOTS>(out _);

        lines.Add($"{label}: {hash} exists. ReplaceAbilityOnSlotBuff:{hasAbilityBuffer} ModifyUnitStatBuff_DOTS:{hasStatBuffer}");

        if (requiresAbilityBuffer && !hasAbilityBuffer)
            lines.Add($"WARNING {label}: {hash} does not have ReplaceAbilityOnSlotBuff.");

        if (requiresStatBuffer && !hasStatBuffer)
            lines.Add($"WARNING {label}: {hash} does not have ModifyUnitStatBuff_DOTS.");
    }
}
