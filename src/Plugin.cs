using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using HutchVRisingTrainer.UI;
using UnityEngine;

namespace HutchVRisingTrainer;

[BepInProcess("VRising.exe")]
[BepInPlugin(Id, Name, Version)]
public sealed class Plugin : BasePlugin
{
    public const string Id = "hutch.vrising.achievementtrainer";
    public const string Name = "Hutch V Rising Achievement Trainer";
    public const string Version = "0.6.4";
    private Harmony? _harmony;

    public override void Load()
    {
        if (System.Diagnostics.Process.GetCurrentProcess().ProcessName.Equals(
                "VRisingServer", StringComparison.OrdinalIgnoreCase))
        {
            Log.LogInfo("Client checklist is not loaded in the server process.");
            return;
        }

        var menuKey = Config.Bind("Interface", "ToggleKey", KeyCode.F6,
            "Open or close the trainer menu. Change this if it conflicts with a game binding.");
        var instantConvertKey = Config.Bind("Interface", "InstantConvertKey", KeyCode.F7,
            "While the trainer is closed, aim at a converting servant coffin and press this key to finish it. Requires adminauth.");
        var clearBoostsOnJoin = Config.Bind("Safety", "ClearBoostsOnJoin", true,
            "Send .mortal once after joining a local world to clear character boosts left by a previous session or crash. Requires admin rights.");
        var steamId = Config.Bind("Achievements", "SteamId64", "76561199099998102",
            "Steam ID used for the read-only checklist. The signed-in client's local cache is used when available; another profile needs public Game details.");
        var checklist = new AchievementChecklist(steamId.Value, Log);
        var commands = new ClientCommandService(Log, clearBoostsOnJoin.Value);
        var worldSettings = new LocalWorldSettingsService(Log);
        ClientCommandService.Active = commands;
        _harmony = new Harmony(Id);
        _harmony.PatchAll(typeof(Plugin).Assembly);
        var window = new TrainerWindow(checklist, commands, worldSettings, menuKey, instantConvertKey);
        AddComponent<TrainerBehaviour>().Initialize(window, checklist, commands, worldSettings, menuKey,
            instantConvertKey, Log);
        Log.LogInfo($"{Name} {Version} loaded. Menu key: {menuKey.Value}. Live commands require a local server and adminauth.");
    }

    public override bool Unload()
    {
        _harmony?.UnpatchSelf();
        ClientCommandService.Active = null;
        return true;
    }
}
