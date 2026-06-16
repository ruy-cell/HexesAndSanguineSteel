using ProjectM;
using Stunlock.Core;
using Unity.Entities;

namespace HexesAndSanguineSteel;

internal static class PlayerSpellOverrideService
{
    static bool _scopeScanQueued;
    static int _scopeScanDelayTicks;
    static int _cooldownRefreshTicks;
    static readonly Dictionary<ulong, string> _lastScopeByPlayer = new();

    internal static void QueueReapplyAllOnline(string reason)
    {
        // Equipment systems can fire several times in the same swap. Defer and coalesce to one scan.
        _scopeScanQueued = true;
        _scopeScanDelayTicks = Math.Max(_scopeScanDelayTicks, 2);
        _cooldownRefreshTicks = Math.Max(_cooldownRefreshTicks, 12);
        RuntimeOptimization.Debug($"Queued coalesced scope scan. Reason: {reason}");
    }

    internal static void QueueCooldownRefresh(string reason)
    {
        _cooldownRefreshTicks = Math.Max(_cooldownRefreshTicks, 12);
        RuntimeOptimization.Debug($"Queued cooldown refresh. Reason: {reason}");
    }

    internal static void ProcessQueuedAfterReplaceAbilitySystem()
    {
        int scopeChangeCount = 0;

        if (_scopeScanQueued)
        {
            if (_scopeScanDelayTicks > 0)
            {
                _scopeScanDelayTicks--;
            }
            else
            {
                _scopeScanQueued = false;
                scopeChangeCount = ApplyScopeChangesOnline();

                if (scopeChangeCount > 0)
                    _cooldownRefreshTicks = Math.Max(_cooldownRefreshTicks, 12);

                RuntimeOptimization.Debug($"Processed coalesced scope scan after ReplaceAbilityOnSlotSystem. scopeChanges:{scopeChangeCount}.");
            }
        }

        if (_cooldownRefreshTicks > 0)
        {
            _cooldownRefreshTicks--;

            int customWeaponOverrideCount = CustomWeaponRuntimeOverrideService.ApplyAllOnline();
            int weaponCooldownCount = CustomWeaponRuntimeCooldownService.ApplyAllOnline();
            int playerCooldownCount = RefreshAllOnlineCooldowns();

            if (RuntimeOptimization.DebugLogging && (scopeChangeCount > 0 || customWeaponOverrideCount > 0 || weaponCooldownCount > 0 || playerCooldownCount > 0))
                Plugin.LogInstance.LogInfo($"Runtime refresh: scope changes:{scopeChangeCount}, custom weapon overrides applied:{customWeaponOverrideCount}, custom weapon cooldown updates:{weaponCooldownCount}, player spell cooldown updates:{playerCooldownCount}.");
        }
    }

    static int ApplyScopeChangesOnline()
    {
        int processed = 0;

        foreach (var player in PlayerLookup.GetOnlinePlayers())
        {
            var scope = WeaponScopeResolver.GetCurrentScope(player.CharacterEntity);
            string newKey = scope.ScopeKey;

            _lastScopeByPlayer.TryGetValue(player.PlatformId, out string? oldKey);
            _lastScopeByPlayer[player.PlatformId] = newKey;

            // Important:
            // Equipment swaps can leave the carrier buff entity present while the game's own
            // replacement pass has removed/overwritten the slot replacements. If we only trust
            // the last-applied hash, TryApply can incorrectly skip rebuilding the carrier.
            //
            // A queued equipment refresh is therefore a *force reapply* boundary: clear the cached
            // spell hash so TryApply removes/reapplies the carrier and rewrites the live
            // ReplaceAbilityOnSlotBuff buffer for the current scope.
            RuntimeStateCache.ClearSpellHash(player.CharacterEntity);
            CustomWeaponRuntimeOverrideService.ForceStatReconcile(player.CharacterEntity, "queued equipment/scope refresh");

            bool applied = TryApplyForPlayer(player, out string message);
            processed++;

            if (!string.Equals(oldKey, newKey, StringComparison.Ordinal))
                RuntimeOptimization.Debug($"Scope changed for {player.Name}: {oldKey ?? "<none>"} -> {newKey}. {message}");
            else
                RuntimeOptimization.Debug($"Scope refresh for {player.Name}: {newKey}. {message}");
        }

        return processed;
    }

