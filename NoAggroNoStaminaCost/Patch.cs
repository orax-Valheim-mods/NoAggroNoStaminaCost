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

            AggroState.NotifyAggro("a hostile started targeting the local player");
        }
    }

    /// <summary>
    /// Blocks stamina consumption for the local player as long as no combat event has been
    /// recorded recently (see Player_RPC_OnTargeted_TrackAggro, Character_Damage_TrackCombat,
    /// Player_OnDamaged_TrackCombat, and the nearby-combat scan), with one exception:
    /// swimming always costs stamina, in or out of combat.
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.UseStamina))]
    internal static class Player_UseStamina_NoDrainOutOfCombat
    {
        // Decision returned the last time the prefix ran: only transitions are logged.
        private static bool _paidLastTime = true;

        private static bool Prefix(Player __instance)
        {
            if (!NoAggroNoStaminaCost.Enabled.Value)
                return true; // Mod disabled -> vanilla behavior.

            if (__instance != Player.m_localPlayer)
                return true; // Never touch other players (multiplayer).

            if (__instance.IsSwimming())
                // Swimming is never free: Player.OnSwimming -> UseStamina must pay
                // (running out of stamina while swimming is what drowns you).
                return Decide(true, "swimming always costs stamina");

            if (AggroState.IsRecent(NoAggroNoStaminaCost.CombatGracePeriod.Value))
                return Decide(true, "CombatGracePeriod: a hostile targets me or hit me");

            if (AggroState.IsAttackRecent(NoAggroNoStaminaCost.AttackGracePeriod.Value))
                return Decide(true, "AttackGracePeriod: I hit a hostile recently");

            if (AggroState.IsCombatNearby(NoAggroNoStaminaCost.CombatRadius.Value))
                return Decide(true, "an alerted hostile fights nearby");

            return Decide(false, "no combat");
        }

        /// <summary>
        /// Returns <paramref name="pay" /> (true = stamina is consumed, false = it is free) and
        /// writes a log line only when the decision changed since the previous call, so the log
        /// shows the mod toggling instead of one line per stamina drain attempt.
        /// </summary>
        private static bool Decide(bool pay, string reason)
        {
            if (pay != _paidLastTime)
            {
                _paidLastTime = pay;
                NoAggroNoStaminaCost.Log(
                    pay
                        ? $"UseStamina: stamina is limited again ({reason})."
                        : "UseStamina: no combat, stamina is unlimited."
                );
            }

            return pay;
        }
    }

    /// <summary>
    /// Records every hit the local player lands on a hostile character (melee, bow, magic, AoE).
    /// In multiplayer the mob's formal aggro is often on another player, but hitting it still
    /// means we are fighting it. This runs on the attacking peer (Character.Damage forwards the
    /// hit to the owner through RPC_Damage), so it works for host and client alike.
    /// Trees, rocks and buildings use IDestructible implementations other than Character and
    /// therefore never reach this patch: gathering and building stay stamina free.
    /// </summary>
    [HarmonyPatch(typeof(Character), nameof(Character.Damage))]
    internal static class Character_Damage_TrackCombat
    {
        private static void Postfix(Character __instance, HitData hit)
        {
            if (hit == null || !hit.HaveAttacker())
                return;

            Player local = Player.m_localPlayer;
            if (local == null || __instance == local)
                return;

            if (hit.GetAttacker() != local)
                return; // Damage between other characters (or a hit we only replicated).

            // Hostile mobs count; friendly tames and passive Dvergr do not (BaseAI.IsEnemy).
            // Hitting another player (PvP) always counts.
            if (__instance is Player || BaseAI.IsEnemy(local, __instance))
                AggroState.NotifyAttack(__instance.m_name); // -> AttackGracePeriod
        }
    }

    /// <summary>
    /// Records damage taken by the local player from a hostile character (adds, AoE, or a mob
    /// whose formal aggro sits on an ally but that still hits us). Runs locally through
    /// Character.RPC_Damage on the owner of our ZDO.
    /// Damage without a Character attacker (falling, drowning, fire, traps) does not count.
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.OnDamaged))]
    internal static class Player_OnDamaged_TrackCombat
    {
        private static void Postfix(Player __instance, HitData hit)
        {
            if (__instance != Player.m_localPlayer || hit == null || !hit.HaveAttacker())
                return;

            Character attacker = hit.GetAttacker();
            if (attacker == null || attacker == __instance)
                return;

            if (attacker is Player || BaseAI.IsEnemy(__instance, attacker))
                AggroState.NotifyAggro($"damaged by {attacker.m_name}"); // -> CombatGracePeriod
        }
    }
}
