using DaScreenshotStudio.Engine.Rendering;
using DaScreenshotStudio.Engine.Core;

namespace DaScreenshotStudio.Engine.Text;

public enum TextAlignment
{
    Left,
    Center,
    Right,
}

public sealed class TextBlock
{
    public const byte LineBreak = (byte)'\n';

    private readonly GameFont _font;
    private readonly TextPalette _palette;

    private byte[] _text = [];
    private int _textLength;

    private byte[] _plain = [];
    private ushort[] _plainColor = [];

    private byte[] _plainDefault = [];
    private int _plainLength;

    private int[] _codeAt = [];
    private int _codeCount;
    private byte[] _lineDefaults = [];
    private int _lineDefaultCount;
    private ushort[] _explicitColors = [];
    private int _explicitColorCount;

    private int[] _lineStart = [];
    private int[] _lineLength = [];
    private int[] _lineWidth = [];
    private int _lineCount;

    private byte[] _mask = [];
    private ushort[] _pixels = [];

    private byte[] _defaultInk = [];
    private int _stride;
    private int _capacityHeight;

    private bool _dirty = true;

    public TextBlock(GameFont font, TextPalette palette, int maxWidth, int maxLines)
    {
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(palette);

        _font = font;
        _palette = palette;
        MaxWidth = Math.Max(1, maxWidth);
        MaxLines = Math.Max(1, maxLines);
        DefaultPaletteIndex = TextPalette.DefaultIndex;
    }

