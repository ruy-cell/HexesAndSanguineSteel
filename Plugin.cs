using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;
using VampireCommandFramework;

namespace HexesAndSanguineSteel;

[BepInPlugin(PluginGuid, PluginName, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency("gg.deca.VampireCommandFramework")]
internal sealed class Plugin : BasePlugin
{
    internal const string PluginGuid = "com.hexesandsanguinesteel.server";
    internal const string PluginName = "Hexes and Sanguine Steel";

    internal static ManualLogSource LogInstance = null!;
    internal static Harmony Harmony = null!;

    public override void Load()
    {
        LogInstance = Log;

        if (Application.productName != "VRisingServer")
        {
            Log.LogWarning("Hexes and Sanguine Steel is server-side only; skipping load on non-server process.");
            return;
        }

        Harmony = new Harmony(PluginGuid);
        Bootstrap.Initialize(Harmony, Log);
        Harmony.PatchAll();
        PlayerSpellOverrideService.Initialize();
        CustomWeaponInstanceStore.Initialize();
        CommandRegistry.RegisterAll();

        Log.LogInfo($"Loaded {PluginName} {MyPluginInfo.PLUGIN_VERSION}; waiting for server bootstrap.");
    }

    public override bool Unload()
    {
        Harmony?.UnpatchSelf();
        CommandRegistry.UnregisterAssembly();
        return true;
    }
}
