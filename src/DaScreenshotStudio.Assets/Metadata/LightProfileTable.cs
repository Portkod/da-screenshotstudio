using DaScreenshotStudio.Assets.Tables;

namespace DaScreenshotStudio.Assets.Metadata;

public readonly struct LightProfileEntry : IEquatable<LightProfileEntry>
{
    public const int HeaIntensityThreshold = 0x20;

    internal LightProfileEntry(int startTime, int endTime, int intensity, byte red, byte green, byte blue, bool heaDisabled)
    {
        StartTime = startTime;
        EndTime = endTime;
        Intensity = intensity;
        Red = red;
        Green = green;
        Blue = blue;
        HeaDisabled = heaDisabled;
    }

    public int StartTime { get; }

    public int EndTime { get; }

    public int Intensity { get; }

    public byte Red { get; }

    public byte Green { get; }

    public byte Blue { get; }

    public bool HeaDisabled { get; }

    public bool UsesHea => !HeaDisabled && Intensity < HeaIntensityThreshold;

    public bool Covers(int time) => time >= StartTime && time <= EndTime;

    public bool Equals(LightProfileEntry other) =>
        StartTime == other.StartTime && EndTime == other.EndTime && Intensity == other.Intensity
        && Red == other.Red && Green == other.Green && Blue == other.Blue && HeaDisabled == other.HeaDisabled;

    public override bool Equals(object? obj) => obj is LightProfileEntry other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(StartTime, EndTime, Intensity, Red, Green, Blue, HeaDisabled);

    public static bool operator ==(LightProfileEntry left, LightProfileEntry right) => left.Equals(right);

    public static bool operator !=(LightProfileEntry left, LightProfileEntry right) => !left.Equals(right);
}

public sealed class LightProfileTable
{
    public const int FallbackIntensity = LightProfileEntry.HeaIntensityThreshold;

    public const int MaxTime = 24;

    private readonly Dictionary<int, string> _mapProfiles;
    private readonly Dictionary<string, LightProfileEntry[]> _profiles;

    private LightProfileTable(Dictionary<int, string> mapProfiles, Dictionary<string, LightProfileEntry[]> profiles)
    {
        _mapProfiles = mapProfiles;
        _profiles = profiles;
    }

    public static LightProfileTable Empty { get; } = new([], []);

    public static int ToProfileTime(int timeStep) => Math.Min(timeStep * 2, MaxTime);

    public static LightProfileTable Parse(Metafile table)
    {
        ArgumentNullException.ThrowIfNull(table);

        var mapProfiles = new Dictionary<int, string>();
        var collected = new Dictionary<string, List<LightProfileEntry>>(StringComparer.Ordinal);

        for (var group = 0; group < table.GroupCount; group++)
        {
            var name = table.GetGroupName(group);
            if (name.Length == 0)
            {
                continue;
            }

            if (char.IsAsciiDigit(name[0]))
            {
                if (TryReadMapId(name, out var mapId) && table.GetValueCount(group) > 0)
                {
                    mapProfiles[mapId] = System.Text.Encoding.ASCII.GetString(table.GetValue(group, 0));
                }

                continue;
            }

            var underscore = name.IndexOf('_', StringComparison.Ordinal);
            var profile = underscore < 0 ? name : name[..underscore];

            if (!TryReadEntry(table, group, out var entry))
            {
                continue;
            }

            if (!collected.TryGetValue(profile, out var entries))
            {
                collected[profile] = entries = [];
            }

            entries.Add(entry);
        }

        var profiles = new Dictionary<string, LightProfileEntry[]>(collected.Count, StringComparer.Ordinal);
        foreach (var pair in collected)
        {
            profiles[pair.Key] = [.. pair.Value];
        }

        return new LightProfileTable(mapProfiles, profiles);
    }

    public string? GetProfileName(int mapId) => _mapProfiles.GetValueOrDefault(mapId);

    public ReadOnlySpan<LightProfileEntry> GetProfile(string profileName) =>
        _profiles.TryGetValue(profileName, out var entries) ? entries : default;

    public bool TryResolve(int mapId, int profileTime, out LightProfileEntry entry)
    {
        entry = default;

        var profileName = GetProfileName(mapId);
        if (profileName is null)
        {
            return false;
        }

        var entries = GetProfile(profileName);
        for (var i = 0; i < entries.Length; i++)
        {
            if (entries[i].Covers(profileTime))
            {
                entry = entries[i];
                return true;
            }
        }

        return false;
    }

    private static bool TryReadEntry(Metafile table, int group, out LightProfileEntry entry)
    {
        entry = default;

        var count = table.GetValueCount(group);
        if (count < 6)
        {
            return false;
        }

        Span<int> fields = stackalloc int[6];
        for (var i = 0; i < 6; i++)
        {
            var line = new TableReader(table.GetValue(group, i));
            if (!line.TryReadLine(out var text) || !text.TryReadInt(out fields[i]))
            {
                return false;
            }
        }

        var heaDisabled = count > 6 && table.GetValue(group, 6) is [(byte)'D'];

        entry = new LightProfileEntry(
            fields[0],
            fields[1],
            (byte)fields[2],
            (byte)fields[3],
            (byte)fields[4],
            (byte)fields[5],
            heaDisabled);

        return true;
    }

    private static bool TryReadMapId(string name, out int mapId)
    {
        var digits = 0;
        while (digits < name.Length && char.IsAsciiDigit(name[digits]))
        {
            digits++;
        }

        return int.TryParse(name.AsSpan(0, digits), out mapId);
    }
}