    static void RecordCurrentScopes()
    {
        foreach (var player in PlayerLookup.GetOnlinePlayers())
        {
            var scope = WeaponScopeResolver.GetCurrentScope(player.CharacterEntity);
            _lastScopeByPlayer[player.PlatformId] = scope.ScopeKey;
        }
    }

    internal static int RefreshAllOnlineCooldowns()
    {
        int updated = 0;

        foreach (var player in PlayerLookup.GetOnlinePlayers())
            updated += RefreshCooldownsForPlayer(player);

        return updated;
    }

    static int RefreshCooldownsForPlayer(PlayerLookupResult player)
    {
        if (!TryGetActiveScopeProfile(player, out _, out var profile, out _, removeCarrierOnNoProfile: false))
            return 0;

        return ApplyCooldownOverridesCount(player.CharacterEntity, profile);
    }

    internal static void Initialize()
    {
        PlayerSpellOverrideConfig.LoadOrCreate();
    }

    internal static bool TrySetSlot(PlayerLookupResult player, SpellSlotKind slot, int abilityGroupGuid, int carrierBuffGuid, out string message)
    {
        return TrySetSlotInternal(player, slot, abilityGroupGuid, null, carrierBuffGuid, out message);
    }

    internal static bool TrySetSlotAndCooldown(PlayerLookupResult player, SpellSlotKind slot, int abilityGroupGuid, float cooldownSeconds, int carrierBuffGuid, out string message)
    {
        if (cooldownSeconds < 0f)
        {
            message = "Cooldown cannot be negative.";
            return false;
        }

        return TrySetSlotInternal(player, slot, abilityGroupGuid, cooldownSeconds, carrierBuffGuid, out message);
    }

    internal static bool TrySetCooldown(PlayerLookupResult player, SpellSlotKind slot, float cooldownSeconds, out string message)
    {
        if (cooldownSeconds < 0f)
        {
            message = "Cooldown cannot be negative.";
            return false;
        }

        var scope = WeaponScopeResolver.GetCurrentScope(player.CharacterEntity);
        if (scope.IsCustomWeapon)
        {
            message = $"Cannot set custom spell cooldown while a custom weapon is equipped ({scope.Detail}). Custom weapons suppress player spell overrides.";
            return false;
        }

        var config = PlayerSpellOverrideConfig.Current;
        string key = player.PlatformId.ToString();

        if (!config.Players.TryGetValue(key, out var entry) || !entry.WeaponScopes.TryGetValue(scope.ScopeKey, out var profile))
        {
            message = $"No spell override exists for {player.Name} on current scope '{scope.ScopeKey}'. Set an ability first while wielding that weapon type.";
            return false;
        }

        switch (slot)
        {
            case SpellSlotKind.Q:
                if (profile.Q == 0) { message = $"No Q spell exists on scope '{scope.ScopeKey}'."; return false; }
                profile.QCooldown = cooldownSeconds;
                break;
            case SpellSlotKind.E:
                if (profile.E == 0) { message = $"No E spell exists on scope '{scope.ScopeKey}'."; return false; }
                profile.ECooldown = cooldownSeconds;
                break;
            case SpellSlotKind.Dash:
                if (profile.Dash == 0) { message = $"No Dash spell exists on scope '{scope.ScopeKey}'."; return false; }
                profile.DashCooldown = cooldownSeconds;
                break;
            case SpellSlotKind.R:
                if (profile.R == 0) { message = $"No R spell exists on scope '{scope.ScopeKey}'."; return false; }
                profile.RCooldown = cooldownSeconds;
                break;
            case SpellSlotKind.C:
                if (profile.C == 0) { message = $"No C spell exists on scope '{scope.ScopeKey}'."; return false; }
                profile.CCooldown = cooldownSeconds;
                break;
            case SpellSlotKind.T:
                if (profile.T == 0) { message = $"No T spell exists on scope '{scope.ScopeKey}'."; return false; }
                profile.TCooldown = cooldownSeconds;
                break;
            default:
                message = $"Unsupported slot {slot}.";
                return false;
        }

        PlayerSpellOverrideConfig.Save();

        if (!TryApply(player.CharacterEntity, entry, profile, scope, out string applyMessage))
        {
            message = applyMessage;
            return false;
        }

        message = $"{applyMessage} Saved {slot} cooldown override on scope '{scope.ScopeKey}': {cooldownSeconds:0.##}s.";
        return true;
    }

