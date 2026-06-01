using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppSystemType = Il2CppSystem.Type;
using ProjectM;
using Stunlock.Core;
using Unity.Entities;

namespace PerkShop.Services;

internal static class CarrierPrefabService
{
    private const string ScriptSpawnTypeName = "ProjectM.Scripting.ScriptSpawn";
    private static int _preparedCarrierGuid;
    private static Type? _scriptSpawnManagedType;
    private static Il2CppSystemType? _scriptSpawnIl2CppType;

    internal static void Reset()
    {
        _preparedCarrierGuid = 0;
    }

    internal static bool PrepareConfiguredStatCarrierPrefab()
    {
        try
        {
            if (!StatService.IsCarrierConfigured(out PrefabGUID carrier, out var error))
            {
                Core.Log.LogWarning($"[CarrierPrefabService] {error}");
                return false;
            }

            if (_preparedCarrierGuid == carrier.GuidHash)
                return true;

            var prefabCollectionSystem = Core.Systems.PrefabCollectionSystem;
            if (!prefabCollectionSystem._PrefabGuidToEntityMap.TryGetValue(carrier, out Entity prefabEntity))
            {
                Core.Log.LogWarning($"[CarrierPrefabService] Could not find stat carrier prefab entity for {carrier.GuidHash}. Stats will not spawn correctly.");
                return false;
            }

            var em = Core.EntityManager;

            // Some V Rising interop builds do not expose ScriptSpawn as a compile-time type.
            // Resolve the managed wrapper by name, convert it to Il2CppSystem.Type through
            // Il2CppType.Of<T>() invoked via reflection, then build ComponentType from that.
            // This avoids the CS1503 System.Type -> Il2CppSystem.Type compile error while still
            // preparing the carrier for ScriptSpawnServer when the runtime type exists.
            if (!TryEnsureScriptSpawn(em, prefabEntity))
            {
                Core.Log.LogWarning("[CarrierPrefabService] Could not resolve/add ScriptSpawn dynamically. Immediate stat-buffer fallback will still be attempted, but HUD/stat sync may be less reliable.");
            }

            // Ensure the carrier prefab has the stat buffer shape expected by the spawned buff.
            if (!em.HasBuffer<ModifyUnitStatBuff_DOTS>(prefabEntity))
                em.AddBuffer<ModifyUnitStatBuff_DOTS>(prefabEntity);
            else
                em.GetBuffer<ModifyUnitStatBuff_DOTS>(prefabEntity).Clear();

            _preparedCarrierGuid = carrier.GuidHash;
            Core.LogDebugIfEnabled($"[CarrierPrefabService] Prepared stat carrier prefab {carrier.GuidHash} with dynamic ScriptSpawn + ModifyUnitStatBuff_DOTS.");
            return true;
        }
        catch (Exception e)
        {
            Core.LogException(e);
            return false;
        }
    }

    private static bool TryEnsureScriptSpawn(EntityManager em, Entity prefabEntity)
    {
        var il2CppType = ResolveScriptSpawnIl2CppType();
        if (il2CppType == null)
            return false;

        ComponentType componentType = new(il2CppType);

        if (!em.HasComponent(prefabEntity, componentType))
            em.AddComponent(prefabEntity, componentType);

        return true;
    }

    private static Il2CppSystemType? ResolveScriptSpawnIl2CppType()
    {
        if (_scriptSpawnIl2CppType != null)
            return _scriptSpawnIl2CppType;

        _scriptSpawnManagedType ??=
            AccessTools.TypeByName(ScriptSpawnTypeName) ??
            AccessTools.TypeByName("ProjectM.Gameplay.Scripting.ScriptSpawn") ??
            AccessTools.TypeByName("ProjectM.ScriptSpawn") ??
            AccessTools.TypeByName("ScriptSpawn");

        if (_scriptSpawnManagedType == null)
            return null;

        MethodInfo? ofMethod = typeof(Il2CppType)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(method =>
                method.Name == nameof(Il2CppType.Of) &&
                method.IsGenericMethodDefinition &&
                method.GetParameters().Length == 0);

        if (ofMethod == null)
            return null;

        object? result = ofMethod.MakeGenericMethod(_scriptSpawnManagedType).Invoke(null, null);
        _scriptSpawnIl2CppType = result as Il2CppSystemType;
        return _scriptSpawnIl2CppType;
    }
}
