using DaScreenshotStudio.Assets.Archives;
using DaScreenshotStudio.Assets.Imaging;
using DaScreenshotStudio.Assets.Palettes;
using DaScreenshotStudio.Engine.Rendering;

namespace DaScreenshotStudio.Engine.World;

public sealed class GroundItemArt
{
    public const int FramesPerFile = 266;

    public const int CanvasSize = 32;

    public const int Anchor = CanvasSize / 2;

    private readonly IAssetArchive _archive;
    private readonly CharacterArt _palettes;
    private readonly PaletteFamily _items;
    private readonly BoundedCache<EpfImage?> _files = new();

    private readonly BoundedCache<WorldSprite> _sprites = new(BoundedCache<WorldSprite>.MaxFrames);

    public GroundItemArt(IAssetArchive archive, CharacterArt palettes)
    {
        ArgumentNullException.ThrowIfNull(archive);
        ArgumentNullException.ThrowIfNull(palettes);

        _archive = archive;
        _palettes = palettes;
        _items = PaletteFamily.Load(archive, "item");
    }

    public int DecodedFrames { get; private set; }

    public static void Locate(int sprite, out int file, out int frame)
    {
        var index = Math.Max(0, sprite - 1);
        file = (index / FramesPerFile) + 1;
        frame = index % FramesPerFile;
    }

    public static string NameFor(int file) => $"item{file:D3}.epf";

    public int HighestSprite()
    {
        var file = 0;
        foreach (var entry in _archive.Entries)
        {
            var name = entry.Name.AsSpan();
            if (name.Length == "item".Length + 7
                && name.StartsWith("item", StringComparison.OrdinalIgnoreCase)
                && name.EndsWith(".epf", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(name.Slice("item".Length, 3), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var number))
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
                    return ((file - 1) * FramesPerFile) + frame + 1;
                }
            }
        }

        return 0;
    }

    public bool TryGet(int sprite, out WorldSprite result) => TryGet(sprite, 0, default, out result);

    public bool HasArt(int sprite)
    {
        Locate(sprite, out var file, out var frameIndex);
        if (GetFile(file) is not { } image || frameIndex >= image.FrameCount || image[frameIndex].IsEmpty)
        {
            return false;
        }

        var frame = image[frameIndex];
        var indexes = image.GetPixels(frameIndex);
        indexes = indexes[..Math.Min(indexes.Length, frame.Width * frame.Height)];
        if (!indexes.ContainsAnyExcept((byte)0))
        {
            return false;
        }

        var colors = _palettes.Pack(_items.ForAsset(Math.Max(0, sprite - 1))).Colors;
        foreach (var value in indexes)
        {
            if (value != 0 && colors[value] != 0)
            {
                return true;
            }
        }

        return false;
    }

    public bool TryGet(int sprite, byte dye, out WorldSprite result)
    {
        var colors = dye == 0 ? default : _palettes.GetDye(dye);
        return colors.IsEmpty
            ? TryGet(sprite, 0, default, out result)
            : TryGet(sprite, dye, colors, out result);
    }

    private bool TryGet(int sprite, byte dye, ReadOnlySpan<ushort> colors, out WorldSprite result)
    {
        var key = ((long)sprite << 8) | dye;
        if (_sprites.TryTouch(key, out result))
        {
            return !result.IsEmpty;
        }

        result = Decode(sprite, colors);
        _sprites.Store(key, result, (long)result.Art.Width * result.Art.Height * sizeof(ushort));
        return !result.IsEmpty;
    }

    private WorldSprite Decode(int sprite, ReadOnlySpan<ushort> dye)
    {
        Locate(sprite, out var file, out var frameIndex);

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

        var colors = _palettes.Pack(_items.ForAsset(Math.Max(0, sprite - 1))).Colors;

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
        return new WorldSprite(new Pixmap(pixels, width, height), Anchor - frame.Left, Anchor - frame.Top);
    }

    private EpfImage? GetFile(int file)
    {
        if (_files.TryTouch(file, out var existing))
        {
            return existing;
        }

        EpfImage? parsed = null;
        if (_archive.TryReadMemory(NameFor(file), out var data))
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
