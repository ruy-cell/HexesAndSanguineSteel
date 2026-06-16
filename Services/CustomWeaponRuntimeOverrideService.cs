using ProjectM;
using Stunlock.Core;
using Unity.Entities;

namespace HexesAndSanguineSteel;

internal static class CustomWeaponRuntimeOverrideService
{
    
    internal static void QueueRuntimeRefresh(string reason)
    {
        PlayerSpellOverrideService.QueueCooldownRefresh(reason);
    }

    internal static void ForceStatReconcile(Entity character, string reason)
    {
        if (!character.ExistsSafe())
            return;

        // Equipment swaps can leave an old stat carrier instance alive while the cached
        // stat hash still says the desired package is current. They can also leave a
        // delayed ScriptSpawn payload from the previous weapon. Treat every equip/swap
        // boundary as authoritative: remove pending payloads and runtime stat carriers,
        // then let the normal ApplyAllOnline pass rebuild the correct stats for the
        // currently equipped weapon/scope.
        CustomWeaponStatCarrierService.ClearPendingForCharacter(character);
        RemoveStatCarriers(character);
        RuntimeStateCache.ClearRuntimeStatHash(character);

        RuntimeOptimization.Debug($"Forced stat reconcile for character {character.Index}:{character.Version}. Reason:{reason}");
    }

    internal static int ApplyAllOnline()
    {
        int applied = 0;

        foreach (var player in PlayerLookup.GetOnlinePlayers())
        {
            if (TryApplyForPlayer(player, out _))
                applied++;
        }

        return applied;
    }

    internal static bool TryApplyForPlayer(PlayerLookupResult player, out string message)
    {
        return TryApplyForCharacter(player.CharacterEntity, out message);
    }

    internal static bool TryApplyForCharacter(Entity character, out string message)
    {
        message = string.Empty;

        if (!character.ExistsSafe())
        {
            message = "Invalid character entity.";
            return false;
        }

        if (ShapeshiftUtility.IsActiveForm(character))
        {
            // Forms replace the weapon bar and should not inherit weapon stat carriers.
            // Remove both ability and stat carriers so weapon stats do not leak into forms
            // or survive the transition back to normal equipment.
            RemoveAllRuntimeCarriers(character);
            message = $"Shapeshift/form active; custom weapon runtime override suppressed. form:{ShapeshiftUtility.DescribeActiveForm(character)}";
            return false;
        }

        if (FeedInteractionUtility.IsActiveFeedOrExtraction(character))
        {
            // Feed / V Blood extraction temporarily owns the ability bar. Do not touch the
            // stat carrier here; only suppress ability carriers/replacement payloads until
            // the interaction ends.
            RemoveAbilityCarriers(character);
            RuntimeStateCache.ClearCustomWeaponAbilityHash(character);
            message = $"Feed/V Blood extraction active; custom weapon ability override suppressed. feed:{FeedInteractionUtility.DescribeActiveFeedOrExtraction(character)}";
            return false;
        }

        var scope = WeaponScopeResolver.GetCurrentScope(character);
        if (!scope.IsCustomWeapon)
        {
            RemoveAbilityCarriers(character);
            RuntimeStateCache.ClearCustomWeaponAbilityHash(character);

            int typeStats = ApplyMergedStats(
                character,
                baseStats: Array.Empty<CustomWeaponStatDef>(),
                statCarrierGuid: new PrefabGUID(CustomWeaponConfig.CustomWeaponStatCarrierBuff),
                statCarrierHash: CustomWeaponConfig.CustomWeaponStatCarrierBuff,
                label: $"WeaponType[{scope.ScopeKey}]",
                customWeaponActive: false
            );

            message = typeStats > 0
                ? $"No custom weapon equipped. Applied weapon-type stat buff for current scope:{scope.ScopeKey}, stats:{typeStats}."
                : $"No custom weapon equipped. Current scope:{scope.ScopeKey}.";

            return typeStats > 0;
        }

        if (!CustomWeaponRegistry.TryGetCustomWeaponForCharacter(character, out var weapon, out string source))
        {
            RemoveAllRuntimeCarriers(character);
            message = $"Custom weapon scope detected, but no matching definition exists for current held weapon. Scope:{scope.ScopeKey}.";
            return false;
        }

        return ApplyWeapon(character, weapon, source, out message);
    }

