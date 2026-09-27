using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using BepInEx.Logging;

namespace HutchVRisingTrainer;

internal sealed class AchievementEntry
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "";
    public bool Earned { get; set; }
}

internal sealed class AchievementChecklist : IDisposable
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };
    private readonly ManualLogSource _log;
    private readonly string _steamId;
    private Task<Dictionary<string, bool>>? _pending;
    private DateTime _nextRefreshUtc;

    internal IReadOnlyList<AchievementEntry> Entries { get; }
    internal string Status { get; private set; } = "Showing the catalog. Waiting for Steam profile refresh.";
    internal bool IsLive { get; private set; }
    internal string SteamId => _steamId;
    internal int EarnedCount => Entries.Count(entry => entry.Earned);

    internal AchievementChecklist(string steamId, ManualLogSource log)
    {
        _steamId = steamId.Trim();
        _log = log;
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(
            "HutchVRisingTrainer.data.Achievements.json")
            ?? throw new InvalidOperationException("Embedded achievement catalog is missing.");
        Entries = JsonSerializer.Deserialize<List<AchievementEntry>>(stream)
            ?? throw new InvalidOperationException("Achievement catalog could not be parsed.");
        if (Entries.Count != 49 || Entries.Any(entry => string.IsNullOrWhiteSpace(entry.Id)))
            throw new InvalidOperationException("The embedded achievement catalog is incomplete.");
    }

    internal void RequestRefresh()
    {
        if (_pending is not null) return;
        if (_steamId.Length != 17 || _steamId.Any(character => character < '0' || character > '9'))
        {
            Status = "SteamId64 must contain exactly 17 digits. Check the BepInEx config.";
            IsLive = false;
            return;
        }

        _nextRefreshUtc = DateTime.UtcNow.AddSeconds(30);
        try
        {
            var local = SteamAchievementCache.ReadForCurrentUser(_steamId);
            if (local is not null)
            {
                if (Entries.Any(entry => !local.Value.States.ContainsKey(entry.Id)))
                    throw new InvalidDataException("The local Steam cache is missing V Rising achievement IDs.");
                ApplyStates(local.Value.States,
                    $"Local Steam cache saved {local.Value.UpdatedUtc.ToLocalTime():HH:mm:ss}");
                return;
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or
                                      InvalidDataException or System.Security.SecurityException)
        {
            IsLive = false;
            _nextRefreshUtc = DateTime.UtcNow.AddSeconds(5);
            Status = "Could not read the local Steam cache. Retrying shortly; see BepInEx log.";
            _log.LogWarning("Local Steam cache refresh failed: " + error);
            return;
        }

        _pending = FetchStatesAsync();
        Status = "Contacting the public Steam profile for achievement status...";
    }

    internal void Tick(bool menuVisible)
    {
        if (_pending is null)
        {
            if (menuVisible && DateTime.UtcNow >= _nextRefreshUtc) RequestRefresh();
            return;
        }
        if (!_pending.IsCompleted) return;

        try
        {
            var states = _pending.GetAwaiter().GetResult();
            ApplyStates(states, "Public Steam profile");
        }
        catch (Exception error)
        {
            IsLive = false;
            Status = "Steam refresh failed. Showing the last known checklist; see BepInEx log.";
            _log.LogWarning("Read-only Steam achievement refresh failed: " + error);
        }
        finally
        {
            _pending = null;
        }
    }

    private void ApplyStates(Dictionary<string, bool> states, string source)
    {
        foreach (var entry in Entries) entry.Earned = states[entry.Id];
        IsLive = true;
        Status = $"{source}: {EarnedCount}/{Entries.Count} earned. Updated {DateTime.Now:HH:mm:ss}.";
    }

    private async Task<Dictionary<string, bool>> FetchStatesAsync()
    {
        var url = $"https://steamcommunity.com/profiles/{_steamId}/stats/1604030/achievements/?xml=1";
        using var response = await _http.GetAsync(url).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        });
        var document = XDocument.Load(reader);
        var privacy = document.Root?.Element("privacyState")?.Value;
        if (privacy != "public")
            throw new InvalidOperationException("Steam profile or Game details are not public.");

        var states = document.Root?.Element("achievements")?.Elements("achievement")
            .Where(row => row.Element("apiname") is not null)
            .ToDictionary(row => row.Element("apiname")!.Value,
                row => (string?)row.Attribute("closed") == "1", StringComparer.Ordinal);
        if (states is null || Entries.Any(entry => !states.ContainsKey(entry.Id)))
            throw new InvalidOperationException("Steam returned an incomplete V Rising achievement list.");
        return states;
    }

    public void Dispose() => _http.Dispose();
}
