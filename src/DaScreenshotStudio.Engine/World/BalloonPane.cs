using DaScreenshotStudio.Engine.App;
using DaScreenshotStudio.Engine.Rendering;
using DaScreenshotStudio.Engine.Text;
using DaScreenshotStudio.Engine.Ui;
using DaScreenshotStudio.Engine.Core;
using DaScreenshotStudio.Engine.Records;

namespace DaScreenshotStudio.Engine.World;

public sealed class BalloonPane : Pane
{
    public const int LifetimeMs = 3000;

    public const int Capacity = 16;

    public const byte SayPalette = TextMarkup.White;

    public const byte ShoutPalette = TextMarkup.Yellow;

    public const byte ChantPalette = TextMarkup.Chant;

    public const byte BorderPalette = TextMarkup.White;

    public const byte FillPalette = TextPalette.ShadowIndex;

    public const int FramePadding = 8;

    public const int CornerRadius = 5;

    public const int ChantDrop = 12;

    public const int HumanLift = 0x40;

    public const int CellArea = 72;

    public const int LineStep = 12;

    public const int CellWidth = 6;

    public const int DefaultTextWidth = 112;

    public const int MaxTextHeight = 80;

    public const int ChantCenterLimit = 10;

    private static ReadOnlySpan<byte> CornerSteps => [4, 1, 3, 1, 2, 2, 1, 3, 1, 4];

    private readonly Balloon[] _balloons = new Balloon[Capacity];
    private readonly TextBlock[] _text = new TextBlock[Capacity];
    private readonly WorldScene _scene;
    private int _count;
    private int _centerBiasY = WorldViewLayout.CompactCenterBiasY;

    public BalloonPane(in PixelRect bounds, PixelBlender blender, WorldScene scene, GameFont font, TextPalette palette)
        : base(in bounds, blender)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(palette);

        _scene = scene;
        Palette = palette;

        TargetBlend = BlendMode.Shade;
        blender.ShadeKey = palette[FillPalette];

