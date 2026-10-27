using DaScreenshotStudio.Assets.Archives;
using DaScreenshotStudio.Assets.Imaging;
using DaScreenshotStudio.Assets.Maps;
using DaScreenshotStudio.Engine.Rendering;

namespace DaScreenshotStudio.Engine.World;

public sealed class StaticTileCache
{
    private readonly IAssetArchive _archive;
    private readonly MapArt _art;
    private readonly PackedPaletteCache _palettes;
    private readonly bool _alternate;
    private readonly List<Cycled> _cycling = [];
    private Entry[] _entries;

    public StaticTileCache(IAssetArchive archive, MapArt art, PackedPaletteCache palettes, bool alternate, int capacity)
    {
        ArgumentNullException.ThrowIfNull(archive);
        ArgumentNullException.ThrowIfNull(art);
        ArgumentNullException.ThrowIfNull(palettes);

        _archive = archive;
        _art = art;
        _palettes = palettes;
        _alternate = alternate;
        _entries = new Entry[Math.Max(1, capacity)];
    }

    public int MaximumHeight { get; private set; }

    public int DecodedCount { get; private set; }

    public bool TryGet(int staticTileId, out Pixmap art, out BlendMode blend)
    {
        if (!MapTileIds.IsDrawnStaticTile(staticTileId))
        {
            art = default;
            blend = BlendMode.Transparent;
            return false;
        }

        if ((uint)staticTileId >= (uint)_entries.Length)
        {
            Grow(staticTileId + 1);
        }

        ref var entry = ref _entries[staticTileId];
        if (!entry.Decoded)
        {
            Decode(staticTileId, ref entry);
        }

        art = entry.Art;
        blend = entry.Blend;
        return !art.IsEmpty;
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
                Prewarm(cell.LeftStaticTile);
                Prewarm(cell.RightStaticTile);
            }
        }
    }

    private void Prewarm(int staticTileId)
    {
        TryGet(staticTileId, out _, out _);

        if (StaticTileAnimations.TryFind(staticTileId, out var group, out _))
        {
            var frames = StaticTileAnimations.FrameCount(group);
            for (var frame = 0; frame < frames; frame++)
            {
                PrewarmTable(StaticTileAnimations.Frame(group, frame));
            }
        }

        PrewarmTable(staticTileId);
    }

    private void PrewarmTable(int staticTileId)
    {
        TryGet(staticTileId, out _, out _);

        if (!_art.StaticAnimations.IsAnimated(staticTileId))
        {
            return;
        }

        for (var tick = 1; tick <= AnimationPrewarmTicks; tick++)
        {
            TryGet(_art.StaticAnimations.ResolveFrame(staticTileId, tick), out _, out _);
        }
    }

    private const int AnimationPrewarmTicks = 64;

    public void RepackCycled()
    {
        for (var i = 0; i < _cycling.Count; i++)
        {
            var cycled = _cycling[i];
            Pack(cycled.Palette, cycled.Source.Pixels, cycled.Pixels);
        }
    }

    private void Grow(int capacity)
    {
        var grown = new Entry[Math.Max(capacity, _entries.Length * 2)];
        _entries.CopyTo(grown.AsSpan());
        _entries = grown;
    }

    private void Decode(int staticTileId, ref Entry entry)
    {
        entry.Decoded = true;
        entry.Blend = BlendFor(staticTileId);

        if (!_art.TryReadStaticArt(_archive, staticTileId, _alternate, out var data))
        {
            return;
        }

        HpfImage image;
        try
        {
            image = HpfImage.Parse(data);
        }
        catch (InvalidDataException)
        {
            return;
        }

        if (image.Height <= 0)
        {
            return;
        }

        var palette = _palettes.Get(
            _art.GetStaticPalette(staticTileId, _alternate),
            _art.GetStaticCycles(staticTileId, _alternate));

        var indexes = image.Pixels;
        var pixels = new ushort[indexes.Length];
        Pack(palette, indexes, pixels);

        entry.Art = new Pixmap(pixels, HpfImage.Width, image.Height);
        MaximumHeight = Math.Max(MaximumHeight, image.Height);
        DecodedCount++;

        if (palette.IsCycling)
        {
            _cycling.Add(new Cycled(image, pixels, palette));
        }
    }

    private static void Pack(PackedPalette palette, ReadOnlySpan<byte> indexes, Span<ushort> pixels)
    {
        for (var i = 0; i < pixels.Length; i++)
        {
            pixels[i] = palette[indexes[i]];
        }
    }

    private BlendMode BlendFor(int staticTileId) =>
        _art.Collision.IsScreenBlend(staticTileId) ? BlendMode.Screen
        : _art.Collision.IsAlternateBlend(staticTileId) ? BlendMode.Average
        : BlendMode.Transparent;

    private struct Entry
    {
        public Pixmap Art;
        public BlendMode Blend;
        public bool Decoded;
    }

    private readonly struct Cycled(HpfImage source, ushort[] pixels, PackedPalette palette)
    {
        public HpfImage Source { get; } = source;

        public ushort[] Pixels { get; } = pixels;

        public PackedPalette Palette { get; } = palette;
    }
}
