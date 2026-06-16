using ProjectM;
using Stunlock.Core;
using Unity.Entities;

namespace HexesAndSanguineSteel;

internal readonly record struct WeaponScopeResult(
    string ScopeKey,
    int WeaponPrefabGuid,
    bool IsUnarmed,
    bool IsCustomWeapon,
    string Detail);

internal static class WeaponScopeResolver
{
    internal const string UnarmedScope = "Unarmed";

    static readonly Dictionary<int, string> WeaponTypeByItemPrefab = new()
    {
        [-2147445292] = "Axe", // Item_Weapon_Axe_Legendary_T06_Shattered
        [-2044057823] = "Axe", // Item_Weapon_Axe_T08_Sanguine
        [-1958888844] = "Axe", // Item_Weapon_Axe_T01_Bone
        [-1579575933] = "Axe", // Item_Weapon_Axe_T05_Iron
        [-1391446205] = "Axe", // Item_Weapon_Axe_T02_Bone_Reinforced
        [-1130238142] = "Axe", // Item_Weapon_Axe_T07_DarkSilver
        [-1024626758] = "Axe", // Item_Weapon_Axe_Legendary_T08_Trader_Template
        [-491969324] = "Axe", // Item_Weapon_Axe_T04_Copper_Reinforced
        [-102830349] = "Axe", // Item_Weapon_Axe_Legendary_T08
        [163122449] = "Axe", // Item_Weapon_Axe_Legendary_NameGenerator_T06
        [198951695] = "Axe", // Item_Weapon_Axe_T06_Iron_Reinforced
        [442700150] = "Axe", // Item_Weapon_Axe_Legendary_T08_Shattered
        [518802008] = "Axe", // Item_Weapon_Axe_T03_Copper
        [1239564213] = "Axe", // Item_Weapon_Axe_Unique_T08_Variation01
        [1259464735] = "Axe", // Item_Weapon_Axe_Legendary_T06
        [1541522788] = "Axe", // Item_Weapon_Axe_T02_WoodCutter
        [1570017693] = "Axe", // Item_Weapon_Axe_Legendary_NameGenerator_T08
        [2099198078] = "Axe", // Item_Weapon_Axe_Unique_T08_Variation01_Shattered
        [2100090213] = "Axe", // Item_Weapon_Axe_T09_ShadowMatter
        [-2060572315] = "Claws", // Item_Weapon_Claws_Legendary_T06
        [-1777908217] = "Claws", // Item_Weapon_Claws_T08_Sanguine
        [-1774269887] = "Claws", // Item_Weapon_Claws_Legendary_NameGenerator_T08
        [-1746159915] = "Claws", // Item_Weapon_Claws_Legendary_T06_Shattered
        [-1470260175] = "Claws", // Item_Weapon_Claws_T07_DarkSilver
        [-1333849822] = "Claws", // Item_Weapon_Claws_T05_Iron
        [-1024379681] = "Claws", // Item_Weapon_Claws_Unique_T08_Variation01_Shattered
        [-996999913] = "Claws", // Item_Weapon_Claws_Unique_T08_Variation01
        [-655493979] = "Claws", // Item_Weapon_Claws_Legendary_T08_Shattered
        [-444900575] = "Claws", // Item_Weapon_Claws_Legendary_NameGenerator_T06
        [-27238530] = "Claws", // Item_Weapon_Claws_Legendary_T08
        [1401940772] = "Claws", // Item_Weapon_Claws_Legendary_T08_Trader_Template
        [1748886117] = "Claws", // Item_Weapon_Claws_T06_Iron_Reinforced
        [-1636801169] = "Crossbow", // Item_Weapon_Crossbow_T04_Copper_Reinforced
        [-1401104184] = "Crossbow", // Item_Weapon_Crossbow_Unique_T08_Variation01
        [-1277074895] = "Crossbow", // Item_Weapon_Crossbow_T03_Copper
        [-814739263] = "Crossbow", // Item_Weapon_Crossbow_T07_DarkSilver
        [-517906196] = "Crossbow", // Item_Weapon_Crossbow_Legendary_T06
        [-20041991] = "Crossbow", // Item_Weapon_Crossbow_T01_Bone
        [517296275] = "Crossbow", // Item_Weapon_Crossbow_Legendary_T08_Trader_Template
        [572026243] = "Crossbow", // Item_Weapon_Crossbow_Legendary_T06_Shattered
        [781586362] = "Crossbow", // Item_Weapon_Crossbow_Unique_T08_Variation01_Shattered
        [836066667] = "Crossbow", // Item_Weapon_Crossbow_T05_Iron
        [898159697] = "Crossbow", // Item_Weapon_Crossbow_T02_Bone_Reinforced
        [935392085] = "Crossbow", // Item_Weapon_Crossbow_Legendary_T08
        [1221976097] = "Crossbow", // Item_Weapon_Crossbow_T06_Iron_Reinforced
        [1389040540] = "Crossbow", // Item_Weapon_Crossbow_T08_Sanguine
        [1716435762] = "Crossbow", // Item_Weapon_Crossbow_Legendary_NameGenerator_T08
        [1957540013] = "Crossbow", // Item_Weapon_Crossbow_T09_ShadowMatter
        [1958482379] = "Crossbow", // Item_Weapon_Crossbow_Legendary_NameGenerator_T06
        [2061238391] = "Crossbow", // Item_Weapon_Crossbow_Legendary_T08_Shattered
        [-2137269775] = "Daggers", // Item_Weapon_Daggers_Legendary_T08_Shattered
        [-1961050884] = "Daggers", // Item_Weapon_Daggers_T09_ShadowMatter
        [-1873605364] = "Daggers", // Item_Weapon_Daggers_Unique_T08_Variation01
        [-1566606969] = "Daggers", // Item_Weapon_Daggers_Legendary_NameGenerator_T08
        [-1276458869] = "Daggers", // Item_Weapon_Daggers_Legendary_T06
        [-1233207977] = "Daggers", // Item_Weapon_Daggers_Unique_T08_Variation01_Shattered
        [-1075670534] = "Daggers", // Item_Weapon_Daggers_Legendary_T06_Shattered
        [-816018167] = "Daggers", // Item_Weapon_Daggers_Legendary_NameGenerator_T06
        [-211034148] = "Daggers", // Item_Weapon_Daggers_T07_DarkSilver
        [140761255] = "Daggers", // Item_Weapon_Daggers_Legendary_T08
        [703783407] = "Daggers", // Item_Weapon_Daggers_T06_Iron_Reinforced
        [1031107636] = "Daggers", // Item_Weapon_Daggers_T08_Sanguine
        [1296724931] = "Daggers", // Item_Weapon_Daggers_T05_Iron
        [1719144622] = "Daggers", // Item_Weapon_Daggers_Legendary_T08_Trader_Template
        [-1766408331] = "FishingPole", // Item_Weapon_FishingPole_Debug
        [1302850112] = "FishingPole", // Item_Weapon_FishingPole_T01
        [-1743584975] = "GreatSword", // Item_Weapon_GreatSword_Legendary_T08_Trader_Template
        [-1638796801] = "GreatSword", // Item_Weapon_GreatSword_Legendary_T08_Shattered
        [-1173681254] = "GreatSword", // Item_Weapon_GreatSword_Legendary_T08
        [-768054337] = "GreatSword", // Item_Weapon_GreatSword_T05_Iron
        [-437176953] = "GreatSword", // Item_Weapon_GreatSword_Legendary_NameGenerator_T06
        [-256643998] = "GreatSword", // Item_Weapon_GreatSword_Legendary_NameGenerator_T08
        [82781195] = "GreatSword", // Item_Weapon_GreatSword_T06_Iron_Reinforced
        [147836723] = "GreatSword", // Item_Weapon_GreatSword_T08_Sanguine
        [674704033] = "GreatSword", // Item_Weapon_GreatSword_T07_DarkSilver
        [747911021] = "GreatSword", // Item_Weapon_GreatSword_Legendary_T06_Shattered
        [820408138] = "GreatSword", // Item_Weapon_GreatSword_Unique_T08_Variation01
        [869276797] = "GreatSword", // Item_Weapon_GreatSword_Legendary_T06
        [1272855317] = "GreatSword", // Item_Weapon_GreatSword_Unique_T08_Variation01_Shattered
        [1322254792] = "GreatSword", // Item_Weapon_GreatSword_T09_ShadowMatter
        [-1993708658] = "Longbow", // Item_Weapon_Longbow_T05_Iron
        [-1830162796] = "Longbow", // Item_Weapon_Longbow_T07_DarkSilver
        [-1003309553] = "Longbow", // Item_Weapon_Longbow_Legendary_T06
        [-726074700] = "Longbow", // Item_Weapon_Longbow_Legendary_NameGenerator_T06
        [-557203874] = "Longbow", // Item_Weapon_Longbow_Unique_T08_Variation01
        [19130904] = "Longbow", // Item_Weapon_Longbow_Legendary_T08_Trader_Template
        [285875674] = "Longbow", // Item_Weapon_Longbow_Legendary_T08_Shattered
        [288292636] = "Longbow", // Item_Weapon_Longbow_Legendary_NameGenerator_T08
        [352247730] = "Longbow", // Item_Weapon_Longbow_T04_Copper_Reinforced
        [532033005] = "Longbow", // Item_Weapon_Longbow_T03_Copper
        [649637190] = "Longbow", // Item_Weapon_Longbow_Legendary_T06_Shattered
        [1102277512] = "Longbow", // Item_Weapon_Longbow_Unique_T08_Variation01_Shattered
        [1177453385] = "Longbow", // Item_Weapon_Longbow_Legendary_T08
        [1283345494] = "Longbow", // Item_Weapon_Longbow_T09_ShadowMatter
        [1860352606] = "Longbow", // Item_Weapon_Longbow_T08_Sanguine
        [1951565953] = "Longbow", // Item_Weapon_Longbow_T06_Iron_Reinforced
        [-2048346225] = "Mace", // Item_Weapon_Mace_Legendary_NameGenerator_T08
        [-1998017941] = "Mace", // Item_Weapon_Mace_T02_Bone_Reinforced
        [-1845443712] = "Mace", // Item_Weapon_Mace_Legendary_T08_Trader_Template
        [-1810734832] = "Mace", // Item_Weapon_Mace_Legendary_T08_Shattered
        [-1714012261] = "Mace", // Item_Weapon_Mace_T05_Iron
        [-915028618] = "Mace", // Item_Weapon_Mace_Unique_T08_Variation01_Shattered
        [-687294429] = "Mace", // Item_Weapon_Mace_T02_Miners
        [-331345186] = "Mace", // Item_Weapon_Mace_T03_Copper
        [-276593802] = "Mace", // Item_Weapon_Mace_T06_Iron_Reinforced
        [-184713893] = "Mace", // Item_Weapon_Mace_T07_DarkSilver
        [-126076280] = "Mace", // Item_Weapon_Mace_T08_Sanguine
        [160471982] = "Mace", // Item_Weapon_Mace_T09_ShadowMatter
        [264593098] = "Mace", // Item_Weapon_Mace_Legendary_NameGenerator_T06
        [343324920] = "Mace", // Item_Weapon_Mace_T04_Copper_Reinforced
        [675187526] = "Mace", // Item_Weapon_Mace_Unique_T08_Variation01
        [1177597629] = "Mace", // Item_Weapon_Mace_Legendary_T06
        [1588258447] = "Mace", // Item_Weapon_Mace_T01_Bone
        [1963988265] = "Mace", // Item_Weapon_Mace_Legendary_T06_Shattered
        [1994084762] = "Mace", // Item_Weapon_Mace_Legendary_T08
        [-1843989041] = "Pistols", // Item_Weapon_Pistols_Legendary_NameGenerator_T08
        [-1502177717] = "Pistols", // Item_Weapon_Pistols_Legendary_T08_Trader_Template
        [-1265586439] = "Pistols", // Item_Weapon_Pistols_T09_ShadowMatter
        [-1038642372] = "Pistols", // Item_Weapon_Pistols_Legendary_T06_Shattered
        [-944318126] = "Pistols", // Item_Weapon_Pistols_Legendary_T08
        [14297698] = "Pistols", // Item_Weapon_Pistols_Legendary_T06
        [674407758] = "Pistols", // Item_Weapon_Pistols_T07_DarkSilver
        [769603740] = "Pistols", // Item_Weapon_Pistols_T05_Iron
        [1040125618] = "Pistols", // Item_Weapon_Pistols_Legendary_T08_Shattered
        [1071656850] = "Pistols", // Item_Weapon_Pistols_T08_Sanguine
        [1333624152] = "Pistols", // Item_Weapon_Pistols_Legendary_NameGenerator_T06
        [1630030026] = "Pistols", // Item_Weapon_Pistols_Unique_T08_Variation01_Shattered
        [1759077469] = "Pistols", // Item_Weapon_Pistols_Unique_T08_Variation01
        [1850870666] = "Pistols", // Item_Weapon_Pistols_T06_Iron_Reinforced
        [-2136716453] = "Reaper", // Item_Weapon_Reaper_Legendary_T08_Trader_Template
        [-2081286944] = "Reaper", // Item_Weapon_Reaper_T05_Iron
        [-2053917766] = "Reaper", // Item_Weapon_Reaper_T08_Sanguine
        [-922125625] = "Reaper", // Item_Weapon_Reaper_Legendary_T06
        [-859437190] = "Reaper", // Item_Weapon_Reaper_Unique_T08_Variation01
        [-576626587] = "Reaper", // Item_Weapon_Reaper_Legendary_NameGenerator_T06
        [-465491217] = "Reaper", // Item_Weapon_Reaper_T09_ShadowMatter
        [-413259500] = "Reaper", // Item_Weapon_Reaper_Legendary_T06_Shattered
        [-383870009] = "Reaper", // Item_Weapon_Reaper_Legendary_NameGenerator_T08
        [-152327780] = "Reaper", // Item_Weapon_Reaper_T01_Bone
        [-105026635] = "Reaper", // Item_Weapon_Reaper_Legendary_T08
        [6711686] = "Reaper", // Item_Weapon_Reaper_T07_DarkSilver
        [886814985] = "Reaper", // Item_Weapon_Reaper_Legendary_T08_Shattered
        [1048518929] = "Reaper", // Item_Weapon_Reaper_T04_Copper_Reinforced
        [1402953369] = "Reaper", // Item_Weapon_Reaper_T02_Bone_Reinforced
        [1522792650] = "Reaper", // Item_Weapon_Reaper_T03_Copper
        [1778128946] = "Reaper", // Item_Weapon_Reaper_T06_Iron_Reinforced
        [1801132968] = "Reaper", // Item_Weapon_Reaper_Unique_T08_Variation01_Shattered
        [1887724512] = "Reaper", // Item_Weapon_Reaper_T06_Iron_UndeadGeneral
        [-2068145306] = "Slashers", // Item_Weapon_Slashers_Unique_T08_Variation01
        [-1930402723] = "Slashers", // Item_Weapon_Slashers_Unique_T08_Variation02_Shattered
        [-1390536751] = "Slashers", // Item_Weapon_Slashers_Legendary_T08_Trader_Template
        [-1042299347] = "Slashers", // Item_Weapon_Slashers_T04_Copper_Reinforced
        [-588909332] = "Slashers", // Item_Weapon_Slashers_T01_Bone
        [-314614708] = "Slashers", // Item_Weapon_Slashers_T05_Iron
        [3759455] = "Slashers", // Item_Weapon_Slashers_Legendary_T06_Shattered
        [506082542] = "Slashers", // Item_Weapon_Slashers_T09_ShadowMatter
        [633666898] = "Slashers", // Item_Weapon_Slashers_T07_DarkSilver
        [658426701] = "Slashers", // Item_Weapon_Slashers_Legendary_NameGenerator_T06
        [810808231] = "Slashers", // Item_Weapon_Slashers_Legendary_NameGenerator_T08
        [821410795] = "Slashers", // Item_Weapon_Slashers_Legendary_T08
        [866934844] = "Slashers", // Item_Weapon_Slashers_T06_Iron_Reinforced
        [926722036] = "Slashers", // Item_Weapon_Slashers_T02_Bone_Reinforced
        [1271087499] = "Slashers", // Item_Weapon_Slashers_Legendary_T08_Shattered
        [1322545846] = "Slashers", // Item_Weapon_Slashers_T08_Sanguine
        [1499160417] = "Slashers", // Item_Weapon_Slashers_T03_Copper
        [1570363331] = "Slashers", // Item_Weapon_Slashers_Unique_T08_Variation02
        [1930526079] = "Slashers", // Item_Weapon_Slashers_Legendary_T06
        [1954207008] = "Slashers", // Item_Weapon_Slashers_Unique_T08_Variation01_Shattered
        [-1931117134] = "Spear", // Item_Weapon_Spear_Legendary_T08
        [-1854790299] = "Spear", // Item_Weapon_Spear_Legendary_NameGenerator_T06
        [-1674680373] = "Spear", // Item_Weapon_Spear_Unique_T08_Variation01
        [-958110636] = "Spear", // Item_Weapon_Spear_Legendary_T08_Trader_Template
        [-850142339] = "Spear", // Item_Weapon_Spear_T08_Sanguine
        [-352704566] = "Spear", // Item_Weapon_Spear_T07_DarkSilver
        [124616797] = "Spear", // Item_Weapon_Spear_Unique_T08_Variation01_Shattered
        [790210443] = "Spear", // Item_Weapon_Spear_T04_Copper_Reinforced
        [912809090] = "Spear", // Item_Weapon_Spear_Legendary_NameGenerator_T08
        [1065194820] = "Spear", // Item_Weapon_Spear_T06_Iron_Reinforced
        [1244180446] = "Spear", // Item_Weapon_Spear_T02_Bone_Reinforced
        [1307774440] = "Spear", // Item_Weapon_Spear_T09_ShadowMatter
        [1370755976] = "Spear", // Item_Weapon_Spear_T03_Copper
        [1717016192] = "Spear", // Item_Weapon_Spear_Legendary_T08_Shattered
        [1853029976] = "Spear", // Item_Weapon_Spear_T05_Iron
        [2001389164] = "Spear", // Item_Weapon_Spear_Legendary_T06
        [2038011836] = "Spear", // Item_Weapon_Spear_T01_Bone
        [2142983740] = "Spear", // Item_Weapon_Spear_Legendary_T06_Shattered
        [-2085919458] = "Sword", // Item_Weapon_Sword_T01_Bone
        [-2037272000] = "Sword", // Item_Weapon_Sword_T03_Copper
        [-1455388114] = "Sword", // Item_Weapon_Sword_T07_DarkSilver
        [-1421775051] = "Sword", // Item_Weapon_Sword_Legendary_T06_Shattered
        [-1219959051] = "Sword", // Item_Weapon_Sword_T04_Copper_Reinforced
        [-1215982687] = "Sword", // Item_Weapon_Sword_T09_ShadowMatter
        [-903587404] = "Sword", // Item_Weapon_Sword_T05_Iron
        [-830893351] = "Sword", // Item_Weapon_Sword_Legendary_T08_Trader_Template
        [-796306296] = "Sword", // Item_Weapon_Sword_T02_Bone_Reinforced
        [-774462329] = "Sword", // Item_Weapon_Sword_T08_Sanguine
        [-435501075] = "Sword", // Item_Weapon_Sword_T06_Iron_Reinforced
        [195858450] = "Sword", // Item_Weapon_Sword_Legendary_T08
        [220001518] = "Sword", // Item_Weapon_Sword_Unique_T08_Variation01_Shattered
        [1048769481] = "Sword", // Item_Weapon_Sword_Legendary_T08_Shattered
        [1564801426] = "Sword", // Item_Weapon_Sword_Legendary_NameGenerator_T06
        [1637216050] = "Sword", // Item_Weapon_Sword_Legendary_T06
        [1908755405] = "Sword", // Item_Weapon_Sword_Legendary_NameGenerator_T08
        [2106567892] = "Sword", // Item_Weapon_Sword_Unique_T08_Variation01
        [-1651990235] = "TwinBlades", // Item_Weapon_TwinBlades_T06_Iron_Reinforced
        [-1634108038] = "TwinBlades", // Item_Weapon_TwinBlades_Legendary_T06
        [-1595292245] = "TwinBlades", // Item_Weapon_TwinBlades_T07_DarkSilver
        [-1397287045] = "TwinBlades", // Item_Weapon_TwinBlades_Unique_T08_Variation01_Shattered
        [-1122389049] = "TwinBlades", // Item_Weapon_TwinBlades_T05_Iron
        [-699863795] = "TwinBlades", // Item_Weapon_TwinBlades_T09_ShadowMatter
        [-297349982] = "TwinBlades", // Item_Weapon_TwinBlades_T08_Sanguine
        [-228881628] = "TwinBlades", // Item_Weapon_TwinBlades_Legendary_T08_Trader_Template
        [152014105] = "TwinBlades", // Item_Weapon_TwinBlades_Legendary_T08
        [601169005] = "TwinBlades", // Item_Weapon_TwinBlades_Unique_T08_Variation01
        [1479621167] = "TwinBlades", // Item_Weapon_TwinBlades_Legendary_T08_Shattered
        [1579758125] = "TwinBlades", // Item_Weapon_TwinBlades_Legendary_T06_Shattered
        [1835208468] = "TwinBlades", // Item_Weapon_TwinBlades_Legendary_NameGenerator_T06
        [2023500574] = "TwinBlades", // Item_Weapon_TwinBlades_Legendary_NameGenerator_T08
        [-1222824286] = "Whip", // Item_Weapon_Whip_Legendary_T06_Shattered
        [-960205578] = "Whip", // Item_Weapon_Whip_T07_DarkSilver
        [-882837429] = "Whip", // Item_Weapon_Whip_Legendary_NameGenerator_T06
        [-847062445] = "Whip", // Item_Weapon_Whip_T05_Iron
        [-671246832] = "Whip", // Item_Weapon_Whip_Unique_T08_Variation01
        [-655095317] = "Whip", // Item_Weapon_Whip_T08_Sanguine
        [429323760] = "Whip", // Item_Weapon_Whip_Legendary_T08
        [567413754] = "Whip", // Item_Weapon_Whip_T09_ShadowMatter
        [950358400] = "Whip", // Item_Weapon_Whip_Unique_T08_Variation01_Shattered
        [1340494453] = "Whip", // Item_Weapon_Whip_Legendary_T08_Trader_Template
        [1393113320] = "Whip", // Item_Weapon_Whip_T06_Iron_Reinforced
        [1490846791] = "Whip", // Item_Weapon_Whip_Legendary_T08_Shattered
        [1705984031] = "Whip", // Item_Weapon_Whip_Legendary_T06
        [1838862498] = "Whip", // Item_Weapon_Whip_Legendary_NameGenerator_T08
    };


