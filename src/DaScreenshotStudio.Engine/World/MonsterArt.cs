using DaScreenshotStudio.Assets.Archives;
using DaScreenshotStudio.Assets.Characters;
using DaScreenshotStudio.Assets.Imaging;
using DaScreenshotStudio.Assets.Palettes;
using DaScreenshotStudio.Engine.Rendering;
using DaScreenshotStudio.Engine.Core;

namespace DaScreenshotStudio.Engine.World;

public sealed class MonsterArt
{
    public const int WalkRange = -1;

    public const long PixelBudgetBytes = HumanImageCache.PixelBudgetBytes;

    public const int MaxSprites = 4096;

    private const int StandingRange = 14;

    private readonly IAssetArchive _archive;
    private readonly CharacterArt _palettes;
    private readonly BoundedCache<MpfImage?> _sheets = new();
    private readonly BoundedCache<FrameKey, WorldSprite> _sprites = new(MaxSprites, PixelBudgetBytes);
    private readonly BoundedCache<int> _idleRises = new();

    public MonsterArt(IAssetArchive archive, CharacterArt palettes)
    {
        ArgumentNullException.ThrowIfNull(archive);
        ArgumentNullException.ThrowIfNull(palettes);

        _archive = archive;
        _palettes = palettes;
    }

    public int DecodedFrames { get; private set; }

    public bool TryGet(
        int sprite,
        byte dye,
        Direction facing,
        int motionFrame,
        in CreatureColoring coloring,
        out WorldSprite result) =>
        TryGet(sprite, dye, facing, WalkRange, motionFrame, in coloring, out result);

    public bool TryGet(
        int sprite,
        byte dye,
        Direction facing,
        int attackRange,
        int motionFrame,
        in CreatureColoring coloring,
        out WorldSprite result)
    {
        var direction = HumanImageCache.ToHumanDirection(facing);
        var group = HumanPartOrder.FrameGroup(direction);
        var mirrored = HumanPartOrder.IsMirrored(direction);

        var admitted = Admitted(sprite, in coloring);
        dye = Admitted(sprite, dye, in admitted);

        var key = new FrameKey(
            sprite,
            dye,
            admitted,
            ((attackRange + 1) << 16) | (group << 9) | ((mirrored ? 1 : 0) << 8) | (motionFrame & 0xFF));

        if (_sprites.TryTouch(key, out result))
        {
            return !result.IsEmpty;
        }

        result = Decode(sprite, dye, in admitted, group, mirrored, attackRange, motionFrame);
        Store(key, in result);
        return !result.IsEmpty;
    }

    private byte Admitted(int sprite, byte dye, in CreatureColoring coloring)
    {
        if (dye == 0 || coloring.IsInstalled || GetSheet(sprite) is not { AdmitsDye: true })
        {
            return 0;
        }

        return _palettes.GetDye(dye).IsEmpty ? (byte)0 : dye;
    }

    private CreatureColoring Admitted(int sprite, in CreatureColoring coloring) =>
        GetSheet(sprite) is { } sheet
            ? coloring.AdmittedFor(sheet.ColoringRanges.Length)
            : CreatureColoring.None;

    public int AttackFrameCount(int sprite, int attackRange)
    {
        if ((uint)attackRange >= MpfImage.AttackRangeCount || GetSheet(sprite) is not { } sheet)
        {
            return 0;
        }

        return sheet.AttackLengths[attackRange];
    }

    public ReadOnlySpan<MpfStandingSegment> StandingSegments(int sprite) =>
        GetSheet(sprite) is { } sheet ? sheet.StandingSegments : default;

    public bool TryPickStandingSegment(int sprite, int roll, out MpfStandingSegment segment)
    {
        if (GetSheet(sprite) is { } sheet && sheet.PickStandingSegment(roll) is var index and >= 0)
        {
            segment = sheet.StandingSegments[index];
            return true;
        }

        segment = default;
        return false;
    }

    public bool TryGetStanding(
        int sprite,
        byte dye,
        Direction facing,
        in MpfStandingSegment segment,
        int step,
        in CreatureColoring coloring,
        out WorldSprite result)
    {
        var direction = HumanImageCache.ToHumanDirection(facing);
        var mirrored = HumanPartOrder.IsMirrored(direction);

        if (GetSheet(sprite) is not { } sheet)
        {
            result = default;
            return false;
        }

        var index = sheet.SelectStandingFrame(in segment, step, HumanPartOrder.FrameGroup(direction));
        if (index < 0)
        {
            result = default;
            return false;
        }

        var admitted = Admitted(sprite, in coloring);
        dye = Admitted(sprite, dye, in admitted);

        var key = new FrameKey(
            sprite,
            dye,
            admitted,
            (StandingRange << 16) | ((mirrored ? 1 : 0) << 8) | (index & 0xFF));

        if (_sprites.TryTouch(key, out result))
        {
            return !result.IsEmpty;
        }

        result = Decode(sheet, dye, in admitted, mirrored, index);
        Store(key, in result);
        return !result.IsEmpty;
    }

    public int WalkSequence(int sprite) => GetSheet(sprite)?.WalkSequence ?? 0;

    public int MeterHeight(int sprite) => IdleRise(sprite);

    public int NameLabelHeight(int sprite) =>
        GetSheet(sprite) is null ? 0 : IdleRise(sprite) + ObjectNamePane.CreatureLift;

