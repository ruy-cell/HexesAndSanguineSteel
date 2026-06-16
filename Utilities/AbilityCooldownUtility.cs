using ProjectM;
using Stunlock.Core;
using Unity.Entities;

namespace HexesAndSanguineSteel;

internal static class AbilityCooldownUtility
{
    internal static int SetCooldownOnAttachedAbilityGroup(Entity character, PrefabGUID abilityGroupGuid, float cooldownSeconds, string sourceLabel)
    {
        if (!character.ExistsSafe() || !abilityGroupGuid.HasValue() || cooldownSeconds <= 0f)
            return 0;

        var startPrefabs = RuntimePrefabCache.GetAbilityStartPrefabs(abilityGroupGuid);

        int updated = 0;

        if (startPrefabs.Count > 0)
        {
            for (int i = 0; i < startPrefabs.Count; i++)
                updated += SetCooldownOnAttachedPrefab(character, startPrefabs[i], cooldownSeconds, sourceLabel);
        }
        else
        {
            // Fallback for configs that accidentally point at an attached/cast prefab directly.
            updated += SetCooldownOnAttachedPrefab(character, abilityGroupGuid, cooldownSeconds, sourceLabel);
        }

        return updated;
    }

    internal static int SetCooldownOnAttachedPrefab(Entity character, PrefabGUID castGuid, float cooldownSeconds, string sourceLabel)
    {
        if (!character.ExistsSafe() || !castGuid.HasValue() || cooldownSeconds <= 0f)
            return 0;

        if (!character.TryGetBuffer<AttachedBuffer>(out var attachedBuffer))
            return 0;

        int updated = 0;

        foreach (AttachedBuffer attached in attachedBuffer)
        {
            if (!attached.PrefabGuid.Equals(castGuid))
                continue;

            Entity attachedEntity = attached.Entity;
            if (!attachedEntity.ExistsSafe() || !attachedEntity.Has<AbilityCooldownData>())
                continue;

            attachedEntity.With<AbilityCooldownData>((ref AbilityCooldownData cooldown) =>
            {
                cooldown.Cooldown._Value = cooldownSeconds;
            });

            updated++;
        }

        if (updated > 0)
            RuntimeOptimization.Debug($"{sourceLabel}: set runtime cooldown {cooldownSeconds:0.##}s on {updated} attached instance(s) for prefab {castGuid.GuidHash}.");

        return updated;
    }

    internal static int SetCooldownOnPrefabAbilityGroup(PrefabGUID abilityGroupGuid, float cooldownSeconds, string sourceLabel)
    {
        if (!abilityGroupGuid.HasValue() || cooldownSeconds <= 0f)
            return 0;

        if (!abilityGroupGuid.TryGetPrefabEntity(out var groupEntity))
            return 0;

        int updated = 0;

        updated += SetCooldownOnPrefabEntity(groupEntity, abilityGroupGuid, cooldownSeconds, sourceLabel);

        if (groupEntity.TryGetBuffer<AbilityGroupStartAbilitiesBuffer>(out var starts) && starts.Length > 0)
        {
            for (int i = 0; i < starts.Length; i++)
            {
                PrefabGUID castGuid = starts[i].PrefabGUID;
                if (castGuid.TryGetPrefabEntity(out var castEntity))
                    updated += SetCooldownOnPrefabEntity(castEntity, castGuid, cooldownSeconds, sourceLabel);
            }
        }

        return updated;
    }

    static int SetCooldownOnPrefabEntity(Entity entity, PrefabGUID prefabGuid, float cooldownSeconds, string sourceLabel)
    {
        if (!entity.ExistsSafe() || !entity.Has<AbilityCooldownData>())
            return 0;

        entity.With<AbilityCooldownData>((ref AbilityCooldownData cooldown) =>
        {
            cooldown.Cooldown._Value = cooldownSeconds;
        });

        RuntimeOptimization.Debug($"{sourceLabel}: set prefab cooldown {cooldownSeconds:0.##}s on prefab {prefabGuid.GuidHash}.");
        return 1;
    }
}
