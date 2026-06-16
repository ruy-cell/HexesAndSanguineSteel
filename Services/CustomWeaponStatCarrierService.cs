using System.Reflection;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppSystemType = Il2CppSystem.Type;
using ProjectM;
using ProjectM.Network;
using ProjectM.Shared;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;

namespace HexesAndSanguineSteel;

internal static class CustomWeaponStatCarrierService
{
    const string ScriptSpawnTypeName = "ProjectM.Scripting.ScriptSpawn";

    static int _preparedCarrierGuid;
    static Type? _scriptSpawnManagedType;
    static Il2CppSystemType? _scriptSpawnIl2CppType;

    sealed class PendingPayload
    {
        public Entity Character = Entity.Null;
        public Entity User = Entity.Null;
        public PrefabGUID Carrier;
        public CustomWeaponStatDef[] Stats = [];
        public int DesiredHash;
        public string Label = string.Empty;
        public DateTime CreatedAtUtc = DateTime.UtcNow;
        public float? HealthRatioBeforeRebuild;
    }

    static readonly Dictionary<long, PendingPayload> _pendingByCharacterAndCarrier = new();
    static readonly TimeSpan PendingPayloadTimeout = TimeSpan.FromSeconds(30);

    internal static int PendingPayloadCount => _pendingByCharacterAndCarrier.Count;

    internal static void ClearPendingForCharacter(Entity character)
    {
        if (!character.ExistsSafe() || _pendingByCharacterAndCarrier.Count == 0)
            return;

        foreach (var pair in _pendingByCharacterAndCarrier.ToArray())
        {
            if (pair.Value.Character.Equals(character))
                _pendingByCharacterAndCarrier.Remove(pair.Key);
        }
    }

    internal static void Reset()
    {
        _preparedCarrierGuid = 0;
        _pendingByCharacterAndCarrier.Clear();
    }

    internal static int ApplyStats(
        Entity character,
        PrefabGUID carrierGuid,
        int carrierHash,
        IReadOnlyList<CustomWeaponStatDef> stats,
        int desiredHash,
        string label)
    {
        if (!character.ExistsSafe() || !carrierGuid.HasValue() || stats.Count == 0)
            return 0;

        if (!PrepareStatCarrierPrefab(carrierGuid))
            return 0;

        Entity userEntity = Entity.Null;
        ulong platformId = 0;
        if (PlayerLookup.TryFindOnlinePlayer(character, out var player))
        {
            userEntity = player.UserEntity;
            platformId = player.PlatformId;
        }

        float? healthRatio = CaptureHealthRatio(character);
        var payload = new PendingPayload
        {
            Character = character,
            User = userEntity,
            Carrier = carrierGuid,
            Stats = stats.ToArray(),
            DesiredHash = desiredHash,
            Label = label,
            HealthRatioBeforeRebuild = healthRatio
        };

        _pendingByCharacterAndCarrier[Key(character, carrierHash)] = payload;

        if (character.TryGetBuff(carrierGuid, out Entity existingBuff)
            && existingBuff.ExistsSafe()
            && !existingBuff.Has<DestroyTag>())
        {
            int count = PopulateStatCarrier(existingBuff, payload);
            if (count > 0)
            {
                RuntimeStateCache.SetRuntimeStatHash(character, desiredHash);
                RuntimeOptimization.Debug($"[StatCarrier] Rebuilt existing stat carrier {carrierHash} for {label}; stats:{count} platform:{platformId}.");
                return count;
            }

            existingBuff.DestroySafe();
        }

        if (!Core.ServerGameManager.TryInstantiateBuffEntityImmediate(character, character, carrierGuid, out Entity statBuffEntity))
        {
            // Keep the pending payload alive; ScriptSpawnServer may still see the carrier if vanilla queued it.
            if (!character.TryGetBuff(carrierGuid, out statBuffEntity))
            {
                RuntimeOptimization.Debug($"[StatCarrier] Queued payload for {label}, but immediate carrier spawn {carrierHash} did not return an entity.");
                return 0;
            }
        }

        if (statBuffEntity.ExistsSafe())
        {
            int count = PopulateStatCarrier(statBuffEntity, payload);
            if (count > 0)
            {
                RuntimeStateCache.SetRuntimeStatHash(character, desiredHash);
                RuntimeOptimization.Debug($"[StatCarrier] Populated immediate stat carrier {carrierHash} for {label}; stats:{count} platform:{platformId}.");
                return count;
            }
        }

        return 0;
    }

