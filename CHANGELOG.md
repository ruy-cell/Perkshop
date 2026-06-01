# Changelog

## 0.2.1

- Changed perk restoration to manual per-session sync: perks are no longer auto-applied on login and no longer auto-reapplied in the background when missing.
- Added clearer `.perk help` guidance that owned perks are restored manually with `.perk sync`.
- Kept the existing in-session duration/lifetime behavior for non-blood perks so synced perks still behave as session-permanent until logout.
- Changed `blood_buff` purchases and restores to stop mutating live blood buff lifetimes, which fixes crashes when drinking matching blood potions after buying blood perks.
- Updated blood-buff config notes to reflect the safer non-permanent lifetime handling for native blood buffs.
- Fixed game-breaking bug where drinking a blood potion while having a bloodbuff perk would crash the game
- Fixed game-breaking bug where the server could not reapply a bloodbuff while also having the bloodtype active, ending up in a crash. 