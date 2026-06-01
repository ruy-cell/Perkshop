using System;
using System.Collections.Generic;
using System.Linq;

namespace PerkShop.Services;

internal static class KeyAliasService
{
    internal static readonly IReadOnlyDictionary<string, string> StatKeyAliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["max_health"] = "MH",
            ["physical_power"] = "PP",
            ["spell_power"] = "SP",
            ["movement_speed"] = "MS",
            ["primary_attack_speed"] = "AS",
            ["physical_life_leech"] = "phll",
            ["spell_life_leech"] = "sll",
            ["primary_life_leech"] = "prll",
            ["physical_critical_strike_chance"] = "PCC",
            ["physical_critical_strike_damage"] = "PCD",
            ["spell_critical_strike_chance"] = "SCC",
            ["spell_critical_strike_damage"] = "SCD",
            ["physical_resistance"] = "PR",
            ["spell_resistance"] = "SR",
            ["healing_received"] = "HR",
            ["damage_reduction"] = "DR",
            ["resource_yield"] = "RY",
            ["reduced_blood_drain"] = "RBD",
            ["spell_cooldown_recovery_rate"] = "SCR",
            ["weapon_cooldown_recovery_rate"] = "WCR",
            ["ultimate_cooldown_recovery_rate"] = "UCR",
            ["minion_damage"] = "MD",
            ["ability_attack_speed"] = "AAS",
            ["corruption_damage_reduction"] = "CDR"
        };

    internal static readonly IReadOnlyDictionary<string, string> BuffKeyAliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["brute_blood_t1"] = "bruteT1",
            ["brute_blood_t2"] = "bruteT2",
            ["brute_blood_t3"] = "bruteT3",
            ["brute_blood_t4"] = "bruteT4",
            ["corruption_blood_t1"] = "corruptionT1",
            ["corruption_blood_t2"] = "corruptionT2",
            ["corruption_blood_t3"] = "corruptionT3",
            ["corruption_blood_t4"] = "corruptionT4",
            ["creature_blood_t1"] = "creatureT1",
            ["creature_blood_t2"] = "creatureT2",
            ["creature_blood_t3"] = "creatureT3",
            ["creature_blood_t4"] = "creatureT4",
            ["dracula_blood_t1"] = "draculaT1",
            ["dracula_blood_t2"] = "draculaT2",
            ["dracula_blood_t3"] = "draculaT3",
            ["dracula_blood_t4"] = "draculaT4",
            ["dracula_blood_t5"] = "draculaT5",
            ["draculin_blood_t1"] = "draculinT1",
            ["draculin_blood_t2"] = "draculinT2",
            ["draculin_blood_t3"] = "draculinT3",
            ["draculin_blood_t4"] = "draculinT4",
            ["mutant_blood_t1"] = "mutantT1",
            ["mutant_blood_t2"] = "mutantT2",
            ["mutant_blood_t3"] = "mutantT3",
            ["mutant_blood_t4"] = "mutantT4",
            ["rogue_blood_t1"] = "rogueT1",
            ["rogue_blood_t2"] = "rogueT2",
            ["rogue_blood_t3"] = "rogueT3",
            ["rogue_blood_t4"] = "rogueT4",
            ["scholar_blood_t1"] = "scholarT1",
            ["scholar_blood_t2"] = "scholarT2",
            ["scholar_blood_t3"] = "scholarT3",
            ["scholar_blood_t4"] = "scholarT4",
            ["warrior_blood_t1"] = "warriorT1",
            ["warrior_blood_t2"] = "warriorT2",
            ["warrior_blood_t3"] = "warriorT3",
            ["warrior_blood_t4"] = "warriorT4",
            ["worker_blood_t1"] = "workerT1",
            ["worker_blood_t2"] = "workerT2",
            ["worker_blood_t3"] = "workerT3",
            ["worker_blood_t4"] = "workerT4",
            ["general_blood_t5"] = "generalT5"
        };

    internal static string NormalizeStatKey(string key)
        => string.IsNullOrWhiteSpace(key) ? key : (StatKeyAliases.TryGetValue(key.Trim(), out var mapped) ? mapped : key.Trim());

    internal static string NormalizeBuffKey(string key)
        => string.IsNullOrWhiteSpace(key) ? key : (BuffKeyAliases.TryGetValue(key.Trim(), out var mapped) ? mapped : key.Trim());

    internal static bool IsLegacyStatKey(string key) => !string.IsNullOrWhiteSpace(key) && StatKeyAliases.ContainsKey(key.Trim());

    internal static bool IsLegacyBuffKey(string key) => !string.IsNullOrWhiteSpace(key) && BuffKeyAliases.ContainsKey(key.Trim());
}
