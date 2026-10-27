using DaScreenshotStudio.Assets.Imaging;
using DaScreenshotStudio.Assets.Maps;
using DaScreenshotStudio.Engine.Rendering;

namespace DaScreenshotStudio.Engine.World;

public sealed class GroundTileCache
{
    private readonly MapArt _art;
    private readonly PackedPaletteCache _palettes;
    private readonly bool _alternate;
    private readonly List<Cycled> _cycling = [];
    private ushort[]?[] _entries;

    public GroundTileCache(MapArt art, PackedPaletteCache palettes, bool alternate, int capacity)
    {
        ArgumentNullException.ThrowIfNull(art);
        ArgumentNullException.ThrowIfNull(palettes);

        _art = art;
        _palettes = palettes;
        _alternate = alternate;
        _entries = new ushort[Math.Max(1, capacity)][];
    }

    public int DecodedCount { get; private set; }

    public ReadOnlySpan<ushort> Get(int groundTileId)
    {
        if (groundTileId <= MapTileIds.Empty)
        {
            return default;
        }

        if ((uint)groundTileId >= (uint)_entries.Length)
        {
            Grow(groundTileId + 1);
        }

        return _entries[groundTileId] ?? Decode(groundTileId);
    }

    public void Prewarm(MapFile map)
    {
        ArgumentNullException.ThrowIfNull(map);

        for (var y = 0; y < map.Height; y++)
        {
            var row = map.GetRowBytes(y);
            for (var x = 0; x < map.Width; x++)
            {
                var cell = MapFile.ReadCell(row[(x * MapCell.Length)..]);
                Get(cell.GroundTile);

                if (_art.GroundAnimations.IsAnimated(cell.GroundTile))
                {
                    for (var tick = 1; tick <= AnimationPrewarmTicks; tick++)
                    {
                        Get(_art.GroundAnimations.ResolveFrame(cell.GroundTile, tick));
                    }
                }
            }
        }
    }

    private const int AnimationPrewarmTicks = 64;

    public void RepackCycled()
    {
        for (var i = 0; i < _cycling.Count; i++)
        {
            var cycled = _cycling[i];
            Pack(cycled.Bank, cycled.Record, cycled.Palette, cycled.Pixels);
        }
    }

    private void Grow(int capacity)
    {
        var grown = new ushort[]?[Math.Max(capacity, _entries.Length * 2)];
        Array.Copy(_entries, grown, _entries.Length);
        _entries = grown;
    }

    private ReadOnlySpan<ushort> Decode(int groundTileId)
    {
        if (!_art.TryGetGroundRecord(groundTileId, _alternate, out _))
        {
            _entries[groundTileId] = [];
            return default;
        }

        var bank = _art.GroundBank;
        var record = MapTileIds.GroundRecord(groundTileId);

        if (_alternate && _art.AlternateGroundBank is { } alternateBank && alternateBank.ContainsRecord(record))
        {
            bank = alternateBank;
        }

        var palette = _palettes.Get(
            _art.GetGroundPalette(groundTileId, _alternate),
            _art.GetGroundCycles(groundTileId, _alternate));

        var pixels = new ushort[GroundTileBank.VisiblePixelCount];
        Pack(bank, record, palette, pixels);

        if (palette.IsCycling)
        {
            _cycling.Add(new Cycled(bank, record, pixels, palette));
        }

        _entries[groundTileId] = pixels;
        DecodedCount++;
        return pixels;
    }

    private static void Pack(GroundTileBank bank, int record, PackedPalette palette, Span<ushort> pixels)
    {
        Span<byte> indexes = stackalloc byte[GroundTileBank.VisiblePixelCount];
        bank.CopyVisiblePixels(record, indexes);

        for (var i = 0; i < pixels.Length; i++)
        {
            pixels[i] = palette[indexes[i]];
        }
    }

    private readonly struct Cycled(GroundTileBank bank, int record, ushort[] pixels, PackedPalette palette)
    {
        public GroundTileBank Bank { get; } = bank;

        public int Record { get; } = record;

        public ushort[] Pixels { get; } = pixels;

        public PackedPalette Palette { get; } = palette;
    }
}
