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

internal static class StatService
{
    private static readonly PrefabGUID BloodcraftBonusStatsCarrier = new(1774716596); // SetBonus_AllLeech_T09, used by Bloodcraft

    private sealed class PendingCarrierInfo
    {
        public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
        public Entity CharacterEntity { get; init; } = Entity.Null;
        public float? HealthRatioBeforeRebuild { get; init; }
    }

    private static readonly Dictionary<Entity, PendingCarrierInfo> PendingPermanentCarriers = new();
    private static readonly Dictionary<ulong, float> PendingHealthRatiosByPlatformId = new();
    private static readonly Dictionary<ulong, int> LastAppliedStatHashes = new();
    private static readonly HashSet<ulong> SpawnValidatedPlatformIds = new();
    private static readonly HashSet<string> WarnedInvalidStatConfig = new(StringComparer.OrdinalIgnoreCase);
    private static readonly TimeSpan PendingCarrierTimeout = TimeSpan.FromSeconds(30);

    public static int PendingCarrierCount => PendingPermanentCarriers.Count;
    public static int CachedStatHashCount => LastAppliedStatHashes.Count;

    public static void ResetRuntimeStateAfterConfigReload()
    {
        SpawnValidatedPlatformIds.Clear();
        PendingPermanentCarriers.Clear();
        PendingHealthRatiosByPlatformId.Clear();
        LastAppliedStatHashes.Clear();
        WarnedInvalidStatConfig.Clear();
    }

    public static string DescribeConfiguredCarrier()
    {
        if (!IsCarrierConfigured(out var carrier, out var error))
            return $"invalid ({error})";

        return $"SetBonus_ShieldPowerAndHealingReceived_T08 ({ConfigService.Shop.StatCarrierBuffPrefab})";
    }

    private static void WarnInvalidStatOnce(string warningKey, string message)
    {
        if (WarnedInvalidStatConfig.Add(warningKey))
            Core.Log.LogWarning(message);
    }

    public static bool IsCarrierConfigured(out PrefabGUID carrier, out string error)
    {
        var configured = ConfigService.Shop.StatCarrierBuffPrefab;
        carrier = configured != 0 ? new PrefabGUID(configured) : PrefabGUID.Empty;
        error = string.Empty;

        if (configured == 0)
        {
            error = "StatCarrierBuffPrefab is 0. Configure a dedicated carrier buff prefab before using the permanent stat shop.";
            return false;
        }

        if (carrier.Equals(BloodcraftBonusStatsCarrier))
        {
            error = "StatCarrierBuffPrefab is set to SetBonus_AllLeech_T09, which Bloodcraft uses as its bonus stats carrier. Use a different dedicated carrier buff.";
            return false;
        }

        return true;
    }

    public static bool ApplyOwnedStatsForUser(Entity userEntity)
    {
        try
        {
            if (!Core.EntityManager.Exists(userEntity) || !Core.EntityManager.HasComponent<User>(userEntity))
                return false;

            var user = Core.EntityManager.GetComponentData<User>(userEntity);
            var character = user.LocalCharacter._Entity;
            if (character == Entity.Null || !Core.EntityManager.Exists(character))
                return false;

            return ApplyOwnedStats(userEntity, character, user.PlatformId);
        }
        catch (Exception e)
        {
            Core.LogException(e);
            return false;
        }
    }

    public static bool ApplyOwnedStats(Entity userEntity, Entity characterEntity, ulong platformId)
        => ApplyOwnedStats(userEntity, characterEntity, platformId, forceRebuildCarrier: false);

    public static bool RebuildOwnedStats(Entity userEntity, Entity characterEntity, ulong platformId)
        => ApplyOwnedStats(userEntity, characterEntity, platformId, forceRebuildCarrier: true);

