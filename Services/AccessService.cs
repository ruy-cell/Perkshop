using System;
using System.Linq;
using ProjectM.Network;
using Unity.Entities;

namespace PerkShop.Services;

internal static class AccessService
{
    public static bool CanAccessPerkShop(Entity userEntity, Action<string> reply)
    {
        var config = ConfigService.Shop;
        if (!config.EnableBuffWhitelist)
            return true;

        if (!TryGetPlatformId(userEntity, out var platformId))
        {
            reply("User component not ready.");
            return false;
        }

        if (config.BuffWhitelistPlatformIds.Contains(platformId))
            return true;

        reply("You are not whitelisted for the perk shop.");
        return false;
    }

    public static bool CanAccessStatShop(Entity userEntity, Action<string> reply)
    {
        var config = ConfigService.Shop;
        if (!config.EnableStatWhitelist)
            return true;

        if (!TryGetPlatformId(userEntity, out var platformId))
        {
            reply("User component not ready.");
            return false;
        }

        if (config.StatWhitelistPlatformIds.Contains(platformId))
            return true;

        reply("You are not whitelisted for the stat shop.");
        return false;
    }

    private static bool TryGetPlatformId(Entity userEntity, out ulong platformId)
    {
        platformId = 0;
        if (userEntity == Entity.Null || !Core.EntityManager.Exists(userEntity) || !Core.EntityManager.HasComponent<User>(userEntity))
            return false;

        var user = Core.EntityManager.GetComponentData<User>(userEntity);
        platformId = user.PlatformId;
        return platformId != 0;
    }
}
