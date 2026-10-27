using System.Runtime.CompilerServices;
using DaScreenshotStudio.Assets.Palettes;

namespace DaScreenshotStudio.Engine.Rendering;

public sealed class PackedPalette
{
    private readonly ushort[] _colors = new ushort[PalettePacker.ColorCount];

    private ushort[]? _unrotated;
    private Band[]? _bands;

    private PackedPalette()
    {
    }

    public ReadOnlySpan<ushort> Colors => _colors;

    public bool IsCycling => _bands is not null;

    public ushort this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _colors[index];
    }

    public bool ApplyCycles(long ticks)
    {
        var bands = _bands;
        if (bands is null)
        {
            return false;
        }

        var moved = false;
        for (var i = 0; i < bands.Length; i++)
        {
            ref var band = ref bands[i];
            var shift = (int)(ticks / band.Delay % band.Length);
            if (shift == band.Shift)
            {
                continue;
            }

            band.Shift = shift;
            moved = true;

            for (var offset = 0; offset < band.Length; offset++)
            {
                var source = offset - shift;
                if (source < 0)
                {
                    source += band.Length;
                }

                _colors[band.Start + offset] = _unrotated![band.Start + source];
            }
        }

        return moved;
    }

    public static PackedPalette From(Palette palette, in PixelPacking packing)
    {
        var packed = new PackedPalette();
        PalettePacker.Pack(palette, in packing, packed._colors);
        return packed;
    }

    public static PackedPalette From(Palette palette, PaletteCycleTable cycles, in PixelPacking packing)
    {
        ArgumentNullException.ThrowIfNull(cycles);

        var packed = From(palette, in packing);
        var bands = BuildBands(cycles.Bands);
        if (bands is null)
        {
            return packed;
        }

        packed._bands = bands;
        packed._unrotated = (ushort[])packed._colors.Clone();
        return packed;
    }

    public static PackedPalette FromGui(Palette palette, in PixelPacking packing)
    {
        var packed = new PackedPalette();
        PalettePacker.PackGui(palette, in packing, packed._colors);
        return packed;
    }

    public static PackedPalette Grayscale(in PixelPacking packing) => From(Palette.Grayscale(), in packing);

    private static Band[]? BuildBands(ReadOnlySpan<PaletteCycleBand> source)
    {
        Span<Band> usable = stackalloc Band[PaletteCycleTable.MaximumBands];
        var count = 0;

        for (var i = 0; i < source.Length && count < usable.Length; i++)
        {
            var start = Math.Max(source[i].Start, 1);
            var end = Math.Min(source[i].End, PalettePacker.ColorCount - 1);
            if (start >= end || source[i].Delay <= 0)
            {
                continue;
            }

            usable[count++] = new Band(start, end - start + 1, source[i].Delay);
        }

        return count == 0 ? null : usable[..count].ToArray();
    }

    private struct Band(int start, int length, int delay)
    {
        public readonly int Start = start;

        public readonly int Length = length;

        public readonly int Delay = delay;

        public int Shift;
    }
}

public sealed class PackedPaletteCache
{
    private readonly Dictionary<Palette, PackedPalette> _packed = [];
    private readonly List<PackedPalette> _cycling = [];
    private readonly PixelPacking _packing;

    public PackedPaletteCache(in PixelPacking packing)
    {
        _packing = packing;
        Fallback = PackedPalette.Grayscale(in packing);
    }

    public PackedPalette Fallback { get; }

    public PackedPalette Get(Palette? palette) => Get(palette, PaletteCycleTable.Empty);

    public PackedPalette Get(Palette? palette, PaletteCycleTable cycles)
    {
        if (palette is null)
        {
            return Fallback;
        }

        if (_packed.TryGetValue(palette, out var existing))
        {
            return existing;
        }

        var created = PackedPalette.From(palette, cycles, in _packing);
        _packed[palette] = created;

        if (created.IsCycling)
        {
            _cycling.Add(created);
        }

        return created;
    }

    public bool ApplyCycles(long ticks)
    {
        var moved = false;
        for (var i = 0; i < _cycling.Count; i++)
        {
            moved |= _cycling[i].ApplyCycles(ticks);
        }

        return moved;
    }
}
