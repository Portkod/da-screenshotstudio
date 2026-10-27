using DaScreenshotStudio.Engine.Rendering;
using DaScreenshotStudio.Engine.Text;

namespace DaScreenshotStudio.Engine.Ui;

public sealed class TextEditPane : GuiChildPane, IFocusableControl
{
    public const byte MaskCharacter = (byte)'*';

    public const byte CarriageReturn = 0x0D;

    public const int MaxValueBytes = 0x7530;

    public const int DefaultMaxLength = MaxValueBytes;

    public const int GrowthStep = 512;

    public const int TextInset = 2;

    public const int CaretWidth = 2;

    public const int CaretHeight = 0xC;

    private readonly TextBlock _text;
    private readonly TextPalette _palette;
    private readonly ScrollBarPane? _verticalBar;
    private readonly ScrollBarPane? _horizontalBar;

    private byte[] _buffer = new byte[GrowthStep];
    private byte[]? _masked;
    private int _length;
    private TextCaret _caret;
    private TextPointerState _pointer;

    private int _contentTop = TextInset;

    public TextEditPane(
        in PixelRect bounds,
        PixelBlender blender,
        GameFont font,
        TextPalette palette,
        Pixmap[]? scrollFrames = null,
        bool verticalBar = false,
        bool horizontalBar = false)
        : base(in bounds, blender)
    {
        ArgumentNullException.ThrowIfNull(palette);

        _palette = palette;

        if (scrollFrames is not null)
        {
            _verticalBar = verticalBar ? MakeBar(scrollFrames, blender, horizontal: false) : null;
            _horizontalBar = horizontalBar ? MakeBar(scrollFrames, blender, horizontal: true) : null;
            PlaceBars();
        }

        _text = new TextBlock(font, palette, LayoutWidth, DefaultMaxLength)
        {
            Wraps = true,
        };
    }

    public PixelRect TextRect => new(
        TextInset,
        TextInset,
        Math.Max(0, Bounds.Width - (TextInset * 2) - (_verticalBar?.Thickness ?? 0)),
        Math.Max(0, Bounds.Height - (TextInset * 2) - (_horizontalBar?.Thickness ?? 0)));

    public PixelRect ContentRect =>
        new(TextRect.X, _contentTop, LayoutWidth, _text.LineCount * _text.LineHeight);

    public int ScrollMaximum
    {
        get
        {
            var overflow = (_text.LineCount * _text.LineHeight) - TextRect.Height;
            return overflow <= 0 ? 0 : ((overflow - 1) / _text.LineHeight) + 1;
        }
    }

    public int VerticalPageStep => Math.Max(1, (TextRect.Height / _text.LineHeight) - 1);

    public int ScrollPosition
    {
        get
        {
            var gap = TextRect.Y - _contentTop;
            return gap <= 0 ? 0 : ((gap - 1) / _text.LineHeight) + 1;
        }

        set
        {
            var clamped = Math.Clamp(value, 0, ScrollMaximum);
            var moved = (ScrollPosition - clamped) * _text.LineHeight;
            if (moved == 0)
            {
                return;
            }

            _contentTop += moved;
            if (_verticalBar is { } bar)
            {
                bar.Position = clamped;
            }

            Invalidate();
        }
    }

    public byte PaletteIndex
    {
        get => _text.DefaultPaletteIndex;
        set
        {
            _text.DefaultPaletteIndex = value;
            Invalidate();
        }
    }

    public byte BackgroundIndex { get; set; }

    public bool IsTransparent { get; set; } = true;

    public bool SelectsAllOnFocus { get; set; }

    public TextCaret Caret => _caret;

    public int CaretPosition => _caret.Position;

    public bool HasSelection => _caret.HasRun;

    public int ActionId { get; set; }

    public int MaxLength
    {
        get;
        set
        {
            field = Math.Clamp(value, 1, MaxValueBytes);
            if (_text.MaxLines < field)
            {
                _text.MaxLines = field;
            }

            if (_length > field)
            {
                TrimToLimit(field);
            }
        }
    } = DefaultMaxLength;

    public bool IsMasked { get; set; }

    public bool InterpretsMarkup
    {
        get => _text.InterpretsMarkup;
        set
        {
            if (_text.InterpretsMarkup == value)
            {
                return;
            }

            _text.InterpretsMarkup = value;
            Invalidate();
        }
    }

    public bool AcceptsDigitsOnly { get; set; }

