using HarmonyLib;

namespace NoAggroNoStaminaCost;

internal class Patch
{
    /// <summary>
    /// Records aggro events on Player.RPC_OnTargeted.
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.RPC_OnTargeted))]
    internal static class Player_RPC_OnTargeted_TrackAggro
    {
        private static void Postfix(Player __instance, bool alerted)
        {
            if (!alerted || __instance != Player.m_localPlayer)
                return;

            AggroState.NotifyAggro();
        }
    }

    /// <summary>
    /// Blocks all stamina consumption for the local player as long as no alerted enemy has
    /// recently targeted them (see Player_RPC_OnTargeted_TrackAggro).
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.UseStamina))]
    internal static class Player_UseStamina_NoDrainOutOfCombat
    {
        private static bool Prefix(Player __instance)
        {
            if (!NoAggroNoStaminaCost.Enabled.Value)
                return true; // Mod disabled -> vanilla behavior.

            if (__instance != Player.m_localPlayer)
                return true; // Never touch other players (multiplayer).

            if (AggroState.IsRecent(NoAggroNoStaminaCost.CombatGracePeriod.Value))
            {
                return true; // In combat (or still within grace period) -> normal consumption.
            }

            return false; // No aggro -> cancel consumption (unlimited stamina).
        }
    }
}
