# PerkShop

PerkShop is a server-side V Rising mod that adds a configurable perk shop through VampireCommandFramework commands.

Players can buy persistent buffs and stat perks. Admins can grant, revoke, whitelist, inspect, validate, reload, and sync player perks. Stats acquired via PerkShop may not all appear in the player's UI, but they still have gameplay effects.


> Current packaged source version: **0.3.0**
>
> Highlights in this release:
> - per-entry currency overrides for buffs and stats
> - relic tower buffs included in the default config
> - relic category standardized to `relic`
> - expanded command/config/key documentation

Command prefix:

```text
.perk
```

## Credits

PerkShop was inspired by and built with reference to the V Rising modding community's work:

- [Bloodcraft](https://thunderstore.io/c/v-rising/p/zfolmt/Bloodcraft/) by [zfolmt](https://thunderstore.io/c/v-rising/p/zfolmt/) — inspiration/reference for the buff and stat systems.
- [PrisonerBlood](https://thunderstore.io/c/v-rising/p/GGs/PrisonerBlood/) by [GGs](https://thunderstore.io/c/v-rising/p/GGs/) — inspiration/reference for the command-based shop flow.

## Transparency

The original working code for the mod is human made, but it has been greatly tinkered with AI for debugging and optimization.

## Features

- Configurable buff shop.
- Configurable permanent stat shop.
- Renewable timed buffs for potions, elixirs, blood buffs, and relics.
- Blood-buff category defaults to five slots.
- Relic category defaults to four slots.
- Purchased ownership persists across relogs/restarts, with manual session sync available.
- Admin-given buffs and flat stats.
- Buff/stat whitelist support.
- `.perk validate`, `.perk diag`, `.perk reload`, and `.perk syncall` for live-server administration.
- Debounced JSON persistence and throttled periodic reapply.
- Bloodcraft-safe default stat carrier.
- Optional per-entry currency override while keeping a global default currency.

## Dependencies

Install these before PerkShop:

- BepInExPack for V Rising
- VampireCommandFramework

This repository does **not** include game, BepInEx, VCF, or interop DLLs.

## Installation

Copy the compiled DLL to the server:

```text
BepInEx/plugins/PerkShop/PerkShop.dll
```

Start the server once to generate:

```text
BepInEx/config/PerkShop/perkconfig.json
BepInEx/config/PerkShop/ownedbuffs.json
BepInEx/config/PerkShop/playercache.json
```

Then edit the config and reload in game:

```text
.perk reload
.perk validate
.perk diag
```

## Command Reference

### Player commands

| Command | Description |
|---|---|
| `.perk menu` | Shows the main PerkShop command menu. |
| `.perk status [page]` | Shows your owned buffs, purchased stats, admin grants, and slot usage. |
| `.perk search <text>` | Searches configured buff and stat entries by key or name. |
| `.perk sync` | Reapplies your owned buffs and stats for the current session. |
| `.perk bufflist [page]` | Lists available buff purchase keys. |
| `.perk buffdet <buffKey>` | Shows detailed information for a buff entry, including cost, category, and notes. |
| `.perk buffbuy <buffKey>` | Buys a buff entry using its configured currency. |
| `.perk buffremove <buffKey>` | Removes one owned buff entry from your active setup. |
| `.perk statlist [page]` | Lists available stat purchase keys. |
| `.perk statdet <statKey>` | Shows detailed information for a stat entry. |
| `.perk statbuy <statKey>` | Buys one rank of a stat entry. |
| `.perk statremove <statKey>` | Removes all purchased ranks of one stat entry. |

### Admin commands

| Command | Description |
|---|---|
| `.perk admin` | Shows the admin command menu. |
| `.perk info <player>` | Shows PerkShop ownership and status info for a player. |
| `.perk reload` | Flushes pending saves, reloads config, and rebuilds runtime caches. |
| `.perk diag` | Shows runtime diagnostics for troubleshooting. |
| `.perk validate` | Validates config entries and reports risky or invalid settings. |
| `.perk syncall` | Reapplies PerkShop buffs and stats for cached online users. |
| `.perk giftbuff <player> <buffKey>` | Grants purchased-style buff ownership to a player. |
| `.perk revokebuff <player> <buffKey>` | Removes purchased-style buff ownership from a player. |
| `.perk addbuff <player> <buffKey>` | Grants an admin-only buff entry to a player. |
| `.perk clearbuff <player> <buffKey>` | Removes an admin-only buff grant and clears the active buff. |
| `.perk giftstat <player> <statKey> <ranks>` | Grants purchased-style stat ranks to a player. |
| `.perk revokestat <player> <statKey> <ranks>` | Revokes purchased-style stat ranks from a player. |
| `.perk addflat <player> <UnitStat|statKey> <amount>` | Adds an admin flat stat modifier. |
| `.perk clearflat <player> <UnitStat|statKey>` | Removes an admin flat stat modifier. |

### Whitelist commands

| Command | Description |
|---|---|
| `.perk wlstatus` | Shows whether buff/stat whitelists are enabled and how many players are listed. |
| `.perk wlcheckbuff` | Checks whether you can use the buff shop. |
| `.perk wlcheckstat` | Checks whether you can use the stat shop. |
| `.perk wlcheckall` | Checks both whitelist types for your current character. |
| `.perk wlplayer <player>` | Checks whitelist status for a target player. |
| `.perk wladdbuff <player>` | Adds a player to the buff whitelist. |
| `.perk wlremovebuff <player>` | Removes a player from the buff whitelist. |
| `.perk wladdstat <player>` | Adds a player to the stat whitelist. |
| `.perk wlremovestat <player>` | Removes a player from the stat whitelist. |

## Configuration Reference

Config file:

```text
BepInEx/config/PerkShop/perkconfig.json
```

Reload after editing:

```text
.perk reload
.perk validate
```

### Important top-level options

| Field | Default | Description |
|---|---:|---|
| `Enabled` | `true` | Enables or disables PerkShop. |
| `EnableBuffShop` | `true` | Enables buff purchases. |
| `EnableStatShop` | `true` | Enables stat purchases. |
| `ForcePermanentBuffs` | `true` | Ensures purchased buffs are maintained by ownership sync. |
| `RenewableTimedBuffDurationSeconds` | `7200` | Default renewable duration for potion/elixir/blood/relic buffs. |
| `EnableClientUnsupportedStats` | `true` | Applies all configured stats, even if the client UI does not display them. |
| `EnableExperimentalBloodBuffs` | `true` | Enables configured blood-buff entries. |
| `ReapplyOwnedBuffsWhenMissing` | `true` | Periodically reapplies missing owned buffs/stats. |
| `ReapplyCheckIntervalSeconds` | `60` | Reapply scan interval. |
| `ReapplyMaxUsersPerCycle` | `5` | Max cached users processed per reapply cycle. |
| `OwnershipSaveDebounceSeconds` | `2` | Debounces ownership JSON writes. |
| `PlayerCacheSaveDebounceSeconds` | `30` | Debounces player-cache writes. |
| `EnableDebugLogging` | `false` | Enables verbose PerkShop logs. |

### Stat carrier

Default:

```json
"StatCarrierBuffPrefab": -809648681
```

This is intended to avoid Bloodcraft's known carrier. Keep it configurable for compatibility with other servers/mod stacks.

### Buff categories

Blood buffs default to five slots:

```json
"BuffCategories": {
  "blood_buff": {
    "MaxOwnedSlots": 5
  }
}
```

Relic buffs default to four slots:

```json
"BuffCategories": {
  "relic": {
    "MaxOwnedSlots": 4
  }
}
```

Set `MaxOwnedSlots` to `-1` for unlimited slots.

### Renewable timed buffs

Potions, elixirs, blood buffs, and relic buffs are intended to keep a normal countdown and renew when missing or expired.

This is preferred over force-removing `LifeTime`, which can leave stale client UI state for some vanilla buff prefabs.

## Configuration Guide

### Workflow

1. Start the server once to generate config.
2. Stop the server.
3. Edit `BepInEx/config/PerkShop/perkconfig.json`.
4. Start the server.
5. Run:

```text
.perk reload
.perk validate
.perk diag
```

### Recommended live defaults

```json
{
  "Enabled": true,
  "EnableBuffShop": true,
  "EnableStatShop": true,
  "EnableClientUnsupportedStats": true,
  "EnableExperimentalBloodBuffs": true,
  "RenewableTimedBuffDurationSeconds": 7200,
  "ReapplyCheckIntervalSeconds": 60,
  "ReapplyMaxUsersPerCycle": 5,
  "OwnershipSaveDebounceSeconds": 2,
  "PlayerCacheSaveDebounceSeconds": 30,
  "EnableDebugLogging": false
}
```

### Buff entries

Each buff entry defines a buyable persistent perk.

Buff entries can optionally override the global currency with `CurrencyPrefab` and `CurrencyName`.

| Field | Description |
|---|---|
| `Enabled` | Enables this shop entry. |
| `PrefabGUID` | V Rising buff prefab GUID. |
| `Name` | Display name shown in commands. |
| `Category` | Slot category such as `potion`, `elixir`, `blood_buff`, `relic`, `set_bonus`, or `misc`. |
| `CostPrefabGUID` | Currency item prefab GUID. |
| `CostAmount` | Purchase cost. |
| `DurationSeconds` | Use `7200` or the global renewable value for timed renewable buffs. |
| `PersistThroughDeath` | Whether PerkShop should maintain ownership after death. |
| `Notes` | Admin-facing description. |

### Stat entries

Each stat entry defines a buyable stat rank.

Stat entries can also optionally override the global currency with `CurrencyPrefab` and `CurrencyName`.

| Field | Description |
|---|---|
| `Enabled` | Enables this shop entry. |
| `UnitStat` | V Rising `UnitStatType`. |
| `ModificationType` | Usually `Add`; movement speed uses `MultiplyBaseAdd`. |
| `ValuePerPurchase` | Modifier value per rank. |
| `MaxPurchases` | Max ranks. |
| `CostPrefabGUID` | Currency item prefab GUID. |
| `CostAmount` | Cost per rank. |
| `Notes` | Admin-facing description. |

### UI limitations

PerkShop applies stats server-side through V Rising stat modifiers. Some stats may affect gameplay but not appear in the vanilla TAB/Eclipse attributes UI.

## Key Reference

### Stat keys

| Key | Display name | UnitStat | Value per purchase | Max purchases |
|---|---|---|---:|---:|
| `MH` | Max Health | `MaxHealth` | 25 | 10 |
| `PP` | Physical Power | `PhysicalPower` | 2 | 10 |
| `SP` | Spell Power | `SpellPower` | 1 | 10 |
| `MS` | Movement Speed | `MovementSpeed` | 0.05 | 5 |
| `AS` | Primary Attack Speed | `PrimaryAttackSpeed` | 0.02 | 5 |
| `phll` | Physical Life Leech | `PhysicalLifeLeech` | 0.02 | 5 |
| `sll` | Spell Life Leech | `SpellLifeLeech` | 0.02 | 5 |
| `prll` | Primary Life Leech | `PrimaryLifeLeech` | 0.03 | 5 |
| `PCC` | Physical Critical Strike Chance | `PhysicalCriticalStrikeChance` | 0.02 | 5 |
| `PCD` | Physical Critical Strike Damage | `PhysicalCriticalStrikeDamage` | 0.10 | 5 |
| `SCC` | Spell Critical Strike Chance | `SpellCriticalStrikeChance` | 0.02 | 5 |
| `SCD` | Spell Critical Strike Damage | `SpellCriticalStrikeDamage` | 0.10 | 5 |
| `PR` | Physical Resistance | `PhysicalResistance` | 0.02 | 5 |
| `SR` | Spell Resistance | `SpellResistance` | 0.02 | 5 |
| `HR` | Healing Received | `HealingReceived` | 0.03 | 5 |
| `DR` | Damage Reduction | `DamageReduction` | 0.01 | 5 |
| `RY` | Resource Yield | `ResourceYield` | 0.05 | 5 |
| `RBD` | Reduced Blood Drain | `ReducedBloodDrain` | 0.10 | 5 |
| `SCR` | Spell Cooldown Recovery Rate | `SpellCooldownRecoveryRate` | 0.02 | 5 |
| `WCR` | Weapon Cooldown Recovery Rate | `WeaponCooldownRecoveryRate` | 0.02 | 5 |
| `UCR` | Ultimate Cooldown Recovery Rate | `UltimateCooldownRecoveryRate` | 0.04 | 5 |
| `MD` | Minion Damage | `MinionDamage` | 0.05 | 5 |
| `AAS` | Ability Attack Speed | `AbilityAttackSpeed` | 0.02 | 5 |
| `CDR` | Corruption Damage Reduction | `CorruptionDamageReduction` | 0.02 | 5 |

### Blood buff keys

| Key | Display name | PrefabGUID | Enabled by default |
|---|---|---:|:---:|
| `bruteT1` | Brute Blood Tier 1 | `-1596803256` | yes |
| `bruteT2` | Brute Blood Tier 2 | `1828387635` | yes |
| `bruteT3` | Brute Blood Tier 3 | `-1861657718` | yes |
| `bruteT4` | Brute Blood Tier 4 | `-584203677` | yes |
| `corruptionT1` | Corruption Blood Tier 1 | `-302908776` | yes |
| `corruptionT2` | Corruption Blood Tier 2 | `-771138642` | yes |
| `corruptionT3` | Corruption Blood Tier 3 | `-1493903943` | yes |
| `corruptionT4` | Corruption Blood Tier 4 | `1491794137` | yes |
| `creatureT1` | Creature Blood Tier 1 | `894725875` | yes |
| `creatureT2` | Creature Blood Tier 2 | `475045773` | yes |
| `creatureT3` | Creature Blood Tier 3 | `-1055766373` | yes |
| `creatureT4` | Creature Blood Tier 4 | `1643157297` | yes |
| `draculinT1` | Draculin Blood Tier 1 | `1558171501` | yes |
| `draculinT2` | Draculin Blood Tier 2 | `997154800` | yes |
| `draculinT3` | Draculin Blood Tier 3 | `1159173627` | yes |
| `draculinT4` | Draculin Blood Tier 4 | `1103099361` | yes |
| `mutantT1` | Mutant Blood Tier 1 | `-1266262267` | yes |
| `mutantT2` | Mutant Blood Tier 2 | `-1413561088` | yes |
| `mutantT3` | Mutant Blood Tier 3 | `946705138` | yes |
| `mutantT4` | Mutant Blood Tier 4 | `-491525099` | yes |
| `rogueT1` | Rogue Blood Tier 1 | `1201299233` | yes |
| `rogueT2` | Rogue Blood Tier 2 | `-154702686` | yes |
| `rogueT3` | Rogue Blood Tier 3 | `-536284884` | yes |
| `rogueT4` | Rogue Blood Tier 4 | `210193036` | yes |
| `scholarT1` | Scholar Blood Tier 1 | `1934870645` | yes |
| `scholarT2` | Scholar Blood Tier 2 | `-993492354` | yes |
| `scholarT3` | Scholar Blood Tier 3 | `-901503997` | yes |
| `scholarT4` | Scholar Blood Tier 4 | `-1859298707` | yes |
| `warriorT1` | Warrior Blood Tier 1 | `-804597757` | yes |
| `warriorT2` | Warrior Blood Tier 2 | `-1510965956` | yes |
| `warriorT3` | Warrior Blood Tier 3 | `-1869022798` | yes |
| `warriorT4` | Warrior Blood Tier 4 | `-397097531` | yes |
| `workerT1` | Worker Blood Tier 1 | `-773025435` | yes |
| `workerT2` | Worker Blood Tier 2 | `-2068307944` | yes |
| `workerT3` | Worker Blood Tier 3 | `1359282533` | yes |
| `workerT4` | Worker Blood Tier 4 | `1791009885` | yes |
| `draculaT1` | Dracula Blood Tier 1 | `-488475343` | no |
| `draculaT2` | Dracula Blood Tier 2 | `2145997375` | no |
| `draculaT3` | Dracula Blood Tier 3 | `1805033464` | no |
| `draculaT4` | Dracula Blood Tier 4 | `-2079057224` | no |
| `draculaT5` | Dracula Blood Tier 5 | `-1923843097` | no |
| `generalT5` | General Blood Tier 5 | `947312310` | no |

### Relic buff keys

These use the standardized `relic` category.


| Key | Display name | PrefabGUID | Enabled by default |
|---|---|---:|:---:|
| `relicBehemoth` | Behemoth Relic Blessing | `-1703886455` | yes |
| `relicManticore` | Manticore Relic Blessing | `-238197495` | yes |
| `relicMonster` | Monster Relic Blessing | `1068709119` | yes |
| `relicPaladin` | Paladin Relic Blessing | `-1161197991` | yes |
