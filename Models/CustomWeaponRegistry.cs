using ProjectM;
using Stunlock.Core;
using Unity.Entities;

namespace HexesAndSanguineSteel;

internal static class CustomWeaponRegistry
{
    internal static IReadOnlyList<CustomWeaponDef> CurrentWeapons { get; private set; } = Array.Empty<CustomWeaponDef>();

    static readonly Dictionary<int, CustomWeaponDef> _customWeaponsByItem = new();
    static readonly Dictionary<int, string> _customWeaponNamesByItem = new();
    static readonly Dictionary<string, CustomWeaponDef> _customWeaponsByName = new(StringComparer.OrdinalIgnoreCase);
    static readonly HashSet<int> _reservedRuntimeCarrierBuffs = new();
    static readonly HashSet<int> _reservedRuntimeStatCarrierBuffs = new();

    internal static bool IsCustomWeaponItem(int itemWeaponGuidHash)
        => _customWeaponsByItem.ContainsKey(itemWeaponGuidHash);

    internal static bool TryGetCustomWeapon(int itemWeaponGuidHash, out CustomWeaponDef weapon)
        => _customWeaponsByItem.TryGetValue(itemWeaponGuidHash, out weapon);

    internal static bool TryGetCustomWeaponByName(string name, out CustomWeaponDef weapon)
    {
        weapon = default;

        if (string.IsNullOrWhiteSpace(name))
            return false;

        return _customWeaponsByName.TryGetValue(name, out weapon);
    }

    internal static bool TryGetCustomWeaponForCharacter(Entity character, out CustomWeaponDef weapon, out string source)
    {
        source = string.Empty;

        // Highest priority: exact item instance created/bound by .csw wepgive.
        if (CustomWeaponInstanceStore.TryGetHeldInstanceWeapon(character, out weapon, out var instance))
        {
            source = $"instance:{instance.SequenceGuidHash}";
            return true;
        }

        // Fallback: prefab-bound custom weapon. This only works for ItemWeapon prefabs that are unique in weapons.json.
        if (ItemInstanceUtility.TryGetHeldWeaponPrefab(character, out var itemPrefab)
            && TryGetCustomWeapon(itemPrefab.GuidHash, out weapon))
        {
            source = $"prefab:{itemPrefab.GuidHash}";
            return true;
        }

        weapon = default;
        return false;
    }

    // Name kept for player-spell carrier conflict protection.
    // In schema v5+ this protects custom weapon runtime carriers, not per-weapon equip packages.
    internal static bool IsCustomWeaponEquipBuff(int equipBuffGuidHash)
        => _reservedRuntimeCarrierBuffs.Contains(equipBuffGuidHash)
            || _reservedRuntimeStatCarrierBuffs.Contains(equipBuffGuidHash);

    internal static string GetCustomWeaponUsingEquipBuff(int equipBuffGuidHash)
        => _reservedRuntimeCarrierBuffs.Contains(equipBuffGuidHash)
            ? $"custom weapon runtime ability carrier {equipBuffGuidHash}"
            : _reservedRuntimeStatCarrierBuffs.Contains(equipBuffGuidHash)
                ? $"custom weapon runtime stat carrier {equipBuffGuidHash}"
                : $"custom weapon using equip buff {equipBuffGuidHash}";

    internal static string GetCustomWeaponName(int itemWeaponGuidHash)
        => _customWeaponNamesByItem.TryGetValue(itemWeaponGuidHash, out string? name)
            ? name
            : $"Custom weapon {itemWeaponGuidHash}";

    internal static IReadOnlyCollection<int> ReservedRuntimeCarrierBuffs => _reservedRuntimeCarrierBuffs;
    internal static IReadOnlyCollection<int> ReservedRuntimeStatCarrierBuffs => _reservedRuntimeStatCarrierBuffs;

