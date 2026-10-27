using DaScreenshotStudio.Assets.Archives;
using DaScreenshotStudio.Assets.Gui;
using DaScreenshotStudio.Assets.Imaging;
using DaScreenshotStudio.Assets.Metadata;
using DaScreenshotStudio.Assets.Tables;
using DaScreenshotStudio.Engine.Rendering;
using DaScreenshotStudio.Engine.Text;
using AssetPalette = DaScreenshotStudio.Assets.Palettes.Palette;
using DaScreenshotStudio.Engine.App;

namespace DaScreenshotStudio.Engine.Ui;

public sealed class UiAssets : IDisposable
{
    public const string LayoutArchiveName = "setoa.dat";

    public const string FontArchiveName = "national.dat";

    public const string LegendArchiveName = "Legend.dat";

    public const string MiniGameArchiveName = "cious.dat";

    public static string GuiPaletteEntryName(int number) => $"gui{number:00}.pal";

    public const string MessageEntryName = "msg.tbl";

    public const string NpcIllustrationEntry = "NPCIllust";

    private const int LegendPaletteNumber = -2;

    private readonly DataArchive? _layouts;
    private readonly DataArchive? _fonts;
    private readonly DataArchive? _legend;
    private readonly DataArchive? _miniGames;
    private readonly Dictionary<string, SpfImage?> _images = [];
    private readonly Dictionary<string, ushort[]> _embeddedPalettes = [];
    private readonly Dictionary<string, EpfImage?> _epfImages = [];
    private readonly Dictionary<(string Entry, int Frame), Pixmap> _frames = [];
    private readonly Dictionary<string, Pixmap> _bitmaps = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SpriteSheet?> _sheets = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PackedPalette?> _sheetPalettes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<(string Entry, int Frame), Pixmap> _sheetFrames = [];
    private readonly Dictionary<string, GuiLayout?> _byName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, GuiLayout?>.AlternateLookup<ReadOnlySpan<char>> _extra;
    private readonly Dictionary<(string Entry, int Frame, int Palette), Pixmap> _paletteFrames = [];
    private readonly Dictionary<string, PackedPalette?> _namedPalettes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, PackedPalette?> _guiFamily = [];
    private readonly PackedPalette? _legendColors;

    private readonly ushort[]? _namedLegendColors;
    private readonly byte[] _localNpcIllustrations;
    private readonly PixelPacking _packing;
    private NpcIllustrationTable? _npcIllustrations;
    private ItemInfoTable? _itemInfo;
    private bool _disposed;

    private UiAssets(
        DataArchive? layouts,
        DataArchive? fonts,
        DataArchive? legend,
        DataArchive? miniGames,
        GuiLayoutCatalog catalog,
        GameFont font,
        TextPalette palette,
        AssetPalette? legendPalette,
        MetafileStore metafiles,
        byte[] localNpcIllustrations,
        in PixelPacking packing)
    {
        _localNpcIllustrations = localNpcIllustrations;
        _layouts = layouts;
        _fonts = fonts;
        _legend = legend;
        _miniGames = miniGames;
        _packing = packing;
        _extra = _byName.GetAlternateLookup<ReadOnlySpan<char>>();
        _legendColors = legendPalette is null ? null : PackedPalette.From(legendPalette, in packing);

        if (legendPalette is not null)
        {
            _namedLegendColors = new ushort[PalettePacker.ColorCount];
            PalettePacker.PackNamed(legendPalette, in packing, _namedLegendColors);
        }

        Layouts = catalog;
        Font = font;
        Palette = palette;
        Metafiles = metafiles;

        Messages = fonts is not null && fonts.TryRead(MessageEntryName, out var messages)
            ? MessageTable.Parse(messages, font.CodePage)
            : MessageTable.Empty;

        ItemIcons = IconArt.TryLoadItems(legend, in packing);
        SpellIcons = IconArt.TryLoadAbilities(legend, "spell", GuiPaletteForName("spell001.epf"), in packing);
        SkillIcons = IconArt.TryLoadAbilities(legend, "skill", GuiPaletteForName("skill001.epf"), in packing);
    }

