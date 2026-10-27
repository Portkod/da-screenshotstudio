using DaScreenshotStudio.Assets.Imaging;
using DaScreenshotStudio.Engine.App;
using DaScreenshotStudio.Engine.Rendering;
using DaScreenshotStudio.Engine.Core;
using DaScreenshotStudio.Engine.Records;

namespace DaScreenshotStudio.Engine.World;

public sealed class DyingObjects
{
    public const int Capacity = 32;

    public const int DrawLayer = WorldDrawLayer.Monster;

    public const int StepIntervalMs = 0x3C;

    public const int StepCount = 9;

    public const int WeightStep = 3;

    public const int LifetimeMs = (StepCount + 1) * StepIntervalMs;

    private readonly Entry[] _entries = new Entry[Capacity];
    private int _count;

    public int Count => _count;

    public ref readonly Entry this[int index] => ref _entries[index];

    public void Clear() => _count = 0;

    public bool Show(IWorldObjectArt? art, WorldObjectTable objects, int slot, uint now)
    {
        ArgumentNullException.ThrowIfNull(objects);

        if (art is null || !objects.IsOccupied(slot))
        {
            return false;
        }

        ref readonly var entry = ref objects.EntryAt(slot);
        if (entry.Kind is not (WorldObjectKind.Creature or WorldObjectKind.Mundane))
        {
            return false;
        }

        var candidate = Freeze(
            in entry,
            objects.WalkAt(slot),
            objects.MotionAt(slot),
            objects.StandingAt(slot),
            objects.ColoringAt(slot),
            now);
        if (!TryResolve(art, in candidate, out var sprite) || sprite.Art.IsEmpty)
        {
            return false;
        }

        candidate.Shift = ShiftFor(sprite.Art.Width);

        if (_count == Capacity)
        {
            for (var i = 1; i < Capacity; i++)
            {
                _entries[i - 1] = _entries[i];
            }

            _count--;
        }

        _entries[_count] = candidate;
        _count++;
        return true;
    }

    public bool Tick(uint now)
    {
        if (_count == 0)
        {
            return false;
        }

        var changed = false;

        for (var i = _count - 1; i >= 0; i--)
        {
            var retire = false;

            while (GameClock.IsDue(now, _entries[i].NextStepDue))
            {
                if (_entries[i].Step >= StepCount)
                {
                    retire = true;
                    break;
                }

                _entries[i].Step++;
                _entries[i].NextStepDue = unchecked(_entries[i].NextStepDue + StepIntervalMs);
                changed = true;
            }

            if (!retire)
            {
                continue;
            }

            for (var j = i; j < _count - 1; j++)
            {
                _entries[j] = _entries[j + 1];
            }

            _count--;
            changed = true;
        }

        return changed;
    }

    public static int ShiftFor(int frameWidth)
    {
        var width = frameWidth > 300 ? frameWidth / 5
            : frameWidth > 120 ? frameWidth / 3
            : frameWidth / 2;

        var shift = Math.Max(width / 9, 2);
        return shift / 2 * 2;
    }

    public static int WeightAt(int step) => PixelBlender.MaxAlphaWeight - (WeightStep * step);

    public static bool TryResolve(IWorldObjectArt art, in Entry entry, out WorldSprite sprite)
    {
        ArgumentNullException.ThrowIfNull(art);

        var coloring = entry.Coloring;

        switch (entry.Sequence)
        {
            case DyingFrame.Attack:
                return art.TryGetCreatureAttack(
                    entry.Sprite,
                    entry.Dye,
                    entry.Direction,
                    entry.AttackRange,
                    entry.FrameStep,
                    in coloring,
                    out sprite);

            case DyingFrame.Walk:
                return art.TryGetCreature(
                    entry.Sprite, entry.Dye, entry.Direction, entry.FrameStep, in coloring, out sprite);

            case DyingFrame.Standing:
                var segment = entry.Segment;
                return art.TryGetCreatureStanding(
                    entry.Sprite, entry.Dye, entry.Direction, in segment, entry.FrameStep, in coloring, out sprite);

            default:
                return art.TryGetCreature(entry.Sprite, entry.Dye, entry.Direction, 0, in coloring, out sprite);
        }
    }

    private static Entry Freeze(
        in WorldObjectEntry entry,
        WalkState walk,
        MotionState motion,
        CreatureStanding standing,
        in CreatureColoring coloring,
        uint now)
    {
        var result = new Entry
        {
            Cell = entry.Position,
            WalkOffsetX = walk.OffsetX,
            WalkOffsetY = walk.OffsetY,
            Sprite = entry.Sprite >= TaggedSprite.CreatureTag ? entry.Sprite - TaggedSprite.CreatureTag : entry.Sprite,
            Dye = entry.DyeColor,
            Coloring = coloring,
            Direction = entry.Direction,
            NextStepDue = unchecked(now + StepIntervalMs),
        };

        if (motion.IsActive)
        {
            result.Sequence = DyingFrame.Attack;
            result.AttackRange = CreatureMotion.AttackRangeFor(motion.Animation);
            result.FrameStep = motion.Frame;
            return result;
        }

        if (walk.Step >= 0)
        {
            result.Sequence = DyingFrame.Walk;
            result.FrameStep = walk.Step;
            return result;
        }

        if (standing.HasSegment)
        {
            result.Sequence = DyingFrame.Standing;
            result.Segment = standing.Segment;
            result.FrameStep = standing.Step;
            return result;
        }

        return result;
    }

    public enum DyingFrame
    {
        Idle,

        Walk,

        Standing,

        Attack,
    }

    public struct Entry
    {
        public MapPoint Cell { get; init; }

        public int WalkOffsetX { get; init; }

        public int WalkOffsetY { get; init; }

        public int Sprite { get; init; }

        public byte Dye { get; init; }

        public CreatureColoring Coloring { get; init; }

        public Direction Direction { get; init; }

        public DyingFrame Sequence { get; internal set; }

        public int AttackRange { get; internal set; }

        public MpfStandingSegment Segment { get; internal set; }

        public int FrameStep { get; internal set; }

        public int Shift { get; internal set; }

        public int Step { get; internal set; }

        public uint NextStepDue { get; internal set; }

        public readonly int RowShift => Step * Shift;

        public readonly int AnchorShift => Step * (Shift / 2);

        public readonly int Weight => WeightAt(Step);
    }
}