    public int MaxWidth
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = Math.Max(1, value);
            _dirty = true;
        }
    }

    public int MaxLines
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = Math.Max(1, value);
            _dirty = true;
        }
    }

    public bool Wraps
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            _dirty = true;
        }
    } = true;

    public bool InterpretsMarkup
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            _dirty = true;
        }
    } = true;

    public bool BreaksOnWordBoundary
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            _dirty = true;
        }
    }

    public TextAlignment Alignment
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            _dirty = true;
        }
    }

    public byte DefaultPaletteIndex
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            _dirty = true;
        }
    }

    public GameFont Font => _font;

    public int LineHeight
    {
        get => field > 0 ? field : _font.LineHeight;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            _dirty = true;
        }
    }

    public int LineCount
    {
        get
        {
            Compose();
            return _lineCount;
        }
    }

    public int LineStart(int line)
    {
        Compose();
        return (uint)line < (uint)_lineCount ? _lineStart[line] : 0;
    }

    public int LineLength(int line)
    {
        Compose();
        return (uint)line < (uint)_lineCount ? _lineLength[line] : 0;
    }

    public int LineOf(int index)
    {
        Compose();

        for (var line = _lineCount - 1; line > 0; line--)
        {
            if (index >= _lineStart[line])
            {
                return line;
            }
        }

        return 0;
    }

    public int PenOn(int line, int index)
    {
        Compose();
        if ((uint)line >= (uint)_lineCount)
        {
            return 0;
        }

        var offset = Math.Clamp(index - _lineStart[line], 0, _lineLength[line]);
        return offset * GameFont.FixedCellWidth;
    }

    public int IndexAt(int line, int pen)
    {
        Compose();
        if (_lineCount == 0)
        {
            return 0;
        }

        line = Math.Clamp(line, 0, _lineCount - 1);

        var start = _lineStart[line];
        var length = _lineLength[line];

        if (length > 0 && IsRunSeparator(_plain[start + length - 1]))
        {
            length--;
        }

        return pen <= 0 ? start : start + _font.CountFitting(_plain.AsSpan(start, length), pen);
    }

    public int LineAt(int row)
    {
        Compose();
        if (_lineCount == 0 || LineHeight <= 0)
        {
            return 0;
        }

        return Math.Clamp(row / LineHeight, 0, _lineCount - 1);
    }

    private static bool IsRunSeparator(byte value) => value is 0x09 or 0x0D or 0x20 or 0x0A;

    public int Width
    {
        get
        {
            Compose();
            return ComposedWidth;
        }
    }

    public int Height
    {
        get
        {
            Compose();
            return _lineCount * LineHeight;
        }
    }

    public ReadOnlySpan<byte> Value => _text.AsSpan(0, _textLength);

    public Pixmap Pixmap
    {
        get
        {
            Compose();
            return _lineCount == 0
                ? default
                : new Pixmap(_pixels, ComposedWidth, _lineCount * LineHeight, _stride);
        }
    }

    private int ComposedWidth { get; set; }

    public bool SetText(scoped ReadOnlySpan<byte> text)
    {
        if (text.Length == _textLength && text.SequenceEqual(_text.AsSpan(0, _textLength)))
        {
            return false;
        }

        if (_text.Length < text.Length)
        {
            _text = new byte[Math.Max(64, text.Length)];
        }

        text.CopyTo(_text);
        _textLength = text.Length;
        _dirty = true;
        return true;
    }

    public void SetExplicitColors(scoped ReadOnlySpan<ushort> colors)
    {
        if (colors.Length == _explicitColorCount
            && colors.SequenceEqual(_explicitColors.AsSpan(0, _explicitColorCount)))
        {
            return;
        }

        if (_explicitColors.Length < colors.Length)
        {
            _explicitColors = new ushort[Math.Max(64, colors.Length)];
        }

        colors.CopyTo(_explicitColors);
        _explicitColorCount = colors.Length;
        _dirty = true;
    }

    public void SetLineDefaults(scoped ReadOnlySpan<byte> defaults)
    {
        if (defaults.Length == _lineDefaultCount && defaults.SequenceEqual(_lineDefaults.AsSpan(0, _lineDefaultCount)))
        {
            return;
        }

        if (_lineDefaults.Length < defaults.Length)
        {
            _lineDefaults = new byte[Math.Max(16, defaults.Length)];
        }

        defaults.CopyTo(_lineDefaults);
        _lineDefaultCount = defaults.Length;
        _dirty = true;
    }

    public void Clear()
    {
        SetText(default);
        SetLineDefaults(default);
    }

    public void Invalidate() => _dirty = true;

    public bool IsDirty => _dirty;

    public int CountLines(scoped ReadOnlySpan<byte> text)
    {
        SetText(text);
        Strip();
        Wrap();

        _dirty = true;
        return _lineCount;
    }

    public void Draw(Canvas canvas, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(canvas);

        var pixmap = Pixmap;
        if (!pixmap.IsEmpty)
        {
            canvas.BlitMasked(x, y, in pixmap, _mask, BlendMode.Transparent);
        }
    }

    public void DrawWithDefaultColor(Canvas canvas, int x, int y, ushort defaultColor)
    {
        ArgumentNullException.ThrowIfNull(canvas);

        var pixmap = Pixmap;
        if (pixmap.IsEmpty)
        {
            return;
        }

        canvas.BlitMasked(x, y, in pixmap, _mask, BlendMode.Transparent);
        canvas.FillMasked(x, y, pixmap.Width, pixmap.Height, _defaultInk, _stride, defaultColor);
    }

    public void DrawIn(Canvas canvas, in PixelRect box, int minimumX = int.MinValue)
    {
        ArgumentNullException.ThrowIfNull(canvas);

        var pixmap = Pixmap;
        if (pixmap.IsEmpty)
        {
            return;
        }

        var x = Alignment switch
        {
            TextAlignment.Center => box.X + ((box.Width - pixmap.Width) / 2),
            TextAlignment.Right => box.Right - pixmap.Width,
            _ => box.X,
        };

        canvas.BlitMasked(Math.Max(x, minimumX), box.Y, in pixmap, _mask, BlendMode.Transparent);
    }

    public void Compose()
    {
        if (!_dirty)
        {
            return;
        }

        _dirty = false;
        Strip();
        Wrap();
        Rasterize();
    }

    private void Strip()
    {
        EnsurePlain(_textLength);
        _plainLength = 0;
        _codeCount = 0;

        var sourceLine = 0;
        var sourceIndex = 0;
        var current = _palette[DefaultFor(sourceLine)];
        var currentIsDefault = true;

        if (!InterpretsMarkup)
        {
            Copy(_text.AsSpan(0, _textLength), ref sourceLine, ref sourceIndex, ref current, ref currentIsDefault);
            return;
        }

        var reader = new MarkupReader(_text.AsSpan(0, _textLength));

        while (reader.TryRead(out var segment))
        {
            if (segment.Kind == MarkupSegmentKind.PaletteChange)
            {
                current = _palette[segment.PaletteIndex];
                currentIsDefault = false;
                sourceIndex += TextMarkup.CodeLength;

                if (_codeCount < _codeAt.Length)
                {
                    _codeAt[_codeCount++] = _plainLength;
                }

                continue;
            }

            Copy(segment.Text, ref sourceLine, ref sourceIndex, ref current, ref currentIsDefault);
        }
    }

    private void Copy(
        scoped ReadOnlySpan<byte> text,
        ref int sourceLine,
        ref int sourceIndex,
        ref ushort current,
        ref bool currentIsDefault)
    {
        for (var i = 0; i < text.Length; i++, sourceIndex++)
        {
            var value = text[i] == (byte)'\r' ? (byte)' ' : text[i];

            if (value == LineBreak)
            {
                current = _palette[DefaultFor(++sourceLine)];
                currentIsDefault = true;
            }

            var explicitColor = sourceIndex < _explicitColorCount ? _explicitColors[sourceIndex] : (ushort)0;

            _plain[_plainLength] = value;
            _plainColor[_plainLength] = explicitColor != 0 ? explicitColor : current;
            _plainDefault[_plainLength] = explicitColor == 0 && currentIsDefault ? (byte)1 : (byte)0;
            _plainLength++;
        }
    }

    private void Wrap()
    {
        EnsureLines(Math.Min(MaxLines, _plainLength + 1));
        _lineCount = 0;
        ComposedWidth = 0;

        var position = 0;
        while (_lineCount < MaxLines)
        {
            if (position >= _plainLength)
            {
                break;
            }

            var line = _plain.AsSpan(position, _plainLength - position);
            var hardBreak = line.IndexOf(LineBreak);
            var limit = hardBreak >= 0 ? hardBreak : line.Length;

            var consumed = limit;
            var taken = Wraps ? Fit(line[..limit], position, out consumed) : limit;
            AddLine(position, taken);
            position += consumed;

            if (consumed < limit)
            {
                continue;
            }

            if (hardBreak >= 0)
            {
                position += 1;

                if (position >= _plainLength && _lineCount < MaxLines)
                {
                    AddLine(position, 0);
                }
            }
        }
    }

    public const byte Separator = 0x20;

    private int Fit(scoped ReadOnlySpan<byte> line, int start, out int consumed)
    {
        var fitting = MaxWidth / GameFont.FixedCellWidth;

        if (fitting >= line.Length)
        {
            consumed = line.Length;
            return line.Length;
        }

        var codePage = _font.CodePage;
        bool keep;

        if (codePage.IsLeadByte(line[fitting]) || (fitting > 0 && codePage.IsLeadByte(line[fitting - 1])))
        {
            var index = 0;
            while (index < fitting)
            {
                index += codePage.IsLeadByte(line[index]) ? 2 : 1;
            }

            if (index > fitting)
            {
                fitting--;
            }

            keep = true;
        }
        else
        {
            keep = MaxWidth < (fitting * GameFont.FixedCellWidth) + (GameFont.FixedCellWidth / 2);
        }

        if (!keep && !BreaksOnWordBoundary)
        {
            fitting--;
        }

        var boundary = Math.Clamp(fitting, 0, line.Length - 1);
        return BreaksOnWordBoundary
            ? BreakOnWordBoundary(line, start, boundary, out consumed)
            : BreakAtSeparator(line, boundary, out consumed);
    }

    private int BreakAtSeparator(scoped ReadOnlySpan<byte> line, int boundary, out int consumed)
    {
        var codePage = _font.CodePage;
        var pending = false;
        var found = false;
        var taken = boundary;
        var next = boundary;
        var fallback = boundary;

        for (var i = 0; i <= boundary; i++)
        {
            if (pending)
            {
                pending = false;
                continue;
            }

            pending = codePage.IsLeadByte(line[i]);

            if (i < boundary && line[i] <= Separator)
            {
                found = true;
                taken = i;
                next = i + 1;

                if (next == boundary)
                {
                    while (next < line.Length && line[next] <= Separator)
                    {
                        next++;
                    }

                    break;
                }

                continue;
            }

            fallback = i;
        }

        if (found)
        {
            consumed = next;
            return taken;
        }

        consumed = Math.Max(1, fallback);
        return consumed;
    }

    private int BreakOnWordBoundary(
        scoped ReadOnlySpan<byte> line, int start, int boundary, out int consumed)
    {
        var codePage = _font.CodePage;
        var origin = RunStart(start, boundary);
        var index = boundary;

        if (!codePage.IsLeadByte(line[boundary])
            && !(boundary > origin && codePage.IsLeadByte(line[boundary - 1]))
            && IsAlphanumeric(line[boundary]))
        {
            var found = false;
            var last = origin;
            var walk = origin;

            while (walk < boundary)
            {
                if (codePage.IsLeadByte(line[walk]))
                {
                    walk += 2;
                    continue;
                }

                if (IsRunSpace(line[walk]))
                {
                    found = true;
                    last = walk;
                }

                walk++;
            }

            index = found ? last : Math.Min(walk, line.Length);
        }

        var next = index;
        while (next < line.Length && IsRunSpace(line[next]))
        {
            next++;
        }

        consumed = Math.Max(1, next);
        return index;
    }

    private int RunStart(int start, int boundary)
    {
        var absolute = start + boundary;
        var origin = 0;

        for (var i = 0; i < _codeCount && _codeAt[i] <= absolute; i++)
        {
            origin = _codeAt[i];
        }

        return Math.Max(0, origin - start);
    }

    private static bool IsRunSpace(byte value) => value is >= 0x09 and <= 0x0D or 0x20;

    private static bool IsAlphanumeric(byte value) =>
        value is >= (byte)'0' and <= (byte)'9' or >= (byte)'A' and <= (byte)'Z' or >= (byte)'a' and <= (byte)'z';

    private void AddLine(int start, int length)
    {
        _lineStart[_lineCount] = start;
        _lineLength[_lineCount] = length;

        var width = _font.MeasureWidth(_plain.AsSpan(start, length));
        _lineWidth[_lineCount] = width;
        ComposedWidth = Math.Max(ComposedWidth, width);
        _lineCount++;
    }

    private void Rasterize()
    {
        if (_lineCount == 0 || ComposedWidth == 0)
        {
            return;
        }

        var height = _lineCount * LineHeight;
        EnsureSurface(ComposedWidth, height);

        var mask = _mask.AsSpan(0, _stride * height);
        var pixels = _pixels.AsSpan(0, _stride * height);
        var defaultInk = _defaultInk.AsSpan(0, _stride * height);
        mask.Clear();
        pixels.Clear();
        defaultInk.Clear();

        for (var line = 0; line < _lineCount; line++)
        {
            var lineTop = line * LineHeight;
            var start = _lineStart[line];
            var length = _lineLength[line];

            var penX = Alignment switch
            {
                TextAlignment.Center => (ComposedWidth - _lineWidth[line]) / 2,
                TextAlignment.Right => ComposedWidth - _lineWidth[line],
                _ => 0,
            };

            var runStart = start;
            while (runStart < start + length)
            {
                var color = _plainColor[runStart];
                var isDefault = _plainDefault[runStart];
                var runEnd = runStart + 1;
                while (runEnd < start + length && _plainColor[runEnd] == color && _plainDefault[runEnd] == isDefault)
                {
                    runEnd++;
                }

                var from = penX;
                penX = _font.Face.DrawText(
                    _plain.AsSpan(runStart, runEnd - runStart),
                    _font.CodePage,
                    mask,
                    _stride,
                    height,
                    penX,
                    lineTop);

                Colorize(mask, pixels, isDefault != 0 ? defaultInk : default, from, penX, lineTop, color);
                runStart = runEnd;
            }
        }
    }

    private void Colorize(
        ReadOnlySpan<byte> mask, Span<ushort> pixels, Span<byte> defaultInk, int fromX, int toX, int lineTop, ushort color)
    {
        var left = Math.Max(0, fromX);
        var right = Math.Min(_stride, toX);
        if (right <= left)
        {
            return;
        }

        var bottom = Math.Min(lineTop + LineHeight, mask.Length / _stride);
        for (var y = lineTop; y < bottom; y++)
        {
            var row = y * _stride;
            for (var x = left; x < right; x++)
            {
                if (mask[row + x] != 0 && pixels[row + x] == 0)
                {
                    pixels[row + x] = color;

                    if (!defaultInk.IsEmpty)
                    {
                        defaultInk[row + x] = 1;
                    }
                }
            }
        }
    }

    private byte DefaultFor(int sourceLine) =>
        (uint)sourceLine < (uint)_lineDefaultCount ? _lineDefaults[sourceLine] : DefaultPaletteIndex;

    private void EnsurePlain(int length)
    {
        if (_plain.Length >= length)
        {
            return;
        }

        var size = Math.Max(128, length);
        _plain = new byte[size];
        _plainColor = new ushort[size];
        _plainDefault = new byte[size];

        _codeAt = new int[(size / TextMarkup.CodeLength) + 1];
    }

    private void EnsureLines(int count)
    {
        if (_lineStart.Length >= count)
        {
            return;
        }

        _lineStart = new int[count];
        _lineLength = new int[count];
        _lineWidth = new int[count];
    }

    private void EnsureSurface(int width, int height)
    {
        if (width <= _stride && height <= _capacityHeight)
        {
            return;
        }

        _stride = Math.Max(width, _stride);
        _capacityHeight = Math.Max(height, _capacityHeight);
        _mask = new byte[_stride * _capacityHeight];
        _pixels = new ushort[_stride * _capacityHeight];
        _defaultInk = new byte[_stride * _capacityHeight];
    }
}