    public GuiLayoutCatalog Layouts { get; }

    public GameFont Font { get; }

    public TextPalette Palette { get; }

    public MetafileStore Metafiles { get; }

    public MessageTable Messages { get; }

    public ItemInfoTable ItemInfo => _itemInfo ??= ItemInfoTable.Load(Metafiles);

    public NpcIllustrationTable NpcIllustrations =>
        _npcIllustrations ??= NpcIllustrationTable.Build(_localNpcIllustrations, Metafiles.Find(NpcIllustrationEntry));

    public IconArt? ItemIcons { get; }

    public IconArt? SpellIcons { get; }

    public IconArt? SkillIcons { get; }

    public int DecodedFrames { get; private set; }

    public static UiAssets? TryLoad(
        ClientDirectory clientDirectory,
        in PixelPacking packing,
        MetafileStore? metafiles = null,
        TextLanguage language = TextLanguage.English,
        bool singleByteText = false)
    {
        ArgumentNullException.ThrowIfNull(clientDirectory);

        DataArchive? layouts = null;
        DataArchive? fonts = null;
        DataArchive? legend = null;
        DataArchive? miniGames = null;

        try
        {
            layouts = clientDirectory.TryOpenArchive(LayoutArchiveName);
            fonts = clientDirectory.TryOpenArchive(FontArchiveName);
            legend = clientDirectory.TryOpenArchive(LegendArchiveName);

            miniGames = clientDirectory.TryOpenArchive(MiniGameArchiveName);

            if (layouts is null || fonts is null)
            {
                layouts?.Dispose();
                fonts?.Dispose();
                legend?.Dispose();
                miniGames?.Dispose();
                return null;
            }

            if (GameFont.TryLoad(fonts, language, singleByteText) is not { } face)
            {
                layouts.Dispose();
                fonts.Dispose();
                legend?.Dispose();
                miniGames?.Dispose();
                return null;
            }

            var legendPalette = legend is not null && legend.TryRead(TextPalette.EntryName, out var paletteData)
                ? AssetPalette.Parse(paletteData)
                : null;

            var palette = legendPalette is null
                ? TextPalette.Fallback(in packing)
                : new TextPalette(legendPalette, in packing);

            var store = metafiles
                ?? MetafileStore.Load(clientDirectory.Subdirectory(MetafileStore.DirectoryName).Path);

            return new UiAssets(
                layouts,
                fonts,
                legend,
                miniGames,
                GuiLayoutCatalog.Load(layouts),
                face,
                palette,
                legendPalette,
                store,
                TryReadLocalNpcIllustrations(clientDirectory),
                in packing);
        }
        catch
        {
            layouts?.Dispose();
            fonts?.Dispose();
            legend?.Dispose();
            miniGames?.Dispose();
            throw;
        }
    }

    public PixelPacking Packing => _packing;

    public GuiLayout? Layout(scoped ReadOnlySpan<char> name)
    {
        if (Layouts.Find(name) is { } cataloged)
        {
            return cataloged;
        }

        if (_extra.TryGetValue(name, out var extra))
        {
            return extra;
        }

        var parsed = (_layouts is null ? null : GuiLayout.Load(_layouts, name))
            ?? (_miniGames is null ? null : GuiLayout.Load(_miniGames, name));

        _byName[new string(name)] = parsed;
        return parsed;
    }

    public Pixmap Frame(string entryName, int frame)
    {
        ArgumentException.ThrowIfNullOrEmpty(entryName);

        var key = (entryName, frame);
        if (_frames.TryGetValue(key, out var existing))
        {
            return existing;
        }

        var decoded = Decode(entryName, frame);
        _frames[key] = decoded;
        return decoded;
    }

    public IconFrame PlacedFrame(string entryName, int frame)
    {
        ArgumentException.ThrowIfNullOrEmpty(entryName);

        var pixels = Frame(entryName, frame);
        if (pixels.IsEmpty || Image(entryName) is not { } image || (uint)frame >= (uint)image.FrameCount)
        {
            return default;
        }

        var record = image[frame];
        return new IconFrame(in pixels, record.Left, record.Top);
    }

