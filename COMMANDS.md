# Hexes and Sanguine Steel Commands

All commands use the `.csw` prefix.

## Custom Weapons

### `.csw list`

Lists enabled custom weapon names from `weapons.json`.

### `.csw wepgive <weaponName>`

Gives the command sender a named custom weapon and binds that exact item instance.

```text
.csw wepgive "Crimson Thorns"
```

### `.csw upgrade <weaponName>`

Converts the held matching Shadow Matter weapon into the selected custom weapon by binding the held item instance.

### `.csw options`

Lists custom weapon upgrade options that match the held Shadow Matter weapon type.

### `.csw identify`

Identifies the currently equipped custom weapon.

### `.csw instances [filter]`

Lists saved item-instance bindings from `weapon-instances.json`.

### `.csw prune`

Removes saved instance bindings whose weapon name no longer exists in `weapons.json`.

## Spell Overrides

Spell overrides are saved per player and per current weapon scope.

### `.csw spell <playerName|steamId> <q|e|dash|r|c|t> <abilityGroupGuid> <cooldownSeconds>`

Sets a weapon-scoped ability override.

```text
.csw spell "Player Name" q -955554663 8
```

Use `*_AbilityGroup` GUIDs, not cast/projectile/buff GUIDs.

### `.csw spellclear <playerName|steamId> <q|e|dash|r|c|t|all>`

Clears one saved slot for the player's current weapon scope, or clears all saved profiles.

### `.csw spellshow <playerName|steamId>`

Shows saved weapon-scoped spell profiles.

### `.csw spellscope <playerName|steamId>`

Shows the player's current weapon scope.

### `.csw spellapply <playerName|steamId>`

Reapplies saved spell overrides to one online player.

## Runtime Refresh

### `.csw reload`

Reloads config and refreshes online players.

### `.csw applyweapons`

Reapplies custom weapon ability and stat carriers to online players.

### `.csw applyspells`

Reapplies saved spell overrides to online players, then reapplies custom weapon priority.

### `.csw applytypebuffs`

Force-refreshes runtime stat carriers after weapon type buff edits.

## Validation and Debug

### `.csw validate`

Runs config and carrier validation.

### `.csw typebuffs`

Lists loaded weapon type buff rules and valid stat counts.

### `.csw typebuffscope`

Shows the held weapon's resolved type and matching type-buff state.

### `.csw stattypes [filter]`

Lists valid `UnitStatType` names.

```text
.csw stattypes speed
.csw stattypes power
```

### `.csw modtypes`

Lists valid `ModificationType` and `AttributeCapType` names.

## Removed

The experimental charge command was removed for the public build:

```text
.csw charges
```
