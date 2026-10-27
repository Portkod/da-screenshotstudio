using DaScreenshotStudio.Assets.Tables;

namespace DaScreenshotStudio.Assets.Effects;

public sealed class EffectFrameTable
{
    private readonly byte[] _frames;
    private readonly int[] _starts;

    private EffectFrameTable(byte[] frames, int[] starts)
    {
        _frames = frames;
        _starts = starts;
    }

    public int Count => _starts.Length - 1;

    public int DeclaredCount { get; private init; }

    public static EffectFrameTable Empty { get; } = new([], [0]) { DeclaredCount = 0 };

    public static EffectFrameTable Parse(ReadOnlySpan<byte> data)
    {
        var reader = new TableReader(data);
        if (!reader.TryReadLine(out var header) || !header.TryReadInt(out var declared) || declared <= 0)
        {
            return Empty;
        }

        List<byte>? frames = null;

        var starts = new List<int>(Math.Min(declared, data.Length) + 1) { 0 };

        while (starts.Count <= declared && reader.TryReadLine(out var line))
        {
            frames ??= [];

            while (line.TryReadInt(out var frame))
            {
                if ((uint)frame >= 0xFF)
                {
                    continue;
                }

                frames.Add((byte)frame);
            }

            starts.Add(frames.Count);
        }

        return new EffectFrameTable(frames is null ? [] : [.. frames], [.. starts]) { DeclaredCount = declared };
    }

    public ReadOnlySpan<byte> GetSequence(int effectId)
    {
        if ((uint)effectId >= (uint)Count)
        {
            return default;
        }

        var start = _starts[effectId];
        return _frames.AsSpan(start, _starts[effectId + 1] - start);
    }
}
