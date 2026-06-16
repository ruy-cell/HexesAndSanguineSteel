using ProjectM;
using Stunlock.Core;
using Unity.Entities;
using VampireCommandFramework;

namespace HexesAndSanguineSteel.Commands;

[CommandGroup(name: "csw")]
internal static class CustomSpawnWeaponCommands
{
    [Command(name: "wepgive", adminOnly: true, usage: ".csw wepgive <weaponName>", description: "Gives the command sender one named custom weapon and binds that exact item instance to the weapon config.")]
    public static void WeaponGiveSelf(ChatCommandContext ctx, string weaponName)
    {
        Entity senderCharacter = ctx.Event.SenderCharacterEntity;
        if (!PlayerLookup.TryFindOnlinePlayer(senderCharacter, out var receiver))
        {
            ctx.Reply("<color=red>Could not resolve the command sender as an online player.</color>");
            return;
        }

        if (TryGiveInstanceWeapon(receiver, weaponName, out string message))
            ctx.Reply($"<color=green>{message}</color>");
        else
            ctx.Reply($"<color=red>{message}</color>");
    }






    [Command(name: "upgrade", adminOnly: true, usage: ".csw upgrade <weaponName>", description: "Upgrades the command sender's held Shadow Matter weapon into a named custom weapon when the weapon type matches.")]
    public static void UpgradeHeldShadowMatter(ChatCommandContext ctx, string weaponName)
    {
        Entity senderCharacter = ctx.Event.SenderCharacterEntity;
        if (!PlayerLookup.TryFindOnlinePlayer(senderCharacter, out var holder))
        {
            ctx.Reply("<color=red>Could not resolve the command sender as an online player.</color>");
            return;
        }

        if (TryUpgradeHeldShadowMatter(holder, weaponName, out string message))
            ctx.Reply($"<color=green>{message}</color>");
        else
            ctx.Reply($"<color=red>{message}</color>");
    }


    [Command(name: "options", adminOnly: true, usage: ".csw options", description: "Lists custom weapon upgrade options that match the command sender's held Shadow Matter weapon type.")]
    public static void ListHeldShadowMatterOptions(ChatCommandContext ctx)
    {
        Entity senderCharacter = ctx.Event.SenderCharacterEntity;
        if (!PlayerLookup.TryFindOnlinePlayer(senderCharacter, out var holder))
        {
            ctx.Reply("<color=red>Could not resolve the command sender as an online player.</color>");
            return;
        }

        ctx.Reply(DescribeHeldShadowMatterUpgradeOptions(holder));
    }



    [Command(name: "list", adminOnly: true, usage: ".csw list", description: "Lists enabled custom weapon names from weapons.json.")]
    public static void List(ChatCommandContext ctx)
    {
        if (CustomWeaponRegistry.CurrentWeapons.Count == 0)
        {
            ctx.Reply("No enabled custom weapons are currently loaded. Try .csw reload.");
            return;
        }

        var names = CustomWeaponRegistry.CurrentWeapons.Select(w => w.Name);
        ctx.Reply("Loaded custom weapons: " + string.Join(", ", names));
    }




    [Command(name: "identify", adminOnly: true, usage: ".csw identify", description: "Identifies the command sender's currently equipped instance-bound or prefab custom weapon.")]
    public static void Identify(ChatCommandContext ctx)
    {
        Entity senderCharacter = ctx.Event.SenderCharacterEntity;
        if (!senderCharacter.ExistsSafe())
        {
            ctx.Reply("<color=red>Could not resolve sender character.</color>");
            return;
        }

        if (CustomWeaponInstanceStore.TryDescribeHeld(senderCharacter, out string instanceMessage))
        {
            ctx.Reply(instanceMessage);
            return;
        }

        if (CustomWeaponRegistry.TryGetCustomWeaponForCharacter(senderCharacter, out var weapon, out string source))
        {
            ctx.Reply($"Held custom weapon: '{weapon.Name}' source:{source} item:{weapon.ItemWeapon.GuidHash}");
            return;
        }

        ctx.Reply(instanceMessage);
    }

    [Command(name: "instances", adminOnly: true, usage: ".csw instances [filter]", description: "Lists saved item-instance custom weapon bindings.")]
    public static void Instances(ChatCommandContext ctx, string filter = "")
    {
        ctx.Reply(CustomWeaponInstanceStore.DescribeInstances(filter));
    }

