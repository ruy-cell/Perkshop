using PerkShop.Services;
using HarmonyLib;
using ProjectM;
using ProjectM.Network;
using Stunlock.Network;
using Unity.Entities;

namespace PerkShop.Patches;

[HarmonyPatch]
internal static class UserConnectionPatch
{
    [HarmonyPatch(typeof(ServerBootstrapSystem), nameof(ServerBootstrapSystem.OnUserConnected))]
    [HarmonyPostfix]
    private static void OnUserConnectedPostfix(ServerBootstrapSystem __instance, NetConnectionId netConnectionId)
    {
        if (!__instance._NetEndPointToApprovedUserIndex.TryGetValue(netConnectionId, out int userIndex)) return;

        var serverClient = __instance._ApprovedUsersLookup[userIndex];
        Entity userEntity = serverClient.UserEntity;
        if (userEntity == Entity.Null || !__instance.EntityManager.Exists(userEntity)) return;
        if (!__instance.EntityManager.HasComponent<User>(userEntity)) return;

        var user = __instance.EntityManager.GetComponentData<User>(userEntity);
        OwnershipService.RegisterOnlineUser(user.PlatformId, userEntity);
        PlayerCacheService.Remember(user);

        var config = ConfigService.Shop;
        if (config.SaveOwnership && Plugin.HasLoaded())
        {
            Core.InitializeAfterLoaded();
            // Perks are intentionally not auto-reapplied on login.
            // Players must use .perk sync to restore owned perks for the current session.
        }
    }

    [HarmonyPatch(typeof(ServerBootstrapSystem), nameof(ServerBootstrapSystem.OnUserDisconnected))]
    [HarmonyPrefix]
    private static void OnUserDisconnectedPrefix(ServerBootstrapSystem __instance, NetConnectionId netConnectionId)
    {
        if (!__instance._NetEndPointToApprovedUserIndex.TryGetValue(netConnectionId, out int userIndex)) return;

        var serverClient = __instance._ApprovedUsersLookup[userIndex];
        Entity userEntity = serverClient.UserEntity;
        if (userEntity == Entity.Null || !__instance.EntityManager.Exists(userEntity)) return;
        if (!__instance.EntityManager.HasComponent<User>(userEntity)) return;

        var user = __instance.EntityManager.GetComponentData<User>(userEntity);
        OwnershipService.UnregisterOnlineUser(user.PlatformId);
    }
}
