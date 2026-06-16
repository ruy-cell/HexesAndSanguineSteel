using ProjectM;
using Stunlock.Core;
using Unity.Entities;

namespace HexesAndSanguineSteel;

internal static class RuntimeOptimization
{
    internal static bool DebugLogging => CustomWeaponConfig.DebugLogging;

    internal static void Debug(string message)
    {
        if (DebugLogging)
            Plugin.LogInstance.LogInfo(message);
    }

    static readonly HashSet<string> _warnedOnce = new(StringComparer.Ordinal);
    static readonly HashSet<string> _infoOnce = new(StringComparer.Ordinal);
    static readonly object _logLock = new();

    internal static void InfoOnce(string key, string message)
    {
        lock (_logLock)
        {
            if (!_infoOnce.Add(key))
                return;
        }

        Plugin.LogInstance.LogInfo(message);
    }

    internal static void WarnOnce(string key, string message)
    {
        lock (_logLock)
        {
            if (!_warnedOnce.Add(key))
                return;
        }

        Plugin.LogInstance.LogWarning(message);
    }
}

internal static class RuntimePrefabCache
{
    static readonly Dictionary<int, Entity> _prefabEntities = new();
    static readonly Dictionary<int, PrefabGUID[]> _abilityGroupStartPrefabs = new();

    internal static void Clear()
    {
        _prefabEntities.Clear();
        _abilityGroupStartPrefabs.Clear();
    }

    internal static bool TryGetPrefabEntity(PrefabGUID prefabGuid, out Entity entity)
    {
        entity = Entity.Null;

        if (!prefabGuid.HasValue())
            return false;

        int hash = prefabGuid.GuidHash;

        if (_prefabEntities.TryGetValue(hash, out entity) && entity.ExistsSafe())
            return true;

        try
        {
            entity = Core.ServerGameManager.GetPrefabEntity(prefabGuid);
            if (!entity.ExistsSafe())
                return false;

            _prefabEntities[hash] = entity;
            return true;
        }
        catch (Exception ex)
        {
            RuntimeOptimization.Debug($"Could not resolve prefab {hash}: {ex.Message}");
            entity = Entity.Null;
            return false;
        }
    }

    internal static IReadOnlyList<PrefabGUID> GetAbilityStartPrefabs(PrefabGUID abilityGroupGuid)
    {
        if (!abilityGroupGuid.HasValue())
            return Array.Empty<PrefabGUID>();

        int hash = abilityGroupGuid.GuidHash;
        if (_abilityGroupStartPrefabs.TryGetValue(hash, out var cached))
            return cached;

        if (!TryGetPrefabEntity(abilityGroupGuid, out var groupEntity))
        {
            _abilityGroupStartPrefabs[hash] = Array.Empty<PrefabGUID>();
            return _abilityGroupStartPrefabs[hash];
        }

        if (!groupEntity.TryGetBuffer<AbilityGroupStartAbilitiesBuffer>(out var starts) || starts.Length == 0)
        {
            _abilityGroupStartPrefabs[hash] = Array.Empty<PrefabGUID>();
            return _abilityGroupStartPrefabs[hash];
        }

        var result = new PrefabGUID[starts.Length];
        for (int i = 0; i < starts.Length; i++)
            result[i] = starts[i].PrefabGUID;

        _abilityGroupStartPrefabs[hash] = result;
        return result;
    }
}

internal static class RuntimeStateCache
{
    sealed class State
    {
        public string ScopeKey = string.Empty;
        public int SpellHash;
        public int CustomWeaponAbilityHash;
        public int RuntimeStatHash;
        public DateTime LastAdminWideRefreshUtc = DateTime.MinValue;
    }

    static readonly Dictionary<long, State> _states = new();

    static long Key(Entity entity) => ((long)entity.Index << 32) ^ (uint)entity.Version;

    static State Get(Entity character)
    {
        long key = Key(character);
        if (!_states.TryGetValue(key, out var state))
        {
            state = new State();
            _states[key] = state;
        }

        return state;
    }

    internal static void Clear(Entity character)
    {
        if (character.ExistsSafe())
            _states.Remove(Key(character));
    }

    internal static bool IsSameScope(Entity character, string scopeKey)
    {
        var state = Get(character);
        return string.Equals(state.ScopeKey, scopeKey, StringComparison.Ordinal);
    }

    internal static void SetScope(Entity character, string scopeKey)
    {
        Get(character).ScopeKey = scopeKey;
    }

    internal static bool ShouldSkipSpellApply(Entity character, int hash)
    {
        var state = Get(character);
        return state.SpellHash == hash;
    }

    internal static void SetSpellHash(Entity character, int hash)
    {
        Get(character).SpellHash = hash;
    }

    internal static void ClearSpellHash(Entity character)
    {
        Get(character).SpellHash = 0;
    }

    internal static bool ShouldSkipCustomWeaponAbilityApply(Entity character, int hash)
    {
        var state = Get(character);
        return state.CustomWeaponAbilityHash == hash;
    }

    internal static void SetCustomWeaponAbilityHash(Entity character, int hash)
    {
        Get(character).CustomWeaponAbilityHash = hash;
    }

    internal static void ClearCustomWeaponAbilityHash(Entity character)
    {
        Get(character).CustomWeaponAbilityHash = 0;
    }

    internal static bool ShouldSkipRuntimeStats(Entity character, int hash)
    {
        var state = Get(character);
        return state.RuntimeStatHash == hash;
    }

    internal static void SetRuntimeStatHash(Entity character, int hash)
    {
        Get(character).RuntimeStatHash = hash;
    }

    internal static void ClearRuntimeStatHash(Entity character)
    {
        Get(character).RuntimeStatHash = 0;
    }
}

internal static class AdminCommandRateLimiter
{
    static readonly Dictionary<string, DateTime> _lastRunUtc = new(StringComparer.OrdinalIgnoreCase);

    internal static bool TryRun(string key, out string message)
    {
        double seconds = CustomWeaponConfig.AdminRefreshCooldownSeconds;
        if (seconds <= 0)
        {
            message = string.Empty;
            return true;
        }

        DateTime now = DateTime.UtcNow;
        if (_lastRunUtc.TryGetValue(key, out DateTime last))
        {
            double elapsed = (now - last).TotalSeconds;
            if (elapsed < seconds)
            {
                message = $"Please wait {(seconds - elapsed):0.0}s before running '{key}' again.";
                return false;
            }
        }

        _lastRunUtc[key] = now;
        message = string.Empty;
        return true;
    }
}
