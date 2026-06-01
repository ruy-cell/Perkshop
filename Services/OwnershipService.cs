using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using PerkShop.Models;
using ProjectM.Network;
using Stunlock.Core;
using Unity.Entities;

namespace PerkShop.Services;

internal static class OwnershipService
{
    public readonly struct UserReapplyResult
    {
        public readonly int BuffsChecked;
        public readonly int BuffsAlreadyActive;
        public readonly int BuffLifetimeRefreshes;
        public readonly int BuffApplyAttempts;
        public readonly int BuffApplySuccesses;
        public readonly bool StatsAttempted;
        public readonly bool StatsApplied;

        public UserReapplyResult(
            int buffsChecked,
            int buffsAlreadyActive,
            int buffLifetimeRefreshes,
            int buffApplyAttempts,
            int buffApplySuccesses,
            bool statsAttempted,
            bool statsApplied)
        {
            BuffsChecked = buffsChecked;
            BuffsAlreadyActive = buffsAlreadyActive;
            BuffLifetimeRefreshes = buffLifetimeRefreshes;
            BuffApplyAttempts = buffApplyAttempts;
            BuffApplySuccesses = buffApplySuccesses;
            StatsAttempted = statsAttempted;
            StatsApplied = statsApplied;
        }

        public static UserReapplyResult Empty => new(0, 0, 0, 0, 0, false, false);
    }

    public readonly struct OnlineReapplyResult
    {
        public readonly int Checked;
        public readonly int Processed;
        public readonly int SkippedInvalid;
        public readonly int BuffsChecked;
        public readonly int BuffsAlreadyActive;
        public readonly int BuffLifetimeRefreshes;
        public readonly int BuffApplyAttempts;
        public readonly int BuffApplySuccesses;
        public readonly int StatsAttempted;
        public readonly int StatsApplied;

        public OnlineReapplyResult(
            int checkedUsers,
            int processed,
            int skippedInvalid,
            int buffsChecked,
            int buffsAlreadyActive,
            int buffLifetimeRefreshes,
            int buffApplyAttempts,
            int buffApplySuccesses,
            int statsAttempted,
            int statsApplied)
        {
            Checked = checkedUsers;
            Processed = processed;
            SkippedInvalid = skippedInvalid;
            BuffsChecked = buffsChecked;
            BuffsAlreadyActive = buffsAlreadyActive;
            BuffLifetimeRefreshes = buffLifetimeRefreshes;
            BuffApplyAttempts = buffApplyAttempts;
            BuffApplySuccesses = buffApplySuccesses;
            StatsAttempted = statsAttempted;
            StatsApplied = statsApplied;
        }

        public static OnlineReapplyResult Empty => new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
    }

    private static readonly object Lock = new();
    private static readonly string StoreFile = Path.Combine(ConfigService.ConfigDir, "ownedbuffs.json");
    private static readonly Dictionary<ulong, Entity> OnlineUsers = new();
    private static OwnershipStore _store = new();
    private static bool _loaded;
    private static bool _dirty;
    private static DateTime _lastSaveUtc = DateTime.MinValue;
    private static int _reapplyCursor;

    public static bool HasPendingSave
    {
        get
        {
            lock (Lock)
                return _dirty;
        }
    }

    public static string LastSaveUtc => _lastSaveUtc == DateTime.MinValue ? "never" : _lastSaveUtc.ToString("O");
    public static int ReapplyCursor
    {
        get
        {
            lock (Lock)
                return _reapplyCursor;
        }
    }

    public static void Initialize() => Load();

    public static void RegisterOnlineUser(ulong platformId, Entity userEntity)
    {
        if (platformId == 0 || userEntity == Entity.Null) return;
        lock (Lock) OnlineUsers[platformId] = userEntity;
    }

    public static void UnregisterOnlineUser(ulong platformId)
    {
        if (platformId == 0) return;
        lock (Lock) OnlineUsers.Remove(platformId);
    }