    public Pixmap PaletteFrame(string entryName, int frame)
    {
        ArgumentException.ThrowIfNullOrEmpty(entryName);
        return PaletteFrame(entryName, frame, GuiPaletteNames.NumberFor(entryName));
    }

    public Pixmap LayoutFrame(string entryName, int frame)
    {
        ArgumentException.ThrowIfNullOrEmpty(entryName);

        return entryName.EndsWith(".epf", StringComparison.OrdinalIgnoreCase)
            ? PaletteFrame(entryName, frame)
            : Frame(entryName, frame);
    }

    public Pixmap LayoutFrame(GuiLayoutControl control, int imageIndex)
    {
        ArgumentNullException.ThrowIfNull(control);

        return (uint)imageIndex >= (uint)control.Images.Count
            ? default
            : LayoutFrame(control.Images[imageIndex].EntryName, control.Images[imageIndex].Frame);
    }

    public Pixmap LegendFrame(string entryName, int frame)
    {
        ArgumentException.ThrowIfNullOrEmpty(entryName);
        return PaletteFrame(entryName, frame, LegendPaletteNumber);
    }

    private Pixmap PaletteFrame(string entryName, int frame, int paletteNumber)
    {
        var key = (entryName, frame, paletteNumber);
        if (_paletteFrames.TryGetValue(key, out var existing))
        {
            return existing;
        }

        var decoded = DecodePalettized(entryName, frame, paletteNumber);
        _paletteFrames[key] = decoded;
        return decoded;
    }

    private PackedPalette? GuiPaletteForName(string entryName) =>
        GuiPalette(GuiPaletteNames.NumberFor(entryName));

    public ReadOnlySpan<ushort> LegendColors => _namedLegendColors;

    private PackedPalette? GuiPalette(int number)
    {
        if (number == LegendPaletteNumber)
        {
            return _legendColors;
        }

        if (number < 0)
        {
            return null;
        }

        if (_guiFamily.TryGetValue(number, out var existing))
        {
            return existing;
        }

        PackedPalette? packed = null;
        if (_layouts is not null && _layouts.TryRead(GuiPaletteEntryName(number), out var data))
        {
            packed = PackedPalette.FromGui(AssetPalette.Parse(data), in _packing);
        }

        _guiFamily[number] = packed;
        return packed;
    }

    public int PaletteFrameCount(string entryName)
    {
        ArgumentException.ThrowIfNullOrEmpty(entryName);
        return Epf(entryName)?.FrameCount ?? 0;
    }

    public IconFrame PlacedPaletteFrame(string entryName, int frame)
    {
        ArgumentException.ThrowIfNullOrEmpty(entryName);

        var pixels = PaletteFrame(entryName, frame);
        if (pixels.IsEmpty || Epf(entryName) is not { } image || (uint)frame >= (uint)image.FrameCount)
        {
            return default;
        }

        var record = image[frame];
        return new IconFrame(in pixels, record.Left, record.Top);
    }

    public SpfImage? Sprite(string entryName)
    {
        ArgumentException.ThrowIfNullOrEmpty(entryName);
        return Image(entryName);
    }

    private Pixmap DecodeBitmap(string entryName)
    {
        ReadOnlyMemory<byte> data;
        if (!(_miniGames?.TryReadMemory(entryName, out data) ?? false)
            && !(_layouts?.TryReadMemory(entryName, out data) ?? false))
        {
            return default;
        }

        DibImage image;
        try
        {
            image = DibImage.Parse(data);
        }
        catch (InvalidDataException)
        {
            return default;
        }

        if (image.BitsPerPixel != 16)
        {
            return default;
        }

        var pixels = new ushort[image.Width * image.Height];
        for (var y = 0; y < image.Height; y++)
        {
            var row = image.GetRow(y);
            var destination = y * image.Width;
            for (var x = 0; x < image.Width && ((x * 2) + 1) < row.Length; x++)
            {
                var packed = (ushort)(row[x * 2] | (row[(x * 2) + 1] << 8));
                pixels[destination + x] = _packing.Pack(
                    (byte)(((packed >> 10) & 0x1F) << 3),
                    (byte)(((packed >> 5) & 0x1F) << 3),
                    (byte)((packed & 0x1F) << 3));
            }
        }

        DecodedFrames++;
        return new Pixmap(pixels, image.Width, image.Height);
    }

