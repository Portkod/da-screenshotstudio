using System.Text;
using DaScreenshotStudio.Assets.Expressions;
using DaScreenshotStudio.Assets.Tables;

namespace DaScreenshotStudio.Assets.Effects;

public enum MotionEffectMode
{
    Distance,

    Time,
}

public enum MotionEffectExpressionGroup
{
    Step,

    X,

    Y,

    Z,
}

public sealed class MotionEffectRecord
{
    private readonly string[][] _sets;
    private readonly int[] _handles;

    internal MotionEffectRecord(
        int id,
        int type,
        int frames,
        MotionEffectMode mode,
        int step,
        int stepDelay,
        int vertical,
        int horizontal,
        string[][] sets,
        int[] handles)
    {
        Id = id;
        Type = type;
        Frames = frames;
        Mode = mode;
        Step = step;
        StepDelay = stepDelay;
        Vertical = vertical;
        Horizontal = horizontal;
        _sets = sets;
        _handles = handles;
        Expressions = sets.Length == 0 ? [] : sets[0];
    }

    public int Id { get; }

    public int Type { get; }

    public int Frames { get; }

    public MotionEffectMode Mode { get; }

    public int Step { get; }

    public int StepDelay { get; }

    public int Vertical { get; }

    public int Horizontal { get; }

    public IReadOnlyList<string> Expressions { get; }

    public int SetCount => _sets.Length;

    public int GetHandle(MotionEffectExpressionGroup group, int set)
    {
        if ((uint)set >= (uint)_sets.Length || (uint)group > (uint)MotionEffectExpressionGroup.Z)
        {
            return -1;
        }

        return _handles[(set * MotionEffectTable.GroupCount) + (int)group];
    }
}

public sealed class MotionEffectTable
{
    public const int GroupCount = 4;

    public const int MaxSets = 5;

    public const byte DistanceVariable = (byte)'d';

    public const byte TotalTimeVariable = (byte)'T';

    public const byte SampleTimeVariable = (byte)'t';

    private static ReadOnlySpan<byte> CommentMarker => "#"u8;

    private const byte ExpressionMarker = (byte)'%';

    private const byte SetTerminator = (byte)'$';

    private readonly MotionEffectRecord[] _records;

    private MotionEffectTable(MotionEffectRecord[] records, int leadingValue, ExpressionContext expressions, int uncompiled)
    {
        _records = records;
        LeadingValue = leadingValue;
        Expressions = expressions;
        UncompiledExpressionCount = uncompiled;
    }

    public IReadOnlyList<MotionEffectRecord> Records => _records;

    public int LeadingValue { get; }

    public ExpressionContext Expressions { get; }

    public int UncompiledExpressionCount { get; }

    public static MotionEffectTable Empty { get; } = new([], 0, NewContext(), 0);

    public static ExpressionContext NewContext()
    {
        var context = new ExpressionContext();
        context.RegisterNumber(DistanceVariable);
        context.RegisterNumber(TotalTimeVariable);
        context.RegisterNumber(SampleTimeVariable);
        return context;
    }

    public static MotionEffectTable Parse(ReadOnlySpan<byte> data)
    {
        var records = new List<MotionEffectRecord>();
        var context = NewContext();
        var uncompiled = 0;
        var leading = 0;
        var seenLeading = false;

        var lines = new MotionEffectLines(data).GetEnumerator();
        while (lines.MoveNext())
        {
            var line = lines.Current;

            if (!seenLeading)
            {
                var head = line;
                if (TryReadInt(ref head, out leading))
                {
                    seenLeading = true;
                }

                continue;
            }

            if (TryReadRecord(line, ref lines, context, ref uncompiled, out var record))
            {
                records.Add(record);
            }
        }

        return records.Count == 0 && !seenLeading
            ? Empty
            : new MotionEffectTable([.. records], leading, context, uncompiled);
    }

    public MotionEffectRecord? Find(int id)
    {
        for (var i = 0; i < _records.Length; i++)
        {
            if (_records[i].Id == id)
            {
                return _records[i];
            }
        }

        return null;
    }

    public void SetVariables(double distance, double totalTime, double sampleTime)
    {
        Expressions.SetNumber(DistanceVariable, distance);
        Expressions.SetNumber(TotalTimeVariable, totalTime);
        Expressions.SetNumber(SampleTimeVariable, sampleTime);
    }

    public double Evaluate(MotionEffectRecord record, MotionEffectExpressionGroup group, int set = 0)
    {
        ArgumentNullException.ThrowIfNull(record);

        var handle = record.GetHandle(group, set);
        return handle < 0 ? 0 : Expressions.Evaluate(handle);
    }

