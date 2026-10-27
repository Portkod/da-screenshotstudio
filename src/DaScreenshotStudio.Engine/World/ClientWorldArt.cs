using DaScreenshotStudio.Assets.Archives;
using DaScreenshotStudio.Assets.Characters;
using DaScreenshotStudio.Assets.Effects;
using DaScreenshotStudio.Assets.Imaging;
using DaScreenshotStudio.Engine.Rendering;
using DaScreenshotStudio.Engine.Text;
using DaScreenshotStudio.Engine.Core;
using DaScreenshotStudio.Engine.Records;
using DaScreenshotStudio.Engine.App;

namespace DaScreenshotStudio.Engine.World;

public sealed class ClientWorldArt : IWorldObjectArt, IDisposable
{
    private readonly CharacterArt _characters;
    private readonly HumanImageCache _humans;
    private readonly MonsterArt? _monsters;
    private readonly GroundItemArt? _items;
    private readonly IDisposable?[] _owned;
    private bool _disposed;

    private readonly EffectArt? _effects;
    private readonly DamageEffectAtlas? _meters;
    private readonly IObjectLightMasks? _lightMasks;

    public ClientWorldArt(CharacterArt characters, MonsterArt? monsters, GroundItemArt? items, params IDisposable?[] owned)
        : this(characters, monsters, items, null, null, null, owned)
    {
    }

    public ClientWorldArt(
        CharacterArt characters,
        MonsterArt? monsters,
        GroundItemArt? items,
        EffectArt? effects,
        DamageEffectAtlas? meters,
        IObjectLightMasks? lightMasks,
        params IDisposable?[] owned)
    {
        ArgumentNullException.ThrowIfNull(characters);

        _characters = characters;
        _humans = new HumanImageCache(characters);
        _monsters = monsters;
        _items = items;
        _effects = effects;
        _meters = meters;
        _lightMasks = lightMasks;
        _owned = owned ?? [];
    }

    public CharacterArt Characters => _characters;

    public HumanSkillMotions SkillMotions => _characters.SkillMotions;

    public MonsterArt? Monsters => _monsters;

    public GroundItemArt? Items => _items;

    public static ClientWorldArt? TryLoad(ClientDirectory clientDirectory, in PixelPacking packing)
    {
        ArgumentNullException.ThrowIfNull(clientDirectory);

        var characters = CharacterArt.TryLoad(clientDirectory, in packing);
        if (characters is null)
        {
            return null;
        }

        DataArchive? hades = null;
        DataArchive? legend = null;
        IDisposable? effectArchive = null;

        try
        {
            hades = clientDirectory.TryOpenArchive("hades.dat");
            legend = clientDirectory.TryOpenArchive("Legend.dat");

            var palette = legend is null ? null : TextPalette.TryLoad(legend, in packing);

            return new ClientWorldArt(
                characters,
                hades is null ? null : new MonsterArt(hades, characters),
                legend is null ? null : new GroundItemArt(legend, characters),
                EffectArt.TryLoad(clientDirectory, in packing, out effectArchive),
                new DamageEffectAtlas((palette ?? TextPalette.Fallback(in packing)).Colors),

                legend is null ? null : new ObjectLightMasks(legend),
                hades,
                legend,
                effectArchive,
                characters);
        }
        catch
        {
            characters.Dispose();
            hades?.Dispose();
            legend?.Dispose();
            effectArchive?.Dispose();
            throw;
        }
    }

    public bool TryGetHuman(in HumanState appearance, Direction facing, int motion, int motionFrame, out WorldSprite sprite) =>
        _humans.TryGet(in appearance, facing, motion, motionFrame, HumanComposite.StandingFramesPerView, -1, out sprite);

    public bool TryGetHuman(in HumanState appearance, Direction facing, int motion, int motionFrame, int idleStep, out WorldSprite sprite) =>
        _humans.TryGet(in appearance, facing, motion, motionFrame, HumanComposite.StandingFramesPerView, idleStep, out sprite);

    public bool TryGetHuman(
        in HumanState appearance,
        Direction facing,
        int motion,
        int motionFrame,
        int framesPerView,
        int idleStep,
        out WorldSprite sprite) =>
        _humans.TryGet(in appearance, facing, motion, motionFrame, framesPerView, idleStep, out sprite);

    public bool TryGetHuman(
        in HumanState appearance,
        Direction facing,
        int motion,
        int motionFrame,
        int framesPerView,
        int idleStep,
        int emoteFrame,
        out WorldSprite sprite) =>
        _humans.TryGet(in appearance, facing, motion, motionFrame, framesPerView, idleStep, out sprite, emoteFrame);

    public bool TryGetHumanVariant(
        in HumanState appearance,
        Direction facing,
        int motionFrame,
        int framesPerView,
        char variant,
        out WorldSprite sprite) =>
        _humans.TryGet(in appearance, facing, 0, motionFrame, framesPerView, -1, out sprite, 0, variant);

    public int HumanIdlePeriod(in HumanState appearance) => _humans.IdlePeriod(in appearance);

    public void BeginFrame() => _humans.BeginFrame();

    public bool TryGetCreature(
        int sprite,
        byte dye,
        Direction facing,
        int motionFrame,
        in CreatureColoring coloring,
        out WorldSprite result)
    {
        if (_monsters is null)
        {
            result = default;
            return false;
        }

        return _monsters.TryGet(sprite, dye, facing, motionFrame, in coloring, out result);
    }