    internal static bool TryPopulateSpawnedCarrier(Entity buffEntity)
    {
        if (!buffEntity.ExistsSafe() || !buffEntity.Has<PrefabGUID>() || !buffEntity.Has<Buff>())
            return false;

        var prefabGuid = Core.EntityManager.GetComponentData<PrefabGUID>(buffEntity);
        var buff = Core.EntityManager.GetComponentData<Buff>(buffEntity);
        Entity target = buff.Target;

        if (!target.ExistsSafe())
            return false;

        long key = Key(target, prefabGuid.GuidHash);
        if (!_pendingByCharacterAndCarrier.TryGetValue(key, out var payload))
            return false;

        int count = PopulateStatCarrier(buffEntity, payload);
        if (count <= 0)
            return false;

        RuntimeStateCache.SetRuntimeStatHash(target, payload.DesiredHash);
        RuntimeOptimization.Debug($"[StatCarrier] Populated spawned stat carrier {prefabGuid.GuidHash} for {payload.Label}; stats:{count}.");

        return true;
    }

    internal static void CleanupExpiredPayloads()
    {
        if (_pendingByCharacterAndCarrier.Count == 0)
            return;

        DateTime now = DateTime.UtcNow;
        foreach (var pair in _pendingByCharacterAndCarrier.ToArray())
        {
            var payload = pair.Value;
            if (!payload.Character.ExistsSafe()
                || now - payload.CreatedAtUtc > PendingPayloadTimeout)
            {
                _pendingByCharacterAndCarrier.Remove(pair.Key);
                RuntimeOptimization.Debug($"[StatCarrier] Expired pending stat payload for {payload.Label}.");
            }
        }
    }

    static int PopulateStatCarrier(Entity buffEntity, PendingPayload payload)
    {
        if (!buffEntity.ExistsSafe() || payload.Stats.Length == 0)
            return 0;

        var em = Core.EntityManager;

        TryStabilizeRuntimeCarrier(buffEntity);
        TryAddSyncToUser(buffEntity, payload.User);

        DynamicBuffer<ModifyUnitStatBuff_DOTS> buffer = em.HasBuffer<ModifyUnitStatBuff_DOTS>(buffEntity)
            ? em.GetBuffer<ModifyUnitStatBuff_DOTS>(buffEntity)
            : em.AddBuffer<ModifyUnitStatBuff_DOTS>(buffEntity);

        buffer.Clear();

        foreach (var stat in payload.Stats)
        {
            buffer.Add(new ModifyUnitStatBuff_DOTS
            {
                StatType = stat.StatType,
                ModificationType = stat.ModificationType,
                AttributeCapType = stat.AttributeCapType,
                SoftCapValue = 0f,
                Value = stat.Value,
                Modifier = 1f,
                IncreaseByStacks = false,
                ValueByStacks = 0,
                Priority = 0,
                Id = ModificationIDs.Create().NewModificationId()
            });
        }

        NormalizeHealthAfterStatCarrierUpdate(payload.Character, payload.HealthRatioBeforeRebuild);
        _pendingByCharacterAndCarrier.Remove(Key(payload.Character, payload.Carrier.GuidHash));

        return buffer.Length;
    }

