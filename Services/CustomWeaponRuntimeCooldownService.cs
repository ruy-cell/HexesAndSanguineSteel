namespace HexesAndSanguineSteel;

internal static class CustomWeaponRuntimeCooldownService
{
    internal static int ApplyAllOnline()
    {
        var weapons = CustomWeaponRegistry.CurrentWeapons;
        if (weapons.Count == 0)
            return 0;

        int updated = 0;

        foreach (var player in PlayerLookup.GetOnlinePlayers())
        {
            var scope = WeaponScopeResolver.GetCurrentScope(player.CharacterEntity);
            if (!scope.IsCustomWeapon)
                continue;

            if (CustomWeaponRegistry.TryGetCustomWeaponForCharacter(player.CharacterEntity, out var weapon, out _))
                updated += ApplyWeaponCooldowns(player.CharacterEntity, weapon);
        }

        return updated;
    }

    static int ApplyWeaponCooldowns(Unity.Entities.Entity character, CustomWeaponDef weapon)
    {
        int updated = 0;

        updated += ApplySlot(character, weapon.Name, "Attack", weapon.Attack);
        updated += ApplySlot(character, weapon.Name, "Primary/Q", weapon.Primary);
        updated += ApplySlot(character, weapon.Name, "Secondary/E", weapon.Secondary);
        updated += ApplySlot(character, weapon.Name, "Dash", weapon.Dash);
        updated += ApplySlot(character, weapon.Name, "Ultimate/T", weapon.Ultimate);
        updated += ApplySlot(character, weapon.Name, "Spell Slot 1", weapon.SpellSlot1);
        updated += ApplySlot(character, weapon.Name, "Spell Slot 2", weapon.SpellSlot2);

        return updated;
    }

    static int ApplySlot(Unity.Entities.Entity character, string weaponName, string slotName, AbilitySlotDef ability)
    {
        if (!ability.AbilityGroup.HasValue() || ability.CooldownSeconds <= 0f)
            return 0;

        return AbilityCooldownUtility.SetCooldownOnAttachedAbilityGroup(
            character,
            ability.AbilityGroup,
            ability.CooldownSeconds,
            $"CustomWeaponRuntimeCooldown[{weaponName}:{slotName}]"
        );
    }
}
