using System.Collections.Generic;

namespace PerkShop.Models;

public sealed class ShopConfigRoot
{
    public int ConfigVersion { get; set; } = 14;

    public bool Enabled { get; set; } = true;

    // Off by default. Enable only while diagnosing spawn/reapply behavior to avoid live-server log spam.
    public bool EnableDebugLogging { get; set; } = false;

    // Keep one global currency for maximum compatibility with other economy/shop mods.
    public int CurrencyPrefab { get; set; } = 576389135;
    public string CurrencyName { get; set; } = "Greater Stygian Shards";

    // Safer defaults: avoid applying/removing paid buffs while the player is in combat.
    public bool BlockPurchasesInCombat { get; set; } = true;
    public bool BlockRemovalsInCombat { get; set; } = true;

    // Persistent ownership means: save purchase by SteamID/PlatformId and reapply if missing.
    // This is safer than trying to prevent vanilla death/logout buff cleanup.
    public bool SaveOwnership { get; set; } = true;
    public bool ReapplyOwnedBuffsOnLogin { get; set; } = true;
    public bool ReapplyOwnedBuffsWhenMissing { get; set; } = true;
    public int ReapplyCheckIntervalSeconds { get; set; } = 60;

    // Live-server smoothing. Periodic missing-buff checks process at most this many online users per cycle.
    // Values <= 0 mean no throttle. Admin .perk syncall always processes every online user.
    public int ReapplyMaxUsersPerCycle { get; set; } = 5;

    // Pending stat carriers only need to be finalized after vanilla spawn systems consume SpawnTag.
    public float CarrierFinalizeCheckIntervalSeconds { get; set; } = 0.25f;

    // Ownership saves are debounced to reduce disk churn during batch grants/purchases.
    // The store is still flushed immediately by admin .perk syncall/reload diagnostics paths when needed.
    public float OwnershipSaveDebounceSeconds { get; set; } = 2f;

    // Optional config auto-reload. For live servers, manual .perk reload is safer and avoids file timestamp checks in hot paths.
    public bool AutoDetectConfigChanges { get; set; } = false;
    public float ConfigFileCheckIntervalSeconds { get; set; } = 5f;

    // Player name cache writes are debounced to avoid small disk writes during login waves.
    public float PlayerCacheSaveDebounceSeconds { get; set; } = 30f;

    // If true, every purchased/owned buff is forced into permanent mode after application.
    // Permanent mode removes LifeTime, strips common remove-on-event cleanup components, and adds death persistence when available.
    public bool ForcePermanentBuffs { get; set; } = true;

    // Legacy/advanced switch. Kept for compatibility, but ForcePermanentBuffs takes precedence.
    public bool AllowBuffEntityMutation { get; set; } = true;
    // Renewable timed mode keeps vanilla-friendly countdown buffs as real timed buffs, then
    // PerkShop ownership reapplies them when they expire. This is safer for potions/elixirs and
    // blood buffs because vanilla cleanup remains intact instead of stripping LifeTime forever.
    public bool UseRenewableTimedBuffs { get; set; } = true;
    public int RenewableTimedBuffSeconds { get; set; } = 7200;
    public List<string> RenewableTimedBuffCategories { get; set; } = new() { "potion", "elixir", "blood_buff" };
    public bool RenewableTimedBuffsPersistThroughDeath { get; set; } = false;


    // Whitelist access control.
    // Buff and stat shops are separated. Empty lists deny everyone when the matching whitelist is enabled.
    public bool EnableBuffWhitelist { get; set; } = false;
    public List<ulong> BuffWhitelistPlatformIds { get; set; } = new();
    public Dictionary<ulong, string> BuffWhitelistNames { get; set; } = new();

    public bool EnableStatWhitelist { get; set; } = false;
    public List<ulong> StatWhitelistPlatformIds { get; set; } = new();
    public Dictionary<ulong, string> StatWhitelistNames { get; set; } = new();

    // Category identifiers used by shop entries.
    // Example keys: blood_buff, set_bonus, potion, elixir
    public Dictionary<string, BuffCategoryDefinition> Categories { get; set; } = new();

    // Permanent stat shop. Uses one hidden carrier buff and rewrites its ModifyUnitStatBuff_DOTS buffer from saved ownership.
    public bool EnableStatShop { get; set; } = true;

    // Compatibility guard for Bloodcraft/Eclipse/VampireAttributes.
    // V Rising gameplay accepts many UnitStatType values, but some client/Bloodcraft attribute panels
    // throw NotImplementedException for non-attribute stats.
    // False = only apply stats known to be accepted by the client attribute layer or mapped through aliases below; true = apply every configured stat for gameplay even if some UI panels cannot render it.
    public bool EnableClientUnsupportedStats { get; set; } = true;

    // Server-side UI workaround: when possible, map gameplay stat names to the client-attribute variants
    // that VampireAttributes/Eclipse can render, for example MovementSpeed -> BonusMovementSpeed.
    // This does not identify the source as PerkShop; it only improves visibility in final totals.
    public bool UseClientAttributeStatAliases { get; set; } = false;


    // Experimental: vanilla blood-buff prefabs interact with the native blood UI and Bloodcraft/Eclipse attribute renderer.
    // Disabled by default because they can leave stale TAB rows or trigger VampireAttributes NotImplementedException spam.
    // Enable only after testing the exact blood buff prefabs you sell.
    public bool EnableExperimentalBloodBuffs { get; set; } = false;

