using System.Text;
using HutchVRisingTrainer;

static void Name(BinaryWriter writer, string name)
{
    writer.Write(Encoding.UTF8.GetBytes(name));
    writer.Write((byte)0);
}

static void Object(BinaryWriter writer, string name, Action body)
{
    writer.Write((byte)0);
    Name(writer, name);
    body();
    writer.Write((byte)8);
}

static void Text(BinaryWriter writer, string name, string value)
{
    writer.Write((byte)1);
    Name(writer, name);
    Name(writer, value);
}

static void Number(BinaryWriter writer, string name, uint value)
{
    writer.Write((byte)2);
    Name(writer, name);
    writer.Write(value);
}

static byte[] Build(Action<BinaryWriter> body)
{
    using var stream = new MemoryStream();
    using var writer = new BinaryWriter(stream);
    body(writer);
    return stream.ToArray();
}

var schema = Build(writer => Object(writer, "1604030", () =>
    Object(writer, "stats", () =>
    {
        Object(writer, "1", () => Object(writer, "bits", () =>
        {
            Object(writer, "0", () => Text(writer, "name", "FirstAchievement"));
            Object(writer, "3", () => Text(writer, "name", "FourthAchievement"));
        }));
        Object(writer, "2", () => Object(writer, "bits", () =>
            Object(writer, "1", () => Text(writer, "name", "OtherGroupAchievement"))));
    })));
var cache = Build(writer => Object(writer, "cache", () =>
{
    Object(writer, "1", () => Number(writer, "data", 1u << 3));
    Object(writer, "2", () => Number(writer, "data", 1u << 1));
}));

var states = SteamAchievementCache.Parse(schema, cache);
if (states.Count != 3 || states["FirstAchievement"] ||
    !states["FourthAchievement"] || !states["OtherGroupAchievement"])
    throw new Exception("Steam achievement bits were mapped incorrectly.");

try
{
    SteamAchievementCache.Parse(schema, cache[..^2]);
    throw new Exception("A truncated Steam cache was accepted.");
}
catch (InvalidDataException) { }

if (SteamAchievementCache.ResolveSteamId("", 12345) != "76561197960278073" ||
    SteamAchievementCache.ResolveSteamId("  ", -1) != "76561202255233023" ||
    SteamAchievementCache.ResolveSteamId("", 0) is not null ||
    SteamAchievementCache.ResolveSteamId("", null) is not null ||
    SteamAchievementCache.ResolveSteamId(" 76561197960278073 ", 67890) != "76561197960278073" ||
    SteamAchievementCache.ResolveSteamId("invalid", 12345) != "invalid")
    throw new Exception("Steam account detection or explicit profile selection was incorrect.");

Console.WriteLine("Steam cache parser and account selection tests passed.");

if (args.Length == 1)
{
    var steamId = SteamAchievementCache.ResolveCurrentSteamId(args[0] == "--current" ? "" : args[0])
        ?? throw new Exception("The signed-in Steam account could not be detected.");
    var live = SteamAchievementCache.ReadForCurrentUser(steamId)
        ?? throw new Exception("The signed-in Steam user's achievement cache was unavailable.");
    Console.WriteLine($"Local Steam cache: {live.States.Values.Count(earned => earned)}/{live.States.Count} earned.");
}
