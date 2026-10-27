namespace DaScreenshotStudio.Engine.Ui;

public sealed class TextLog
{
    public const int DefaultMaxBytes = 30000;

    public const int DefaultMaxLines = 30000;

    public const int MaxAcceptedMessageLength = 70;

    private readonly byte[][] _lines;
    private readonly int[] _lengths;
    private readonly byte[] _palettes;
    private readonly ushort[]?[] _colors;
    private readonly int _maxBytes;
    private int _head;
    private int _count;
    private int _bytes;

    public TextLog(int maxLines = DefaultMaxLines, int maxBytes = DefaultMaxBytes)
    {
        var lines = Math.Clamp(maxLines, 1, DefaultMaxLines);
        _lines = new byte[lines][];
        _lengths = new int[lines];
        _palettes = new byte[lines];
        _colors = new ushort[lines][];
        _maxBytes = Math.Max(1, maxBytes);
    }

    public int Count => _count;

    public int Capacity => _lines.Length;

    public int Revision { get; private set; }

    public long FirstSequence { get; private set; }

    public long EndSequence { get; private set; }

    public ReadOnlySpan<byte> this[int index]
    {
        get
        {
            if ((uint)index >= (uint)_count)
            {
                return default;
            }

            var slot = (_head + index) % _lines.Length;
            return _lines[slot].AsSpan(0, _lengths[slot]);
        }
    }

    public byte PaletteAt(int index)
    {
        if ((uint)index >= (uint)_count)
        {
            return 0;
        }

        return _palettes[(_head + index) % _lines.Length];
    }

    public ReadOnlySpan<ushort> ColorsAt(int index)
    {
        if ((uint)index >= (uint)_count)
        {
            return default;
        }

        var slot = (_head + index) % _lines.Length;
        var colors = _colors[slot];
        return colors is null ? default : colors.AsSpan(0, _lengths[slot]);
    }

    public bool HasColors { get; private set; }

    public void Append(scoped ReadOnlySpan<byte> line) => Append(line, DefaultPalette);

    public byte DefaultPalette { get; set; } = 0x12;

    public void Append(scoped ReadOnlySpan<byte> line, byte paletteIndex) =>
        Append(line, paletteIndex, default);

    public void Append(scoped ReadOnlySpan<byte> line, byte paletteIndex, scoped ReadOnlySpan<ushort> colors)
    {
        var slot = (_head + _count) % _lines.Length;

        if (_count == _lines.Length)
        {
            _bytes -= _lengths[_head];
            _head = (_head + 1) % _lines.Length;
            FirstSequence++;
        }
        else
        {
            _count++;
        }

        EndSequence++;

        var buffer = _lines[slot];
        if (buffer is null || buffer.Length < line.Length)
        {
            buffer = new byte[Math.Max(64, line.Length)];
            _lines[slot] = buffer;
        }

        line.CopyTo(buffer);
        _lengths[slot] = line.Length;
        _palettes[slot] = paletteIndex;
        _bytes += line.Length;

        if (colors.IsEmpty)
        {
            _colors[slot] = null;
        }
        else
        {
            var stored = _colors[slot];
            if (stored is null || stored.Length < line.Length)
            {
                stored = new ushort[Math.Max(64, line.Length)];
                _colors[slot] = stored;
            }

            stored.AsSpan().Clear();
            colors[..Math.Min(colors.Length, line.Length)].CopyTo(stored);
            HasColors = true;
        }

        while (_bytes > _maxBytes && _count > 1)
        {
            _bytes -= _lengths[_head];
            _head = (_head + 1) % _lines.Length;
            _count--;
            FirstSequence++;
        }

        Revision++;
    }

    public void Clear()
    {
        _head = 0;
        _count = 0;
        _bytes = 0;
        HasColors = false;
        FirstSequence = EndSequence;
        Revision++;
    }
}