    private static bool ApplyOwnedStats(Entity userEntity, Entity characterEntity, ulong platformId, bool forceRebuildCarrier)
    {
        var config = ConfigService.Shop;
        if (!config.EnableStatShop) return false;

        if (!IsCarrierConfigured(out var carrier, out var error))
        {
            Core.Log.LogWarning($"[StatService] {error}");
            return false;
        }

        CarrierPrefabService.PrepareConfiguredStatCarrierPrefab();

        var ownedStats = OwnershipService.GetOwnedStats(platformId);
        var adminFlatStats = OwnershipService.GetAdminFlatStats(platformId);
        bool hasAnyStats = ownedStats.Count > 0 || adminFlatStats.Count > 0;

        if (!hasAnyStats)
        {
            BuffService.RemoveBuff(characterEntity, carrier);
            SpawnValidatedPlatformIds.Remove(platformId);
            PendingHealthRatiosByPlatformId.Remove(platformId);
            LastAppliedStatHashes.Remove(platformId);
            return true;
        }

        int desiredHash = ComputeStatOwnershipHash(platformId, ownedStats, adminFlatStats, config);
        bool preserveRatio = string.Equals(config.MaxHealthPurchaseBehavior, "PreserveRatio", StringComparison.OrdinalIgnoreCase);

        if (forceRebuildCarrier || !LastAppliedStatHashes.TryGetValue(platformId, out var lastHash) || lastHash != desiredHash)
            CaptureHealthRatioIfNeeded(characterEntity, platformId, preserveRatio);

        if (forceRebuildCarrier)
        {
            SpawnValidatedPlatformIds.Remove(platformId);
            BuffService.RemoveBuff(characterEntity, carrier);
        }
        else if (BuffUtility.TryGetBuff(Core.EntityManager, characterEntity, carrier, out var existingBuffEntity))
        {
            if (SpawnValidatedPlatformIds.Contains(platformId) && LastAppliedStatHashes.TryGetValue(platformId, out lastHash) && lastHash == desiredHash)
            {
                MarkStatCarrierPendingPermanent(existingBuffEntity, characterEntity, TryConsumeHealthRatio(platformId));
                return true;
            }

            SpawnValidatedPlatformIds.Remove(platformId);
            BuffService.RemoveBuff(characterEntity, carrier);
        }

        var fromCharacter = new FromCharacter { User = userEntity, Character = characterEntity };
        var buffEvent = new ApplyBuffDebugEvent { BuffPrefabGUID = carrier };
        Core.DebugEventsSystem.ApplyBuff(fromCharacter, buffEvent);

        // Fallback path: in some interop builds ScriptSpawnServer is difficult to patch by type name.
        // Populate immediately if the carrier entity already exists. When ScriptSpawnServer also sees it,
        // the spawn patch will simply rebuild the same buffer during the vanilla spawn window.
        if (BuffUtility.TryGetBuff(Core.EntityManager, characterEntity, carrier, out var spawnedCarrier))
            TryPopulateOwnedStatBuffer(spawnedCarrier, characterEntity, userEntity);

        return true;
    }

    public static bool TryPopulateOwnedStatBuffer(Entity buffEntity, Entity characterEntity, Entity userEntity)
    {
        try
        {
            var em = Core.EntityManager;

            if (buffEntity == Entity.Null || !em.Exists(buffEntity))
                return false;

            if (characterEntity == Entity.Null || !em.Exists(characterEntity))
                return false;

            if (userEntity == Entity.Null || !em.Exists(userEntity) || !em.HasComponent<User>(userEntity))
                return false;

            var user = em.GetComponentData<User>(userEntity);
            bool populated = TryPopulateOwnedStatBuffer(buffEntity, user.PlatformId, clearExisting: true);

            if (!populated)
                return false;

            SpawnValidatedPlatformIds.Add(user.PlatformId);
            LastAppliedStatHashes[user.PlatformId] = ComputeStatOwnershipHash(user.PlatformId);
            TryAddSyncToUser(buffEntity, userEntity);

            float? healthRatio = TryConsumeHealthRatio(user.PlatformId);
            if (!healthRatio.HasValue && string.Equals(ConfigService.Shop.MaxHealthPurchaseBehavior, "PreserveRatio", StringComparison.OrdinalIgnoreCase))
                healthRatio = CaptureHealthRatio(characterEntity);

            MarkStatCarrierPendingPermanent(buffEntity, characterEntity, healthRatio);
            return true;
        }
        catch (Exception e)
        {
            Core.LogException(e);
            return false;
        }
    }

