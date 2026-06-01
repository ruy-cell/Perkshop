using System;
using System.Reflection;
using Unity.Entities;

namespace PerkShop.Services;

internal static class QueryService
{
    private static EntityQuery? _scriptSpawnServerQuery;
    private static EntityQuery? _buffSystemSpawnServerQuery;

    internal static void Initialize(SystemService systems)
    {
        // Keep this method for existing call sites, but avoid a hard compile-time reference
        // to ScriptSpawnServer. Some V Rising interop builds expose the system under a
        // generated/non-imported type name. The query is resolved lazily from the Harmony
        // __instance in ScriptSpawnServerPatch.
        _scriptSpawnServerQuery = null;
        _buffSystemSpawnServerQuery = null;
    }

    internal static EntityQuery GetScriptSpawnServerQuery(object instance)
    {
        if (_scriptSpawnServerQuery.HasValue)
            return _scriptSpawnServerQuery.Value;

        _scriptSpawnServerQuery = ResolveScriptSpawnServerQuery(instance);
        return _scriptSpawnServerQuery.Value;
    }

    internal static EntityQuery GetBuffSystemSpawnServerQuery(object instance)
    {
        if (_buffSystemSpawnServerQuery.HasValue)
            return _buffSystemSpawnServerQuery.Value;

        _buffSystemSpawnServerQuery = ResolveSystemQuery(instance, "BuffSystem_Spawn_Server", "_Query");
        return _buffSystemSpawnServerQuery.Value;
    }

    internal static void ResetRuntimeCaches()
    {
        _scriptSpawnServerQuery = null;
        _buffSystemSpawnServerQuery = null;
    }

    private static EntityQuery ResolveScriptSpawnServerQuery(object instance)
        => ResolveSystemQuery(instance, "ScriptSpawnServer", "_EntityQuery");

    private static EntityQuery ResolveSystemQuery(object instance, string systemName, string preferredQueryName)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        Type type = instance.GetType();

        var field = type.GetField(preferredQueryName, flags);
        if (field?.GetValue(instance) is EntityQuery fieldQuery)
        {
            Core.LogDebugIfEnabled($"[QueryService] {systemName} query resolved from {preferredQueryName} field.");
            return fieldQuery;
        }

        var property = type.GetProperty(preferredQueryName, flags);
        if (property?.GetValue(instance) is EntityQuery propertyQuery)
        {
            Core.LogDebugIfEnabled($"[QueryService] {systemName} query resolved from {preferredQueryName} property.");
            return propertyQuery;
        }

        var entityQueriesProperty = type.GetProperty("EntityQueries", flags);
        if (entityQueriesProperty?.GetValue(instance) is EntityQuery[] entityQueries && entityQueries.Length > 0)
        {
            Core.Log.LogWarning($"[QueryService] {systemName}.{preferredQueryName} was unavailable; falling back to EntityQueries[0].");
            return entityQueries[0];
        }

        var entityQueriesField = type.GetField("EntityQueries", flags);
        if (entityQueriesField?.GetValue(instance) is EntityQuery[] fieldEntityQueries && fieldEntityQueries.Length > 0)
        {
            Core.Log.LogWarning($"[QueryService] {systemName}.{preferredQueryName} was unavailable; falling back to EntityQueries[0].");
            return fieldEntityQueries[0];
        }

        throw new InvalidOperationException($"Unable to resolve {systemName} query.");
    }
}
