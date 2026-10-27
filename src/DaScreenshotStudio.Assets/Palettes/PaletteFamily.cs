using DaScreenshotStudio.Assets.Archives;

namespace DaScreenshotStudio.Assets.Palettes;

public sealed class PaletteFamily
{
    private readonly Palette?[] _palettes;
    private readonly PaletteCycleTable?[] _cycles;
    private readonly PaletteRangeTable _ranges;

    private PaletteFamily(string prefix, Palette?[] palettes, PaletteCycleTable?[] cycles, PaletteRangeTable ranges)
    {
        Prefix = prefix;
        _palettes = palettes;
        _cycles = cycles;
        _ranges = ranges;
    }

    public string Prefix { get; }

    public Palette? GetPalette(int number) =>
        (uint)number < (uint)_palettes.Length ? _palettes[number] : null;

    public PaletteCycleTable GetCycles(int number) =>
        (uint)number < (uint)_cycles.Length ? _cycles[number] ?? PaletteCycleTable.Empty : PaletteCycleTable.Empty;

    public int ResolveNumber(int assetIndex)
    {
        var number = _ranges.GetPaletteNumber(assetIndex);
        if (number != PaletteRangeTable.Unmapped && GetPalette(number) is not null)
        {
            return number;
        }

        return GetPalette(0) is null ? PaletteRangeTable.Unmapped : 0;
    }

    public Palette? ForAsset(int assetIndex) => GetPalette(ResolveNumber(assetIndex));

    public PaletteCycleTable CyclesForAsset(int assetIndex) => GetCycles(ResolveNumber(assetIndex));

    public static PaletteFamily Load(IAssetArchive archive, string prefix)
    {
        ArgumentNullException.ThrowIfNull(archive);
        ArgumentException.ThrowIfNullOrEmpty(prefix);

        var palettes = new Dictionary<int, Palette>();
        var cycles = new Dictionary<int, PaletteCycleTable>();
        var ranges = PaletteRangeTable.Empty;

        var entries = archive.Entries;
        for (var i = 0; i < entries.Count; i++)
        {
            var name = entries[i].Name.AsSpan();
            if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var dot = name.LastIndexOf('.');
            if (dot < 0)
            {
                continue;
            }

            var extension = name[dot..];
            var stem = name[prefix.Length..dot];

            if (extension.Equals(".pal", StringComparison.OrdinalIgnoreCase))
            {
                if (TryParseNumber(stem, out var number) && archive.TryRead(name, out var data))
                {
                    palettes[number] = Palette.Parse(data);
                }
            }
            else if (extension.Equals(".tbl", StringComparison.OrdinalIgnoreCase) && archive.TryRead(name, out var data))
            {
                if (TryParseNumber(stem, out var number))
                {
                    cycles[number] = PaletteCycleTable.Parse(data);
                }
                else if (stem.Equals("pal", StringComparison.OrdinalIgnoreCase))
                {
                    ranges = PaletteRangeTable.Parse(data);
                }
            }
        }

        var capacity = 0;
        foreach (var number in palettes.Keys)
        {
            capacity = Math.Max(capacity, number + 1);
        }

        foreach (var number in cycles.Keys)
        {
            capacity = Math.Max(capacity, number + 1);
        }

        var palettesByNumber = new Palette?[capacity];
        foreach (var pair in palettes)
        {
            palettesByNumber[pair.Key] = pair.Value;
        }

        var cyclesByNumber = new PaletteCycleTable?[capacity];
        foreach (var pair in cycles)
        {
            cyclesByNumber[pair.Key] = pair.Value;
        }

        return new PaletteFamily(prefix, palettesByNumber, cyclesByNumber, ranges);
    }

    private static bool TryParseNumber(ReadOnlySpan<char> stem, out int number) =>
        AssetNumbers.TryParseDigits(stem, out number);
}
