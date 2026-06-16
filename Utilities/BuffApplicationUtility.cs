using System.Reflection;
using ProjectM;
using Stunlock.Core;
using Unity.Entities;

namespace HexesAndSanguineSteel;

internal static class BuffApplicationUtility
{
    static MethodInfo? _instantiateBuffEntityImmediate;
    static bool _methodResolved;

    internal static bool TryApplyBuff(Entity target, Entity owner, PrefabGUID buffPrefabGuid)
    {
        if (!target.ExistsSafe() || !owner.ExistsSafe() || !buffPrefabGuid.HasValue())
            return false;

        try
        {
            if (!_methodResolved)
            {
                _methodResolved = true;
                _instantiateBuffEntityImmediate = Core.ServerGameManager
                    .GetType()
                    .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .FirstOrDefault(m => m.Name == "InstantiateBuffEntityImmediate");
            }

            if (_instantiateBuffEntityImmediate is not null)
            {
                var parameters = _instantiateBuffEntityImmediate.GetParameters();
                object?[] args = parameters.Length switch
                {
                    5 => [owner, target, buffPrefabGuid, null, 0],
                    4 => [owner, target, buffPrefabGuid, 0],
                    3 => [target, buffPrefabGuid, 0],
                    _ => []
                };

                if (args.Length > 0)
                {
                    _instantiateBuffEntityImmediate.Invoke(Core.ServerGameManager, args);
                    TrySetOwner(target, owner, buffPrefabGuid);
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            RuntimeOptimization.Debug($"InstantiateBuffEntityImmediate failed for buff {buffPrefabGuid.GuidHash}: {ex.Message}");
        }

        return false;
    }

    static void TrySetOwner(Entity target, Entity owner, PrefabGUID buffPrefabGuid)
    {
        if (!target.TryGetBuff(buffPrefabGuid, out var buffEntity))
            return;

        if (!buffEntity.Has<EntityOwner>())
            return;

        buffEntity.With<EntityOwner>((ref EntityOwner entityOwner) =>
        {
            entityOwner.Owner = owner;
        });
    }
}
