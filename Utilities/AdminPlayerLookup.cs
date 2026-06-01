using System;
using System.Collections.Generic;
using System.Linq;
using PerkShop.Services;
using ProjectM.Network;
using Unity.Collections;
using Unity.Entities;

namespace PerkShop.Utilities;

internal static class AdminPlayerLookup
{
    public static bool TryFindUser(string playerRef, out Entity userEntity, out User user, out string error)
    {
        userEntity = Entity.Null;
        user = default;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(playerRef))
        {
            error = "Usage: specify an online player name or PlatformId.";
            return false;
        }

        var em = Core.EntityManager;
        var query = em.CreateEntityQuery(ComponentType.ReadOnly<User>());
        var entities = query.ToEntityArray(Allocator.Temp);

        try
        {
            if (ulong.TryParse(playerRef.Trim(), out var platformId))
            {
                foreach (var entity in entities)
                {
                    var current = em.GetComponentData<User>(entity);
                    if (current.PlatformId == platformId)
                    {
                        userEntity = entity;
                        user = current;
                        PlayerCacheService.Remember(current);
                        return true;
                    }
                }

                error = $"No online player found with PlatformId {platformId}.";
                return false;
            }

            string needle = playerRef.Trim();
            var exact = new List<(Entity entity, User user)>();
            var partial = new List<(Entity entity, User user)>();

            foreach (var entity in entities)
            {
                var current = em.GetComponentData<User>(entity);
                string characterName = current.CharacterName.ToString();

                if (string.Equals(characterName, needle, StringComparison.OrdinalIgnoreCase))
                    exact.Add((entity, current));
                else if (characterName.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                    partial.Add((entity, current));
            }

            var matches = exact.Count > 0 ? exact : partial;

            if (matches.Count == 0)
            {
                error = $"No online player found matching '{needle}'.";
                return false;
            }

            if (matches.Count > 1)
            {
                error = "Multiple players match that query: " + string.Join(", ", matches.Select(x => x.user.CharacterName.ToString()));
                return false;
            }

            userEntity = matches[0].entity;
            user = matches[0].user;
            PlayerCacheService.Remember(user);
            return true;
        }
        finally
        {
            if (entities.IsCreated)
                entities.Dispose();
        }
    }
}
