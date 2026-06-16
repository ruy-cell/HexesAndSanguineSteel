using HarmonyLib;
using ProjectM;
using ProjectM.Network;
using Stunlock.Network;
using Unity.Entities;

namespace HexesAndSanguineSteel.Patches;

[HarmonyPatch(typeof(ServerBootstrapSystem), nameof(ServerBootstrapSystem.OnUserConnected))]
internal static class ServerBootstrapSystemPatch
{
    [HarmonyPostfix]
    static void OnUserConnectedPostfix(ServerBootstrapSystem __instance, NetConnectionId netConnectionId)
    {
        try
        {
            if (!__instance._NetEndPointToApprovedUserIndex.TryGetValue(netConnectionId, out int userIndex))
                return;

            ServerBootstrapSystem.ServerClient serverClient = __instance._ApprovedUsersLookup[userIndex];

            Entity userEntity = serverClient.UserEntity;
            if (!userEntity.ExistsSafe() || !Core.EntityManager.HasComponent<User>(userEntity))
                return;

            User user = Core.EntityManager.GetComponentData<User>(userEntity);
            Entity character = user.LocalCharacter.GetEntityOnServer();

            if (!character.ExistsSafe())
                return;

            if (PlayerSpellOverrideService.TryApplyByPlatformId(user.PlatformId, character, out string message))
                RuntimeOptimization.Debug($"Applied login spell/custom weapon override for {user.CharacterName.Value}: {message}");

            if (CustomWeaponRuntimeOverrideService.TryApplyForCharacter(character, out string weaponMessage))
                RuntimeOptimization.Debug($"Applied login runtime custom weapon override for {user.CharacterName.Value}: {weaponMessage}");
        }
        catch (Exception ex)
        {
            Plugin.LogInstance.LogWarning($"Failed applying login spell override: {ex}");
        }
    }
}
