using UnityEngine;

namespace NoAggroNoStaminaCost;

/// <summary>
/// Holds the timestamps of the last combat events seen by the local player:
/// aggro/damage received (CombatGracePeriod) and damage dealt (AttackGracePeriod).
/// </summary>
internal static class AggroState
{
    private const float NearbyScanInterval = 0.25f;

    private static float _lastAggroTime = float.NegativeInfinity;
    private static float _lastAttackTime = float.NegativeInfinity;
    private static float _lastNearbyScanTime = float.NegativeInfinity;
    private static bool _nearbyCombat;

    /// <summary>
    /// A hostile started targeting me, or hit me.
    /// </summary>
    /// <param name="reason">
    /// Optional detail written to the mod's log. Null keeps the call silent, which is what the
    /// nearby-combat scan uses: it refreshes the timestamp every NearbyScanInterval seconds and
    /// would otherwise print a line 4 times per second.
    /// </param>
    public static void NotifyAggro(string reason = null)
    {
        if (reason != null)
            NoAggroNoStaminaCost.Log($"NotifyAggro: {reason} (CombatGracePeriod refreshed).");

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
    /// True when a hostile that is alerted and chasing a target (in multiplayer: another player
    /// holding its aggro) stands within <paramref name="radius"/> meters of the local player.
    /// The target's ZDOID is never replicated, but "alerted" and "haveTarget" are, so this works
    /// for clients and hosts alike. Result is cached for NearbyScanInterval seconds; when a
    /// combat is found the aggro timestamp is refreshed so CombatGracePeriod applies afterwards.
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
                    ? $"IsCombatNearby: alerted hostile fighting within {radius:0.#}m (multiplayer)."
                    : "IsCombatNearby: no alerted hostile fighting nearby anymore."
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

        foreach (BaseAI ai in BaseAI.GetAllInstances())
        {
            if (ai == null || !ai.IsAlerted() || !ai.HaveTarget())
                continue; // Cheap checks first: most AI instances are idle.

            Character ch = ai.GetComponent<Character>();
            if (ch == null || ch.IsDead())
                continue;

            if (!BaseAI.IsEnemy(local, ch))
                continue; // Friendly tames / Dvergr / same-group characters don't count.

            if ((ch.transform.position - position).sqrMagnitude <= sqrRadius)
                return true;
        }

        return false;
    }
}
