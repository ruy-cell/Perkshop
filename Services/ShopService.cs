using System;
using System.Collections.Generic;
using System.Linq;
using PerkShop.Models;
using PerkShop.Utilities;
using ProjectM;
using ProjectM.Network;
using Stunlock.Core;
using Unity.Entities;

namespace PerkShop.Services;

internal static class ShopService
{
    private static string InferStatGroup(string key, string unitStat)
    {
        string value = $"{key} {unitStat}".ToLowerInvariant();

        if (value.Contains("power") || value.Contains("minion"))
            return "Power";
        if (value.Contains("health") || value.Contains("resistance") || value.Contains("reduction") || value.Contains("healing"))
            return "Health / Defense";
        if (value.Contains("speed") || value.Contains("cooldown"))
            return "Speed / Cooldown";
        if (value.Contains("critical") || value.Contains("crit"))
            return "Critical";
        if (value.Contains("leech"))
            return "Leech";
        return "Utility";
    }

    private static string FormatNumber(float value)
        => Math.Abs(value % 1f) <= 0.0001f ? ((int)value).ToString() : value.ToString("0.####");

    private const int ListPageSize = 8;
    private const int StatusPageSize = 12;

    private static (int page, int totalPages) NormalizePage(int requestedPage, int totalItems)
    {
        int totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)ListPageSize));
        int page = Math.Clamp(requestedPage <= 0 ? 1 : requestedPage, 1, totalPages);
        return (page, totalPages);
    }
    private static IEnumerable<KeyValuePair<string, BuffCategoryDefinition>> VisibleCategories(ulong platformId)
    {
        var config = ConfigService.Shop;

        return config.Categories
            .Where(kv =>
            {
                if (!string.Equals(kv.Key, "potion_elixir", StringComparison.OrdinalIgnoreCase))
                    return true;

                bool hasConfiguredBuffs = config.Buffs.Values.Any(entry =>
                    string.Equals(entry.Category, kv.Key, StringComparison.OrdinalIgnoreCase));

                bool hasOwnedBuffs = platformId != 0 && OwnershipService.CountOwnedBuffsInCategory(platformId, kv.Key) > 0;

                return hasConfiguredBuffs || hasOwnedBuffs;
            });
    }

    public static void Menu(Entity userEntity, Action<string> reply)
    {
        var config = ConfigService.Shop;
        string buffAccess = config.EnableBuffWhitelist ? "Whitelist ON" : "Open";
        string statAccess = config.EnableStatWhitelist ? "Whitelist ON" : "Open";

        reply(
            "=== PerkShop Menu ===\n" +
            $"Currency: {config.CurrencyName}\n" +
            $"Perk Shop: {buffAccess}\n" +
            "  .perk bufflist <page>\n" +
            "  .perk buffdet <buffKey>\n" +
            "  .perk buffbuy <buffKey>\n" +
            "  .perk buffremove <buffKey>\n\n" +
            $"Stat Shop: {statAccess}\n" +
            "  .perk statlist <page>\n" +
            "  .perk statdet <statKey>\n" +
            "  .perk statbuy <statKey>\n" +
            "  .perk statremove <statKey>\n\n" +
            "Utility:\n" +
            "  .perk status <page>\n" +
            "  .perk search <text>\n" +
            "  .perk sync\n\nOwned perks are restored manually per session with .perk sync.");
    }

    public static void Status(Entity userEntity, int page, Action<string> reply)
    {
        try
        {
            if (!PlayerStateHelper.Exists(userEntity) || !Core.EntityManager.HasComponent<User>(userEntity))
            {
                reply("User entity not ready.");
                return;
            }

            var user = Core.EntityManager.GetComponentData<User>(userEntity);
            var config = ConfigService.Shop;

            var lines = new List<string>
            {
                $"Player: {user.CharacterName}"
            };

            var buffSlots = VisibleCategories(user.PlatformId)
                .OrderBy(kv => kv.Key)
                .Select(kv =>
                {
                    int used = OwnershipService.CountOwnedBuffsInCategory(user.PlatformId, kv.Key);
                    string cap = kv.Value.MaxOwnedSlots.HasValue ? kv.Value.MaxOwnedSlots.Value.ToString() : "∞";
                    return $"{kv.Value.DisplayName}: {used}/{cap}";
                })
                .ToArray();

            lines.Add("Buff Slots: " + (buffSlots.Length == 0 ? "none" : string.Join(" | ", buffSlots)));

            var ownedBuffKeys = OwnershipService.GetOwnedBuffKeys(user.PlatformId);
            lines.Add("Owned Buffs:");
            lines.AddRange(ownedBuffKeys.Count == 0
                ? new[] { "  none" }
                : ownedBuffKeys.Select(k => $"  {k}"));

            var adminBuffKeys = OwnershipService.GetAdminGrantedBuffKeys(user.PlatformId);
            lines.Add("Admin Given Buffs:");
            lines.AddRange(adminBuffKeys.Count == 0
                ? new[] { "  none" }
                : adminBuffKeys.Select(k => $"  {k}"));

            var ownedStats = OwnershipService.GetOwnedStats(user.PlatformId);
            var adminStats = OwnershipService.GetAdminFlatStats(user.PlatformId);

            int usedStatSlots = OwnershipService.CountOwnedStatTypes(user.PlatformId);
            string statCap = config.EnableStatTypeSlots && config.MaxOwnedStatTypes > 0 ? config.MaxOwnedStatTypes.ToString() : "∞";

            lines.Add("Stat Slots:");
            lines.Add($"  {usedStatSlots}/{statCap}");

            lines.Add("Purchased Stats:");
            var purchasedStatLines = ownedStats
                .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                .Select(kv =>
                {
                    if (!config.Stats.TryGetValue(kv.Key, out var entry))
                        return $"  {kv.Key} x{kv.Value}";

                    float total = entry.ValuePerPurchase * kv.Value;
                    return $"  {kv.Key} x{kv.Value} = +{FormatNumber(total)} {entry.UnitStat}";
                })
                .ToArray();

            lines.AddRange(purchasedStatLines.Length == 0 ? new[] { "  none" } : purchasedStatLines);

            lines.Add("Admin Flat Stats:");
            var adminFlatStatLines = adminStats
                .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                .Select(kv => $"  {kv.Key} +{FormatNumber(kv.Value)}")
                .ToArray();

            lines.AddRange(adminFlatStatLines.Length == 0 ? new[] { "  none" } : adminFlatStatLines);

            var (currentPage, totalPages) = NormalizePage(page, lines.Count);
            var pageLines = lines
                .Skip((currentPage - 1) * StatusPageSize)
                .Take(StatusPageSize)
                .ToArray();

            reply(
                $"=== PerkShop Status Page {currentPage}/{totalPages} ===\n" +
                string.Join("\n", pageLines) +
                (currentPage < totalPages ? $"\n\nNext page: .perk status {currentPage + 1}" : ""));
        }
        catch (Exception e)
        {
            Core.LogException(e);
            reply("Could not read PerkShop status. Check server logs.");
        }
    }

    public static void BuyBuff(Entity userEntity, Entity characterEntity, string key, Action<string> reply)
    {
        try
        {
            var config = ConfigService.Shop;
            if (!config.Enabled) { reply("Perk shop is disabled."); return; }
            if (!AccessService.CanAccessPerkShop(userEntity, reply)) return;
            if (!PlayerStateHelper.Exists(userEntity) || !PlayerStateHelper.Exists(characterEntity)) { reply("Player entity not ready."); return; }
            if (!Core.EntityManager.HasComponent<User>(userEntity)) { reply("User component not ready."); return; }
            if (config.BlockPurchasesInCombat && PlayerStateHelper.IsInCombat(characterEntity)) { reply("You cannot buy buffs while in combat."); return; }
            if (string.IsNullOrWhiteSpace(key)) { ListBuffKeys(userEntity, 1, reply); return; }

            key = KeyAliasService.NormalizeBuffKey(key);
            if (!config.Buffs.TryGetValue(key, out var entry) || !entry.Enabled)
            {
                reply($"Unknown buff '{key}'. Use .perk bufflist or .perk search <text>.");
                return;
            }

            if (entry.BuffPrefab == 0)
            {
                reply($"Buff '{key}' has BuffPrefab = 0. Configure a valid buff prefab first.");
                return;
            }

            if (string.Equals(entry.Category, "blood_buff", StringComparison.OrdinalIgnoreCase) && !config.EnableExperimentalBloodBuffs)
            {
                reply("Blood-buff purchases are disabled by config. Set EnableExperimentalBloodBuffs = true to sell renewable 2-hour blood buffs.");
                return;
            }

            var user = Core.EntityManager.GetComponentData<User>(userEntity);
            var platformId = user.PlatformId;
            OwnershipService.RegisterOnlineUser(platformId, userEntity);

            if (config.SaveOwnership && entry.PersistentPurchase && OwnershipService.PlayerOwns(platformId, key))
            {
                reply($"You already own {entry.DisplayName}.");
                OwnershipService.ReapplyOwnedBuffsForUser(userEntity);
                return;
            }

            var buffGuid = new PrefabGUID(entry.BuffPrefab);

            // Blood buffs now use category slots (default 5) instead of forced mutual exclusion.
            // Do not clear other owned blood buffs here; slot validation below controls the cap.

            if (config.SaveOwnership && entry.PersistentPurchase
                && config.Categories.TryGetValue(entry.Category, out var category)
                && category.MaxOwnedSlots.HasValue)
            {
                int usedSlots = OwnershipService.CountOwnedBuffsInCategory(platformId, entry.Category);
                if (usedSlots >= category.MaxOwnedSlots.Value)
                {
                    reply($"Category '{category.DisplayName}' is full: {usedSlots}/{category.MaxOwnedSlots.Value} owned slots used.");
                    return;
                }
            }

            if (entry.PreventDuplicate && BuffService.HasBuff(characterEntity, buffGuid))
            {
                reply($"You already have {entry.DisplayName}.");
                return;
            }

            var currency = new PrefabGUID(config.CurrencyPrefab);
            var have = InventoryHelper.Count(characterEntity, currency);
            if (have < entry.Cost)
            {
                reply($"Not enough {config.CurrencyName}: {have}/{entry.Cost}.");
                return;
            }

            if (!InventoryHelper.TryRemove(characterEntity, currency, entry.Cost))
            {
                reply($"Could not remove {entry.Cost} {config.CurrencyName} from your inventory.");
                return;
            }

            bool isBloodBuff = string.Equals(entry.Category, "blood_buff", StringComparison.OrdinalIgnoreCase);

            var applied = BuffService.ApplyPurchasedBuff(
                userEntity,
                characterEntity,
                buffGuid,
                entry.PreventDuplicate,
                isBloodBuff ? config.AllowBuffEntityMutation : (config.ForcePermanentBuffs || config.AllowBuffEntityMutation),
                isBloodBuff ? false : (config.ForcePermanentBuffs || entry.MutateAppliedBuffLifetime),
                BuffService.ResolveDurationSeconds(config, entry),
                BuffService.ResolvePersistThroughDeath(config, entry),
                entry.KeepVisibleTimerFrozen,
                entry.VisibleTimerSeconds,
                BuffService.PreserveVanillaCleanup(config, entry));

            if (!applied)
            {
                Core.ServerGameManager.TryAddInventoryItem(characterEntity, currency, entry.Cost);
                reply($"Could not apply {entry.DisplayName}. Currency was refunded if the inventory accepted it.");
                return;
            }

            if (config.SaveOwnership && entry.PersistentPurchase)
                OwnershipService.AddOwnedBuff(platformId, key);

            var persistenceText = config.SaveOwnership && entry.PersistentPurchase ? " Ownership saved." : string.Empty;
            reply($"Purchased {entry.DisplayName} for {entry.Cost} {config.CurrencyName}.{persistenceText}");
        }
        catch (Exception e)
        {
            Core.LogException(e);
            reply("Buff purchase failed. Check server logs.");
        }
    }

    public static void RemoveBuff(Entity userEntity, Entity characterEntity, string key, Action<string> reply)
    {
        try
        {
            var config = ConfigService.Shop;
            if (!config.Enabled) { reply("Perk shop is disabled."); return; }
            if (!AccessService.CanAccessPerkShop(userEntity, reply)) return;
            if (!PlayerStateHelper.Exists(userEntity) || !PlayerStateHelper.Exists(characterEntity)) { reply("Player entity not ready."); return; }
            if (!Core.EntityManager.HasComponent<User>(userEntity)) { reply("User component not ready."); return; }
            if (config.BlockRemovalsInCombat && PlayerStateHelper.IsInCombat(characterEntity)) { reply("You cannot remove shop buffs while in combat."); return; }
            if (string.IsNullOrWhiteSpace(key)) { reply("Usage: .perk buffremove <key>"); return; }

            key = KeyAliasService.NormalizeBuffKey(key);
            if (!config.Buffs.TryGetValue(key, out var entry) || entry.BuffPrefab == 0)
            {
                reply($"Unknown buff '{key}'. Use .perk bufflist or .perk search <text>.");
                return;
            }

            var user = Core.EntityManager.GetComponentData<User>(userEntity);
            OwnershipService.RegisterOnlineUser(user.PlatformId, userEntity);
            PlayerCacheService.Remember(user);

            bool removingOwnedSlot = config.SaveOwnership && OwnershipService.PlayerOwns(user.PlatformId, key);
            int slotFreeCost = 0;
            string categoryName = entry.Category;

            if (removingOwnedSlot
                && config.Categories.TryGetValue(entry.Category, out var category)
                && category.SlotFreeCost.HasValue)
            {
                slotFreeCost = category.SlotFreeCost.Value;
                categoryName = category.DisplayName;
            }

            if (removingOwnedSlot && slotFreeCost > 0)
            {
                var currency = new PrefabGUID(config.CurrencyPrefab);
                var have = InventoryHelper.Count(characterEntity, currency);
                if (have < slotFreeCost)
                {
                    reply($"Not enough {config.CurrencyName} to free a slot in category '{categoryName}': {have}/{slotFreeCost}.");
                    return;
                }

                if (!InventoryHelper.TryRemove(characterEntity, currency, slotFreeCost))
                {
                    reply($"Could not remove {slotFreeCost} {config.CurrencyName} to free a slot in category '{categoryName}'.");
                    return;
                }
            }

            bool removedBuff = BuffService.RemoveBuff(characterEntity, new PrefabGUID(entry.BuffPrefab));
            bool removedOwnership = config.SaveOwnership && OwnershipService.RemoveOwnedBuff(user.PlatformId, key);

            if (removedBuff || removedOwnership)
            {
                string costText = removingOwnedSlot && slotFreeCost > 0
                    ? $" Slot freed for {slotFreeCost} {config.CurrencyName}."
                    : string.Empty;
                reply($"Removed {entry.DisplayName}. Ownership removed: {removedOwnership}.{costText}");
            }
            else
            {
                reply($"You do not have or own {entry.DisplayName}.");
            }
        }
        catch (Exception e)
        {
            Core.LogException(e);
            reply("Buff removal failed. Check server logs.");
        }
    }

    public static void AdminMenu(Action<string> reply)
    {
        var config = ConfigService.Shop;
        string buffWhitelist = config.EnableBuffWhitelist ? $"ON ({config.BuffWhitelistPlatformIds.Count})" : "OFF";
        string statWhitelist = config.EnableStatWhitelist ? $"ON ({config.StatWhitelistPlatformIds.Count})" : "OFF";

        reply(
            "=== PerkShop Admin ===\n" +
            "Player Audit:\n" +
            "  .perk info <playerName|platformId>\n" +
            "  .perk wlplayer <playerName|platformId>\n\n" +
            "Buff Ownership:\n" +
            "  .perk giftbuff <playerName|platformId> <buffKey>\n" +
            "  .perk revokebuff <playerName|platformId> <buffKey>\n" +
            "  .perk addbuff <playerName|platformId> <buffKey>\n" +
            "  .perk clearbuff <playerName|platformId> <buffKey>\n\n" +
            "Stats:\n" +
            "  .perk giftstat <playerName|platformId> <statKey> <ranks>\n" +
            "  .perk revokestat <playerName|platformId> <statKey> <ranks>\n" +
            "  .perk addflat <playerName|platformId> <UnitStat|statKey> <amount>\n" +
            "  .perk clearflat <playerName|platformId> <UnitStat|statKey>\n\n" +
            "Whitelist:\n" +
            $"  Buff whitelist: {buffWhitelist}\n" +
            $"  Stat whitelist: {statWhitelist}\n" +
            "  .perk wlstatus\n" +
            "  .perk wlcheckbuff\n" +
            "  .perk wlcheckstat\n" +
            "  .perk wlcheckall\n" +
            "  .perk wladdbuff <playerName|platformId>\n" +
            "  .perk wlremovebuff <playerName|platformId>\n" +
            "  .perk wladdstat <playerName|platformId>\n" +
            "  .perk wlremovestat <playerName|platformId>\n\n" +
            "Config / Operations:\n" +
            "  .perk reload\n" +
            "  .perk diag\n" +
            "  .perk validate\n" +
            "  .perk syncall");
    }

    public static void WhitelistList(string scope, Action<string> reply)
    {
        var config = ConfigService.Shop;
        scope = string.IsNullOrWhiteSpace(scope) ? "all" : scope.Trim().ToLowerInvariant();

        if (scope != "buff" && scope != "stat" && scope != "all")
        {
            reply("Usage: .perk wlcheck <buff|stat|all>");
            return;
        }

        string FormatList(string title, List<ulong> ids, Dictionary<ulong, string> names)
        {
            if (ids.Count == 0)
                return $"{title}:\n  none";

            var lines = ids
                .Select(id =>
                {
                    if (names != null && names.TryGetValue(id, out var storedName) && !string.IsNullOrWhiteSpace(storedName))
                        return $"  {storedName}";

                    if (PlayerCacheService.TryGetName(id, out var cachedName))
                        return $"  {cachedName}";

                    if (AdminPlayerLookup.TryFindUser(id.ToString(), out _, out var onlineUser, out _))
                    {
                        PlayerCacheService.Remember(onlineUser);
                        return $"  {onlineUser.CharacterName}";
                    }

                    return "  Unknown/offline";
                });

            return $"{title}:\n" + string.Join("\n", lines);
        }

        if (scope == "buff")
        {
            reply("=== Buff Whitelist ===\n" + FormatList("Players", config.BuffWhitelistPlatformIds, config.BuffWhitelistNames));
            return;
        }

        if (scope == "stat")
        {
            reply("=== Stat Whitelist ===\n" + FormatList("Players", config.StatWhitelistPlatformIds, config.StatWhitelistNames));
            return;
        }

        reply(
            "=== Whitelist Lists ===\n" +
            FormatList("Perk Shop", config.BuffWhitelistPlatformIds, config.BuffWhitelistNames) +
            "\n\n" +
            FormatList("Stat Shop", config.StatWhitelistPlatformIds, config.StatWhitelistNames));
    }

    public static void WhitelistPlayer(string playerRef, Action<string> reply)
    {
        try
        {
            if (!AdminPlayerLookup.TryFindUser(playerRef, out _, out var user, out var error))
            {
                reply(error);
                return;
            }

            var config = ConfigService.Shop;
            bool inBuff = config.BuffWhitelistPlatformIds.Contains(user.PlatformId);
            bool inStat = config.StatWhitelistPlatformIds.Contains(user.PlatformId);

            reply(
                $"=== Whitelist: {user.CharacterName} ===\n" +
                $"PlatformId: {user.PlatformId}\n" +
                $"Perk Shop: {(config.EnableBuffWhitelist ? (inBuff ? "Allowed" : "Blocked") : "Open to everyone")} | Listed: {(inBuff ? "yes" : "no")}\n" +
                $"Stat Shop: {(config.EnableStatWhitelist ? (inStat ? "Allowed" : "Blocked") : "Open to everyone")} | Listed: {(inStat ? "yes" : "no")}");
        }
        catch (Exception e)
        {
            Core.LogException(e);
            reply("Could not read whitelist status for player. Check server logs.");
        }
    }

    public static void AdminInfo(string playerRef, Action<string> reply)
    {
        try
        {
            if (!AdminPlayerLookup.TryFindUser(playerRef, out var userEntity, out var user, out var error))
            {
                reply(error);
                return;
            }

            OwnershipService.RegisterOnlineUser(user.PlatformId, userEntity);

            var config = ConfigService.Shop;
            var keys = OwnershipService.GetOwnedBuffKeys(user.PlatformId);

            var categorySummary = VisibleCategories(user.PlatformId)
                .OrderBy(kv => kv.Key)
                .Select(kv =>
                {
                    int used = OwnershipService.CountOwnedBuffsInCategory(user.PlatformId, kv.Key);
                    string cap = kv.Value.MaxOwnedSlots.HasValue ? kv.Value.MaxOwnedSlots.Value.ToString() : "∞";
                    return $"  {kv.Value.DisplayName}: {used}/{cap}";
                })
                .ToArray();

            string buffList = keys.Count == 0
                ? "  none"
                : string.Join("\n", keys.Select(k => config.Buffs.TryGetValue(k, out var entry) ? $"  {k} - {entry.DisplayName}" : $"  {k} - missing config"));

            var adminBuffKeys = OwnershipService.GetAdminGrantedBuffKeys(user.PlatformId);
            string adminBuffList = adminBuffKeys.Count == 0
                ? "  none"
                : string.Join("\n", adminBuffKeys.Select(k => config.Buffs.TryGetValue(k, out var entry) ? $"  {k} - {entry.DisplayName}" : $"  {k} - missing config"));

            var ownedStats = OwnershipService.GetOwnedStats(user.PlatformId);
            int usedStatSlots = OwnershipService.CountOwnedStatTypes(user.PlatformId);
            string statCap = config.EnableStatTypeSlots && config.MaxOwnedStatTypes > 0 ? config.MaxOwnedStatTypes.ToString() : "∞";

            string statList = ownedStats.Count == 0
                ? "  none"
                : string.Join("\n", ownedStats.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase).Select(kv =>
                {
                    if (!config.Stats.TryGetValue(kv.Key, out var entry))
                        return $"  {kv.Key} x{kv.Value} - missing config";
                    return $"  {kv.Key} x{kv.Value} = +{FormatNumber(entry.ValuePerPurchase * kv.Value)} {entry.UnitStat}";
                }));

            var adminStats = OwnershipService.GetAdminFlatStats(user.PlatformId);
            string adminStatList = adminStats.Count == 0
                ? "  none"
                : string.Join("\n", adminStats.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase).Select(kv => $"  {kv.Key} +{FormatNumber(kv.Value)}"));

            bool inBuffWhitelist = config.BuffWhitelistPlatformIds.Contains(user.PlatformId);
            bool inStatWhitelist = config.StatWhitelistPlatformIds.Contains(user.PlatformId);

            reply(
                $"=== Player Info: {user.CharacterName} ===\n" +
                $"PlatformId: {user.PlatformId}\n\n" +
                "Buff Slots:\n" +
                $"{string.Join("\n", categorySummary)}\n\n" +
                "Owned Buffs:\n" +
                $"{buffList}\n\n" +
                "Admin Given Buffs:\n" +
                $"{adminBuffList}\n\n" +
                $"Stat Type Slots: {usedStatSlots}/{statCap}\n" +
                "Purchased Stats:\n" +
                $"{statList}\n\n" +
                "Admin Flat Stats:\n" +
                $"{adminStatList}\n\n" +
                "Whitelist:\n" +
                $"  Perk Shop: {(config.EnableBuffWhitelist ? (inBuffWhitelist ? "Allowed" : "Blocked") : "Open")} | Listed: {(inBuffWhitelist ? "yes" : "no")}\n" +
                $"  Stat Shop: {(config.EnableStatWhitelist ? (inStatWhitelist ? "Allowed" : "Blocked") : "Open")} | Listed: {(inStatWhitelist ? "yes" : "no")}");
        }
        catch (Exception e)
        {
            Core.LogException(e);
            reply("Could not read target player ownership. Check server logs.");
        }
    }

    public static void AdminAdd(string playerRef, string buffKey, Action<string> reply)
    {
        try
        {
            if (!AdminPlayerLookup.TryFindUser(playerRef, out var userEntity, out var user, out var error))
            {
                reply(error);
                return;
            }

            var config = ConfigService.Shop;
            if (string.IsNullOrWhiteSpace(buffKey))
            {
                reply("Usage: .perk addbuff <playerName|platformId> <buffKey>");
                return;
            }

            buffKey = KeyAliasService.NormalizeBuffKey(buffKey);
            if (!config.Buffs.TryGetValue(buffKey, out var entry))
            {
                reply($"Unknown buff '{buffKey}'. Use .perk bufflist or .perk search <text>.");
                return;
            }

            bool added = OwnershipService.EnsureAdminGrantedBuff(user.PlatformId, buffKey);
            OwnershipService.RegisterOnlineUser(user.PlatformId, userEntity);

            if (entry.BuffPrefab != 0)
            {
                var characterEntity = user.LocalCharacter._Entity;
                if (PlayerStateHelper.Exists(characterEntity))
                {
                    bool isBloodBuff = string.Equals(entry.Category, "blood_buff", StringComparison.OrdinalIgnoreCase);

                    BuffService.ApplyPurchasedBuff(
                        userEntity,
                        characterEntity,
                        new PrefabGUID(entry.BuffPrefab),
                        entry.PreventDuplicate,
                        isBloodBuff ? config.AllowBuffEntityMutation : (config.ForcePermanentBuffs || config.AllowBuffEntityMutation),
                        isBloodBuff ? false : (config.ForcePermanentBuffs || entry.MutateAppliedBuffLifetime),
                        BuffService.ResolveDurationSeconds(config, entry),
                        BuffService.ResolvePersistThroughDeath(config, entry),
                        entry.KeepVisibleTimerFrozen,
                        entry.VisibleTimerSeconds,
                        BuffService.PreserveVanillaCleanup(config, entry));
                }
            }

            reply(added
                ? $"Admin granted {entry.DisplayName} to {user.CharacterName}."
                : $"{user.CharacterName} already had {entry.DisplayName} as an admin-given buff.");
        }
        catch (Exception e)
        {
            Core.LogException(e);
            reply("Could not add buff to target player. Check server logs.");
        }
    }

    public static void AdminRemove(string playerRef, string buffKey, Action<string> reply)
    {
        try
        {
            if (!AdminPlayerLookup.TryFindUser(playerRef, out var userEntity, out var user, out var error))
            {
                reply(error);
                return;
            }

            var config = ConfigService.Shop;
            if (string.IsNullOrWhiteSpace(buffKey))
            {
                reply("Usage: .perk clearbuff <playerName|platformId> <buffKey>");
                return;
            }

            buffKey = KeyAliasService.NormalizeBuffKey(buffKey);
            if (!config.Buffs.TryGetValue(buffKey, out var entry))
            {
                reply($"Unknown buff '{buffKey}'. Use .perk bufflist or .perk search <text>.");
                return;
            }

            bool removedOwnership = OwnershipService.RemoveOwnedBuff(user.PlatformId, buffKey);
            OwnershipService.RegisterOnlineUser(user.PlatformId, userEntity);

            bool removedActive = false;
            if (entry.BuffPrefab != 0)
            {
                var characterEntity = user.LocalCharacter._Entity;
                if (PlayerStateHelper.Exists(characterEntity))
                {
                    removedActive = BuffService.RemoveBuff(characterEntity, new PrefabGUID(entry.BuffPrefab));
                }
            }

            if (removedOwnership || removedActive)
                reply($"Admin cleared {entry.DisplayName} from {user.CharacterName}. Saved ownership removed: {removedOwnership}, active buff removed: {removedActive}.");
            else
                reply($"{user.CharacterName} did not have {entry.DisplayName} as a saved/admin buff or as an active buff.");
        }
        catch (Exception e)
        {
            Core.LogException(e);
            reply("Could not remove buff from target player. Check server logs.");
        }
    }




    public static void AdminGiftBuff(string playerRef, string buffKey, Action<string> reply)
    {
        try
        {
            if (!AdminPlayerLookup.TryFindUser(playerRef, out var userEntity, out var user, out var error))
            {
                reply(error);
                return;
            }

            var config = ConfigService.Shop;
            if (string.IsNullOrWhiteSpace(buffKey))
            {
                reply("Usage: .perk giftbuff <playerName|platformId> <buffKey>");
                return;
            }

            buffKey = KeyAliasService.NormalizeBuffKey(buffKey);
            if (!config.Buffs.TryGetValue(buffKey, out var entry) || !entry.Enabled)
            {
                reply($"Unknown or disabled buff '{buffKey}'.");
                return;
            }

            if (config.Categories.TryGetValue(entry.Category, out var category) && category.MaxOwnedSlots.HasValue)
            {
                int usedSlots = OwnershipService.CountOwnedBuffsInCategory(user.PlatformId, entry.Category);
                bool alreadyOwns = OwnershipService.PlayerOwns(user.PlatformId, buffKey);

                if (!alreadyOwns && usedSlots >= category.MaxOwnedSlots.Value)
                {
                    reply($"Cannot gift {entry.DisplayName}. Category '{category.DisplayName}' is full: {usedSlots}/{category.MaxOwnedSlots.Value}.");
                    return;
                }
            }

            bool added = OwnershipService.EnsureOwnedBuff(user.PlatformId, buffKey);
            OwnershipService.RegisterOnlineUser(user.PlatformId, userEntity);
            PlayerCacheService.Remember(user);

            bool applied = false;
            if (entry.BuffPrefab != 0)
            {
                var characterEntity = user.LocalCharacter._Entity;
                if (PlayerStateHelper.Exists(characterEntity))
                {
                    applied = BuffService.ApplyPurchasedBuff(
                        userEntity,
                        characterEntity,
                        new PrefabGUID(entry.BuffPrefab),
                        entry.PreventDuplicate,
                        config.ForcePermanentBuffs || config.AllowBuffEntityMutation,
                        config.ForcePermanentBuffs || entry.MutateAppliedBuffLifetime,
                        BuffService.ResolveDurationSeconds(config, entry),
                        BuffService.ResolvePersistThroughDeath(config, entry),
                        entry.KeepVisibleTimerFrozen,
                        entry.VisibleTimerSeconds,
                        BuffService.PreserveVanillaCleanup(config, entry));
                }
            }

            reply(added
                ? $"Gifted purchased buff {entry.DisplayName} to {user.CharacterName}. Counts toward slot usage. Applied now: {applied}."
                : $"{user.CharacterName} already has purchased/admin ownership for {entry.DisplayName}. No additional slot was consumed.");
        }
        catch (Exception e)
        {
            Core.LogException(e);
            reply("Could not gift purchased buff. Check server logs.");
        }
    }

    public static void AdminRevokePurchasedBuff(string playerRef, string buffKey, Action<string> reply)
    {
        try
        {
            if (!AdminPlayerLookup.TryFindUser(playerRef, out var userEntity, out var user, out var error))
            {
                reply(error);
                return;
            }

            var config = ConfigService.Shop;
            if (string.IsNullOrWhiteSpace(buffKey))
            {
                reply("Usage: .perk revokebuff <playerName|platformId> <buffKey>");
                return;
            }

            buffKey = KeyAliasService.NormalizeBuffKey(buffKey);
            if (!config.Buffs.TryGetValue(buffKey, out var entry))
            {
                reply($"Unknown buff '{buffKey}'. Use .perk bufflist or .perk search <text>.");
                return;
            }

            bool removedPurchased = OwnershipService.RemovePurchasedBuffOnly(user.PlatformId, buffKey);
            OwnershipService.RegisterOnlineUser(user.PlatformId, userEntity);
            PlayerCacheService.Remember(user);

            bool removedActive = false;
            if (removedPurchased && entry.BuffPrefab != 0)
            {
                bool stillOwnsAdmin = OwnershipService.PlayerOwns(user.PlatformId, buffKey);
                var characterEntity = user.LocalCharacter._Entity;
                if (!stillOwnsAdmin && PlayerStateHelper.Exists(characterEntity))
                    removedActive = BuffService.RemoveBuff(characterEntity, new PrefabGUID(entry.BuffPrefab));
            }

            reply(removedPurchased
                ? $"Revoked purchased buff {entry.DisplayName} from {user.CharacterName}. Active buff removed: {removedActive}."
                : $"{user.CharacterName} did not have purchased ownership for {entry.DisplayName}. Admin-given buffs are not removed by revokebuff.");
        }
        catch (Exception e)
        {
            Core.LogException(e);
            reply("Could not revoke purchased buff. Check server logs.");
        }
    }

    public static void AdminGiftStat(string playerRef, string statKey, int ranks, Action<string> reply)
    {
        try
        {
            if (!AdminPlayerLookup.TryFindUser(playerRef, out var userEntity, out var user, out var error))
            {
                reply(error);
                return;
            }

            var config = ConfigService.Shop;
            if (!config.EnableStatShop)
            {
                reply("Permanent stat shop is disabled.");
                return;
            }

            if (string.IsNullOrWhiteSpace(statKey) || ranks <= 0)
            {
                reply("Usage: .perk giftstat <playerName|platformId> <statKey> <ranks>");
                return;
            }

            statKey = KeyAliasService.NormalizeStatKey(statKey);
            if (!config.Stats.TryGetValue(statKey, out var entry) || !entry.Enabled)
            {
                reply($"Unknown or disabled stat key '{statKey}'. Use .perk statlist or .perk search <text>.");
                return;
            }

            int owned = OwnershipService.GetOwnedStatCount(user.PlatformId, statKey);
            if (entry.MaxPurchases > 0 && owned + ranks > entry.MaxPurchases)
            {
                reply($"Cannot gift {ranks} rank(s) of {entry.DisplayName}. Purchase cap would be exceeded: {owned}/{entry.MaxPurchases} owned.");
                return;
            }

            if (config.EnableStatTypeSlots && config.MaxOwnedStatTypes > 0 && owned == 0)
            {
                int usedTypes = OwnershipService.CountOwnedStatTypes(user.PlatformId);
                if (usedTypes >= config.MaxOwnedStatTypes)
                {
                    reply($"Cannot gift {entry.DisplayName}. Stat type slots are full: {usedTypes}/{config.MaxOwnedStatTypes}.");
                    return;
                }
            }

            int total = OwnershipService.AddOwnedStat(user.PlatformId, statKey, ranks);
            OwnershipService.RegisterOnlineUser(user.PlatformId, userEntity);
            PlayerCacheService.Remember(user);

            bool applied = false;
            var characterEntity = user.LocalCharacter._Entity;
            if (PlayerStateHelper.Exists(characterEntity))
                applied = StatService.RebuildOwnedStats(userEntity, characterEntity, user.PlatformId);

            reply($"Gifted {ranks} purchased rank(s) of {entry.DisplayName} to {user.CharacterName}. Total purchased ranks: {total}. Counts toward stat slots and MaxPurchases. Applied now: {applied}.");
        }
        catch (Exception e)
        {
            Core.LogException(e);
            reply("Could not gift purchased stat. Check server logs.");
        }
    }

    public static void AdminRevokePurchasedStat(string playerRef, string statKey, int ranks, Action<string> reply)
    {
        try
        {
            if (!AdminPlayerLookup.TryFindUser(playerRef, out var userEntity, out var user, out var error))
            {
                reply(error);
                return;
            }

            if (string.IsNullOrWhiteSpace(statKey) || ranks <= 0)
            {
                reply("Usage: .perk revokestat <playerName|platformId> <statKey> <ranks>");
                return;
            }

            statKey = KeyAliasService.NormalizeStatKey(statKey);
            var config = ConfigService.Shop;
            if (!config.Stats.TryGetValue(statKey, out var entry))
            {
                reply($"Unknown stat key '{statKey}'. Use .perk statlist or .perk search <text>.");
                return;
            }

            int before = OwnershipService.GetOwnedStatCount(user.PlatformId, statKey);
            bool removed = OwnershipService.RemoveOwnedStat(user.PlatformId, statKey, ranks);
            int after = OwnershipService.GetOwnedStatCount(user.PlatformId, statKey);

            OwnershipService.RegisterOnlineUser(user.PlatformId, userEntity);
            PlayerCacheService.Remember(user);

            bool applied = false;
            var characterEntity = user.LocalCharacter._Entity;
            if (PlayerStateHelper.Exists(characterEntity))
                applied = StatService.RebuildOwnedStats(userEntity, characterEntity, user.PlatformId);

            reply(removed
                ? $"Revoked {Math.Min(ranks, before)} purchased rank(s) of {entry.DisplayName} from {user.CharacterName}. Remaining ranks: {after}. Applied now: {applied}."
                : $"{user.CharacterName} did not have purchased ranks of {entry.DisplayName}.");
        }
        catch (Exception e)
        {
            Core.LogException(e);
            reply("Could not revoke purchased stat. Check server logs.");
        }
    }

    private static bool TryResolveUnitStatOrShopKey(string input, out UnitStatType unitStat, out string canonicalStat, out string sourceLabel)
    {
        unitStat = default;
        canonicalStat = string.Empty;
        sourceLabel = string.Empty;

        if (string.IsNullOrWhiteSpace(input))
            return false;

        input = input.Trim();
        var config = ConfigService.Shop;

        if (config.Stats.TryGetValue(input, out var statEntry)
            && Enum.TryParse<UnitStatType>(statEntry.UnitStat, ignoreCase: true, out unitStat))
        {
            canonicalStat = unitStat.ToString();
            sourceLabel = $"{input} -> {canonicalStat}";
            return true;
        }

        if (Enum.TryParse<UnitStatType>(input, ignoreCase: true, out unitStat))
        {
            canonicalStat = unitStat.ToString();
            sourceLabel = canonicalStat;
            return true;
        }

        return false;
    }

    public static void AdminAddFlatStat(string playerRef, string unitStatName, float amount, Action<string> reply)
    {
        try
        {
            if (!AdminPlayerLookup.TryFindUser(playerRef, out var userEntity, out var user, out var error))
            {
                reply(error);
                return;
            }

            if (string.IsNullOrWhiteSpace(unitStatName) || Math.Abs(amount) <= 0.0001f)
            {
                reply("Usage: .perk addflat <playerName|platformId> <UnitStat|statKey> <amount>");
                return;
            }

            if (!TryResolveUnitStatOrShopKey(unitStatName, out _, out var canonicalStat, out var sourceLabel))
            {
                reply($"Unknown stat '{unitStatName}'. Use a UnitStat like PhysicalPower or a stat key from .perk statlist like PP.");
                return;
            }

            float total = OwnershipService.AddAdminFlatStat(user.PlatformId, canonicalStat, amount);
            OwnershipService.RegisterOnlineUser(user.PlatformId, userEntity);

            bool applied = false;
            var characterEntity = user.LocalCharacter._Entity;
            if (PlayerStateHelper.Exists(characterEntity))
                applied = StatService.RebuildOwnedStats(userEntity, characterEntity, user.PlatformId);

            reply($"Admin added {amount} flat {canonicalStat} to {user.CharacterName}. Source: {sourceLabel}. Total admin flat {canonicalStat}: +{total}. Applied now: {applied}. This does not consume stat slots or MaxPurchases.");
        }
        catch (Exception e)
        {
            Core.LogException(e);
            reply("Could not add admin flat stat. Check server logs.");
        }
    }

    public static void AdminClearFlatStat(string playerRef, string unitStatName, Action<string> reply)
    {
        try
        {
            if (!AdminPlayerLookup.TryFindUser(playerRef, out var userEntity, out var user, out var error))
            {
                reply(error);
                return;
            }

            if (string.IsNullOrWhiteSpace(unitStatName))
            {
                reply("Usage: .perk clearflat <playerName|platformId> <UnitStat|statKey>");
                return;
            }

            if (!TryResolveUnitStatOrShopKey(unitStatName, out _, out var canonicalStat, out var sourceLabel))
            {
                reply($"Unknown stat '{unitStatName}'. Use a UnitStat like PhysicalPower or a stat key from .perk statlist like PP.");
                return;
            }

            bool removed = OwnershipService.ClearAdminFlatStat(user.PlatformId, canonicalStat);
            OwnershipService.RegisterOnlineUser(user.PlatformId, userEntity);

            bool applied = false;
            var characterEntity = user.LocalCharacter._Entity;
            if (PlayerStateHelper.Exists(characterEntity))
                applied = StatService.RebuildOwnedStats(userEntity, characterEntity, user.PlatformId);

            reply(removed
                ? $"Admin cleared flat {canonicalStat} from {user.CharacterName}. Source: {sourceLabel}. Applied now: {applied}."
                : $"{user.CharacterName} did not have admin flat {canonicalStat}. Source: {sourceLabel}.");
        }
        catch (Exception e)
        {
            Core.LogException(e);
            reply("Could not clear admin flat stat. Check server logs.");
        }
    }

    public static void ListStatKeys(Entity userEntity, int page, Action<string> reply)
    {
        if (!AccessService.CanAccessStatShop(userEntity, reply)) return;
        var config = ConfigService.Shop;
        if (!config.EnableStatShop) { reply("Permanent stat shop is disabled."); return; }

        var lines = config.Stats
            .Where(kv => kv.Value.Enabled && StatDefinitionService.IsClientUnsupportedStatAllowed(config, kv.Value))
            .OrderBy(kv => InferStatGroup(kv.Key, kv.Value.UnitStat))
            .ThenBy(kv => kv.Key)
            .Select(kv => $"  {kv.Key} [{InferStatGroup(kv.Key, kv.Value.UnitStat)}]")
            .ToArray();

        if (lines.Length == 0)
        {
            reply("No permanent stats configured.");
            return;
        }

        var (currentPage, totalPages) = NormalizePage(page, lines.Length);
        var pageLines = lines
            .Skip((currentPage - 1) * ListPageSize)
            .Take(ListPageSize)
            .ToArray();

        var nextText = currentPage < totalPages
            ? $"\nNext page: .perk statlist {currentPage + 1}"
            : string.Empty;

        reply(
            $"=== Stat Keys Page {currentPage}/{totalPages} ===\n" +
            string.Join("\n", pageLines) +
            "\n\nDetails: .perk statdet <key>" +
            nextText);
    }

    public static void StatDetails(Entity userEntity, string key, Action<string> reply)
    {
        if (!AccessService.CanAccessStatShop(userEntity, reply)) return;
        var config = ConfigService.Shop;
        if (!config.EnableStatShop) { reply("Permanent stat shop is disabled."); return; }
        if (string.IsNullOrWhiteSpace(key)) { reply("Usage: .perk statdet <key>"); return; }

        key = KeyAliasService.NormalizeStatKey(key);
        if (!config.Stats.TryGetValue(key, out var entry) || !entry.Enabled)
        {
            reply($"Unknown stat '{key}'. Use .perk statlist or .perk search <text>.");
            return;
        }

        int owned = 0;
        int usedSlots = 0;
        string statCap = "∞";
        if (PlayerStateHelper.Exists(userEntity) && Core.EntityManager.HasComponent<User>(userEntity))
        {
            var user = Core.EntityManager.GetComponentData<User>(userEntity);
            owned = OwnershipService.GetOwnedStatCount(user.PlatformId, key);
            usedSlots = OwnershipService.CountOwnedStatTypes(user.PlatformId);
            statCap = config.EnableStatTypeSlots && config.MaxOwnedStatTypes > 0 ? config.MaxOwnedStatTypes.ToString() : "∞";
        }

        string max = entry.MaxPurchases > 0 ? entry.MaxPurchases.ToString() : "∞";
        float total = owned * entry.ValuePerPurchase;
        string slotText = owned > 0 ? "Uses 1 existing stat type slot" : "Would use 1 new stat type slot";
        string compatibilityText = StatDefinitionService.IsClientUnsupportedStatAllowed(config, entry)
            ? "Enabled"
            : "Disabled by client-compatibility guard. Set EnableClientUnsupportedStats=true to allow gameplay-only/non-TAB stats.";

        reply(
            $"=== {entry.DisplayName} ===\n" +
            $"Key: {key}\n" +
            $"Group: {InferStatGroup(key, entry.UnitStat)}\n" +
            $"Effect: +{FormatNumber(entry.ValuePerPurchase)} {entry.UnitStat} per purchase\n" +
            $"Cost: {entry.Cost} {config.CurrencyName}\n" +
            $"Owned: {owned}/{max}\n" +
            $"Current total: +{FormatNumber(total)} {entry.UnitStat}\n" +
            $"Stat slots: {usedSlots}/{statCap}. {slotText}.\n" +
            $"Client compatibility: {compatibilityText}\n" +
            $"Notes: {entry.Notes}\n" +
            $"Buy: .perk statbuy {key}");
    }

    public static void Search(Entity userEntity, string query, Action<string> reply)
    {
        var config = ConfigService.Shop;
        if (string.IsNullOrWhiteSpace(query))
        {
            reply("Usage: .perk search <text>");
            return;
        }

        query = query.Trim();

        var buffMatches = config.Enabled
            ? config.Buffs
                .Where(kv => kv.Value.Enabled
                    && (config.EnableExperimentalBloodBuffs || !string.Equals(kv.Value.Category, "blood_buff", StringComparison.OrdinalIgnoreCase))
                    &&
                    (kv.Key.Contains(query, StringComparison.OrdinalIgnoreCase)
                     || kv.Value.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
                     || kv.Value.Category.Contains(query, StringComparison.OrdinalIgnoreCase)
                     || kv.Value.Notes.Contains(query, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(kv => kv.Key)
                .Select(kv =>
                {
                    string categoryText = config.Categories.TryGetValue(kv.Value.Category, out var cat) ? cat.DisplayName : kv.Value.Category;
                    return $"  buff:{kv.Key} [{categoryText}]";
                })
                .Take(8)
                .ToArray()
            : Array.Empty<string>();

        var statMatches = config.EnableStatShop
            ? config.Stats
                .Where(kv => kv.Value.Enabled &&
                    StatDefinitionService.IsClientUnsupportedStatAllowed(config, kv.Value) &&
                    (kv.Key.Contains(query, StringComparison.OrdinalIgnoreCase)
                     || kv.Value.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
                     || kv.Value.UnitStat.Contains(query, StringComparison.OrdinalIgnoreCase)
                     || kv.Value.Notes.Contains(query, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(kv => kv.Key)
                .Select(kv => $"  stat:{kv.Key} [{InferStatGroup(kv.Key, kv.Value.UnitStat)}]")
                .Take(8)
                .ToArray()
            : Array.Empty<string>();

        if (buffMatches.Length == 0 && statMatches.Length == 0)
        {
            reply($"No PerkShop results for '{query}'.");
            return;
        }

        reply(
            $"=== Search: {query} ===\n" +
            "Buffs:\n" + (buffMatches.Length == 0 ? "  none" : string.Join("\n", buffMatches)) +
            "\n\nStats:\n" + (statMatches.Length == 0 ? "  none" : string.Join("\n", statMatches)) +
            "\n\nDetails: .perk buffdet <key> or .perk statdet <key>");
    }

    public static void BuyStat(Entity userEntity, Entity characterEntity, string key, Action<string> reply)
    {
        try
        {
            var config = ConfigService.Shop;
            if (!config.Enabled) { reply("Perk shop is disabled."); return; }
            if (!AccessService.CanAccessStatShop(userEntity, reply)) return;
            if (!config.EnableStatShop) { reply("Permanent stat shop is disabled."); return; }
            if (!StatService.IsCarrierConfigured(out _, out var carrierError)) { reply($"Permanent stat shop is not ready: {carrierError}"); return; }
            if (!PlayerStateHelper.Exists(userEntity) || !PlayerStateHelper.Exists(characterEntity)) { reply("Player entity not ready."); return; }
            if (!Core.EntityManager.HasComponent<User>(userEntity)) { reply("User component not ready."); return; }
            if (config.BlockPurchasesInCombat && PlayerStateHelper.IsInCombat(characterEntity)) { reply("You cannot buy stats while in combat."); return; }
            if (string.IsNullOrWhiteSpace(key)) { ListStatKeys(userEntity, 1, reply); return; }

            key = KeyAliasService.NormalizeStatKey(key);
            if (!config.Stats.TryGetValue(key, out var entry) || !entry.Enabled)
            {
                reply($"Unknown stat '{key}'. Use .perk statlist or .perk search <text>.");
                return;
            }

            if (!Enum.TryParse<UnitStatType>(entry.UnitStat, ignoreCase: true, out _))
            {
                reply($"Stat '{key}' has invalid UnitStat '{entry.UnitStat}'. Check config.");
                return;
            }

            if (!StatDefinitionService.IsClientUnsupportedStatAllowed(config, entry))
            {
                reply(
                    $"Stat '{entry.DisplayName}' ({entry.UnitStat}) is disabled by the client-compatibility guard. " +
                    "It has gameplay effect, but Bloodcraft/Eclipse/VampireAttributes can spam NotImplementedException and it may not appear in TAB. " +
                    "Set EnableClientUnsupportedStats = true only if you accept that risk.");
                return;
            }

            var user = Core.EntityManager.GetComponentData<User>(userEntity);
            var platformId = user.PlatformId;
            OwnershipService.RegisterOnlineUser(platformId, userEntity);

            int ownedCount = OwnershipService.GetOwnedStatCount(platformId, key);
            if (entry.MaxPurchases > 0 && ownedCount >= entry.MaxPurchases)
            {
                reply($"You already reached the purchase cap for {entry.DisplayName}: {ownedCount}/{entry.MaxPurchases}.");
                return;
            }

            if (config.EnableStatTypeSlots && ownedCount == 0 && config.MaxOwnedStatTypes > 0)
            {
                int usedStatTypeSlots = OwnershipService.CountOwnedStatTypes(platformId);
                if (usedStatTypeSlots >= config.MaxOwnedStatTypes)
                {
                    reply($"Stat type slots are full: {usedStatTypeSlots}/{config.MaxOwnedStatTypes}. You can still buy more ranks of already-owned stats, but not a new stat type.");
                    return;
                }
            }

            var currency = new PrefabGUID(config.CurrencyPrefab);
            var have = InventoryHelper.Count(characterEntity, currency);
            if (have < entry.Cost)
            {
                reply($"Not enough {config.CurrencyName}: {have}/{entry.Cost}.");
                return;
            }

            if (!InventoryHelper.TryRemove(characterEntity, currency, entry.Cost))
            {
                reply($"Could not remove {entry.Cost} {config.CurrencyName} from your inventory.");
                return;
            }

            int newCount = OwnershipService.AddOwnedStat(platformId, key, 1);
            bool applied = StatService.RebuildOwnedStats(userEntity, characterEntity, platformId);

            if (!applied)
            {
                Core.Log.LogWarning($"[StatShop] Stat purchase '{key}' was saved but stat carrier could not be applied immediately.");
                reply($"Purchased {entry.DisplayName}. Ownership saved, but immediate application failed. It may apply on relog/reapply.");
                return;
            }

            float total = newCount * entry.ValuePerPurchase;
            reply($"Purchased {entry.DisplayName} for {entry.Cost} {config.CurrencyName}. Owned: {newCount}. Total {entry.UnitStat}: +{total}.");
        }
        catch (Exception e)
        {
            Core.LogException(e);
            reply("Permanent stat purchase failed. Check server logs.");
        }
    }

    public static void Sync(Entity userEntity, Action<string> reply)
    {
        try
        {
            bool buffAccess = AccessService.CanAccessPerkShop(userEntity, reply);
            bool statAccess = AccessService.CanAccessStatShop(userEntity, reply);

            var buffResult = default(OwnershipService.UserReapplyResult);
            if (buffAccess)
                buffResult = OwnershipService.ReapplyOwnedBuffsForUser(userEntity, onlyIfMissing: false);

            bool statsAttempted = false;
            bool statsApplied = false;
            if (statAccess)
            {
                statsAttempted = true;
                statsApplied = StatService.ApplyOwnedStatsForUser(userEntity);
            }

            reply(
                "PerkShop sync complete.\n" +
                $"Buff access: {buffAccess}\n" +
                $"Buffs checked: {buffResult.BuffsChecked}\n" +
                $"Buffs already active: {buffResult.BuffsAlreadyActive}\n" +
                $"Buff apply attempts: {buffResult.BuffApplyAttempts}\n" +
                $"Buff apply successes: {buffResult.BuffApplySuccesses}\n" +
                $"Stats attempted: {statsAttempted}\n" +
                $"Stats applied or already current: {statsApplied}");
        }
        catch (Exception e)
        {
            Core.LogException(e);
            reply("Could not sync PerkShop buffs/stats. Check server logs.");
        }
    }


    public static void RemoveStat(Entity userEntity, Entity characterEntity, string key, Action<string> reply)
    {
        try
        {
            var config = ConfigService.Shop;
            if (!AccessService.CanAccessStatShop(userEntity, reply)) return;
            if (!config.EnableStatShop) { reply("Permanent stat shop is disabled."); return; }
            if (!PlayerStateHelper.Exists(userEntity) || !PlayerStateHelper.Exists(characterEntity)) { reply("Player entity not ready."); return; }
            if (!Core.EntityManager.HasComponent<User>(userEntity)) { reply("User component not ready."); return; }
            if (config.BlockRemovalsInCombat && PlayerStateHelper.IsInCombat(characterEntity)) { reply("You cannot remove stats while in combat."); return; }
            if (string.IsNullOrWhiteSpace(key)) { reply("Usage: .perk statremove <key>"); return; }

            key = KeyAliasService.NormalizeStatKey(key);
            if (!config.Stats.TryGetValue(key, out var entry))
            {
                reply($"Unknown stat '{key}'. Use .perk statlist or .perk search <text>.");
                return;
            }

            var user = Core.EntityManager.GetComponentData<User>(userEntity);
            var platformId = user.PlatformId;
            int ownedCount = OwnershipService.GetOwnedStatCount(platformId, key);
            if (ownedCount <= 0)
            {
                reply($"You do not own {entry.DisplayName}.");
                return;
            }

            int removeCost = config.EnableStatTypeSlots ? config.StatTypeSlotFreeCost : 0;

            if (removeCost > 0)
            {
                var currency = new PrefabGUID(config.CurrencyPrefab);
                var have = InventoryHelper.Count(characterEntity, currency);
                if (have < removeCost)
                {
                    reply($"Not enough {config.CurrencyName} to free stat type slot: {have}/{removeCost}.");
                    return;
                }

                if (!InventoryHelper.TryRemove(characterEntity, currency, removeCost))
                {
                    reply($"Could not remove {removeCost} {config.CurrencyName} to free stat type slot.");
                    return;
                }
            }

            bool removed = OwnershipService.RemoveOwnedStat(platformId, key, ownedCount);
            if (!removed)
            {
                reply($"Could not remove {entry.DisplayName}.");
                return;
            }

            StatService.RebuildOwnedStats(userEntity, characterEntity, platformId);

            string costText = removeCost > 0
                ? $" Stat type slot freed for {removeCost} {config.CurrencyName}."
                : " Stat type slot freed.";

            reply($"Removed all {ownedCount} rank(s) of {entry.DisplayName}.{costText}");
        }
        catch (Exception e)
        {
            Core.LogException(e);
            reply("Could not remove permanent stat. Check server logs.");
        }
    }



    public static void ReloadConfig(Action<string> reply)
    {
        try
        {
            OwnershipService.FlushPendingSaves(force: true);
            ConfigService.Reload();
            QueryService.Initialize(Core.Systems);
            CarrierPrefabService.Reset();
            CarrierPrefabService.PrepareConfiguredStatCarrierPrefab();
            StatService.ResetRuntimeStateAfterConfigReload();
            BuffService.ResetRuntimeStateAfterConfigReload();

            var config = ConfigService.Shop;
            string carrier = StatService.DescribeConfiguredCarrier();

            reply(
                "PerkShop config reloaded.\n" +
                $"Version: {MyPluginInfo.PLUGIN_VERSION}\n" +
                $"ConfigVersion: {config.ConfigVersion}\n" +
                $"Debug logging: {config.EnableDebugLogging}\n" +
                $"Permanent buffs: {config.ForcePermanentBuffs}\n" +
                $"Stat carrier: {carrier}\n" +
                "Run .perk syncall to force reapply for online players.");
        }
        catch (Exception e)
        {
            Core.LogException(e);
            reply("PerkShop config reload failed. Check server logs.");
        }
    }

    public static void Diagnostics(Action<string> reply)
    {
        try
        {
            var config = ConfigService.Shop;
            var online = OwnershipService.GetOnlineUsersSnapshot();

            reply(
                "=== PerkShop Diagnostics ===\n" +
                $"Version: {MyPluginInfo.PLUGIN_VERSION}\n" +
                $"ConfigVersion: {config.ConfigVersion}\n" +
                $"Enabled: {config.Enabled}\n" +
                $"Buff shop access: {(config.EnableBuffWhitelist ? $"Whitelist ON ({config.BuffWhitelistPlatformIds.Count})" : "Open")}\n" +
                $"Stat shop enabled: {config.EnableStatShop}\n" +
                $"Stat shop access: {(config.EnableStatWhitelist ? $"Whitelist ON ({config.StatWhitelistPlatformIds.Count})" : "Open")}\n" +
                $"Currency: {config.CurrencyName} ({config.CurrencyPrefab})\n" +
                $"Permanent buffs: {config.ForcePermanentBuffs}\n" +
                $"Debug logging: {config.EnableDebugLogging}\n" +
                $"Config auto-detect: {config.AutoDetectConfigChanges}, check interval: {config.ConfigFileCheckIntervalSeconds:0.##}s\n" +
                $"Reapply missing: {config.ReapplyOwnedBuffsWhenMissing}, every {config.ReapplyCheckIntervalSeconds}s, max users/cycle: {(config.ReapplyMaxUsersPerCycle <= 0 ? "unlimited" : config.ReapplyMaxUsersPerCycle.ToString())}, cursor: {OwnershipService.ReapplyCursor}\n" +
                $"Carrier finalize interval: {config.CarrierFinalizeCheckIntervalSeconds:0.##}s\n" +
                $"Ownership save debounce: {config.OwnershipSaveDebounceSeconds:0.##}s, pending: {OwnershipService.HasPendingSave}, last save: {OwnershipService.LastSaveUtc}\n" +
                $"Player cache entries: {PlayerCacheService.CachedPlayerCount}, save debounce: {config.PlayerCacheSaveDebounceSeconds:0.##}s, pending: {PlayerCacheService.HasPendingSave}\n" +
                $"Online cached users: {online.Count}\n" +
                $"Pending stat carriers: {StatService.PendingCarrierCount}\n" +
                $"Pending permanent buffs: {BuffService.PendingPermanentBuffCount}\n" +
                $"Cached stat hashes: {StatService.CachedStatHashCount}\n" +
                $"Parsed stat definitions: {StatDefinitionService.ParsedStatCount}\n" +
                $"Max health behavior: {config.MaxHealthPurchaseBehavior}\n" +
                $"Stat carrier: {StatService.DescribeConfiguredCarrier()}\n" +
                $"Client attribute aliases: {config.UseClientAttributeStatAliases}\n" +
                $"Blood buffs enabled: {config.EnableExperimentalBloodBuffs}\n" +
                $"Unsupported client stats allowed: {config.EnableClientUnsupportedStats}");
        }
        catch (Exception e)
        {
            Core.LogException(e);
            reply("Could not build PerkShop diagnostics. Check server logs.");
        }
    }

    public static void SyncAll(Action<string> reply)
    {
        try
        {
            OwnershipService.FlushPendingSaves(force: true);
            PlayerCacheService.FlushPendingSaves(force: true);

            var result = OwnershipService.ReapplyOnlineOwnedBuffs(onlyIfMissing: false);

            OwnershipService.FlushPendingSaves(force: true);
            PlayerCacheService.FlushPendingSaves(force: true);

            reply(
                "PerkShop syncall complete.\n" +
                $"Online cached users checked: {result.Checked}\n" +
                $"Valid users processed: {result.Processed}\n" +
                $"Invalid/stale users skipped: {result.SkippedInvalid}\n" +
                $"Buffs checked: {result.BuffsChecked}\n" +
                $"Buffs already active: {result.BuffsAlreadyActive}\n" +
                $"Buff lifetime refreshes: {result.BuffLifetimeRefreshes}\n" +
                $"Buff apply attempts: {result.BuffApplyAttempts}\n" +
                $"Buff apply successes: {result.BuffApplySuccesses}\n" +
                $"Stats attempted: {result.StatsAttempted}\n" +
                $"Stats applied or already current: {result.StatsApplied}\n" +
                $"Pending stat carriers after sync: {StatService.PendingCarrierCount}");
        }
        catch (Exception e)
        {
            Core.LogException(e);
            reply("PerkShop syncall failed. Check server logs.");
        }
    }

    public static void Validate(Action<string> reply)
    {
        try
        {
            var config = ConfigService.Shop;
            var warnings = new List<string>();
            var errors = new List<string>();

            if (!StatService.IsCarrierConfigured(out _, out var carrierError))
                errors.Add(carrierError);

            if (config.StatCarrierBuffPrefab == 1774716596)
                errors.Add("StatCarrierBuffPrefab uses Bloodcraft's SetBonus_AllLeech_T09 carrier.");

            if (config.CurrencyPrefab == 0)
                errors.Add("CurrencyPrefab is 0.");

            foreach (var (key, entry) in config.Stats)
            {
                if (entry == null)
                {
                    errors.Add($"Stat '{key}' has a null entry.");
                    continue;
                }

                if (!entry.Enabled)
                    continue;

                if (!StatDefinitionService.TryGetParsedStat(key, out _))
                    errors.Add($"Stat '{key}' is enabled but has invalid UnitStat/ModificationType/AttributeCapType.");

                if (entry.Cost <= 0)
                    errors.Add($"Stat '{key}' has non-positive cost.");

                if (entry.MaxPurchases < 0)
                    errors.Add($"Stat '{key}' has negative MaxPurchases.");

                if (StatDefinitionService.IsDangerousModifierCombo(entry))
                    warnings.Add($"Stat '{key}' uses MovementSpeed + Add. Prefer MultiplyBaseAdd.");

                if (!config.EnableClientUnsupportedStats && !StatDefinitionService.IsClientTabSafeStat(config, entry))
                    warnings.Add($"Stat '{key}' ({entry.UnitStat}) is enabled in config but suppressed by EnableClientUnsupportedStats=false to avoid Bloodcraft/Eclipse VampireAttributes spam. If aliases are enabled, PerkShop will test the resolved client-attribute stat instead.");
            }

            var seenBuffPrefabs = new Dictionary<int, string>();
            foreach (var (key, entry) in config.Buffs)
            {
                if (entry == null)
                {
                    errors.Add($"Buff '{key}' has a null entry.");
                    continue;
                }

                if (!entry.Enabled)
                    continue;

                if (entry.BuffPrefab == 0)
                    errors.Add($"Buff '{key}' has BuffPrefab 0.");

                if (!config.Categories.ContainsKey(entry.Category))
                    errors.Add($"Buff '{key}' references missing category '{entry.Category}'.");

                if (entry.Cost <= 0)
                    errors.Add($"Buff '{key}' has non-positive cost.");

                if (entry.BuffPrefab != 0)
                {
                    if (seenBuffPrefabs.TryGetValue(entry.BuffPrefab, out var otherKey))
                        warnings.Add($"Buff '{key}' and '{otherKey}' share prefab {entry.BuffPrefab}.");
                    else
                        seenBuffPrefabs[entry.BuffPrefab] = key;
                }

                string lowered = $"{key} {entry.DisplayName}".ToLowerInvariant();
                if (lowered.Contains("shapeshift") || lowered.Contains("travel") || lowered.Contains("summon") || lowered.Contains("channel"))
                    warnings.Add($"Buff '{key}' may be a scripted/exotic buff. Test carefully before selling it permanently.");
            }

            string warningText = warnings.Count == 0 ? "none" : string.Join("\n", warnings.Take(15).Select(w => "  - " + w));
            string errorText = errors.Count == 0 ? "none" : string.Join("\n", errors.Take(15).Select(e => "  - " + e));

            reply(
                "=== PerkShop Config Validation ===\n" +
                $"Errors: {errors.Count}\n" +
                errorText + "\n" +
                $"Warnings: {warnings.Count}\n" +
                warningText + "\n" +
                $"Parsed stat definitions: {StatDefinitionService.ParsedStatCount}\n" +
                $"Buff entries: {config.Buffs.Count}\n" +
                $"Stat entries: {config.Stats.Count}");
        }
        catch (Exception e)
        {
            Core.LogException(e);
            reply("PerkShop validation failed. Check server logs.");
        }
    }

    public static void WhitelistStatus(Action<string> reply)
    {
        var config = ConfigService.Shop;

        string buffState = config.EnableBuffWhitelist
            ? $"ON ({config.BuffWhitelistPlatformIds.Count} player(s))"
            : $"OFF ({config.BuffWhitelistPlatformIds.Count} saved player(s))";

        string statState = config.EnableStatWhitelist
            ? $"ON ({config.StatWhitelistPlatformIds.Count} player(s))"
            : $"OFF ({config.StatWhitelistPlatformIds.Count} saved player(s))";

        reply(
            "=== Whitelist Status ===\n" +
            $"Buff Whitelist: {buffState}\n" +
            $"Stat Whitelist: {statState}");
    }

    public static void WhitelistAdd(string shopType, string playerRef, Action<string> reply)
    {
        if (!TryResolveWhitelistTarget(playerRef, out var platformId, out var characterName, out var error))
        {
            reply(error);
            return;
        }

        bool changed = ConfigService.UpdateWhitelist(shopType, platformId, add: true, characterName);
        string label = string.IsNullOrWhiteSpace(characterName) ? platformId.ToString() : $"{characterName} ({platformId})";
        reply(changed
            ? $"Added {label} to {shopType} whitelist."
            : $"{label} is already in {shopType} whitelist.");
    }

    public static void WhitelistRemove(string shopType, string playerRef, Action<string> reply)
    {
        if (!TryResolveWhitelistTarget(playerRef, out var platformId, out var characterName, out var error))
        {
            reply(error);
            return;
        }

        bool changed = ConfigService.UpdateWhitelist(shopType, platformId, add: false);
        string label = string.IsNullOrWhiteSpace(characterName) ? platformId.ToString() : $"{characterName} ({platformId})";
        reply(changed
            ? $"Removed {label} from {shopType} whitelist."
            : $"{label} was not in {shopType} whitelist.");
    }

    private static bool TryResolveWhitelistTarget(string playerRef, out ulong platformId, out string characterName, out string error)
    {
        platformId = 0;
        characterName = string.Empty;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(playerRef))
        {
            error = "Usage: .perk wladdbuff <platformId|onlineCharacterName>, .perk wladdstat <platformId|onlineCharacterName>, .perk wlremovebuff <platformId|onlineCharacterName>, or .perk wlremovestat <platformId|onlineCharacterName>";
            return false;
        }

        playerRef = playerRef.Trim();

        if (ulong.TryParse(playerRef, out platformId) && platformId != 0)
        {
            if (AdminPlayerLookup.TryFindUser(playerRef, out _, out var numericUser, out _))
            {
                characterName = numericUser.CharacterName.ToString();
                PlayerCacheService.Remember(platformId, characterName);
            }
            else if (PlayerCacheService.TryGetName(platformId, out var cachedName))
            {
                characterName = cachedName;
            }

            return true;
        }

        if (!AdminPlayerLookup.TryFindUser(playerRef, out _, out var user, out error))
            return false;

        platformId = user.PlatformId;
        characterName = user.CharacterName.ToString();
        PlayerCacheService.Remember(platformId, characterName);

        if (platformId == 0)
        {
            error = $"Could not resolve PlatformId for '{playerRef}'.";
            return false;
        }

        return true;
    }

    public static void ListBuffKeys(Entity userEntity, int page, Action<string> reply)
    {
        if (!AccessService.CanAccessPerkShop(userEntity, reply)) return;
        var config = ConfigService.Shop;
        if (!config.Enabled) { reply("Perk shop is disabled."); return; }

        var lines = config.Buffs
            .Where(kv => kv.Value.Enabled)
            .Where(kv => config.EnableExperimentalBloodBuffs || !string.Equals(kv.Value.Category, "blood_buff", StringComparison.OrdinalIgnoreCase))
            .OrderBy(kv => config.Categories.TryGetValue(kv.Value.Category, out var cat) ? cat.DisplayName : kv.Value.Category)
            .ThenBy(kv => kv.Key)
            .Select(kv =>
            {
                string category = config.Categories.TryGetValue(kv.Value.Category, out var cat)
                    ? cat.DisplayName
                    : kv.Value.Category;

                return $"  {kv.Key} [{category}]";
            })
            .ToArray();

        if (lines.Length == 0)
        {
            reply("No buffs configured.");
            return;
        }

        var (currentPage, totalPages) = NormalizePage(page, lines.Length);
        var pageLines = lines
            .Skip((currentPage - 1) * ListPageSize)
            .Take(ListPageSize)
            .ToArray();

        var nextText = currentPage < totalPages
            ? $"\nNext page: .perk bufflist {currentPage + 1}"
            : string.Empty;

        reply(
            $"=== Buff Keys Page {currentPage}/{totalPages} ===\n" +
            string.Join("\n", pageLines) +
            "\n\nDetails: .perk buffdet <key>" +
            nextText);
    }

    public static void BuffDetails(Entity userEntity, string key, Action<string> reply)
    {
        if (!AccessService.CanAccessPerkShop(userEntity, reply)) return;
        var config = ConfigService.Shop;
        if (!config.Enabled) { reply("Perk shop is disabled."); return; }
        if (string.IsNullOrWhiteSpace(key))
        {
            reply("Usage: .perk buffdet <key>");
            return;
        }

        key = KeyAliasService.NormalizeBuffKey(key);
        if (!config.Buffs.TryGetValue(key, out var entry) || !entry.Enabled)
        {
            reply($"Unknown buff '{key}'. Use .perk bufflist or .perk search <text>.");
            return;
        }

        string categoryText = config.Categories.TryGetValue(entry.Category, out var category)
            ? category.DisplayName
            : entry.Category;

        string effect = string.IsNullOrWhiteSpace(entry.Notes)
            ? "No effect description configured."
            : entry.Notes;

        bool owned = false;
        string slotLine = "Slot: no cap configured";
        if (PlayerStateHelper.Exists(userEntity) && Core.EntityManager.HasComponent<User>(userEntity))
        {
            var user = Core.EntityManager.GetComponentData<User>(userEntity);
            owned = OwnershipService.PlayerOwns(user.PlatformId, key);

            if (config.Categories.TryGetValue(entry.Category, out var categoryDef))
            {
                int used = OwnershipService.CountOwnedBuffsInCategory(user.PlatformId, entry.Category);
                string cap = categoryDef.MaxOwnedSlots.HasValue ? categoryDef.MaxOwnedSlots.Value.ToString() : "∞";
                slotLine = $"Slot: {categoryDef.DisplayName} {used}/{cap}";
            }
        }

        reply(
            $"=== {entry.DisplayName} ===\n" +
            $"Key: {key}\n" +
            $"Category: {categoryText}\n" +
            $"Effect: {effect}\n" +
            $"Cost: {entry.Cost} {config.CurrencyName}\n" +
            $"Owned: {(owned ? "Yes" : "No")}\n" +
            $"{slotLine}\n" +
            $"Buy: .perk buffbuy {key}\n" +
            $"Remove: .perk buffremove {key}");
    }

}
