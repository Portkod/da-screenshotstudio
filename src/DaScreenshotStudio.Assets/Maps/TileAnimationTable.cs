using DaScreenshotStudio.Assets.Tables;

namespace DaScreenshotStudio.Assets.Maps;

public sealed class TileAnimationTable
{
    private readonly int[] _frames;
    private readonly int[] _cycleStart;
    private readonly int[] _cycleLength;
    private readonly int[] _cycleDelay;
    private readonly int[] _cycleOfId;
    private readonly int[] _phaseOfId;

    private TileAnimationTable(int[] frames, int[] cycleStart, int[] cycleLength, int[] cycleDelay, int[] cycleOfId, int[] phaseOfId)
    {
        _frames = frames;
        _cycleStart = cycleStart;
        _cycleLength = cycleLength;
        _cycleDelay = cycleDelay;
        _cycleOfId = cycleOfId;
        _phaseOfId = phaseOfId;
    }

    public int AnimatedIdCount { get; private init; }

    public static TileAnimationTable Empty { get; } = new([], [], [], [], [], []);

    public static TileAnimationTable Parse(ReadOnlySpan<byte> data)
    {
        var frames = new List<int>();
        var starts = new List<int>();
        var lengths = new List<int>();
        var delays = new List<int>();
        var values = new List<int>();
        var highestId = -1;

        var reader = new TableReader(data);
        while (reader.TryReadLine(out var line))
        {
            values.Clear();
            while (line.TryReadInt(out var value))
            {
                values.Add(value);
            }

            if (values.Count < 2)
            {
                continue;
            }

            var delay = values[^1];
            var count = values.Count - 1;
            var valid = delay > 0;
            for (var i = 0; i < count && valid; i++)
            {
                valid = (uint)values[i] <= MapCell.MaxTileId;
            }

            if (!valid)
            {
                continue;
            }

            starts.Add(frames.Count);
            lengths.Add(count);
            delays.Add(delay);

            for (var i = 0; i < count; i++)
            {
                frames.Add(values[i]);
                highestId = Math.Max(highestId, values[i]);
            }
        }

        if (highestId < 0)
        {
            return Empty;
        }

        var cycleOfId = new int[highestId + 1];
        var phaseOfId = new int[highestId + 1];
        cycleOfId.AsSpan().Fill(-1);

        var animated = 0;
        for (var cycle = 0; cycle < starts.Count; cycle++)
        {
            for (var phase = 0; phase < lengths[cycle]; phase++)
            {
                var id = frames[starts[cycle] + phase];
                if (cycleOfId[id] != -1)
                {
                    continue;
                }

                cycleOfId[id] = cycle;
                phaseOfId[id] = phase;
                animated++;
            }
        }

        return new TileAnimationTable([.. frames], [.. starts], [.. lengths], [.. delays], cycleOfId, phaseOfId)
        {
            AnimatedIdCount = animated,
        };
    }

    public int IdLimit => _cycleOfId.Length;

    public void WriteFrames(long ticks, Span<ushort> frames)
    {
        for (var cycle = 0; cycle < _cycleStart.Length; cycle++)
        {
            var length = _cycleLength[cycle];
            if (length <= 1)
            {
                continue;
            }

            var start = _cycleStart[cycle];
            var advance = (int)(ticks / _cycleDelay[cycle] % length);
            if (advance < 0)
            {
                advance += length;
            }

            for (var phase = 0; phase < length; phase++)
            {
                var id = _frames[start + phase];

                if (_cycleOfId[id] != cycle || _phaseOfId[id] != phase)
                {
                    continue;
                }

                var step = advance + phase;
                if (step >= length)
                {
                    step -= length;
                }

                frames[id] = (ushort)_frames[start + step];
            }
        }
    }

    public bool IsAnimated(int tileId) =>
        (uint)tileId < (uint)_cycleOfId.Length && _cycleOfId[tileId] >= 0 && _cycleLength[_cycleOfId[tileId]] > 1;

    public int ResolveFrame(int tileId, long ticks)
    {
        if ((uint)tileId >= (uint)_cycleOfId.Length)
        {
            return tileId;
        }

        var cycle = _cycleOfId[tileId];
        if (cycle < 0)
        {
            return tileId;
        }

        var length = _cycleLength[cycle];
        if (length <= 1)
        {
            return tileId;
        }

        var step = ((ticks / _cycleDelay[cycle]) + _phaseOfId[tileId]) % length;
        if (step < 0)
        {
            step += length;
        }

        return _frames[_cycleStart[cycle] + (int)step];
    }
}
