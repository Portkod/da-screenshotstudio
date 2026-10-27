using DaScreenshotStudio.Assets.Gui;
using DaScreenshotStudio.Engine.Rendering;
using DaScreenshotStudio.Engine.Text;

namespace DaScreenshotStudio.Engine.Ui;

public static class GuiLayoutGeometry
{
    public static PixelRect Rect(GuiLayoutControl control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return PixelRect.FromEdges(control.Left, control.Top, control.Right, control.Bottom);
    }

    public static bool TryRect(GuiLayout? layout, scoped ReadOnlySpan<char> name, out PixelRect rect)
    {
        if (layout is not null && layout.TryFind(name, out var control))
        {
            rect = Rect(control);
            return true;
        }

        rect = PixelRect.Empty;
        return false;
    }

    public static PixelRect ControlRect(UiAssets assets, string layoutName, scoped ReadOnlySpan<char> control)
    {
        ArgumentNullException.ThrowIfNull(assets);

        return TryRect(assets.Layout(layoutName), control, out var rect) ? rect : PixelRect.Empty;
    }

    public static Pixmap ControlFrame(
        UiAssets assets, GuiLayout? layout, scoped ReadOnlySpan<char> control, int index = 0)
    {
        ArgumentNullException.ThrowIfNull(assets);

        return layout is not null && layout.TryFind(control, out var found) && found.Images.Count > index
            ? assets.LayoutFrame(found.Images[index].EntryName, found.Images[index].Frame)
            : default;
    }

    public static byte Color(GuiLayout? layout, scoped ReadOnlySpan<char> name, byte fallback) =>
        layout is not null && layout.TryFind(name, out var control) && control.Colors.Count > 0
            ? (byte)control.Colors[0]
            : fallback;

    public static int ParseInt(scoped ReadOnlySpan<byte> value)
    {
        var result = 0;
        var i = 0;
        var negative = i < value.Length && value[i] == (byte)'-';
        if (negative)
        {
            i++;
        }

        for (; i < value.Length && value[i] >= (byte)'0' && value[i] <= (byte)'9'; i++)
        {
            result = (result * 10) + (value[i] - '0');
        }

        return negative ? -result : result;
    }
}

public abstract class GuiChildPane : Pane
{
    protected GuiChildPane(in PixelRect bounds, PixelBlender blender)
        : base(in bounds, blender) => TargetBlend = BlendMode.Transparent;

    public Pixmap Background { get; set; }

    public PixelRect BackgroundOrigin { get; set; }

    protected void PaintBackground(Canvas canvas)
    {
        canvas.Clear();

        var background = Background;
        if (!background.IsEmpty)
        {
            canvas.BlitPixmap(
                BackgroundOrigin.X - Bounds.X, BackgroundOrigin.Y - Bounds.Y, in background, BlendMode.Copy);
        }
    }
}

public enum ControlKind
{
    Button = 0,

    RadioGroup = 1,

    Scrollable = 2,

    TextEdit = 3,

    StaticText = 4,

    OwnerDrawn = 5,

    Image = 6,

    ProgressBar = 0xB,
}

public interface IFocusableControl
{
    ControlKind Kind { get; }

    bool TakesEnter { get; }

    void DispatchAction()
    {
    }

    bool IsFocusable { get; set; }

    bool IsFocused { get; set; }

    bool IsEnabled => true;
}

public sealed class ImagePane : Pane
{
    public ImagePane(in PixelRect bounds, PixelBlender blender)
        : base(in bounds, blender) => TargetBlend = BlendMode.Transparent;

    public Pixmap Image
    {
        get;
        set
        {
            field = value;
            Invalidate();
        }
    }

    protected override void OnDrawContent(Canvas canvas)
    {
        canvas.Clear();

        var image = Image;
        if (!image.IsEmpty)
        {
            canvas.BlitPixmap(0, 0, in image, BlendMode.Transparent);
        }
    }
}

public sealed class SlotKeyRowPane : Pane
{
    public const string ArtEntryName = "_ninvn.spf";

    public const int OriginInsetX = 0x14;

    private Pixmap _art;
    private int _originX;
    private int _originY;

    public SlotKeyRowPane(in PixelRect bounds, PixelBlender blender)
        : base(in bounds, blender) => TargetBlend = BlendMode.Transparent;

    public void SetArt(in Pixmap art, int originX, int originY)
    {
        _art = art;
        _originX = originX;
        _originY = originY;
        Invalidate();
    }

    protected override void OnDrawContent(Canvas canvas)
    {
        canvas.Clear();

        if (!_art.IsEmpty)
        {
            canvas.BlitPixmap(_originX, _originY, in _art, BlendMode.Transparent);
        }
    }
}

