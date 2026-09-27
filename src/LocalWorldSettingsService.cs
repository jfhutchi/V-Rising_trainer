using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using BepInEx.Logging;

namespace HutchVRisingTrainer;

internal sealed class LocalWorldSettingsService(ManualLogSource log)
{
    private static readonly Regex SaveDirectoryPattern = new(
        @"CreateAndHostServer - SaveDirectory:(?<path>[^,\r\n]+), Loaded Save:",
        RegexOptions.Compiled);
    private static readonly Regex ServantRatePattern = new(
        "\"ServantConvertRateModifier\"\\s*:\\s*(?<rate>[0-9]+(?:\\.[0-9]+)?)",
        RegexOptions.Compiled);

    private readonly string _gameData = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "AppData", "LocalLow", "Stunlock Studios", "VRising");
    private int _serverProcessId;
    private string? _settingsPath;
    private DateTime _nextCheckUtc;
    private bool _travelReloadNeeded;
    private bool _servantReloadNeeded;
    private float? _activeServantConvertRate;

    internal string TravelStatus { get; private set; } = "Load a private local world to change its travel setting.";
    internal string ServantStatus { get; private set; } = "Load a private local world to change servant conversion speed.";
    internal bool Available => _settingsPath is not null;
    internal bool UnrestrictedTravel { get; private set; }
    internal float ServantConvertRate { get; private set; } = 1f;

    internal void Tick(bool visible)
    {
        if (!visible || DateTime.UtcNow < _nextCheckUtc) return;
        _nextCheckUtc = DateTime.UtcNow.AddSeconds(3);

        try
        {
            var servers = System.Diagnostics.Process.GetProcessesByName("VRisingServer");
            try
            {
                if (servers.Length == 0)
                {
                    _serverProcessId = 0;
                    _settingsPath = null;
                    _travelReloadNeeded = false;
                    _servantReloadNeeded = false;
                    _activeServantConvertRate = null;
                    TravelStatus = "Load a private local world to change its travel setting.";
                    ServantStatus = "Load a private local world to change servant conversion speed.";
                    return;
                }
                if (servers.Length != 1)
                {
                    _settingsPath = null;
                    TravelStatus = ServantStatus = "More than one local server is running; close the others to select a world.";
                    return;
                }

                var server = servers[0];
                if (server.Id != _serverProcessId)
                {
                    _serverProcessId = server.Id;
                    _settingsPath = null;
                    _travelReloadNeeded = false;
                    _servantReloadNeeded = false;
                    _activeServantConvertRate = null;
                }
                if (_settingsPath is null)
                    _settingsPath = FindActiveSettingsFile(server.StartTime.ToUniversalTime());

                if (_settingsPath is null)
                {
                    TravelStatus = ServantStatus = "Could not identify the active private world from its server log.";
                    return;
                }

                UnrestrictedTravel = WorldSettingsFile.AllowsAllItems(_settingsPath);
                ServantConvertRate = WorldSettingsFile.ServantConvertRate(_settingsPath);
                TravelStatus = _travelReloadNeeded
                    ? "Saved for this world. Leave and reload it to apply the travel change."
                    : UnrestrictedTravel
                        ? "This world's saved setting allows bound items through waygates."
                        : "This world's saved setting blocks bound items at waygates.";
                ServantStatus = DescribeServantRate();
            }
            finally
            {
                foreach (var server in servers) server.Dispose();
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or
                                      JsonException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            _settingsPath = null;
            TravelStatus = ServantStatus = "Could not read this private world's settings; see the BepInEx log.";
            log.LogWarning("Private world settings read failed: " + error);
        }
    }

    internal void SetUnrestrictedTravel(bool enabled)
    {
        if (_settingsPath is null)
        {
            TravelStatus = "Load a private local world before changing its travel setting.";
            return;
        }

        try
        {
            var backupPath = WorldSettingsFile.SetAllowsAllItems(_settingsPath, enabled);
            UnrestrictedTravel = enabled;
            _travelReloadNeeded = true;
            TravelStatus = "Saved for this world. Leave and reload it to apply the travel change.";
            log.LogInfo($"Set TeleportBoundItems={!enabled} in {_settingsPath}. Original saved at {backupPath}.");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or
                                      JsonException or InvalidOperationException)
        {
            TravelStatus = "Could not save the travel setting; see the BepInEx log.";
            log.LogError("Waypoint travel setting write failed: " + error);
        }
    }

    internal void SetServantConvertRate(float rate)
    {
        if (_settingsPath is null)
        {
            ServantStatus = "Load a private local world before changing servant conversion speed.";
            return;
        }

        try
        {
            var backupPath = WorldSettingsFile.SetServantConvertRate(_settingsPath, rate);
            ServantConvertRate = rate;
            _servantReloadNeeded = true;
            ServantStatus = DescribeServantRate();
            log.LogInfo($"Set ServantConvertRateModifier={rate} in {_settingsPath}. Original saved at {backupPath}.");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or
                                      JsonException or InvalidOperationException or ArgumentOutOfRangeException)
        {
            ServantStatus = "Could not save servant conversion speed; see the BepInEx log.";
            log.LogError("Servant conversion speed write failed: " + error);
        }
    }

    private string? FindActiveSettingsFile(DateTime serverStartUtc)
    {
        var logPath = Path.Combine(_gameData, "Player-server.log");
        if (!File.Exists(logPath) || File.GetLastWriteTimeUtc(logPath) < serverStartUtc) return null;

        using var stream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        var logText = reader.ReadToEnd();
        var matches = SaveDirectoryPattern.Matches(logText);
        if (matches.Count == 0) return null;

        var finalSettingsAt = logText.LastIndexOf("Final ServerGameSettings Values:", StringComparison.Ordinal);
        if (finalSettingsAt >= 0)
        {
            var rateMatch = ServantRatePattern.Match(logText, finalSettingsAt);
            if (rateMatch.Success && float.TryParse(rateMatch.Groups["rate"].Value,
                    NumberStyles.Float, CultureInfo.InvariantCulture, out var rate))
                _activeServantConvertRate = rate;
        }

        var saveDirectory = Path.GetFullPath(matches[^1].Groups["path"].Value.Trim());
        var cloudRoot = Path.GetFullPath(Path.Combine(_gameData, "CloudSaves"));
        var relative = Path.GetRelativePath(cloudRoot, saveDirectory);
        var parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (parts.Length != 3 || !ulong.TryParse(parts[0], out _) ||
            !Regex.IsMatch(parts[1], @"^v\d+$") || !Guid.TryParse(parts[2], out _))
            return null;

        var settingsPath = Path.Combine(saveDirectory, "ServerGameSettings.json");
        return File.Exists(settingsPath) ? settingsPath : null;
    }

    private string DescribeServantRate()
    {
        var saved = ServantConvertRate.ToString("0.##", CultureInfo.InvariantCulture);
        if (_activeServantConvertRate is not { } active)
            return _servantReloadNeeded
                ? $"Saved {saved}x for this world. Leave and reload it to apply the change."
                : $"Saved servant conversion speed: {saved}x. Active server rate is unavailable.";

        var loaded = active.ToString("0.##", CultureInfo.InvariantCulture);
        return Math.Abs(active - ServantConvertRate) < 0.01f
            ? $"Active servant conversion speed: {loaded}x."
            : $"Active now: {loaded}x. Saved for next world load: {saved}x. Leave and reload the world.";
    }

}
