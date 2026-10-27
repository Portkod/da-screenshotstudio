using System.Buffers;
using DaScreenshotStudio.Assets.Archives;
using DaScreenshotStudio.Assets.Effects;
using DaScreenshotStudio.Assets.Imaging;
using DaScreenshotStudio.Assets.Palettes;
using DaScreenshotStudio.Engine.Rendering;
using DaScreenshotStudio.Engine.App;

namespace DaScreenshotStudio.Engine.World;

public sealed class EffectArt
{
    public const string ArchiveName = "roh.dat";

    public const string SequenceEntryName = "effect.tbl";

    public const string PaletteTableEntryName = "effpal.tbl";

    public const string MotionTableEntryName = "meffect.tbl";

    private const int NoAnchor = -1;

    private const byte AlphaPlaneBlending = 3;

    private const uint AlphaPlanePixelMode = 4;

    private readonly IAssetArchive _archive;
    private readonly PixelPacking _packing;
    private readonly PaletteRangeTable _palettes;
    private readonly PackedPaletteCache _packed;
    private readonly BoundedCache<EfaImage?> _efa = new();
    private readonly BoundedCache<EpfImage?> _epf = new();
    private readonly BoundedCache<EffectOffsetTable> _offsets = new();
    private readonly BoundedCache<EffectOffsetTable> _motionOffsets = new();
    private readonly BoundedCache<SpfImage?> _mefc = new();
    private readonly BoundedCache<WorldSprite> _sprites = new(BoundedCache<WorldSprite>.MaxFrames);
    private readonly BoundedCache<WorldSprite> _motionSprites = new(BoundedCache<WorldSprite>.MaxFrames);

    public EffectArt(IAssetArchive archive, in PixelPacking packing)
    {
        ArgumentNullException.ThrowIfNull(archive);

        _archive = archive;
        _packing = packing;
        _packed = new PackedPaletteCache(in packing);

        Sequences = archive.TryRead(SequenceEntryName, out var table)
            ? EffectFrameTable.Parse(table)
            : EffectFrameTable.Empty;

        _palettes = archive.TryRead(PaletteTableEntryName, out var palettes)
            ? PaletteRangeTable.Parse(palettes)
            : PaletteRangeTable.Empty;

        Motions = archive.TryRead(MotionTableEntryName, out var motions)
            ? MotionEffectTable.Parse(motions)
            : MotionEffectTable.Empty;
    }

    public EffectFrameTable Sequences { get; }

    public MotionEffectTable Motions { get; }

    public int DecodedFrames { get; private set; }

    public static EffectArt? TryLoad(ClientDirectory clientDirectory, in PixelPacking packing, out IDisposable? owned)
    {
        ArgumentNullException.ThrowIfNull(clientDirectory);
        owned = null;

        if (clientDirectory.TryOpenArchive(ArchiveName) is not { } archive)
        {
            return null;
        }

        owned = archive;
        return new EffectArt(archive, in packing);
    }

    public static string EfaNameFor(int effectNumber) => $"efct{effectNumber:D3}.efa";

    public static string EpfNameFor(int effectNumber) => $"efct{effectNumber:D3}.epf";

    public static string PaletteNameFor(int paletteNumber) => $"eff{paletteNumber:D3}.pal";

    public static string MotionNameFor(int motionIndex) => $"mefc{motionIndex:D3}.spf";

    public MotionEffectRecord? MotionFor(int motionIndex)
    {
        if (Motions.Records.Count == 0)
        {
            return null;
        }

        var stored = motionIndex >= 0 && motionIndex < Motions.LeadingValue
            ? Motions.Find(motionIndex)
            : null;

        return stored ?? Motions.Records[0];
    }

    public bool TryGetMotionFrame(int motionIndex, int frameIndex, out WorldSprite sprite)
    {
        var key = ((long)motionIndex << 12) | (uint)(frameIndex & 0xFFF);
        if (_motionSprites.TryTouch(key, out sprite))
        {
            return !sprite.IsEmpty;
        }

        sprite = DecodeMotion(motionIndex, frameIndex);
        _motionSprites.Store(key, sprite, (long)sprite.Art.Width * sprite.Art.Height * sizeof(ushort));
        return !sprite.IsEmpty;
    }

    private WorldSprite DecodeMotion(int motionIndex, int frameIndex)
    {
        if (Mefc(motionIndex) is not { } sheet || (uint)frameIndex >= (uint)sheet.FrameCount)
        {
            return default;
        }

        var frame = sheet[frameIndex];
        if (frame.IsEmpty)
        {
            return default;
        }

        var source = sheet.GetPixels(frameIndex);
        if (source.IsEmpty)
        {
            return default;
        }

        var colors = _packing.Format == RenderPixelFormat.Rgb555 ? sheet.Rgb555Palette : sheet.Rgb565Palette;
        if (colors.IsEmpty)
        {
            return default;
        }

        var width = frame.Width;
        var height = frame.Height;
        var pixels = new ushort[width * height];

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

        DecodedFrames++;

        var anchorX = frame.HasAnchor ? frame.AnchorX : NoAnchor;
        var anchorY = frame.HasAnchor ? frame.AnchorY : NoAnchor;
        if (MotionOffsets(motionIndex).TryAnchor(frameIndex, out var storedX, out var storedY))
        {
            anchorX = storedX;
            anchorY = storedY;
        }

        return new WorldSprite(new Pixmap(pixels, width, height), anchorX - frame.Left, anchorY - frame.Top);
    }

