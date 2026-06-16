using ProjectM;
using ProjectM.Scripting;
using Unity.Entities;

namespace HexesAndSanguineSteel;

internal static class Core
{
    static World? _server;

    internal static World Server
    {
        get
        {
            if (_server?.IsCreated == true)
                return _server;

            _server = WorldUtility.FindServerWorld();

            if (_server?.IsCreated != true)
                throw new InvalidOperationException("Server world is not ready.");

            return _server;
        }
    }

    internal static EntityManager EntityManager => Server.EntityManager;

    internal static ServerGameManager ServerGameManager
        => Server.GetExistingSystemManaged<ServerScriptMapper>().GetServerGameManager();
}