    public static Dictionary<ulong, Entity> GetOnlineUsersSnapshot()
    {
        lock (Lock)
            return OnlineUsers.ToDictionary(kv => kv.Key, kv => kv.Value);
    }

    public static bool PlayerOwns(ulong platformId, string key)
    {
        if (platformId == 0 || string.IsNullOrWhiteSpace(key)) return false;
        Load();
        lock (Lock)
            return _store.Players.TryGetValue(platformId, out var data)
                && ((data.OwnedBuffKeys != null && data.OwnedBuffKeys.Any(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase)))
                    || (data.AdminGrantedBuffKeys != null && data.AdminGrantedBuffKeys.Any(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase))));
    }

    public static void AddOwnedBuff(ulong platformId, string key)
    {
        if (platformId == 0 || string.IsNullOrWhiteSpace(key)) return;
        Load();
        lock (Lock)
        {
            if (!_store.Players.TryGetValue(platformId, out var data))
            {
                data = new PlayerOwnedBuffs();
                _store.Players[platformId] = data;
            }

            if (!data.OwnedBuffKeys.Any(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase)))
            {
                data.OwnedBuffKeys.Add(key.Trim());
                data.OwnedBuffKeys.Sort(StringComparer.OrdinalIgnoreCase);
                MarkDirty_NoLock();
            }
        }
    }

    public static bool EnsureAdminGrantedBuff(ulong platformId, string key)
    {
        if (platformId == 0 || string.IsNullOrWhiteSpace(key)) return false;
        Load();

        lock (Lock)
        {
            if (!_store.Players.TryGetValue(platformId, out var data))
            {
                data = new PlayerOwnedBuffs();
                _store.Players[platformId] = data;
            }

            data.AdminGrantedBuffKeys ??= new List<string>();

            if (data.AdminGrantedBuffKeys.Any(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase)))
                return false;

            data.AdminGrantedBuffKeys.Add(key.Trim());
            data.AdminGrantedBuffKeys.Sort(StringComparer.OrdinalIgnoreCase);
            MarkDirty_NoLock();
            return true;
        }
    }

    public static List<string> GetAdminGrantedBuffKeys(ulong platformId)
    {
        if (platformId == 0) return new List<string>();
        Load();

        lock (Lock)
        {
            return _store.Players.TryGetValue(platformId, out var data) && data.AdminGrantedBuffKeys != null
                ? data.AdminGrantedBuffKeys
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .ToList()
                : new List<string>();
        }
    }


    public static int CountOwnedBuffsInCategory(ulong platformId, string categoryKey)
    {
        if (platformId == 0 || string.IsNullOrWhiteSpace(categoryKey)) return 0;
        Load();

        var config = ConfigService.Shop;
        lock (Lock)
        {
            if (!_store.Players.TryGetValue(platformId, out var data))
                return 0;

            var keys = (data.OwnedBuffKeys ?? new List<string>())
                .Concat(data.AdminGrantedBuffKeys ?? new List<string>())
                .Distinct(StringComparer.OrdinalIgnoreCase);

            return keys.Count(key =>
                config.Buffs.TryGetValue(key, out var entry)
                && string.Equals(entry.Category, categoryKey, StringComparison.OrdinalIgnoreCase));
        }
    }

    public static List<string> GetOwnedBuffKeys(ulong platformId)
    {
        if (platformId == 0) return new List<string>();
        Load();

        lock (Lock)
        {
            return _store.Players.TryGetValue(platformId, out var data) && data.OwnedBuffKeys != null
                ? data.OwnedBuffKeys
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .ToList()
                : new List<string>();
        }
    }

    public static bool EnsureOwnedBuff(ulong platformId, string key)
    {
        if (platformId == 0 || string.IsNullOrWhiteSpace(key)) return false;
        Load();

        lock (Lock)
        {
            if (!_store.Players.TryGetValue(platformId, out var data))
            {
                data = new PlayerOwnedBuffs();
                _store.Players[platformId] = data;
            }

            if (data.OwnedBuffKeys.Any(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase)))
                return false;

            data.OwnedBuffKeys.Add(key.Trim());
            data.OwnedBuffKeys.Sort(StringComparer.OrdinalIgnoreCase);
            MarkDirty_NoLock();
            return true;
        }
    }


    public static Dictionary<string, int> GetOwnedStats(ulong platformId)
    {
        if (platformId == 0) return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        Load();

        lock (Lock)
        {
            return _store.Players.TryGetValue(platformId, out var data) && data.OwnedStats != null
                ? new Dictionary<string, int>(data.OwnedStats, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }
    }


    public static Dictionary<string, float> GetAdminFlatStats(ulong platformId)
    {
        if (platformId == 0) return new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        Load();

        lock (Lock)
        {
            return _store.Players.TryGetValue(platformId, out var data) && data.AdminFlatStats != null
                ? new Dictionary<string, float>(data.AdminFlatStats, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public static float AddAdminFlatStat(ulong platformId, string unitStat, float amount)
    {
        if (platformId == 0 || string.IsNullOrWhiteSpace(unitStat) || Math.Abs(amount) <= 0.0001f) return 0f;
        Load();

        lock (Lock)
        {
            if (!_store.Players.TryGetValue(platformId, out var data))
            {
                data = new PlayerOwnedBuffs();
                _store.Players[platformId] = data;
            }

            data.AdminFlatStats ??= new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            unitStat = unitStat.Trim();
            data.AdminFlatStats.TryGetValue(unitStat, out var current);
            data.AdminFlatStats[unitStat] = current + amount;

            if (Math.Abs(data.AdminFlatStats[unitStat]) <= 0.0001f)
                data.AdminFlatStats.Remove(unitStat);

            MarkDirty_NoLock();
            return data.AdminFlatStats.TryGetValue(unitStat, out var next) ? next : 0f;
        }
    }

    public static bool ClearAdminFlatStat(ulong platformId, string unitStat)
    {
        if (platformId == 0 || string.IsNullOrWhiteSpace(unitStat)) return false;
        Load();

        lock (Lock)
        {
            if (!_store.Players.TryGetValue(platformId, out var data) || data.AdminFlatStats == null)
                return false;

            bool removed = data.AdminFlatStats.Remove(unitStat.Trim());

            if (data.OwnedBuffKeys.Count == 0 && data.OwnedStats.Count == 0 && data.AdminFlatStats.Count == 0)
                _store.Players.Remove(platformId);

            if (removed) MarkDirty_NoLock();
            return removed;
        }
    }

    public static int GetOwnedStatCount(ulong platformId, string key)
    {
        if (platformId == 0 || string.IsNullOrWhiteSpace(key)) return 0;
        Load();

        lock (Lock)
        {
            return _store.Players.TryGetValue(platformId, out var data)
                && data.OwnedStats != null
                && data.OwnedStats.TryGetValue(key, out var count)
                    ? count
                    : 0;
        }
    }

    public static int CountOwnedStatTypes(ulong platformId)
    {
        if (platformId == 0) return 0;
        Load();

        lock (Lock)
        {
            return _store.Players.TryGetValue(platformId, out var data) && data.OwnedStats != null
                ? data.OwnedStats.Count(kv => !string.IsNullOrWhiteSpace(kv.Key) && kv.Value > 0)
                : 0;
        }
    }

    public static int AddOwnedStat(ulong platformId, string key, int amount = 1)
    {
        if (platformId == 0 || string.IsNullOrWhiteSpace(key) || amount <= 0) return 0;
        Load();

        lock (Lock)
        {
            if (!_store.Players.TryGetValue(platformId, out var data))
            {
                data = new PlayerOwnedBuffs();
                _store.Players[platformId] = data;
            }

            data.OwnedStats ??= new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            key = key.Trim();
            data.OwnedStats.TryGetValue(key, out var current);
            data.OwnedStats[key] = current + amount;
            MarkDirty_NoLock();
            return data.OwnedStats[key];
        }
    }

    public static bool RemoveOwnedStat(ulong platformId, string key, int amount = 1)
    {
        if (platformId == 0 || string.IsNullOrWhiteSpace(key) || amount <= 0) return false;
        Load();

        lock (Lock)
        {
            if (!_store.Players.TryGetValue(platformId, out var data) || data.OwnedStats == null)
                return false;

            key = key.Trim();
            if (!data.OwnedStats.TryGetValue(key, out var current))
                return false;

            int next = current - amount;
            if (next > 0)
                data.OwnedStats[key] = next;
            else
                data.OwnedStats.Remove(key);

            if (data.OwnedBuffKeys.Count == 0
                && (data.AdminGrantedBuffKeys == null || data.AdminGrantedBuffKeys.Count == 0)
                && data.OwnedStats.Count == 0
                && (data.AdminFlatStats == null || data.AdminFlatStats.Count == 0))
                _store.Players.Remove(platformId);

            MarkDirty_NoLock();
            return true;
        }
    }

    public static bool RemovePurchasedBuffOnly(ulong platformId, string key)
    {
        if (platformId == 0 || string.IsNullOrWhiteSpace(key)) return false;
        Load();

        lock (Lock)
        {
            if (!_store.Players.TryGetValue(platformId, out var data)) return false;

            data.OwnedBuffKeys ??= new List<string>();

            int removed = data.OwnedBuffKeys.RemoveAll(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));
            if (removed <= 0)
                return false;

            if (data.OwnedBuffKeys.Count == 0
                && (data.AdminGrantedBuffKeys == null || data.AdminGrantedBuffKeys.Count == 0)
                && (data.OwnedStats == null || data.OwnedStats.Count == 0)
                && (data.AdminFlatStats == null || data.AdminFlatStats.Count == 0))
            {
                _store.Players.Remove(platformId);
            }

            MarkDirty_NoLock();
            return true;
        }
    }

    public static bool RemoveOwnedBuff(ulong platformId, string key)
    {
        if (platformId == 0 || string.IsNullOrWhiteSpace(key)) return false;
        Load();
        lock (Lock)
        {
            if (!_store.Players.TryGetValue(platformId, out var data)) return false;

            data.OwnedBuffKeys ??= new List<string>();
            data.AdminGrantedBuffKeys ??= new List<string>();

            int removedOwned = data.OwnedBuffKeys.RemoveAll(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));
            int removedAdmin = data.AdminGrantedBuffKeys.RemoveAll(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));
            bool removed = removedOwned > 0 || removedAdmin > 0;

            if (removed)
            {
                if (data.OwnedBuffKeys.Count == 0
                    && data.AdminGrantedBuffKeys.Count == 0
                    && (data.OwnedStats == null || data.OwnedStats.Count == 0)
                    && (data.AdminFlatStats == null || data.AdminFlatStats.Count == 0))
                {
                    _store.Players.Remove(platformId);
                }

                MarkDirty_NoLock();
            }

            return removed;
        }
    }

    public static int RemoveOwnedBuffsInCategory(ulong platformId, string categoryKey, string? exceptKey = null)
    {
        if (platformId == 0 || string.IsNullOrWhiteSpace(categoryKey)) return 0;

        Load();

        var config = ConfigService.Shop;
        var keysToRemove = config.Buffs
            .Where(kv => kv.Value != null
                         && string.Equals(kv.Value.Category, categoryKey, StringComparison.OrdinalIgnoreCase)
                         && (string.IsNullOrWhiteSpace(exceptKey) || !string.Equals(kv.Key, exceptKey, StringComparison.OrdinalIgnoreCase)))
            .Select(kv => kv.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (keysToRemove.Count == 0)
            return 0;

        lock (Lock)
        {
            if (!_store.Players.TryGetValue(platformId, out var data))
                return 0;

            data.OwnedBuffKeys ??= new List<string>();
            data.AdminGrantedBuffKeys ??= new List<string>();

            int removed = 0;
            removed += data.OwnedBuffKeys.RemoveAll(k => keysToRemove.Contains(k));
            removed += data.AdminGrantedBuffKeys.RemoveAll(k => keysToRemove.Contains(k));

            if (removed > 0)
            {
                if (data.OwnedBuffKeys.Count == 0
                    && data.AdminGrantedBuffKeys.Count == 0
                    && (data.OwnedStats == null || data.OwnedStats.Count == 0)
                    && (data.AdminFlatStats == null || data.AdminFlatStats.Count == 0))
                {
                    _store.Players.Remove(platformId);
                }

                MarkDirty_NoLock();
            }

            return removed;
        }
    }

    public static UserReapplyResult ReapplyOwnedBuffsForUser(Entity userEntity, bool onlyIfMissing = true)
    {
        try
        {
            if (!Core.EntityManager.Exists(userEntity) || !Core.EntityManager.HasComponent<User>(userEntity)) return UserReapplyResult.Empty;
            var user = Core.EntityManager.GetComponentData<User>(userEntity);
            var platformId = user.PlatformId;
            if (platformId == 0) return UserReapplyResult.Empty;
            RegisterOnlineUser(platformId, userEntity);

            var character = user.LocalCharacter._Entity;
            if (character == Entity.Null || !Core.EntityManager.Exists(character)) return UserReapplyResult.Empty;

            var config = ConfigService.Shop;
            if (!config.Enabled || !config.SaveOwnership) return UserReapplyResult.Empty;

            List<string> keys;
            lock (Lock)
            {
                Load_NoLock();
                keys = _store.Players.TryGetValue(platformId, out var data)
                    ? (data.OwnedBuffKeys ?? new List<string>())
                        .Concat(data.AdminGrantedBuffKeys ?? new List<string>())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList()
                    : new List<string>();
            }

            int buffsChecked = 0;
            int buffsAlreadyActive = 0;
            int buffLifetimeRefreshes = 0;
            int buffApplyAttempts = 0;
            int buffApplySuccesses = 0;

            foreach (var key in keys)
            {
                if (!config.Buffs.TryGetValue(key, out var entry)) continue;
                if (!entry.Enabled || !entry.PersistentPurchase || entry.BuffPrefab == 0) continue;

                if (string.Equals(entry.Category, "blood_buff", StringComparison.OrdinalIgnoreCase) && !config.EnableExperimentalBloodBuffs)
                {
                    // Keep ownership data intact, but do not reapply experimental blood-buff prefabs
                    // unless explicitly enabled. Also clear any stale live PerkShop blood buffs.
                    BuffService.RemoveConfiguredCategoryBuffs(character, "blood_buff");
                    continue;
                }

                buffsChecked++;
                var buffGuid = new PrefabGUID(entry.BuffPrefab);
                bool isBloodBuff = string.Equals(entry.Category, "blood_buff", StringComparison.OrdinalIgnoreCase);
                bool isRenewableTimed = BuffService.IsRenewableTimedEntry(config, entry);
                if (BuffService.HasBuff(character, buffGuid))
                {
                    buffsAlreadyActive++;

                    // Renewable timed buffs keep their live countdown and are only re-applied after expiry.
                    // No periodic refresh while active, otherwise the UI countdown never reaches the end.
                    if (isRenewableTimed)
                        continue;

                    if (!isBloodBuff && entry.MutateAppliedBuffLifetime)
                    {
                        if (BuffService.ForceOwnedBuffLifetime(
                            character,
                            buffGuid,
                            durationSeconds: BuffService.ResolveDurationSeconds(config, entry),
                            persistThroughDeath: BuffService.ResolvePersistThroughDeath(config, entry),
                            keepVisibleTimerFrozen: entry.KeepVisibleTimerFrozen,
                            visibleTimerSeconds: entry.VisibleTimerSeconds,
                            preserveVanillaCleanup: BuffService.PreserveVanillaCleanup(config, entry)))
                        {
                            buffLifetimeRefreshes++;
                        }
                    }

                    if (onlyIfMissing)
                        continue;
                }

                buffApplyAttempts++;
                if (BuffService.ApplyPurchasedBuff(
                    userEntity,
                    character,
                    buffGuid,
                    preventDuplicate: true,
                    allowMutation: config.AllowBuffEntityMutation,
                    mutateLifetime: isBloodBuff ? false : entry.MutateAppliedBuffLifetime,
                    durationSeconds: BuffService.ResolveDurationSeconds(config, entry),
                    persistThroughDeath: BuffService.ResolvePersistThroughDeath(config, entry),
                    keepVisibleTimerFrozen: entry.KeepVisibleTimerFrozen,
                    visibleTimerSeconds: entry.VisibleTimerSeconds,
                    preserveVanillaCleanup: BuffService.PreserveVanillaCleanup(config, entry)))
                {
                    buffApplySuccesses++;
                }
            }

            bool statsAttempted = false;
            bool statsApplied = false;
            if (config.EnableStatShop)
            {
                statsAttempted = true;
                statsApplied = StatService.ApplyOwnedStatsForUser(userEntity);
            }

            return new UserReapplyResult(
                buffsChecked,
                buffsAlreadyActive,
                buffLifetimeRefreshes,
                buffApplyAttempts,
                buffApplySuccesses,
                statsAttempted,
                statsApplied);
        }
        catch (Exception e)
        {
            Core.LogException(e);
            return UserReapplyResult.Empty;
        }
    }

    public static void ReapplyOnlineOwnedBuffs()
    {
        var config = ConfigService.Shop;
        if (!config.ReapplyOwnedBuffsWhenMissing) return;

        ReapplyOnlineOwnedBuffs(onlyIfMissing: true, maxUsers: config.ReapplyMaxUsersPerCycle);
    }

    public static OnlineReapplyResult ReapplyOnlineOwnedBuffs(bool onlyIfMissing)
        => ReapplyOnlineOwnedBuffs(onlyIfMissing, maxUsers: 0);

    public static OnlineReapplyResult ReapplyOnlineOwnedBuffs(bool onlyIfMissing, int maxUsers)
    {
        var config = ConfigService.Shop;
        if (!config.Enabled || !config.SaveOwnership)
            return OnlineReapplyResult.Empty;

        List<Entity> users;
        lock (Lock)
        {
            users = OnlineUsers.Values.ToList();

            if (maxUsers > 0 && users.Count > maxUsers)
            {
                if (_reapplyCursor >= users.Count)
                    _reapplyCursor = 0;

                users = users
                    .Skip(_reapplyCursor)
                    .Concat(users.Take(_reapplyCursor))
                    .Take(maxUsers)
                    .ToList();

                _reapplyCursor = (_reapplyCursor + maxUsers) % Math.Max(1, OnlineUsers.Count);
            }
        }

        int checkedUsers = 0;
        int processed = 0;
        int skippedInvalid = 0;
        int buffsChecked = 0;
        int buffsAlreadyActive = 0;
        int buffLifetimeRefreshes = 0;
        int buffApplyAttempts = 0;
        int buffApplySuccesses = 0;
        int statsAttempted = 0;
        int statsApplied = 0;

        foreach (var userEntity in users)
        {
            checkedUsers++;

            if (userEntity == Entity.Null || !Core.EntityManager.Exists(userEntity) || !Core.EntityManager.HasComponent<User>(userEntity))
            {
                skippedInvalid++;
                continue;
            }

            var userResult = ReapplyOwnedBuffsForUser(userEntity, onlyIfMissing);
            processed++;

            buffsChecked += userResult.BuffsChecked;
            buffsAlreadyActive += userResult.BuffsAlreadyActive;
            buffLifetimeRefreshes += userResult.BuffLifetimeRefreshes;
            buffApplyAttempts += userResult.BuffApplyAttempts;
            buffApplySuccesses += userResult.BuffApplySuccesses;
            if (userResult.StatsAttempted) statsAttempted++;
            if (userResult.StatsApplied) statsApplied++;
        }

        return new OnlineReapplyResult(
            checkedUsers,
            processed,
            skippedInvalid,
            buffsChecked,
            buffsAlreadyActive,
            buffLifetimeRefreshes,
            buffApplyAttempts,
            buffApplySuccesses,
            statsAttempted,
            statsApplied);
    }


    private static bool NormalizeStoredKeys_NoLock()
    {
        bool changed = false;

        foreach (var data in _store.Players.Values)
        {
            if (data == null)
                continue;

            changed |= NormalizeBuffKeyList_NoLock(data.OwnedBuffKeys);
            changed |= NormalizeBuffKeyList_NoLock(data.AdminGrantedBuffKeys);

            if (data.OwnedStats != null)
            {
                foreach (var alias in KeyAliasService.StatKeyAliases)
                {
                    if (!data.OwnedStats.TryGetValue(alias.Key, out var amount))
                        continue;

                    data.OwnedStats.Remove(alias.Key);
                    data.OwnedStats.TryGetValue(alias.Value, out var current);
                    data.OwnedStats[alias.Value] = current + amount;
                    changed = true;
                }
            }
        }

        return changed;
    }

    private static bool NormalizeBuffKeyList_NoLock(List<string>? keys)
    {
        if (keys == null || keys.Count == 0)
            return false;

        bool changed = false;
        var normalized = new List<string>();

        foreach (var key in keys)
        {
            var mapped = KeyAliasService.NormalizeBuffKey(key);
            if (!string.Equals(key, mapped, StringComparison.Ordinal))
                changed = true;

            if (!normalized.Any(existing => string.Equals(existing, mapped, StringComparison.OrdinalIgnoreCase)))
                normalized.Add(mapped);
            else
                changed = true;
        }

        if (!changed)
            return false;

        keys.Clear();
        keys.AddRange(normalized.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
        return true;
    }

    private static void Load()
    {
        lock (Lock) Load_NoLock();
    }

    private static void Load_NoLock()
    {
        if (_loaded) return;
        try
        {
            Directory.CreateDirectory(ConfigService.ConfigDir);
            if (File.Exists(StoreFile))
                _store = JsonSerializer.Deserialize<OwnershipStore>(File.ReadAllText(StoreFile), ConfigService.JsonOptions) ?? new OwnershipStore();
            else
                _store = new OwnershipStore();

            _store.Players ??= new Dictionary<ulong, PlayerOwnedBuffs>();
            foreach (var key in _store.Players.Keys.ToList())
            {
                _store.Players[key].OwnedBuffKeys ??= new List<string>();
                _store.Players[key].AdminGrantedBuffKeys ??= new List<string>();
                _store.Players[key].OwnedStats ??= new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                _store.Players[key].AdminFlatStats ??= new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            }

            if (NormalizeStoredKeys_NoLock())
                _dirty = true;

            _loaded = true;
        }
        catch (Exception e)
        {
            Core.LogException(e);
            _store = new OwnershipStore();
            _loaded = true;
        }
    }

    private static void MarkDirty_NoLock()
    {
        _dirty = true;

        var debounce = ConfigService.Shop.OwnershipSaveDebounceSeconds;
        if (debounce <= 0f)
            Save_NoLock();
    }

    public static void FlushPendingSaves(bool force = false)
    {
        lock (Lock)
        {
            if (!_dirty)
                return;

            var debounce = ConfigService.Shop.OwnershipSaveDebounceSeconds;
            if (!force && debounce > 0f && (DateTime.UtcNow - _lastSaveUtc).TotalSeconds < debounce)
                return;

            Save_NoLock();
        }
    }

    private static void Save_NoLock()
    {
        Directory.CreateDirectory(ConfigService.ConfigDir);
        File.WriteAllText(StoreFile, JsonSerializer.Serialize(_store, ConfigService.JsonOptions));
        _lastSaveUtc = DateTime.UtcNow;
        _dirty = false;
    }
}