    public bool TryGetCreatureAttack(
        int sprite,
        byte dye,
        Direction facing,
        int attackRange,
        int motionFrame,
        in CreatureColoring coloring,
        out WorldSprite result)
    {
        if (_monsters is null)
        {
            result = default;
            return false;
        }

        return _monsters.TryGet(sprite, dye, facing, attackRange, motionFrame, in coloring, out result);
    }

    public bool TryPickCreatureStanding(int sprite, int roll, out MpfStandingSegment segment)
    {
        if (_monsters is null)
        {
            segment = default;
            return false;
        }

        return _monsters.TryPickStandingSegment(sprite, roll, out segment);
    }

    public bool TryGetCreatureStanding(
        int sprite,
        byte dye,
        Direction facing,
        in MpfStandingSegment segment,
        int segmentStep,
        in CreatureColoring coloring,
        out WorldSprite result)
    {
        if (_monsters is null)
        {
            result = default;
            return false;
        }

        return _monsters.TryGetStanding(sprite, dye, facing, in segment, segmentStep, in coloring, out result);
    }

    public int CreatureWalkSequence(int sprite) => _monsters?.WalkSequence(sprite) ?? 0;

    public int NameLabelHeight(in WorldObjectEntry entry)
    {
        if (entry.Kind is WorldObjectKind.Human)
        {
            return ObjectNamePane.HumanHeight;
        }

        return _monsters?.NameLabelHeight(Untag(entry.Sprite)) ?? 0;
    }

    public int MeterHeight(in WorldObjectEntry entry)
    {
        if (entry.Kind is WorldObjectKind.Human)
        {
            return WorldRenderer.HumanMeterHeight;
        }

        return _monsters?.MeterHeight(Untag(entry.Sprite)) ?? 0;
    }

    public int MotionFrameCount(in WorldObjectEntry entry, byte animation)
    {
        if (entry.Kind == WorldObjectKind.Human)
        {
            if (HumanMotion.TryGetBodyMotion(animation, in entry.ActiveAppearance, SkillMotions, out var body))
            {
                return body.Steps;
            }

            return HumanEmotes.TryGetMotion(animation, entry.Direction, out var emote) ? emote.Steps : 0;
        }

        if (_monsters is null)
        {
            return 0;
        }

        return _monsters.AttackFrameCount(Untag(entry.Sprite), CreatureMotion.AttackRangeFor(animation));
    }

    public int MotionDurationMs(in WorldObjectEntry entry, byte animation, int durationMs)
    {
        if (entry.Kind != WorldObjectKind.Human)
        {
            return durationMs;
        }

        if (HumanMotion.TryGetBodyMotion(animation, in entry.ActiveAppearance, SkillMotions, out var body))
        {
            return body.DurationDivisor > 1 ? durationMs / body.DurationDivisor : durationMs;
        }

        return HumanEmotes.TryGetMotion(animation, entry.Direction, out var emote) ? emote.DurationMs : durationMs;
    }

    public DamageEffectAtlas? DamageMeters => _meters;

    public EffectFrameTable EffectSequences => _effects?.Sequences ?? EffectFrameTable.Empty;

    public bool TryGetEffectSequenceFrame(int effectId, int sequenceIndex, out int frame)
    {
        if (_effects is null)
        {
            frame = 0;
            return false;
        }

        return _effects.TryGetSequenceFrame(effectId, sequenceIndex, out frame);
    }

    public bool TryGetEffect(int effectId, int frameNumber, out WorldSprite result) =>
        TryGetEffect(effectId, frameNumber, flipX: false, flipY: false, out result);

    public bool TryGetEffect(int effectId, int frameNumber, bool flipX, bool flipY, out WorldSprite result)
    {
        if (_effects is null)
        {
            result = default;
            return false;
        }

        return _effects.TryGetFrame(effectId, frameNumber, flipX, flipY, out result);
    }

    public bool EffectFlipsWithDirection(int effectId) => _effects?.FlipsWithDirection(effectId) ?? false;

    public int EffectFrameIntervalMs(int effectId) => _effects?.FrameIntervalMs(effectId) ?? 0;

    public BlendMode EffectBlend(int effectId) => _effects?.BlendFor(effectId) ?? BlendMode.Transparent;

    public MotionEffectTable MotionEffects => _effects?.Motions ?? MotionEffectTable.Empty;

    public MotionEffectRecord? MotionEffectFor(int motionIndex) => _effects?.MotionFor(motionIndex);

    public bool TryGetMotionEffect(int motionIndex, int sheetFrame, out WorldSprite result)
    {
        if (_effects is null)
        {
            result = default;
            return false;
        }

        return _effects.TryGetMotionFrame(motionIndex, sheetFrame, out result);
    }

    public bool TryGetItem(int sprite, byte dye, out WorldSprite result)
    {
        if (_items is null)
        {
            result = default;
            return false;
        }

        return _items.TryGet(sprite, dye, out result);
    }

    public ObjectLightMask? ObjectLightMaskFor(int selector) => _lightMasks?.LoadObjectLightMask(selector);

    private static int Untag(ushort sprite) =>
        sprite >= TaggedSprite.CreatureTag ? sprite - TaggedSprite.CreatureTag : sprite;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (var owned in _owned)
        {
            owned?.Dispose();
        }
    }
}