    public static bool TryPopulateOwnedStatBuffer(Entity buffEntity, ulong platformId, bool clearExisting = true)
    {
        var em = Core.EntityManager;
        var config = ConfigService.Shop;

        if (!config.EnableStatShop)
            return false;

        if (buffEntity == Entity.Null || !em.Exists(buffEntity))
            return false;

        var ownedStats = OwnershipService.GetOwnedStats(platformId);
        var adminFlatStats = OwnershipService.GetAdminFlatStats(platformId);

        if (ownedStats.Count == 0 && adminFlatStats.Count == 0)
            return false;

        DynamicBuffer<ModifyUnitStatBuff_DOTS> buffer = em.HasBuffer<ModifyUnitStatBuff_DOTS>(buffEntity)
            ? em.GetBuffer<ModifyUnitStatBuff_DOTS>(buffEntity)
            : em.AddBuffer<ModifyUnitStatBuff_DOTS>(buffEntity);

        if (clearExisting)
            buffer.Clear();

        foreach (var (key, count) in ownedStats.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (count <= 0) continue;
            if (!StatDefinitionService.TryGetParsedStat(key, out var definition))
                continue;

            var entry = definition.Entry;

            if (!StatDefinitionService.IsClientUnsupportedStatAllowed(config, entry))
            {
                WarnInvalidStatOnce(
                    $"ClientUnsupported:{key}",
                    $"[StatService] Stat '{key}' ({entry.UnitStat}) is disabled by EnableClientUnsupportedStats=false because Bloodcraft/Eclipse/VampireAttributes can spam NotImplementedException for it. It remains owned but is not applied.");
                continue;
            }

            buffer.Add(new ModifyUnitStatBuff_DOTS
            {
                StatType = definition.UnitStat,
                ModificationType = definition.ModificationType,
                AttributeCapType = definition.AttributeCapType,
                SoftCapValue = 0f,
                Value = entry.ValuePerPurchase * count,
                Modifier = 1,
                IncreaseByStacks = false,
                ValueByStacks = 0,
                Priority = 0,
                Id = ModificationIDs.Create().NewModificationId()
            });
        }

        foreach (var (unitStatName, value) in adminFlatStats.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (Math.Abs(value) <= 0.0001f) continue;

            if (!Enum.TryParse<UnitStatType>(unitStatName, ignoreCase: true, out var unitStat))
            {
                WarnInvalidStatOnce($"AdminFlat:{unitStatName}", $"[StatService] Unknown admin flat UnitStat '{unitStatName}'.");
                continue;
            }

            if (!config.EnableClientUnsupportedStats && !StatDefinitionService.IsClientTabSafeUnitStat(unitStatName))
            {
                WarnInvalidStatOnce(
                    $"ClientUnsupportedAdminFlat:{unitStatName}",
                    $"[StatService] Admin flat stat '{unitStatName}' is disabled by EnableClientUnsupportedStats=false because Bloodcraft/Eclipse/VampireAttributes can spam NotImplementedException for it.");
                continue;
            }

            buffer.Add(new ModifyUnitStatBuff_DOTS
            {
                StatType = unitStat,
                ModificationType = ModificationType.Add,
                AttributeCapType = AttributeCapType.Uncapped,
                SoftCapValue = 0f,
                Value = value,
                Modifier = 1,
                IncreaseByStacks = false,
                ValueByStacks = 0,
                Priority = 0,
                Id = ModificationIDs.Create().NewModificationId()
            });
        }

        return buffer.Length > 0;
    }

