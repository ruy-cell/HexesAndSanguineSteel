using BepInEx.Logging;
using HarmonyLib;
using ProjectM.Gameplay.WarEvents;

namespace HexesAndSanguineSteel;

[HarmonyPatch(typeof(WarEventRegistrySystem), nameof(WarEventRegistrySystem.RegisterWarEventEntities))]
internal static class Bootstrap
{
    static ManualLogSource? _log;
    static bool _initialized;

    internal static void Initialize(Harmony harmony, ManualLogSource log)
    {
        _log = log;
        harmony.CreateClassProcessor(typeof(Bootstrap)).Patch();
    }

    [HarmonyPostfix]
    static void Postfix()
    {
        if (_initialized)
            return;

        _initialized = true;

        try
        {
            CustomWeaponRegistry.ApplyAll();
            RuntimeOptimization.InfoOnce("bootstrap-prefab-mutations", "Hexes and Sanguine Steel bootstrap completed; runtime systems registered.");
        }
        catch (Exception ex)
        {
            _log?.LogError($"Custom weapon bootstrap failed: {ex}");
        }
    }
}
