namespace DaScreenshotStudio.Assets.Characters;

public readonly struct HumanIdleMotion
{
    public const int MotionIndex = 3;

    public const int MinimumFrameCount = 2;

    public const int MaxPeriod = 1024;

    private readonly CycleTable _cycles;
    private readonly int _period;

    private HumanIdleMotion(in CycleTable cycles, int period)
    {
        _cycles = cycles;
        _period = period;
    }

    public int Period => _period <= 0 ? 1 : _period;

    public static HumanIdleMotion Build(ReadOnlySpan<int> frameCounts)
    {
        CycleTable cycles = default;
        var period = 1;
        var any = false;

        for (var category = 0; category < HumanPartCategory.Count && category < frameCounts.Length; category++)
        {
            var frames = frameCounts[category];
            if (frames < MinimumFrameCount)
            {
                continue;
            }

            var cycle = Math.Min(frames / 2, byte.MaxValue);
            cycles[category] = (byte)cycle;
            period = LeastCommonMultiple(period, cycle);
            any = true;
        }

        return any && period <= MaxPeriod ? new HumanIdleMotion(in cycles, period) : default;
    }

    public int CycleAt(int category) =>
        _period > 0 && (uint)category < HumanPartCategory.Count ? _cycles[category] : 0;

    [System.Runtime.CompilerServices.InlineArray(HumanPartCategory.Count)]
    private struct CycleTable
    {
        private byte _first;
    }

    public bool TryGetFrame(int category, int step, out int frame)
    {
        var cycle = CycleAt(category);
        if (cycle <= 0 || step < 0)
        {
            frame = 0;
            return false;
        }

        frame = step % cycle;
        return true;
    }

    private static int LeastCommonMultiple(int a, int b) =>
        (int)Math.Min((long)a / GreatestCommonDivisor(a, b) * b, MaxPeriod + 1L);

    private static int GreatestCommonDivisor(int a, int b)
    {
        while (b != 0)
        {
            (a, b) = (b, a % b);
        }

        return a;
    }
}