public class GuiTextPane : GuiChildPane
{
    private readonly TextBlock _text;

    public GuiTextPane(in PixelRect bounds, PixelBlender blender, GameFont font, TextPalette palette, int maxLines = 1)
        : base(in bounds, blender)
    {
        _text = new TextBlock(font, palette, Math.Max(1, bounds.Width), maxLines)
        {
            Wraps = maxLines > 1,
        };
    }

    public TextBlock Text => _text;

    public TextAlignment Alignment
    {
        get => _text.Alignment;
        set
        {
            if (_text.Alignment != value)
            {
                _text.Alignment = value;
                Invalidate();
            }
        }
    }

    public int TopPadding { get; set; }

    public bool Wraps
    {
        get => _text.Wraps;
        set
        {
            if (_text.Wraps == value)
            {
                return;
            }

            _text.Wraps = value;
            Invalidate();
        }
    }

    public byte PaletteIndex
    {
        get => _text.DefaultPaletteIndex;
        set
        {
            if (_text.DefaultPaletteIndex == value)
            {
                return;
            }

            _text.DefaultPaletteIndex = value;
            Invalidate();
        }
    }

    public bool SetText(scoped ReadOnlySpan<byte> value)
    {
        if (!_text.SetText(value) && !_text.IsDirty)
        {
            return false;
        }

        if (_text.Wraps && _text.MaxLines < value.Length)
        {
            _text.MaxLines = value.Length;
        }

        Invalidate();
        return true;
    }

    public int WrapWidth
    {
        get;
        set
        {
            field = value;
            OnBoundsChanged();
        }
    }

    protected override void OnBoundsChanged()
    {
        _text.MaxWidth = Math.Max(1, WrapWidth > 0 ? WrapWidth : Bounds.Width);
        _text.Invalidate();
    }

    public bool IsScrolled { get; set; }

    protected override void OnDrawContent(Canvas canvas)
    {
        PaintBackground(canvas);

        var top = IsScrolled
            ? TopPadding
            : TextLayout.Top(TopPadding, canvas.Height, _text.LineCount * _text.LineHeight);

        _text.DrawIn(canvas, new PixelRect(0, top, canvas.Width, canvas.Height - top));
    }
}

public enum ImageButtonStates
{
    DialogPress,

    Retained,

    RetainedWithHover,
}

public sealed class ImageButtonPane : GuiChildPane, IFocusableControl
{
    private readonly Pixmap[] _frames = new Pixmap[GuiLayoutControl.ImageButtonStateCount];
    private int _hitWidth;
    private int _hitHeight;
    private int _state;
    private bool _pressed;

    public const byte FocusOutlineIndex = 0x1F;

    private const int HeldState = 1;

    private const int DisabledState = 2;

    public ImageButtonPane(in PixelRect bounds, PixelBlender blender)
        : base(in bounds, blender)
    {
        _hitWidth = bounds.Width;
        _hitHeight = bounds.Height;
    }

    public int ActionId { get; set; }

    public Action<ImageButtonPane>? Activated { get; set; }

    public Action<ImageButtonPane>? FocusRequested { get; set; }

    public Action<ImageButtonPane>? PointerActionCompleted { get; set; }

    public bool DrawsFocusOutline { get; set; }

    public TextPalette? Palette { get; set; }

    public int Frame => _state;

    public bool IsFocusable { get; set; } = true;

    ControlKind IFocusableControl.Kind => ControlKind.Button;

    bool IFocusableControl.TakesEnter => true;

    void IFocusableControl.DispatchAction() => Activate(InputModifiers.None);

    bool IFocusableControl.IsEnabled => Enabled;

