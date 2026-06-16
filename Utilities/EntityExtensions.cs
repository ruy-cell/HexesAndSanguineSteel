using ProjectM;
using ProjectM.Shared;
using Stunlock.Core;
using Unity.Entities;

namespace HexesAndSanguineSteel;

internal static class EntityExtensions
{
    public delegate void WithRefHandler<T>(ref T item) where T : struct;

    internal static bool ExistsSafe(this Entity entity)
        => entity != Entity.Null && Core.EntityManager.Exists(entity);

    internal static bool Has<T>(this Entity entity) where T : struct
        => entity.ExistsSafe() && Core.EntityManager.HasComponent<T>(entity);

    internal static bool TryRead<T>(this Entity entity, out T value) where T : struct
    {
        value = default;
        if (!entity.Has<T>())
            return false;

        value = Core.EntityManager.GetComponentData<T>(entity);
        return true;
    }

    internal static void With<T>(this Entity entity, WithRefHandler<T> action) where T : struct
    {
        if (!entity.TryRead<T>(out var value))
        {
            Plugin.LogInstance.LogWarning($"Missing component {typeof(T).Name} on {entity}.");
            return;
        }

        action(ref value);
        Core.EntityManager.SetComponentData(entity, value);
    }

    internal static bool TryGetBuffer<T>(this Entity entity, out DynamicBuffer<T> buffer) where T : struct
    {
        buffer = default;
        if (!entity.ExistsSafe() || !Core.EntityManager.HasBuffer<T>(entity))
            return false;

        buffer = Core.EntityManager.GetBuffer<T>(entity);
        return true;
    }

    internal static void ClearBuffer<T>(this Entity entity) where T : struct
    {
        if (!entity.TryGetBuffer<T>(out var buffer))
        {
            Plugin.LogInstance.LogWarning($"Missing buffer {typeof(T).Name} on {entity}.");
            return;
        }

        buffer.Clear();
    }

    internal static void AddToBuffer<T>(this Entity entity, T element) where T : struct
    {
        if (!entity.TryGetBuffer<T>(out var buffer))
        {
            Plugin.LogInstance.LogWarning($"Missing buffer {typeof(T).Name} on {entity}.");
            return;
        }

        buffer.Add(element);
    }

    internal static void EditBuffer<T>(this Entity entity, int index, WithRefHandler<T> action) where T : struct
    {
        if (!entity.TryGetBuffer<T>(out var buffer))
        {
            Plugin.LogInstance.LogWarning($"Missing buffer {typeof(T).Name} on {entity}.");
            return;
        }

        if ((uint)index >= (uint)buffer.Length)
        {
            Plugin.LogInstance.LogWarning($"Buffer index {index} out of range for {typeof(T).Name}; len={buffer.Length}.");
            return;
        }

        var value = buffer[index];
        action(ref value);
        buffer[index] = value;
    }

    internal static void InsertBuffer<T>(this Entity entity, int index, T element) where T : struct
    {
        if (!entity.TryGetBuffer<T>(out var buffer))
        {
            Plugin.LogInstance.LogWarning($"Missing buffer {typeof(T).Name} on {entity}.");
            return;
        }

        if ((uint)index > (uint)buffer.Length)
        {
            Plugin.LogInstance.LogWarning($"Insert index {index} out of range for {typeof(T).Name}; len={buffer.Length}.");
            return;
        }

        buffer.Insert(index, element);
    }

    internal static bool TryGetBuff(this Entity target, PrefabGUID buffPrefabGuid, out Entity buffEntity)
    {
        buffEntity = Entity.Null;

        if (!target.ExistsSafe() || !buffPrefabGuid.HasValue())
            return false;

        return Core.ServerGameManager.TryGetBuff(target, buffPrefabGuid.ToIdentifier(), out buffEntity)
            && buffEntity.ExistsSafe();
    }


    internal static bool HasBuff(this Entity target, PrefabGUID buffPrefabGuid)
        => target.TryGetBuff(buffPrefabGuid, out _);

    internal static bool TryApplyBuffSimple(this Entity target, PrefabGUID buffPrefabGuid)
        => BuffApplicationUtility.TryApplyBuff(target, target, buffPrefabGuid);

    internal static bool TryApplyBuffWithOwner(this Entity target, Entity owner, PrefabGUID buffPrefabGuid)
        => BuffApplicationUtility.TryApplyBuff(target, owner, buffPrefabGuid);

    internal static void DestroySafe(this Entity entity)
    {
        if (!entity.ExistsSafe())
            return;

        // Equip/form/stat swaps can queue the same runtime carrier for removal more than once.
        // Do not call DestroyUtility again on entities that are already marked for destruction;
        // that can race with DestroyGroup and surface as "entity does not exist" Burst aborts.
        if (entity.Has<DestroyTag>())
            return;

        try
        {
            if (entity.Has<Buff>())
                DestroyUtility.Destroy(Core.EntityManager, entity, DestroyDebugReason.TryRemoveBuff);
            else
                DestroyUtility.Destroy(Core.EntityManager, entity);
        }
        catch (Exception ex)
        {
            RuntimeOptimization.Debug($"DestroySafe skipped stale entity {entity.Index}:{entity.Version}: {ex.Message}");
        }
    }

    internal static bool TryGetPrefabEntity(this PrefabGUID prefabGuid, out Entity entity)
        => RuntimePrefabCache.TryGetPrefabEntity(prefabGuid, out entity);

    internal static bool HasValue(this PrefabGUID prefabGuid)
        => prefabGuid.GuidHash != 0;
}
