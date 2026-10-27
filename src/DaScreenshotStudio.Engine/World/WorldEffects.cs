using DaScreenshotStudio.Engine.Core;
using DaScreenshotStudio.Engine.Records;

namespace DaScreenshotStudio.Engine.World;

public sealed class WorldEffects
{
    public const int Capacity = 64;

    public const ushort NoOpAnimation = EffectLayer.NoOpAnimation;

    private readonly Effect[] _effects = new Effect[Capacity];
    private int _count;

    public int Count => _count;

    public Effect this[int index] => _effects[index];

    public bool ShowAt(in MapPoint position, int effectId, uint intervalMs, uint now) =>
        Add(new Effect(default, position, effectId, intervalMs, now, attached: false));

    public static void FlipsFor(Direction direction, out bool flipX, out bool flipY)
    {
        flipX = direction is Direction.Down or Direction.Left;
        flipY = direction is Direction.Up or Direction.Left;
    }

    public bool ShowOn(EntityId target, int effectId, uint intervalMs, uint now, Direction direction = Direction.Down) =>
        !target.IsNone && Add(new Effect(target, default, effectId, intervalMs, now, attached: true, direction));

    public bool Tick(uint now, IWorldObjectArt? art)
    {
        if (_count == 0)
        {
            return false;
        }

        var changed = false;

        for (var i = _count - 1; i >= 0; i--)
        {
            var effect = _effects[i];
            var index = (int)Math.Min((uint)effect.Elapsed(now) / effect.IntervalMs, int.MaxValue);

            if (art is not null && art.TryGetEffectSequenceFrame(effect.EffectId, index, out var frame))
            {
                if (frame == effect.Frame)
                {
                    continue;
                }

                _effects[i] = effect.WithFrame(frame);
                changed = true;
                continue;
            }

            for (var j = i; j < _count - 1; j++)
            {
                _effects[j] = _effects[j + 1];
            }

            _count--;
            changed = true;
        }

        return changed;
    }

    public void Clear() => _count = 0;

    public void Remove(EntityId target)
    {
        for (var i = _count - 1; i >= 0; i--)
        {
            if (!_effects[i].IsAttached || _effects[i].Target != target)
            {
                continue;
            }

            for (var j = i; j < _count - 1; j++)
            {
                _effects[j] = _effects[j + 1];
            }

            _count--;
        }
    }

    private bool Add(in Effect effect)
    {
        if (effect.EffectId < 0 || effect.IntervalMs == 0)
        {
            return false;
        }

        if (_count == Capacity)
        {
            for (var i = 1; i < Capacity; i++)
            {
                _effects[i - 1] = _effects[i];
            }

            _count--;
        }

        _effects[_count++] = effect;
        return true;
    }

    public readonly struct Effect
    {
        public Effect(EntityId target, in MapPoint position, int effectId, uint intervalMs, uint started, bool attached, Direction direction = Direction.Down)
        {
            Target = target;
            Position = position;
            EffectId = effectId;
            IntervalMs = intervalMs;
            Started = started;
            IsAttached = attached;
            Direction = direction;
            Frame = 0;
        }

        private Effect(in Effect source, int frame)
        {
            Target = source.Target;
            Position = source.Position;
            EffectId = source.EffectId;
            IntervalMs = source.IntervalMs;
            Started = source.Started;
            IsAttached = source.IsAttached;
            Direction = source.Direction;
            Frame = frame;
        }

        public EntityId Target { get; }

        public MapPoint Position { get; }

        public int EffectId { get; }

        public uint IntervalMs { get; }

        public uint Started { get; }

        public bool IsAttached { get; }

        public Direction Direction { get; }

        public int Frame { get; }

        public int DrawLayer => IsAttached ? WorldDrawLayer.ObjectEffect : WorldDrawLayer.StaticEffect;

        public int Elapsed(uint now) => (int)Math.Min(unchecked(now - Started), int.MaxValue);

        internal Effect WithFrame(int frame) => new(in this, frame);
    }
}
