# Hexes and Sanguine Steel

Server-side custom weapon framework for V Rising.

Create named weapons, replace weapon and spells ability slots, customize their cooldowns, add stat bonuses, and apply school on-hit effects.

This mod bases itself on admin commands, but the players can access to the weapon through the upgrade system. 

If the weapon type bonuses are enabled, every weapon of that type will recieve the benefit of it. With this admins/server owners can change how some weapons behave regarding stats and balance them out in case a weapon is being overshadowed by the other-

Aside from the weapons, this mod allows to replace weapons/spells to the Q|E|Dash|R|C|T keys and manually set their cooldowns as the user pleases. 

# Credits

This mod is heavealy inspired for its systems and ideas by the following amazing mods and people:

- [Bloodcraft](https://thunderstore.io/c/v-rising/p/zfolmt/Bloodcraft/) by [zfolmt](https://thunderstore.io/c/v-rising/p/zfolmt/) — inspiration/reference for the boss style weapons
- [CustomAbilities](https://thunderstore.io/c/v-rising/p/Fryke/CustomAbilities/) by [Fryke](https://github.com/Tmktahu) — inspiration/reference for custom spells and abilities workflow.

## Table of Contents

- [Feature Overview](#feature-overview)
- [Install](#install)
- [Configuration](#configuration)
- [Creating Custom Weapons](#creating-custom-weapons)
- [Using Commands](#using-commands)
- [Compatibility Notes](#compatibility-notes)
- [Development](#development)

## Feature Overview

<details>
<summary><strong>Custom Weapons</strong></summary>

- Define custom weapons in `weapons.json`.
- Override Attack, Q, E, Dash, R, C, and Ultimate-style slots.
- Set cooldowns per configured ability.
- Bind weapons to a specific item instance with `.csw wepgive` or `.csw upgrade`.
- Upgrade held Shadow Matter weapons into configured custom weapons.

</details>

<details>
<summary><strong>Stats and Weapon Type Bonuses</strong></summary>

- Apply weapon stats through runtime stat carriers instead of global prefab mutation.
- Add per-weapon `Stats[]` entries.
- Add `WeaponTypeBuffs` for weapon-type-wide stat rules.
- Supports stat aliases such as `AttackSpeed` → `PrimaryAttackSpeed`.

</details>

<details>
<summary><strong>School On-Hit Effects</strong></summary>

- Add Bloodcraft-style school effects through `OnHitEffects[]`.
- Supported proven school effects include Leech, Static, Chill, Ignite, Weaken, and Condemn.
- Effects can be restricted to players, non-players, or both.

</details>

<details>
<summary><strong>Runtime Safety</strong></summary>

- Suppresses custom ability injection during shapeshift forms.
- Suppresses custom ability injection during feed and V Blood extraction states.
- Guards replacement slots against temporary ability bars with fewer slots.
- Normal logging is quiet; detailed logs require `DebugLogging: true`.

</details>

## Install

1. Install BepInEx IL2CPP for your V Rising dedicated server.
2. Install VampireCommandFramework.
3. Build or download `HexesAndSanguineSteel.dll`.
4. Place the DLL in:

```text
BepInEx/plugins/HexesAndSanguineSteel/
```

5. Start the server once to generate config files.

## Configuration

Generated config files live in:

```text
BepInEx/config/HexesAndSanguineSteel/
```

Main files:

```text
weapons.json           Custom weapons, stats, weapon type buffs, and on-hit effects
player-spells.json     Saved per-player weapon-scoped spell overrides
weapon-instances.json  Saved item-instance custom weapon bindings
```

A minimal starter config is included at:

```text
Resources/weapons.example.json
```

Legacy configs from `BepInEx/config/CustomWeaponMod/` are copied into the new folder on first run when needed.

## Creating Custom Weapons

Custom weapons are configured in `weapons.json`.

The safest workflow is:

```text
1. Start the server once so the config folder is generated.
2. Open BepInEx/config/HexesAndSanguineSteel/weapons.json.
3. Copy an existing weapon entry in the Weapons array.
4. Change Name, ItemWeapon, abilities, cooldowns, stats, and optional on-hit effects.
5. Save the file.
6. Run .csw reload in-game.
7. Run .csw validate.
8. Give or bind the weapon with .csw wepgive or .csw upgrade.
```

### Minimal weapon entry

```json
{
  "Enabled": true,
  "Name": "Crimson Thorns",
  "ItemWeapon": 1307774440,
  "EquipBuff": 673013659,
  "CarrierBuff": 0,
  "StatCarrierBuff": 0,
  "WeaponLevel": 100,
  "PhysicalPower": 0,
  "SpellPower": 0,
  "Stats": [
    {
      "StatType": "PhysicalPower",
      "ModificationType": "AddToBase",
      "AttributeCapType": "SoftCapped",
      "Value": 35
    },
    {
      "StatType": "SpellPower",
      "ModificationType": "AddToBase",
      "AttributeCapType": "SoftCapped",
      "Value": 10
    }
  ],
  "Attack": -208121356,
  "AttackCooldown": 0,
  "Primary": 1826128809,
  "PrimaryCooldown": 8,
  "Secondary": 1730729556,
  "SecondaryCooldown": 10,
  "Dash": -1940289109,
  "DashCooldown": 6,
  "Ultimate": -1730693034,
  "UltimateCooldown": 60,
  "Spell Slot 1": 841757706,
  "Spell Slot 1 Cooldown": 8,
  "Spell Slot 2": 1295370119,
  "Spell Slot 2 Cooldown": 8,
  "OnHitEffects": []
}
```

### Weapon fields

| Field | Purpose |
| --- | --- |
| `Name` | The name used by `.csw wepgive`, `.csw upgrade`, and `.csw list`. |
| `ItemWeapon` | The base weapon item prefab GUID. This controls which weapon item is used. |
| `EquipBuff` | The weapon equip buff reference. Keep the matching vanilla equip buff for the base weapon. |
| `CarrierBuff` | Optional ability carrier override. Use `0` to use `DefaultCustomWeaponCarrierBuff`. |
| `StatCarrierBuff` | Optional stat carrier override. Use `0` to use `DefaultCustomWeaponStatCarrierBuff`. |
| `WeaponLevel` | Weapon level/power display value used by the custom weapon definition. |
| `Stats[]` | Runtime stat modifiers. This is the preferred stat system. |
| `Attack`, `Primary`, `Secondary`, `Dash`, `Ultimate`, `Spell Slot 1`, `Spell Slot 2` | Ability group prefab GUIDs for each slot. Use `0` to leave a slot unchanged. |
| `*Cooldown` | Cooldown in seconds for the matching ability. |
| `OnHitEffects[]` | Optional school debuffs applied on hit. |

Keep `PhysicalPower` and `SpellPower` at `0` when using `Stats[]` to avoid double-stacking.

### Ability slot mapping

```text
Attack       Basic attack
Primary      Q
Secondary    E
Dash         Dash
Ultimate     T
Spell Slot 1 R-style slot
Spell Slot 2 C-style slot
```

Use `*_AbilityGroup` prefab GUIDs for ability slots. Do not use cast, projectile, hit buff, or spell mod GUIDs for these fields.

### Stats

Stats are added through `Stats[]`.

```json
{
  "StatType": "AttackSpeed",
  "ModificationType": "AddToBase",
  "AttributeCapType": "SoftCapped",
  "Value": 0.10
}
```

Useful commands:

```text
.csw stattypes speed
.csw stattypes power
.csw modtypes
```

`AttackSpeed` is accepted as an alias for `PrimaryAttackSpeed`.

### School on-hit effects

Use `OnHitEffects[]` to apply Bloodcraft-style school debuffs on valid hits.

```json
"OnHitEffects": [
  {
    "Enabled": true,
    "Chance": 0.15,
    "TargetBuff": -1576512627,
    "AffectPlayers": false,
    "AffectNonPlayers": true
  }
]
```

Known school effect buff GUIDs:

```text
Blood     Leech    -1246704569
Storm     Static   -1576512627
Frost     Chill    27300215
Chaos     Ignite   348724578
Illusion  Weaken   1723455773
Unholy    Condemn  -325758519
```

### Weapon type buffs

`WeaponTypeBuffs` add stat rules by weapon type.

```json
"WeaponTypeBuffs": [
  {
    "Enabled": true,
    "WeaponType": "Mace",
    "ApplyToCustomWeapons": true,
    "Stats": [
      {
        "StatType": "AttackSpeed",
        "ModificationType": "AddToBase",
        "AttributeCapType": "SoftCapped",
        "Value": 0.10
      }
    ]
  }
]
```

Use these commands to inspect and apply weapon type rules:

```text
.csw typebuffs
.csw typebuffscope
.csw applytypebuffs
```

## Using Commands

All admin commands use one prefix:

```text
.csw
```

### Common setup flow

```text
.csw reload
.csw validate
.csw list
.csw wepgive "Crimson Thorns"
.csw identify
```

### Create and test a custom weapon

```text
.csw reload
.csw validate
.csw wepgive "Crimson Thorns"
.csw identify
```

`.csw wepgive` gives the sender a named custom weapon and binds that exact item instance.

### Upgrade a Shadow Matter weapon

```text
.csw options
.csw upgrade "Crimson Thorns"
.csw identify
```

Hold a matching Shadow Matter weapon first. `.csw options` shows compatible upgrade choices for the held weapon type.

### Edit stats or weapon type buffs

```text
.csw reload
.csw typebuffs
.csw typebuffscope
.csw applytypebuffs
```

Use `.csw typebuffscope` while holding the weapon you want to test. It shows the resolved weapon type and whether a matching rule exists.

### Player spell overrides

Spell overrides are optional and are saved per player and weapon scope.

```text
.csw spell "Player Name" q <abilityGroupGuid> 8
.csw spellscope "Player Name"
.csw spellshow "Player Name"
.csw spellclear "Player Name" q
.csw spellapply "Player Name"
```

Custom weapons have priority over player spell overrides while equipped.

See [`COMMANDS.md`](COMMANDS.md) for the full command reference.

## Compatibility Notes

- Server-side only.
- Designed for dedicated server use.
- Does not require Bloodcraft.
- School on-hit effects follow the same style of school debuffs used by Bloodcraft classes, but this mod keeps its own config and runtime logic.
- Custom weapon abilities are intentionally disabled during shapeshift and boss-feed/V Blood extraction states to avoid temporary ability-bar conflicts.

## Development

Repository layout follows the flat Bloodcraft-style structure:

```text
Commands/     VampireCommandFramework commands
Configs/      JSON config loading and migration
Models/       Runtime definitions and registries
Patches/      Harmony patches
Services/     Runtime custom weapon systems
Utilities/    ECS helpers and safety utilities
Resources/    Example config files
```

Build:

```bash
dotnet restore HexesAndSanguineSteel.csproj
dotnet build HexesAndSanguineSteel.csproj -c Release
```

The output DLL is written to:

```text
bin/Release/net6.0/
```