    static bool TrySetSlotInternal(PlayerLookupResult player, SpellSlotKind slot, int abilityGroupGuid, float? cooldownSeconds, int carrierBuffGuid, out string message)
    {
        if (abilityGroupGuid == 0)
        {
            message = "Ability group GUID cannot be 0.";
            return false;
        }

        var scope = WeaponScopeResolver.GetCurrentScope(player.CharacterEntity);
        if (scope.IsCustomWeapon)
        {
            message = $"Cannot bind player custom spells while a custom weapon is equipped ({scope.Detail}). Unequip it or switch to a normal weapon/unarmed first.";
            return false;
        }

        var config = PlayerSpellOverrideConfig.Current;
        string key = player.PlatformId.ToString();

        if (!config.Players.TryGetValue(key, out var entry))
        {
            entry = new PlayerSpellOverrideEntry
            {
                Enabled = true,
                PlatformId = player.PlatformId,
                LastKnownName = player.Name
            };

            config.Players[key] = entry;
        }

        entry.Enabled = true;
        entry.PlatformId = player.PlatformId;
        entry.LastKnownName = player.Name;
        entry.WeaponScopes ??= [];

        if (carrierBuffGuid != 0)
            entry.CarrierBuff = carrierBuffGuid;

        if (!entry.WeaponScopes.TryGetValue(scope.ScopeKey, out var profile))
        {
            profile = new PlayerSpellScopeEntry();
            entry.WeaponScopes[scope.ScopeKey] = profile;
        }

        switch (slot)
        {
            case SpellSlotKind.Q:
                profile.Q = abilityGroupGuid;
                if (cooldownSeconds.HasValue)
                    profile.QCooldown = cooldownSeconds.Value;
                break;
            case SpellSlotKind.E:
                profile.E = abilityGroupGuid;
                if (cooldownSeconds.HasValue)
                    profile.ECooldown = cooldownSeconds.Value;
                break;
            case SpellSlotKind.Dash:
                profile.Dash = abilityGroupGuid;
                if (cooldownSeconds.HasValue)
                    profile.DashCooldown = cooldownSeconds.Value;
                break;
            case SpellSlotKind.R:
                profile.R = abilityGroupGuid;
                if (cooldownSeconds.HasValue)
                    profile.RCooldown = cooldownSeconds.Value;
                break;
            case SpellSlotKind.C:
                profile.C = abilityGroupGuid;
                if (cooldownSeconds.HasValue)
                    profile.CCooldown = cooldownSeconds.Value;
                break;
            case SpellSlotKind.T:
                profile.T = abilityGroupGuid;
                if (cooldownSeconds.HasValue)
                    profile.TCooldown = cooldownSeconds.Value;
                break;
            default:
                message = $"Unsupported slot {slot}.";
                return false;
        }

        PlayerSpellOverrideConfig.Save();

        if (!TryApply(player.CharacterEntity, entry, profile, scope, out message))
            return false;

        message += $" Bound to current weapon scope '{scope.ScopeKey}' ({scope.Detail}).";
        return true;
    }

    internal static bool TryClearSlot(PlayerLookupResult player, string slotText, out string message)
    {
        var config = PlayerSpellOverrideConfig.Current;
        string key = player.PlatformId.ToString();

        if (!config.Players.TryGetValue(key, out var entry))
        {
            message = $"No spell override exists for {player.Name}.";
            return false;
        }

        if (string.Equals(slotText, "all", StringComparison.OrdinalIgnoreCase))
        {
            config.Players.Remove(key);
            PlayerSpellOverrideConfig.Save();
            RemoveCarrierBuff(player.CharacterEntity, entry);
            message = $"Cleared all weapon-scoped spell overrides for {player.Name}.";
            return true;
        }

        if (!TryParseSlot(slotText, out var slot))
        {
            message = "Invalid slot. Use q, e, dash, r, c, t, or all.";
            return false;
        }

        var scope = WeaponScopeResolver.GetCurrentScope(player.CharacterEntity);
        if (scope.IsCustomWeapon)
        {
            message = $"Cannot clear a scoped player spell while custom weapon is equipped ({scope.Detail}). Use '.csw spellclear <player> all' or switch to the target weapon type first.";
            return false;
        }

        if (!entry.WeaponScopes.TryGetValue(scope.ScopeKey, out var profile))
        {
            message = $"No spell profile exists for {player.Name} on current scope '{scope.ScopeKey}'.";
            return false;
        }

        switch (slot)
        {
            case SpellSlotKind.Q:
                profile.Q = 0;
                profile.QCooldown = 0f;
                break;
            case SpellSlotKind.E:
                profile.E = 0;
                profile.ECooldown = 0f;
                break;
            case SpellSlotKind.Dash:
                profile.Dash = 0;
                profile.DashCooldown = 0f;
                break;
            case SpellSlotKind.R:
                profile.R = 0;
                profile.RCooldown = 0f;
                break;
            case SpellSlotKind.C:
                profile.C = 0;
                profile.CCooldown = 0f;
                break;
            case SpellSlotKind.T:
                profile.T = 0;
                profile.TCooldown = 0f;
                break;
        }

        if (!profile.HasAnySpell())
            entry.WeaponScopes.Remove(scope.ScopeKey);

        if (entry.WeaponScopes.Count == 0)
            config.Players.Remove(key);

        PlayerSpellOverrideConfig.Save();
        return TryApplyForPlayer(player, out message);
    }