    public bool IsFocused
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
    }

    public InputModifiers ActivationModifiers { get; private set; }

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

            if (States == ImageButtonStates.DialogPress)
            {
                _pressed = false;
                SetState(value ? Resting(false) : DisabledState);
            }

            Invalidate();
        }
    } = true;

    public bool IsToggle { get; set; }

    public ImageButtonStates States { get; set; }

    public bool IsChecked
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            _state = value ? 1 : 0;
            Invalidate();
        }
    }

    public void SetHitBounds(in PixelRect bounds)
    {
        _hitWidth = bounds.Width;
        _hitHeight = bounds.Height;
        SetBounds(in bounds);
    }

    public void SetFrames(Pixmap normal, Pixmap pressed, Pixmap disabled)
    {
        _frames[0] = normal;
        _frames[1] = pressed;
        _frames[2] = disabled;

        var width = _hitWidth;
        var height = _hitHeight;

        foreach (var frame in _frames)
        {
            width = Math.Max(width, frame.Width);
            height = Math.Max(height, frame.Height);
        }

        SetBounds(new PixelRect(Bounds.X, Bounds.Y, width, height));
        Invalidate();
    }

    protected override bool OnPointer(in PointerEvent pointer)
    {
        if (!Enabled)
        {
            return false;
        }

        var inside = pointer.X >= 0 && pointer.Y >= 0 && pointer.X < _hitWidth && pointer.Y < _hitHeight;

        var held = States == ImageButtonStates.DialogPress;

        switch (pointer.Type)
        {
            case UiEventType.PointerMove:
                SetState(held && _pressed && inside ? HeldState : Resting(inside));
                return false;

            case UiEventType.LeftDown when inside:
                if (!held)
                {
                    Activate(pointer.Modifiers);
                    return true;
                }

                if (IsFocusable)
                {
                    FocusRequested?.Invoke(this);
                }

                _pressed = true;
                SetState(HeldState);
                return true;

            case UiEventType.LeftUp when _pressed:
                _pressed = false;
                SetState(Resting(inside));

                if (inside)
                {
                    Activate(pointer.Modifiers);
                    PointerActionCompleted?.Invoke(this);
                }

                return true;

            default:
                return false;
        }
    }

    protected override void OnDrawContent(Canvas canvas)
    {
        canvas.Clear();

        var frame = _frames[Math.Clamp(_state, 0, _frames.Length - 1)];
        if (!frame.IsEmpty)
        {
            canvas.BlitPixmap(0, 0, in frame, BlendMode.Transparent);
        }

        DrawFocusOutline(canvas);
        DrawFlashOutline(canvas);
    }

    public int FlashOutlineIndex
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
    } = -1;

    private void DrawFlashOutline(Canvas canvas)
    {
        if (FlashOutlineIndex < 0 || Palette is not { } palette)
        {
            return;
        }

        canvas.SetDrawColor(palette[FlashOutlineIndex]);
        canvas.DrawRectOutline(new PixelRect(0, 0, _hitWidth, _hitHeight));
    }

    private void DrawFocusOutline(Canvas canvas)
    {
        if (!DrawsFocusOutline || !IsFocused || !Enabled || Palette is not { } palette)
        {
            return;
        }

        canvas.SetDrawColor(palette[FocusOutlineIndex]);
        canvas.DrawRectOutline(new PixelRect(0, 0, _hitWidth, _hitHeight));
    }

    public void ResetState()
    {
        _pressed = false;
        IsFocused = false;
        SetState(Resting(false));
    }

    private void Activate(InputModifiers modifiers)
    {
        ActivationModifiers = modifiers;

        if (IsToggle)
        {
            IsChecked = !IsChecked;
        }

        Activated?.Invoke(this);
    }

    private int Resting(bool inside) => States switch
    {
        ImageButtonStates.Retained => IsChecked ? 1 : 0,
        ImageButtonStates.RetainedWithHover => IsChecked ? 1 : inside ? 2 : 0,
        _ => !Enabled ? DisabledState : IsChecked ? HeldState : 0,
    };

    private void SetState(int state)
    {
        if (_state == state)
        {
            return;
        }

        _state = state;
        Invalidate();
    }
}

public sealed class DescriptionPane : Pane
{
    public const int CharacterWidth = 6;

    public const int LineHeight = 0x0C;

    public const int Padding = 4;

    public const int PenInset = 2;

    private readonly TextBlock _text;
    private readonly ushort _fill;
    private readonly ushort _ink;
    private object? _owner;

    public DescriptionPane(PixelBlender blender, GameFont font, TextPalette palette)
        : base(new PixelRect(0, 0, GuiBackPane.ScreenWidth, LineHeight + Padding), blender)
    {
        ArgumentNullException.ThrowIfNull(blender);
        ArgumentNullException.ThrowIfNull(palette);

        TargetBlend = BlendMode.Shade;

        _fill = palette[TextPalette.ShadowIndex];
        _ink = palette[TextPalette.WhiteIndex];
        blender.ShadeKey = _fill;

        _text = new TextBlock(font, palette, GuiBackPane.ScreenWidth, 1)
        {
            Wraps = false,
            DefaultPaletteIndex = TextPalette.WhiteIndex,
        };

        Visible = false;
    }

    public static PixelRect BoxFor(int x, int y, scoped ReadOnlySpan<byte> text) =>
        new(x, y, (text.Length * CharacterWidth) + Padding, LineHeight + Padding);

    public object? Owner => _owner;

