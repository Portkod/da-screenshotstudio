using DaScreenshotStudio.Engine.Rendering;
using DaScreenshotStudio.Engine.Text;
using DaScreenshotStudio.Engine.App;

namespace DaScreenshotStudio.Engine.Ui;

public static class ProfileTextFile
{
    public const string FileName = "profile.txt";

    public const int MaxBytes = 0x172;

    public static int Load(ClientDirectory? characterDirectory, Span<byte> buffer)
    {
        if (characterDirectory is null || buffer.IsEmpty)
        {
            return 0;
        }

        var room = Math.Min(buffer.Length, MaxBytes);
        var length = characterDirectory.TryReadInto(FileName, buffer[..room]);
        if (length <= 0)
        {
            return 0;
        }

        var terminator = buffer[..length].IndexOf((byte)0);
        return terminator >= 0 ? terminator : length;
    }

    public static int ToDisplay(scoped ReadOnlySpan<byte> text, Span<byte> display)
    {
        var length = Math.Min(text.Length, display.Length);
        for (var i = 0; i < length; i++)
        {
            display[i] = text[i] == (byte)'\r' ? TextBlock.LineBreak : text[i];
        }

        return length;
    }
}

public sealed class ProfileTextEditPane : GuiChildPane, IFocusableControl
{
    public const int MaxBytes = 0x7530;

    public const int MaxLines = 0x7530;

    public const byte Break = (byte)'\r';

    public const int Inset = 2;

    public const byte Ink = 0xFF;

    public const ushort FillColor = DescPane.FillColor;

    public const int CaretWidth = 2;

    private readonly TextBlock _text;
    private readonly byte[] _value = new byte[MaxBytes];
    private readonly byte[] _display = new byte[MaxBytes];
    private readonly ushort _caretColor;
    private int _length;
    private int _topLine;
    private TextCaret _caret;
    private TextPointerState _pointer;
    private ushort[]? _selectionColors;

    private readonly ushort? _fill;

    public ProfileTextEditPane(
        in PixelRect bounds,
        PixelBlender blender,
        GameFont font,
        TextPalette palette,
        byte ink = Ink,
        ushort? fill = FillColor)
        : base(in bounds, blender)
    {
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(palette);

        _text = new TextBlock(font, palette, Math.Max(1, bounds.Width), MaxLines)
        {
            Wraps = true,
            DefaultPaletteIndex = ink,

            InterpretsMarkup = false,
        };

        _fill = fill;
        _caretColor = palette[ink];
    }

    public bool IsEditable { get; init; } = true;

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

    public bool IsFocusable { get; set; } = true;

    ControlKind IFocusableControl.Kind => ControlKind.TextEdit;

    bool IFocusableControl.TakesEnter => true;

    public Action? Canceled { get; set; }

    public ReadOnlySpan<byte> Value => _value.AsSpan(0, _length);

    public int Length => _length;

    public TextCaret Caret => _caret;

    public int CaretPosition => _caret.Position;

    public void SetValue(scoped ReadOnlySpan<byte> value)
    {
        _length = Math.Min(value.Length, MaxBytes);
        value[.._length].CopyTo(_value);
        _caret.Collapse(_length, _length);
        Compose();
    }

    public bool IsReadOnly { get; set; }

    protected override bool OnKey(in KeyEvent key)
    {
        if (IsReadOnly)
        {
            return false;
        }

        if (!Visible || !IsFocused || !IsEditable)
        {
            return false;
        }

        switch (key.Type)
        {
            case UiEventType.KeyDown when TextEditing.IsArrow(key.Key):
                Navigate(key.Key, key.Modifiers);
                return true;

            case UiEventType.Character when key.Character >= 0x20 && key.Character != 0x7F:
                if (TextEditing.IsModified(key.Modifiers))
                {
                    return true;
                }

                Insert(key.Character);
                return true;

            case UiEventType.KeyDown when key.Key == UiKeys.Enter:
                Insert(Break);
                return true;

            case UiEventType.KeyDown when key.Key == UiKeys.Backspace:
                if (!TextEditing.IsModified(key.Modifiers)
                    && TextEditing.Backspace(_value, ref _length, ref _caret))
                {
                    Compose();
                }

                return true;

            case UiEventType.KeyDown when key.Key == UiKeys.Delete:
                if (!TextEditing.IsModified(key.Modifiers)
                    && TextEditing.DeleteForward(_value, ref _length, ref _caret))
                {
                    Compose();
                }

                return true;

            case UiEventType.KeyDown when key.Key == UiKeys.Escape:
                Canceled?.Invoke();
                return true;

            default:
                return false;
        }
    }

