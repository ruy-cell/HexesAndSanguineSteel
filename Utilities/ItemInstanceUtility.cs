using ProjectM;
using Unity.Entities;

namespace HexesAndSanguineSteel;

internal static class ItemInstanceUtility
{
    internal static bool TryGetHeldWeaponEntity(Entity character, out Entity itemEntity)
    {
        itemEntity = Entity.Null;

        if (!character.ExistsSafe() || !character.TryRead<Equipment>(out var equipment))
            return false;

        itemEntity = equipment.WeaponSlot.SlotEntity._Entity;
        return itemEntity.ExistsSafe();
    }

    internal static bool TryGetHeldWeaponPrefab(Entity character, out Stunlock.Core.PrefabGUID prefabGuid)
    {
        prefabGuid = default;

        if (!TryGetHeldWeaponEntity(character, out var itemEntity))
            return false;

        return itemEntity.TryRead(out prefabGuid) && prefabGuid.HasValue();
    }

    internal static bool TryGetHeldWeaponEquipBuff(Entity character, out Stunlock.Core.PrefabGUID buffGuid)
    {
        buffGuid = default;

        if (!TryGetHeldWeaponPrefab(character, out var itemPrefab) || !itemPrefab.HasValue())
            return false;

        if (!itemPrefab.TryGetPrefabEntity(out var itemPrefabEntity))
            return false;

        if (!itemPrefabEntity.TryRead<EquippableData>(out var equippableData))
            return false;

        buffGuid = equippableData.BuffGuid;
        return buffGuid.HasValue();
    }

    internal static bool TryGetSequenceGuid(Entity itemEntity, out int sequenceGuidHash)
    {
        sequenceGuidHash = 0;

        if (!itemEntity.ExistsSafe())
            return false;

        if (!itemEntity.TryRead<SequenceGUID>(out var sequenceGuid))
            return false;

        sequenceGuidHash = sequenceGuid.GuidHash;
        return sequenceGuidHash != 0;
    }

    internal static int GetOrCreateSequenceGuid(Entity itemEntity)
    {
        if (TryGetSequenceGuid(itemEntity, out int existing))
            return existing;

        int guidHash = Guid.NewGuid().GetHashCode();
        Core.EntityManager.AddComponentData(itemEntity, new SequenceGUID(guidHash));
        return guidHash;
    }
}