    static bool ApplyWeapon(Entity character, CustomWeaponDef weapon, string source, out string message)
    {
        int abilityCarrierHash = ResolveAbilityCarrier(weapon);
        var abilityCarrierGuid = new PrefabGUID(abilityCarrierHash);

        if (!abilityCarrierGuid.TryGetPrefabEntity(out var abilityCarrierPrefab))
        {
            message = $"Custom weapon ability carrier buff prefab {abilityCarrierHash} was not found for {weapon.Name}.";
            return false;
        }

        if (!abilityCarrierPrefab.TryGetBuffer<ReplaceAbilityOnSlotBuff>(out _))
        {
            message = $"Custom weapon ability carrier buff prefab {abilityCarrierHash} does not have ReplaceAbilityOnSlotBuff buffer.";
            return false;
        }

        int statCarrierHash = ResolveStatCarrier(weapon);
        var statCarrierGuid = new PrefabGUID(statCarrierHash);

        if (weapon.Stats.Count > 0)
        {
            if (!statCarrierGuid.TryGetPrefabEntity(out var statCarrierPrefab))
            {
                message = $"Custom weapon stat carrier buff prefab {statCarrierHash} was not found for {weapon.Name}.";
                return false;
            }

            if (!statCarrierPrefab.TryGetBuffer<ModifyUnitStatBuff_DOTS>(out _))
            {
                message = $"Custom weapon stat carrier buff prefab {statCarrierHash} does not have ModifyUnitStatBuff_DOTS buffer.";
                return false;
            }
        }

        int abilityHash = ComputeWeaponAbilityHash(weapon, source, abilityCarrierHash);

        bool abilityCarrierAlreadyApplied =
            RuntimeStateCache.ShouldSkipCustomWeaponAbilityApply(character, abilityHash)
            && character.TryGetBuff(abilityCarrierGuid, out _);

        if (!abilityCarrierAlreadyApplied)
        {
            RemoveAbilityCarriers(character);
            PlayerSpellOverrideService.ClearRuntimeSpellState(character);

            if (!Core.ServerGameManager.TryInstantiateBuffEntityImmediate(character, character, abilityCarrierGuid, out Entity abilityBuffEntity))
            {
                if (!character.TryGetBuff(abilityCarrierGuid, out abilityBuffEntity))
                {
                    message = $"Failed to apply custom weapon runtime ability carrier buff {abilityCarrierHash} for {weapon.Name}.";
                    return false;
                }
            }

            if (!abilityBuffEntity.TryGetBuffer<ReplaceAbilityOnSlotBuff>(out var replacements))
            {
                message = $"Applied custom weapon ability carrier buff {abilityCarrierHash}, but its instance has no ReplaceAbilityOnSlotBuff buffer.";
                return false;
            }

            replacements.Clear();

            AddSlot(replacements, slot: 0, weapon.Attack);
            AddSlot(replacements, slot: 1, weapon.Primary);   // Q
            AddSlot(replacements, slot: 2, weapon.Dash);      // Space / dash
            AddSlot(replacements, slot: 5, weapon.SpellSlot1);         // Spell Slot 1
            AddSlot(replacements, slot: 4, weapon.Secondary); // E
            AddSlot(replacements, slot: 6, weapon.SpellSlot2);         // Spell Slot 2
            AddSlot(replacements, slot: 7, weapon.Ultimate);  // T

            RuntimeStateCache.SetCustomWeaponAbilityHash(character, abilityHash);
        }

        int statsApplied = ApplyMergedStats(character, weapon.Stats, statCarrierGuid, statCarrierHash, weapon.Name, customWeaponActive: true);
        int cooldowns = ApplyCooldowns(character, weapon);

        message = abilityCarrierAlreadyApplied
            ? $"Runtime custom weapon '{weapon.Name}' already current ({source}). statUpdates:{statsApplied} cooldownAttachedUpdates:{cooldowns}."
            : $"Applied runtime custom weapon '{weapon.Name}' ({source}) abilityCarrier:{abilityCarrierHash} statCarrier:{statCarrierHash} stats:{statsApplied} cooldownAttachedUpdates:{cooldowns}.";
        return true;
    }

