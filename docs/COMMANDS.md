# PerkShop Commands

Command prefix:

```text
.perk
```

## Player commands

| Command | Description |
|---|---|
| `.perk menu` | Shows the main command menu. |
| `.perk status [page]` | Shows owned buffs, stats, admin grants, and slot usage. |
| `.perk search <text>` | Searches buff and stat entries. |
| `.perk sync` | Reapplies owned buffs/stats for your character and reports sync details. |
| `.perk bufflist [page]` | Lists available buff keys. |
| `.perk buffdet <buffKey>` | Shows details for one buff entry. |
| `.perk buffbuy <buffKey>` | Buys a buff entry. |
| `.perk buffremove <buffKey>` | Removes one owned buff entry. |
| `.perk statlist [page]` | Lists available stat keys. |
| `.perk statdet <statKey>` | Shows details for one stat entry. |
| `.perk statbuy <statKey>` | Buys one rank of a stat entry. |
| `.perk statremove <statKey>` | Removes all purchased ranks of one stat entry. |

## Admin commands

| Command | Description |
|---|---|
| `.perk admin` | Shows admin command help. |
| `.perk info <player>` | Shows PerkShop info for a player. |
| `.perk reload` | Flushes pending saves, reloads config, rebuilds runtime caches. |
| `.perk diag` | Shows runtime diagnostics. |
| `.perk validate` | Validates config and highlights risky/invalid entries. |
| `.perk syncall` | Reapplies PerkShop buffs/stats for cached online users. |
| `.perk giftbuff <player> <buffKey>` | Grants purchased-style buff ownership. |
| `.perk revokebuff <player> <buffKey>` | Removes purchased-style buff ownership. |
| `.perk addbuff <player> <buffKey>` | Grants admin-only buff ownership. |
| `.perk clearbuff <player> <buffKey>` | Removes admin-only buff ownership and active buff. |
| `.perk giftstat <player> <statKey> <ranks>` | Grants purchased-style stat ranks. |
| `.perk revokestat <player> <statKey> <ranks>` | Revokes purchased-style stat ranks. |
| `.perk addflat <player> <UnitStat|statKey> <amount>` | Adds an admin flat stat modifier. |
| `.perk clearflat <player> <UnitStat|statKey>` | Removes an admin flat stat modifier. |

## Whitelist commands

| Command | Description |
|---|---|
| `.perk wlstatus` | Shows whitelist status. |
| `.perk wlcheckbuff` | Checks whether you are buff-whitelisted. |
| `.perk wlcheckstat` | Checks whether you are stat-whitelisted. |
| `.perk wlcheckall` | Checks both whitelist types. |
| `.perk wlplayer <player>` | Checks a player whitelist status. |
| `.perk wladdbuff <player>` | Adds a player to the buff whitelist. |
| `.perk wlremovebuff <player>` | Removes a player from the buff whitelist. |
| `.perk wladdstat <player>` | Adds a player to the stat whitelist. |
| `.perk wlremovestat <player>` | Removes a player from the stat whitelist. |

## Notes

- Use `.perk search <text>` when you do not know an exact key.
- Use `.perk validate` after config edits.
- Use `.perk reload` after config edits.
- Use `.perk syncall` after major config changes or migrations.


## Key reference

See [`KEYS.md`](KEYS.md) for the complete short stat and blood-buff key list.
