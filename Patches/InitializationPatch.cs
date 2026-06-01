using PerkShop.Services;
using HarmonyLib;
using ProjectM;
using UnityEngine;

namespace PerkShop.Patches;

[HarmonyPatch(typeof(ServerBootstrapSystem), nameof(ServerBootstrapSystem.OnUpdate))]
internal static class InitializationPatch
{
    private static bool _initialized;
    private static float _nextReapplyCheck;
    private static float _nextCarrierFinalizeCheck;

    [HarmonyPostfix]
    private static void OneShot_AfterServerBootstrap()
    {
        if (!_initialized)
        {
            if (!Plugin.HasLoaded()) return;
            _initialized = true;
            Core.InitializeAfterLoaded();
            OwnershipService.Initialize();
            PlayerCacheService.Initialize();
        }

        var config = ConfigService.Shop;

        if (Time.time >= _nextCarrierFinalizeCheck)
        {
            _nextCarrierFinalizeCheck = Time.time + config.CarrierFinalizeCheckIntervalSeconds;
            StatService.ProcessPendingPermanentCarriers();
            BuffService.ProcessPendingPermanentBuffs();
            OwnershipService.FlushPendingSaves();
            PlayerCacheService.FlushPendingSaves();
        }
        // Perks are intentionally not auto-reapplied while online.
        // Players must use .perk sync to restore owned perks for the current session.
    }
}