    [Command(name: "prune", adminOnly: true, usage: ".csw prune", description: "Removes saved instance bindings whose WeaponName no longer exists in weapons.json.")]
    public static void Prune(ChatCommandContext ctx)
    {
        int removed = CustomWeaponInstanceStore.PruneInvalidWeaponNames();
        ctx.Reply($"Pruned {removed} stale instance binding(s). This only removes bindings for weapon names no longer present in weapons.json.");
    }

    [Command(name: "validate", adminOnly: true, usage: ".csw validate", description: "Runs carrier conflict and buffer validation.")]
    public static void Validate(ChatCommandContext ctx)
    {
        ctx.Reply(CustomWeaponValidationService.ValidateCarriers(log: false));
    }

    [Command(name: "spell", adminOnly: true, usage: ".csw spell <playerName|steamId> <q|e|dash|r|c|t> <abilityGroupGuid> <cooldownSeconds>", description: "Sets a weapon-scoped Q/E/Dash/R/C/T ability override with cooldown.")]
    public static void SpellSetCd(ChatCommandContext ctx, string playerNameOrSteamId, string slotText, int abilityGroupGuid, float cooldownSeconds)
    {
        if (!PlayerSpellOverrideService.TryParseSlot(slotText, out var slot))
        {
            ctx.Reply("Invalid slot. Use q, e, dash, r, c, or t.");
            return;
        }

        if (!PlayerLookup.TryFindOnlinePlayer(playerNameOrSteamId, out var player))
        {
            ctx.Reply($"Could not find online player '{playerNameOrSteamId}'. Use quotes for names with spaces.");
            return;
        }

        if (PlayerSpellOverrideService.TrySetSlotAndCooldown(player, slot, abilityGroupGuid, cooldownSeconds, carrierBuffGuid: 0, out string message))
            ctx.Reply($"<color=green>{message}</color>");
        else
            ctx.Reply($"<color=red>{message}</color>");
    }

    [Command(name: "spellclear", adminOnly: true, usage: ".csw spellclear <playerName|steamId> <q|e|dash|r|c|t|all>", description: "Clears a weapon-scoped spell override.")]
    public static void SpellClear(ChatCommandContext ctx, string playerNameOrSteamId, string slotText = "all")
    {
        if (!PlayerLookup.TryFindOnlinePlayer(playerNameOrSteamId, out var player))
        {
            ctx.Reply($"Could not find online player '{playerNameOrSteamId}'.");
            return;
        }

        if (PlayerSpellOverrideService.TryClearSlot(player, slotText, out string message))
            ctx.Reply($"<color=green>{message}</color>");
        else
            ctx.Reply($"<color=red>{message}</color>");
    }

    [Command(name: "spellshow", adminOnly: true, usage: ".csw spellshow <playerName|steamId>", description: "Shows a player's saved weapon-scoped spell overrides.")]
    public static void SpellShow(ChatCommandContext ctx, string playerNameOrSteamId)
    {
        if (!PlayerLookup.TryFindOnlinePlayer(playerNameOrSteamId, out var player))
        {
            ctx.Reply($"Could not find online player '{playerNameOrSteamId}'.");
            return;
        }

        ctx.Reply(PlayerSpellOverrideService.Describe(player));
    }

    [Command(name: "spellscope", adminOnly: true, usage: ".csw spellscope <playerName|steamId>", description: "Shows the player's current weapon scope.")]
    public static void SpellScope(ChatCommandContext ctx, string playerNameOrSteamId)
    {
        if (!PlayerLookup.TryFindOnlinePlayer(playerNameOrSteamId, out var player))
        {
            ctx.Reply($"Could not find online player '{playerNameOrSteamId}'.");
            return;
        }

        ctx.Reply(PlayerSpellOverrideService.DescribeCurrentScope(player));
    }

    [Command(name: "spellapply", adminOnly: true, usage: ".csw spellapply <playerName|steamId>", description: "Reapplies saved spell overrides to one online player.")]
    public static void SpellApply(ChatCommandContext ctx, string playerNameOrSteamId)
    {
        if (!PlayerLookup.TryFindOnlinePlayer(playerNameOrSteamId, out var player))
        {
            ctx.Reply($"Could not find online player '{playerNameOrSteamId}'.");
            return;
        }

        if (PlayerSpellOverrideService.TryApplyForPlayer(player, out string message))
            ctx.Reply($"<color=green>{message}</color>");
        else
            ctx.Reply($"<color=red>{message}</color>");
    }

