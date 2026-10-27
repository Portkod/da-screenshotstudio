using DaScreenshotStudio.Engine.Core;

namespace DaScreenshotStudio.Engine.World;

public readonly struct HumanEmoteMotion(int startFrame, int steps, int durationMs)
{
    public int StartFrame { get; } = startFrame;

    public int Steps { get; } = steps;

    public int DurationMs { get; } = durationMs;

    public int FrameAt(int step) => StartFrame + step;
}

public static class HumanEmotes
{
    private const int MotionCount = 45;

    private static readonly Entry[] Table =
    [
        new(0x1031, 0, 9, 1, 2, 1500),
        new(0x1032, 1, 10, 2, 2, 1500),
        new(0x1033, 2, 11, 3, 2, 1500),
        new(0x1034, 3, 12, 4, 2, 1500),
        new(0x1035, 4, 13, 5, 2, 1500),
        new(0x1036, 5, 14, 6, 2, 1500),
        new(0x1037, 6, 15, 7, 2, 1500),
        new(0x1038, 7, 16, 8, 3, 1000),
        new(0x1039, 8, 17, 10, 3, 1000),
        new(0x1030, 12, -1, -1, 1, 1000),
        new(0x102D, 13, -1, -1, 1, 1000),
        new(0x3031, 14, 23, 12, 2, 1500),
        new(0x3032, 15, -1, -1, 2, 1500),
        new(0x3033, 16, -1, -1, 2, 1500),
        new(0x3034, 17, -1, -1, 2, 1500),
        new(0x3035, 18, -1, -1, 4, 500),
        new(0x3036, 19, -1, -1, 2, 1500),
        new(0x3037, 20, -1, -1, 2, 1500),
        new(0x3038, 21, -1, -1, 2, 1500),
        new(0x3039, 22, -1, -1, 2, 1500),
        new(0x3030, 23, -1, -1, 2, 1500),
        new(0x302D, 24, -1, -1, 2, 1500),
        new(0x2031, 25, 34, -1, 2, 1500),
        new(0x2032, 26, -1, -1, 2, 1500),
        new(0x2033, 27, -1, -1, 2, 1500),
        new(0x2034, 28, -1, -1, 2, 1500),
        new(0x2035, 29, -1, -1, 2, 1500),
        new(0x2036, 30, -1, -1, 2, 1500),
        new(0x2037, 31, -1, -1, 2, 1500),
        new(0x2038, 32, -1, -1, 2, 1500),
        new(0x2039, 33, -1, -1, 4, 500),
        new(0x2030, 34, -1, -1, 4, 500),
        new(0x202D, 35, -1, -1, 5, 500),
    ];

    private static readonly HumanEmoteMotion[] Motions = ResolveMotions();

    public static bool TryGetMotion(byte animation, Direction facing, out HumanEmoteMotion motion)
    {
        if (facing != Direction.Right && facing != Direction.Down || animation >= MotionCount)
        {
            motion = default;
            return false;
        }

        motion = Motions[animation];
        return motion.Steps > 0;
    }

    private static HumanEmoteMotion[] ResolveMotions()
    {
        var motions = new HumanEmoteMotion[MotionCount];
        var motionAccumulator = 0;
        var startAccumulator = 0;

        foreach (var entry in Table)
        {
            var motion = entry.Motion;
            if (motion < 0)
            {
                motion = motionAccumulator;
            }
            else
            {
                motionAccumulator = motion;
            }

            if (entry.StartFrame >= 0)
            {
                startAccumulator = entry.StartFrame;
            }

            if ((uint)motion < (uint)MotionCount && entry.Frames > 1)
            {
                motions[motion] = new HumanEmoteMotion(startAccumulator, entry.Frames - 1, entry.DurationMs);
            }

            startAccumulator += entry.Frames - 1;
            motionAccumulator++;
        }

        return motions;
    }

    private readonly struct Entry(
        int key, byte emotion, int motion, int startFrame, int frames, int durationMs)
    {
        public int Key { get; } = key;

        public byte Emotion { get; } = emotion;

        public int Motion { get; } = motion;

        public int StartFrame { get; } = startFrame;

        public int Frames { get; } = frames;

        public int DurationMs { get; } = durationMs;
    }
}