    internal static void ApplyAll()
    {
        RuntimePrefabCache.Clear();
        CustomWeaponStatCarrierService.Reset();

        var weapons = CustomWeaponConfig.LoadOrCreate();
        CurrentWeapons = weapons;

        _customWeaponsByItem.Clear();
        _customWeaponNamesByItem.Clear();
        _customWeaponsByName.Clear();
        _reservedRuntimeCarrierBuffs.Clear();
        _reservedRuntimeStatCarrierBuffs.Clear();

        var countsByItem = weapons
            .GroupBy(w => w.ItemWeapon.GuidHash)
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (var weapon in weapons)
        {
            if (!_customWeaponsByName.TryAdd(weapon.Name, weapon))
            {
                RuntimeOptimization.WarnOnce($"dup-name:{weapon.Name}", $"Duplicate custom weapon name '{weapon.Name}'. .csw wepgive uses names, so only the first definition with this name can be targeted reliably.");
            }

            if (countsByItem.TryGetValue(weapon.ItemWeapon.GuidHash, out int count) && count == 1)
            {
                _customWeaponsByItem[weapon.ItemWeapon.GuidHash] = weapon;
                _customWeaponNamesByItem[weapon.ItemWeapon.GuidHash] = weapon.Name;
            }
            else
            {
                RuntimeOptimization.WarnOnce($"dup-item:{weapon.ItemWeapon.GuidHash}", $"ItemWeapon {weapon.ItemWeapon.GuidHash} is used by multiple custom weapon definitions. It will not be prefab-bound; give it with .csw wepgive so the generated item instance decides which definition applies.");
            }

            if (weapon.CarrierBuff.HasValue())
                _reservedRuntimeCarrierBuffs.Add(weapon.CarrierBuff.GuidHash);

            if (weapon.StatCarrierBuff.HasValue())
                _reservedRuntimeStatCarrierBuffs.Add(weapon.StatCarrierBuff.GuidHash);
        }

        if (CustomWeaponConfig.CustomWeaponCarrierBuff != 0)
            _reservedRuntimeCarrierBuffs.Add(CustomWeaponConfig.CustomWeaponCarrierBuff);

        if (CustomWeaponConfig.CustomWeaponStatCarrierBuff != 0)
            _reservedRuntimeStatCarrierBuffs.Add(CustomWeaponConfig.CustomWeaponStatCarrierBuff);

        CustomWeaponValidationService.ValidateCarriers(log: true);

        if (weapons.Count == 0)
        {
            RuntimeOptimization.WarnOnce("no-enabled-weapons", $"No enabled custom weapons found in {CustomWeaponConfig.ConfigPath}.");
            return;
        }

        RuntimeOptimization.InfoOnce("custom-weapon-apply-summary", $"Loaded {weapons.Count} custom weapon definition(s), {CustomWeaponConfig.WeaponTypeBuffs.Count} weapon type buff(s), runtime carrier {CustomWeaponConfig.CustomWeaponCarrierBuff}, stat carrier {CustomWeaponConfig.CustomWeaponStatCarrierBuff}. Use .csw validate for details.");
        RuntimeOptimization.Debug($"WeaponTypeBuffs loaded: {CustomWeaponConfig.WeaponTypeBuffs.Count}. They share the runtime stat carrier {CustomWeaponConfig.CustomWeaponStatCarrierBuff} and are merged into one active player stat carrier.");

        foreach (var weapon in weapons)
        {
            try
            {
                if (countsByItem.TryGetValue(weapon.ItemWeapon.GuidHash, out int itemCountForStats) && itemCountForStats == 1)
                {
                    ApplyPrefabWeaponLevelOnly(weapon);
                }
                else
                {
                    RuntimeOptimization.Debug($"{weapon.Name}: skipped prefab WeaponLevelSource mutation because ItemWeapon {weapon.ItemWeapon.GuidHash} is shared by multiple custom weapon definitions. Ability/stat behavior will be instance-bound by .csw wepgive.");
                }

                ValidateRuntimeCarriers(weapon);

                RuntimeOptimization.Debug($"Registered runtime custom weapon: {weapon.Name} item:{weapon.ItemWeapon.GuidHash} abilityCarrier:{weapon.CarrierBuff.GuidHash} statCarrier:{weapon.StatCarrierBuff.GuidHash} stats:{weapon.Stats.Count} onHit:{weapon.OnHitEffects.Count}");
            }
            catch (Exception ex)
            {
                Plugin.LogInstance.LogError($"Failed custom weapon '{weapon.Name}': {ex}");
            }
        }
    }

    static void ApplyPrefabWeaponLevelOnly(CustomWeaponDef weapon)
    {
        if (!weapon.ItemWeapon.TryGetPrefabEntity(out var weaponEntity))
        {
            RuntimeOptimization.WarnOnce($"missing-item:{weapon.ItemWeapon.GuidHash}", $"{weapon.Name}: item prefab not found: {weapon.ItemWeapon.GuidHash}");
            return;
        }

        weaponEntity.With<WeaponLevelSource>((ref WeaponLevelSource source) =>
        {
            source.Level = weapon.Power.WeaponLevel;
        });

        // Schema v6+:
        // Do NOT mutate item ModifyUnitStatBuff_DOTS here. PhysicalPower, SpellPower, AttackSpeed,
        // resistances, etc. are now applied with a runtime stat carrier while the weapon is equipped.
    }

    static void ValidateRuntimeCarriers(CustomWeaponDef weapon)
    {
        if (weapon.CarrierBuff.HasValue())
        {
            if (weapon.CarrierBuff.TryGetPrefabEntity(out var abilityCarrier))
            {
                if (!abilityCarrier.TryGetBuffer<ReplaceAbilityOnSlotBuff>(out _))
                    RuntimeOptimization.WarnOnce($"ability-carrier-no-buffer:{weapon.CarrierBuff.GuidHash}", $"{weapon.Name}: ability carrier {weapon.CarrierBuff.GuidHash} does not have ReplaceAbilityOnSlotBuff.");
            }
            else
            {
                RuntimeOptimization.WarnOnce($"ability-carrier-missing:{weapon.CarrierBuff.GuidHash}", $"{weapon.Name}: ability carrier {weapon.CarrierBuff.GuidHash} not found.");
            }
        }

        if (weapon.StatCarrierBuff.HasValue() && weapon.Stats.Count > 0)
        {
            if (weapon.StatCarrierBuff.TryGetPrefabEntity(out var statCarrier))
            {
                if (!statCarrier.TryGetBuffer<ModifyUnitStatBuff_DOTS>(out _))
                    RuntimeOptimization.WarnOnce($"stat-carrier-no-buffer:{weapon.StatCarrierBuff.GuidHash}", $"{weapon.Name}: stat carrier {weapon.StatCarrierBuff.GuidHash} does not have ModifyUnitStatBuff_DOTS.");
            }
            else
            {
                RuntimeOptimization.WarnOnce($"stat-carrier-missing:{weapon.StatCarrierBuff.GuidHash}", $"{weapon.Name}: stat carrier {weapon.StatCarrierBuff.GuidHash} not found.");
            }
        }
    }
}
