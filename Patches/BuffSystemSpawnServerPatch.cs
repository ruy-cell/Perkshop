using System;
using System.Reflection;
using HarmonyLib;
using PerkShop.Services;
using Unity.Entities;

namespace PerkShop.Patches;

[HarmonyPatch]
internal static class BuffSystemSpawnServerPatch
{
    private static MethodBase TargetMethod()
    {
        Type? type =
            AccessTools.TypeByName("BuffSystem_Spawn_Server") ??
            AccessTools.TypeByName("ProjectM.BuffSystem_Spawn_Server");

        if (type == null)
            throw new MissingMethodException("Unable to find BuffSystem_Spawn_Server type.");

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

            if (BuffService.PendingPermanentBuffCount == 0)
                return;

            EntityQuery query = QueryService.GetBuffSystemSpawnServerQuery(__instance);
            BuffService.ProcessPendingPermanentBuffsDuringSpawn(query);
        }
        catch (Exception e)
        {
            Core.LogException(e);
        }
    }
}
