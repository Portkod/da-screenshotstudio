using DaScreenshotStudio.Assets.Tables;

namespace DaScreenshotStudio.Assets.Maps;

public readonly struct GroundPaint : IEquatable<GroundPaint>
{
    public const int SpecialHeight = 1;

    internal GroundPaint(byte red, byte green, byte blue, byte alpha, int height)
    {
        Red = red;
        Green = green;
        Blue = blue;
        Alpha = alpha;
        Height = height;
    }

    public byte Red { get; }

    public byte Green { get; }

    public byte Blue { get; }

    public byte Alpha { get; }

    public int Height { get; }

    public bool IsSpecialGround => Height == SpecialHeight;

    public int TintWeight => Math.Clamp(TintWeightRange - (Alpha / 8), 0, TintWeightRange);

    public const int TintWeightRange = 32;

    public void GetTint(out byte red, out byte green, out byte blue)
    {
        red = Red;
        green = Blue;
        blue = Blue;
    }

    public bool Equals(GroundPaint other) =>
        Red == other.Red && Green == other.Green && Blue == other.Blue && Alpha == other.Alpha && Height == other.Height;

    public override bool Equals(object? obj) => obj is GroundPaint other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Red, Green, Blue, Alpha, Height);

    public static bool operator ==(GroundPaint left, GroundPaint right) => left.Equals(right);

    public static bool operator !=(GroundPaint left, GroundPaint right) => !left.Equals(right);
}

public sealed class GroundAttributeTable
{
    public static GroundPaint ItemOverride { get; } = new(0x39, 0xAE, 0xEF, 0x80, 100);

    private readonly int[] _records;
    private readonly GroundPaint[] _paints;

    private GroundAttributeTable(int[] records, GroundPaint[] paints)
    {
        _records = records;
        _paints = paints;
    }

    public static GroundAttributeTable Empty { get; } = new([], []);

    public static GroundAttributeTable Parse(ReadOnlySpan<byte> data)
    {
        var paints = new List<GroundPaint>();
        var tileIds = new List<int>();
        var owners = new List<int>();

        var reader = new GroundAttributeReader(data);
        Span<int> color = stackalloc int[5];

        while (reader.TryReadRecord(color, tileIds, out var height))
        {
            var paint = new GroundPaint((byte)color[0], (byte)color[1], (byte)color[2], (byte)color[3], height);
            paints.Add(paint);

            while (owners.Count < tileIds.Count)
            {
                owners.Add(paints.Count - 1);
            }
        }

        if (tileIds.Count == 0)
        {
            return Empty;
        }

        var highest = 0;
        for (var i = 0; i < tileIds.Count; i++)
        {
            highest = Math.Max(highest, tileIds[i]);
        }

        var records = new int[highest + 1];
        records.AsSpan().Fill(-1);

        for (var i = 0; i < tileIds.Count; i++)
        {
            records[tileIds[i]] = owners[i];
        }

        return new GroundAttributeTable(records, [.. paints]);
    }

    public bool TryGetPaint(int groundTileId, out GroundPaint paint)
    {
        if ((uint)groundTileId < (uint)_records.Length && _records[groundTileId] >= 0)
        {
            paint = _paints[_records[groundTileId]];
            return true;
        }

        paint = default;
        return false;
    }

    public bool IsSpecialGround(int groundTileId) =>
        TryGetPaint(groundTileId, out var paint) && paint.IsSpecialGround;

    private ref struct GroundAttributeReader(ReadOnlySpan<byte> data)
    {
        private ReadOnlySpan<byte> _remaining = data;

        public bool TryReadRecord(scoped Span<int> color, List<int> tileIds, out int height)
        {
            height = 0;

            if (!SkipPast("ATTR_gnd_paint"u8))
            {
                return false;
            }

            for (var i = 0; i < 5; i++)
            {
                if (!TryReadNumber(out color[i]))
                {
                    return false;
                }
            }

            height = color[4];

            if (!SkipPast("apply_to"u8))
            {
                return false;
            }

            var pending = -1;
            var inRange = false;

            while (!_remaining.IsEmpty)
            {
                SkipTrivia();
                if (_remaining.IsEmpty)
                {
                    break;
                }

                var next = _remaining[0];
                if (next == (byte)']')
                {
                    _remaining = _remaining[1..];
                    if (pending >= 0)
                    {
                        Add(tileIds, pending);
                    }

                    return true;
                }

                if (next == (byte)'(')
                {
                    _remaining = _remaining[1..];
                    if (pending >= 0)
                    {
                        Add(tileIds, pending);
                        pending = -1;
                    }

                    inRange = true;
                    continue;
                }

                if (next == (byte)')')
                {
                    _remaining = _remaining[1..];
                    inRange = false;
                    continue;
                }

                if (next == (byte)',')
                {
                    _remaining = _remaining[1..];
                    continue;
                }

                if (!TryReadInt(out var value))
                {
                    continue;
                }

                if (inRange && pending >= 0)
                {
                    var last = Math.Min(value, MapCell.MaxTileId);
                    for (var id = pending; id <= last; id++)
                    {
                        tileIds.Add(id);
                    }

                    pending = -1;
                    continue;
                }

                if (pending >= 0)
                {
                    Add(tileIds, pending);
                }

                pending = value;
            }

            if (pending >= 0)
            {
                Add(tileIds, pending);
            }

            return true;
        }

        private static void Add(List<int> tileIds, int tileId)
        {
            if ((uint)tileId <= MapCell.MaxTileId)
            {
                tileIds.Add(tileId);
            }
        }

        private bool SkipPast(ReadOnlySpan<byte> token)
        {
            var index = _remaining.IndexOf(token);
            if (index < 0)
            {
                _remaining = default;
                return false;
            }

            _remaining = _remaining[(index + token.Length)..];
            return true;
        }

        private void SkipTrivia()
        {
            while (!_remaining.IsEmpty)
            {
                if (_remaining[0] <= (byte)' ')
                {
                    _remaining = _remaining[1..];
                    continue;
                }

                if (_remaining.Length >= 2 && _remaining[0] == (byte)'/' && _remaining[1] == (byte)'*')
                {
                    var end = _remaining.IndexOf("*/"u8);
                    _remaining = end < 0 ? default : _remaining[(end + 2)..];
                    continue;
                }

                return;
            }
        }

        private bool TryReadNumber(out int value) => TryReadInt(out value, stopAtBrackets: false);

        private bool TryReadInt(out int value) => TryReadInt(out value, stopAtBrackets: true);

        private bool TryReadInt(out int value, bool stopAtBrackets)
        {
            value = 0;

            while (!_remaining.IsEmpty)
            {
                SkipTrivia();
                if (_remaining.IsEmpty)
                {
                    return false;
                }

                if ((uint)(_remaining[0] - (byte)'0') <= 9)
                {
                    break;
                }

                if (stopAtBrackets && _remaining[0] is (byte)'(' or (byte)')' or (byte)']')
                {
                    return false;
                }

                _remaining = _remaining[1..];
            }

            return TableReader.TryReadDigits(ref _remaining, out value);
        }
    }
}