    private static bool TryReadRecord(
        ReadOnlySpan<byte> line,
        ref MotionEffectLines.Enumerator lines,
        ExpressionContext context,
        ref int uncompiled,
        out MotionEffectRecord record)
    {
        record = null!;

        var rest = line;
        if (!TryReadInt(ref rest, out var id)
            || !TryReadInt(ref rest, out var type)
            || !TryReadInt(ref rest, out var frames))
        {
            return false;
        }

        SkipSpace(ref rest);

        MotionEffectMode mode;
        if (StartsWithToken(rest, "distance"u8))
        {
            mode = MotionEffectMode.Distance;
            rest = rest["distance"u8.Length..];
        }
        else if (StartsWithToken(rest, "time"u8))
        {
            mode = MotionEffectMode.Time;
            rest = rest["time"u8.Length..];
        }
        else
        {
            return false;
        }

        var step = 0;
        var vertical = -1;
        var horizontal = -1;

        if (mode == MotionEffectMode.Distance)
        {
            if (!TryReadInt(ref rest, out step) || !TryReadInt(ref rest, out var distanceDelay))
            {
                return false;
            }

            var tail = rest;
            if (TryReadInt(ref tail, out var v))
            {
                SkipSpace(ref tail);
                if (!tail.IsEmpty && tail[0] == (byte)'/')
                {
                    tail = tail[1..];
                    if (TryReadInt(ref tail, out var h))
                    {
                        vertical = v;
                        horizontal = h;
                    }
                }
            }

            record = new MotionEffectRecord(id, type, frames, mode, step, distanceDelay, vertical, horizontal, [], []);
            return true;
        }

        if (!TryReadInt(ref rest, out var timeDelay))
        {
            return false;
        }

        var sets = new List<string[]>(MaxSets) { ReadExpressions(rest) };

        while (sets.Count < MaxSets && lines.TryPeek(out var next))
        {
            if (next[0] == SetTerminator)
            {
                lines.MoveNext();
                break;
            }

            if (next[0] != ExpressionMarker)
            {
                break;
            }

            lines.MoveNext();
            sets.Add(ReadExpressions(next));
        }

        var handles = new int[sets.Count * GroupCount];
        for (var set = 0; set < sets.Count; set++)
        {
            var fields = sets[set];
            for (var group = 0; group < GroupCount; group++)
            {
                var slot = (set * GroupCount) + group;
                if (group >= fields.Length || !context.TryCompile(Encoding.ASCII.GetBytes(fields[group]), out var handle))
                {
                    handles[slot] = -1;
                    uncompiled++;
                    continue;
                }

                handles[slot] = handle;
            }
        }

        record = new MotionEffectRecord(
            id, type, frames, mode, step, timeDelay, vertical, horizontal, [.. sets], handles);
        return true;
    }

    private static string[] ReadExpressions(ReadOnlySpan<byte> rest)
    {
        List<string>? found = null;

        while (true)
        {
            var open = rest.IndexOf(ExpressionMarker);
            if (open < 0)
            {
                break;
            }

            rest = rest[(open + 1)..];

            var close = rest.IndexOf(ExpressionMarker);
            if (close < 0)
            {
                break;
            }

            found ??= [];
            found.Add(Encoding.ASCII.GetString(rest[..close]).Trim());
            rest = rest[(close + 1)..];
        }

        return found is null ? [] : [.. found];
    }

    private static bool StartsWithToken(ReadOnlySpan<byte> value, ReadOnlySpan<byte> token) =>
        value.Length >= token.Length && value[..token.Length].SequenceEqual(token);

    private static void SkipSpace(ref ReadOnlySpan<byte> value)
    {
        while (!value.IsEmpty && value[0] is (byte)' ' or (byte)'\t')
        {
            value = value[1..];
        }
    }

    private static bool TryReadInt(ref ReadOnlySpan<byte> value, out int result)
    {
        SkipSpace(ref value);

        result = 0;
        var negative = !value.IsEmpty && value[0] == (byte)'-';
        if (negative)
        {
            value = value[1..];
        }

        var digits = 0;
        while (!value.IsEmpty && (uint)(value[0] - (byte)'0') <= 9)
        {
            result = (result * 10) + (value[0] - (byte)'0');
            value = value[1..];
            digits++;
        }

        if (digits == 0)
        {
            return false;
        }

        if (negative)
        {
            result = -result;
        }

        return true;
    }

    private readonly ref struct MotionEffectLines(ReadOnlySpan<byte> data)
    {
        private readonly ReadOnlySpan<byte> _data = data;

        public Enumerator GetEnumerator() => new(_data);

        public ref struct Enumerator(ReadOnlySpan<byte> data)
        {
            private ReadOnlySpan<byte> _remaining = data;

            public ReadOnlySpan<byte> Current { get; private set; }

            public readonly bool TryPeek(out ReadOnlySpan<byte> next)
            {
                var probe = this;
                if (probe.MoveNext())
                {
                    next = probe.Current;
                    return true;
                }

                next = default;
                return false;
            }

            public bool MoveNext()
            {
                while (!_remaining.IsEmpty)
                {
                    var raw = TableReader.TakeLine(ref _remaining, CommentMarker);

                    while (!raw.IsEmpty && raw[^1] is (byte)' ' or (byte)'\t')
                    {
                        raw = raw[..^1];
                    }

                    while (!raw.IsEmpty && raw[0] is (byte)' ' or (byte)'\t')
                    {
                        raw = raw[1..];
                    }

                    if (!raw.IsEmpty)
                    {
                        Current = raw;
                        return true;
                    }
                }

                return false;
            }
        }
    }
}
