using System.Globalization;
using System.Text;
using Microsoft.Win32;

namespace HutchVRisingTrainer;

internal static class SteamAchievementCache
{
    private const ulong SteamIdBase = 76561197960265728;
    private const string AppId = "1604030";

    internal static string? ResolveSteamId(string configuredId, int? activeUser)
    {
        if (!string.IsNullOrWhiteSpace(configuredId)) return configuredId.Trim();
        if (activeUser is null or 0) return null;
        return (SteamIdBase + unchecked((uint)activeUser.Value)).ToString(CultureInfo.InvariantCulture);
    }

    internal static string? ResolveCurrentSteamId(string configuredId)
    {
        if (!string.IsNullOrWhiteSpace(configuredId)) return configuredId.Trim();
        if (!OperatingSystem.IsWindows()) return null;
        using var activeKey = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam\ActiveProcess");
        return ResolveSteamId(configuredId, activeKey?.GetValue("ActiveUser") as int?);
    }

    internal static (Dictionary<string, bool> States, DateTime UpdatedUtc)? ReadForCurrentUser(string steamId)
    {
        if (!OperatingSystem.IsWindows()) return null;
        if (!ulong.TryParse(steamId, NumberStyles.None, CultureInfo.InvariantCulture, out var id) ||
            id < SteamIdBase || id - SteamIdBase > uint.MaxValue)
            return null;

        using var steamKey = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
        using var activeKey = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam\ActiveProcess");
        if (steamKey?.GetValue("SteamPath") is not string steamPath ||
            activeKey?.GetValue("ActiveUser") is not int activeUser ||
            unchecked((uint)activeUser) != (uint)(id - SteamIdBase))
            return null;

        var directory = Path.Combine(steamPath, "appcache", "stats");
        var schemaPath = Path.Combine(directory, $"UserGameStatsSchema_{AppId}.bin");
        var statsPath = Path.Combine(directory, $"UserGameStats_{unchecked((uint)activeUser)}_{AppId}.bin");
        if (!File.Exists(schemaPath) || !File.Exists(statsPath)) return null;

        var states = Parse(ReadFile(schemaPath), ReadFile(statsPath));
        if (states.Count != 49)
            throw new InvalidDataException($"Steam cache schema has {states.Count} V Rising achievements, expected 49.");
        return (states, File.GetLastWriteTimeUtc(statsPath));
    }

    internal static Dictionary<string, bool> Parse(byte[] schemaBytes, byte[] statsBytes)
    {
        var schema = RequireObject(new BinaryVdfReader(schemaBytes).Read(), AppId);
        var groups = RequireObject(schema, "stats");
        var cache = RequireObject(new BinaryVdfReader(statsBytes).Read(), "cache");
        var states = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        foreach (var (groupId, groupValue) in groups)
        {
            if (groupValue is not Dictionary<string, object> group) continue;
            if (!group.TryGetValue("bits", out var bitsValue) || bitsValue is not Dictionary<string, object> bits)
                continue;
            var cacheGroup = RequireObject(cache, groupId);
            var data = RequireUInt32(cacheGroup, "data");
            foreach (var (indexText, bitValue) in bits)
            {
                if (bitValue is not Dictionary<string, object> bit ||
                    !bit.TryGetValue("name", out var nameValue) || nameValue is not string name ||
                    !int.TryParse(indexText, NumberStyles.None, CultureInfo.InvariantCulture, out var index) ||
                    index is < 0 or > 31)
                    throw new InvalidDataException("Steam achievement schema has an invalid bit entry.");
                if (!states.TryAdd(name, (data & (1u << index)) != 0))
                    throw new InvalidDataException($"Steam achievement schema repeats {name}.");
            }
        }

        if (states.Count == 0) throw new InvalidDataException("Steam cache has no achievement entries.");
        return states;
    }

    private static byte[] ReadFile(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (stream.Length is <= 0 or > 2_000_000)
            throw new InvalidDataException($"Unexpected Steam cache file size: {path}.");
        var bytes = new byte[stream.Length];
        var read = 0;
        while (read < bytes.Length)
        {
            var count = stream.Read(bytes, read, bytes.Length - read);
            if (count == 0) throw new EndOfStreamException($"Steam cache file was truncated: {path}.");
            read += count;
        }
        return bytes;
    }

    private static Dictionary<string, object> RequireObject(Dictionary<string, object> parent, string key) =>
        parent.TryGetValue(key, out var value) && value is Dictionary<string, object> node
            ? node
            : throw new InvalidDataException($"Steam cache is missing object {key}.");

    private static uint RequireUInt32(Dictionary<string, object> parent, string key) =>
        parent.TryGetValue(key, out var value) && value is uint number
            ? number
            : throw new InvalidDataException($"Steam cache is missing number {key}.");

    private sealed class BinaryVdfReader(byte[] data)
    {
        private static readonly UTF8Encoding Utf8 = new(false, true);
        private int _offset;

        internal Dictionary<string, object> Read()
        {
            var root = ReadObject(0, true);
            if (_offset != data.Length) throw new InvalidDataException("Steam cache has trailing bytes.");
            return root;
        }

        private Dictionary<string, object> ReadObject(int depth, bool root = false)
        {
            if (depth > 24) throw new InvalidDataException("Steam cache nesting is too deep.");
            var values = new Dictionary<string, object>(StringComparer.Ordinal);
            while (_offset < data.Length)
            {
                var kind = data[_offset++];
                if (kind == 8)
                {
                    return values;
                }

                var key = ReadString();
                object value = kind switch
                {
                    0 => ReadObject(depth + 1),
                    1 or 5 => ReadString(),
                    2 or 3 or 4 or 6 or 10 => ReadUInt32(),
                    7 or 9 => ReadUInt64(),
                    _ => throw new InvalidDataException($"Steam cache has unsupported value type {kind}.")
                };
                if (!values.TryAdd(key, value))
                    throw new InvalidDataException($"Steam cache repeats key {key}.");
            }
            if (!root) throw new InvalidDataException("Steam cache ends inside an object.");
            return values;
        }

        private string ReadString()
        {
            var end = Array.IndexOf(data, (byte)0, _offset);
            if (end < 0) throw new InvalidDataException("Steam cache has an unterminated string.");
            string value;
            try { value = Utf8.GetString(data, _offset, end - _offset); }
            catch (DecoderFallbackException error)
            {
                throw new InvalidDataException("Steam cache has invalid UTF-8 text.", error);
            }
            _offset = end + 1;
            return value;
        }

        private uint ReadUInt32()
        {
            if (data.Length - _offset < 4) throw new InvalidDataException("Steam cache has a truncated number.");
            var value = BitConverter.ToUInt32(data, _offset);
            _offset += 4;
            return value;
        }

        private ulong ReadUInt64()
        {
            if (data.Length - _offset < 8) throw new InvalidDataException("Steam cache has a truncated number.");
            var value = BitConverter.ToUInt64(data, _offset);
            _offset += 8;
            return value;
        }
    }
}