    public bool IsReadOnly { get; set; }

    public Action<TextEditPane>? FocusRequested { get; set; }

    public Action<bool>? TabRequested { get; set; }

    public bool IsFocusable { get; set; } = true;

    ControlKind IFocusableControl.Kind => ControlKind.TextEdit;

    public bool TakesEnter { get; set; }

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
            RefreshSelection();
            Invalidate();
        }
    }

    public ReadOnlySpan<byte> Value => _buffer.AsSpan(0, _length);

    public int Length => _length;

    public bool IsEmpty => _length == 0;

    public Action? Changed { get; set; }

    public void SetValue(scoped ReadOnlySpan<byte> value)
    {
        _length = Math.Min(DigitPrefix(value), MaxLength);
        EnsureCapacity(_length);
        value[.._length].CopyTo(_buffer);
        _caret.Collapse(_length, _length);
        AfterValueChanged();

        RefreshSelection();
    }

    public void Clear()
    {
        if (_length == 0)
        {
            return;
        }

        _buffer.AsSpan(0, _length).Clear();
        _length = 0;
        _caret.Collapse(0, 0);
        AfterValueChanged();
    }

    public void ClearQuietly()
    {
        if (_length == 0)
        {
            return;
        }

        _buffer.AsSpan(0, _length).Clear();
        _length = 0;
        _caret.Collapse(0, 0);
        ComposeText();
        RefreshScrollState();
    }

    protected override bool OnKey(in KeyEvent key)
    {
        if (!IsFocused)
        {
            return false;
        }

        switch (key.Type)
        {
            case UiEventType.KeyDown when TextEditing.IsArrow(key.Key):
                Navigate(key.Key, key.Modifiers);
                return true;

            case UiEventType.Character when key.Character >= 0x20 && key.Character != 0x7F:
                if (IsReadOnly || TextEditing.IsModified(key.Modifiers) || !Accepts(key.Character))
                {
                    return true;
                }

                Insert(key.Character);
                return true;

            case UiEventType.KeyDown when key.Key == UiKeys.Backspace:
                if (IsReadOnly || TextEditing.IsModified(key.Modifiers))
                {
                    return true;
                }

                Backspace();
                return true;

            case UiEventType.KeyDown when key.Key == UiKeys.Delete:
                if (IsReadOnly || TextEditing.IsModified(key.Modifiers))
                {
                    return true;
                }

                DeleteForward();
                return true;

            case UiEventType.KeyDown when key.Key == UiKeys.Tab:
                TabRequested?.Invoke((key.Modifiers & InputModifiers.Shift) != 0);
                return true;

            case UiEventType.KeyDown when key.Key == UiKeys.Enter:
                if (!TakesEnter)
                {
                    return false;
                }

                if (!IsReadOnly && !TextEditing.IsModified(key.Modifiers))
                {
                    Insert(CarriageReturn);
                }

                return true;

            case UiEventType.KeyDown when key.Key == UiKeys.Escape:
                return false;

            case UiEventType.KeyDown when key.Key == UiKeys.F12:
                return false;

            default:
                return true;
        }
    }

    protected override ScrollBarPane? WheelScrollBar(int index) => _verticalBar;

    protected override bool OnPointer(in PointerEvent pointer)
    {
        if (CaretPointer(in pointer))
        {
            return true;
        }

        if (pointer.Type != UiEventType.LeftDown || !Contains(in pointer))
        {
            return false;
        }

        if (IsFocusable)
        {
            FocusRequested?.Invoke(this);
        }

        return true;
    }

    private bool CaretPointer(in PointerEvent pointer)
    {
        if (!IsFocused || IsReadOnly)
        {
            return false;
        }

        ComposeText();

        var view = TextRect;
        var column = pointer.X - view.X;
        var row = pointer.Y - _contentTop;

        switch (pointer.Type)
        {
            case UiEventType.LeftDown:
            case UiEventType.LeftDoubleClick:
                if (pointer.X < view.X || pointer.Y < view.Y
                    || pointer.X >= view.Right || pointer.Y >= view.Bottom)
                {
                    return false;
                }

                TextPointer.Press(
                    ref _caret, ref _pointer, _text, Value, column, row, pointer.Modifiers, pointer.Time);
                AfterCaretMoved();
                return true;

            case UiEventType.PointerMove when _pointer.IsDragging:
                TextPointer.Drag(ref _caret, ref _pointer, _text, Value, column, row);
                AfterCaretMoved();
                return true;

            case UiEventType.LeftUp when _pointer.IsDragging:
                TextPointer.Release(ref _pointer);
                return true;

            default:
                return false;
        }
    }

    private void AfterCaretMoved()
    {
        ScrollCaretIntoView();
        Invalidate();
    }

    protected override void OnBoundsChanged()
    {
        _text.MaxWidth = LayoutWidth;
        PlaceBars();
        PlaceContent();
        RefreshScrollState();
    }

    private void RefreshSelection()
    {
        if (IsFocused && SelectsAllOnFocus)
        {
            _caret.Select(0, _length, _length);
            return;
        }

        _caret.Collapse(_caret.Position, _length);
    }

    private bool Accepts(byte character) =>
        !AcceptsDigitsOnly || (character >= (byte)'0' && character <= (byte)'9');

    private int DigitPrefix(scoped ReadOnlySpan<byte> value) =>
        AcceptsDigitsOnly ? TextEditing.DigitPrefix(value) : value.Length;

    private void Navigate(int key, InputModifiers modifiers)
    {
        ComposeText();
        TextEditing.Navigate(ref _caret, _text, Value, key, modifiers, TextRect, _contentTop);
        ScrollCaretIntoView();
        Invalidate();
    }

    private void TrimToLimit(int limit)
    {
        var over = _length - limit;
        var codePage = _text.Font.CodePage;
        var cut = 0;

        while (cut < over && cut < _length)
        {
            cut += codePage.IsLeadByte(_buffer[cut]) && cut + 1 < _length ? 2 : 1;
        }

        if (cut <= 0)
        {
            return;
        }

        _buffer.AsSpan(cut, _length - cut).CopyTo(_buffer);
        _length -= cut;
        _buffer.AsSpan(_length).Clear();
        _caret.Clamp(_length);
        AfterValueChanged();
    }

    private void EnsureCapacity(int bytes)
    {
        var wanted = Math.Min(MaxLength, bytes);
        if (_buffer.Length >= wanted)
        {
            return;
        }

        Array.Resize(ref _buffer, Math.Min(MaxLength, Math.Max(wanted, _buffer.Length * 2)));
        _masked = null;
    }

    private void Insert(byte value)
    {
        EnsureCapacity(_length + 2);
        if (TextEditing.Insert(_buffer, ref _length, ref _caret, value, MaxLength))
        {
            AfterValueChanged();
        }
    }

    private void Backspace()
    {
        if (TextEditing.Backspace(_buffer, ref _length, ref _caret))
        {
            AfterValueChanged();
        }
    }

    private void DeleteForward()
    {
        if (TextEditing.DeleteForward(_buffer, ref _length, ref _caret))
        {
            AfterValueChanged();
        }
    }

    protected override void OnDrawContent(Canvas canvas)
    {
        PaintBackground(canvas);
        FillBox(canvas);
        ComposeText();

        var view = TextRect;
        var top = _contentTop;

        var previous = canvas.Clip;
        canvas.SetClip(in view);

        if (_caret.HasRun)
        {
            DrawSelection(canvas, in view, top);
        }
        else
        {
            _text.Draw(canvas, view.X, top);

            if (IsFocused)
            {
                DrawCaret(canvas, top);
            }
        }

        canvas.SetClip(previous);
    }

    private int LayoutWidth => Math.Max(
        1, Bounds.Width - (_verticalBar is { } bar ? bar.Thickness + (TextInset * 2) : 0));

    private ScrollBarPane? MakeBar(Pixmap[] frames, PixelBlender blender, bool horizontal)
    {
        var bar = new ScrollBarPane(Bounds, blender, frames, horizontal);

        bar.FocusRequested = () => FocusRequested?.Invoke(this);
        bar.Scrolled = () => ScrollPosition = bar.Position;

        Add(bar);
        return bar;
    }

    private void PlaceBars()
    {
        var vertical = _verticalBar?.Thickness ?? 0;
        var horizontal = _horizontalBar?.Thickness ?? 0;

        _verticalBar?.SetBounds(new PixelRect(
            Bounds.Width - TextInset - vertical,
            TextInset,
            vertical,
            Math.Max(0, Bounds.Height - (TextInset * 2) - horizontal)));

        _horizontalBar?.SetBounds(new PixelRect(
            TextInset,
            Bounds.Height - TextInset - horizontal,
            Math.Max(0, Bounds.Width - (TextInset * 2) - vertical),
            horizontal));
    }

    private void RefreshScrollState()
    {
        if (_horizontalBar is { } across)
        {
            across.Maximum = 0;
            across.Position = 0;
        }

        if (_verticalBar is not { } bar)
        {
            return;
        }

        var maximum = ScrollMaximum;
        var position = ScrollPosition;
        var moved = bar.Maximum != maximum || bar.Position != position;

        bar.Maximum = maximum;
        bar.Position = position;
        bar.PageStep = VerticalPageStep;

        if (moved)
        {
            PlaceContent();
        }
    }

    private void PlaceContent()
    {
        var view = TextRect;
        var height = _text.LineCount * _text.LineHeight;

        if (height <= view.Height)
        {
            _contentTop = view.Y;
            return;
        }

        var top = view.Y - (ScrollPosition * _text.LineHeight);
        _contentTop = top + height < view.Bottom ? view.Bottom - height : top;
    }

    private void ScrollCaretIntoView()
    {
        var view = TextRect;
        var height = _text.LineHeight;
        var content = ContentRect;
        var caret = _contentTop + (_text.LineOf(_caret.Position) * height);

        var gap = 0;
        if (view.Y > caret)
        {
            gap = view.Y - caret;
        }

        if (view.Bottom < caret + CaretHeight)
        {
            gap = view.Bottom - (caret + CaretHeight);
        }

        if (gap == 0)
        {
            return;
        }

        gap = Math.Max(gap, view.Bottom - content.Bottom);
        gap = Math.Min(gap, view.Y - content.Y);
        if (gap == 0)
        {
            return;
        }

        var rows = gap < 0 ? 1 - ((gap + 1) / height) : -(((gap - 1) / height) + 1);

        _contentTop -= rows * height;
        if (_verticalBar is { } bar)
        {
            bar.Position = Math.Clamp(ScrollPosition, 0, ScrollMaximum);
        }

        Invalidate();
    }

    private void ComposeText()
    {
        if (!IsMasked)
        {
            _text.SetText(Value);
            return;
        }

        _masked ??= new byte[_buffer.Length];
        _masked.AsSpan(0, _length).Fill(MaskCharacter);
        _text.SetText(_masked.AsSpan(0, _length));
    }

    private void AfterValueChanged()
    {
        ComposeText();
        RefreshScrollState();
        ScrollCaretIntoView();
        Invalidate();        Changed?.Invoke();
    }

    private void FillBox(Canvas canvas)
    {
        if (IsTransparent)
        {
            return;
        }

        var width = canvas.Width - (TextInset * 2);
        var height = canvas.Height - (TextInset * 2);
        if (width <= 0 || height <= 0)
        {
            return;
        }

        canvas.FillRect(
            new PixelRect(TextInset, TextInset, width, height),
            _palette[BackgroundIndex],
            BlendMode.Copy);
    }

    private void DrawSelection(Canvas canvas, in PixelRect view, int top)
    {
        var lastLine = Math.Max(0, _text.LineCount - 1);
        var height = Math.Min(_text.LineHeight, Math.Max(0, view.Height));

        for (var line = 0; line <= lastLine; line++)
        {
            var start = _text.LineStart(line);
            var end = start + _text.LineLength(line);
            if (_caret.High <= start || _caret.Low >= end)
            {
                continue;
            }

            var from = _text.PenOn(line, _caret.Low);
            var to = _text.PenOn(line, _caret.High);
            if (to <= from)
            {
                continue;
            }

            canvas.FillRect(
                new PixelRect(view.X + from, top + (line * _text.LineHeight), to - from, height),
                _palette[PaletteIndex],
                BlendMode.Copy);
        }

        _text.DrawWithDefaultColor(canvas, view.X, top, _palette[BackgroundIndex]);
    }

    private void DrawCaret(Canvas canvas, int top)
    {
        var view = TextRect;
        var line = _text.LineOf(_caret.Position);
        var pen = view.X + _text.PenOn(line, _caret.Position);
        var left = Math.Clamp(pen - 1, view.X, Math.Max(view.X, view.Right - CaretWidth));
        var caretTop = top + (line * _text.LineHeight);
        var height = Math.Min(_text.LineHeight, Math.Max(0, view.Height));

        canvas.FillRect(
            new PixelRect(left, caretTop, CaretWidth, height), _palette[PaletteIndex], BlendMode.Copy);
    }
}
