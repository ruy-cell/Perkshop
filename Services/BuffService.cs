using System;
using System.Collections.Generic;
using System.Linq;
using PerkShop.Models;
using ProjectM;
using ProjectM.Network;
using ProjectM.Shared;
using Stunlock.Core;
using Unity.Entities;

namespace PerkShop.Services;

internal static class BuffService
{
    private sealed class PendingPermanentBuffInfo
    {
        public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
        public int DurationSeconds { get; init; }
        public bool PersistThroughDeath { get; init; }
        public bool PreserveVanillaCleanup { get; init; }
    }

    private static readonly Dictionary<Entity, PendingPermanentBuffInfo> PendingPermanentBuffs = new();
    private static readonly TimeSpan PendingBuffTimeout = TimeSpan.FromSeconds(30);
    private static readonly HashSet<int> PreparedPermanentBuffPrefabs = new();

    public static int PendingPermanentBuffCount => PendingPermanentBuffs.Count;


    public static bool IsRenewableTimedEntry(ShopConfigRoot config, PerkShopEntry entry)
        => ConfigService.IsRenewableTimedCategory(config, entry.Category);

    public static int ResolveDurationSeconds(ShopConfigRoot config, PerkShopEntry entry)
        => ConfigService.ResolveBuffDuration(config, entry);

    public static bool ResolvePersistThroughDeath(ShopConfigRoot config, PerkShopEntry entry)
        => ConfigService.ResolveBuffPersistThroughDeath(config, entry);

    public static bool PreserveVanillaCleanup(ShopConfigRoot config, PerkShopEntry entry)
        => ConfigService.PreserveVanillaBuffCleanup(config, entry);

    public static void ResetRuntimeStateAfterConfigReload()
    {
        PendingPermanentBuffs.Clear();
        PreparedPermanentBuffPrefabs.Clear();
    }

    public static void ProcessPendingPermanentBuffsDuringSpawn(EntityQuery spawnQuery)
    {
        if (PendingPermanentBuffs.Count == 0)
            return;

        var entities = spawnQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
        try
        {
            for (int i = 0; i < entities.Length; i++)
            {
                Entity buffEntity = entities[i];

                if (!PendingPermanentBuffs.TryGetValue(buffEntity, out var info))
                    continue;

                // Important: remove/neutralize LifeTime while the buff still has SpawnTag, before the
                // vanilla/client spawn path serializes a countdown into the UI.
                TrySetLifetime(buffEntity, info.DurationSeconds, info.PersistThroughDeath, info.PreserveVanillaCleanup);
                PendingPermanentBuffs.Remove(buffEntity);
            }
        }
        finally
        {
            if (entities.IsCreated)
                entities.Dispose();
        }
    }

    private static void MarkPendingLifetimeMutation(
        Entity buffEntity,
        int durationSeconds,
        bool persistThroughDeath,
        bool preserveVanillaCleanup)
    {
        if (buffEntity == Entity.Null)
            return;

        PendingPermanentBuffs[buffEntity] = new PendingPermanentBuffInfo
        {
            DurationSeconds = durationSeconds,
            PersistThroughDeath = persistThroughDeath,
            PreserveVanillaCleanup = preserveVanillaCleanup
        };
    }

    public static void ProcessPendingPermanentBuffs()
    {
        if (PendingPermanentBuffs.Count == 0)
            return;

        var em = Core.EntityManager;

        foreach (var pair in PendingPermanentBuffs.ToArray())
        {
            Entity buffEntity = pair.Key;
            PendingPermanentBuffInfo info = pair.Value;

            if (buffEntity == Entity.Null || !em.Exists(buffEntity))
            {
                PendingPermanentBuffs.Remove(buffEntity);
                continue;
            }

            // Let vanilla spawn systems consume SpawnTag before stripping LifeTime/cleanup components.
            if (em.HasComponent<SpawnTag>(buffEntity))
            {
                if (DateTime.UtcNow - info.CreatedAtUtc > PendingBuffTimeout)
                {
                    PendingPermanentBuffs.Remove(buffEntity);
                    Core.Log.LogWarning($"[BuffService] Timed out waiting to finalize permanent buff entity:{buffEntity.Index}:{buffEntity.Version}");
                }

                continue;
            }

            TrySetLifetime(buffEntity, info.DurationSeconds, info.PersistThroughDeath, info.PreserveVanillaCleanup);
            PendingPermanentBuffs.Remove(buffEntity);
        }
    }

