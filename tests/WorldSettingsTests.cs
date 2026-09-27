using System.Text.Json.Nodes;
using HutchVRisingTrainer;

var root = Path.Combine(Path.GetTempPath(), "HutchWorldTravelTests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    var settingsPath = Path.Combine(root, "ServerGameSettings.json");
    const string original = """
        {
          "GameDifficulty": 2,
          "UnitStatModifiers_VBlood": { "PowerModifier": 1.7 },
          "UnlockedAchievements": []
        }
        """;
    File.WriteAllText(settingsPath, original);
    if (WorldSettingsFile.AllowsAllItems(settingsPath))
        throw new Exception("The absent setting should use the game's restrictive default.");

    var backupPath = WorldSettingsFile.SetAllowsAllItems(settingsPath, true);
    if (!WorldSettingsFile.AllowsAllItems(settingsPath))
        throw new Exception("Travel with all items was not enabled.");
    if (File.ReadAllText(backupPath) != original)
        throw new Exception("The original settings were not backed up.");

    var settings = JsonNode.Parse(File.ReadAllText(settingsPath))!.AsObject();
    if (settings["GameDifficulty"]!.GetValue<int>() != 2 ||
        settings["UnitStatModifiers_VBlood"]!["PowerModifier"]!.GetValue<double>() != 1.7 ||
        settings["UnlockedAchievements"]!.AsArray().Count != 0)
        throw new Exception("Unrelated world settings changed.");

    WorldSettingsFile.SetAllowsAllItems(settingsPath, false);
    if (WorldSettingsFile.AllowsAllItems(settingsPath) || File.ReadAllText(backupPath) != original)
        throw new Exception("Restoring the restriction changed the original backup.");

    if (WorldSettingsFile.ServantConvertRate(settingsPath) != 1f)
        throw new Exception("The absent servant rate should use the game's 1x default.");
    WorldSettingsFile.SetServantConvertRate(settingsPath, 6f);
    if (WorldSettingsFile.ServantConvertRate(settingsPath) != 6f ||
        WorldSettingsFile.AllowsAllItems(settingsPath) ||
        File.ReadAllText(backupPath) != original)
        throw new Exception("The faster servant rate changed another setting or the backup.");
    WorldSettingsFile.SetServantConvertRate(settingsPath, 1f);
    if (WorldSettingsFile.ServantConvertRate(settingsPath) != 1f)
        throw new Exception("The normal servant conversion rate was not restored.");
    try
    {
        WorldSettingsFile.SetServantConvertRate(settingsPath, 10f);
        throw new Exception("An unsupported conversion rate was accepted.");
    }
    catch (ArgumentOutOfRangeException) { }

    File.WriteAllText(settingsPath, "{ invalid json");
    try
    {
        WorldSettingsFile.SetServantConvertRate(settingsPath, 6f);
        throw new Exception("Invalid JSON was accepted.");
    }
    catch (System.Text.Json.JsonException) { }
    if (File.ReadAllText(settingsPath) != "{ invalid json")
        throw new Exception("Invalid settings were overwritten.");

    Console.WriteLine("World settings tests passed.");
}
finally
{
    var safeRoot = Path.GetFullPath(Path.GetTempPath());
    var target = Path.GetFullPath(root);
    if (!target.StartsWith(safeRoot, StringComparison.OrdinalIgnoreCase) ||
        Path.GetFileName(target) is not { } name || !name.StartsWith("HutchWorldTravelTests-"))
        throw new Exception("Refusing to remove a test directory outside the temporary root.");
    Directory.Delete(target, true);
}