    [Command(name: "reload", adminOnly: true, usage: ".csw reload", description: "Reloads weapons.json and reapplies runtime custom weapon systems.")]
    public static void ReloadAll(ChatCommandContext ctx)
    {
        if (!AdminCommandRateLimiter.TryRun(".csw reload", out string rateMessage))
        {
            ctx.Reply(rateMessage);
            return;
        }

        RuntimePrefabCache.Clear();
        CustomWeaponRegistry.ApplyAll();
        int weapons = CustomWeaponRuntimeOverrideService.ApplyAllOnline();
        int cooldowns = CustomWeaponRuntimeCooldownService.ApplyAllOnline();
        ctx.Reply($"Reloaded custom weapon config. Runtime custom weapons applied:{weapons}, cooldown updates:{cooldowns}. A full restart is still safer for production changes.");
    }

    [Command(name: "applyweapons", adminOnly: true, usage: ".csw applyweapons", description: "Reapplies runtime custom weapon carriers to online players currently holding configured custom weapons.")]
    public static void ApplyWeapons(ChatCommandContext ctx)
    {
        if (!AdminCommandRateLimiter.TryRun(".csw applyweapons", out string rateMessage))
        {
            ctx.Reply(rateMessage);
            return;
        }

        int weapons = CustomWeaponRuntimeOverrideService.ApplyAllOnline();
        int cooldowns = CustomWeaponRuntimeCooldownService.ApplyAllOnline();
        ctx.Reply($"Runtime custom weapons applied:{weapons}, cooldown updates:{cooldowns}.");
    }

    [Command(name: "applyspells", adminOnly: true, usage: ".csw applyspells", description: "Reapplies all online player spell overrides, then reapplies custom weapon priority.")]
    public static void ApplySpells(ChatCommandContext ctx)
    {
        if (!AdminCommandRateLimiter.TryRun(".csw applyspells", out string rateMessage))
        {
            ctx.Reply(rateMessage);
            return;
        }

        int count = PlayerSpellOverrideService.ApplyAllOnline();
        int weapons = CustomWeaponRuntimeOverrideService.ApplyAllOnline();
        ctx.Reply($"Applied spell overrides to {count} online player(s). Runtime custom weapons applied:{weapons}.");
    }

    [Command(name: "typebuffs", adminOnly: true, usage: ".csw typebuffs", description: "Lists loaded WeaponTypeBuffs from weapons.json.")]
    public static void TypeBuffs(ChatCommandContext ctx)
    {
        ctx.Reply(CustomWeaponTypeBuffService.Describe());
    }

    [Command(name: "typebuffscope", adminOnly: true, usage: ".csw typebuffscope", description: "Shows the command sender's current weapon type and whether it matches WeaponTypeBuffs.")]
    public static void TypeBuffScope(ChatCommandContext ctx)
    {
        ctx.Reply(CustomWeaponTypeBuffService.DescribeCurrentScope(ctx.Event.SenderCharacterEntity));
    }

    [Command(name: "applytypebuffs", adminOnly: true, usage: ".csw applytypebuffs", description: "Reapplies runtime custom weapon/stat carriers so WeaponTypeBuffs update for online players.")]
    public static void ApplyTypeBuffs(ChatCommandContext ctx)
    {
        if (!AdminCommandRateLimiter.TryRun(".csw applytypebuffs", out string rateMessage))
        {
            ctx.Reply(rateMessage);
            return;
        }

        int applied = CustomWeaponTypeBuffService.ApplyAllOnline(forceReconcile: true);
        ctx.Reply($"Runtime stat carriers force-refreshed for {applied} online player(s).");
    }

    [Command(name: "stattypes", adminOnly: true, usage: ".csw stattypes [filter]", description: "Lists UnitStatType names accepted by weapons.json Stats[].")]
    public static void StatTypes(ChatCommandContext ctx, string filter = "")
    {
        IEnumerable<string> names = Enum.GetNames(typeof(UnitStatType));

        if (!string.IsNullOrWhiteSpace(filter))
            names = names.Where(n => n.Contains(filter, StringComparison.OrdinalIgnoreCase));

        string text = string.Join(", ", names.Take(80));
        int total = names.Count();

        if (string.IsNullOrWhiteSpace(text))
            ctx.Reply("No UnitStatType names matched that filter. Useful aliases: AttackSpeed/AS -> PrimaryAttackSpeed, AbilitySpeed/AAS -> AbilityAttackSpeed.");
        else
            ctx.Reply($"UnitStatTypes ({total} shown/max 80): {text}\nAliases: AttackSpeed/AS -> PrimaryAttackSpeed, AbilitySpeed/AAS -> AbilityAttackSpeed.");
    }

