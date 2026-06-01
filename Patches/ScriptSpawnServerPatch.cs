using System;
using System.Reflection;
using HarmonyLib;
using PerkShop.Services;
using ProjectM;
using ProjectM.Network;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;

namespace PerkShop.Patches;

[HarmonyPatch]
internal static class ScriptSpawnServerPatch
{
    private static MethodBase TargetMethod()
    {
        Type? type =
            AccessTools.TypeByName("ScriptSpawnServer") ??
            AccessTools.TypeByName("ProjectM.ScriptSpawnServer") ??
            AccessTools.TypeByName("ProjectM.Shared.Systems.ScriptSpawnServer");

        if (type == null)
            throw new MissingMethodException("Unable to find ScriptSpawnServer type.");

        MethodInfo? method = AccessTools.Method(type, "OnUpdate");
        if (method == null)
            throw new MissingMethodException(type.FullName, "OnUpdate");

        return method;
    }

    [HarmonyPrefix]
    private static void OnUpdatePrefix(object __instance)
    {
        try
        {
            if (!Plugin.HasLoaded())
                return;

            if (!StatService.IsCarrierConfigured(out var carrier, out _))
                return;

            // Use ScriptSpawnServer's own query. Per the generated query dump this is the
            // PrefabGUID + ScriptSpawn + SpawnTag query, which is the same spawn window
            // Bloodcraft uses for its bonus stat carrier.
            EntityQuery query = QueryService.GetScriptSpawnServerQuery(__instance);
            NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);

            try
            {
                var em = Core.EntityManager;

                for (int i = 0; i < entities.Length; i++)
                {
                    Entity buffEntity = entities[i];

                    if (buffEntity == Entity.Null || !em.Exists(buffEntity))
                        continue;

                    if (!em.HasComponent<PrefabGUID>(buffEntity))
                        continue;

                    PrefabGUID prefabGuid = em.GetComponentData<PrefabGUID>(buffEntity);
                    if (!prefabGuid.Equals(carrier))
                        continue;

                    if (!em.HasComponent<Buff>(buffEntity))
                        continue;

                    Buff buff = em.GetComponentData<Buff>(buffEntity);
                    Entity target = buff.Target;

                    if (target == Entity.Null || !em.Exists(target))
                        continue;

                    if (!em.HasComponent<PlayerCharacter>(target))
                        continue;

                    PlayerCharacter playerCharacter = em.GetComponentData<PlayerCharacter>(target);
                    Entity userEntity = playerCharacter.UserEntity;

                    if (userEntity == Entity.Null || !em.Exists(userEntity) || !em.HasComponent<User>(userEntity))
                        continue;

                    if (StatService.TryPopulateOwnedStatBuffer(buffEntity, target, userEntity))
                    {
                        var user = em.GetComponentData<User>(userEntity);
                        Core.LogDebugIfEnabled($"[ScriptSpawnServerPatch] Populated PerkShop stat carrier entity:{buffEntity.Index}:{buffEntity.Version} player:{user.CharacterName} platformId:{user.PlatformId}");
                    }
                }
            }
            finally
            {
                if (entities.IsCreated)
                    entities.Dispose();
            }
        }
        catch (Exception e)
        {
            Core.LogException(e);
        }
    }
}
