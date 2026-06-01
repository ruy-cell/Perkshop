using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using BepInEx;
using PerkShop.Models;

namespace PerkShop.Services;

internal static class ConfigService
{
    private static readonly object Lock = new();
    internal static readonly string ConfigDir = Path.Combine(Paths.ConfigPath, MyPluginInfo.PLUGIN_NAME);
    private static readonly string ConfigFile = Path.Combine(ConfigDir, "perkconfig.json");
    private static DateTime _lastWrite = DateTime.MinValue;
    private static DateTime _nextConfigFileCheckUtc = DateTime.MinValue;
    private static ShopConfigRoot _root = DefaultConfig();

    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        WriteIndented = true
    };

    public static void Initialize() => Load(force: true);
    public static void Reload() => Load(force: true);
    public static ShopConfigRoot Shop => GetShop();
    public static bool DebugLoggingEnabled
    {
        get
        {
            try { return Shop.EnableDebugLogging; }
            catch { return false; }
        }
    }
    public static bool UpdateWhitelist(string shopType, ulong platformId, bool add, string displayName = "")
    {
        if (platformId == 0) return false;

        lock (Lock)
        {
            Load(force: true);
            _root = Normalize(_root);

            bool isStat = string.Equals(shopType, "stat", StringComparison.OrdinalIgnoreCase);
            List<ulong> list = isStat
                ? _root.StatWhitelistPlatformIds
                : _root.BuffWhitelistPlatformIds;

            Dictionary<ulong, string> names = isStat
                ? _root.StatWhitelistNames
                : _root.BuffWhitelistNames;

            bool contains = list.Contains(platformId);
            if (add)
            {
                if (!contains)
                {
                    list.Add(platformId);
                    list.Sort();
                }

                if (!string.IsNullOrWhiteSpace(displayName))
                    names[platformId] = displayName.Trim();

                File.WriteAllText(ConfigFile, JsonSerializer.Serialize(_root, JsonOptions));
                _lastWrite = File.GetLastWriteTime(ConfigFile);
                return !contains;
            }
            else
            {
                if (!contains) return false;
                list.Remove(platformId);
                names.Remove(platformId);
            }

            File.WriteAllText(ConfigFile, JsonSerializer.Serialize(_root, JsonOptions));
            _lastWrite = File.GetLastWriteTime(ConfigFile);
            return true;
        }
    }


    private static ShopConfigRoot GetShop()
    {
        Load(force: false);
        lock (Lock) return _root;
    }

    private static void Load(bool force)
    {
        lock (Lock)
        {
            try
            {
                Directory.CreateDirectory(ConfigDir);
                if (!File.Exists(ConfigFile))
                {
                    _root = Normalize(DefaultConfig());
                    StatDefinitionService.RebuildCache(_root);
                    File.WriteAllText(ConfigFile, JsonSerializer.Serialize(_root, JsonOptions));
                    _lastWrite = File.GetLastWriteTime(ConfigFile);
                    _nextConfigFileCheckUtc = DateTime.UtcNow.AddSeconds(Math.Max(1f, _root.ConfigFileCheckIntervalSeconds));
                    return;
                }

                if (!force)
                {
                    if (!_root.AutoDetectConfigChanges)
                        return;

                    if (DateTime.UtcNow < _nextConfigFileCheckUtc)
                        return;

                    _nextConfigFileCheckUtc = DateTime.UtcNow.AddSeconds(Math.Max(1f, _root.ConfigFileCheckIntervalSeconds));
                }

                var writeTime = File.GetLastWriteTime(ConfigFile);
                if (!force && writeTime <= _lastWrite) return;

                _root = JsonSerializer.Deserialize<ShopConfigRoot>(File.ReadAllText(ConfigFile), JsonOptions) ?? DefaultConfig();

                if (ConfigMigrationService.TryMigrate(_root, out var migrated))
                {
                    _root = Normalize(migrated);
                    StatDefinitionService.RebuildCache(_root);
                    File.WriteAllText(ConfigFile, JsonSerializer.Serialize(_root, JsonOptions));
                    writeTime = File.GetLastWriteTime(ConfigFile);
                    Core.Log.LogInfo($"[ConfigService] Migrated perkconfig.json to version {_root.ConfigVersion}.");
                }
                else
                {
                    _root = Normalize(_root);
                    StatDefinitionService.RebuildCache(_root);
                }

                _lastWrite = writeTime;
                _nextConfigFileCheckUtc = DateTime.UtcNow.AddSeconds(Math.Max(1f, _root.ConfigFileCheckIntervalSeconds));
            }
            catch (Exception e)
            {
                Core.LogException(e);
                _root = Normalize(DefaultConfig());
                StatDefinitionService.RebuildCache(_root);
                _nextConfigFileCheckUtc = DateTime.UtcNow.AddSeconds(Math.Max(1f, _root.ConfigFileCheckIntervalSeconds));
            }
        }
    }

    private static ShopConfigRoot Normalize(ShopConfigRoot cfg)
    {
        cfg ??= DefaultConfig();
        if (cfg.ConfigVersion <= 0) cfg.ConfigVersion = StatDefinitionService.CurrentConfigVersion;
        cfg.CurrencyName = string.IsNullOrWhiteSpace(cfg.CurrencyName) ? "Currency" : cfg.CurrencyName.Trim();
        if (cfg.CurrencyPrefab == 0) cfg.CurrencyPrefab = 576389135;
        if (cfg.ReapplyCheckIntervalSeconds < 3) cfg.ReapplyCheckIntervalSeconds = 3;
        if (cfg.ReapplyMaxUsersPerCycle < 0) cfg.ReapplyMaxUsersPerCycle = 0;
        if (cfg.CarrierFinalizeCheckIntervalSeconds < 0.05f) cfg.CarrierFinalizeCheckIntervalSeconds = 0.05f;
        if (cfg.OwnershipSaveDebounceSeconds < 0f) cfg.OwnershipSaveDebounceSeconds = 0f;
        if (cfg.ConfigFileCheckIntervalSeconds < 1f) cfg.ConfigFileCheckIntervalSeconds = 5f;
        if (cfg.PlayerCacheSaveDebounceSeconds < 0f) cfg.PlayerCacheSaveDebounceSeconds = 0f;
        if (cfg.RenewableTimedBuffSeconds < 60) cfg.RenewableTimedBuffSeconds = 7200;
        cfg.RenewableTimedBuffCategories ??= new List<string> { "potion", "elixir", "blood_buff" };
        cfg.RenewableTimedBuffCategories = cfg.RenewableTimedBuffCategories
            .Where(category => !string.IsNullOrWhiteSpace(category))
            .Select(category => category.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (cfg.RenewableTimedBuffCategories.Count == 0)
            cfg.RenewableTimedBuffCategories.AddRange(new[] { "potion", "elixir", "blood_buff" });

        // Permanent-buff policy. This is intentionally forced so the shop behaves as a permanent perk shop.
        if (cfg.ForcePermanentBuffs)
            cfg.AllowBuffEntityMutation = true;

        cfg.BuffWhitelistPlatformIds ??= new List<ulong>();
        cfg.StatWhitelistPlatformIds ??= new List<ulong>();
        cfg.BuffWhitelistNames ??= new Dictionary<ulong, string>();
        cfg.StatWhitelistNames ??= new Dictionary<ulong, string>();
        cfg.BuffWhitelistPlatformIds = cfg.BuffWhitelistPlatformIds.Where(id => id != 0).Distinct().OrderBy(id => id).ToList();
        cfg.StatWhitelistPlatformIds = cfg.StatWhitelistPlatformIds.Where(id => id != 0).Distinct().OrderBy(id => id).ToList();
        cfg.BuffWhitelistNames = cfg.BuffWhitelistNames
            .Where(kv => kv.Key != 0 && !string.IsNullOrWhiteSpace(kv.Value))
            .ToDictionary(kv => kv.Key, kv => kv.Value.Trim());
        cfg.StatWhitelistNames = cfg.StatWhitelistNames
            .Where(kv => kv.Key != 0 && !string.IsNullOrWhiteSpace(kv.Value))
            .ToDictionary(kv => kv.Key, kv => kv.Value.Trim());

        if (cfg.MaxOwnedStatTypes < 0) cfg.MaxOwnedStatTypes = 0;
        if (cfg.StatTypeSlotFreeCost < 0) cfg.StatTypeSlotFreeCost = 0;
        cfg.MaxHealthPurchaseBehavior = string.IsNullOrWhiteSpace(cfg.MaxHealthPurchaseBehavior)
            ? "ClampOnly"
            : cfg.MaxHealthPurchaseBehavior.Trim();

        if (!string.Equals(cfg.MaxHealthPurchaseBehavior, "ClampOnly", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(cfg.MaxHealthPurchaseBehavior, "FillToMax", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(cfg.MaxHealthPurchaseBehavior, "PreserveRatio", StringComparison.OrdinalIgnoreCase))
        {
            cfg.MaxHealthPurchaseBehavior = "ClampOnly";
        }

        cfg.Stats ??= new Dictionary<string, StatShopEntry>(StringComparer.OrdinalIgnoreCase);
        var normalizedStats = new Dictionary<string, StatShopEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, entry) in cfg.Stats)
        {
            if (string.IsNullOrWhiteSpace(key) || entry == null) continue;
            entry.DisplayName = string.IsNullOrWhiteSpace(entry.DisplayName) ? key.Trim() : entry.DisplayName.Trim();
            entry.UnitStat = string.IsNullOrWhiteSpace(entry.UnitStat) ? "PhysicalPower" : entry.UnitStat.Trim();
            entry.ModificationType = string.IsNullOrWhiteSpace(entry.ModificationType) ? "Add" : entry.ModificationType.Trim();
            entry.AttributeCapType = string.IsNullOrWhiteSpace(entry.AttributeCapType) ? "Uncapped" : entry.AttributeCapType.Trim();
            if (entry.Cost <= 0) entry.Cost = 1;
            if (entry.ValuePerPurchase == 0f) entry.ValuePerPurchase = 1f;
            if (entry.MaxPurchases < 0) entry.MaxPurchases = 0;
            entry.Notes = string.IsNullOrWhiteSpace(entry.Notes) ? "Permanent stat purchase." : entry.Notes.Trim();
            StatDefinitionService.NormalizeKnownStatEntry(key.Trim(), entry);
            normalizedStats[key.Trim()] = entry;
        }
        cfg.Stats = normalizedStats;

        cfg.Categories ??= new Dictionary<string, BuffCategoryDefinition>(StringComparer.OrdinalIgnoreCase);
        var normalizedCategories = new Dictionary<string, BuffCategoryDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, category) in cfg.Categories)
        {
            if (string.IsNullOrWhiteSpace(key) || category == null) continue;
            category.DisplayName = string.IsNullOrWhiteSpace(category.DisplayName) ? key.Trim() : category.DisplayName.Trim();
            category.Documentation = string.IsNullOrWhiteSpace(category.Documentation) ? string.Empty : category.Documentation.Trim();
            if (category.MaxOwnedSlots.HasValue && category.MaxOwnedSlots.Value <= 0)
                category.MaxOwnedSlots = null;
            if (category.SlotFreeCost.HasValue && category.SlotFreeCost.Value <= 0)
                category.SlotFreeCost = null;
            normalizedCategories[key.Trim()] = category;
        }
        // Bloodcraft-compatible defaults:
        // - potion and elixir are separated.
        // - elixir is capped at 1 slot to avoid fighting vanilla/Bloodcraft elixir replacement/stacking behavior.
        EnsureCategory(normalizedCategories, "blood_buff", "Blood Buff", "Renewable 2-hour blood-buff effects. Default limit: five blood packages.", 5, 250);
        EnsureCategory(normalizedCategories, "set_bonus", "Set Bonus", "Armor set style bonuses or set-derived effects.", null, null);
        EnsureCategory(normalizedCategories, "potion", "Potion", "Potion and brew-style buffs. Safer for multi-slot ownership.", 3, 100);
        EnsureCategory(normalizedCategories, "elixir", "Elixir", "Elixir-style buffs. Bloodcraft compatibility default: one owned elixir slot.", 1, 500);
        EnsureCategory(normalizedCategories, "misc", "Misc", "Fallback category for uncategorized buffs.", null, null);

        // v0.1.1: blood buffs are intended to allow up to five owned blood-buff slots by default.
        // Preserve custom values other than the previous one-slot default.
        if (normalizedCategories.TryGetValue("blood_buff", out var bloodBuffCategory)
            && bloodBuffCategory.MaxOwnedSlots == 1)
        {
            bloodBuffCategory.MaxOwnedSlots = 5;
            bloodBuffCategory.Documentation = "Renewable 2-hour blood-buff effects. Default limit: five blood packages.";
        }
        cfg.Categories = normalizedCategories;

        cfg.Buffs ??= new Dictionary<string, PerkShopEntry>(StringComparer.OrdinalIgnoreCase);
        var normalizedBuffs = new Dictionary<string, PerkShopEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, entry) in cfg.Buffs)
        {
            if (string.IsNullOrWhiteSpace(key) || entry == null) continue;
            entry.DisplayName = string.IsNullOrWhiteSpace(entry.DisplayName) ? key.Trim() : entry.DisplayName.Trim();
            entry.Category = string.IsNullOrWhiteSpace(entry.Category) ? "misc" : entry.Category.Trim();
            if (!cfg.Categories.ContainsKey(entry.Category))
                entry.Category = "misc";
            if (entry.Cost <= 0) entry.Cost = 1;

            if (cfg.ForcePermanentBuffs)
            {
                entry.PersistentPurchase = true;
                entry.KeepVisibleTimerFrozen = false;
                entry.VisibleTimerSeconds = 0;
                entry.MutateAppliedBuffLifetime = true;

                if (IsRenewableTimedCategory(cfg, entry.Category))
                {
                    // Renewable timed mode: leave vanilla cleanup intact and use ownership reapply
                    // to restore the buff after the 2-hour countdown expires.
                    entry.DurationSeconds = cfg.RenewableTimedBuffSeconds;
                    entry.PersistThroughDeath = cfg.RenewableTimedBuffsPersistThroughDeath;
                }
                else
                {
                    // No-countdown permanent mode for passive/simple buffs.
                    entry.DurationSeconds = -1;
                    entry.PersistThroughDeath = true;
                }
            }
            else
            {
                if (entry.DurationSeconds < -1) entry.DurationSeconds = -1;
                if (entry.VisibleTimerSeconds < 0) entry.VisibleTimerSeconds = 0;
                if (!entry.MutateAppliedBuffLifetime) entry.PersistThroughDeath = false;
            }

            normalizedBuffs[key.Trim()] = entry;
        }
        cfg.Buffs = normalizedBuffs;
        return cfg;
    }


    internal static bool IsRenewableTimedCategory(ShopConfigRoot cfg, string category)
    {
        if (cfg == null || !cfg.UseRenewableTimedBuffs || string.IsNullOrWhiteSpace(category))
            return false;

        return cfg.RenewableTimedBuffCategories != null
            && cfg.RenewableTimedBuffCategories.Any(c => string.Equals(c, category, StringComparison.OrdinalIgnoreCase));
    }

    internal static int ResolveBuffDuration(ShopConfigRoot cfg, PerkShopEntry entry)
    {
        if (cfg != null && entry != null && IsRenewableTimedCategory(cfg, entry.Category))
            return Math.Max(60, cfg.RenewableTimedBuffSeconds);

        return cfg != null && cfg.ForcePermanentBuffs && !(entry.KeepVisibleTimerFrozen && entry.VisibleTimerSeconds > 0)
            ? -1
            : entry.DurationSeconds;
    }

    internal static bool ResolveBuffPersistThroughDeath(ShopConfigRoot cfg, PerkShopEntry entry)
    {
        if (cfg != null && entry != null && IsRenewableTimedCategory(cfg, entry.Category))
            return cfg.RenewableTimedBuffsPersistThroughDeath;

        return (cfg != null && cfg.ForcePermanentBuffs) || entry.PersistThroughDeath;
    }

    internal static bool PreserveVanillaBuffCleanup(ShopConfigRoot cfg, PerkShopEntry entry)
        => cfg != null && entry != null && IsRenewableTimedCategory(cfg, entry.Category);

    private static void EnsureCategory(
        Dictionary<string, BuffCategoryDefinition> categories,
        string key,
        string displayName,
        string documentation,
        int? maxOwnedSlots,
        int? slotFreeCost)
    {
        if (!categories.TryGetValue(key, out var category) || category == null)
        {
            categories[key] = new BuffCategoryDefinition
            {
                DisplayName = displayName,
                Documentation = documentation,
                MaxOwnedSlots = maxOwnedSlots,
                SlotFreeCost = slotFreeCost
            };
            return;
        }

        category.DisplayName = string.IsNullOrWhiteSpace(category.DisplayName) ? displayName : category.DisplayName.Trim();
        category.Documentation = string.IsNullOrWhiteSpace(category.Documentation) ? documentation : category.Documentation.Trim();

        if (category.MaxOwnedSlots.HasValue && category.MaxOwnedSlots.Value <= 0)
            category.MaxOwnedSlots = null;

        if (category.SlotFreeCost.HasValue && category.SlotFreeCost.Value <= 0)
            category.SlotFreeCost = null;
    }

    internal static ShopConfigRoot DefaultConfigForMigration() => DefaultConfig();

    private static ShopConfigRoot DefaultConfig() => new()
    {
        ConfigVersion = StatDefinitionService.CurrentConfigVersion,
        Enabled = true,
        EnableDebugLogging = false,
        CurrencyPrefab = 576389135,
        CurrencyName = "Greater Stygian Shards",
        BlockPurchasesInCombat = true,
        BlockRemovalsInCombat = true,
        SaveOwnership = true,
        ReapplyOwnedBuffsOnLogin = false,
        ReapplyOwnedBuffsWhenMissing = false,
        ReapplyCheckIntervalSeconds = 60,
        ReapplyMaxUsersPerCycle = 5,
        CarrierFinalizeCheckIntervalSeconds = 0.25f,
        OwnershipSaveDebounceSeconds = 2f,
        AutoDetectConfigChanges = false,
        ConfigFileCheckIntervalSeconds = 5f,
        PlayerCacheSaveDebounceSeconds = 30f,
        ForcePermanentBuffs = true,
        AllowBuffEntityMutation = true,
        UseRenewableTimedBuffs = true,
        RenewableTimedBuffSeconds = 7200,
        RenewableTimedBuffCategories = new List<string> { "potion", "elixir", "blood_buff" },
        RenewableTimedBuffsPersistThroughDeath = false,
        EnableStatShop = true,
        EnableClientUnsupportedStats = true,
        UseClientAttributeStatAliases = false,
        EnableExperimentalBloodBuffs = true,
        StatCarrierBuffPrefab = -809648681,
        EnableStatTypeSlots = true,
        MaxOwnedStatTypes = 4,
        StatTypeSlotFreeCost = 500,
        MaxHealthPurchaseBehavior = "ClampOnly",
        EnableBuffWhitelist = false,
        BuffWhitelistPlatformIds = new List<ulong>(),
        BuffWhitelistNames = new Dictionary<ulong, string>(),
        EnableStatWhitelist = false,
        StatWhitelistPlatformIds = new List<ulong>(),
        StatWhitelistNames = new Dictionary<ulong, string>(),
        Stats = new Dictionary<string, StatShopEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["MH"] = new()
            {
                DisplayName = "Max Health",
                UnitStat = "MaxHealth",
                ModificationType = "Add",
                AttributeCapType = "Uncapped",
                ValuePerPurchase = 25.0f,
                Cost = 500,
                MaxPurchases = 10,
                Notes = "Permanently increases Max Health by 25 per purchase."},
            ["PP"] = new()
            {
                DisplayName = "Physical Power",
                UnitStat = "PhysicalPower",
                ModificationType = "Add",
                AttributeCapType = "Uncapped",
                ValuePerPurchase = 2f,
                Cost = 500,
                MaxPurchases = 10,
                Notes = "Permanently increases Physical Power by 2 per purchase."},
            ["SP"] = new()
            {
                DisplayName = "Spell Power",
                UnitStat = "SpellPower",
                ModificationType = "Add",
                AttributeCapType = "Uncapped",
                ValuePerPurchase = 1.0f,
                Cost = 500,
                MaxPurchases = 10,
                Notes = "Permanently increases Spell Power by 1 per purchase."},
            ["MS"] = new()
            {
                DisplayName = "Movement Speed",
                UnitStat = "MovementSpeed",
                ModificationType = "MultiplyBaseAdd",
                AttributeCapType = "Uncapped",
                ValuePerPurchase = 0.05f,
                Cost = 500,
                MaxPurchases = 5,
                Notes = "Permanently increases Movement Speed by 0.05 per purchase. Uses MultiplyBaseAdd for HUD/stat compatibility."},
            ["AS"] = new()
            {
                DisplayName = "Primary Attack Speed",
                UnitStat = "PrimaryAttackSpeed",
                ModificationType = "Add",
                AttributeCapType = "Uncapped",
                ValuePerPurchase = 0.02f,
                Cost = 500,
                MaxPurchases = 5,
                Notes = "Permanently increases Primary Attack Speed by 0.02 per purchase."},
            ["phll"] = new()
            {
                DisplayName = "Physical Life Leech",
                UnitStat = "PhysicalLifeLeech",
                ModificationType = "Add",
                AttributeCapType = "Uncapped",
                ValuePerPurchase = 0.02f,
                Cost = 500,
                MaxPurchases = 5,
                Notes = "Permanently increases Physical Life Leech by 0.02 per purchase."},
            ["sll"] = new()
            {
                DisplayName = "Spell Life Leech",
                UnitStat = "SpellLifeLeech",
                ModificationType = "Add",
                AttributeCapType = "Uncapped",
                ValuePerPurchase = 0.02f,
                Cost = 500,
                MaxPurchases = 5,
                Notes = "Permanently increases Spell Life Leech by 0.02 per purchase."},
            ["prll"] = new()
            {
                DisplayName = "Primary Life Leech",
                UnitStat = "PrimaryLifeLeech",
                ModificationType = "Add",
                AttributeCapType = "Uncapped",
                ValuePerPurchase = 0.03f,
                Cost = 500,
                MaxPurchases = 5,
                Notes = "Permanently increases Primary Life Leech by 0.03 per purchase."},
            ["PCC"] = new()
            {
                DisplayName = "Physical Critical Strike Chance",
                UnitStat = "PhysicalCriticalStrikeChance",
                ModificationType = "Add",
                AttributeCapType = "Uncapped",
                ValuePerPurchase = 0.02f,
                Cost = 500,
                MaxPurchases = 5,
                Notes = "Permanently increases Physical Critical Strike Chance by 0.02 per purchase."},
            ["PCD"] = new()
            {
                DisplayName = "Physical Critical Strike Damage",
                UnitStat = "PhysicalCriticalStrikeDamage",
                ModificationType = "Add",
                AttributeCapType = "Uncapped",
                ValuePerPurchase = 0.1f,
                Cost = 500,
                MaxPurchases = 5,
                Notes = "Permanently increases Physical Critical Strike Damage by 0.1 per purchase."},
            ["SCC"] = new()
            {
                DisplayName = "Spell Critical Strike Chance",
                UnitStat = "SpellCriticalStrikeChance",
                ModificationType = "Add",
                AttributeCapType = "Uncapped",
                ValuePerPurchase = 0.02f,
                Cost = 500,
                MaxPurchases = 5,
                Notes = "Permanently increases Spell Critical Strike Chance by 0.02 per purchase."},
            ["SCD"] = new()
            {
                DisplayName = "Spell Critical Strike Damage",
                UnitStat = "SpellCriticalStrikeDamage",
                ModificationType = "Add",
                AttributeCapType = "Uncapped",
                ValuePerPurchase = 0.1f,
                Cost = 500,
                MaxPurchases = 5,
                Notes = "Permanently increases Spell Critical Strike Damage by 0.1 per purchase."},
            ["PR"] = new()
            {
                DisplayName = "Physical Resistance",
                UnitStat = "PhysicalResistance",
                ModificationType = "Add",
                AttributeCapType = "Uncapped",
                ValuePerPurchase = 0.02f,
                Cost = 500,
                MaxPurchases = 5,
                Notes = "Permanently increases Physical Resistance by 0.02 per purchase."},
            ["SR"] = new()
            {
                DisplayName = "Spell Resistance",
                UnitStat = "SpellResistance",
                ModificationType = "Add",
                AttributeCapType = "Uncapped",
                ValuePerPurchase = 0.02f,
                Cost = 500,
                MaxPurchases = 5,
                Notes = "Permanently increases Spell Resistance by 0.02 per purchase."},
            ["HR"] = new()
            {
                DisplayName = "Healing Received",
                UnitStat = "HealingReceived",
                ModificationType = "Add",
                AttributeCapType = "Uncapped",
                ValuePerPurchase = 0.03f,
                Cost = 500,
                MaxPurchases = 5,
                Notes = "Permanently increases Healing Received by 0.03 per purchase."},
            ["DR"] = new()
            {
                DisplayName = "Damage Reduction",
                UnitStat = "DamageReduction",
                ModificationType = "Add",
                AttributeCapType = "Uncapped",
                ValuePerPurchase = 0.01f,
                Cost = 750,
                MaxPurchases = 5,
                Notes = "Permanently increases Damage Reduction by 0.01 per purchase."},
            ["RY"] = new()
            {
                DisplayName = "Resource Yield",
                UnitStat = "ResourceYield",
                ModificationType = "Add",
                AttributeCapType = "Uncapped",
                ValuePerPurchase = 0.05f,
                Cost = 500,
                MaxPurchases = 5,
                Notes = "Permanently increases Resource Yield by 0.05 per purchase."},
            ["RBD"] = new()
            {
                DisplayName = "Reduced Blood Drain",
                UnitStat = "ReducedBloodDrain",
                ModificationType = "Add",
                AttributeCapType = "Uncapped",
                ValuePerPurchase = 0.1f,
                Cost = 500,
                MaxPurchases = 5,
                Notes = "Permanently increases Reduced Blood Drain by 0.1 per purchase."},
            ["SCR"] = new()
            {
                DisplayName = "Spell Cooldown Recovery Rate",
                UnitStat = "SpellCooldownRecoveryRate",
                ModificationType = "Add",
                AttributeCapType = "Uncapped",
                ValuePerPurchase = 0.02f,
                Cost = 500,
                MaxPurchases = 5,
                Notes = "Permanently increases Spell Cooldown Recovery Rate by 0.02 per purchase."},
            ["WCR"] = new()
            {
                DisplayName = "Weapon Cooldown Recovery Rate",
                UnitStat = "WeaponCooldownRecoveryRate",
                ModificationType = "Add",
                AttributeCapType = "Uncapped",
                ValuePerPurchase = 0.02f,
                Cost = 500,
                MaxPurchases = 5,
                Notes = "Permanently increases Weapon Cooldown Recovery Rate by 0.02 per purchase."},
            ["UCR"] = new()
            {
                DisplayName = "Ultimate Cooldown Recovery Rate",
                UnitStat = "UltimateCooldownRecoveryRate",
                ModificationType = "Add",
                AttributeCapType = "Uncapped",
                ValuePerPurchase = 0.04f,
                Cost = 500,
                MaxPurchases = 5,
                Notes = "Permanently increases Ultimate Cooldown Recovery Rate by 0.04 per purchase."},
            ["MD"] = new()
            {
                DisplayName = "Minion Damage",
                UnitStat = "MinionDamage",
                ModificationType = "Add",
                AttributeCapType = "Uncapped",
                ValuePerPurchase = 0.05f,
                Cost = 500,
                MaxPurchases = 5,
                Notes = "Permanently increases Minion Damage by 0.05 per purchase."},
            ["AAS"] = new()
            {
                DisplayName = "Ability Attack Speed",
                UnitStat = "AbilityAttackSpeed",
                ModificationType = "Add",
                AttributeCapType = "Uncapped",
                ValuePerPurchase = 0.02f,
                Cost = 500,
                MaxPurchases = 5,
                Notes = "Permanently increases Ability Attack Speed by 0.02 per purchase."},
            ["CDR"] = new()
            {
                DisplayName = "Corruption Damage Reduction",
                UnitStat = "CorruptionDamageReduction",
                ModificationType = "Add",
                AttributeCapType = "Uncapped",
                ValuePerPurchase = 0.02f,
                Cost = 500,
                MaxPurchases = 5,
                Notes = "Permanently increases Corruption Damage Reduction by 0.02 per purchase."}
        },
        Categories = new Dictionary<string, BuffCategoryDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["blood_buff"] = new() { DisplayName = "Blood Buff", Documentation = "Renewable 2-hour blood-buff effects. Default limit: five blood packages.", MaxOwnedSlots = 5, SlotFreeCost = 250 },
            ["set_bonus"] = new() { DisplayName = "Set Bonus", Documentation = "Armor set style bonuses or set-derived effects.", MaxOwnedSlots = null, SlotFreeCost = null },
            ["potion"] = new() { DisplayName = "Potion", Documentation = "Potion and brew-style buffs. Safer for multi-slot ownership.", MaxOwnedSlots = 3, SlotFreeCost = 100 },
            ["elixir"] = new() { DisplayName = "Elixir", Documentation = "Elixir-style buffs. Bloodcraft compatibility default: one owned elixir slot.", MaxOwnedSlots = 1, SlotFreeCost = 500 },
            ["misc"] = new() { DisplayName = "Misc", Documentation = "Fallback category for uncategorized buffs.", MaxOwnedSlots = null, SlotFreeCost = null }
        },
        Buffs = DefaultBuffEntries()
    };

    internal static Dictionary<string, PerkShopEntry> DefaultBuffEntriesForMigration() => DefaultBuffEntries();

    private static Dictionary<string, PerkShopEntry> DefaultBuffEntries()
    {
        var buffs = new Dictionary<string, PerkShopEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["sun_immunity"] = new()
            {
                DisplayName = "Sun Immunity",
                Category = "misc",
                BuffPrefab = 32681348,
                Cost = 100,
                PersistentPurchase = true,
                DurationSeconds = -1,
                KeepVisibleTimerFrozen = false,
                VisibleTimerSeconds = 0,
                PersistThroughDeath = true,
                MutateAppliedBuffLifetime = false,
                PreventDuplicate = true,
                Notes = "Grants complete immunity to sun exposure."
            },

            ["potion_of_rage"] = new()
            {
                DisplayName = "Potion of Rage",
                Category = "potion",
                BuffPrefab = -1591883586,
                Cost = 400,
                PersistentPurchase = true,
                DurationSeconds = 7200,
                KeepVisibleTimerFrozen = false,
                VisibleTimerSeconds = 0,
                PersistThroughDeath = false,
                MutateAppliedBuffLifetime = false,
                PreventDuplicate = true,
                Notes = "Applies AB_Consumable_PhysicalPowerPotion_T02_Buff as a renewable 2-hour owned buff."
            },

            ["elixir_of_the_crow"] = new()
            {
                DisplayName = "Elixir of the Crow",
                Category = "elixir",
                BuffPrefab = -262239794,
                Cost = 750,
                PersistentPurchase = true,
                DurationSeconds = -1,
                KeepVisibleTimerFrozen = false,
                VisibleTimerSeconds = 0,
                PersistThroughDeath = true,
                MutateAppliedBuffLifetime = false,
                PreventDuplicate = true,
                Notes = "Applies AB_Elixir_Crow_T01_Buff as a renewable 2-hour owned buff."
            },

            ["empty_layout"] = new()
            {
                Enabled = false,
                DisplayName = "Empty Buff Layout",
                Category = "misc",
                BuffPrefab = 32681348,
                Cost = 100,
                PersistentPurchase = true,
                DurationSeconds = -1,
                KeepVisibleTimerFrozen = false,
                VisibleTimerSeconds = 0,
                PersistThroughDeath = true,
                MutateAppliedBuffLifetime = false,
                PreventDuplicate = true,
                Notes = "Replace this text with the player-visible effect description."
            }
        };

        AddBloodBuffs(buffs);
        return buffs;
    }

    private static void AddBloodBuffs(Dictionary<string, PerkShopEntry> buffs)
    {
        AddBloodBuff(buffs, "bruteT1", "Brute Blood Tier 1", -1596803256, 1, "AB_BloodBuff_Brute_Tier1");
        AddBloodBuff(buffs, "bruteT2", "Brute Blood Tier 2", 1828387635, 2, "AB_BloodBuff_Brute_Tier2");
        AddBloodBuff(buffs, "bruteT3", "Brute Blood Tier 3", -1861657718, 3, "AB_BloodBuff_Brute_Tier3");
        AddBloodBuff(buffs, "bruteT4", "Brute Blood Tier 4", -584203677, 4, "AB_BloodBuff_Brute_Tier4");

        AddBloodBuff(buffs, "corruptionT1", "Corruption Blood Tier 1", -302908776, 1, "AB_BloodBuff_Corruption_Tier1");
        AddBloodBuff(buffs, "corruptionT2", "Corruption Blood Tier 2", -771138642, 2, "AB_BloodBuff_Corruption_Tier2");
        AddBloodBuff(buffs, "corruptionT3", "Corruption Blood Tier 3", -1493903943, 3, "AB_BloodBuff_Corruption_Tier3");
        AddBloodBuff(buffs, "corruptionT4", "Corruption Blood Tier 4", 1491794137, 4, "AB_BloodBuff_Corruption_Tier4");

        AddBloodBuff(buffs, "creatureT1", "Creature Blood Tier 1", 894725875, 1, "AB_BloodBuff_Creature_Tier1");
        AddBloodBuff(buffs, "creatureT2", "Creature Blood Tier 2", 475045773, 2, "AB_BloodBuff_Creature_Tier2");
        AddBloodBuff(buffs, "creatureT3", "Creature Blood Tier 3", -1055766373, 3, "AB_BloodBuff_Creature_Tier3");
        AddBloodBuff(buffs, "creatureT4", "Creature Blood Tier 4", 1643157297, 4, "AB_BloodBuff_Creature_Tier4");

        AddBloodBuff(buffs, "draculaT1", "Dracula Blood Tier 1", -488475343, 1, "AB_BloodBuff_Dracula_Tier1", enabled: false);
        AddBloodBuff(buffs, "draculaT2", "Dracula Blood Tier 2", 2145997375, 2, "AB_BloodBuff_Dracula_Tier2", enabled: false);
        AddBloodBuff(buffs, "draculaT3", "Dracula Blood Tier 3", 1805033464, 3, "AB_BloodBuff_Dracula_Tier3", enabled: false);
        AddBloodBuff(buffs, "draculaT4", "Dracula Blood Tier 4", -2079057224, 4, "AB_BloodBuff_Dracula_Tier4", enabled: false);
        AddBloodBuff(buffs, "draculaT5", "Dracula Blood Tier 5", -1923843097, 5, "AB_BloodBuff_Dracula_Tier5", enabled: false);

        AddBloodBuff(buffs, "draculinT1", "Draculin Blood Tier 1", 1558171501, 1, "AB_BloodBuff_Draculin_Tier1");
        AddBloodBuff(buffs, "draculinT2", "Draculin Blood Tier 2", 997154800, 2, "AB_BloodBuff_Draculin_Tier2");
        AddBloodBuff(buffs, "draculinT3", "Draculin Blood Tier 3", 1159173627, 3, "AB_BloodBuff_Draculin_Tier3");
        AddBloodBuff(buffs, "draculinT4", "Draculin Blood Tier 4", 1103099361, 4, "AB_BloodBuff_Draculin_Tier4");

        AddBloodBuff(buffs, "mutantT1", "Mutant Blood Tier 1", -1266262267, 1, "AB_BloodBuff_Mutant_Tier1");
        AddBloodBuff(buffs, "mutantT2", "Mutant Blood Tier 2", -1413561088, 2, "AB_BloodBuff_Mutant_Tier2");
        AddBloodBuff(buffs, "mutantT3", "Mutant Blood Tier 3", 946705138, 3, "AB_BloodBuff_Mutant_Tier3");
        AddBloodBuff(buffs, "mutantT4", "Mutant Blood Tier 4", -491525099, 4, "AB_BloodBuff_Mutant_Tier4");

        AddBloodBuff(buffs, "rogueT1", "Rogue Blood Tier 1", 1201299233, 1, "AB_BloodBuff_Rogue_Tier1");
        AddBloodBuff(buffs, "rogueT2", "Rogue Blood Tier 2", -154702686, 2, "AB_BloodBuff_Rogue_Tier2");
        AddBloodBuff(buffs, "rogueT3", "Rogue Blood Tier 3", -536284884, 3, "AB_BloodBuff_Rogue_Tier3");
        AddBloodBuff(buffs, "rogueT4", "Rogue Blood Tier 4", 210193036, 4, "AB_BloodBuff_Rogue_Tier4");

        AddBloodBuff(buffs, "scholarT1", "Scholar Blood Tier 1", 1934870645, 1, "AB_BloodBuff_Scholar_Tier1");
        AddBloodBuff(buffs, "scholarT2", "Scholar Blood Tier 2", -993492354, 2, "AB_BloodBuff_Scholar_Tier2");
        AddBloodBuff(buffs, "scholarT3", "Scholar Blood Tier 3", -901503997, 3, "AB_BloodBuff_Scholar_Tier3");
        AddBloodBuff(buffs, "scholarT4", "Scholar Blood Tier 4", -1859298707, 4, "AB_BloodBuff_Scholar_Tier4");

        AddBloodBuff(buffs, "warriorT1", "Warrior Blood Tier 1", -804597757, 1, "AB_BloodBuff_Warrior_Tier1");
        AddBloodBuff(buffs, "warriorT2", "Warrior Blood Tier 2", -1510965956, 2, "AB_BloodBuff_Warrior_Tier2");
        AddBloodBuff(buffs, "warriorT3", "Warrior Blood Tier 3", -1869022798, 3, "AB_BloodBuff_Warrior_Tier3");
        AddBloodBuff(buffs, "warriorT4", "Warrior Blood Tier 4", -397097531, 4, "AB_BloodBuff_Warrior_Tier4");

        AddBloodBuff(buffs, "workerT1", "Worker Blood Tier 1", -773025435, 1, "AB_BloodBuff_Worker_Tier1");
        AddBloodBuff(buffs, "workerT2", "Worker Blood Tier 2", -2068307944, 2, "AB_BloodBuff_Worker_Tier2");
        AddBloodBuff(buffs, "workerT3", "Worker Blood Tier 3", 1359282533, 3, "AB_BloodBuff_Worker_Tier3");
        AddBloodBuff(buffs, "workerT4", "Worker Blood Tier 4", 1791009885, 4, "AB_BloodBuff_Worker_Tier4");

        AddBloodBuff(buffs, "generalT5", "General Blood Tier 5", 947312310, 5, "AB_BloodBuff_General_Tier5", enabled: false);
    }

    private static void AddBloodBuff(
        Dictionary<string, PerkShopEntry> buffs,
        string key,
        string displayName,
        int prefab,
        int tier,
        string prefabName,
        bool enabled = true)
    {
        buffs[key] = new PerkShopEntry
        {
            Enabled = enabled,
            DisplayName = displayName,
            Category = "blood_buff",
            BuffPrefab = prefab,
            Cost = 250 * Math.Max(1, tier),
            PersistentPurchase = true,
            DurationSeconds = 7200,
            KeepVisibleTimerFrozen = false,
            VisibleTimerSeconds = 0,
            PersistThroughDeath = false,
            MutateAppliedBuffLifetime = false,
            PreventDuplicate = true,
            Notes = $"Applies {prefabName} as a renewable 2-hour owned blood buff without permanent lifetime mutation."
        };
    }

}