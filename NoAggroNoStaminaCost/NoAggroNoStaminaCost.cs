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

    private Harmony _harmony;

    private void Awake()
    {
        Enabled = Config.Bind(
            "General", "Enabled", true,
            "While enabled, the local player's stamina no longer drains as long as no enemy has aggro on them.");

        CombatGracePeriod = Config.Bind(
            "General", "CombatGracePeriod", 1f,
            new ConfigDescription(
                "Delay (in seconds) after losing aggro before stamina becomes unlimited again. " +
                "Prevents rapid flickering when a monster loses and quickly regains aggro.",
                new AcceptableValueRange<float>(1f, 30f)));

        _harmony = new Harmony(PluginGuid);
        _harmony.PatchAll();

        Logger.LogInfo($"{PluginName} {PluginVersion} loaded.");
    }

    private void OnDestroy()
    {
        _harmony?.UnpatchSelf();
    }
}
