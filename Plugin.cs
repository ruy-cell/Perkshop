using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;
using VampireCommandFramework;
using PerkShop.Services;

namespace PerkShop;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency("gg.deca.VampireCommandFramework")]
public sealed class Plugin : BasePlugin
{
    internal static ManualLogSource PluginLog { get; private set; } = null!;
    internal static Harmony Harmony { get; private set; } = null!;

    public override void Load()
    {
        if (Application.productName != "VRisingServer") return;

        PluginLog = Log;
        ConfigService.Initialize();
        OwnershipService.Initialize();
        PlayerCacheService.Initialize();

        Harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        Harmony.PatchAll(Assembly.GetExecutingAssembly());

        CommandRegistry.RegisterAll();
        Log.LogInfo($"{MyPluginInfo.PLUGIN_NAME} {MyPluginInfo.PLUGIN_VERSION} loaded.");
    }

    public override bool Unload()
    {
        OwnershipService.FlushPendingSaves(force: true);
        PlayerCacheService.FlushPendingSaves(force: true);
        CommandRegistry.UnregisterAssembly();
        Harmony?.UnpatchSelf();
        return true;
    }

    internal static bool HasLoaded()
    {
        var server = Core.GetWorld("Server");
        if (server == null) return false;
        var prefabCollection = server.GetExistingSystemManaged<ProjectM.PrefabCollectionSystem>();
        return prefabCollection?.SpawnableNameToPrefabGuidDictionary.Count > 0;
    }
}
