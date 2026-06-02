# PerkShop Config Reference

Config file:

```text
BepInEx/config/PerkShop/perkconfig.json
```

Reload after editing:

```text
.perk reload
.perk validate
```

## Important top-level options

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

## Stat carrier

Default:

```json
"StatCarrierBuffPrefab": -809648681
```

This is intended to avoid Bloodcraft's known carrier. Keep it configurable for compatibility with other servers/mod stacks.

## Buff categories

Blood buffs default to five slots:

```json
"BuffCategories": {
  "blood_buff": {
    "MaxOwnedSlots": 5
  }
}
```

Set `MaxOwnedSlots` to `-1` for unlimited slots.

## Renewable timed buffs

Potions, elixirs, blood buffs, and relic buffs are intended to keep a normal countdown and renew when missing/expired.

This is preferred over force-removing `LifeTime`, which can leave stale client UI state for some vanilla buff prefabs.


## Key reference

See [`KEYS.md`](KEYS.md) for the complete short stat and blood-buff key list.


## Per-entry currency override

Buff and stat entries can optionally set `CurrencyPrefab` and `CurrencyName`. If omitted, PerkShop uses the global default currency.


## Entry-specific currency overrides

Buff and stat entries can optionally override the global currency with:

```json
"CurrencyPrefab": 123456789,
"CurrencyName": "Custom Currency"
```

If those fields are omitted, PerkShop uses the top-level global `CurrencyPrefab` and `CurrencyName`.

## Relic category

Relic tower buffs use the standardized `relic` category key.
