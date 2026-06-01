using System;
using System.Linq;
using PerkShop.Models;

namespace PerkShop.Services;

internal static class ConfigMigrationService
{
    internal static bool TryMigrate(ShopConfigRoot cfg, out ShopConfigRoot migrated)
    {
        migrated = cfg ?? ConfigService.DefaultConfigForMigration();
        bool changed = false;

        if (migrated.ConfigVersion < 2)
        {
            changed |= FixMovementSpeed(migrated);
            changed |= ClampOldOvertunedDefaults(migrated);
        }

        if (migrated.ConfigVersion < 4)
        {
            changed |= DisableVisibleTimerResetLoop(migrated);
            changed |= EnsureDefaultBloodBuffs(migrated);
        }


        if (migrated.ConfigVersion < 6)
        {
            changed |= MigrateDefaultStatCarrier(migrated);
        }

        if (migrated.ConfigVersion < 7)
        {
            changed |= FixDefaultStatNotes(migrated);
        }

        if (migrated.ConfigVersion < 8)
        {
            changed |= ApplyConfigVersion8RuntimeDefaults(migrated);
        }

        if (migrated.ConfigVersion < 11)
        {
            changed |= ApplyConfigVersion11CompatibilityDefaults(migrated);
        }

        if (migrated.ConfigVersion < 12)
        {
            changed |= ApplyConfigVersion12RenewableTimedBuffDefaults(migrated);
        }

        if (migrated.ConfigVersion < 13)
        {
            changed |= ApplyConfigVersion13AllStatsAndBloodSlots(migrated);
        }

        if (migrated.ConfigVersion < 14)
        {
            changed |= ApplyConfigVersion14ShortKeys(migrated);
        }

        if (migrated.ConfigVersion < StatDefinitionService.CurrentConfigVersion)
        {
            migrated.ConfigVersion = StatDefinitionService.CurrentConfigVersion;
            changed = true;
        }

        return changed;
    }


    private static bool MigrateDefaultStatCarrier(ShopConfigRoot cfg)
    {
        // Move existing configs off SetBonus_GearLevel_01, which can overlap with vanilla set-bonus logic.
        // Do not override admins who already selected Bloodcraft's carrier or another custom prefab.
        const int oldDefaultCarrier = -1469378405; // SetBonus_GearLevel_01
        const int newDefaultCarrier = -809648681; // SetBonus_ShieldPowerAndHealingReceived_T08

        if (cfg.StatCarrierBuffPrefab != oldDefaultCarrier)
            return false;

        cfg.StatCarrierBuffPrefab = newDefaultCarrier;
        return true;
    }

    private static bool FixMovementSpeed(ShopConfigRoot cfg)
    {
        if (cfg.Stats == null || !cfg.Stats.TryGetValue("movement_speed", out var entry) || entry == null)
            return false;

        if (!StatDefinitionService.IsDangerousModifierCombo(entry))
            return false;

        entry.ModificationType = "MultiplyBaseAdd";
        entry.Notes = "Permanently increases Movement Speed by 0.05 per purchase. Uses MultiplyBaseAdd for HUD/stat compatibility.";
        return true;
    }

    private static bool ClampOldOvertunedDefaults(ShopConfigRoot cfg)
    {
        if (cfg.Stats == null)
            return false;

        bool changed = false;

        changed |= SetIfOldValue(cfg, "primary_attack_speed", 0.05f, 0.02f);
        changed |= SetIfOldValue(cfg, "physical_resistance", 0.05f, 0.02f);
        changed |= SetIfOldValue(cfg, "spell_resistance", 0.05f, 0.02f);
        changed |= SetIfOldValue(cfg, "healing_received", 0.05f, 0.03f);
        changed |= SetIfOldValue(cfg, "damage_reduction", 0.02f, 0.01f);
        changed |= SetIfOldValue(cfg, "spell_cooldown_recovery_rate", 0.05f, 0.02f);
        changed |= SetIfOldValue(cfg, "weapon_cooldown_recovery_rate", 0.05f, 0.02f);
        changed |= SetIfOldValue(cfg, "ability_attack_speed", 0.05f, 0.02f);
        changed |= SetIfOldValue(cfg, "corruption_damage_reduction", 0.05f, 0.02f);

        changed |= SetIfOldValue(cfg, "physical_power", 1f, 2f);
        changed |= SetIfOldValue(cfg, "primary_life_leech", 0.02f, 0.03f);
        changed |= SetIfOldValue(cfg, "physical_critical_strike_damage", 0.05f, 0.1f);
        changed |= SetIfOldValue(cfg, "spell_critical_strike_damage", 0.05f, 0.1f);
        changed |= SetIfOldValue(cfg, "reduced_blood_drain", 0.05f, 0.1f);

        return changed;
    }