    public int StatCarrierBuffPrefab { get; set; } = -809648681; // SetBonus_ShieldPowerAndHealingReceived_T08; preferred hidden stat carrier

    // Stat type slots:
    // Each distinct owned stat key consumes 1 slot.
    // Buying more ranks of an already-owned stat does not consume additional slots.
    public bool EnableStatTypeSlots { get; set; } = true;
    public int MaxOwnedStatTypes { get; set; } = 4;
    public int StatTypeSlotFreeCost { get; set; } = 500;

    // Max health stat carrier behavior after a carrier rebuild.
    // ClampOnly preserves current health unless it exceeds the new max.
    // FillToMax fills the player after any max health update.
    // PreserveRatio keeps the previous current/max ratio when possible.
    public string MaxHealthPurchaseBehavior { get; set; } = "ClampOnly";

    public Dictionary<string, StatShopEntry> Stats { get; set; } = new();

    public Dictionary<string, PerkShopEntry> Buffs { get; set; } = new();
}

public sealed class BuffCategoryDefinition
{
    public string DisplayName { get; set; } = "Unnamed Category";
    public string Documentation { get; set; } = string.Empty;

    // Optional slot cap. Null or values <= 0 mean unlimited.
    // Each owned buff in this category consumes one slot.
    public int? MaxOwnedSlots { get; set; }

    // Optional cost to free one owned slot in this category.
    // Uses the global shop currency. Null or values <= 0 mean free.
    public int? SlotFreeCost { get; set; }
}

public sealed class PerkShopEntry
{
    public bool Enabled { get; set; } = true;

    // Off by default. Enable only while diagnosing spawn/reapply behavior to avoid live-server log spam.
    public bool EnableDebugLogging { get; set; } = false;
    public string DisplayName { get; set; } = "Unnamed Buff";

    // Identifier key from ShopConfigRoot.Categories
    public string Category { get; set; } = "misc";

    public int BuffPrefab { get; set; }
    public int Cost { get; set; } = 100;
    public int? CurrencyPrefab { get; set; }
    public string? CurrencyName { get; set; }

    // If true, buying this buff records ownership and reapplication can restore it after login/respawn.
    public bool PersistentPurchase { get; set; } = true;

    // Permanent default:
    // -1 = no time limit; the mod removes LifeTime when possible.
    // Positive values are still supported but not recommended if ForcePermanentBuffs is true.
    public int DurationSeconds { get; set; } = -1;

    // Legacy compatibility fields.
    // Server-only PerkShop cannot truly freeze the client countdown text. In permanent mode these
    // fields are normalized to false/0 so potion and elixir buffs remove LifeTime instead of
    // repeatedly resetting to a visible 1-minute countdown.
    public bool KeepVisibleTimerFrozen { get; set; } = false;
    public int VisibleTimerSeconds { get; set; } = 0;

    public bool PersistThroughDeath { get; set; } = true;
    public bool MutateAppliedBuffLifetime { get; set; } = true;

    // Compatibility default: one active instance per buff prefab.
    public bool PreventDuplicate { get; set; } = true;

    // Allows selling passive carrier buffs while preventing accidental purchase of visible/proc-heavy buffs.
    public string Notes { get; set; } = "Use an isolated carrier buff prefab for best compatibility.";
}

public sealed class StatShopEntry
{
    public bool Enabled { get; set; } = true;

    // Off by default. Enable only while diagnosing spawn/reapply behavior to avoid live-server log spam.
    public bool EnableDebugLogging { get; set; } = false;
    public string DisplayName { get; set; } = "Unnamed Stat";
    public string UnitStat { get; set; } = "PhysicalPower";
    public string ModificationType { get; set; } = "Add";
    public string AttributeCapType { get; set; } = "Uncapped";
    public float ValuePerPurchase { get; set; } = 1f;
    public int Cost { get; set; } = 100;
    public int? CurrencyPrefab { get; set; }
    public string? CurrencyName { get; set; }
    public int MaxPurchases { get; set; } = 1; // <= 0 = unlimited
    public string Notes { get; set; } = "Permanent stat purchase.";
}

public sealed class OwnershipStore
{
    public Dictionary<ulong, PlayerOwnedBuffs> Players { get; set; } = new();
}

public sealed class PlayerOwnedBuffs
{
    // Player-bought or legacy saved permanent buff ownership.
    public List<string> OwnedBuffKeys { get; set; } = new();

    // Admin-granted permanent buff ownership. These are displayed separately in .perk info.
    public List<string> AdminGrantedBuffKeys { get; set; } = new();

    // Player-purchased permanent stat ranks. These count toward stat type slots and MaxPurchases.
    public Dictionary<string, int> OwnedStats { get; set; } = new();

    // Admin-granted flat stats. These do not count toward slots or MaxPurchases.
    // Key = UnitStatType name, value = flat additive stat amount.
    public Dictionary<string, float> AdminFlatStats { get; set; } = new();
}


public sealed class PlayerCacheStore
{
    public Dictionary<ulong, PlayerCacheEntry> Players { get; set; } = new();
}

public sealed class PlayerCacheEntry
{
    public string CharacterName { get; set; } = string.Empty;
    public string LastSeenUtc { get; set; } = string.Empty;
}
