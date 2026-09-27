using System.Text.Json;
using System.Text.Json.Nodes;

namespace HutchVRisingTrainer;

internal static class WorldSettingsFile
{
    internal static bool AllowsAllItems(string path) =>
        ReadSettings(path)["TeleportBoundItems"]?.GetValue<bool>() == false;

    internal static float ServantConvertRate(string path) =>
        ReadSettings(path)["ServantConvertRateModifier"]?.GetValue<float>() ?? 1f;

    internal static string SetAllowsAllItems(string path, bool enabled) =>
        UpdateSettings(path, settings => settings["TeleportBoundItems"] = !enabled);

    internal static string SetServantConvertRate(string path, float rate)
    {
        if (rate is not (1f or 3f or 6f))
            throw new ArgumentOutOfRangeException(nameof(rate), "Choose 1x, 3x, or 6x conversion speed.");
        return UpdateSettings(path, settings => settings["ServantConvertRateModifier"] = rate);
    }

    private static string UpdateSettings(string path, Action<JsonObject> update)
    {
        var settings = ReadSettings(path);
        update(settings);
        var backupPath = path + ".hutch-backup";
        if (!File.Exists(backupPath)) File.Copy(path, backupPath);

        var temporaryPath = path + ".hutch-" + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporaryPath, settings.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true
            }) + Environment.NewLine);
            File.Move(temporaryPath, path, true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }

        return backupPath;
    }

    private static JsonObject ReadSettings(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return JsonNode.Parse(stream) as JsonObject ??
            throw new JsonException("ServerGameSettings.json must contain a JSON object.");
    }
}