    private static void PreparePermanentBuffPrefab(PrefabGUID buffPrefab, int durationSeconds, bool persistThroughDeath)
    {
        if (durationSeconds >= 0)
            return;

        if (PreparedPermanentBuffPrefabs.Contains(buffPrefab.GuidHash))
            return;

        try
        {
            var prefabCollectionSystem = Core.Systems.PrefabCollectionSystem;
            if (!prefabCollectionSystem._PrefabGuidToEntityMap.TryGetValue(buffPrefab, out Entity prefabEntity))
                return;

            var em = Core.EntityManager;
            if (prefabEntity == Entity.Null || !em.Exists(prefabEntity))
                return;

            // Remove lifetime/cleanup from the prefab before the buff instance is spawned.
            // This prevents the client from receiving a countdown in the first place.
            if (em.HasComponent<LifeTime>(prefabEntity))
                em.RemoveComponent<LifeTime>(prefabEntity);

            if (em.HasComponent<RemoveBuffOnGameplayEvent>(prefabEntity))
                em.RemoveComponent<RemoveBuffOnGameplayEvent>(prefabEntity);

            if (em.HasComponent<RemoveBuffOnGameplayEventEntry>(prefabEntity))
                em.RemoveComponent<RemoveBuffOnGameplayEventEntry>(prefabEntity);

            if (persistThroughDeath && !em.HasComponent<Buff_Persists_Through_Death>(prefabEntity))
                em.AddComponent<Buff_Persists_Through_Death>(prefabEntity);

            PreparedPermanentBuffPrefabs.Add(buffPrefab.GuidHash);
            Core.LogDebugIfEnabled($"[BuffService] Prepared permanent buff prefab {buffPrefab.GuidHash} with no LifeTime.");
        }
        catch (Exception e)
        {
            Core.Log.LogWarning($"[BuffService] Failed to prepare permanent buff prefab {buffPrefab.GuidHash}: {e.Message}");
        }
    }

    public static bool HasBuff(Entity characterEntity, PrefabGUID buffPrefab)
        => BuffUtility.TryGetBuff(Core.EntityManager, characterEntity, buffPrefab, out _);

    public static bool ApplyPurchasedBuff(
        Entity userEntity,
        Entity characterEntity,
        PrefabGUID buffPrefab,
        bool preventDuplicate,
        bool allowMutation,
        bool mutateLifetime,
        int durationSeconds,
        bool persistThroughDeath,
        bool keepVisibleTimerFrozen = false,
        int visibleTimerSeconds = 0,
        bool preserveVanillaCleanup = false)
    {
        int effectiveDuration = durationSeconds;

        if (BuffUtility.TryGetBuff(Core.EntityManager, characterEntity, buffPrefab, out var existingBuffEntity))
        {
            // Important:
            // If the player already has the potion/elixir buff active, preventDuplicate used to return early.
            // That left the vanilla countdown untouched. Existing owned buffs must still be lifetime-mutated.
            if (allowMutation && mutateLifetime)
                MarkPendingLifetimeMutation(existingBuffEntity, effectiveDuration, persistThroughDeath, preserveVanillaCleanup);

            return true;
        }

        if (preventDuplicate && HasBuff(characterEntity, buffPrefab))
            return false;

        // Do not mutate the prefab before vanilla spawn. Live testing showed some potion/elixir
        // prefabs stop contributing to the client attribute layer when LifeTime is stripped on
        // the prefab itself. Instance finalization below remains the safer server-side behavior.
        var fromCharacter = new FromCharacter { User = userEntity, Character = characterEntity };
        var buffEvent = new ApplyBuffDebugEvent { BuffPrefabGUID = buffPrefab };
        Core.DebugEventsSystem.ApplyBuff(fromCharacter, buffEvent);

        if (!BuffUtility.TryGetBuff(Core.EntityManager, characterEntity, buffPrefab, out var buffEntity))
            return false;

        // Permanent-shop behavior:
        // Apply lifetime mutation whenever requested by config. DurationSeconds = -1 means no time limit.
        if (allowMutation && mutateLifetime)
            MarkPendingLifetimeMutation(buffEntity, effectiveDuration, persistThroughDeath, preserveVanillaCleanup);

        return true;
    }

    public static bool RemoveBuff(Entity characterEntity, PrefabGUID buffPrefab)
    {
        if (!BuffUtility.TryGetBuff(Core.EntityManager, characterEntity, buffPrefab, out var buffEntity))
            return false;

        PendingPermanentBuffs.Remove(buffEntity);
        DestroyUtility.Destroy(Core.EntityManager, buffEntity, DestroyDebugReason.TryRemoveBuff);

        // Some interop paths mark destruction asynchronously. Ensure DestroyTag is present so
        // vanilla Destroy_* systems can clean up stat/modification side effects.
        if (Core.EntityManager.Exists(buffEntity) && !Core.EntityManager.HasComponent<DestroyTag>(buffEntity))
            Core.EntityManager.AddComponent<DestroyTag>(buffEntity);

        return true;
    }

    public static void MakeBuffPermanent(Entity buffEntity, bool persistThroughDeath = true)
    {
        TrySetLifetime(buffEntity, -1, persistThroughDeath, preserveVanillaCleanup: false);
    }

