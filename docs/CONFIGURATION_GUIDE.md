# PerkShop Configuration Guide

This guide explains the recommended live-server configuration for PerkShop.

## Workflow

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

## Recommended live defaults

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

## Buff entries

Each buff entry defines a buyable persistent perk.

Important fields:

| Field | Description |
|---|---|
| `Enabled` | Enables this shop entry. |
| `PrefabGUID` | V Rising buff prefab GUID. |
| `Name` | Display name shown in commands. |
| `Category` | Slot category such as `potion`, `elixir`, `blood_buff`, `set_bonus`, or `misc`. |
| `CostPrefabGUID` | Currency item prefab GUID. |
| `CostAmount` | Purchase cost. |
| `DurationSeconds` | Use `7200` or the global renewable value for timed renewable buffs. |
| `PersistThroughDeath` | Whether PerkShop should maintain ownership after death. |
| `Notes` | Admin-facing description. |

## Stat entries

Each stat entry defines a buyable stat rank.

Important fields:

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

## UI limitations

PerkShop applies stats server-side through V Rising stat modifiers. Some stats may affect gameplay but not appear in TAB/Eclipse because the client UI does not render every `UnitStatType`.

Known UI-safe examples from testing:

- `MaxHealth`
- `PhysicalPower`
- `SpellPower`

Other stats can still be enabled for gameplay.

## Blood buffs

Blood buffs are renewable timed buffs. They should:

- keep a normal countdown;
- renew when missing/expired;
- not override equipped blood type perks;
- allow multiple owned entries up to the `blood_buff` slot limit;
- remove cleanly when `.perk buffremove <key>` is used.

Default slot count:

```json
"blood_buff": {
  "MaxOwnedSlots": 5
}
```

## Bloodcraft compatibility

PerkShop uses a separate stat carrier by default and is intended to coexist with Bloodcraft. Balance is still server-dependent: Bloodcraft progression and PerkShop stats can stack.

## Troubleshooting

Run:

```text
.perk validate
.perk diag
.perk sync
```

After major config changes, run:

```text
.perk reload
.perk syncall
```

If a stat has gameplay effect but does not appear in TAB/Eclipse, it is likely a client UI limitation rather than a server-side PerkShop failure.


## Key reference

See [`KEYS.md`](KEYS.md) for the complete short stat and blood-buff key list.