    internal static bool PrepareStatCarrierPrefab(PrefabGUID carrier)
    {
        try
        {
            if (!carrier.HasValue())
                return false;

            if (_preparedCarrierGuid == carrier.GuidHash)
                return true;

            if (!carrier.TryGetPrefabEntity(out Entity prefabEntity))
            {
                Plugin.LogInstance.LogWarning($"[StatCarrier] Could not find stat carrier prefab entity for {carrier.GuidHash}.");
                return false;
            }

            var em = Core.EntityManager;

            if (!TryEnsureScriptSpawn(em, prefabEntity))
            {
                Plugin.LogInstance.LogWarning($"[StatCarrier] Could not resolve/add ScriptSpawn dynamically for carrier {carrier.GuidHash}. Immediate stat-buffer fallback will still be used.");
            }

            if (!em.HasBuffer<ModifyUnitStatBuff_DOTS>(prefabEntity))
                em.AddBuffer<ModifyUnitStatBuff_DOTS>(prefabEntity);
            else
                em.GetBuffer<ModifyUnitStatBuff_DOTS>(prefabEntity).Clear();

            _preparedCarrierGuid = carrier.GuidHash;
            RuntimeOptimization.Debug($"[StatCarrier] Prepared stat carrier prefab {carrier.GuidHash} with ScriptSpawn/ModifyUnitStatBuff_DOTS.");
            return true;
        }
        catch (Exception ex)
        {
            Plugin.LogInstance.LogWarning($"[StatCarrier] Failed to prepare stat carrier {carrier.GuidHash}: {ex.Message}");
            return false;
        }
    }

    static bool TryEnsureScriptSpawn(EntityManager em, Entity prefabEntity)
    {
        var il2CppType = ResolveScriptSpawnIl2CppType();
        if (il2CppType == null)
            return false;

        ComponentType componentType = new(il2CppType);

        if (!em.HasComponent(prefabEntity, componentType))
            em.AddComponent(prefabEntity, componentType);

        return true;
    }

    static Il2CppSystemType? ResolveScriptSpawnIl2CppType()
    {
        if (_scriptSpawnIl2CppType != null)
            return _scriptSpawnIl2CppType;

        _scriptSpawnManagedType ??=
            AccessTools.TypeByName(ScriptSpawnTypeName) ??
            AccessTools.TypeByName("ProjectM.Gameplay.Scripting.ScriptSpawn") ??
            AccessTools.TypeByName("ProjectM.ScriptSpawn") ??
            AccessTools.TypeByName("ScriptSpawn");

        if (_scriptSpawnManagedType == null)
            return null;

        MethodInfo? ofMethod = typeof(Il2CppType)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(method =>
                method.Name == nameof(Il2CppType.Of) &&
                method.IsGenericMethodDefinition &&
                method.GetParameters().Length == 0);

        if (ofMethod == null)
            return null;

        object? result = ofMethod.MakeGenericMethod(_scriptSpawnManagedType).Invoke(null, null);
        _scriptSpawnIl2CppType = result as Il2CppSystemType;
        return _scriptSpawnIl2CppType;
    }

    static void TryAddSyncToUser(Entity buffEntity, Entity userEntity)
    {
        var em = Core.EntityManager;

        if (!buffEntity.ExistsSafe() || !userEntity.ExistsSafe())
            return;

        DynamicBuffer<SyncToUserBuffer> syncToUsers = em.HasBuffer<SyncToUserBuffer>(buffEntity)
            ? em.GetBuffer<SyncToUserBuffer>(buffEntity)
            : em.AddBuffer<SyncToUserBuffer>(buffEntity);

        for (int i = 0; i < syncToUsers.Length; i++)
        {
            if (syncToUsers[i].UserEntity.Equals(userEntity))
                return;
        }

        syncToUsers.Add(new SyncToUserBuffer
        {
            UserEntity = userEntity
        });
    }

    static void TryStabilizeRuntimeCarrier(Entity buffEntity)
    {
        var em = Core.EntityManager;

        if (!buffEntity.ExistsSafe())
            return;

        if (em.HasComponent<LifeTime>(buffEntity))
            em.RemoveComponent<LifeTime>(buffEntity);

        if (em.HasComponent<RemoveBuffOnGameplayEvent>(buffEntity))
            em.RemoveComponent<RemoveBuffOnGameplayEvent>(buffEntity);

        if (em.HasComponent<RemoveBuffOnGameplayEventEntry>(buffEntity))
            em.RemoveComponent<RemoveBuffOnGameplayEventEntry>(buffEntity);
    }

