using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;

namespace NoAggroNoStaminaCost;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public class NoAggroNoStaminaCost : BaseUnityPlugin
{
    public const string PluginGuid = "orax.NoAggroNoStaminaCost";
    public const string PluginName = "NoAggroNoStaminaCost";
    public const string PluginVersion = "0.1.0";

    public static ConfigEntry<bool> Enabled;
    public static ConfigEntry<float> CombatGracePeriod;
    public static ConfigEntry<float> AttackGracePeriod;
    public static ConfigEntry<float> CombatRadius;

    private Harmony _harmony;

    private void Awake()
    {
        Enabled = Config.Bind(
            "General", "Enabled", true,
            "While enabled, the local player's stamina no longer drains as long as they are not fighting. "
            + "You are considered to be fighting while an enemy targets you, while you are hitting a hostile "
            + "character, while a hostile character is hitting you, or while an alerted hostile is fighting "
            + "another target within CombatRadius (multiplayer). "
            + "Swimming always costs stamina, in or out of combat.");

        CombatGracePeriod = Config.Bind(
            "General", "CombatGracePeriod", 1f,
            new ConfigDescription(
                "Delay (in seconds) after an enemy starts targeting you, or hits you, before stamina "
                + "becomes unlimited again. Prevents rapid flickering when a monster loses and quickly "
                + "regains aggro on you. Independent from AttackGracePeriod.",
                new AcceptableValueRange<float>(1f, 30f)));

        AttackGracePeriod = Config.Bind(
            "General", "AttackGracePeriod", 3f,
            new ConfigDescription(
                "Delay (in seconds) after you hit a hostile character before stamina becomes unlimited "
                + "again. Keeps stamina costs active between the slow hits of a fight, even when the "
                + "enemy's aggro is on another player. Set to 0 to disable this and rely on "
                + "CombatGracePeriod only. Independent from CombatGracePeriod.",
                new AcceptableValueRange<float>(0f, 30f)));

        CombatRadius = Config.Bind(
            "General", "CombatRadius", 15f,
            new ConfigDescription(
                "Radius (in meters) in which an alerted hostile that is chasing a target counts "
                + "as combat for you. In multiplayer this catches fights where another player "
                + "holds the enemy's aggro: the target identity is not replicated, but the "
                + "'alerted' and 'haveTarget' states are, so any alerted and targeting hostile "
                + "of yours inside this radius keeps stamina costs active. Set to 0 to disable.",
                new AcceptableValueRange<float>(0f, 100f)));

        _harmony = new Harmony(PluginGuid);
        _harmony.PatchAll();

        Logger.LogInfo($"{PluginName} {PluginVersion} loaded.");
    }

    private void OnDestroy()
    {
        _harmony?.UnpatchSelf();
    }
}
