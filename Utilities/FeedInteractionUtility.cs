using ProjectM;
using Stunlock.Core;
using Unity.Entities;

namespace HexesAndSanguineSteel;

internal static class FeedInteractionUtility
{
    // Feed / V Blood extraction / revive feeds temporarily replace or constrain the player's
    // ability bar. Hexes and Sanguine Steel must not inject weapon or spell override replacements into those
    // bars, otherwise high slots such as C/T can be outside the temporary buffer.
    static readonly HashSet<int> FeedAndExtractionPrefabs =
    [
        -1114937852, // AB_Feed_01_EnemyTarget_Debuff
        -376210658, // AB_Feed_01_Initiate_AbilityGroup
        1535667383, // AB_Feed_01_Initiate_Cast
        1231644507, // AB_Feed_01_Initiate_DashChannel
        -2068632541, // AB_Feed_02_Bite_Abort_AbilityGroup
        -1414476683, // AB_Feed_02_Bite_Abort_Cast
        366323518, // AB_Feed_02_Bite_Abort_Trigger
        1548379114, // AB_Feed_03_Complete_AbilityGroup
        1787637039, // AB_Feed_03_Complete_Cast
        -1106009274, // AB_Feed_03_Complete_Trigger
        -1948299363, // AB_Feed_04_DashAwayFromTarget
        -139079478, // AB_Feed_Trigger_Base
        1418642324, // AB_FeedBoss_01_EnemyTarget_Debuff
        -948311829, // AB_FeedBoss_01_Initiate_AbilityGroup
        1851942548, // AB_FeedBoss_01_Initiate_Cast
        697172810, // AB_FeedBoss_01_Initiate_DashChannel
        -1347356700, // AB_FeedBoss_03_Complete_AbilityGroup
        -1797102561, // AB_FeedBoss_03_Complete_AreaDamage
        -1031870834, // AB_FeedBoss_03_Complete_Cast
        958508368, // AB_FeedBoss_03_Complete_Trigger
        1233405326, // AB_FeedBoss_04_Complete_AreaTriggerBuff
        -670750933, // AB_FeedBoss_FeedOnDracula_01_EnemyTarget_Debuff
        1790847128, // AB_FeedBoss_FeedOnDracula_01_Initiate_AbilityGroup
        -649630224, // AB_FeedBoss_FeedOnDracula_01_Initiate_Cast
        51055185, // AB_FeedBoss_FeedOnDracula_01_Initiate_DashChannel
        -1560986745, // AB_FeedBoss_FeedOnDracula_03_Complete_AbilityGroup
        1340086775, // AB_FeedBoss_FeedOnDracula_03_Complete_AreaDamage
        1306418337, // AB_FeedBoss_FeedOnDracula_03_Complete_Cast
        1229263634, // AB_FeedBoss_FeedOnDracula_03_Complete_Trigger
        1186118159, // AB_FeedBoss_FeedOnDracula_04_Complete_AreaTriggerBuff
        -206231665, // AB_FeedDraculaBloodSoul_01_EnemyTarget_Debuff
        1491658671, // AB_FeedDraculaBloodSoul_01_Initiate_AbilityGroup
        -1573506596, // AB_FeedDraculaBloodSoul_01_Initiate_Cast
        -1044861452, // AB_FeedDraculaBloodSoul_01_Initiate_DashChannel
        -1565434117, // AB_FeedDraculaBloodSoul_03_Complete_AbilityGroup
        250152500, // AB_FeedDraculaBloodSoul_03_Complete_AreaDamage
        713711633, // AB_FeedDraculaBloodSoul_03_Complete_Cast
        -1889378229, // AB_FeedDraculaBloodSoul_03_Complete_Trigger
        -331003702, // AB_FeedDraculaBloodSoul_04_Complete_AreaTriggerBuff
        1815101964, // AB_FeedDraculaOrb_01_EnemyTarget_Debuff
        1393329717, // AB_FeedDraculaOrb_01_Initiate_AbilityGroup
        -1529850854, // AB_FeedDraculaOrb_01_Initiate_Cast
        -1551286374, // AB_FeedDraculaOrb_01_Initiate_DashChannel
        253163764, // AB_FeedDraculaOrb_03_Complete_AbilityGroup
        1301390971, // AB_FeedDraculaOrb_03_Complete_AreaDamage
        -1212689323, // AB_FeedDraculaOrb_03_Complete_Cast
        -526712042, // AB_FeedDraculaOrb_03_Complete_Trigger
        -604904760, // AB_FeedDraculaOrb_04_Complete_AreaTriggerBuff
        -1581547715, // AB_FeedEnemyVampire_01_EnemyTarget_Debuff
        391943159, // AB_FeedEnemyVampire_01_Initiate_AbilityGroup
        1283732362, // AB_FeedEnemyVampire_01_Initiate_Cast
        1798838621, // AB_FeedEnemyVampire_01_Initiate_DashChannel
        -1365570044, // AB_FeedEnemyVampire_02_Bite_Abort_AbilityGroup
        950997250, // AB_FeedEnemyVampire_02_Bite_Abort_Cast
        -1399190295, // AB_FeedEnemyVampire_02_Bite_Abort_Trigger
        -578672354, // AB_FeedEnemyVampire_03_Complete_AbilityGroup
        -1213188934, // AB_FeedEnemyVampire_03_Complete_Cast
        -1006431398, // AB_FeedEnemyVampire_03_Complete_Trigger
        -856042777, // AB_FeedEnemyVampire_04_DashAwayFromTarget
        -264516260, // AB_FeedFriendly_01_EnemyTarget_Debuff
        1501158829, // AB_FeedFriendly_01_Initiate_AbilityGroup
        -877555015, // AB_FeedFriendly_01_Initiate_Cast
        1690123127, // AB_FeedFriendly_01_Initiate_DashChannel
        -1278121774, // AB_FeedFriendly_02_Bite_Abort_AbilityGroup
        -316716610, // AB_FeedFriendly_02_Bite_Abort_Cast
        236713613, // AB_FeedFriendly_02_Bite_Abort_Trigger
        117247654, // AB_FeedFriendly_03_Complete_AbilityGroup
        1739570447, // AB_FeedFriendly_03_Complete_Cast
        -1275273993, // AB_FeedFriendly_03_Complete_Trigger
        -1745004158, // AB_FeedFriendly_04_DashAwayFromTarget
        585228944, // AB_FeedGateBoss_01_EnemyTarget_Debuff
        -1695763915, // AB_FeedGateBoss_01_Initiate_AbilityGroup
        -234803016, // AB_FeedGateBoss_01_Initiate_Cast
        -1335827123, // AB_FeedGateBoss_01_Initiate_DashChannel
        -1446310610, // AB_FeedGateBoss_03_Complete_AbilityGroup
        402061920, // AB_FeedGateBoss_03_Complete_AreaDamage
        1289149810, // AB_FeedGateBoss_03_Complete_Cast
        -1669827947, // AB_FeedGateBoss_03_Complete_Trigger
        -354622715, // AB_FeedGateBoss_04_Complete_AreaTriggerBuff
        579690183, // AB_FeedRevive_01_AllyTarget_Debuff
        1754040702, // AB_FeedRevive_01_Initiate_AbilityGroup
        1525766239, // AB_FeedRevive_01_Initiate_Cast
        -595871655, // AB_FeedRevive_01_Initiate_DashChannel
        1586616867, // AB_FeedRevive_02_Bite_Abort_AbilityGroup
        1508231357, // AB_FeedRevive_02_Bite_Abort_Cast
        -205830402, // AB_FeedRevive_02_Bite_Abort_Trigger
        2072201164, // AB_FeedRevive_03_Complete_AbilityGroup
        -1354315272, // AB_FeedRevive_03_Complete_Cast
        -938281780, // AB_FeedRevive_03_Complete_Trigger
        -179005483, // AB_FeedRevive_04_DashAwayFromTarget
    ];

    internal static bool IsActiveFeedOrExtraction(Entity character)
    {
        if (!character.ExistsSafe())
            return false;

        foreach (int prefabHash in FeedAndExtractionPrefabs)
        {
            if (character.TryGetBuff(new PrefabGUID(prefabHash), out _))
                return true;
        }

        return false;
    }

    internal static bool IsFeedOrExtractionReplacementBuff(Entity buffEntity)
    {
        if (!buffEntity.ExistsSafe())
            return false;

        if (!buffEntity.TryRead<PrefabGUID>(out var prefabGuid))
            return false;

        return FeedAndExtractionPrefabs.Contains(prefabGuid.GuidHash);
    }

    internal static string DescribeActiveFeedOrExtraction(Entity character)
    {
        if (!character.ExistsSafe())
            return "none";

        foreach (int prefabHash in FeedAndExtractionPrefabs)
        {
            if (character.TryGetBuff(new PrefabGUID(prefabHash), out _))
                return prefabHash.ToString();
        }

        return "none";
    }
}