    public static void TryAddSyncToUser(Entity buffEntity, Entity userEntity)
    {
        var em = Core.EntityManager;

        if (buffEntity == Entity.Null || userEntity == Entity.Null)
            return;

        if (!em.Exists(buffEntity) || !em.Exists(userEntity))
            return;

        DynamicBuffer<SyncToUserBuffer> syncToUsers = em.HasBuffer<SyncToUserBuffer>(buffEntity)
            ? em.GetBuffer<SyncToUserBuffer>(buffEntity)
            : em.AddBuffer<SyncToUserBuffer>(buffEntity);

        for (int i = 0; i < syncToUsers.Length; i++)
        {
            if (syncToUsers[i].UserEntity.Equals(userEntity))
                return;
        }

        syncToUsers.Add(new SyncToUserBuffer
        {
            UserEntity = userEntity
        });
    }

    public static void MarkStatCarrierPendingPermanent(Entity buffEntity)
        => MarkStatCarrierPendingPermanent(buffEntity, Entity.Null, null);

    public static void MarkStatCarrierPendingPermanent(Entity buffEntity, Entity characterEntity, float? healthRatioBeforeRebuild)
    {
        if (buffEntity == Entity.Null)
            return;

        PendingPermanentCarriers[buffEntity] = new PendingCarrierInfo
        {
            CharacterEntity = characterEntity,
            HealthRatioBeforeRebuild = healthRatioBeforeRebuild
        };
    }

    public static void ProcessPendingPermanentCarriers()
    {
        if (PendingPermanentCarriers.Count == 0)
            return;

        var em = Core.EntityManager;

        foreach (var pair in PendingPermanentCarriers.ToArray())
        {
            var buffEntity = pair.Key;
            var info = pair.Value;

            if (buffEntity == Entity.Null || !em.Exists(buffEntity))
            {
                PendingPermanentCarriers.Remove(buffEntity);
                continue;
            }

            if (em.HasComponent<SpawnTag>(buffEntity))
            {
                if (DateTime.UtcNow - info.CreatedAtUtc > PendingCarrierTimeout)
                {
                    PendingPermanentCarriers.Remove(buffEntity);
                    Core.Log.LogWarning($"[StatService] Timed out waiting to finalize stat carrier entity:{buffEntity.Index}:{buffEntity.Version}");
                }

                continue;
            }

            BuffService.MakeBuffPermanent(buffEntity, persistThroughDeath: true);

            Entity characterEntity = info.CharacterEntity;
            if ((characterEntity == Entity.Null || !em.Exists(characterEntity)) && em.HasComponent<Buff>(buffEntity))
            {
                var buff = em.GetComponentData<Buff>(buffEntity);
                characterEntity = buff.Target;
            }

            NormalizeHealthAfterStatCarrierUpdate(characterEntity, info.HealthRatioBeforeRebuild);

            PendingPermanentCarriers.Remove(buffEntity);
            Core.LogDebugIfEnabled($"[StatService] Finalized permanent stat carrier entity:{buffEntity.Index}:{buffEntity.Version}");
        }
    }

    public static void NormalizeHealthAfterStatCarrierUpdate(Entity characterEntity)
        => NormalizeHealthAfterStatCarrierUpdate(characterEntity, null);

