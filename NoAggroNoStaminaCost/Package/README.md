# Description

Provides unlimited stamina as long as you are not fighting.

You count as fighting while an enemy is targeting you, while you are hitting a hostile
character, or while a hostile character is hitting you. In multiplayer this also covers
fights where the enemy's aggro is on another player. Environmental damage (falling,
drowning, fire, traps) does not count as combat.

Attack speed, movement, jumping, swimming, building and gathering are all affected while
in combat you pay stamina exactly like in vanilla.

# Configuration

| Setting | Default | Description |
|---|---|---|
| `Enabled` | `true` | Master switch. |
| `CombatGracePeriod` | `1` | Seconds after an enemy targets you or hits you before stamina becomes unlimited again (range 1-30). |
| `AttackGracePeriod` | `3` | Seconds after **you** hit a hostile character before stamina becomes unlimited again, even if the enemy's aggro is on another player. `0` disables it (range 0-30). |
| `CombatRadius` | `15` | Meters in which an alerted hostile chasing a target (another player holding its aggro, in multiplayer) counts as combat for you. `0` disables it (range 0-100). |

# Installation

This mod is client-side only. You do not need to install it on the server.

## Using a mod manager (recommended)
You can install the mod using Vortex, R2ModMan or Gale.

## Manual
Install BepInExPack for Valheim.
Copy the DLL file to `Steam\steamapps\common\Valheim\BepInEx\plugins\`

# Misc.

Source code: https://github.com/orax-Valheim-mods/NoAggroNoStaminaCost
