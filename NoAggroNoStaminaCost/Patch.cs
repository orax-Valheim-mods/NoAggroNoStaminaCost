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
    /// Blocks all stamina consumption for the local player as long as no combat event has been
    /// recorded recently (see Player_RPC_OnTargeted_TrackAggro, Character_Damage_TrackCombat,
    /// Player_OnDamaged_TrackCombat).
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
                return true; // Targeted or hit by an enemy (or still inside that grace period).
            }

            if (AggroState.IsAttackRecent(NoAggroNoStaminaCost.AttackGracePeriod.Value))
            {
                return true; // I hit a hostile recently (or still inside that grace period).
            }

            if (AggroState.IsCombatNearby(NoAggroNoStaminaCost.CombatRadius.Value))
            {
                return true; // A hostile fights another target nearby (multiplayer aggro).
            }

            return false; // No combat -> cancel consumption (unlimited stamina).
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
                AggroState.NotifyAttack(); // -> AttackGracePeriod
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
                AggroState.NotifyAggro(); // -> CombatGracePeriod
        }
    }
}
