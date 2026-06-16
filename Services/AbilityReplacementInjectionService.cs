using ProjectM;
using Stunlock.Core;
using Unity.Entities;

namespace HexesAndSanguineSteel;

internal static class AbilityReplacementInjectionService
{
    static readonly int[] CustomWeaponManagedSlots = [0, 1, 2, 4, 5, 6, 7];
    static readonly int[] PlayerSpellManagedSlots = [1, 2, 4, 5, 6, 7];

    internal static bool TryInjectForBuff(Entity buffEntity, out string message)
    {
        message = string.Empty;

        if (!buffEntity.ExistsSafe() || buffEntity.Has<DestroyTag>())
            return false;

        if (!buffEntity.TryGetBuffer<ReplaceAbilityOnSlotBuff>(out var replacements))
            return false;

        if (!buffEntity.TryRead<EntityOwner>(out var owner) || !owner.Owner.ExistsSafe() || owner.Owner.Has<DestroyTag>())
            return false;

        Entity character = owner.Owner;
        if (!character.Has<PlayerCharacter>())
            return false;

        // Do not inject weapon/spell override replacements into temporary ability bars.
        // Shapeshift forms and feed/V Blood extraction states own their own ability payloads.
        // Injecting custom weapon slots there can make form/feed abilities unusable or crash
        // when high slots such as C/T are outside the temporary abilityGroupSlots buffer.
        if (ShapeshiftUtility.IsFormReplacementBuff(buffEntity) || ShapeshiftUtility.IsActiveForm(character))
        {
            CustomWeaponRuntimeOverrideService.RemoveAbilityCarriers(character);

            if (PlayerLookup.TryFindOnlinePlayer(character, out var formPlayer))
                PlayerSpellOverrideService.RemoveRuntimeCarrierForForm(character, formPlayer.PlatformId);

            message = $"Skipped ability injection while shapeshift/form is active. form:{ShapeshiftUtility.DescribeActiveForm(character)}";
            return false;
        }

        if (FeedInteractionUtility.IsFeedOrExtractionReplacementBuff(buffEntity) || FeedInteractionUtility.IsActiveFeedOrExtraction(character))
        {
            CustomWeaponRuntimeOverrideService.RemoveAbilityCarriers(character);

            if (PlayerLookup.TryFindOnlinePlayer(character, out var feedPlayer))
                PlayerSpellOverrideService.RemoveRuntimeCarrierForTemporaryAbilityState(character, feedPlayer.PlatformId);

            message = $"Skipped ability injection while feed/V Blood extraction is active. feed:{FeedInteractionUtility.DescribeActiveFeedOrExtraction(character)}";
            return false;
        }

        // Custom weapon has highest priority. Inject it only into the active held-weapon replacement
        // buff or our own runtime ability carrier. This is the important crash guard:
        // unknown form/temporary bars can also be owned by the player while a custom weapon is held,
        // but their abilityGroupSlots buffer may be shorter than the normal weapon bar.
        if (CustomWeaponRegistry.TryGetCustomWeaponForCharacter(character, out var weapon, out string source))
        {
            if (!IsExpectedCurrentWeaponReplacementBuff(buffEntity, character, weapon, out string buffDetail))
            {
                message = $"Skipped custom weapon injection into non-weapon replacement buff {buffDetail} for '{weapon.Name}'.";
                return false;
            }

            InjectCustomWeapon(replacements, weapon);
            PlayerSpellOverrideService.ClearRuntimeSpellState(character);
            CustomWeaponRuntimeOverrideService.QueueRuntimeRefresh($"ReplaceAbilityOnSlotSystem injection custom weapon {weapon.Name} ({source})");
            message = $"Injected custom weapon '{weapon.Name}' ({source}) into live ReplaceAbilityOnSlotBuff {buffDetail}.";
            return true;
        }

        if (!PlayerLookup.TryFindOnlinePlayer(character, out var player))
            return false;

        var config = PlayerSpellOverrideConfig.Current;
        if (!config.Players.TryGetValue(player.PlatformId.ToString(), out var entry) || !entry.Enabled)
            return false;

        entry.WeaponScopes ??= [];
        var scope = WeaponScopeResolver.GetCurrentScope(character);

        if (scope.IsCustomWeapon)
            return false;

        if (!entry.WeaponScopes.TryGetValue(scope.ScopeKey, out var profile) || !profile.HasAnySpell())
            return false;

        if (!IsExpectedCurrentWeaponReplacementBuff(buffEntity, character, default, out string playerBuffDetail))
        {
            message = $"Skipped spell override injection into non-weapon replacement buff {playerBuffDetail} for {player.Name}:{scope.ScopeKey}.";
            return false;
        }

        InjectPlayerSpellProfile(replacements, profile);
        PlayerSpellOverrideService.QueueCooldownRefresh($"ReplaceAbilityOnSlotSystem injection spell override {player.Name}:{scope.ScopeKey}");
        message = $"Injected spell override profile for {player.Name} scope '{scope.ScopeKey}' into live ReplaceAbilityOnSlotBuff {playerBuffDetail}.";
        return true;
    }

