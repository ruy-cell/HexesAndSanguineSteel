using ProjectM;
using Stunlock.Core;

namespace HexesAndSanguineSteel;

internal readonly record struct AbilitySlotDef(PrefabGUID AbilityGroup, float CooldownSeconds = 0f);

internal readonly record struct WeaponPowerDef(
    float WeaponLevel,
    float PhysicalPower,
    float SpellPower)
{
    internal static WeaponPowerDef Default => new(100f, 35f, 10f);
}

internal readonly record struct CustomWeaponStatDef(
    UnitStatType StatType,
    ModificationType ModificationType,
    AttributeCapType AttributeCapType,
    float Value,
    float Modifier = 1f);


internal readonly record struct WeaponTypeBuffDef(
    string WeaponType,
    bool ApplyToCustomWeapons,
    IReadOnlyList<CustomWeaponStatDef> Stats);

internal readonly record struct CustomWeaponCostDef(
    string Name,
    PrefabGUID Item,
    int Amount);

internal readonly record struct CustomWeaponOnHitEffectDef(
    string Name,
    string School,
    float Chance,
    PrefabGUID TargetBuff,
    bool AffectPlayers,
    bool AffectNonPlayers);

internal readonly record struct CustomWeaponDef(
    string Name,
    PrefabGUID ItemWeapon,

    // Legacy field kept for old configs/source compatibility.
    // Option 3 no longer uses this as the custom weapon ability container.
    PrefabGUID EquipBuff,

    // Runtime carrier used only while a player has this custom weapon equipped.
    // 0 means use weapons.json DefaultCustomWeaponCarrierBuff.
    PrefabGUID CarrierBuff,

    // Runtime stat carrier used only while a player has this custom weapon equipped.
    // 0 means use weapons.json DefaultCustomWeaponStatCarrierBuff.
    PrefabGUID StatCarrierBuff,

    // Deprecated/unused. .csw upgrade uses CustomWeaponConfig.GlobalUpgradeCost only.
    IReadOnlyList<CustomWeaponCostDef> UpgradeCost,

    WeaponPowerDef Power,
    IReadOnlyList<CustomWeaponStatDef> Stats,
    IReadOnlyList<CustomWeaponOnHitEffectDef> OnHitEffects,
    AbilitySlotDef Attack,
    AbilitySlotDef Primary,
    AbilitySlotDef Secondary,
    AbilitySlotDef Dash,
    AbilitySlotDef Ultimate,

    // Extra custom-weapon-only override slots.
    // Spell Slot 1 -> slot 5, Spell Slot 2 -> slot 6.
    AbilitySlotDef SpellSlot1,
    AbilitySlotDef SpellSlot2);
