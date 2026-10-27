using DaScreenshotStudio.Engine.App;
using DaScreenshotStudio.Engine.Rendering;

namespace DaScreenshotStudio.Engine.Ui;

public enum ScrollBarPart
{
    StartArrow = 0,

    TroughBefore = 1,

    Thumb = 2,

    TroughAfter = 3,

    EndArrow = 4,
}

public sealed class ScrollBarPane : Pane
{
    public Action? FocusRequested { get; set; }

    public const int FrameCount = 10;

    public const string LayoutName = "lscroll.txt";

    public const int MaximumLimit = 30000;

    public const int HorizontalStartFrame = 0;

    public const int HorizontalEndFrame = 2;

    public const int VerticalStartFrame = 4;

    public const int VerticalEndFrame = 6;

    public const int ThumbFrame = 8;

    public const int TroughFrame = 9;

    public const int NoPart = -1;

    public const uint RepeatDelayMs = 0xC8;

    public const uint RepeatIntervalMs = 0x32;

    private readonly Pixmap[] _frames;

    private int _heldPart = NoPart;

    private int _grabX;
    private int _grabY;

    private int _pointerX;
    private int _pointerY;

    private uint _now;

    private uint _repeatDue;

    public static Pixmap[]? LoadFrames(UiAssets assets)
    {
        ArgumentNullException.ThrowIfNull(assets);

        if (assets.Layout(LayoutName) is not { ControlCount: > 0 } layout || layout[0].Images.Count == 0)
        {
            return null;
        }

        var entry = layout[0].Images[0].EntryName;
        var frames = new Pixmap[FrameCount];

        for (var i = 0; i < frames.Length; i++)
        {
            frames[i] = assets.PaletteFrame(entry, i);
        }

        return frames[0].IsEmpty ? null : frames;
    }

    public static int ThicknessOf(ReadOnlySpan<Pixmap> frames) => frames.IsEmpty ? 0 : frames[0].Width;

    public ScrollBarPane(in PixelRect bounds, PixelBlender blender, Pixmap[] frames, bool horizontal)
        : base(in bounds, blender)
    {
        ArgumentNullException.ThrowIfNull(frames);
        ArgumentOutOfRangeException.ThrowIfNotEqual(frames.Length, FrameCount);

        _frames = frames;
        Horizontal = horizontal;
        TargetBlend = BlendMode.Transparent;
    }

    public bool Horizontal { get; }

    public int Thickness => _frames[0].Width;

