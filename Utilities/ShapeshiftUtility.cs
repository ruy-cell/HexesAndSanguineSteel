using ProjectM;
using Stunlock.Core;
using Unity.Entities;

namespace HexesAndSanguineSteel;

internal static class ShapeshiftUtility
{
    // These are player form/shape buffs that replace the normal weapon bar.
    // When any of these are active, Hexes and Sanguine Steel must not inject custom weapon/spell override
    // replacements, otherwise the weapon bar overwrites the form bar.
    static readonly HashSet<int> ActiveFormBuffs =
    [
        1205505492,   // AB_Shapeshift_Bat_TakeFlight_Buff
        -1569370346,  // AB_Shapeshift_Bear_Buff
        -858273386,   // AB_Shapeshift_Bear_Skin01_Buff
        -1447419822,  // AB_Shapeshift_DominatingPresence_PsychicForm_Buff
        914043867,    // AB_Shapeshift_Golem_T02_Buff
        -53860211,    // AB_Shapeshift_Human_Buff
        -434940480,   // AB_Shapeshift_Human_Grandma_Skin01_Buff
        -868350144,   // AB_Shapeshift_Human_PMK_Skin02_Buff
        902394170,    // AB_Shapeshift_Rat_Buff
        -2126626806,  // AB_Shapeshift_Rat_Burrow_Buff
        124832551,    // AB_Shapeshift_Spider_Buff
        -1665328650,  // AB_Shapeshift_Spider_Burrow_Buff
        -1038422434,  // AB_Shapeshift_Toad_Buff
        332075952,    // AB_Shapeshift_Toad_PMK_Skin01_Buff
        -351718282,   // AB_Shapeshift_Wolf_Buff
        -46579774,    // AB_Shapeshift_Wolf_Blackfang_Skin03_Buff
        -1687924191,  // AB_Shapeshift_Wolf_PMK_Skin02_Buff
        -1158884666,  // AB_Shapeshift_Wolf_Skin01_Buff
        -395216184,   // AB_Tailor_Shapeshift_Gargoyle_Buff
        1352541204,   // AB_Shapeshift_NormalForm_Buff
        361281067,    // Buff_Shapeshift_Base
        -222170350,   // AB_Shapeshift_CommandingPresence_Buff
        1199823151,   // AB_Shapeshift_BloodHunger_BloodSight_Buff
        -728707862,   // AB_Tailor_Shapeshift_Transition_Buff
        -1882904996,  // AB_Shapeshift_Wolf_Leap_Travel_Buff
        -1075909278   // AB_Shapeshift_Wolf_Leap_Travel_Blackfang_Buff
    ];

    internal static bool IsActiveForm(Entity character)
    {
        if (!character.ExistsSafe())
            return false;

        foreach (int buffHash in ActiveFormBuffs)
        {
            if (character.TryGetBuff(new PrefabGUID(buffHash), out _))
                return true;
        }

        return false;
    }

    internal static bool IsFormReplacementBuff(Entity buffEntity)
    {
        if (!buffEntity.ExistsSafe())
            return false;

        if (!buffEntity.TryRead<PrefabGUID>(out var prefabGuid))
            return false;

        return ActiveFormBuffs.Contains(prefabGuid.GuidHash);
    }

    internal static string DescribeActiveForm(Entity character)
    {
        if (!character.ExistsSafe())
            return "none";

        foreach (int buffHash in ActiveFormBuffs)
        {
            if (character.TryGetBuff(new PrefabGUID(buffHash), out _))
                return buffHash.ToString();
        }

        return "none";
    }
}