    private EffectOffsetTable MotionOffsets(int motionIndex)
    {
        if (_motionOffsets.TryTouch(motionIndex, out var existing))
        {
            return existing;
        }

        var parsed = _archive.TryRead(EffectOffsetTable.MotionNameFor(motionIndex), out var data)
            ? EffectOffsetTable.Parse(data)
            : EffectOffsetTable.Empty;

        _motionOffsets.Store(motionIndex, parsed);
        return parsed;
    }

    private SpfImage? Mefc(int motionIndex)
    {
        if (_mefc.TryTouch(motionIndex, out var existing))
        {
            return existing;
        }

        SpfImage? parsed = null;
        if (_archive.TryReadMemory(MotionNameFor(motionIndex), out var data))
        {
            try
            {
                parsed = SpfImage.Parse(data);
            }
            catch (InvalidDataException)
            {
            }
        }

        _mefc.Store(motionIndex, parsed);
        return parsed;
    }

    public int FrameIntervalMs(int effectId) =>
        Efa(effectId + 1) is { } efa ? (int)efa.FrameIntervalMs : 0;

    public BlendMode BlendFor(int effectId)
    {
        if (Efa(effectId + 1) is { } efa)
        {
            return efa.BlendMode switch
            {
                1 => BlendMode.Additive,
                2 => BlendMode.Screen,
                3 => BlendMode.AlphaPlane,
                _ => BlendMode.Transparent,
            };
        }

        var mapped = _palettes.GetPaletteNumber(effectId);
        return mapped != PaletteRangeTable.Unmapped && mapped / 1000 != 0
            ? BlendMode.SourceWeighted
            : BlendMode.Transparent;
    }

    public bool TryGetSequenceFrame(int effectId, int sequenceIndex, out int frame)
    {
        frame = 0;

        if ((uint)effectId >= (uint)Sequences.Count || sequenceIndex < 0)
        {
            return false;
        }

        if (Efa(effectId + 1) is { } efa)
        {
            if (sequenceIndex >= efa.FrameCount || efa.FrameCount <= 1)
            {
                return false;
            }

            frame = sequenceIndex;
            return true;
        }

        var sequence = Sequences.GetSequence(effectId);
        if (sequenceIndex >= sequence.Length)
        {
            return false;
        }

        frame = sequence[sequenceIndex];
        return true;
    }

    public bool TryGetFrame(int effectId, int frameNumber, out WorldSprite sprite) =>
        TryGetFrame(effectId, frameNumber, flipX: false, flipY: false, out sprite);

    public bool TryGetFrame(int effectId, int frameNumber, bool flipX, bool flipY, out WorldSprite sprite)
    {
        var flips = (flipX ? 1 : 0) | (flipY ? 2 : 0);
        var key = ((long)effectId << 14) | ((long)flips << 12) | (uint)(frameNumber & 0xFFF);
        if (_sprites.TryTouch(key, out sprite))
        {
            return !sprite.IsEmpty;
        }

        sprite = Decode(effectId + 1, frameNumber);
        if (flips != 0 && !sprite.IsEmpty)
        {
            sprite = Mirror(in sprite, flipX, flipY);
        }

        _sprites.Store(key, sprite, (long)sprite.Art.Width * sprite.Art.Height * sizeof(ushort));
        return !sprite.IsEmpty;
    }

    public bool FlipsWithDirection(int effectId) =>
        Efa(effectId + 1) is { } efa && (efa.Flags & 1) != 0;

    private static WorldSprite Mirror(in WorldSprite sprite, bool flipX, bool flipY)
    {
        var width = sprite.Art.Width;
        var height = sprite.Art.Height;
        var source = sprite.Art.Pixels;
        var pixels = new ushort[width * height];

        var weightsIn = sprite.AlphaWeights.Span;
        var hasWeights = weightsIn.Length == pixels.Length;
        var weights = hasWeights ? new byte[pixels.Length] : null;

        for (var y = 0; y < height; y++)
        {
            var sourceY = flipY ? height - 1 - y : y;
            var pixelRow = sourceY * sprite.Art.Stride;
            var weightRow = sourceY * width;
            var targetRow = y * width;
            for (var x = 0; x < width; x++)
            {
                var sourceX = flipX ? width - 1 - x : x;
                pixels[targetRow + x] = source[pixelRow + sourceX];
                if (weights is not null)
                {
                    weights[targetRow + x] = weightsIn[weightRow + sourceX];
                }
            }
        }

        return new WorldSprite(
            new Pixmap(pixels, width, height),
            flipX ? width - sprite.AnchorX : sprite.AnchorX,
            flipY ? height - sprite.AnchorY : sprite.AnchorY,
            weights);
    }