    public SpriteSheet? Sheet(string entryName)
    {
        ArgumentException.ThrowIfNullOrEmpty(entryName);

        if (_sheets.TryGetValue(entryName, out var existing))
        {
            return existing;
        }

        SpriteSheet? cut = null;
        ReadOnlyMemory<byte> data;

        if ((_miniGames?.TryReadMemory(entryName, out data) ?? false)
            || (_layouts?.TryReadMemory(entryName, out data) ?? false))
        {
            try
            {
                cut = SpriteSheet.Cut(PcxImage.Parse(data));
                _sheetPalettes[entryName] = PackedPalette.From(PcxImage.Parse(data).ReadPalette(), in _packing);
            }
            catch (InvalidDataException)
            {
            }
        }

        _sheets[entryName] = cut;
        return cut;
    }

    private Pixmap DecodeSheetFrame(string entryName, int frame)
    {
        if (Sheet(entryName) is not { } sheet
            || (uint)frame >= (uint)sheet.FrameCount
            || !_sheetPalettes.TryGetValue(entryName, out var colors)
            || colors is null)
        {
            return default;
        }

        var cell = sheet[frame];
        var pixels = new ushort[cell.Width * cell.Height];

        for (var y = 0; y < cell.Height; y++)
        {
            var row = sheet.Row(frame, y);
            var destination = y * cell.Width;
            for (var x = 0; x < row.Length; x++)
            {
                if (!SpriteSheet.IsReserved(row[x]))
                {
                    pixels[destination + x] = colors[row[x]];
                }
            }
        }

        DecodedFrames++;
        return new Pixmap(pixels, cell.Width, cell.Height);
    }

    public Pixmap Frame(GuiLayoutControl control, int imageIndex)
    {
        ArgumentNullException.ThrowIfNull(control);

        return (uint)imageIndex >= (uint)control.Images.Count
            ? default
            : Frame(control.Images[imageIndex].EntryName, control.Images[imageIndex].Frame);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _layouts?.Dispose();
        _fonts?.Dispose();
        _legend?.Dispose();
        _miniGames?.Dispose();
    }

    private static byte[] TryReadLocalNpcIllustrations(ClientDirectory clientDirectory)
    {
        try
        {
            using var archive = clientDirectory.TryOpenArchive("npc/npcbase.dat");
            if (archive is null)
            {
                return [];
            }

            return archive.TryRead(NpcIllustrationTable.LocalEntryName, out var entry) ? entry.ToArray() : [];
        }
        catch (Exception e) when (e is InvalidDataException or NotSupportedException or IOException)
        {
            return [];
        }
    }

    private Pixmap Decode(string entryName, int frameIndex)
    {
        if (Image(entryName) is not { } image || (uint)frameIndex >= (uint)image.FrameCount)
        {
            return default;
        }

        var frame = image[frameIndex];
        if (frame.IsEmpty)
        {
            return default;
        }

        var source = image.GetPixels(frameIndex);
        if (source.IsEmpty)
        {
            return default;
        }

        var width = frame.Width;
        var height = frame.Height;
        var pixels = new ushort[width * height];

        if (image.PixelMode == SpfImage.DirectPixelMode)
        {
            for (var y = 0; y < height; y++)
            {
                var row = source.Slice(y * frame.Pitch, Math.Min(frame.Pitch, width * 2));
                for (var x = 0; x * 2 + 1 < row.Length; x++)
                {
                    pixels[(y * width) + x] = (ushort)(row[x * 2] | (row[(x * 2) + 1] << 8));
                }
            }
        }
        else
        {
            var colors = EmbeddedPalette(entryName, image);
            if (colors.IsEmpty)
            {
                return default;
            }

            for (var y = 0; y < height; y++)
            {
                var start = y * frame.Pitch;
                if (start + width > source.Length)
                {
                    break;
                }

                var row = source.Slice(start, width);
                for (var x = 0; x < width; x++)
                {
                    int index = row[x];
                    if (index != 0)
                    {
                        pixels[(y * width) + x] = colors[index];
                    }
                }
            }
        }

        DecodedFrames++;
        return new Pixmap(pixels, width, height);
    }

