# PerkShop

PerkShop is a server-side V Rising mod that adds a configurable perk shop through VampireCommandFramework commands.

Players can buy persistent buffs and stat perks. Admins can grant, revoke, whitelist, inspect, validate, reload, and sync player perks. Stats acquired via PerkShop may not all appear in the player's UI, but they still have gameplay effects.

Command prefix:

```text
.perk
```

## Credits

PerkShop was inspired by and built with reference to the V Rising modding community's work:

- [Bloodcraft](https://thunderstore.io/c/v-rising/p/zfolmt/Bloodcraft/) by [zfolmt](https://thunderstore.io/c/v-rising/p/zfolmt/) — inspiration/reference for the buff and stat systems.
- [PrisonerBlood](https://thunderstore.io/c/v-rising/p/GGs/PrisonerBlood/) by [GGs](https://thunderstore.io/c/v-rising/p/GGs/) — inspiration/reference for the command-based shop flow.

## Transparency

The original working code for the mod is human made, but it has been greatly tinkered with AI for debuging and optimization.

## Features

- Configurable buff shop.
- Configurable permanent stat shop.
- Renewable timed buffs for potions, elixirs, and blood buffs.
- Blood-buff category defaults to five slots.
- Purchased ownership persists across relogs/restarts.
- Admin-given buffs and flat stats.
- Buff/stat whitelist support.
- `.perk validate`, `.perk diag`, `.perk reload`, and `.perk syncall` for live-server administration.
- Debounced JSON persistence and throttled periodic reapply.
- Bloodcraft-safe default stat carrier.

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

---

<details>
<summary><strong>Commands</strong></summary>

### Player commands

```text
.perk menu
.perk status [page]
.perk search <text>
.perk sync

.perk bufflist [page]
.perk buffdet <buffKey>
.perk buffbuy <buffKey>
.perk buffremove <buffKey>

.perk statlist [page]
.perk statdet <statKey>
.perk statbuy <statKey>
.perk statremove <statKey>
```

### Admin commands

```text
.perk admin
.perk info <player>
.perk reload
.perk validate
.perk diag
.perk syncall

.perk giftbuff <player> <buffKey>
.perk revokebuff <player> <buffKey>
.perk addbuff <player> <buffKey>
.perk clearbuff <player> <buffKey>

.perk giftstat <player> <statKey> <ranks>
.perk revokestat <player> <statKey> <ranks>
.perk addflat <player> <UnitStat|statKey> <amount>
.perk clearflat <player> <UnitStat|statKey>
```

### Whitelist commands

```text
.perk wlstatus
.perk wlcheckbuff
.perk wlcheckstat
.perk wlcheckall

.perk wlplayer <player>
.perk wladdbuff <player>
.perk wlremovebuff <player>
.perk wladdstat <player>
.perk wlremovestat <player>
```

</details>

<details>
<summary><strong>Whitelist management</strong></summary>

PerkShop has optional whitelist controls for limiting who can use the buff shop, stat shop, or both.

Whitelist behavior is controlled in `BepInEx/config/PerkShop/perkconfig.json`.

Typical whitelist fields are:

```json
{
  "EnableBuffWhitelist": false,
  "EnableStatWhitelist": false,
  "BuffWhitelist": [],
  "StatWhitelist": []
}
```

When a whitelist is disabled, everyone can use that shop if the shop itself is enabled.

When a whitelist is enabled, only players in that whitelist can buy/use that shop.

### Check whitelist state

```text
.perk wlstatus
```

Shows whether buff/stat whitelists are enabled and how many players are listed.

```text
.perk wlcheckbuff
.perk wlcheckstat
.perk wlcheckall
```

Checks whether your current character is allowed to use the buff shop, stat shop, or both.

### Check a specific player

```text
.perk wlplayer <player>
```

Shows whether a player is currently allowed to use PerkShop features.

### Add or remove a player

Allow a player to use the buff shop:

```text
.perk wladdbuff <player>
```

Remove a player from the buff shop whitelist:

```text
.perk wlremovebuff <player>
```

Allow a player to use the stat shop:

```text
.perk wladdstat <player>
```

Remove a player from the stat shop whitelist:

```text
.perk wlremovestat <player>
```

### Recommended whitelist setup

For a public server, a common setup is:

```json
{
  "EnableBuffWhitelist": false,
  "EnableStatWhitelist": false
}
```

For a donor/event/admin-only perk shop, enable one or both whitelists and add players with the commands above.

Whitelist data is saved in PerkShop's config/data files and persists across restarts.

</details>

<details>
<summary><strong>Configuration</strong></summary>

Main config file:

```text
BepInEx/config/PerkShop/perkconfig.json
```

Recommended live-server defaults:

```json
{
  "EnableDebugLogging": false,
  "AutoDetectConfigChanges": false,
  "ReapplyOwnedBuffsWhenMissing": true,
  "ReapplyCheckIntervalSeconds": 60,
  "ReapplyMaxUsersPerCycle": 5,
  "OwnershipSaveDebounceSeconds": 2,
  "PlayerCacheSaveDebounceSeconds": 30,
  "RenewableTimedBuffDurationSeconds": 7200
}
```

After editing the config, reload and validate:

```text
.perk reload
.perk validate
.perk diag
```

### Adding buffs to the shop

PerkShop buffs are configured under the `Buffs` object. Each entry key is the command key players use with `.perk buffbuy <key>`.

Minimal example:

```json
"myBuffKey": {
  "Enabled": true,
  "DisplayName": "My Custom Buff",
  "Category": "misc",
  "BuffPrefab": 123456789,
  "Cost": 100,
  "PersistentPurchase": true,
  "PreventDuplicate": true,
  "DurationSeconds": 7200,
  "PersistThroughDeath": false,
  "MutateAppliedBuffLifetime": true,
  "Notes": "Short explanation shown in .perk buffdet."
}
```

Recommended process:

1. Pick a short, clear key such as `ragePotion`, `sunImmune`, or `warriorT1`.
2. Set `BuffPrefab` to the V Rising `PrefabGUID` for the buff.
3. Choose an existing category such as `potion`, `elixir`, `blood_buff`, or `misc`.
4. Set `Cost` and `Enabled`.
5. Run `.perk reload`, then `.perk validate`.
6. Test with `.perk buffdet <key>` and `.perk buffbuy <key>`.

Important fields:

| Field | Purpose |
|---|---|
| `Enabled` | Whether players can see/buy the buff. |
| `DisplayName` | Friendly name shown in commands. |
| `Category` | Slot group. `blood_buff` defaults to five slots. |
| `BuffPrefab` | The V Rising buff `PrefabGUID` to apply. |
| `Cost` | Currency cost per purchase. |
| `PersistentPurchase` | Saves ownership and reapplies the buff when missing. |
| `PreventDuplicate` | Blocks buying/applying a duplicate active buff. |
| `DurationSeconds` | Active buff duration. Renewable categories use the configured renewable duration. |
| `PersistThroughDeath` | Whether the active buff instance should persist through death when not using renewable timed mode. |
| `MutateAppliedBuffLifetime` | Advanced compatibility setting. Leave true unless testing a special buff. |
| `Notes` | Description shown by `.perk buffdet`. |

Potions, elixirs, and blood buffs use renewable timed mode by default. They keep a visible countdown and are reapplied by ownership when missing or expired. This is intentional and safer than stripping vanilla lifetime cleanup.

Avoid adding exotic or scripted buffs unless you test them carefully. Good candidates are passive stat buffs, consumable-style buffs, blood-tier buffs, and simple utility buffs. Avoid shapeshift, travel, channel, summon, boss phase, quest, tutorial, or temporary spell-execution buffs.

</details>

<details>
<summary><strong>Keys</strong></summary>

### Stat keys

Players use these keys with:

```text
.perk statbuy <key>
.perk statdet <key>
.perk statremove <key>
```

Common stat keys:

| Key | Stat |
|---|---|
| `HP` | Max Health |
| `PP` | Physical Power |
| `SP` | Spell Power |
| `MS` | Movement Speed |
| `AS` | Primary Attack Speed |
| `PR` | Physical Resistance |
| `SR` | Spell Resistance |
| `phll` | Physical Life Leech |
| `sll` | Spell Life Leech |
| `prll` | Primary Life Leech |
| `PCC` | Physical Critical Strike Chance |
| `PCD` | Physical Critical Strike Damage |
| `SCC` | Spell Critical Strike Chance |
| `SCD` | Spell Critical Strike Damage |
| `HR` | Healing Received |
| `DR` | Damage Reduction |
| `RY` | Resource Yield |
| `RBD` | Reduced Blood Drain |
| `SCR` | Spell Cooldown Recovery Rate |
| `WCR` | Weapon Cooldown Recovery Rate |
| `UCR` | Ultimate Cooldown Recovery Rate |
| `MD` | Minion Damage |
| `AAS` | Ability Attack Speed |
| `CDR` | Corruption Damage Reduction |

Some stats are gameplay-active but may not appear in TAB/Eclipse if the client UI does not render that stat type.

### Blood-buff keys

Players use these keys with:

```text
.perk buffbuy <key>
.perk buffdet <key>
.perk buffremove <key>
```

Examples:

```text
warriorT1
warriorT2
warriorT3
warriorT4

rogueT1
rogueT2
rogueT3
rogueT4

bruteT1
bruteT2
bruteT3
bruteT4
```

Default blood families:

```text
bruteT1-T4
corruptionT1-T4
creatureT1-T4
draculinT1-T4
mutantT1-T4
rogueT1-T4
scholarT1-T4
warriorT1-T4
workerT1-T4
```

Disabled by default:

```text
draculaT1-T5
generalT5
```

For the complete key list, see [`docs/KEYS.md`](docs/KEYS.md).

</details>

## Documentation

- [`docs/COMMANDS.md`](docs/COMMANDS.md)
- [`docs/CONFIG.md`](docs/CONFIG.md)
- [`docs/CONFIGURATION_GUIDE.md`](docs/CONFIGURATION_GUIDE.md)
- [`docs/KEYS.md`](docs/KEYS.md)

## Important compatibility notes

PerkShop is server-side. It can apply stats through gameplay systems, but the client attributes UI only displays stat types it knows how to render. Some perks may therefore be gameplay-active without appearing in TAB/Eclipse.

Blood buffs are intentionally renewable timed buffs. They keep a countdown and are reapplied when missing/expired by PerkShop ownership sync. This avoids the stale UI/cleanup problems caused by force-removing vanilla lifetime behavior.

## License

See [`LICENSE`](LICENSE).