    private static bool SetIfOldValue(ShopConfigRoot cfg, string key, float oldValue, float newValue)
    {
        if (!cfg.Stats.TryGetValue(key, out var entry) || entry == null)
            return false;

        if (Math.Abs(entry.ValuePerPurchase - oldValue) > 0.0001f)
            return false;

        entry.ValuePerPurchase = newValue;
        return true;
    }
    private static bool DisableVisibleTimerResetLoop(ShopConfigRoot cfg)
    {
        if (cfg.Buffs == null)
            return false;

        bool changed = false;

        foreach (var (_, entry) in cfg.Buffs)
        {
            if (entry == null)
                continue;

            // Permanent no-countdown mode: VisibleTimerSeconds is not converted into a real LifeTime.
            if (entry.KeepVisibleTimerFrozen || entry.VisibleTimerSeconds != 0 || entry.DurationSeconds != -1)
            {
                entry.KeepVisibleTimerFrozen = false;
                entry.VisibleTimerSeconds = 0;
                entry.DurationSeconds = -1;
                entry.MutateAppliedBuffLifetime = true;
                entry.PersistThroughDeath = true;
                changed = true;
            }
        }

        return changed;
    }

    private static bool EnsureDefaultBloodBuffs(ShopConfigRoot cfg)
    {
        cfg.Buffs ??= new System.Collections.Generic.Dictionary<string, PerkShopEntry>(StringComparer.OrdinalIgnoreCase);

        bool changed = false;
        foreach (var (key, entry) in ConfigService.DefaultBuffEntriesForMigration())
        {
            if (entry == null || !string.Equals(entry.Category, "blood_buff", StringComparison.OrdinalIgnoreCase))
                continue;

            if (cfg.Buffs.ContainsKey(key))
                continue;

            cfg.Buffs[key] = entry;
            changed = true;
        }

        return changed;
    }

