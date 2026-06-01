using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using BepInEx;
using PerkShop.Models;
using ProjectM.Network;

namespace PerkShop.Services;

internal static class PlayerCacheService
{
    private static readonly object Lock = new();
    private static readonly string CacheFile = Path.Combine(ConfigService.ConfigDir, "playercache.json");
    private static PlayerCacheStore _store = new();
    private static bool _loaded;
    private static bool _dirty;
    private static DateTime _lastSaveUtc = DateTime.MinValue;

    public static int CachedPlayerCount
    {
        get
        {
            Load();
            lock (Lock)
                return _store.Players?.Count ?? 0;
        }
    }

    public static bool HasPendingSave
    {
        get
        {
            lock (Lock)
                return _dirty;
        }
    }

    public static void Initialize() => Load();

    public static void Remember(User user)
    {
        string name = user.CharacterName.ToString();
        Remember(user.PlatformId, name);
    }

    public static void Remember(ulong platformId, string characterName)
    {
        if (platformId == 0 || string.IsNullOrWhiteSpace(characterName))
            return;

        Load();

        lock (Lock)
        {
            _store.Players ??= new Dictionary<ulong, PlayerCacheEntry>();

            string trimmedName = characterName.Trim();
            string now = DateTime.UtcNow.ToString("O");

            PlayerCacheEntry? existing = null;
            bool changed = true;
            if (_store.Players.TryGetValue(platformId, out var cachedEntry))
            {
                existing = cachedEntry;
                changed = !string.Equals(cachedEntry.CharacterName, trimmedName, StringComparison.Ordinal);
            }

            if (!changed && existing != null)
            {
                if (DateTime.TryParse(existing.LastSeenUtc, out var lastSeen)
                    && (DateTime.UtcNow - lastSeen.ToUniversalTime()).TotalMinutes < 10)
                {
                    return;
                }
            }

            _store.Players[platformId] = new PlayerCacheEntry
            {
                CharacterName = trimmedName,
                LastSeenUtc = now
            };

            MarkDirty_NoLock();
        }
    }

    public static bool TryGetName(ulong platformId, out string characterName)
    {
        characterName = string.Empty;
        if (platformId == 0) return false;

        Load();

        lock (Lock)
        {
            if (_store.Players != null
                && _store.Players.TryGetValue(platformId, out var entry)
                && !string.IsNullOrWhiteSpace(entry.CharacterName))
            {
                characterName = entry.CharacterName.Trim();
                return true;
            }
        }

        return false;
    }

    public static void FlushPendingSaves(bool force = false)
    {
        lock (Lock)
        {
            if (!_dirty)
                return;

            var debounce = ConfigService.Shop.PlayerCacheSaveDebounceSeconds;
            if (!force && debounce > 0f && (DateTime.UtcNow - _lastSaveUtc).TotalSeconds < debounce)
                return;

            Save_NoLock();
        }
    }

    private static void Load()
    {
        lock (Lock)
        {
            if (_loaded) return;

            try
            {
                Directory.CreateDirectory(ConfigService.ConfigDir);

                if (File.Exists(CacheFile))
                    _store = JsonSerializer.Deserialize<PlayerCacheStore>(File.ReadAllText(CacheFile), ConfigService.JsonOptions) ?? new PlayerCacheStore();
                else
                    _store = new PlayerCacheStore();

                _store.Players ??= new Dictionary<ulong, PlayerCacheEntry>();
                _loaded = true;
            }
            catch (Exception e)
            {
                Core.LogException(e);
                _store = new PlayerCacheStore();
                _loaded = true;
            }
        }
    }

    private static void MarkDirty_NoLock()
    {
        _dirty = true;

        var debounce = ConfigService.Shop.PlayerCacheSaveDebounceSeconds;
        if (debounce <= 0f)
            Save_NoLock();
    }

    private static void Save_NoLock()
    {
        Directory.CreateDirectory(ConfigService.ConfigDir);
        File.WriteAllText(CacheFile, JsonSerializer.Serialize(_store, ConfigService.JsonOptions));
        _lastSaveUtc = DateTime.UtcNow;
        _dirty = false;
    }
}