    static float? CaptureHealthRatio(Entity character)
    {
        var em = Core.EntityManager;

        if (!character.ExistsSafe() || !em.HasComponent<Health>(character))
            return null;

        var health = em.GetComponentData<Health>(character);
        float maxHealth = health.MaxHealth._Value;

        if (maxHealth <= 0f)
            return null;

        float ratio = health.Value / maxHealth;
        if (ratio < 0f) ratio = 0f;
        if (ratio > 1f) ratio = 1f;
        return ratio;
    }

    static void NormalizeHealthAfterStatCarrierUpdate(Entity character, float? previousRatio)
    {
        var em = Core.EntityManager;

        if (!character.ExistsSafe() || !em.HasComponent<Health>(character))
            return;

        var health = em.GetComponentData<Health>(character);
        float maxHealth = health.MaxHealth._Value;

        if (maxHealth <= 0f)
            return;

        if (previousRatio.HasValue)
        {
            float ratio = previousRatio.Value;
            if (ratio < 0f) ratio = 0f;
            if (ratio > 1f) ratio = 1f;

            health.Value = maxHealth * ratio;
            if (health.Value > maxHealth)
                health.Value = maxHealth;

            em.SetComponentData(character, health);
            return;
        }

        if (health.Value > maxHealth)
        {
            health.Value = maxHealth;
            em.SetComponentData(character, health);
        }
    }

    static long Key(Entity character, int carrierHash)
        => (((long)character.Index << 32) ^ (uint)character.Version) ^ carrierHash;
}

[HarmonyPatch]
internal static class CustomWeaponScriptSpawnServerPatch
{
    static EntityQuery? _scriptSpawnQuery;

    static MethodBase TargetMethod()
    {
        Type? type =
            AccessTools.TypeByName("ScriptSpawnServer") ??
            AccessTools.TypeByName("ProjectM.ScriptSpawnServer") ??
            AccessTools.TypeByName("ProjectM.Shared.Systems.ScriptSpawnServer");

        if (type == null)
            throw new MissingMethodException("Unable to find ScriptSpawnServer type.");

        MethodInfo? method = AccessTools.Method(type, "OnUpdate");
        if (method == null)
            throw new MissingMethodException(type.FullName, "OnUpdate");

        return method;
    }

    [HarmonyPrefix]
    static void Prefix(object __instance)
    {
        try
        {
            if (CustomWeaponStatCarrierService.PendingPayloadCount == 0)
                return;

            EntityQuery query = GetScriptSpawnServerQuery(__instance);
            NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);

            try
            {
                for (int i = 0; i < entities.Length; i++)
                    CustomWeaponStatCarrierService.TryPopulateSpawnedCarrier(entities[i]);
            }
            finally
            {
                if (entities.IsCreated)
                    entities.Dispose();
            }

            CustomWeaponStatCarrierService.CleanupExpiredPayloads();
        }
        catch (Exception ex)
        {
            Plugin.LogInstance.LogWarning($"[StatCarrier] ScriptSpawnServer patch failed: {ex.Message}");
        }
    }

    static EntityQuery GetScriptSpawnServerQuery(object instance)
    {
        if (_scriptSpawnQuery.HasValue)
            return _scriptSpawnQuery.Value;

        _scriptSpawnQuery = ResolveSystemQuery(instance, "ScriptSpawnServer", "_EntityQuery");
        return _scriptSpawnQuery.Value;
    }

    static EntityQuery ResolveSystemQuery(object instance, string systemName, string preferredQueryName)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        Type type = instance.GetType();

        var field = type.GetField(preferredQueryName, flags);
        if (field?.GetValue(instance) is EntityQuery fieldQuery)
            return fieldQuery;

        var property = type.GetProperty(preferredQueryName, flags);
        if (property?.GetValue(instance) is EntityQuery propertyQuery)
            return propertyQuery;

        var entityQueriesProperty = type.GetProperty("EntityQueries", flags);
        if (entityQueriesProperty?.GetValue(instance) is EntityQuery[] entityQueries && entityQueries.Length > 0)
            return entityQueries[0];

        var entityQueriesField = type.GetField("EntityQueries", flags);
        if (entityQueriesField?.GetValue(instance) is EntityQuery[] fieldEntityQueries && fieldEntityQueries.Length > 0)
            return fieldEntityQueries[0];

        throw new InvalidOperationException($"Unable to resolve {systemName} query.");
    }
}
