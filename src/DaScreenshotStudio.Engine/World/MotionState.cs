using DaScreenshotStudio.Assets.Characters;
using DaScreenshotStudio.Assets.Imaging;

namespace DaScreenshotStudio.Engine.World;

public struct MotionState
{
    public const byte AssaultAnimation = 0x01;

    public const byte StandingAnimation = 0x00;

    public byte Animation { get; private set; }

    public int StandingStep { get; private set; }

    public byte Frame { get; private set; }

    public byte FrameCount { get; private set; }

    public readonly bool IsActive => Animation != StandingAnimation;

    public static MotionState Posed(byte animation, byte frame, int standingStep) => new()
    {
        Animation = animation,
        Frame = frame,
        FrameCount = (byte)Math.Min(frame + 1, byte.MaxValue),
        StandingStep = standingStep,
    };

    public void RestartStanding()
    {
        StandingStep = 0;
    }

    public void Cancel()
    {
        Animation = StandingAnimation;
        Frame = 0;
        FrameCount = 0;
        RestartStanding();
    }
}

public struct CreatureStanding
{
    public MpfStandingSegment Segment { get; private set; }

    public int Step { get; private set; }

    public bool HasSegment { get; private set; }

    public void Restart()
    {
        Segment = default;
        Step = 0;
        HasSegment = false;
    }

    public static CreatureStanding Posed(in MpfStandingSegment segment, int step) => new()
    {
        Segment = segment,
        Step = step,
        HasSegment = true,
    };
}

public static class CreatureMotion
{
    public static int AttackRangeFor(byte animation) => animation switch
    {
        0x83 => 1,
        0x85 => 2,
        _ => 0,
    };
}

public readonly struct HumanBodyMotion
{
    public HumanBodyMotion(
        int motionIndex,
        int startFrame,
        int framesPerView,
        int repeat,
        int durationDivisor = 1,
        char variant = '\0')
    {
        MotionIndex = motionIndex;
        StartFrame = startFrame;
        FramesPerView = framesPerView;
        Steps = framesPerView * repeat;
        DurationDivisor = durationDivisor;
        Variant = variant;
    }

    public int MotionIndex { get; }

    public int StartFrame { get; }

    public int FramesPerView { get; }

    public int Steps { get; }

    public int DurationDivisor { get; }

    public char Variant { get; }

    public int FrameAt(int step) => StartFrame + (step % FramesPerView);
}

public static class HumanMotion
{
    public const int AssaultMotionIndex = 1;

    public const int StandingMotionIndex = 0;

    public const int BodyMotionIndex = 2;

    public const byte SpellAnimation = 0x06;

    public const byte KissAnimation = 0x15;

    public const byte WaveAnimation = 0x16;

    public static bool TryGetBodyMotion(byte animation, out HumanBodyMotion motion)
    {
        switch (animation)
        {
            case MotionState.AssaultAnimation:
                motion = new HumanBodyMotion(AssaultMotionIndex, 0, 2, 1);
                return true;

            case SpellAnimation:
                motion = new HumanBodyMotion(BodyMotionIndex, 0, 1, 1);
                return true;

            case KissAnimation:
                motion = new HumanBodyMotion(BodyMotionIndex, 2, 2, 1);
                return true;

            case WaveAnimation:
                motion = new HumanBodyMotion(BodyMotionIndex, 6, 2, 2, durationDivisor: 3);
                return true;

            default:
                motion = default;
                return false;
        }
    }

    public static bool TryGetBodyMotion(
        byte animation,
        in HumanState state,
        HumanSkillMotions skills,
        out HumanBodyMotion motion)
    {
        if (TryGetBodyMotion(animation, out motion))
        {
            return true;
        }

        if (skills is null
            || !skills.TryGetMotion(
                animation,
                state.Appearance.WeaponSprite,
                state.Appearance.ArmorSprite,
                state.Appearance.OvercoatSprite,
                out var skill))
        {
            return false;
        }

        motion = new HumanBodyMotion(0, skill.StartFrame, skill.Frames, 1, variant: skill.Variant);
        return true;
    }
}