        for (var i = 0; i < _text.Length; i++)
        {
            _text[i] = new TextBlock(font, palette, DefaultTextWidth, MaxTextHeight / LineStep)
            {
                BreaksOnWordBoundary = true,
            };
        }
    }

    public TextPalette Palette { get; }

    public int Count => _count;

    public void ApplyViewLayout(in WorldViewLayout layout)
    {
        _centerBiasY = layout.CenterBiasY;
        SetBounds(layout.PaneBounds);
    }

    public void Show(EntityId speaker, SpeechMode mode, scoped ReadOnlySpan<byte> text, uint now)
    {
        var slot = -1;
        for (var i = 0; i < _count; i++)
        {
            if (_balloons[i].Speaker == speaker)
            {
                slot = i;
                break;
            }
        }

        if (slot < 0 && _count < Capacity)
        {
            slot = _count++;
        }
        else if (slot < 0)
        {
            for (var i = 1; i < Capacity; i++)
            {
                (_text[i - 1], _text[i]) = (_text[i], _text[i - 1]);
                _balloons[i - 1] = _balloons[i];
            }

            slot = Capacity - 1;
        }

        _balloons[slot] = new Balloon(speaker, mode, unchecked(now + LifetimeMs), TextMarkup.StrippedLength(text));

        Measure(_balloons[slot].Characters, out var boxWidth, out _);
        _text[slot].MaxWidth = boxWidth - (FramePadding * 2);
        _text[slot].DefaultPaletteIndex = PaletteFor(mode);
        _text[slot].SetText(text);

        Invalidate();
    }

    public bool Tick(uint now)
    {
        var changed = false;

        for (var i = _count - 1; i >= 0; i--)
        {
            if (!GameClock.IsDue(now, _balloons[i].Expires))
            {
                continue;
            }

            for (var j = i; j < _count - 1; j++)
            {
                (_text[j], _text[j + 1]) = (_text[j + 1], _text[j]);
                _balloons[j] = _balloons[j + 1];
            }

            _count--;
            changed = true;
        }

        if (changed)
        {
            Invalidate();
        }

        return changed;
    }

    public void Clear()
    {
        if (_count == 0)
        {
            return;
        }

        _count = 0;
        Invalidate();
    }

    public static byte PaletteFor(SpeechMode mode) => mode switch
    {
        SpeechMode.Shout => ShoutPalette,
        SpeechMode.Chant => ChantPalette,
        _ => SayPalette,
    };

    public static void Measure(int characters, out int width, out int height)
    {
        var area = unchecked((sbyte)(characters + 2)) * CellArea;
        var textWidth = DefaultTextWidth;
        var textHeight = ((((area / textWidth) - 1) / LineStep) + 1) * LineStep;

        if (textHeight > MaxTextHeight)
        {
            textHeight = MaxTextHeight;
            textWidth = ((((area / textHeight) - 1) / CellWidth) + 1) * CellWidth;
        }

        width = textWidth + (FramePadding * 2);
        height = textHeight + (FramePadding * 2);
    }

    protected override bool DrawsNothing => _count == 0;

    protected override void OnDrawContent(Canvas canvas)
    {
        canvas.Clear();

        WorldRenderer.ViewOffset(
            _scene.Camera, canvas.Width, canvas.Height, _centerBiasY, out var offsetX, out var offsetY);

        for (var i = 0; i < _count; i++)
        {
            if (!_scene.Objects.TryGetSlot(_balloons[i].Speaker, out var slot))
            {
                continue;
            }

            ref readonly var entry = ref _scene.Objects.EntryAt(slot);

            var characters = _balloons[i].Characters;
            Measure(characters, out var width, out var height);
            if (width <= FramePadding * 2 || height <= FramePadding * 2)
            {
                continue;
            }

            WorldRenderer.ObjectAnchor(
                in entry, _scene.Objects.WalkAt(slot), offsetX, offsetY, out var centerX, out var anchorY);

            var chant = _balloons[i].Mode == SpeechMode.Chant;

            if (!_scene.Objects.TryGetSpriteBoundsAt(slot, out var drawn))
            {
                drawn = SpriteBounds.Unit;
            }

            var above = anchorY + drawn.Top - height;
            var tailAtTop = above < 0;

            if (!tailAtTop && entry.Kind == WorldObjectKind.Human)
            {
                above = anchorY - HumanLift - height;
            }

            var top = (tailAtTop ? anchorY + drawn.Bottom : above) + (chant ? ChantDrop : 0);
            var box = new PixelRect(centerX - (width / 2), top, width, height);

            if (!chant)
            {
                DrawFrame(canvas, in box, tailAtTop);
            }

            var textLeft = box.X + FramePadding;
            var textRight = box.Right - FramePadding;

            if (chant && characters <= ChantCenterLimit)
            {
                textLeft = ((textLeft + textRight) / 2) - (characters * (CellWidth / 2));
            }

            _text[i].Draw(canvas, textLeft, box.Y + FramePadding);
        }
    }

    private void DrawFrame(Canvas canvas, in PixelRect box, bool tailAtTop)
    {
        var fill = Palette[FillPalette];
        var border = Palette[BorderPalette];

        var top = box.Y + CornerRadius;
        var left = box.X + 1;
        var bottom = box.Bottom - CornerRadius;
        var right = box.Right - 1;
        var middle = (left + right) / 2;

        canvas.TryBeginPixels(out var pixel);

        canvas.FillRect(
            PixelRect.FromEdges(left + 1, top + CornerRadius, right, bottom - CornerRadius + 1),
            fill,
            BlendMode.Copy);

        canvas.DrawLine(left, top + CornerRadius, left, bottom - CornerRadius, border);
        canvas.DrawLine(right, top + CornerRadius, right, bottom - CornerRadius, border);

        for (var step = 0; step < CornerSteps.Length; step += 2)
        {
            int down = CornerSteps[step];
            int across = CornerSteps[step + 1];
            var run = right - left - (across * 2) - 1;

            canvas.DrawHorizontalSpan(left + across + 1, top + down, run, fill);
            canvas.DrawHorizontalSpan(left + across + 1, bottom - down, run, fill);

            pixel.Set(left + across, top + down, border);
            pixel.Set(left + across, bottom - down, border);
            pixel.Set(right - across, top + down, border);
            pixel.Set(right - across, bottom - down, border);
        }

        var whole = tailAtTop ? bottom : top;
        var tailed = tailAtTop ? top : bottom;
        var reach = tailAtTop ? -1 : 1;

        canvas.DrawLine(left + CornerRadius, whole, right - CornerRadius, whole, border);
        canvas.DrawLine(left + CornerRadius, tailed, middle - 4, tailed, border);
        canvas.DrawLine(right - CornerRadius, tailed, middle + 4, tailed, border);

        for (var step = 0; step < CornerSteps.Length; step += 2)
        {
            int down = CornerSteps[step];
            int across = CornerSteps[step + 1];

            pixel.Set(middle - across + 1, tailed + (down * reach), border);
            pixel.Set(middle + across - 1, tailed + (down * reach), border);
        }

        canvas.DrawHorizontalSpan(middle - 3, tailed, 7, fill);
        canvas.DrawHorizontalSpan(middle - 1, tailed + reach, 3, fill);
        pixel.Set(middle, tailed + (reach * 2), fill);
    }

    private readonly struct Balloon(EntityId speaker, SpeechMode mode, uint expires, int characters)
    {
        public EntityId Speaker { get; } = speaker;

        public SpeechMode Mode { get; } = mode;

        public uint Expires { get; } = expires;

        public int Characters { get; } = characters;
    }
}
