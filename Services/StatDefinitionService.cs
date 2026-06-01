using System;
using System.Collections.Generic;
using PerkShop.Models;
using ProjectM;

namespace PerkShop.Services;

internal static class StatDefinitionService
{
    public const int CurrentConfigVersion = 14;

    internal sealed class ParsedStatDefinition
    {
        public string Key { get; init; } = string.Empty;
        public StatShopEntry Entry { get; init; } = new();
        public UnitStatType UnitStat { get; init; }
        public ModificationType ModificationType { get; init; }
        public AttributeCapType AttributeCapType { get; init; }
    }

    private static readonly object Lock = new();
    private static readonly Dictionary<string, ParsedStatDefinition> ParsedStats = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> WarnedInvalidDefinitions = new(StringComparer.OrdinalIgnoreCase);

    public static int ParsedStatCount
    {
        get
        {
            lock (Lock)
                return ParsedStats.Count;
        }
    }

    public static void RebuildCache(ShopConfigRoot config)
    {
        lock (Lock)
        {
            ParsedStats.Clear();
            WarnedInvalidDefinitions.Clear();

            if (config?.Stats == null)
                return;

            foreach (var (rawKey, entry) in config.Stats)
            {
                if (string.IsNullOrWhiteSpace(rawKey) || entry == null || !entry.Enabled)
                    continue;

                string key = rawKey.Trim();

                if (!Enum.TryParse<UnitStatType>(entry.UnitStat, ignoreCase: true, out var unitStat))
                {
                    WarnOnce($"UnitStat:{key}:{entry.UnitStat}", $"[StatDefinitionService] Stat '{key}' has invalid UnitStat '{entry.UnitStat}' and will be skipped.");
                    continue;
                }

                if (!Enum.TryParse<ModificationType>(entry.ModificationType, ignoreCase: true, out var modificationType))
                {
                    WarnOnce($"ModificationType:{key}:{entry.ModificationType}", $"[StatDefinitionService] Stat '{key}' has invalid ModificationType '{entry.ModificationType}'. Falling back to Add.");
                    modificationType = ModificationType.Add;
                }

                if (!Enum.TryParse<AttributeCapType>(entry.AttributeCapType, ignoreCase: true, out var capType))
                {
                    WarnOnce($"AttributeCapType:{key}:{entry.AttributeCapType}", $"[StatDefinitionService] Stat '{key}' has invalid AttributeCapType '{entry.AttributeCapType}'. Falling back to Uncapped.");
                    capType = AttributeCapType.Uncapped;
                }

                UnitStatType resolvedUnitStat = ResolveClientAttributeAlias(unitStat, config.UseClientAttributeStatAliases);

                ParsedStats[key] = new ParsedStatDefinition
                {
                    Key = key,
                    Entry = entry,
                    UnitStat = resolvedUnitStat,
                    ModificationType = modificationType,
                    AttributeCapType = capType
                };
            }
        }
    }

    public static bool TryGetParsedStat(string key, out ParsedStatDefinition definition)
    {
        definition = null!;

        if (string.IsNullOrWhiteSpace(key))
            return false;

        lock (Lock)
        {
            if (ParsedStats.TryGetValue(key, out var found))
            {
                definition = found;
                return true;
            }

            return false;
        }
    }

    public static bool IsClientTabSafeUnitStat(string unitStat)
    {
        if (string.IsNullOrWhiteSpace(unitStat))
            return false;

        if (!Enum.TryParse<UnitStatType>(unitStat.Trim(), ignoreCase: true, out var parsed))
            return false;

        return IsClientTabSafeUnitStat(parsed);
    }