    protected override bool OnPointer(in PointerEvent pointer)
    {
        if (!Visible || !IsFocused || !IsEditable)
        {
            return false;
        }

        _text.Compose();

        switch (pointer.Type)
        {
            case UiEventType.LeftDown:
            case UiEventType.LeftDoubleClick:
                if (!Contains(in pointer))
                {
                    return false;
                }

                TextPointer.Press(
                    ref _caret, ref _pointer, _text, Value,
                    pointer.X, pointer.Y, pointer.Modifiers, pointer.Time);
                Invalidate();
                return true;

            case UiEventType.PointerMove when _pointer.IsDragging:
                TextPointer.Drag(ref _caret, ref _pointer, _text, Value, pointer.X, pointer.Y);
                Invalidate();
                return true;

            case UiEventType.LeftUp when _pointer.IsDragging:
                TextPointer.Release(ref _pointer);
                return true;

            default:
                return false;
        }
    }

    private void Navigate(int key, InputModifiers modifiers)
    {
        _text.Compose();
        TextEditing.Navigate(
            ref _caret, _text, Value, key, modifiers, new PixelRect(0, 0, Bounds.Width, Bounds.Height), 0);
        ScrollCaretIntoView();
        Invalidate();
    }

    protected override void OnDrawContent(Canvas canvas)
    {
        if (_fill is { } fill)
        {
            canvas.FillRect(canvas.Bounds, fill, BlendMode.Copy);
        }
        else
        {
            canvas.Clear();
        }

        ApplySelectionColors();

        if (_caret.HasRun)
        {
            DrawSelection(canvas);
        }

        _text.DrawIn(canvas, new PixelRect(0, -ViewOffset, canvas.Width, canvas.Height + ViewOffset));

        if (IsFocused && IsEditable && !_caret.HasRun)
        {
            DrawCaret(canvas);
        }
    }

    private void DrawSelection(Canvas canvas)
    {
        var height = _text.LineHeight;

        for (var line = 0; line < _text.LineCount; line++)
        {
            var start = _text.LineStart(line);
            var end = start + _text.LineLength(line);
            if (_caret.High <= start || _caret.Low >= end)
            {
                continue;
            }

            var top = (line * height) - ViewOffset;
            if (top >= canvas.Height)
            {
                break;
            }

            if (top + height <= 0)
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
                new PixelRect(from, top, to - from, Math.Min(height, canvas.Height - top)),
                _caretColor,
                BlendMode.Copy);
        }
    }

    private void ApplySelectionColors()
    {
        if (!_caret.HasRun)
        {
            _selectionColors = null;
            _text.SetExplicitColors(default);
            return;
        }

        if (_selectionColors is null || _selectionColors.Length < _length)
        {
            _selectionColors = new ushort[Math.Max(64, _length)];
        }

        var colors = _selectionColors.AsSpan(0, _length);
        colors.Clear();
        colors[_caret.Low..Math.Min(_caret.High, _length)].Fill(_fill ?? FillColor);
        _text.SetExplicitColors(colors);
    }

    private void Insert(byte value)
    {
        if (TextEditing.Insert(_value, ref _length, ref _caret, value, MaxBytes))
        {
            Compose();
        }
    }

    private void Compose()
    {
        _text.SetText(_display.AsSpan(0, ProfileTextFile.ToDisplay(Value, _display)));
        ScrollCaretIntoView();
        Invalidate();
    }

    private int ViewOffset => _topLine * _text.LineHeight;

    public int VisibleLines => Math.Max(1, Bounds.Height / Math.Max(1, _text.LineHeight));

    private void ScrollCaretIntoView()
    {
        _text.Compose();

        var visible = VisibleLines;
        var line = _text.LineCount == 0 ? 0 : _text.LineOf(_caret.Position);

        if (line < _topLine)
        {
            _topLine = line;
        }
        else if (line >= _topLine + visible)
        {
            _topLine = line - visible + 1;
        }

        _topLine = Math.Clamp(_topLine, 0, Math.Max(0, _text.LineCount - visible));
    }

    private void DrawCaret(Canvas canvas)
    {
        var line = _text.LineCount == 0 ? 0 : _text.LineOf(_caret.Position);
        var top = (line * _text.LineHeight) - ViewOffset;
        if (top >= canvas.Height || top + _text.LineHeight <= 0)
        {
            return;
        }

        var pen = _text.LineCount == 0 ? 0 : _text.PenOn(line, _caret.Position);
        var left = Math.Max(0, pen - (CaretWidth - 1));
        var height = Math.Min(_text.LineHeight, canvas.Height - top);

        canvas.FillRect(new PixelRect(left, top, CaretWidth, height), _caretColor, BlendMode.Copy);
    }
}

