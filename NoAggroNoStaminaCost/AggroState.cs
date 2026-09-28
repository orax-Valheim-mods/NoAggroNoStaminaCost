using System.Collections.Generic;
using UnityEngine;

namespace NoAggroNoStaminaCost;

/// <summary>
/// Holds the combat state of the local player: the timestamps of the last combat events
/// (aggro/damage received = CombatGracePeriod, damage dealt = AttackGracePeriod) and the
/// throttled scan for fights between an enemy and an ally nearby (CombatRadius).
/// </summary>
internal static class AggroState
{
    private const float NearbyScanInterval = 0.25f;
    private const float AggroLogRepeatInterval = 1f;

    private static float _lastAggroTime = float.NegativeInfinity;
    private static float _lastAttackTime = float.NegativeInfinity;
    private static float _lastNearbyScanTime = float.NegativeInfinity;
    private static bool _nearbyCombat;
    private static string _lastAggroLogReason;
    private static float _lastAggroLogTime = float.NegativeInfinity;

    /// <summary>
    /// A hostile started targeting me, hit me, or a fight against an ally was detected nearby.
    /// Always refreshes the CombatGracePeriod timestamp.
    /// </summary>
    /// <param name="reason">
    /// Optional detail written to the mod's log. Null keeps the call silent, which is what the
    /// nearby-combat scan uses: it refreshes the timestamp every NearbyScanInterval seconds and
    /// would otherwise print a line 4 times per second. A reason repeating within
    /// AggroLogRepeatInterval is logged only once, because the game re-sends "targeted" every
    /// 0.5 s for as long as a hostile keeps its aggro on me.
    /// </param>
    public static void NotifyAggro(string reason = null)
    {
        if (
            reason != null
            && (
                reason != _lastAggroLogReason
                || Time.time - _lastAggroLogTime >= AggroLogRepeatInterval
            )
        )
        {
            _lastAggroLogReason = reason;
            _lastAggroLogTime = Time.time;
            NoAggroNoStaminaCost.Log($"NotifyAggro: {reason} (CombatGracePeriod refreshed).");
        }

        _lastAggroTime = Time.time;
    }

    /// <summary>I hit a hostile character.</summary>
    /// <param name="target">Name of the character I hit, logged for context.</param>
    public static void NotifyAttack(string target = null)
    {
        NoAggroNoStaminaCost.Log(
            $"NotifyAttack: local player hit {target ?? "a hostile"} (AttackGracePeriod refreshed)."
        );

        _lastAttackTime = Time.time;
    }

    public static bool IsRecent(float gracePeriod)
    {
        return Time.time - _lastAggroTime <= gracePeriod;
    }

    public static bool IsAttackRecent(float gracePeriod)
    {
        return Time.time - _lastAttackTime <= gracePeriod;
    }

    /// <summary>
    /// True when an enemy of the local player is alerted and fighting an ally, with at least one
    /// of the two combatants within <paramref name="radius" /> meters of the local player:
    /// the ally being another player (multiplayer) or a tamed/summoned mob. Fights between two
    /// wild mobs, or a mob chewing on a building, never count.
    /// The target's ZDOID is never replicated, but "alerted" and "haveTarget" are: the peer that
    /// simulates the AI knows exactly what the enemy is fighting, other peers only know that it
    /// fights "something" and fall back to "an ally stands right next to the fight".
    /// Result is cached for NearbyScanInterval seconds; while a fight is found the aggro
    /// timestamp is refreshed so CombatGracePeriod applies during the fight and right after it.
    /// </summary>
    public static bool IsCombatNearby(float radius)
    {
        if (radius <= 0f)
            return false;

        if (Time.time - _lastNearbyScanTime < NearbyScanInterval)
            return _nearbyCombat;

        _lastNearbyScanTime = Time.time;
        bool found = ScanForNearbyCombat(radius);
        if (found != _nearbyCombat)
        {
            NoAggroNoStaminaCost.Log(
                found
                    ? $"IsCombatNearby: alerted hostile fighting an ally within {radius:0.#}m (multiplayer)."
                    : "IsCombatNearby: no alerted hostile fighting an ally nearby anymore."
            );
        }

        _nearbyCombat = found;
        if (_nearbyCombat)
            NotifyAggro(); // Keep paying during CombatGracePeriod after the fight moves away.

        return _nearbyCombat;
    }