    [Command(name: "modtypes", adminOnly: true, usage: ".csw modtypes", description: "Lists ModificationType and AttributeCapType names accepted by weapons.json Stats[].")]
    public static void ModTypes(ChatCommandContext ctx)
    {
        ctx.Reply($"ModificationType: {string.Join(", ", Enum.GetNames(typeof(ModificationType)))}\nAttributeCapType: {string.Join(", ", Enum.GetNames(typeof(AttributeCapType)))}");
    }



    static string DescribeHeldShadowMatterUpgradeOptions(PlayerLookupResult holder)
    {
        if (!holder.CharacterEntity.ExistsSafe())
            return $"<color=red>Player '{holder.Name}' does not have a valid character entity.</color>";

        if (!ItemInstanceUtility.TryGetHeldWeaponEntity(holder.CharacterEntity, out Entity heldItem))
            return $"<color=red>{holder.Name} is not holding a weapon.</color>";

        if (!heldItem.TryRead<PrefabGUID>(out var heldPrefab) || !heldPrefab.HasValue())
            return "<color=red>Held weapon does not have a valid PrefabGUID.</color>";

        int heldItemHash = heldPrefab.GuidHash;

        if (!WeaponScopeResolver.IsShadowMatterWeaponPrefab(heldItemHash))
            return $"<color=red>Held weapon item:{heldItemHash} is not a known Shadow Matter weapon. Only Shadow Matter weapons can be upgraded with .csw upgrade.</color>";

        if (!WeaponScopeResolver.TryGetWeaponTypeForPrefab(heldItemHash, out string heldType))
            return $"<color=red>Could not determine weapon type for held Shadow Matter item:{heldItemHash}.</color>";

        var matches = CustomWeaponRegistry.CurrentWeapons
            .Where(w => WeaponScopeResolver.TryGetWeaponTypeForPrefab(w.ItemWeapon.GuidHash, out string targetType)
                && string.Equals(heldType, targetType, StringComparison.OrdinalIgnoreCase))
            .Select(w => w.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (matches.Length == 0)
            return $"Held Shadow Matter weapon type: <color=yellow>{heldType}</color>.\nNo loaded custom weapons match this weapon type. Check weapons.json and run .csw reload.";

        string names = string.Join("\n", matches.Select(n => $"  - {n}"));
        return $"Held Shadow Matter weapon type: <color=yellow>{heldType}</color>.\nMatching custom weapon upgrade names:\n{names}\nUse: <color=white>.csw upgrade \"weapon name\"</color>";
    }


    static bool TryUpgradeHeldShadowMatter(PlayerLookupResult holder, string weaponName, out string message)
    {
        message = string.Empty;

        if (string.IsNullOrWhiteSpace(weaponName))
        {
            message = "Weapon name cannot be empty.";
            return false;
        }

        if (!CustomWeaponRegistry.TryGetCustomWeaponByName(weaponName, out var targetWeapon))
        {
            message = $"No enabled custom weapon named '{weaponName}' was found. Use .csw list to see loaded names.";
            return false;
        }

        if (!holder.CharacterEntity.ExistsSafe())
        {
            message = $"Player '{holder.Name}' does not have a valid character entity.";
            return false;
        }

        if (!ItemInstanceUtility.TryGetHeldWeaponEntity(holder.CharacterEntity, out Entity heldItem))
        {
            message = $"{holder.Name} is not holding a weapon.";
            return false;
        }

        if (!heldItem.TryRead<PrefabGUID>(out var heldPrefab) || !heldPrefab.HasValue())
        {
            message = "Held weapon does not have a valid PrefabGUID.";
            return false;
        }

        int heldItemHash = heldPrefab.GuidHash;

        if (!WeaponScopeResolver.IsShadowMatterWeaponPrefab(heldItemHash))
        {
            message = $"Held weapon item:{heldItemHash} is not a known Shadow Matter weapon. Only Shadow Matter weapons can be upgraded this way.";
            return false;
        }

        if (!WeaponScopeResolver.TryGetWeaponTypeForPrefab(heldItemHash, out string heldType))
        {
            message = $"Could not determine weapon type for held Shadow Matter item:{heldItemHash}.";
            return false;
        }

        if (!WeaponScopeResolver.TryGetWeaponTypeForPrefab(targetWeapon.ItemWeapon.GuidHash, out string targetType))
        {
            message = $"Could not determine weapon type for custom weapon '{targetWeapon.Name}' item:{targetWeapon.ItemWeapon.GuidHash}.";
            return false;
        }

        if (!string.Equals(heldType, targetType, StringComparison.OrdinalIgnoreCase))
        {
            message = $"Type mismatch. Held Shadow Matter weapon is '{heldType}', but custom weapon '{targetWeapon.Name}' is '{targetType}'.";
            return false;
        }

        int sequenceGuidHash = ItemInstanceUtility.GetOrCreateSequenceGuid(heldItem);
        if (sequenceGuidHash == 0)
        {
            message = "Held weapon matched, but the mod could not assign/read a SequenceGUID before consuming upgrade cost.";
            return false;
        }

        var upgradeCost = BuildEffectiveUpgradeCost(targetWeapon);
        string costMessage = string.Empty;
        if (!InventoryCostUtility.TryConsumeUpgradeCost(holder, upgradeCost, out costMessage))
        {
            message = $"Held weapon matched type '{heldType}', but upgrade cost failed: {costMessage}";
            return false;
        }

        if (!CustomWeaponInstanceStore.TryBindHeldItem(heldItem, targetWeapon, holder, out string bindMessage))
        {
            message = $"Held weapon matched type '{heldType}' and cost was consumed, but instance binding failed: {bindMessage}";
            return false;
        }

        RuntimeStateCache.Clear(holder.CharacterEntity);
        CustomWeaponRuntimeOverrideService.TryApplyForPlayer(holder, out string applyMessage);
        CustomWeaponRuntimeOverrideService.QueueRuntimeRefresh($"Shadow Matter upgrade {targetWeapon.Name}");

        string costSuffix = string.IsNullOrWhiteSpace(costMessage) ? string.Empty : $" {costMessage}";
        message = $"Upgraded held Shadow Matter {heldType} into '{targetWeapon.Name}'.{costSuffix} {bindMessage} {applyMessage}";
        return true;
    }

    static IReadOnlyList<CustomWeaponCostDef> BuildEffectiveUpgradeCost(CustomWeaponDef weapon)
    {
        // Shadow Matter upgrades now use only the global cost from weapons.json.
        // Per-weapon UpgradeCost[] was removed in schema v14 to keep upgrade pricing simple.
        return CustomWeaponConfig.GlobalUpgradeCost;
    }


    static bool TryGiveInstanceWeapon(PlayerLookupResult receiver, string weaponName, out string message)
    {
        message = string.Empty;

        if (string.IsNullOrWhiteSpace(weaponName))
        {
            message = "Weapon name cannot be empty.";
            return false;
        }

        if (!CustomWeaponRegistry.TryGetCustomWeaponByName(weaponName, out var weapon))
        {
            message = $"No enabled custom weapon named '{weaponName}' was found. Use .csw list to see loaded names.";
            return false;
        }

        if (!receiver.CharacterEntity.ExistsSafe())
        {
            message = $"Player '{receiver.Name}' does not have a valid character entity.";
            return false;
        }

        if (!InventoryUtilities.TryGetInventoryEntity(Core.EntityManager, receiver.CharacterEntity, out Entity inventoryEntity))
        {
            message = $"Could not resolve inventory entity for '{receiver.Name}'.";
            return false;
        }

        AddItemResponse response = Core.ServerGameManager.TryAddInventoryItem(inventoryEntity, weapon.ItemWeapon, 1);
        if (!response.Success)
        {
            message = $"Failed to give '{weapon.Name}' item:{weapon.ItemWeapon.GuidHash} to {receiver.Name}. Inventory may be full.";
            return false;
        }

        Entity newItem = response.NewEntity;
        if (!newItem.ExistsSafe())
        {
            message = $"The game reported success giving '{weapon.Name}', but no new item entity was returned.";
            return false;
        }

        if (!CustomWeaponInstanceStore.TryBindGeneratedItem(newItem, weapon, receiver, out string bindMessage))
        {
            message = $"Gave '{weapon.Name}' to {receiver.Name}, but failed to bind instance: {bindMessage}";
            return false;
        }

        message = $"Gave '{weapon.Name}' to {receiver.Name}. {bindMessage} This custom weapon follows that physical item if it is dropped/traded.";
        return true;
    }
}