    public void Show(object? owner, Pane on, int x, int y, scoped ReadOnlySpan<byte> text)
    {
        ArgumentNullException.ThrowIfNull(on);

        if (text.IsEmpty)
        {
            if (owner is null)
            {
                HideAny();
            }
            else
            {
                Hide(owner);
            }

            return;
        }

        var clamp = on.Content.Bounds;
        var box = BoxFor(x, y, text);

        if (box.Right > clamp.Right)
        {
            box = box.Translate(clamp.Right - box.Right, 0);
        }

        if (box.Bottom > clamp.Bottom)
        {
            box = box.Translate(0, clamp.Bottom - box.Bottom);
        }

        var changed = _text.SetText(text) || !ReferenceEquals(_owner, owner) || Bounds != box || !Visible;

        if (!ReferenceEquals(Parent, on))
        {
            Parent?.Remove(this);
            on.Add(this);
            changed = true;
        }

        _owner = owner;
        SetBounds(in box);
        Visible = true;

        if (changed)
        {
            Invalidate();
        }
    }

    public void Hide(object owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        if (!ReferenceEquals(_owner, owner))
        {
            return;
        }

        HideAny();
    }

    public void HideAny()
    {
        _owner = null;
        Visible = false;
        Parent?.Remove(this);
    }

    public ReadOnlySpan<byte> Label => _text.Value;

    protected override bool OnKey(in KeyEvent key)
    {
        HideAny();
        return false;
    }

    protected override void OnDrawContent(Canvas canvas)
    {
        var box = new PixelRect(0, 0, canvas.Width, canvas.Height);

        canvas.FillRect(in box, _fill, BlendMode.Copy);
        canvas.SetDrawColor(_ink);
        canvas.DrawRectOutline(in box);

        _text.Draw(canvas, PenInset, PenInset);
    }
}

public sealed class OrbPane : GuiChildPane
{
    public const int LabelShadowOffset = 1;

    private Pixmap[] _frames;
    private TextBlock? _shadow;
    private TextBlock? _label;
    private PixelRect _labelBox;
    private int _frame;

    public OrbPane(in PixelRect bounds, PixelBlender blender, Pixmap[] frames)
        : base(in bounds, blender)
    {
        ArgumentNullException.ThrowIfNull(frames);
        _frames = frames;
    }

    public int FrameCount => _frames.Length;

    public void SetFrames(Pixmap[] frames)
    {
        ArgumentNullException.ThrowIfNull(frames);

        _frames = frames;
        _frame = Math.Clamp(_frame, 0, Math.Max(0, frames.Length - 1));
        Invalidate();
    }

    public void SetLabelBox(in PixelRect box, GameFont font, TextPalette palette)
    {
        _labelBox = box;

        _shadow = new TextBlock(font, palette, Math.Max(1, box.Width), 1)
        {
            Wraps = false,
            Alignment = TextAlignment.Center,
            DefaultPaletteIndex = TextPalette.ShadowIndex,
        };

        _label = new TextBlock(font, palette, Math.Max(1, box.Width), 1)
        {
            Wraps = false,
            Alignment = TextAlignment.Center,
            DefaultPaletteIndex = TextPalette.WhiteIndex,
        };

        Invalidate();
    }

    public void ClearLabel()
    {
        if (_label is null && _shadow is null)
        {
            return;
        }

        _label = null;
        _shadow = null;
        _labelBox = default;
        Invalidate();
    }

    public void SetLabel(scoped ReadOnlySpan<byte> text)
    {
        if (_label is null)
        {
            return;
        }

        _shadow?.SetText(text);

        if (_label.SetText(text))
        {
            Invalidate();
        }
    }

    public void SetRatio(uint current, uint maximum)
    {
        var frame = 0;
        if (_frames.Length > 1 && maximum > 0 && current > 0)
        {
            var last = _frames.Length - 1;
            frame = (int)Math.Min((ulong)last, ((ulong)current * (ulong)last / maximum) + 1);
            frame = Math.Clamp(frame, 1, last);
        }

        if (_frame == frame)
        {
            return;
        }

        _frame = frame;
        Invalidate();
    }

    protected override void OnDrawContent(Canvas canvas)
    {
        PaintBackground(canvas);

        if ((uint)_frame < (uint)_frames.Length && !_frames[_frame].IsEmpty)
        {
            canvas.BlitPixmap(0, 0, _frames[_frame], BlendMode.Transparent);
        }

        _shadow?.DrawIn(canvas, in _labelBox);
        _label?.DrawIn(canvas, _labelBox.Translate(-LabelShadowOffset, -LabelShadowOffset));
    }
}