    private static bool ScanForNearbyCombat(float radius)
    {
        Player local = Player.m_localPlayer;
        if (local == null)
            return false;

        Vector3 position = local.transform.position;
        float sqrRadius = radius * radius;
        List<Character> allies = CollectAllies(local);

        foreach (BaseAI ai in BaseAI.GetAllInstances())
        {
            if (ai == null || !ai.IsAlerted() || !ai.HaveTarget())
                continue; // Cheap checks first: most AI instances are idle.

            Character enemy = ai.GetComponent<Character>();
            if (enemy == null || enemy.IsDead())
                continue;

            if (!BaseAI.IsEnemy(local, enemy))
                continue; // Friendly tames / Dvergr / same-group characters don't count.

            bool enemyNear = (enemy.transform.position - position).sqrMagnitude <= sqrRadius;

            Character target = ai.GetTargetCreature();
            if (target != null && !target.IsDead())
            {
                // This peer simulates the AI, so we know what the enemy is fighting:
                // only a fight against an ally counts, never enemy vs enemy.
                if (!IsAllied(target))
                    continue;

                if (enemyNear || (target.transform.position - position).sqrMagnitude <= sqrRadius)
                    return true;

                continue;
            }

            // Target identity unknown (this peer does not simulate the AI: typically a client
            // while the host owns the mob, or the AI is chasing a building). Count it only when
            // an ally stands right next to the fight. The local player is deliberately not
            // among those allies: aggro on us already arrives via RPC_OnTargeted / OnDamaged,
            // so a wild-vs-wild fight next to us alone does not count.
            if (enemyNear && IsAllyNextTo(allies, enemy.transform.position, radius))
                return true;
        }

        return false;
    }

    /// <summary>
    /// True for characters fighting on our side: the local player, any other player (their
    /// Player object exists locally whenever they are in range), and allied mobs.
    /// </summary>
    private static bool IsAllied(Character character)
    {
        if (character == null || character.IsDead())
            return false;

        if (character is Player)
            return true; // Me or another player.

        return IsAlliedMob(character);
    }

    /// <summary>
    /// True for mobs on the players' side: tamed pets (Character.IsTamed reads the replicated
    /// "tamed" flag), creatures with the PlayerSpawned faction, and creatures commanded to
    /// follow a player (replicated "follow" flag, set for summons). Wild and neutral mobs
    /// (deer, Dvergr, enemy factions of each other, ...) are not allies.
    /// </summary>
    private static bool IsAlliedMob(Character character)
    {
        if (character.IsTamed() || character.GetFaction() == Character.Faction.PlayerSpawned)
            return true;

        ZDO zdo = character.GetComponent<ZNetView>()?.GetZDO();
        return zdo != null && !string.IsNullOrEmpty(zdo.GetString(ZDOVars.s_follow));
    }

    /// <summary>
    /// Characters that can take part in a nearby fight for the local player: other players and
    /// allied mobs. The local player is excluded (see ScanForNearbyCombat).
    /// </summary>
    private static List<Character> CollectAllies(Player local)
    {
        List<Character> allies = new List<Character>();

        foreach (Player player in Player.GetAllPlayers())
        {
            if (player != null && player != local && !player.IsDead())
                allies.Add(player);
        }

        foreach (Character character in Character.GetAllCharacters())
        {
            if (
                character != null
                && character is not Player
                && !character.IsDead()
                && IsAlliedMob(character)
            )
                allies.Add(character);
        }

        return allies;
    }

    /// <summary>True when any of the allies stands within radius of the fight.</summary>
    private static bool IsAllyNextTo(List<Character> allies, Vector3 fightPosition, float radius)
    {
        float sqrRadius = radius * radius;

        foreach (Character ally in allies)
        {
            if ((ally.transform.position - fightPosition).sqrMagnitude <= sqrRadius)
                return true;
        }

        return false;
    }
}