    static readonly HashSet<int> ShadowMatterWeaponPrefabs = new()
    {
        2100090213,   // Item_Weapon_Axe_T09_ShadowMatter
        1957540013,   // Item_Weapon_Crossbow_T09_ShadowMatter
        -1961050884,  // Item_Weapon_Daggers_T09_ShadowMatter
        1322254792,   // Item_Weapon_GreatSword_T09_ShadowMatter
        1283345494,   // Item_Weapon_Longbow_T09_ShadowMatter
        160471982,    // Item_Weapon_Mace_T09_ShadowMatter
        -1265586439,  // Item_Weapon_Pistols_T09_ShadowMatter
        -465491217,   // Item_Weapon_Reaper_T09_ShadowMatter
        506082542,    // Item_Weapon_Slashers_T09_ShadowMatter
        1307774440,   // Item_Weapon_Spear_T09_ShadowMatter
        -1215982687,  // Item_Weapon_Sword_T09_ShadowMatter
        -699863795,   // Item_Weapon_TwinBlades_T09_ShadowMatter
        567413754,    // Item_Weapon_Whip_T09_ShadowMatter
    };

    internal static bool IsShadowMatterWeaponPrefab(int itemWeaponGuidHash)
        => ShadowMatterWeaponPrefabs.Contains(itemWeaponGuidHash);