public sealed class PortraitTextDialogPane : LayoutPane
{
    public const string LayoutName = "_nui_pi.txt";

    public const string EditControl = "TEXT";

    public const int TopOffset = 0x64;

    public const string BackgroundEntryName = "DlgBack2.spf";

    private readonly ProfileTextEditPane? _editor;
    private readonly PixelRect _controlRect;

    public PortraitTextDialogPane(PixelBlender blender, UiAssets assets)
        : base(assets, blender, LayoutName)
    {
        ConsumesPointerInside = true;
        Visible = false;

        if (GuiLayoutGeometry.TryRect(Layout, EditControl, out _controlRect))
        {
            var editRect = _controlRect.Inflate(-ProfileTextEditPane.Inset, -ProfileTextEditPane.Inset);

            _editor = new ProfileTextEditPane(in editRect, Blender, Assets.Font, Assets.Palette)
            {
                Canceled = Cancel,
            };

            Add(_editor);
        }

        AddButton("OK", _ => Accept());
        AddButton("CANCEL", _ => Cancel());

        Place();
    }

    public ClientDirectory? CharacterDirectory { get; set; }

    public ReadOnlySpan<byte> Text => _editor is null ? default : _editor.Value;

    public void Open()
    {
        Span<byte> buffer = stackalloc byte[ProfileTextFile.MaxBytes];
        var length = ProfileTextFile.Load(CharacterDirectory, buffer);

        _editor?.SetValue(buffer[..length]);

        ResetFocus();

        Visible = true;
        Invalidate();
    }

    public void Accept() => Close();

    public void Cancel() => Close();

    private void Close()
    {
        if (!Visible)
        {
            return;
        }

        ResetFocus();
        Visible = false;
        Invalidate();
    }

    protected override void OnCancel() => Cancel();

    protected override bool OnPointer(in PointerEvent pointer)
    {
        if (!Visible)
        {
            return false;
        }

        if (pointer.Type == UiEventType.LeftDown)
        {
            FocusControl(_controlRect.Contains(pointer.X, pointer.Y) ? _editor : null);
        }

        return false;
    }

    protected override void OnDrawContent(Canvas canvas)
    {
        canvas.Clear();

        var tile = Assets.Frame(BackgroundEntryName, 0);
        if (!tile.IsEmpty)
        {
            for (var y = 0; y < canvas.Height; y += tile.Height)
            {
                for (var x = 0; x < canvas.Width; x += tile.Width)
                {
                    canvas.BlitPixmap(x, y, in tile, BlendMode.Copy);
                }
            }
        }

        DrawBorder(canvas);
    }

    private void DrawBorder(Canvas canvas)
    {
        var width = canvas.Width;
        var height = canvas.Height;
        var tile = DescPane.TileSize;

        for (var x = tile; x + tile < width - 1; x += tile)
        {
            Blit(canvas, 1, x, 0);
            Blit(canvas, 6, x, height - tile);
        }

        for (var y = tile; y + tile < height - 1; y += tile)
        {
            Blit(canvas, 3, 0, y);
            Blit(canvas, 4, width - tile, y);
        }

        Blit(canvas, 0, 0, 0);
        Blit(canvas, 2, width - tile, 0);
        Blit(canvas, 5, 0, height - tile);
        Blit(canvas, 7, width - tile, height - tile);
    }

    private void Blit(Canvas canvas, int frame, int x, int y)
    {
        var art = Assets.PaletteFrame(DescPane.FrameEntryName, frame);
        if (!art.IsEmpty)
        {
            canvas.BlitPixmap(x, y, in art, BlendMode.Transparent);
        }
    }

    private void Place()
    {
        var window = Bounds;
        SetBounds(new PixelRect(
            (GuiBackPane.ScreenWidth - window.Width) / 2,
            TopOffset,
            window.Width,
            window.Height));
    }
}