    static int ComputeWeaponAbilityHash(CustomWeaponDef weapon, string source, int carrierHash)
    {
        var hash = new HashCode();
        hash.Add(weapon.Name, StringComparer.OrdinalIgnoreCase);
        hash.Add(source);
        hash.Add(carrierHash);
        AddAbilityHash(ref hash, weapon.Attack);
        AddAbilityHash(ref hash, weapon.Primary);
        AddAbilityHash(ref hash, weapon.Secondary);
        AddAbilityHash(ref hash, weapon.Dash);
        AddAbilityHash(ref hash, weapon.Ultimate);
        AddAbilityHash(ref hash, weapon.SpellSlot1);
        AddAbilityHash(ref hash, weapon.SpellSlot2);
        return hash.ToHashCode();
    }

    static int ComputeStatHash(int carrierHash, IReadOnlyList<CustomWeaponStatDef> stats, bool customWeaponActive, string label)
    {
        var hash = new HashCode();
        hash.Add(carrierHash);
        hash.Add(customWeaponActive);
        hash.Add(label, StringComparer.OrdinalIgnoreCase);

        foreach (var stat in stats)
        {
            hash.Add(stat.StatType);
            hash.Add(stat.ModificationType);
            hash.Add(stat.AttributeCapType);
            hash.Add(stat.Value);
            hash.Add(stat.Modifier);
        }

        return hash.ToHashCode();
    }

    static void AddAbilityHash(ref HashCode hash, AbilitySlotDef ability)
    {
        hash.Add(ability.AbilityGroup.GuidHash);
        hash.Add(ability.CooldownSeconds);
    }

    static int ResolveAbilityCarrier(CustomWeaponDef weapon)
    {
        if (weapon.CarrierBuff.HasValue())
            return weapon.CarrierBuff.GuidHash;

        return CustomWeaponConfig.CustomWeaponCarrierBuff;
    }

    static int ResolveStatCarrier(CustomWeaponDef weapon)
    {
        if (weapon.StatCarrierBuff.HasValue())
            return weapon.StatCarrierBuff.GuidHash;

        return CustomWeaponConfig.CustomWeaponStatCarrierBuff;
    }

    static void AddSlot(DynamicBuffer<ReplaceAbilityOnSlotBuff> replacements, int slot, AbilitySlotDef ability)
    {
        if (!ability.AbilityGroup.HasValue())
            return;

        replacements.Add(new ReplaceAbilityOnSlotBuff
        {
            Slot = slot,
            NewGroupId = ability.AbilityGroup,
            CopyCooldown = false
        });
    }

