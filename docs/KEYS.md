# PerkShop Key Reference

These are the player-facing keys used with `.perk statbuy`, `.perk statdet`, `.perk buffbuy`, and `.perk buffdet`.

## Stat keys

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

Stats are enabled by default. Some UnitStat types apply gameplay effects but may not appear in the vanilla TAB/Eclipse attributes UI.

## Blood buff keys

Blood buffs use simple `<bloodType>T<tier>` keys. Dracula blood and General T5 are included as disabled examples by default.

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


## Relic buff keys

| Key | Display name | PrefabGUID | Enabled by default |
|---|---|---:|:---:|
| `relicBehemoth` | Behemoth Relic Blessing | `-1703886455` | yes |
| `relicManticore` | Manticore Relic Blessing | `-238197495` | yes |
| `relicMonster` | Monster Relic Blessing | `1068709119` | yes |
| `relicPaladin` | Paladin Relic Blessing | `-1161197991` | yes |


## Example commands

```text
.perk statbuy PP
.perk statbuy SR
.perk buffbuy warriorT1
.perk buffbuy rogueT2
.perk buffbuy relicBehemoth
```