    public const ushort PlaceholderColor = 0x1BEF;

    private ReadOnlySpan<ushort> EmbeddedPalette(string entryName, SpfImage image)
    {
        if (_embeddedPalettes.TryGetValue(entryName, out var cached))
        {
            return cached;
        }

        var source = _packing.Format == RenderPixelFormat.Rgb555 ? image.Rgb555Palette : image.Rgb565Palette;
        if (source.IsEmpty)
        {
            _embeddedPalettes[entryName] = [];
            return default;
        }

        var colors = source.ToArray();

        if (colors[PalettePacker.GuiFixupIndex] == PlaceholderColor)
        {
            colors[PalettePacker.GuiFixupIndex] =
                colors[0] == PalettePacker.TransparentKey ? PalettePacker.BumpedBlack : colors[0];
        }

        colors[0] = PalettePacker.TransparentKey;

        _embeddedPalettes[entryName] = colors;
        return colors;
    }

    private Pixmap DecodePalettized(string entryName, int frameIndex, int paletteNumber) =>
        DecodePalettized(entryName, frameIndex, GuiPalette(paletteNumber));

    private Pixmap DecodePalettized(string entryName, int frameIndex, PackedPalette? colors)
    {
        if (colors is null || Epf(entryName) is not { } image)
        {
            return default;
        }

        if ((uint)frameIndex >= (uint)image.FrameCount)
        {
            return default;
        }

        var frame = image[frameIndex];
        var indexes = image.GetPixels(frameIndex);
        if (frame.IsEmpty || indexes.IsEmpty)
        {
            return default;
        }

        var width = frame.Width;
        var height = frame.Height;
        var pixels = new ushort[width * height];

        for (var y = 0; y < height; y++)
        {
            var start = y * width;
            if (start + width > indexes.Length)
            {
                break;
            }

            var row = indexes.Slice(start, width);
            for (var x = 0; x < width; x++)
            {
                int index = row[x];
                if (index != 0)
                {
                    pixels[start + x] = colors[index];
                }
            }
        }

        DecodedFrames++;
        return new Pixmap(pixels, width, height);
    }

    private EpfImage? Epf(string entryName)
    {
        if (_epfImages.TryGetValue(entryName, out var existing))
        {
            return existing;
        }

        EpfImage? parsed = null;
        ReadOnlyMemory<byte> data;

        if ((_layouts?.TryReadMemory(entryName, out data) ?? false)
            || (_legend?.TryReadMemory(entryName, out data) ?? false)
            || (_miniGames?.TryReadMemory(entryName, out data) ?? false)
            || (_fonts?.TryReadMemory(entryName, out data) ?? false))
        {
            try
            {
                parsed = EpfImage.Parse(data);
            }
            catch (InvalidDataException)
            {
            }
        }

        _epfImages[entryName] = parsed;
        return parsed;
    }

    private SpfImage? Image(string entryName)
    {
        if (_images.TryGetValue(entryName, out var existing))
        {
            return existing;
        }

        SpfImage? parsed = null;
        ReadOnlyMemory<byte> data;

        if ((_layouts?.TryReadMemory(entryName, out data) ?? false)
            || (_miniGames?.TryReadMemory(entryName, out data) ?? false)
            || (_fonts?.TryReadMemory(entryName, out data) ?? false))
        {
            try
            {
                parsed = SpfImage.Parse(data);
            }
            catch (InvalidDataException)
            {
            }
        }

        _images[entryName] = parsed;
        return parsed;
    }
}
