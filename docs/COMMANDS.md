# PerkShop Commands

Command prefix:

```text
.perk
```

## Player commands

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

## Admin commands

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

## Whitelist commands

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

## Notes

- Use `.perk search <text>` when you do not know an exact key.
- Use `.perk validate` after config edits.
- Use `.perk reload` after config edits.
- Use `.perk syncall` after major config changes or migrations.

## Key reference

See [`KEYS.md`](KEYS.md) for the complete short stat, blood-buff, and relic key lists.