    static bool IsExpectedCurrentWeaponReplacementBuff(Entity buffEntity, Entity character, CustomWeaponDef weapon, out string detail)
    {
        detail = DescribeBuff(buffEntity);

        if (!buffEntity.TryRead<PrefabGUID>(out var buffPrefab) || !buffPrefab.HasValue())
            return false;

        // Our runtime ability carriers are always safe targets: their payload is owned by this mod
        // and is not a shapeshift/form bar.
        if (CustomWeaponRegistry.ReservedRuntimeCarrierBuffs.Contains(buffPrefab.GuidHash))
            return true;

        // The live vanilla/custom weapon equip buff is safe. Shapeshift, mount, vampire-power,
        // and other temporary bars should not match this held-weapon equip buff.
        if (ItemInstanceUtility.TryGetHeldWeaponEquipBuff(character, out var heldWeaponEquipBuff)
            && heldWeaponEquipBuff.HasValue())
        {
            if (buffPrefab.GuidHash == heldWeaponEquipBuff.GuidHash)
                return true;

            // If we know the current held weapon's equip buff and this buff is not it, reject it.
            // This is stricter than the older form-list-only check and catches newly added skins/forms
            // that are not yet listed in ShapeshiftUtility.ActiveFormBuffs.
            return false;
        }

        // Legacy/custom weapon configs may carry the expected equip buff explicitly.
        if (weapon.EquipBuff.HasValue() && buffPrefab.GuidHash == weapon.EquipBuff.GuidHash)
            return true;

        // Unarmed has no held weapon item. Keep allowing it unless ShapeshiftUtility already marked
        // the replacement buff as a form. This preserves existing spell-override unarmed behavior.
        return true;
    }

    static string DescribeBuff(Entity buffEntity)
    {
        if (!buffEntity.ExistsSafe())
            return "<destroyed>";

        if (buffEntity.TryRead<PrefabGUID>(out var prefabGuid) && prefabGuid.HasValue())
            return $"{prefabGuid.GuidHash} entity:{buffEntity.Index}:{buffEntity.Version}";

        return $"entity:{buffEntity.Index}:{buffEntity.Version}";
    }

    static void InjectCustomWeapon(DynamicBuffer<ReplaceAbilityOnSlotBuff> replacements, CustomWeaponDef weapon)
    {
        AddOrReplace(replacements, slot: 0, weapon.Attack.AbilityGroup);
        AddOrReplace(replacements, slot: 1, weapon.Primary.AbilityGroup);     // Q
        AddOrReplace(replacements, slot: 2, weapon.Dash.AbilityGroup);        // Dash / Space
        AddOrReplace(replacements, slot: 4, weapon.Secondary.AbilityGroup);   // E
        AddOrReplace(replacements, slot: 5, weapon.SpellSlot1.AbilityGroup);  // Spell Slot 1 / R
        AddOrReplace(replacements, slot: 6, weapon.SpellSlot2.AbilityGroup);  // Spell Slot 2 / C
        AddOrReplace(replacements, slot: 7, weapon.Ultimate.AbilityGroup);    // T
    }

    static void InjectPlayerSpellProfile(DynamicBuffer<ReplaceAbilityOnSlotBuff> replacements, PlayerSpellScopeEntry profile)
    {
        AddOrReplace(replacements, slot: 1, new PrefabGUID(profile.Q));       // Q
        AddOrReplace(replacements, slot: 2, new PrefabGUID(profile.Dash));    // Dash / Space
        AddOrReplace(replacements, slot: 4, new PrefabGUID(profile.E));       // E
        AddOrReplace(replacements, slot: 5, new PrefabGUID(profile.R));       // R
        AddOrReplace(replacements, slot: 6, new PrefabGUID(profile.C));       // C
        AddOrReplace(replacements, slot: 7, new PrefabGUID(profile.T));       // T
    }

    static void AddOrReplace(DynamicBuffer<ReplaceAbilityOnSlotBuff> replacements, int slot, PrefabGUID abilityGroup)
    {
        if (!abilityGroup.HasValue())
            return;

        RemoveSlot(replacements, slot);

        replacements.Add(new ReplaceAbilityOnSlotBuff
        {
            Slot = slot,
            NewGroupId = abilityGroup,
            CopyCooldown = false
        });
    }

    static void RemoveSlot(DynamicBuffer<ReplaceAbilityOnSlotBuff> replacements, int slot)
    {
        for (int i = replacements.Length - 1; i >= 0; i--)
        {
            if (replacements[i].Slot == slot)
                replacements.RemoveAt(i);
        }
    }
}
