using DaScreenshotStudio.Engine.Rendering;
using DaScreenshotStudio.Engine.Records;

namespace DaScreenshotStudio.Engine.Ui;

public sealed class SpelledViewPane : Pane
{
    public const string BackgroundEntryName = "spelled.epf";

    public const int SlotCount = SpelledEffect.IndicatorSlotCount;

    public const int SlotPitch = 23;

    public const int IconOffset = 3;

    public const int IconBoxSize = 15;

    public const int BarTopOffset = 19;

    public const int BarLeft = 4;

    public const int BarHeight = 2;

    public const int OriginY = 2;

    public const int OriginX = 0x26A;

    public const short EmptySlot = -1;

    private static readonly byte[] StagePaletteIndexes = [0x58, 0x89, 0x45, 0x97, 0x28, 0xFF];

    private readonly UiAssets _assets;
    private readonly PixelBlender _blender;
    private readonly short[] _icons = new short[SlotCount];
    private readonly byte[] _stages = new byte[SlotCount];
    private readonly BoundedCache<Pixmap> _halved = new(BoundedCache<Pixmap>.MaxFrames);
    private readonly Pixmap _background;
    private bool _everUpdated;

    public SpelledViewPane(UiAssets assets, PixelBlender blender)
        : base(BoundsFor(assets), blender)
    {
        ArgumentNullException.ThrowIfNull(assets);

        _assets = assets;
        _blender = blender;
        _background = assets.PaletteFrame(BackgroundEntryName, 0);
        TargetBlend = BlendMode.Transparent;

        Array.Fill(_icons, EmptySlot);
    }

    private static PixelRect BoundsFor(UiAssets assets)
    {
        ArgumentNullException.ThrowIfNull(assets);

        var frame = assets.PaletteFrame(BackgroundEntryName, 0);
        return new PixelRect(OriginX, OriginY, frame.Width, frame.Height);
    }

    public int ActiveCount
    {
        get
        {
            var count = 0;
            for (var i = 0; i < SlotCount; i++)
            {
                if (_icons[i] != EmptySlot)
                {
                    count++;
                }
            }

            return count;
        }
    }

    public short IconAt(int slot) => _icons[slot];

    public byte StageAt(int slot) => _stages[slot];

    public void Apply(in SpelledEffect effect)
    {
        var icon = unchecked((short)effect.Icon);
        var stage = effect.DurationStage;
        var matched = false;

        for (var i = 0; i < SlotCount && !matched; i++)
        {
            if (_icons[i] != icon)
            {
                continue;
            }

            if (stage == SpelledEffect.StageNone)
            {
                _icons[i] = EmptySlot;
                _stages[i] = 0;
            }
            else
            {
                _stages[i] = stage;
            }

            matched = true;
        }

        if (!matched && stage != SpelledEffect.StageNone)
        {
            for (var i = 0; i < SlotCount && !matched; i++)
            {
                if (_icons[i] != EmptySlot)
                {
                    continue;
                }

                _icons[i] = icon;
                _stages[i] = stage;
                matched = true;
            }
        }

        _everUpdated = true;
        Invalidate();

        Visible = ActiveCount > 0;
    }

    public void SetSlots(ReadOnlySpan<(short Icon, byte Stage)> slots)
    {
        Array.Fill(_icons, EmptySlot);
        Array.Clear(_stages);

        for (var i = 0; i < Math.Min(slots.Length, SlotCount); i++)
        {
            _icons[i] = slots[i].Icon;
            _stages[i] = slots[i].Icon == EmptySlot ? (byte)0 : slots[i].Stage;
        }

        _everUpdated = slots.Length > 0;
        Visible = !_everUpdated || ActiveCount > 0;
        Invalidate();
    }

    protected override void OnDrawContent(Canvas canvas)
    {
        if (!_everUpdated)
        {
            return;
        }

        canvas.Clear();

        if (!_background.IsEmpty)
        {
            canvas.BlitPixmap(0, 0, in _background, BlendMode.Transparent);
        }

        for (var i = 0; i < SlotCount; i++)
        {
            var icon = _icons[i];
            if (icon == EmptySlot)
            {
                continue;
            }

            var stage = unchecked((sbyte)_stages[i]);
            var index = stage - 1;
            if ((uint)index < (uint)StagePaletteIndexes.Length)
            {
                canvas.State.PrimaryColor = ColorFor(StagePaletteIndexes[index]);
            }

            var top = (i * SlotPitch) + BarTopOffset;
            canvas.FillRect(PixelRect.FromEdges(BarLeft, top, (stage * 2) + 6, top + BarHeight));

            var halved = HalvedIcon(icon);
            if (!halved.IsEmpty)
            {
                canvas.BlitPixmap(IconOffset, (i * SlotPitch) + IconOffset, in halved, BlendMode.Transparent);
            }
        }
    }

    private ushort ColorFor(byte index)
    {
        var colors = _assets.LegendColors;
        return index < colors.Length ? colors[index] : (ushort)0;
    }

    private Pixmap HalvedIcon(short icon)
    {
        var key = (ushort)icon;
        if (_halved.TryTouch(key, out var existing))
        {
            return existing;
        }

        var halved = default(Pixmap);
        if (_assets.SpellIcons is { } bank)
        {
            var frame = bank.Frame(key);
            if (!frame.Image.IsEmpty)
            {
                var buffer = new ushort[PixmapResampler.HalvedPixelCount(in frame.Image)];
                halved = buffer.Length == 0
                    ? default
                    : PixmapResampler.Halve(in frame.Image, _blender, buffer);
            }
        }

        _halved.Store(key, halved, (long)halved.Width * halved.Height * sizeof(ushort));
        return halved;
    }
}