    public static int RemoveConfiguredCategoryBuffs(Entity characterEntity, string category)
    {
        if (characterEntity == Entity.Null || string.IsNullOrWhiteSpace(category))
            return 0;

        var config = ConfigService.Shop;
        var prefabSet = config.Buffs.Values
            .Where(entry => entry != null
                            && entry.BuffPrefab != 0
                            && string.Equals(entry.Category, category, StringComparison.OrdinalIgnoreCase))
            .Select(entry => entry.BuffPrefab)
            .ToHashSet();

        if (prefabSet.Count == 0)
            return 0;

        int removed = 0;

        // First use BuffUtility for the common one-active-instance path.
        foreach (int prefab in prefabSet)
        {
            if (RemoveBuff(characterEntity, new PrefabGUID(prefab)))
                removed++;
        }

        // Then aggressively scan live buff entities. Some blood buffs can leave stacked/secondary
        // instances that BuffUtility.TryGetBuff does not return after permanent mutation.
        removed += RemoveLiveBuffsByPrefabSet(characterEntity, prefabSet);
        return removed;
    }

    private static int RemoveLiveBuffsByPrefabSet(Entity characterEntity, HashSet<int> prefabSet)
    {
        if (characterEntity == Entity.Null || prefabSet.Count == 0)
            return 0;

        Unity.Collections.NativeArray<Entity> entities = default;

        try
        {
            var em = Core.EntityManager;
            var query = em.CreateEntityQuery(ComponentType.ReadOnly<Buff>(), ComponentType.ReadOnly<PrefabGUID>());
            entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);

            int removed = 0;

            for (int i = 0; i < entities.Length; i++)
            {
                Entity buffEntity = entities[i];

                if (!em.Exists(buffEntity))
                    continue;

                if (!em.HasComponent<Buff>(buffEntity) || !em.HasComponent<PrefabGUID>(buffEntity))
                    continue;

                var buff = em.GetComponentData<Buff>(buffEntity);
                if (buff.Target != characterEntity)
                    continue;

                var prefab = em.GetComponentData<PrefabGUID>(buffEntity);
                if (!prefabSet.Contains(prefab.GuidHash))
                    continue;

                PendingPermanentBuffs.Remove(buffEntity);
                DestroyUtility.Destroy(em, buffEntity, DestroyDebugReason.TryRemoveBuff);

                if (em.Exists(buffEntity) && !em.HasComponent<DestroyTag>(buffEntity))
                    em.AddComponent<DestroyTag>(buffEntity);

                removed++;
            }

            return removed;
        }
        catch (Exception e)
        {
            Core.Log.LogWarning($"[BuffService] Failed scanning live category buffs for removal: {e.Message}");
            return 0;
        }
        finally
        {
            if (entities.IsCreated)
                entities.Dispose();
        }
    }

    public static bool ForceOwnedBuffLifetime(
        Entity characterEntity,
        PrefabGUID buffPrefab,
        int durationSeconds,
        bool persistThroughDeath,
        bool keepVisibleTimerFrozen = false,
        int visibleTimerSeconds = 0,
        bool preserveVanillaCleanup = false)
    {
        if (!BuffUtility.TryGetBuff(Core.EntityManager, characterEntity, buffPrefab, out var buffEntity))
            return false;
        int effectiveDuration = durationSeconds;

        MarkPendingLifetimeMutation(buffEntity, effectiveDuration, persistThroughDeath, preserveVanillaCleanup);
        return true;
    }

    private static void TrySetLifetime(Entity buffEntity, int durationSeconds, bool persistThroughDeath, bool preserveVanillaCleanup)
    {
        var em = Core.EntityManager;

        if (buffEntity == Entity.Null || !em.Exists(buffEntity))
            return;

        if (persistThroughDeath && !em.HasComponent<Buff_Persists_Through_Death>(buffEntity))
            em.AddComponent<Buff_Persists_Through_Death>(buffEntity);

        // Remove common vanilla cleanup components only for no-countdown permanent buffs.
        // Renewable timed buffs preserve vanilla cleanup so blood/potion/elixir effects cleanly expire and can be reapplied.
        if (!preserveVanillaCleanup)
        {
            if (em.HasComponent<RemoveBuffOnGameplayEvent>(buffEntity))
                em.RemoveComponent<RemoveBuffOnGameplayEvent>(buffEntity);

            if (em.HasComponent<RemoveBuffOnGameplayEventEntry>(buffEntity))
                em.RemoveComponent<RemoveBuffOnGameplayEventEntry>(buffEntity);
        }

        if (durationSeconds < 0)
        {
            // Permanent/no time limit.
            if (em.HasComponent<LifeTime>(buffEntity))
                em.RemoveComponent<LifeTime>(buffEntity);

            return;
        }

        if (durationSeconds > 0)
        {
            if (!em.HasComponent<LifeTime>(buffEntity))
                em.AddComponent<LifeTime>(buffEntity);

            em.SetComponentData(buffEntity, new LifeTime
            {
                Duration = durationSeconds,
                EndAction = LifeTimeEndAction.Destroy
            });
        }
    }
}

