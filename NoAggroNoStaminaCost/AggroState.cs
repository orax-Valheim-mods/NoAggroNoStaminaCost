using UnityEngine;

namespace NoAggroNoStaminaCost;

/// <summary>
/// Holds the timestamp of the last confirmed aggro event on the local player.
/// </summary>
internal static class AggroState
{
    private static float _lastAggroTime = float.NegativeInfinity;

    public static void NotifyAggro()
    {
        _lastAggroTime = Time.time;
    }

    public static bool IsRecent(float gracePeriod)
    {
        return Time.time - _lastAggroTime <= gracePeriod;
    }
}