    private static bool FixDefaultStatNotes(ShopConfigRoot cfg)
    {
        if (cfg.Stats == null)
            return false;

        bool changed = false;

        foreach (var (key, entry) in cfg.Stats)
        {
            if (entry == null)
                continue;

            if (!StatDefinitionService.TryGetDefaultStatNote(key, entry.ValuePerPurchase, entry.UnitStat, out var expected))
                continue;

            if (string.Equals(entry.Notes, expected, StringComparison.Ordinal))
                continue;

            if (!string.IsNullOrWhiteSpace(entry.Notes)
                && !entry.Notes.StartsWith("Permanently increases ", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(entry.Notes, "Permanent stat purchase.", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // A legacy development build generated several stale default notes after value balancing.
            // Only generated/default-style notes are aligned with the active value; custom admin notes are preserved.
            entry.Notes = expected;
            changed = true;
        }

        return changed;
    }


    private static bool ApplyConfigVersion8RuntimeDefaults(ShopConfigRoot cfg)
    {
        bool changed = false;

        if (cfg.ConfigFileCheckIntervalSeconds <= 0f)
        {
            cfg.ConfigFileCheckIntervalSeconds = 5f;
            changed = true;
        }

        if (cfg.PlayerCacheSaveDebounceSeconds <= 0f)
        {
            cfg.PlayerCacheSaveDebounceSeconds = 30f;
            changed = true;
        }

        // Manual config reload is the live-server default. Admins can opt back into auto-detect.
        if (cfg.AutoDetectConfigChanges)
        {
            // Preserve explicit true values from hand-edited configs.
        }

        return changed;
    }


    private static bool ApplyConfigVersion11CompatibilityDefaults(ShopConfigRoot cfg)
    {
        bool changed = false;

        // A legacy development build attempted to alias gameplay stats into client attribute variants. On live tests
        // this still triggered VampireAttributes NotImplementedException spam, so the safer default
        // is to leave aliases off and only apply the runtime-tested stable TAB stats unless admins opt in.
        if (cfg.UseClientAttributeStatAliases)
        {
            cfg.UseClientAttributeStatAliases = false;
            changed = true;
        }

        // Vanilla blood-buff prefabs are experimental because they interact with native blood state
        // and Bloodcraft/Eclipse attribute rendering. Keep configured entries, but prevent new
        // purchases/reapplication unless explicitly enabled.
        if (cfg.EnableExperimentalBloodBuffs)
        {
            // Preserve explicit true if the admin has already enabled it on this schema.
        }

        return changed;
    }



    private static bool ApplyConfigVersion12RenewableTimedBuffDefaults(ShopConfigRoot cfg)
    {
        bool changed = false;

        if (!cfg.UseRenewableTimedBuffs)
        {
            cfg.UseRenewableTimedBuffs = true;
            changed = true;
        }

        if (cfg.RenewableTimedBuffSeconds < 60)
        {
            cfg.RenewableTimedBuffSeconds = 7200;
            changed = true;
        }

        cfg.RenewableTimedBuffCategories ??= new System.Collections.Generic.List<string>();
        foreach (var category in new[] { "potion", "elixir", "blood_buff" })
        {
            if (!cfg.RenewableTimedBuffCategories.Contains(category, StringComparer.OrdinalIgnoreCase))
            {
                cfg.RenewableTimedBuffCategories.Add(category);
                changed = true;
            }
        }

        // Blood buffs are now handled as renewable timed vanilla-friendly buffs rather than
        // no-countdown permanent entities, so enable the configured blood shop by default.
        if (!cfg.EnableExperimentalBloodBuffs)
        {
            cfg.EnableExperimentalBloodBuffs = true;
            changed = true;
        }

        if (cfg.Buffs != null)
        {
            foreach (var (_, entry) in cfg.Buffs)
            {
                if (entry == null)
                    continue;

                bool renewable = string.Equals(entry.Category, "potion", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(entry.Category, "elixir", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(entry.Category, "blood_buff", StringComparison.OrdinalIgnoreCase);

                if (!renewable)
                    continue;

                if (entry.DurationSeconds != cfg.RenewableTimedBuffSeconds)
                {
                    entry.DurationSeconds = cfg.RenewableTimedBuffSeconds;
                    changed = true;
                }

                if (entry.KeepVisibleTimerFrozen || entry.VisibleTimerSeconds != 0)
                {
                    entry.KeepVisibleTimerFrozen = false;
                    entry.VisibleTimerSeconds = 0;
                    changed = true;
                }

                if (entry.PersistThroughDeath)
                {
                    entry.PersistThroughDeath = false;
                    changed = true;
                }

                if (!entry.MutateAppliedBuffLifetime)
                {
                    entry.MutateAppliedBuffLifetime = true;
                    changed = true;
                }
            }
        }

        return changed;
    }


    private static bool ApplyConfigVersion13AllStatsAndBloodSlots(ShopConfigRoot cfg)
    {
        bool changed = false;

        if (!cfg.EnableClientUnsupportedStats)
        {
            cfg.EnableClientUnsupportedStats = true;
            changed = true;
        }

        if (cfg.Stats != null)
        {
            foreach (var (_, entry) in cfg.Stats)
            {
                if (entry == null)
                    continue;

                if (!entry.Enabled)
                {
                    entry.Enabled = true;
                    changed = true;
                }
            }
        }

        cfg.Categories ??= new System.Collections.Generic.Dictionary<string, BuffCategoryDefinition>(StringComparer.OrdinalIgnoreCase);

        if (!cfg.Categories.TryGetValue("blood_buff", out var bloodCategory) || bloodCategory == null)
        {
            cfg.Categories["blood_buff"] = new BuffCategoryDefinition
            {
                DisplayName = "Blood Buff",
                Documentation = "Renewable 2-hour blood-buff effects. Default limit: five blood packages.",
                MaxOwnedSlots = 5,
                SlotFreeCost = 250
            };
            changed = true;
        }
        else
        {
            if (bloodCategory.MaxOwnedSlots != 5)
            {
                bloodCategory.MaxOwnedSlots = 5;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(bloodCategory.Documentation)
                || bloodCategory.Documentation.Contains("one blood package", StringComparison.OrdinalIgnoreCase)
                || bloodCategory.Documentation.Contains("Recommended limit", StringComparison.OrdinalIgnoreCase))
            {
                bloodCategory.Documentation = "Renewable 2-hour blood-buff effects. Default limit: five blood packages.";
                changed = true;
            }
        }

        return changed;
    }

    private static bool ApplyConfigVersion14ShortKeys(ShopConfigRoot cfg)
    {
        bool changed = false;

        if (cfg.Stats != null)
        {
            foreach (var alias in KeyAliasService.StatKeyAliases)
                changed |= RenameStatKey(cfg, alias.Key, alias.Value);
        }

        if (cfg.Buffs != null)
        {
            foreach (var alias in KeyAliasService.BuffKeyAliases)
                changed |= RenameBuffKey(cfg, alias.Key, alias.Value);

            foreach (var key in new[] { "draculaT1", "draculaT2", "draculaT3", "draculaT4", "draculaT5", "generalT5" })
            {
                if (cfg.Buffs.TryGetValue(key, out var entry) && entry != null && entry.Enabled)
                {
                    entry.Enabled = false;
                    changed = true;
                }
            }
        }

        return changed;
    }

    private static bool RenameStatKey(ShopConfigRoot cfg, string oldKey, string newKey)
    {
        if (cfg.Stats == null || !cfg.Stats.TryGetValue(oldKey, out var oldEntry) || oldEntry == null)
            return false;

        if (!cfg.Stats.ContainsKey(newKey))
            cfg.Stats[newKey] = oldEntry;

        cfg.Stats.Remove(oldKey);
        return true;
    }

    private static bool RenameBuffKey(ShopConfigRoot cfg, string oldKey, string newKey)
    {
        if (cfg.Buffs == null || !cfg.Buffs.TryGetValue(oldKey, out var oldEntry) || oldEntry == null)
            return false;

        if (!cfg.Buffs.ContainsKey(newKey))
            cfg.Buffs[newKey] = oldEntry;

        cfg.Buffs.Remove(oldKey);
        return true;
    }


}