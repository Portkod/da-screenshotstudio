using DaScreenshotStudio.Assets.Tables;

namespace DaScreenshotStudio.Assets.Palettes;

public readonly struct PaletteCycleBand : IEquatable<PaletteCycleBand>
{
    internal PaletteCycleBand(int start, int end, int delay)
    {
        Start = start;
        End = end;
        Delay = delay;
    }

    public int Start { get; }

    public int End { get; }

    public int Delay { get; }

    public bool Equals(PaletteCycleBand other) => Start == other.Start && End == other.End && Delay == other.Delay;

    public override bool Equals(object? obj) => obj is PaletteCycleBand other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Start, End, Delay);

    public static bool operator ==(PaletteCycleBand left, PaletteCycleBand right) => left.Equals(right);

    public static bool operator !=(PaletteCycleBand left, PaletteCycleBand right) => !left.Equals(right);
}

public sealed class PaletteCycleTable
{
    public const int MaximumBands = 10;

    private readonly PaletteCycleBand[] _bands;

    private PaletteCycleTable(PaletteCycleBand[] bands) => _bands = bands;

    public ReadOnlySpan<PaletteCycleBand> Bands => _bands;

    public static PaletteCycleTable Empty { get; } = new([]);

    public static PaletteCycleTable Parse(ReadOnlySpan<byte> data)
    {
        List<PaletteCycleBand>? bands = null;
        var reader = new TableReader(data);
        Span<int> parts = stackalloc int[3];

        while (reader.TryReadLine(out var line))
        {
            int start;
            int end;
            int delay;

            switch (line.ReadInts(parts))
            {
                case 2:
                    start = parts[0];
                    end = start;
                    delay = parts[1];
                    break;
                case 3:
                    start = parts[0];
                    end = parts[1];
                    delay = parts[2];
                    break;
                default:
                    continue;
            }

            if (start < 0 || end < start || end >= Palette.ColorCount || delay <= 0)
            {
                continue;
            }

            if (bands is { Count: MaximumBands })
            {
                break;
            }

            bands ??= [];
            bands.Add(new PaletteCycleBand(start, end, delay));
        }

        return bands is null ? Empty : new PaletteCycleTable([.. bands]);
    }
}
