using System;
using System.Runtime.CompilerServices;
using BepInEx.Logging;
using ProjectM;
using ProjectM.Scripting;
using ProjectM.Gameplay.Systems;
using Unity.Entities;
using PerkShop.Services;

namespace PerkShop;

internal static class Core
{
    private static bool _initialized;
    private static World? _server;
    private static EntityManager _entityManager;
    private static ServerScriptMapper? _serverScriptMapper;
    private static DebugEventsSystem? _debugEventsSystem;
    private static SystemService? _systemService;

    public static World Server => _server ??= GetWorld("Server") ?? throw new Exception("Server world not found.");
    public static EntityManager EntityManager => _entityManager == default ? (_entityManager = Server.EntityManager) : _entityManager;
    public static ServerScriptMapper ServerScriptMapper => _serverScriptMapper ??= Server.GetExistingSystemManaged<ServerScriptMapper>();
    public static ServerGameManager ServerGameManager => ServerScriptMapper.GetServerGameManager();
    public static DebugEventsSystem DebugEventsSystem => _debugEventsSystem ??= Server.GetExistingSystemManaged<DebugEventsSystem>();
    public static SystemService Systems => _systemService ??= new SystemService(Server);
    public static ManualLogSource Log => Plugin.PluginLog;

    internal static void InitializeAfterLoaded()
    {
        if (_initialized) return;
        _server = GetWorld("Server") ?? throw new Exception("Server world not found.");
        _entityManager = _server.EntityManager;
        _serverScriptMapper = _server.GetExistingSystemManaged<ServerScriptMapper>();
        _debugEventsSystem = _server.GetExistingSystemManaged<DebugEventsSystem>();
        _systemService = new SystemService(_server);
        QueryService.Initialize(_systemService);
        CarrierPrefabService.PrepareConfiguredStatCarrierPrefab();
        _initialized = true;
        Log.LogInfo("Core initialized.");
    }

    internal static World? GetWorld(string name)
    {
        foreach (var world in World.s_AllWorlds)
            if (world != null && world.Name == name)
                return world;
        return null;
    }

    public static void LogDebugIfEnabled(string message)
    {
        try
        {
            if (ConfigService.DebugLoggingEnabled)
                Log.LogInfo(message);
        }
        catch
        {
            // Never let debug logging participate in gameplay or spawn failures.
        }
    }

    public static void LogException(Exception e, [CallerMemberName] string? caller = null)
        => Log.LogError($"[{caller}] {e.Message}\n{e.StackTrace}");
}
