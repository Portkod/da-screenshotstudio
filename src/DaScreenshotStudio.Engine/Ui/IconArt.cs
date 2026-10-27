using DaScreenshotStudio.Assets.Archives;
using DaScreenshotStudio.Assets.Imaging;
using DaScreenshotStudio.Assets.Palettes;
using DaScreenshotStudio.Engine.Rendering;

namespace DaScreenshotStudio.Engine.Ui;

public readonly struct IconFrame
{
    public IconFrame(in Pixmap image, int left, int top)
    {
        Image = image;
        Left = left;
        Top = top;
    }

    public readonly Pixmap Image;

    public readonly int Left;

    public readonly int Top;

    public int Width => Image.Width;

    public int Height => Image.Height;

    public bool IsEmpty => Image.IsEmpty;
}

public sealed class IconArt
{
    public const int FramesPerFile = 266;

    public const int AbilityBias = 0;

    public const int ItemBias = 0x8001;

    public const int ItemTag = 0x8000;

    private readonly IAssetArchive _archive;
    private readonly string _prefix;
    private readonly PaletteFamily? _family;
    private readonly PackedPalette? _fixedColors;
    private readonly PixelPacking _packing;
    private readonly BoundedCache<EpfImage?> _files = new();

    private readonly BoundedCache<IconFrame> _frames = new(BoundedCache<IconFrame>.MaxFrames);
    private readonly Dictionary<Palette, ushort[]> _packed = [];
    private readonly ushort[] _empty = new ushort[Palette.ColorCount];
    private readonly PackedColoringTable? _dyes;
    private readonly bool _numbered;
    private readonly int _bias;

    private IconArt(
        IAssetArchive archive,
        string prefix,
        bool numbered,
        int bias,
        PaletteFamily? family,
        PackedPalette? fixedColors,
        in PixelPacking packing)
    {
        _archive = archive;
        _prefix = prefix;
        _numbered = numbered;
        _bias = bias;
        _family = family;
        _fixedColors = fixedColors;
        _packing = packing;
        _dyes = PackedColoringTable.TryLoad(archive, in packing);
    }

    public int DecodedFrames { get; private set; }

    public static IconArt? TryLoadItems(IAssetArchive? archive, in PixelPacking packing)
    {
        if (archive is null || !archive.Contains("item001.epf"))
        {
            return null;
        }

        return new IconArt(archive, "item", true, ItemBias, PaletteFamily.Load(archive, "item"), null, in packing);
    }

    public static IconArt? TryLoadAbilities(
        IAssetArchive? archive,
        string prefix,
        PackedPalette? palette,
        in PixelPacking packing)
    {
        ArgumentException.ThrowIfNullOrEmpty(prefix);

        if (archive is null || !archive.Contains(prefix + "001.epf"))
        {
            return null;
        }

        return new IconArt(archive, prefix, false, AbilityBias, null, palette, in packing);
    }

    public static void Locate(int icon, int bias, out int file, out int frame)
    {
        var index = IndexOf(icon, bias);
        file = (index / FramesPerFile) + 1;
        frame = index % FramesPerFile;
    }

    public static int IndexOf(int icon, int bias) => icon > bias - 1 ? icon - bias : 0;

    public IconFrame Frame(int icon) => Frame(icon, 0);

    public IconFrame Frame(int icon, byte dye) => FrameAt(IndexOf(icon, _bias), dye);

    private IconFrame FrameAt(int index, byte dye)
    {
        var colors = dye == 0 || _dyes is null ? default : _dyes.Get(dye);
        return colors.IsEmpty ? FrameAt(index, 0, default) : FrameAt(index, dye, colors);
    }

    private IconFrame FrameAt(int index, byte dye, ReadOnlySpan<ushort> colors)
    {
        var key = ((long)index << 8) | dye;
        if (_frames.TryTouch(key, out var existing))
        {
            return existing;
        }

        var decoded = Decode(index, colors);
        _frames.Store(key, decoded, (long)decoded.Width * decoded.Height * sizeof(ushort));
        return decoded;
    }

    private IconFrame Decode(int index, ReadOnlySpan<ushort> dye)
    {
        var file = (index / FramesPerFile) + 1;
        var frameIndex = index % FramesPerFile;

        if (GetFile(file) is not { } image || frameIndex >= image.FrameCount)
        {
            return default;
        }

        var frame = image[frameIndex];
        if (frame.IsEmpty)
        {
            return default;
        }

        var indexes = image.GetPixels(frameIndex);
        if (indexes.IsEmpty)
        {
            return default;
        }

        var colors = PackFor(index);
        var width = frame.Width;
        var height = frame.Height;
        var pixels = new ushort[width * height];

        for (var y = 0; y < height; y++)
        {
            var start = y * width;
            if (start >= indexes.Length)
            {
                break;
            }

            var row = indexes.Slice(start, Math.Min(width, indexes.Length - start));
            for (var x = 0; x < row.Length; x++)
            {
                int value = row[x];
                if (value == 0)
                {
                    continue;
                }

                var dyed = value - ColoringTable.DyeFirstIndex;
                pixels[start + x] = (uint)dyed < (uint)dye.Length ? dye[dyed] : colors[value];
            }
        }

        DecodedFrames++;
        return new IconFrame(new Pixmap(pixels, width, height), frame.Left, frame.Top);
    }

    private ReadOnlySpan<ushort> PackFor(int index)
    {
        if (_family is null)
        {
            return _fixedColors is null ? _empty : _fixedColors.Colors;
        }

        var palette = _family.ForAsset(index);
        if (palette is null)
        {
            return _empty;
        }

        if (_packed.TryGetValue(palette, out var colors))
        {
            return colors;
        }

        colors = new ushort[Palette.ColorCount];
        PalettePacker.Pack(palette, in _packing, colors);
        _packed[palette] = colors;
        return colors;
    }

    public int HighestIcon()
    {
        var file = 0;
        foreach (var entry in _archive.Entries)
        {
            var name = entry.Name.AsSpan();
            if (name.Length == _prefix.Length + 7
                && name.StartsWith(_prefix, StringComparison.OrdinalIgnoreCase)
                && name.EndsWith(".epf", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(name.Slice(_prefix.Length, 3), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var number))
            {
                file = Math.Max(file, number);
            }
        }

        for (; file >= 1; file--)
        {
            if (GetFile(file) is not { } image)
            {
                continue;
            }

            for (var frame = Math.Min(image.FrameCount, FramesPerFile) - 1; frame >= 0; frame--)
            {
                if (!image[frame].IsEmpty)
                {
                    return ((file - 1) * FramesPerFile) + frame + _bias;
                }
            }
        }

        return 0;
    }

    private EpfImage? GetFile(int file)
    {
        if (_files.TryTouch(file, out var existing))
        {
            return existing;
        }

        EpfImage? parsed = null;
        var name = _numbered || file > 1 ? $"{_prefix}{file:D3}.epf" : $"{_prefix}001.epf";

        if (_archive.TryReadMemory(name, out var data))
        {
            try
            {
                parsed = EpfImage.Parse(data);
            }
            catch (InvalidDataException)
            {
            }
        }

        _files.Store(file, parsed);
        return parsed;
    }
}