    public static void NormalizeHealthAfterStatCarrierUpdate(Entity characterEntity, float? previousHealthRatio)
    {
        var em = Core.EntityManager;

        if (characterEntity == Entity.Null || !em.Exists(characterEntity) || !em.HasComponent<Health>(characterEntity))
            return;

        var health = em.GetComponentData<Health>(characterEntity);
        var maxHealth = health.MaxHealth._Value;
        var behavior = ConfigService.Shop.MaxHealthPurchaseBehavior;

        if (maxHealth <= 0f)
            return;

        if (string.Equals(behavior, "FillToMax", StringComparison.OrdinalIgnoreCase))
        {
            health.Value = maxHealth;
            em.SetComponentData(characterEntity, health);
            return;
        }

        if (string.Equals(behavior, "PreserveRatio", StringComparison.OrdinalIgnoreCase) && previousHealthRatio.HasValue)
        {
            float ratio = previousHealthRatio.Value;
            if (ratio < 0f) ratio = 0f;
            if (ratio > 1f) ratio = 1f;

            health.Value = maxHealth * ratio;

            if (health.Value > maxHealth)
                health.Value = maxHealth;

            em.SetComponentData(characterEntity, health);
            return;
        }

        if (health.Value > maxHealth)
        {
            health.Value = maxHealth;
            em.SetComponentData(characterEntity, health);
        }
    }

    private static void CaptureHealthRatioIfNeeded(Entity characterEntity, ulong platformId, bool enabled)
    {
        if (!enabled)
            return;

        var ratio = CaptureHealthRatio(characterEntity);
        if (ratio.HasValue)
            PendingHealthRatiosByPlatformId[platformId] = ratio.Value;
    }

    private static float? TryConsumeHealthRatio(ulong platformId)
    {
        if (!PendingHealthRatiosByPlatformId.TryGetValue(platformId, out var ratio))
            return null;

        PendingHealthRatiosByPlatformId.Remove(platformId);
        return ratio;
    }

    private static float? CaptureHealthRatio(Entity characterEntity)
    {
        var em = Core.EntityManager;

        if (characterEntity == Entity.Null || !em.Exists(characterEntity) || !em.HasComponent<Health>(characterEntity))
            return null;

        var health = em.GetComponentData<Health>(characterEntity);
        var maxHealth = health.MaxHealth._Value;

        if (maxHealth <= 0f)
            return null;

        float ratio = health.Value / maxHealth;
        if (ratio < 0f) ratio = 0f;
        if (ratio > 1f) ratio = 1f;
        return ratio;
    }

    private static int ComputeStatOwnershipHash(ulong platformId)
    {
        return ComputeStatOwnershipHash(platformId, OwnershipService.GetOwnedStats(platformId), OwnershipService.GetAdminFlatStats(platformId), ConfigService.Shop);
    }

    private static int ComputeStatOwnershipHash(ulong platformId, Dictionary<string, int> ownedStats, Dictionary<string, float> adminFlatStats, ShopConfigRoot config)
    {
        unchecked
        {
            int hash = 17;
            hash = (hash * 31) + config.StatCarrierBuffPrefab;
            hash = (hash * 31) + (config.EnableClientUnsupportedStats ? 1 : 0);
            hash = (hash * 31) + (config.UseClientAttributeStatAliases ? 1 : 0);

            foreach (var (key, count) in ownedStats.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (count <= 0)
                    continue;

                hash = (hash * 31) + StringComparer.OrdinalIgnoreCase.GetHashCode(key);
                hash = (hash * 31) + count;

                if (config.Stats.TryGetValue(key, out var entry) && entry != null)
                {
                    hash = (hash * 31) + StringComparer.OrdinalIgnoreCase.GetHashCode(entry.UnitStat ?? string.Empty);
                    hash = (hash * 31) + StringComparer.OrdinalIgnoreCase.GetHashCode(entry.ModificationType ?? string.Empty);
                    hash = (hash * 31) + StringComparer.OrdinalIgnoreCase.GetHashCode(entry.AttributeCapType ?? string.Empty);
                    hash = (hash * 31) + entry.ValuePerPurchase.GetHashCode();
                }
            }

            foreach (var (key, value) in adminFlatStats.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (Math.Abs(value) <= 0.0001f)
                    continue;

                hash = (hash * 31) + StringComparer.OrdinalIgnoreCase.GetHashCode(key);
                hash = (hash * 31) + value.GetHashCode();
            }

            return hash;
        }
    }

}