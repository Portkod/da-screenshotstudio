using DaScreenshotStudio.Assets.Archives;
using DaScreenshotStudio.Assets.Imaging;
using DaScreenshotStudio.Assets.Palettes;

namespace DaScreenshotStudio.Assets.Maps;

public sealed class MapArt
{
    public const int StaticArtNameLength = 12;

    private MapArt(
        GroundTileBank groundBank,
        GroundTileBank? alternateGroundBank,
        PaletteFamily groundPalettes,
        PaletteFamily alternateGroundPalettes,
        PaletteFamily staticPalettes,
        PaletteFamily alternateStaticPalettes,
        TileAnimationTable groundAnimations,
        TileAnimationTable staticAnimations,
        SotpTable collision,
        GroundAttributeTable groundAttributes)
    {
        GroundBank = groundBank;
        AlternateGroundBank = alternateGroundBank;
        GroundPalettes = groundPalettes;
        AlternateGroundPalettes = alternateGroundPalettes;
        StaticPalettes = staticPalettes;
        AlternateStaticPalettes = alternateStaticPalettes;
        GroundAnimations = groundAnimations;
        StaticAnimations = staticAnimations;
        Collision = collision;
        GroundAttributes = groundAttributes;
    }

    public GroundTileBank GroundBank { get; }

    public GroundTileBank? AlternateGroundBank { get; }

    public PaletteFamily GroundPalettes { get; }

    public PaletteFamily AlternateGroundPalettes { get; }

    public PaletteFamily StaticPalettes { get; }

    public PaletteFamily AlternateStaticPalettes { get; }

    public TileAnimationTable GroundAnimations { get; }

    public TileAnimationTable StaticAnimations { get; }

    public SotpTable Collision { get; }

    public GroundAttributeTable GroundAttributes { get; }

    public static MapArt Load(IAssetArchive archive)
    {
        ArgumentNullException.ThrowIfNull(archive);

        if (!archive.TryReadMemory("tilea.bmp", out var baseBank))
        {
            throw new FileNotFoundException($"'{archive.Name}' has no tilea.bmp, so it holds no ground art.");
        }

        return new MapArt(
            GroundTileBank.Wrap(baseBank),
            archive.TryReadMemory("tileas.bmp", out var alternateBank) ? GroundTileBank.Wrap(alternateBank) : null,
            PaletteFamily.Load(archive, "mpt"),
            PaletteFamily.Load(archive, "mps"),
            PaletteFamily.Load(archive, "stc"),
            PaletteFamily.Load(archive, "sts"),
            ParseTable(archive, "gndani.tbl"),
            ParseTable(archive, "stcani.tbl"),
            archive.TryRead("sotp.dat", out var sotp) ? SotpTable.Parse(sotp) : SotpTable.Empty,
            archive.TryRead("gndattr.tbl", out var ground)
                ? GroundAttributeTable.Parse(ground)
                : GroundAttributeTable.Empty);
    }

    public bool TryGetGroundRecord(int groundTileId, bool alternate, out ReadOnlySpan<byte> record)
    {
        var index = MapTileIds.GroundRecord(groundTileId);

        if (alternate && AlternateGroundBank is { } alternateBank && alternateBank.ContainsRecord(index))
        {
            record = alternateBank.GetRecord(index);
            return true;
        }

        if (GroundBank.ContainsRecord(index))
        {
            record = GroundBank.GetRecord(index);
            return true;
        }

        record = default;
        return false;
    }

    public Palette? GetGroundPalette(int groundTileId, bool alternate)
    {
        var index = MapTileIds.GroundPaletteIndex(groundTileId);
        return alternate
            ? AlternateGroundPalettes.ForAsset(index) ?? GroundPalettes.ForAsset(index)
            : GroundPalettes.ForAsset(index);
    }

    public Palette? GetStaticPalette(int staticTileId, bool alternate)
    {
        var index = MapTileIds.StaticPaletteIndex(staticTileId);
        return alternate
            ? AlternateStaticPalettes.ForAsset(index) ?? StaticPalettes.ForAsset(index)
            : StaticPalettes.ForAsset(index);
    }

    public PaletteCycleTable GetGroundCycles(int groundTileId, bool alternate)
    {
        var index = MapTileIds.GroundPaletteIndex(groundTileId);
        var family = alternate && AlternateGroundPalettes.ForAsset(index) is not null
            ? AlternateGroundPalettes
            : GroundPalettes;

        return family.CyclesForAsset(index);
    }

    public PaletteCycleTable GetStaticCycles(int staticTileId, bool alternate)
    {
        var index = MapTileIds.StaticPaletteIndex(staticTileId);
        var family = alternate && AlternateStaticPalettes.ForAsset(index) is not null
            ? AlternateStaticPalettes
            : StaticPalettes;

        return family.CyclesForAsset(index);
    }

    public bool TryReadStaticArt(IAssetArchive archive, int staticTileId, bool alternate, out ReadOnlySpan<byte> data)
    {
        ArgumentNullException.ThrowIfNull(archive);

        Span<char> name = stackalloc char[StaticArtNameLength];

        if (alternate && FormatStaticArtName(staticTileId, alternate: true, name) && archive.TryRead(name, out data))
        {
            return true;
        }

        if (FormatStaticArtName(staticTileId, alternate: false, name))
        {
            return archive.TryRead(name, out data);
        }

        data = default;
        return false;
    }

    public static bool FormatStaticArtName(int staticTileId, bool alternate, scoped Span<char> destination)
    {
        var number = MapTileIds.StaticArtNumber(staticTileId);
        if (destination.Length < StaticArtNameLength || (uint)number > 99999)
        {
            return false;
        }

        destination[0] = 's';
        destination[1] = 't';
        destination[2] = alternate ? 's' : 'c';

        for (var i = 7; i >= 3; i--)
        {
            destination[i] = (char)('0' + (number % 10));
            number /= 10;
        }

        ".hpf".CopyTo(destination[8..]);
        return true;
    }

    private static TileAnimationTable ParseTable(IAssetArchive archive, scoped ReadOnlySpan<char> name) =>
        archive.TryRead(name, out var data) ? TileAnimationTable.Parse(data) : TileAnimationTable.Empty;
}