    public bool Enabled
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            Invalidate();
        }
    } = true;

    public int Maximum
    {
        get;
        set
        {
            value = Math.Clamp(value, 0, MaximumLimit);
            if (field == value)
            {
                return;
            }

            field = value;

            Position = Math.Min(Position, value);
            Invalidate();
        }
    }

    public int Position
    {
        get;
        set
        {
            value = Math.Clamp(value, 0, Maximum);
            if (field == value)
            {
                return;
            }

            field = value;
            Invalidate();
        }
    }

    public int PageStep
    {
        get;
        set => field = Math.Max(1, value);
    } = 1;

    public Action? Scrolled { get; set; }

    public int PressedPart
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            Invalidate();
        }
    } = NoPart;

    public bool HasThumb => Enabled && Maximum > 0;

    public PixelRect PartRect(ScrollBarPart part)
    {
        var size = Thickness;
        var span = Horizontal ? Bounds.Width : Bounds.Height;
        var cross = Horizontal ? Bounds.Height : Bounds.Width;

        var (start, length) = Extent(part, size, span);
        return length <= 0
            ? PixelRect.Empty
            : Horizontal ? new PixelRect(start, 0, length, cross) : new PixelRect(0, start, cross, length);
    }

    private (int Start, int Length) Extent(ScrollBarPart part, int size, int span)
    {
        switch (part)
        {
            case ScrollBarPart.StartArrow:
                return (0, size);

            case ScrollBarPart.EndArrow:
                return (span - size, size);
        }

        var trackStart = size;
        var trackEnd = span - size;

        if (!HasThumb)
        {
            return part == ScrollBarPart.Thumb ? (trackStart, trackEnd - trackStart) : (0, 0);
        }

        var thumbStart = trackStart + ((trackEnd - size - trackStart) * Position / Maximum);

        return part switch
        {
            ScrollBarPart.Thumb => (thumbStart, size),
            ScrollBarPart.TroughBefore => (trackStart, thumbStart - trackStart),
            _ => (thumbStart + size, trackEnd - thumbStart - size),
        };
    }

    public int HitTest(int x, int y)
    {
        for (var part = 0; part <= (int)ScrollBarPart.EndArrow; part++)
        {
            var rect = PartRect((ScrollBarPart)part);
            if (!rect.IsEmpty && x >= rect.X && x < rect.Right && y >= rect.Y && y < rect.Bottom)
            {
                return part;
            }
        }

        return NoPart;
    }

    protected override bool OnPointer(in PointerEvent pointer)
    {
        if (pointer.Type == UiEventType.LeftDown && Contains(in pointer))
        {
            FocusRequested?.Invoke();
        }

        switch (pointer.Type)
        {
            case UiEventType.LeftDown:
                if (_heldPart != NoPart || !HasThumb || !Contains(in pointer))
                {
                    return false;
                }

                var part = HitTest(pointer.X, pointer.Y);
                if (part == NoPart)
                {
                    return false;
                }

                BeginPress(part, pointer.X, pointer.Y);
                return true;

            case UiEventType.PointerMove:
                if (_heldPart == NoPart)
                {
                    return false;
                }

                _pointerX = pointer.X;
                _pointerY = pointer.Y;
                return true;

            case UiEventType.LeftUp:
                if (_heldPart == NoPart)
                {
                    return false;
                }

                _heldPart = NoPart;
                PressedPart = NoPart;
                return true;

            default:
                return false;
        }
    }

    private void BeginPress(int part, int x, int y)
    {
        _heldPart = part;
        _pointerX = x;
        _pointerY = y;

        if (part == (int)ScrollBarPart.Thumb)
        {
            var thumb = PartRect(ScrollBarPart.Thumb);
            _grabX = x - thumb.X;
            _grabY = y - thumb.Y;
        }

        Act(x, y);
        _repeatDue = unchecked(_now + RepeatDelayMs);
    }

    protected override void OnTick(uint now) => Tick(now);

    public void Tick(uint now)
    {
        _now = now;

        if (_heldPart == NoPart || !GameClock.IsDue(now, _repeatDue))
        {
            return;
        }

        Act(_pointerX, _pointerY);
        _repeatDue = unchecked(now + RepeatIntervalMs);
    }

    private void Act(int x, int y)
    {
        if (_heldPart == (int)ScrollBarPart.Thumb)
        {
            DragTo(x, y);
            return;
        }

        var over = HitTest(x, y);
        if (over != _heldPart)
        {
            PressedPart = NoPart;
            return;
        }

        PressedPart = _heldPart;

        var moved = (ScrollBarPart)_heldPart switch
        {
            ScrollBarPart.StartArrow => Position - 1,
            ScrollBarPart.TroughBefore => Position - PageStep,
            ScrollBarPart.TroughAfter => Position + PageStep,
            _ => Position + 1,
        };

        MoveTo(moved);
    }

    private void DragTo(int x, int y)
    {
        var size = Thickness;
        var span = Horizontal ? Bounds.Width : Bounds.Height;
        var origin = Horizontal ? x - _grabX : y - _grabY;

        var trackStart = size;
        var travel = span - size - size - trackStart;
        if (travel <= 0)
        {
            return;
        }

        origin = Math.Clamp(origin, trackStart, trackStart + travel);
        MoveTo((origin - trackStart) * Maximum / travel);
    }

    private void MoveTo(int position)
    {
        var clamped = Math.Clamp(position, 0, Maximum);
        if (clamped == Position)
        {
            return;
        }

        Position = clamped;
        Scrolled?.Invoke();
    }

    private int NormalArrowFrame(ScrollBarPart part) => part == ScrollBarPart.StartArrow
        ? Horizontal ? HorizontalStartFrame : VerticalStartFrame
        : Horizontal ? HorizontalEndFrame : VerticalEndFrame;

    private int PressedArrowFrame(ScrollBarPart part) => NormalArrowFrame(part) + 1;

    private int ArrowFrame(ScrollBarPart part) =>
        PressedPart == (int)part ? PressedArrowFrame(part) : NormalArrowFrame(part);

    protected override void OnDrawContent(Canvas canvas)
    {
        canvas.Clear();

        if (!HasThumb)
        {
            var whole = canvas.Bounds;
            canvas.TilePixmap(in whole, _frames[TroughFrame], BlendMode.Copy);
            Blit(canvas, PartRect(ScrollBarPart.StartArrow), PressedArrowFrame(ScrollBarPart.StartArrow));
            Blit(canvas, PartRect(ScrollBarPart.EndArrow), PressedArrowFrame(ScrollBarPart.EndArrow));
            return;
        }

        Blit(canvas, PartRect(ScrollBarPart.StartArrow), ArrowFrame(ScrollBarPart.StartArrow));
        Blit(canvas, PartRect(ScrollBarPart.EndArrow), ArrowFrame(ScrollBarPart.EndArrow));

        Tile(canvas, PartRect(ScrollBarPart.TroughBefore));
        Tile(canvas, PartRect(ScrollBarPart.TroughAfter));

        Blit(canvas, PartRect(ScrollBarPart.Thumb), ThumbFrame);
    }

    private void Blit(Canvas canvas, in PixelRect rect, int frame)
    {
        if (!rect.IsEmpty && !_frames[frame].IsEmpty)
        {
            canvas.BlitPixmap(rect.X, rect.Y, _frames[frame], BlendMode.Copy);
        }
    }

    private void Tile(Canvas canvas, in PixelRect rect)
    {
        if (!rect.IsEmpty)
        {
            canvas.TilePixmap(in rect, _frames[TroughFrame], BlendMode.Copy);
        }
    }
}