    private WorldSprite Decode(int effectNumber, int frameNumber)
    {
        if (Efa(effectNumber) is { } efa)
        {
            return DecodeEfa(efa, frameNumber);
        }

        return Epf(effectNumber) is { } epf ? DecodeEpf(epf, effectNumber, frameNumber) : default;
    }

    private WorldSprite DecodeEfa(EfaImage efa, int frameNumber)
    {
        if ((uint)frameNumber >= (uint)efa.FrameCount)
        {
            return default;
        }

        var frame = efa[frameNumber];
        var width = frame.CropWidth;
        var height = frame.CropHeight;
        if (width <= 0 || height <= 0 || frame.DecodedSize <= 0)
        {
            return default;
        }

        var buffer = ArrayPool<byte>.Shared.Rent(frame.DecodedSize);
        try
        {
            var written = efa.Decode(frameNumber, buffer);
            if (written != frame.DecodedSize)
            {
                return default;
            }

            var plane = EfaImage.GetPrimaryPlane(in frame, buffer.AsSpan(0, written));
            if (plane.IsEmpty)
            {
                return default;
            }

            var pixels = new ushort[width * height];
            var toRgb555 = _packing.Format == RenderPixelFormat.Rgb555;

            for (var y = 0; y < height; y++)
            {
                var row = plane.Slice(y * frame.PixelPitch, width * 2);

                for (var x = 0; x < width; x++)
                {
                    var value = (ushort)(row[x * 2] | (row[(x * 2) + 1] << 8));
                    pixels[(y * width) + x] = toRgb555 ? ToRgb555(value) : value;
                }
            }

            DecodedFrames++;

            byte[]? weights = null;
            if (efa.BlendMode == AlphaPlaneBlending && frame.PixelMode == AlphaPlanePixelMode)
            {
                weights = new byte[width * height];
                if (!EfaImage.TryGetAlphaWeights(in frame, buffer.AsSpan(0, written), weights))
                {
                    return default;
                }
            }

            return new WorldSprite(
                new Pixmap(pixels, width, height),
                frame.AnchorX - frame.CropLeft,
                frame.AnchorY - frame.CropTop,
                weights);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private WorldSprite DecodeEpf(EpfImage epf, int effectNumber, int frameNumber)
    {
        if ((uint)frameNumber >= (uint)epf.FrameCount)
        {
            return default;
        }

        var frame = epf[frameNumber];
        if (frame.IsEmpty)
        {
            return default;
        }

        var source = epf.GetPixels(frameNumber);
        if (source.IsEmpty)
        {
            return default;
        }

        var colors = PaletteFor(effectNumber).Colors;
        var width = frame.Width;
        var height = frame.Height;
        var pixels = new ushort[width * height];

        for (var y = 0; y < height; y++)
        {
            var start = y * width;
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
                    pixels[start + x] = colors[index];
                }
            }
        }

        DecodedFrames++;

        Offsets(effectNumber).Anchor(frameNumber, out var anchorX, out var anchorY);
        return new WorldSprite(new Pixmap(pixels, width, height), anchorX - frame.Left, anchorY - frame.Top);
    }

    private EffectOffsetTable Offsets(int effectNumber)
    {
        if (_offsets.TryTouch(effectNumber, out var existing))
        {
            return existing;
        }

        var parsed = _archive.TryRead(EffectOffsetTable.NameFor(effectNumber), out var data)
            ? EffectOffsetTable.Parse(data)
            : EffectOffsetTable.Empty;

        _offsets.Store(effectNumber, parsed);
        return parsed;
    }

    private PackedPalette PaletteFor(int effectNumber)
    {
        var mapped = _palettes.GetPaletteNumber(effectNumber - 1);
        var number = mapped == PaletteRangeTable.Unmapped ? 0 : mapped % 1000;

        if (_archive.TryRead(PaletteNameFor(number), out var data))
        {
            return _packed.Get(Palette.Parse(data));
        }

        return number != 0 && _archive.TryRead(PaletteNameFor(0), out var first)
            ? _packed.Get(Palette.Parse(first))
            : _packed.Fallback;
    }

    private static ushort ToRgb555(ushort rgb565) =>
        (ushort)(((rgb565 & 0xFFC0) >> 1) | (rgb565 & 0x001F));

    private EfaImage? Efa(int effectNumber)
    {
        if (_efa.TryTouch(effectNumber, out var existing))
        {
            return existing;
        }

        EfaImage? parsed = null;
        if (_archive.TryReadMemory(EfaNameFor(effectNumber), out var data))
        {
            try
            {
                parsed = EfaImage.Parse(data);
            }
            catch (InvalidDataException)
            {
            }
        }

        _efa.Store(effectNumber, parsed);
        return parsed;
    }

    private EpfImage? Epf(int effectNumber)
    {
        if (_epf.TryTouch(effectNumber, out var existing))
        {
            return existing;
        }

        EpfImage? parsed = null;
        if (_archive.TryReadMemory(EpfNameFor(effectNumber), out var data))
        {
            try
            {
                parsed = EpfImage.Parse(data);
            }
            catch (InvalidDataException)
            {
            }
        }

        _epf.Store(effectNumber, parsed);
        return parsed;
    }
}
