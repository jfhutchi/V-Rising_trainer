using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace HutchVRisingTrainer;

internal sealed class ItemCatalogEntry
{
    public string Name { get; set; } = "";
    public int Id { get; set; }
    public string DisplayName { get; set; } = "";
}

internal static class ItemCatalog
{
    private static readonly Lazy<IReadOnlyList<ItemCatalogEntry>> Entries = new(Load);

    internal static IReadOnlyList<ItemCatalogEntry> All => Entries.Value;

    internal static IReadOnlyList<ItemCatalogEntry> Search(string query)
    {
        var words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length == 0) return All;
        return All.Where(entry => words.All(word =>
                entry.Name.Contains(word, StringComparison.OrdinalIgnoreCase) ||
                entry.DisplayName.Contains(word, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
    }

    private static IReadOnlyList<ItemCatalogEntry> Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(
            "HutchVRisingTrainer.data.Items.json")
            ?? throw new InvalidOperationException("Embedded item catalog is missing.");
        var entries = JsonSerializer.Deserialize<List<ItemCatalogEntry>>(stream)
            ?? throw new InvalidOperationException("Embedded item catalog could not be parsed.");
        if (entries.Count == 0 || entries.Any(entry => !entry.Name.StartsWith("Item_")))
            throw new InvalidOperationException("Embedded item catalog is invalid.");
        foreach (var entry in entries) entry.DisplayName = MakeDisplayName(entry.Name);
        return entries.OrderBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Id).ToArray();
    }

    private static string MakeDisplayName(string name)
    {
        var chunks = name["Item_".Length..].Split('_', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" / ", chunks.Select(chunk =>
        {
            var tier = Regex.Match(chunk, @"^T0?(\d+)$");
            return tier.Success ? "Tier " + tier.Groups[1].Value :
                Regex.Replace(chunk, @"(?<=[a-z0-9])(?=[A-Z])", " ");
        }));
    }
}
