using DaScreenshotStudio.Assets.Effects;
using DaScreenshotStudio.Engine.Core;

namespace DaScreenshotStudio.Engine.World;

public sealed class MovingEffects
{
    public const int Capacity = 64;

    public const int DrawLayer = WorldDrawLayer.ObjectEffect;

    public const int DirectionCount = 4;

    private readonly Effect[] _effects = new Effect[Capacity];
    private readonly MotionEffectRecord?[] _records = new MotionEffectRecord?[Capacity];
    private int _count;

    public int Count => _count;

    public Effect this[int index] => _effects[index];

    public void Clear()
    {
        _count = 0;
        Array.Clear(_records);
    }

    public bool Show(
        int motionIndex,
        MotionEffectRecord? record,
        MotionEffectTable table,
        in MapPoint source,
        in MapPoint target,
        uint now,
        EntityId targetEntity = default)
    {
        ArgumentNullException.ThrowIfNull(table);

        if (record is null || (record.Mode == MotionEffectMode.Time && record.StepDelay <= 0))
        {
            return false;
        }

        if (_count == Capacity)
        {
            for (var i = 1; i < Capacity; i++)
            {
                _effects[i - 1] = _effects[i];
                _records[i - 1] = _records[i];
            }

            _count--;
        }

        _effects[_count] = Build(motionIndex, record, table, in source, in target, now, targetEntity);
        _records[_count] = record;
        _count++;
        return true;
    }

    public bool Tick(uint now, MotionEffectTable table, WorldObjectTable? objects = null)
    {
        ArgumentNullException.ThrowIfNull(table);

        if (_count == 0)
        {
            return objects?.SweepImpactTints(now) ?? false;
        }

        var changed = false;

        for (var i = _count - 1; i >= 0; i--)
        {
            if (_records[i] is not { } record)
            {
                continue;
            }

            var retire = false;

            while (App.GameClock.IsDue(now, _effects[i].Due))
            {
                if (_effects[i].Step >= _effects[i].StepCount)
                {
                    retire = true;
                    break;
                }

                Advance(ref _effects[i], record, table);
                changed = true;
            }

            if (!retire)
            {
                continue;
            }

            objects?.SetImpactTint(
                _effects[i].TargetEntity, unchecked(now + (uint)WorldRenderer.ImpactDurationMs));

            for (var j = i; j < _count - 1; j++)
            {
                _effects[j] = _effects[j + 1];
                _records[j] = _records[j + 1];
            }

            _count--;
            _records[_count] = null;
            changed = true;
        }

        changed |= objects?.SweepImpactTints(now) ?? false;
        return changed;
    }

    private static void Advance(ref Effect effect, MotionEffectRecord record, MotionEffectTable table)
    {
        PointAt(in effect, record, table, effect.Step, out var x, out var y);

        effect.X = x;
        effect.Y = y;
        effect.Frame = effect.Frames > 0 ? (effect.Frame + 1) % effect.Frames : effect.Frame;
        effect.Step++;
        effect.Due = unchecked(effect.Due + (uint)effect.StepDelayMs);
    }

    private static void PointAt(
        in Effect effect,
        MotionEffectRecord record,
        MotionEffectTable table,
        int step,
        out int x,
        out int y)
    {
        if (record.Mode == MotionEffectMode.Time)
        {
            TimePoint(in effect, record, table, step, out x, out y);
            return;
        }

        var divisor = effect.StepCount + 1;
        x = effect.SourceX + (effect.DeltaX * (step + 1) / divisor);
        y = effect.SourceY + (effect.DeltaY * (step + 1) / divisor);

        var half = effect.Distance / 2;
        var halfSquared = half * half;
        if (halfSquared <= 0 || effect.Horizontal <= 0)
        {
            return;
        }

        var travelledX = x - effect.SourceX;
        var travelledY = y - effect.SourceY;
        var travelled = (int)Math.Sqrt((travelledX * travelledX) + (double)(travelledY * travelledY));

        var peak = half * effect.Vertical / effect.Horizontal;
        var fromMiddle = travelled - half;
        y += (fromMiddle * fromMiddle * peak / halfSquared) - peak;
    }

    private static void TimePoint(
        in Effect effect,
        MotionEffectRecord record,
        MotionEffectTable table,
        int step,
        out int x,
        out int y)
    {
        table.SetVariables(effect.Length, effect.TotalTime, (double)record.StepDelay * step);

        var across = table.Evaluate(record, MotionEffectExpressionGroup.X, effect.Set);
        var along = table.Evaluate(record, MotionEffectExpressionGroup.Y, effect.Set);
        var lift = table.Evaluate(record, MotionEffectExpressionGroup.Z, effect.Set);

        x = effect.SourceX + (int)((effect.UnitY * across) + (effect.UnitX * along));
        y = effect.SourceY + (int)((effect.UnitY * along) - (effect.UnitX * across)) - (int)lift;
    }

