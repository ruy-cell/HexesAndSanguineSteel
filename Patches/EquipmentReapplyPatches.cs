using HarmonyLib;
using ProjectM;
using Unity.Collections;
using Unity.Entities;

namespace HexesAndSanguineSteel.Patches;

[HarmonyPatch]
internal static class EquipmentReapplyPatches
{
    [HarmonyPatch(typeof(EquipItemSystem), "OnUpdate")]
    [HarmonyPostfix]
    static void EquipItemSystemPostfix()
    {
        PlayerSpellOverrideService.QueueReapplyAllOnline("EquipItemSystem");
    }

    [HarmonyPatch(typeof(EquipItemFromInventorySystem), "OnUpdate")]
    [HarmonyPostfix]
    static void EquipItemFromInventorySystemPostfix()
    {
        PlayerSpellOverrideService.QueueReapplyAllOnline("EquipItemFromInventorySystem");
    }

    [HarmonyPatch(typeof(EquipmentTransferSystem), "OnUpdate")]
    [HarmonyPostfix]
    static void EquipmentTransferSystemPostfix()
    {
        PlayerSpellOverrideService.QueueReapplyAllOnline("EquipmentTransferSystem");
    }
}

[HarmonyPatch(typeof(ReplaceAbilityOnSlotSystem), "OnUpdate")]
internal static class ReplaceAbilityOnSlotSystemPatch
{
    [HarmonyPrefix]
    static void Prefix(ReplaceAbilityOnSlotSystem __instance)
    {
        NativeArray<Entity> entities = default;

        try
        {
            // Same live replacement timing used by CustomAbilities:
            // mutate the buff entities before V Rising consumes ReplaceAbilityOnSlotBuff.
            entities = __instance.__query_1482480545_0.ToEntityArray(Allocator.Temp);

            foreach (Entity buffEntity in entities)
            {
                try
                {
                    if (AbilityReplacementInjectionService.TryInjectForBuff(buffEntity, out string message)
                        && RuntimeOptimization.DebugLogging)
                    {
                        Plugin.LogInstance.LogInfo(message);
                    }
                }
                catch (Exception ex)
                {
                    Plugin.LogInstance.LogWarning($"Ability replacement injection failed for buff entity {buffEntity.Index}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.LogInstance.LogWarning($"ReplaceAbilityOnSlotSystem prefix injection failed: {ex.Message}");
        }
        finally
        {
            if (entities.IsCreated)
                entities.Dispose();
        }
    }

    [HarmonyPostfix]
    static void Postfix()
    {
        try
        {
            PlayerSpellOverrideService.ProcessQueuedAfterReplaceAbilitySystem();
        }
        catch (Exception ex)
        {
            Plugin.LogInstance.LogWarning($"Queued spell/weapon cooldown reapply failed: {ex}");
        }
    }
}