    static int ApplyMergedStats(
        Entity character,
        IReadOnlyList<CustomWeaponStatDef> baseStats,
        PrefabGUID statCarrierGuid,
        int statCarrierHash,
        string label,
        bool customWeaponActive)
    {
        var mergedStats = new List<CustomWeaponStatDef>();

        if (baseStats.Count > 0)
            mergedStats.AddRange(baseStats);

        if (CustomWeaponTypeBuffService.TryGetMatchingStats(character, customWeaponActive, out var typeStats, out string weaponType, out int typeEntries))
        {
            mergedStats.AddRange(typeStats);
            RuntimeOptimization.Debug($"Merged weapon-type stats '{weaponType}' into runtime stat carrier for {label}. customWeapon:{customWeaponActive} typeEntries:{typeEntries} typeStats:{typeStats.Count}");
        }

        int statHash = ComputeStatHash(statCarrierHash, mergedStats, customWeaponActive, label);

        if (mergedStats.Count == 0)
        {
            RemoveStatCarriers(character);
            RuntimeStateCache.ClearRuntimeStatHash(character);
            return 0;
        }

        if (RuntimeStateCache.ShouldSkipRuntimeStats(character, statHash)
            && character.TryGetBuff(statCarrierGuid, out _))
            return 0;

        // PerkShop-inspired stat carrier path:
        // - prepare the carrier prefab with ScriptSpawn + ModifyUnitStatBuff_DOTS when possible;
        // - spawn/apply the carrier normally;
        // - populate the player-owned spawned buff instance, not the global prefab;
        // - add SyncToUserBuffer and stable modifier IDs for better HUD/network sync.
        return CustomWeaponStatCarrierService.ApplyStats(
            character,
            statCarrierGuid,
            statCarrierHash,
            mergedStats,
            statHash,
            label
        );
    }

    static int ApplyCooldowns(Entity character, CustomWeaponDef weapon)
    {
        int updated = 0;

        updated += ApplySlotCooldown(character, weapon.Name, "Attack", weapon.Attack);
        updated += ApplySlotCooldown(character, weapon.Name, "Primary/Q", weapon.Primary);
        updated += ApplySlotCooldown(character, weapon.Name, "Secondary/E", weapon.Secondary);
        updated += ApplySlotCooldown(character, weapon.Name, "Dash", weapon.Dash);
        updated += ApplySlotCooldown(character, weapon.Name, "Ultimate/T", weapon.Ultimate);
        updated += ApplySlotCooldown(character, weapon.Name, "Spell Slot 1", weapon.SpellSlot1);
        updated += ApplySlotCooldown(character, weapon.Name, "Spell Slot 2", weapon.SpellSlot2);

        return updated;
    }

    static int ApplySlotCooldown(Entity character, string weaponName, string slotName, AbilitySlotDef ability)
    {
        if (!ability.AbilityGroup.HasValue() || ability.CooldownSeconds <= 0f)
            return 0;

        return AbilityCooldownUtility.SetCooldownOnAttachedAbilityGroup(
            character,
            ability.AbilityGroup,
            ability.CooldownSeconds,
            $"CustomWeaponRuntimeOverride[{weaponName}:{slotName}]"
        );
    }

    internal static void RemoveAllRuntimeCarriers(Entity character)
    {
        RemoveAbilityCarriers(character);
        RemoveStatCarriers(character);
        RuntimeStateCache.ClearCustomWeaponAbilityHash(character);
        RuntimeStateCache.ClearRuntimeStatHash(character);
    }

    internal static void RemoveAbilityCarriers(Entity character)
    {
        if (!character.ExistsSafe())
            return;

        foreach (int carrierHash in CustomWeaponRegistry.ReservedRuntimeCarrierBuffs)
        {
            if (carrierHash == 0)
                continue;

            var carrierGuid = new PrefabGUID(carrierHash);
            if (character.TryGetBuff(carrierGuid, out Entity existing))
                existing.DestroySafe();
        }

        RuntimeStateCache.ClearCustomWeaponAbilityHash(character);
    }

    internal static void RemoveStatCarriers(Entity character)
    {
        if (!character.ExistsSafe())
            return;

        CustomWeaponStatCarrierService.ClearPendingForCharacter(character);

        foreach (int carrierHash in CustomWeaponRegistry.ReservedRuntimeStatCarrierBuffs)
        {
            if (carrierHash == 0)
                continue;

            var carrierGuid = new PrefabGUID(carrierHash);
            if (character.TryGetBuff(carrierGuid, out Entity existing))
                existing.DestroySafe();
        }

        RuntimeStateCache.ClearRuntimeStatHash(character);
    }
}
