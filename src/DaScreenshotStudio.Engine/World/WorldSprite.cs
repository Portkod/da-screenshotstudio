using DaScreenshotStudio.Assets.Characters;
using DaScreenshotStudio.Assets.Effects;
using DaScreenshotStudio.Assets.Imaging;
using DaScreenshotStudio.Engine.Rendering;
using DaScreenshotStudio.Engine.Core;

namespace DaScreenshotStudio.Engine.World;

public readonly struct WorldSprite
{
    public WorldSprite(in Pixmap art, int anchorX, int anchorY)
        : this(in art, anchorX, anchorY, default)
    {
    }

    public WorldSprite(in Pixmap art, int anchorX, int anchorY, ReadOnlyMemory<byte> alphaWeights)
    {
        Art = art;
        AnchorX = anchorX;
        AnchorY = anchorY;
        AlphaWeights = alphaWeights;
    }

    public Pixmap Art { get; }

    public ReadOnlyMemory<byte> AlphaWeights { get; }

    public int AnchorX { get; }

    public int AnchorY { get; }

    public bool IsEmpty => Art.IsEmpty;
}

public readonly struct SpriteBounds
{
    private SpriteBounds(int top, int left, int bottom, int right)
    {
        Top = top;
        Left = left;
        Bottom = bottom;
        Right = right;
        IsKnown = true;
    }

    public bool IsKnown { get; }

    public int Top { get; }

    public int Left { get; }

    public int Bottom { get; }

    public int Right { get; }

    public bool IsDegenerate => Right <= Left || Bottom <= Top;

    public static SpriteBounds Minimum => new(-5, -5, 5, 5);

    public static SpriteBounds Unit => new(0, 0, 1, 1);

    public static SpriteBounds FromSprite(in WorldSprite sprite) => new(
        -sprite.AnchorY,
        -sprite.AnchorX,
        sprite.Art.Height - sprite.AnchorY,
        sprite.Art.Width - sprite.AnchorX);
}

public interface IWorldObjectArt
{
    bool TryGetHuman(in HumanState appearance, Direction facing, int motion, int motionFrame, out WorldSprite sprite);

    bool TryGetHuman(in HumanState appearance, Direction facing, int motion, int motionFrame, int idleStep, out WorldSprite sprite) =>
        TryGetHuman(in appearance, facing, motion, motionFrame, out sprite);

    bool TryGetHuman(
        in HumanState appearance,
        Direction facing,
        int motion,
        int motionFrame,
        int framesPerView,
        int idleStep,
        out WorldSprite sprite) =>
        TryGetHuman(in appearance, facing, motion, motionFrame, idleStep, out sprite);

    bool TryGetHuman(
        in HumanState appearance,
        Direction facing,
        int motion,
        int motionFrame,
        int framesPerView,
        int idleStep,
        int emoteFrame,
        out WorldSprite sprite) =>
        TryGetHuman(in appearance, facing, motion, motionFrame, framesPerView, idleStep, out sprite);

    bool TryGetHumanVariant(
        in HumanState appearance,
        Direction facing,
        int motionFrame,
        int framesPerView,
        char variant,
        out WorldSprite sprite) =>
        TryGetHuman(in appearance, facing, 0, motionFrame, framesPerView, -1, out sprite);

    HumanSkillMotions SkillMotions => HumanSkillMotions.Empty;

    int HumanIdlePeriod(in HumanState appearance) => 1;

    void BeginFrame()
    {
    }

    bool TryGetCreature(
        int sprite,
        byte dye,
        Direction facing,
        int motionFrame,
        in CreatureColoring coloring,
        out WorldSprite result);

    bool TryPickCreatureStanding(int sprite, int roll, out MpfStandingSegment segment)
    {
        segment = default;
        return false;
    }

    bool TryGetCreatureStanding(
        int sprite,
        byte dye,
        Direction facing,
        in MpfStandingSegment segment,
        int segmentStep,
        in CreatureColoring coloring,
        out WorldSprite result)
    {
        result = default;
        return false;
    }

    int CreatureWalkSequence(int sprite) => 0;

    int NameLabelHeight(in WorldObjectEntry entry) => ObjectNamePane.HumanHeight;

    int MeterHeight(in WorldObjectEntry entry) => WorldRenderer.HumanMeterHeight;

    bool TryGetItem(int sprite, byte dye, out WorldSprite result);

    bool TryGetCreatureAttack(
        int sprite,
        byte dye,
        Direction facing,
        int attackRange,
        int motionFrame,
        in CreatureColoring coloring,
        out WorldSprite result)
    {
        result = default;
        return false;
    }

    int MotionFrameCount(in WorldObjectEntry entry, byte animation) =>
        HumanMotion.TryGetBodyMotion(animation, in entry.ActiveAppearance, SkillMotions, out var body) ? body.Steps : 0;

    int MotionDurationMs(in WorldObjectEntry entry, byte animation, int durationMs) => durationMs;

    DamageEffectAtlas? DamageMeters => null;

    EffectFrameTable EffectSequences => EffectFrameTable.Empty;

    bool TryGetEffectSequenceFrame(int effectId, int sequenceIndex, out int frame)
    {
        frame = 0;
        return false;
    }

    bool TryGetEffect(int effectId, int frameNumber, out WorldSprite result)
    {
        result = default;
        return false;
    }

    bool TryGetEffect(int effectId, int frameNumber, bool flipX, bool flipY, out WorldSprite result) =>
        TryGetEffect(effectId, frameNumber, out result);

    bool EffectFlipsWithDirection(int effectId) => false;

    int EffectFrameIntervalMs(int effectId) => 0;

    BlendMode EffectBlend(int effectId) => BlendMode.Transparent;

    MotionEffectTable MotionEffects => MotionEffectTable.Empty;

    MotionEffectRecord? MotionEffectFor(int motionIndex) => null;

    bool TryGetMotionEffect(int motionIndex, int sheetFrame, out WorldSprite result)
    {
        result = default;
        return false;
    }

    ObjectLightMask? ObjectLightMaskFor(int selector) => null;
}