    private static Effect Build(
        int motionIndex,
        MotionEffectRecord record,
        MotionEffectTable table,
        in MapPoint source,
        in MapPoint target,
        uint now,
        EntityId targetEntity)
    {
        IsometricProjection.CellAnchor(source.X, source.Y, out var sourceX, out var sourceY);
        IsometricProjection.CellAnchor(target.X, target.Y, out var targetX, out var targetY);

        var deltaX = targetX - sourceX;
        var deltaY = targetY - sourceY;
        var length = Math.Sqrt((deltaX * deltaX) + (double)(deltaY * deltaY));

        var set = 0;
        var totalTime = 0.0;
        var unitX = 0.0;
        var unitY = 0.0;
        int stepCount;

        if (record.Mode == MotionEffectMode.Time)
        {
            if (length != 0)
            {
                unitX = deltaX / length;
                unitY = deltaY / length;
            }

            set = record.SetCount <= 1 ? 0 : Random.Shared.Next(record.SetCount);

            table.Expressions.SetNumber(MotionEffectTable.DistanceVariable, length);
            totalTime = (int)table.Evaluate(record, MotionEffectExpressionGroup.Step, set);
            stepCount = (int)totalTime / record.StepDelay;
        }
        else
        {
            stepCount = record.Step <= 0 ? 0 : (int)length / record.Step;
        }

        return new Effect
        {
            MotionIndex = motionIndex,
            TargetEntity = targetEntity,
            SourceX = sourceX,
            SourceY = sourceY,
            DeltaX = deltaX,
            DeltaY = deltaY,
            Distance = (int)length,
            Length = length,
            UnitX = unitX,
            UnitY = unitY,
            TotalTime = totalTime,
            Set = set,
            StepCount = Math.Max(stepCount, 0),
            StepDelayMs = record.StepDelay,
            Frames = record.Frames,
            Vertical = KeepsItsOwnArc(table, record) ? record.Vertical : ErasedVertical,
            Horizontal = record.Horizontal,
            HasDirections = record.Type != 0,
            Direction = record.Type == 0 ? Direction.Up : DirectionBetween(in source, in target),
            Frame = -1,
            Due = unchecked(now + (uint)record.StepDelay),
        };
    }

    public static bool KeepsItsOwnArc(MotionEffectTable table, MotionEffectRecord record)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(record);

        var records = table.Records;
        var last = -1;

        for (var i = 0; i < records.Count; i++)
        {
            if (records[i].Id >= 0 && records[i].Id < table.LeadingValue)
            {
                last = i;
            }
        }

        return last >= 0 && ReferenceEquals(records[last], record);
    }

    private const int ErasedVertical = 0;

    public static Direction DirectionBetween(in MapPoint source, in MapPoint target)
    {
        var acrossX = target.X - source.X;
        var acrossY = target.Y - source.Y;

        var direction = Math.Abs(acrossX) >= Math.Abs(acrossY) ? Direction.Right : Direction.Up;

        if (acrossY > 0 && direction == Direction.Up)
        {
            direction = Direction.Down;
        }

        if (acrossX < 0 && direction == Direction.Right)
        {
            direction = Direction.Left;
        }

        return direction;
    }

    public struct Effect
    {
        public int MotionIndex { get; init; }

        public EntityId TargetEntity { get; init; }

        public int SourceX { get; init; }

        public int SourceY { get; init; }

        public int DeltaX { get; init; }

        public int DeltaY { get; init; }

        public int Distance { get; init; }

        public double Length { get; init; }

        public double UnitX { get; init; }

        public double UnitY { get; init; }

        public double TotalTime { get; init; }

        public int Set { get; init; }

        public int StepCount { get; init; }

        public int StepDelayMs { get; init; }

        public int Frames { get; init; }

        public int Vertical { get; init; }

        public int Horizontal { get; init; }

        public bool HasDirections { get; init; }

        public Direction Direction { get; init; }

        public int Step { get; internal set; }

        public int Frame { get; internal set; }

        public int X { get; internal set; }

        public int Y { get; internal set; }

        public uint Due { get; internal set; }

        public readonly bool IsVisible => Step > 0 && Frame >= 0;

        public readonly int SheetFrame => Frame < 0
            ? 0
            : HasDirections ? (int)Direction + (Frame * DirectionCount) : Frame;
    }
}