    public static bool IsClientTabSafeUnitStat(UnitStatType unitStat)
    {
        // Runtime-tested stable server-side display path:
        // These stats apply through the PerkShop carrier and do not trigger VampireAttributes spam.
        // Other UnitStatType values can still have gameplay effect, but the client/Bloodcraft/Eclipse
        // attribute layer may not render them or may throw NotImplementedException.
        return unitStat switch
        {
            UnitStatType.MaxHealth => true,
            UnitStatType.PhysicalPower => true,
            UnitStatType.SpellPower => true,
            _ => false
        };
    }

    public static UnitStatType ResolveClientAttributeAlias(UnitStatType unitStat, bool useAliases)
    {
        if (!useAliases)
            return unitStat;

        // Do not alias MaxHealth/PhysicalPower/SpellPower because they already show correctly
        // for PerkShop and some gameplay systems expect the base stat.
        return unitStat switch
        {
            UnitStatType.MovementSpeed => UnitStatType.BonusMovementSpeed,
            _ => unitStat
        };
    }

    public static bool IsClientTabSafeStat(ShopConfigRoot config, StatShopEntry entry)
    {
        if (entry == null)
            return false;

        if (!Enum.TryParse<UnitStatType>(entry.UnitStat, ignoreCase: true, out var unitStat))
            return false;

        UnitStatType resolved = ResolveClientAttributeAlias(unitStat, config.UseClientAttributeStatAliases);
        return IsClientTabSafeUnitStat(resolved);
    }

    public static bool IsClientUnsupportedStatAllowed(ShopConfigRoot config, StatShopEntry entry)
        => config.EnableClientUnsupportedStats || IsClientTabSafeStat(config, entry);

    public static bool IsDangerousModifierCombo(StatShopEntry entry)
    {
        if (entry == null)
            return false;

        return string.Equals(entry.UnitStat, "MovementSpeed", StringComparison.OrdinalIgnoreCase)
            && string.Equals(entry.ModificationType, "Add", StringComparison.OrdinalIgnoreCase);
    }

    public static void NormalizeKnownStatEntry(string key, StatShopEntry entry)
    {
        if (entry == null)
            return;

        if (string.Equals(key, "MS", StringComparison.OrdinalIgnoreCase)
            && string.Equals(entry.UnitStat, "MovementSpeed", StringComparison.OrdinalIgnoreCase)
            && string.Equals(entry.ModificationType, "Add", StringComparison.OrdinalIgnoreCase))
        {
            entry.ModificationType = "MultiplyBaseAdd";
        }

        if (TryGetDefaultStatNote(key, entry.ValuePerPurchase, entry.UnitStat, out var note)
            && (string.IsNullOrWhiteSpace(entry.Notes)
                || entry.Notes.StartsWith("Permanently increases ", StringComparison.OrdinalIgnoreCase)
                || string.Equals(entry.Notes, "Permanent stat purchase.", StringComparison.OrdinalIgnoreCase)))
        {
            entry.Notes = note;
        }
    }

    public static bool TryGetDefaultStatNote(string key, float valuePerPurchase, string unitStat, out string note)
    {
        note = string.Empty;

        if (string.IsNullOrWhiteSpace(key))
            return false;

        string formatted = FormatNumber(valuePerPurchase);

        note = key.Trim().ToLowerInvariant() switch
        {
            "MS" => $"Permanently increases Movement Speed by {formatted} per purchase. Uses MultiplyBaseAdd for HUD/stat compatibility.",
            _ => $"Permanently increases {ToTitle(unitStat)} by {formatted} per purchase."
        };

        return true;
    }

    private static string ToTitle(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "the configured stat";

        var result = string.Empty;
        foreach (char c in value.Trim())
        {
            if (char.IsUpper(c) && result.Length > 0)
                result += " ";
            result += c;
        }

        return result;
    }

    private static string FormatNumber(float value)
    {
        return Math.Abs(value % 1f) < 0.0001f
            ? ((int)value).ToString()
            : value.ToString("0.####");
    }

    private static void WarnOnce(string key, string message)
    {
        if (WarnedInvalidDefinitions.Add(key))
            Core.Log.LogWarning(message);
    }
}
