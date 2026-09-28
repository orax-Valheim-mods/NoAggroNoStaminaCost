# Changelog

All notable changes to NoAggroNoStaminaCost.
Sections follow [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), versions follow
[Semantic Versioning](https://semver.org/).

## 0.1.0 - 2026-09-28

First release. Client-side only: it works for the host and for clients alike, the server
does not need the mod.

### Added

- **Unlimited stamina while out of combat**: attacking, sprinting, jumping, building and
  gathering cost no stamina until a combat event is recorded. While in combat, stamina
  behaves exactly like vanilla.
- **Combat detection** from four sources:
  - an enemy starts targeting you (`Player.RPC_OnTargeted`),
  - you hit a hostile character, melee/bow/magic/AoE (`Character.Damage`),
  - a hostile character hits you (`Player.OnDamaged`),
  - an alerted enemy fights an **ally** within `CombatRadius` (multiplayer).
- **Environmental damage never counts as combat**: falling, drowning, fire and traps have
  no attacking character, so they are ignored.
- **Configuration**, in `BepInEx\config\orax.NoAggroNoStaminaCost.cfg`:

  | Option | Default | Range | Description |
  |---|---|---|---|
  | `Enabled` | `true` | - | Master switch. |
  | `CombatGracePeriod` | `1` | 1-30 s | Stamina cost kept after being targeted or hit, or after a nearby ally fight is detected. |
  | `AttackGracePeriod` | `3` | 0-30 s | Stamina cost kept after you hit a hostile. `0` disables it. |
  | `CombatRadius` | `15` | 0-100 m | Radius of the nearby-fight detection. `0` disables it. |
  | `LogsEnabled` | `true` | - | Writes the mod's diagnostic lines to `BepInEx\LogOutput.log`. |

- **Diagnostic logs** (toggle with `LogsEnabled`): startup config summary, one line per hit
  you land, combat state transitions of `UseStamina`, and nearby-fight transitions.

### Changed

- **Swimming always costs stamina**, in or out of combat (running out of stamina while
  swimming is what drowns you).
- Nearby combat only counts a fight against an **ally**: another player (multiplayer) or a
  tamed/summoned mob. Fights between two wild mobs, or a mob attacking a building, never
  count.

### Fixed

- Hits the game itself discards (dead target, or a victim with PVP disabled) no longer
  count as combat.
- The `NotifyAggro` log line is written at most once per second: the game re-sends the
  "targeted" event every 0.5 s for as long as a hostile keeps its aggro on you.