    internal static bool TryApplyForPlayer(PlayerLookupResult player, out string message)
    {
        if (!TryGetActiveScopeProfile(player, out var entry, out var profile, out var scope, removeCarrierOnNoProfile: true))
        {
            message = $"{player.Name}: no active scoped override for current weapon.";
            return false;
        }

        return TryApply(player.CharacterEntity, entry, profile, scope, out message);
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

    internal static string Describe(PlayerLookupResult player)
    {
        var config = PlayerSpellOverrideConfig.Current;
        var scope = WeaponScopeResolver.GetCurrentScope(player.CharacterEntity);

        if (!config.Players.TryGetValue(player.PlatformId.ToString(), out var entry))
            return $"{player.Name}: no override. Current scope:{scope.ScopeKey} ({scope.Detail}) customWeapon:{scope.IsCustomWeapon}.";

        int carrier = ResolveCarrier(entry);
        var lines = new List<string>
        {
            $"{player.Name} ({player.PlatformId}) carrier:{carrier} enabled:{entry.Enabled} currentScope:{scope.ScopeKey} detail:{scope.Detail} customWeapon:{scope.IsCustomWeapon}"
        };

        if (entry.WeaponScopes.Count == 0)
        {
            lines.Add("No weapon-scoped profiles saved.");
        }
        else
        {
            foreach (var kvp in entry.WeaponScopes.OrderBy(k => k.Key))
            {
                var p = kvp.Value;
                lines.Add($"{kvp.Key}: Q:{p.Q} cd:{p.QCooldown:0.##}s E:{p.E} cd:{p.ECooldown:0.##}s Dash:{p.Dash} cd:{p.DashCooldown:0.##}s R:{p.R} cd:{p.RCooldown:0.##}s C:{p.C} cd:{p.CCooldown:0.##}s T:{p.T} cd:{p.TCooldown:0.##}s");
            }
        }

        return string.Join("\n", lines);
    }

    internal static string DescribeCurrentScope(PlayerLookupResult player)
    {
        var scope = WeaponScopeResolver.GetCurrentScope(player.CharacterEntity);
        return $"{player.Name} current weapon scope: {scope.ScopeKey} prefab:{scope.WeaponPrefabGuid} unarmed:{scope.IsUnarmed} customWeapon:{scope.IsCustomWeapon} detail:{scope.Detail}";
    }

    internal static bool TryParseSlot(string slotText, out SpellSlotKind slot)
    {
        slot = default;

        if (string.Equals(slotText, "q", StringComparison.OrdinalIgnoreCase))
        {
            slot = SpellSlotKind.Q;
            return true;
        }

        if (string.Equals(slotText, "e", StringComparison.OrdinalIgnoreCase))
        {
            slot = SpellSlotKind.E;
            return true;
        }

        if (string.Equals(slotText, "dash", StringComparison.OrdinalIgnoreCase)
            || string.Equals(slotText, "d", StringComparison.OrdinalIgnoreCase)
            || string.Equals(slotText, "space", StringComparison.OrdinalIgnoreCase))
        {
            slot = SpellSlotKind.Dash;
            return true;
        }

        if (string.Equals(slotText, "r", StringComparison.OrdinalIgnoreCase))
        {
            slot = SpellSlotKind.R;
            return true;
        }

        if (string.Equals(slotText, "c", StringComparison.OrdinalIgnoreCase))
        {
            slot = SpellSlotKind.C;
            return true;
        }

        if (string.Equals(slotText, "t", StringComparison.OrdinalIgnoreCase)
            || string.Equals(slotText, "ult", StringComparison.OrdinalIgnoreCase)
            || string.Equals(slotText, "ultimate", StringComparison.OrdinalIgnoreCase))
        {
            slot = SpellSlotKind.T;
            return true;
        }

        return false;
    }

    internal static bool TryApplyByPlatformId(ulong platformId, Entity character, out string message)
    {
        var config = PlayerSpellOverrideConfig.Current;

        if (!config.Players.TryGetValue(platformId.ToString(), out var entry) || !entry.Enabled)
        {
            message = $"No enabled override for platform id {platformId}.";
            return false;
        }

        var scope = WeaponScopeResolver.GetCurrentScope(character);
        if (ShapeshiftUtility.IsActiveForm(character))
        {
            RemoveCarrierBuff(character, entry);
            RuntimeStateCache.ClearSpellHash(character);
            message = $"Shapeshift/form active; player spell override suppressed. form:{ShapeshiftUtility.DescribeActiveForm(character)}";
            return false;
        }

        if (scope.IsCustomWeapon)
        {
            RemoveCarrierBuff(character, entry);

            if (CustomWeaponRuntimeOverrideService.TryApplyForCharacter(character, out string weaponMessage))
            {
                message = $"Custom weapon equipped ({scope.Detail}); player spell override suppressed. {weaponMessage}";
                return true;
            }

            message = $"Custom weapon equipped ({scope.Detail}); player spell override suppressed. {weaponMessage}";
            return false;
        }

        entry.WeaponScopes ??= [];
        if (!entry.WeaponScopes.TryGetValue(scope.ScopeKey, out var profile) || !profile.HasAnySpell())
        {
            RemoveCarrierBuff(character, entry);
            message = $"No player spell profile for scope '{scope.ScopeKey}'.";
            return false;
        }

        return TryApply(character, entry, profile, scope, out message);
    }

    internal static void RemoveRuntimeCarrierForForm(Entity character, ulong platformId)
    {
        RemoveRuntimeCarrierForTemporaryAbilityState(character, platformId);
    }

    internal static void RemoveRuntimeCarrierForTemporaryAbilityState(Entity character, ulong platformId)
    {
        if (!character.ExistsSafe())
            return;

        var config = PlayerSpellOverrideConfig.Current;
        if (config.Players.TryGetValue(platformId.ToString(), out var entry))
            RemoveCarrierBuff(character, entry);

        RuntimeStateCache.ClearSpellHash(character);
    }

    static bool TryGetActiveScopeProfile(PlayerLookupResult player, out PlayerSpellOverrideEntry entry, out PlayerSpellScopeEntry profile, out WeaponScopeResult scope, bool removeCarrierOnNoProfile)
    {
        entry = default!;
        profile = default!;
        scope = WeaponScopeResolver.GetCurrentScope(player.CharacterEntity);

        if (ShapeshiftUtility.IsActiveForm(player.CharacterEntity) || FeedInteractionUtility.IsActiveFeedOrExtraction(player.CharacterEntity))
        {
            var formConfig = PlayerSpellOverrideConfig.Current;
            if (removeCarrierOnNoProfile && formConfig.Players.TryGetValue(player.PlatformId.ToString(), out var formEntry))
                RemoveCarrierBuff(player.CharacterEntity, formEntry);

            return false;
        }

        var config = PlayerSpellOverrideConfig.Current;

        if (!config.Players.TryGetValue(player.PlatformId.ToString(), out entry!) || !entry.Enabled)
            return false;

        entry.WeaponScopes ??= [];

        if (scope.IsCustomWeapon)
        {
            if (removeCarrierOnNoProfile)
                RemoveCarrierBuff(player.CharacterEntity, entry);

            return false;
        }

        if (!entry.WeaponScopes.TryGetValue(scope.ScopeKey, out profile!) || !profile.HasAnySpell())
        {
            if (removeCarrierOnNoProfile)
                RemoveCarrierBuff(player.CharacterEntity, entry);

            return false;
        }

        return true;
    }

    static bool TryApply(Entity character, PlayerSpellOverrideEntry entry, PlayerSpellScopeEntry profile, WeaponScopeResult scope, out string message)
    {
        if (!character.ExistsSafe())
        {
            message = "Target character entity is not valid.";
            return false;
        }

        if (ShapeshiftUtility.IsActiveForm(character))
        {
            RemoveCarrierBuff(character, entry);
            RuntimeStateCache.ClearSpellHash(character);
            message = $"Shapeshift/form active; player spell override suppressed. form:{ShapeshiftUtility.DescribeActiveForm(character)}";
            return false;
        }

        if (scope.IsCustomWeapon)
        {
            RemoveCarrierBuff(character, entry);

            if (CustomWeaponRuntimeOverrideService.TryApplyForCharacter(character, out string weaponMessage))
            {
                message = $"Custom weapon equipped ({scope.Detail}); player spell override suppressed. {weaponMessage}";
                return true;
            }

            message = $"Custom weapon equipped ({scope.Detail}); player spell override suppressed. {weaponMessage}";
            return false;
        }

        int carrierHash = ResolveCarrier(entry);
        var carrierGuid = new PrefabGUID(carrierHash);

        if (!carrierGuid.TryGetPrefabEntity(out var carrierPrefab))
        {
            message = $"Carrier buff prefab {carrierHash} was not found.";
            return false;
        }

        if (!carrierPrefab.TryGetBuffer<ReplaceAbilityOnSlotBuff>(out _))
        {
            message = $"Carrier buff prefab {carrierHash} does not have ReplaceAbilityOnSlotBuff buffer.";
            return false;
        }

        int spellHash = ComputeSpellHash(scope.ScopeKey, carrierHash, profile);
        bool carrierAlreadyCurrent =
            RuntimeStateCache.ShouldSkipSpellApply(character, spellHash)
            && character.TryGetBuff(carrierGuid, out _);

        if (!carrierAlreadyCurrent)
        {
            RemoveCarrierBuff(character, entry);

            if (!Core.ServerGameManager.TryInstantiateBuffEntityImmediate(character, character, carrierGuid, out Entity buffEntity))
            {
                if (!character.TryGetBuff(carrierGuid, out buffEntity))
                {
                    message = $"Failed to apply carrier buff {carrierHash}.";
                    return false;
                }
            }

            if (!buffEntity.TryGetBuffer<ReplaceAbilityOnSlotBuff>(out var replacements))
            {
                message = $"Applied carrier buff {carrierHash}, but its instance has no ReplaceAbilityOnSlotBuff buffer.";
                return false;
            }

            replacements.Clear();

            AddSlot(replacements, slot: 1, profile.Q);
            AddSlot(replacements, slot: 4, profile.E);
            AddSlot(replacements, slot: 2, profile.Dash);
            AddSlot(replacements, slot: 5, profile.R);
            AddSlot(replacements, slot: 6, profile.C);
            AddSlot(replacements, slot: 7, profile.T);

            RuntimeStateCache.SetSpellHash(character, spellHash);
        }

        string cooldownMessage = ApplyCooldownOverrides(character, profile);

        QueueCooldownRefresh($"weapon-scoped spell override applied for {scope.ScopeKey}");

        message = carrierAlreadyCurrent
            ? $"Spell overrides for scope '{scope.ScopeKey}' already current. {cooldownMessage}"
            : $"Applied spell overrides for scope '{scope.ScopeKey}' Q:{profile.Q} E:{profile.E} Dash:{profile.Dash} R:{profile.R} C:{profile.C} T:{profile.T} using carrier buff {carrierHash}. {cooldownMessage}";
        return true;
    }

    static int ComputeSpellHash(string scopeKey, int carrierHash, PlayerSpellScopeEntry profile)
    {
        var hash = new HashCode();
        hash.Add(scopeKey, StringComparer.Ordinal);
        hash.Add(carrierHash);
        hash.Add(profile.Q); hash.Add(profile.QCooldown);
        hash.Add(profile.E); hash.Add(profile.ECooldown);
        hash.Add(profile.Dash); hash.Add(profile.DashCooldown);
        hash.Add(profile.R); hash.Add(profile.RCooldown);
        hash.Add(profile.C); hash.Add(profile.CCooldown);
        hash.Add(profile.T); hash.Add(profile.TCooldown);
        return hash.ToHashCode();
    }

    static void AddSlot(DynamicBuffer<ReplaceAbilityOnSlotBuff> replacements, int slot, int abilityGroupGuid)
    {
        if (abilityGroupGuid == 0)
            return;

        replacements.Add(new ReplaceAbilityOnSlotBuff
        {
            Slot = slot,
            NewGroupId = new PrefabGUID(abilityGroupGuid),
            CopyCooldown = false
        });
    }

    static string ApplyCooldownOverrides(Entity character, PlayerSpellScopeEntry profile)
    {
        int requested = CountRequestedCooldowns(profile);
        if (requested == 0)
            return "No per-player cooldown overrides configured.";

        int updated = ApplyCooldownOverridesCount(character, profile);
        int missing = Math.Max(0, requested - updated);

        return $"Cooldown overrides requested:{requested}, attached entities updated:{updated}, missing/not-ready:{missing}.";
    }

    static int ApplyCooldownOverridesCount(Entity character, PlayerSpellScopeEntry profile)
    {
        int updated = 0;

        updated += ApplySlotCooldown(character, "Q", profile.Q, profile.QCooldown);
        updated += ApplySlotCooldown(character, "E", profile.E, profile.ECooldown);
        updated += ApplySlotCooldown(character, "Dash", profile.Dash, profile.DashCooldown);
        updated += ApplySlotCooldown(character, "R", profile.R, profile.RCooldown);
        updated += ApplySlotCooldown(character, "C", profile.C, profile.CCooldown);
        updated += ApplySlotCooldown(character, "T", profile.T, profile.TCooldown);

        return updated;
    }

    static int CountRequestedCooldowns(PlayerSpellScopeEntry profile)
    {
        int requested = 0;

        if (profile.Q != 0 && profile.QCooldown > 0f) requested++;
        if (profile.E != 0 && profile.ECooldown > 0f) requested++;
        if (profile.Dash != 0 && profile.DashCooldown > 0f) requested++;
        if (profile.R != 0 && profile.RCooldown > 0f) requested++;
        if (profile.C != 0 && profile.CCooldown > 0f) requested++;
        if (profile.T != 0 && profile.TCooldown > 0f) requested++;

        return requested;
    }

    static int ApplySlotCooldown(Entity character, string slotName, int abilityGroupGuidHash, float cooldownSeconds)
    {
        if (abilityGroupGuidHash == 0 || cooldownSeconds <= 0f)
            return 0;

        return AbilityCooldownUtility.SetCooldownOnAttachedAbilityGroup(
            character,
            new PrefabGUID(abilityGroupGuidHash),
            cooldownSeconds,
            $"PlayerSpellCooldown[{slotName}]"
        );
    }

    static int ResolveCarrier(PlayerSpellOverrideEntry entry)
    {
        int requested = entry.CarrierBuff != 0
            ? entry.CarrierBuff
            : PlayerSpellOverrideConfig.Current.DefaultCarrierBuff;

        if (!CustomWeaponRegistry.IsCustomWeaponEquipBuff(requested))
            return requested;

        string weaponName = CustomWeaponRegistry.GetCustomWeaponUsingEquipBuff(requested);
        int fallback = PlayerSpellOverrideConfig.SafeDefaultCarrierBuff;

        if (fallback != requested && !CustomWeaponRegistry.IsCustomWeaponEquipBuff(fallback))
        {
            Plugin.LogInstance.LogWarning($"Player spell carrier buff {requested} is also used by custom weapon '{weaponName}'. Falling back to reserved carrier {fallback} to avoid stripping custom weapon abilities.");
            return fallback;
        }

        Plugin.LogInstance.LogError($"Player spell carrier buff {requested} conflicts with custom weapon '{weaponName}', and fallback carrier {fallback} is also unavailable. Player spell overrides may conflict with custom weapons.");
        return requested;
    }

    internal static void ClearRuntimeSpellState(Entity character)
    {
        RuntimeStateCache.ClearSpellHash(character);
    }

    static void RemoveCarrierBuff(Entity character, PlayerSpellOverrideEntry entry)
    {
        int carrierHash = ResolveCarrier(entry);
        var carrierGuid = new PrefabGUID(carrierHash);

        if (character.TryGetBuff(carrierGuid, out Entity existing))
            existing.DestroySafe();

        RuntimeStateCache.ClearSpellHash(character);
    }
}
