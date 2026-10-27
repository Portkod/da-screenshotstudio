using DaScreenshotStudio.Engine.Rendering;
using DaScreenshotStudio.Engine.Text;

namespace DaScreenshotStudio.Engine.Ui;

public sealed class LogComposer
{
    private readonly TextBlock _text;
    private readonly TextBlock _measure;
    private byte[] _joined = new byte[512];
    private ushort[] _joinedColors = new ushort[512];
    private byte[] _defaults = new byte[16];

    private int[] _rowCounts = [];
    private long _measuredFirst;
    private long _measuredEnd;
    private bool _measureAll = true;
    private int _totalRows;
    private int _revision = -1;
    private int _position;
    private bool _atEnd = true;
    private int _skip;
    private int _width;
    private int _height;

    public LogComposer(GameFont font, TextPalette palette, TextLog log, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(log);

        Log = log;
        _width = Math.Max(1, width);
        _height = Math.Max(1, height);
        _text = new TextBlock(font, palette, _width, Math.Max(1, _height / font.LineHeight));
        _measure = new TextBlock(font, palette, _width, 64);
    }

    public TextLog Log { get; }

    public TextBlock Text => _text;

    public int LineHeight => _text.LineHeight;

    public int Rows => Math.Max(1, _height / _text.LineHeight);

    public int Maximum
    {
        get
        {
            Refresh();
            var overflow = (_totalRows * _text.LineHeight) - _height;
            return overflow <= 0 ? 0 : ((overflow - 1) / _text.LineHeight) + 1;
        }
    }

    public int Position
    {
        get
        {
            Refresh();
            return _position;
        }

        set
        {
            var maximum = Maximum;
            var clamped = Math.Clamp(value, 0, maximum);

            _atEnd = clamped == maximum;

            if (_position == clamped)
            {
                return;
            }

            _position = clamped;
            _revision = -1;
        }
    }

    public int PageStep => Math.Max(1, (_height / _text.LineHeight) - 1);

    public void ScrollToEnd() => Position = int.MaxValue;

    public byte DefaultPaletteIndex
    {
        get => _text.DefaultPaletteIndex;
        set
        {
            _text.DefaultPaletteIndex = value;
            _measure.DefaultPaletteIndex = value;
        }
    }

    public void Resize(int width, int height)
    {
        var rewrap = _width != Math.Max(1, width);
        _width = Math.Max(1, width);
        _height = Math.Max(1, height);
        _text.MaxWidth = _width;
        _measure.MaxWidth = _width;
        _measureAll |= rewrap;
        _revision = -1;
    }

    public bool Refresh()
    {
        if (_revision == Log.Revision)
        {
            return false;
        }

        _revision = Log.Revision;
        Rebuild();
        return true;
    }

    public void Draw(Canvas canvas, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(canvas);

        Refresh();

        _text.Draw(canvas, x, y - (_skip * _text.LineHeight));
    }

    private void Rebuild()
    {
        MeasureEntries();

        var rows = Rows;
        var overflow = (_totalRows * _text.LineHeight) - _height;
        var maximum = overflow <= 0 ? 0 : ((overflow - 1) / _text.LineHeight) + 1;

        _position = _atEnd ? maximum : Math.Min(_position, maximum);
        _atEnd = _position == maximum;

        var count = Log.Count;
        var first = 0;
        var before = 0;

        while (first < count && before + RowsOf(first) <= _position)
        {
            before += RowsOf(first);
            first++;
        }

        _skip = _position - before;

        if (first >= count)
        {
            _skip = 0;
            _text.Clear();
            return;
        }

        var needed = _skip + rows;
        var used = 0;
        var last = first;

        for (var index = first; index < count; index++)
        {
            used += RowsOf(index);
            last = index;

            if (used >= needed)
            {
                break;
            }
        }

        _text.MaxLines = needed;
        Join(first, last);
    }

    private int RowsOf(int index) => _rowCounts[(int)((Log.FirstSequence + index) % _rowCounts.Length)];

    private void MeasureEntries()
    {
        if (_rowCounts.Length != Log.Capacity)
        {
            _rowCounts = new int[Log.Capacity];
            _measureAll = true;
        }

        var first = Log.FirstSequence;
        var end = Log.EndSequence;

        if (_measureAll || first < _measuredFirst || first > _measuredEnd)
        {
            _measureAll = false;
            _totalRows = 0;
            _measuredFirst = first;
            _measuredEnd = first;
        }

        for (var sequence = _measuredFirst; sequence < first; sequence++)
        {
            _totalRows -= _rowCounts[(int)(sequence % _rowCounts.Length)];
        }

        _measuredFirst = first;

        for (var sequence = _measuredEnd; sequence < end; sequence++)
        {
            var taken = Math.Max(1, _measure.CountLines(Log[(int)(sequence - first)]));
            _rowCounts[(int)(sequence % _rowCounts.Length)] = taken;
            _totalRows += taken;
        }

        _measuredEnd = end;
    }

    private void Join(int first, int last)
    {
        var length = 0;
        var entries = 0;

        for (var index = first; index <= last; index++)
        {
            var line = Log[index];
            var needed = length + line.Length + 1;
            if (_joined.Length < needed)
            {
                Array.Resize(ref _joined, Math.Max(needed, _joined.Length * 2));
            }

            if (_joinedColors.Length < _joined.Length)
            {
                Array.Resize(ref _joinedColors, _joined.Length);
            }

            if (_defaults.Length <= entries)
            {
                Array.Resize(ref _defaults, Math.Max(16, entries * 2));
            }

            if (index > first)
            {
                _joinedColors[length] = 0;
                _joined[length++] = TextBlock.LineBreak;
            }

            _defaults[entries++] = Log.PaletteAt(index);
            line.CopyTo(_joined.AsSpan(length));

            var colors = Log.ColorsAt(index);
            var target = _joinedColors.AsSpan(length, line.Length);
            target.Clear();
            colors[..Math.Min(colors.Length, line.Length)].CopyTo(target);

            length += line.Length;
        }

        _text.SetLineDefaults(_defaults.AsSpan(0, entries));
        _text.SetExplicitColors(Log.HasColors ? _joinedColors.AsSpan(0, length) : default);
        _text.SetText(_joined.AsSpan(0, length));
    }
}