    private int IdleRise(int sprite)
    {
        if (_idleRises.TryTouch(sprite, out var cached))
        {
            return cached;
        }

        var rise = 0;
        if (GetSheet(sprite) is { } sheet)
        {
            var frames = sheet.Frames;
            var count = sheet.IdleLength == 0 ? 1 : sheet.IdleLength;
            var start = sheet.IdleStart >= frames.Length ? 0 : sheet.IdleStart;
            var sum = 0;
            var read = 0;

            for (var i = start; i < start + count; i++)
            {
                if (i >= frames.Length)
                {
                    continue;
                }

                sum += frames[i].AnchorY - frames[i].Top;
                read++;
            }

            rise = read > 0 ? sum / read : 0;
        }

        _idleRises.Store(sprite, rise);
        return rise;
    }

    private WorldSprite Decode(
        int sprite,
        byte dye,
        in CreatureColoring coloring,
        int group,
        bool mirrored,
        int attackRange,
        int motionFrame)
    {
        if (GetSheet(sprite) is not { } sheet)
        {
            return default;
        }

        var index = attackRange == WalkRange
            ? sheet.SelectWalkFrame(group, motionFrame)
            : sheet.SelectAttackFrame(attackRange, group, motionFrame);

        return index < 0 ? default : Decode(sheet, dye, in coloring, mirrored, index);
    }

    private WorldSprite Decode(MpfImage sheet, byte dye, in CreatureColoring coloring, bool mirrored, int index)
    {
        var frame = sheet.Frames[index];
        var indexes = sheet.GetPixels(index);
        if (indexes.IsEmpty)
        {
            return default;
        }

        Palette? palette = null;
        if (_archive.TryRead(MpfImage.PaletteNameFor(sheet.PaletteNumber), out var paletteData))
        {
            palette = Palette.Parse(paletteData);
        }

        scoped ReadOnlySpan<ushort> colors = _palettes.Pack(palette).Colors;
        Span<ushort> remapped = stackalloc ushort[PalettePacker.ColorCount];
        if (coloring.IsInstalled && Remap(sheet, in coloring, colors, remapped))
        {
            colors = remapped;
        }

        var tuple = dye == 0 ? default : _palettes.GetDye(dye);
        var width = frame.Width;
        var height = frame.Height;
        var pixels = new ushort[width * height];

        for (var y = 0; y < height; y++)
        {
            var row = indexes.Slice(y * width, width);
            var destination = (y * width) + (mirrored ? width - 1 : 0);
            var step = mirrored ? -1 : 1;

            for (var x = 0; x < width; x++)
            {
                int value = row[x];
                if (value != 0)
                {
                    var dyed = value - ColoringTable.DyeFirstIndex;
                    pixels[destination + (x * step)] =
                        (uint)dyed < (uint)tuple.Length ? tuple[dyed] : colors[value];
                }
            }
        }

        DecodedFrames++;

        var anchorX = mirrored ? frame.Right - frame.AnchorX : frame.AnchorX - frame.Left;
        return new WorldSprite(new Pixmap(pixels, width, height), anchorX, frame.AnchorY - frame.Top);
    }

    private bool Remap(
        MpfImage sheet,
        in CreatureColoring coloring,
        ReadOnlySpan<ushort> colors,
        Span<ushort> destination)
    {
        var ranges = sheet.ColoringRanges;
        var moved = false;

        for (var i = 0; i < ranges.Length && i < CreatureColoring.SelectorCount; i++)
        {
            if (ranges[i].Table != CharacterArt.ColoringTableNumber)
            {
                continue;
            }

            var tuple = _palettes.GetDye(coloring[i]);
            if (tuple.IsEmpty)
            {
                continue;
            }

            var first = ranges[i].FirstIndex;
            var last = Math.Min(Math.Min(first + tuple.Length - 1, ranges[i].LastIndex), PalettePacker.ColorCount - 1);

            if (!moved)
            {
                colors.CopyTo(destination);
                moved = true;
            }

            for (var source = first; source <= last; source++)
            {
                destination[source] = tuple[source - first];
            }
        }

        return moved;
    }

    private void Store(in FrameKey key, in WorldSprite sprite) =>
        _sprites.Store(key, sprite, (long)sprite.Art.Width * sprite.Art.Height * sizeof(ushort));

    public IReadOnlyList<int> Sprites()
    {
        var sprites = new List<int>();
        foreach (var entry in _archive.Entries)
        {
            var name = entry.Name.AsSpan();
            if (name.Length > 7
                && name.StartsWith("mns", StringComparison.OrdinalIgnoreCase)
                && name.EndsWith(".mpf", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(name[3..^4], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var sprite))
            {
                sprites.Add(sprite);
            }
        }

        sprites.Sort();
        return sprites;
    }

    private MpfImage? GetSheet(int sprite)
    {
        if (_sheets.TryTouch(sprite, out var existing))
        {
            return existing;
        }

        MpfImage? sheet = null;
        if (_archive.TryReadMemory(MpfImage.NameFor(sprite), out var data))
        {
            try
            {
                sheet = MpfImage.Parse(data);
            }
            catch (InvalidDataException)
            {
            }
        }

        _sheets.Store(sprite, sheet);
        return sheet;
    }

    private readonly record struct FrameKey(int Sprite, byte Dye, CreatureColoring Coloring, int View);
}
