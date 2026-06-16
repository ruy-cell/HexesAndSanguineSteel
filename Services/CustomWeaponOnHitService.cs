using HarmonyLib;
using ProjectM;
using ProjectM.Gameplay.Systems;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;

namespace HexesAndSanguineSteel;

[HarmonyPatch(typeof(StatChangeSystem), nameof(StatChangeSystem.OnUpdate))]
internal static class CustomWeaponOnHitService
{
    static readonly Random Rng = new();

    [HarmonyPostfix]
    static void Postfix(StatChangeSystem __instance)
    {
        if (CustomWeaponRegistry.CurrentWeapons.Count == 0)
            return;

        NativeArray<Entity> entities = default;
        NativeArray<DamageTakenEvent> events = default;

        try
        {
            entities = __instance._DamageTakenEventQuery.ToEntityArray(Allocator.Temp);
            events = __instance._DamageTakenEventQuery.ToComponentDataArray<DamageTakenEvent>(Allocator.Temp);

            for (int i = 0; i < events.Length; i++)
            {
                var damageEvent = events[i];
                Entity target = damageEvent.Entity;

                if (!target.ExistsSafe() || !target.Has<Health>())
                    continue;

                Entity source = damageEvent.Source;
                Entity sourceOwner = ResolveOwner(source);

                if (!sourceOwner.ExistsSafe() || !sourceOwner.Has<PlayerCharacter>())
                    continue;

                if (!CustomWeaponRegistry.TryGetCustomWeaponForCharacter(sourceOwner, out var weapon, out _))
                    continue;

                if (weapon.OnHitEffects.Count == 0)
                    continue;

                bool targetIsPlayer = target.Has<PlayerCharacter>();

                foreach (var effect in weapon.OnHitEffects)
                {
                    if (targetIsPlayer && !effect.AffectPlayers)
                        continue;

                    if (!targetIsPlayer && !effect.AffectNonPlayers)
                        continue;

                    if (effect.Chance <= 0f)
                        continue;

                    if (effect.Chance < 1f && Rng.NextDouble() > effect.Chance)
                        continue;

                    ApplyEffect(sourceOwner, target, weapon, effect);
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.LogInstance.LogWarning($"[CustomWeaponOnHitService] Exception: {ex}");
        }
        finally
        {
            if (events.IsCreated) events.Dispose();
            if (entities.IsCreated) entities.Dispose();
        }
    }

    static Entity ResolveOwner(Entity source)
    {
        if (!source.ExistsSafe())
            return Entity.Null;

        if (source.Has<PlayerCharacter>())
            return source;

        if (source.TryRead<EntityOwner>(out var owner) && owner.Owner.ExistsSafe())
            return owner.Owner;

        return source;
    }

    static void ApplyEffect(Entity sourcePlayer, Entity target, CustomWeaponDef weapon, CustomWeaponOnHitEffectDef effect)
    {
        if (!effect.TargetBuff.HasValue())
            return;

        if (target.TryApplyBuffWithOwner(sourcePlayer, effect.TargetBuff))
            RuntimeOptimization.Debug($"OnHit '{effect.Name}' ({effect.School}) from {weapon.Name}: applied school buff {effect.TargetBuff.GuidHash}.");
    }
}