    internal static bool TryGetWeaponTypeForPrefab(int itemWeaponGuidHash, out string weaponType)
        => WeaponTypeByItemPrefab.TryGetValue(itemWeaponGuidHash, out weaponType!);

    internal static WeaponScopeResult GetCurrentScope(Entity character)
    {
        if (!character.ExistsSafe() || !character.TryRead<Equipment>(out var equipment))
            return new(UnarmedScope, 0, true, false, "No valid character/equipment component.");

        Entity weaponEntity = equipment.WeaponSlot.SlotEntity._Entity;

        if (!weaponEntity.ExistsSafe())
            return new(UnarmedScope, 0, true, false, "No weapon entity equipped.");

        if (!weaponEntity.TryRead<PrefabGUID>(out var prefabGuid) || !prefabGuid.HasValue())
            return new(UnarmedScope, 0, true, false, "Equipped weapon has no PrefabGUID.");

        int weaponHash = prefabGuid.GuidHash;

        if (CustomWeaponInstanceStore.TryGetHeldInstanceWeapon(character, out var instanceWeapon, out var instance))
        {
            return new(
                $"CustomWeaponInstance:{instance.SequenceGuidHash}",
                weaponHash,
                false,
                true,
                $"{instanceWeapon.Name} instance:{instance.SequenceGuidHash}"
            );
        }

        if (CustomWeaponRegistry.IsCustomWeaponItem(weaponHash))
        {
            string name = CustomWeaponRegistry.GetCustomWeaponName(weaponHash);
            return new($"CustomWeapon:{weaponHash}", weaponHash, false, true, name);
        }

        if (WeaponTypeByItemPrefab.TryGetValue(weaponHash, out string? weaponType))
            return new(weaponType, weaponHash, false, false, weaponType);

        // Unknown weapon item. Keep the override scoped to this exact item instead of applying globally.
        return new($"Item:{weaponHash}", weaponHash, false, false, $"Unknown weapon item {weaponHash}");
    }
}
