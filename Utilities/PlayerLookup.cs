using ProjectM.Network;
using Unity.Collections;
using Unity.Entities;

namespace HexesAndSanguineSteel;

internal readonly record struct PlayerLookupResult(ulong PlatformId, string Name, Entity UserEntity, Entity CharacterEntity, User User);

internal static class PlayerLookup
{
    internal static bool TryFindOnlinePlayer(string nameOrPlatformId, out PlayerLookupResult result)
    {
        result = default;

        if (string.IsNullOrWhiteSpace(nameOrPlatformId))
            return false;

        ulong requestedPlatformId = 0;
        bool searchByPlatformId = ulong.TryParse(nameOrPlatformId, out requestedPlatformId);

        var query = Core.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<User>());
        var users = query.ToEntityArray(Allocator.Temp);

        try
        {
            foreach (var userEntity in users)
            {
                if (!userEntity.ExistsSafe() || !Core.EntityManager.HasComponent<User>(userEntity))
                    continue;

                var user = Core.EntityManager.GetComponentData<User>(userEntity);
                ulong platformId = user.PlatformId;
                string characterName = user.CharacterName.Value;

                bool matches = searchByPlatformId
                    ? platformId == requestedPlatformId
                    : string.Equals(characterName, nameOrPlatformId, StringComparison.OrdinalIgnoreCase);

                if (!matches)
                    continue;

                Entity character = user.LocalCharacter.GetEntityOnServer();
                result = new PlayerLookupResult(platformId, characterName, userEntity, character, user);
                return character.ExistsSafe();
            }
        }
        finally
        {
            users.Dispose();
            query.Dispose();
        }

        return false;
    }


    internal static bool TryFindOnlinePlayer(Entity characterEntity, out PlayerLookupResult result)
    {
        result = default;

        if (!characterEntity.ExistsSafe())
            return false;

        foreach (var player in GetOnlinePlayers())
        {
            if (player.CharacterEntity.Equals(characterEntity))
            {
                result = player;
                return true;
            }
        }

        return false;
    }

    internal static List<PlayerLookupResult> GetOnlinePlayers()
    {
        var results = new List<PlayerLookupResult>();
        var query = Core.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<User>());
        var users = query.ToEntityArray(Allocator.Temp);

        try
        {
            foreach (var userEntity in users)
            {
                if (!userEntity.ExistsSafe() || !Core.EntityManager.HasComponent<User>(userEntity))
                    continue;

                var user = Core.EntityManager.GetComponentData<User>(userEntity);
                Entity character = user.LocalCharacter.GetEntityOnServer();

                if (character.ExistsSafe())
                    results.Add(new PlayerLookupResult(user.PlatformId, user.CharacterName.Value, userEntity, character, user));
            }
        }
        finally
        {
            users.Dispose();
            query.Dispose();
        }

        return results;
    }
}
