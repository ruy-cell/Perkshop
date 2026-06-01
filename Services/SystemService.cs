using ProjectM;
using ProjectM.Gameplay.Systems;
using ProjectM.Scripting;
using ProjectM.Shared;
using Unity.Entities;

namespace PerkShop.Services;

internal sealed class SystemService
{
    internal World ServerWorld { get; }
    internal ServerScriptMapper ServerScriptMapper { get; }
    internal DebugEventsSystem DebugEventsSystem { get; }
    internal PrefabCollectionSystem PrefabCollectionSystem { get; }

    internal SystemService(World serverWorld)
    {
        ServerWorld = serverWorld;
        ServerScriptMapper = serverWorld.GetExistingSystemManaged<ServerScriptMapper>();
        DebugEventsSystem = serverWorld.GetExistingSystemManaged<DebugEventsSystem>();
        PrefabCollectionSystem = serverWorld.GetExistingSystemManaged<PrefabCollectionSystem>();
    }
}
