using DaScreenshotStudio.Engine.App;
using DaScreenshotStudio.Engine.Core;

namespace DaScreenshotStudio.Engine.World;

public sealed class DamageMeters
{
    public const int LifetimeMs = 2000;

    public const int Capacity = 64;

    private readonly Meter[] _meters = new Meter[Capacity];
    private int _count;

    public int Count => _count;

    public void Show(EntityId target, int stage, uint now)
    {
        if (target.IsNone)
        {
            return;
        }

        var clamped = (byte)Math.Clamp(stage, 0, DamageEffectAtlas.StageCount - 1);
        var expires = unchecked(now + LifetimeMs);

        for (var i = 0; i < _count; i++)
        {
            if (_meters[i].Target == target)
            {
                _meters[i] = new Meter(target, clamped, expires);
                return;
            }
        }

        if (_count < Capacity)
        {
            _meters[_count++] = new Meter(target, clamped, expires);
            return;
        }

        for (var i = 1; i < Capacity; i++)
        {
            _meters[i - 1] = _meters[i];
        }

        _meters[Capacity - 1] = new Meter(target, clamped, expires);
    }

    public bool Tick(uint now)
    {
        var changed = false;

        for (var i = _count - 1; i >= 0; i--)
        {
            if (!GameClock.IsDue(now, _meters[i].Expires))
            {
                continue;
            }

            for (var j = i; j < _count - 1; j++)
            {
                _meters[j] = _meters[j + 1];
            }

            _count--;
            changed = true;
        }

        return changed;
    }

    public void Remove(EntityId target)
    {
        for (var i = 0; i < _count; i++)
        {
            if (_meters[i].Target != target)
            {
                continue;
            }

            for (var j = i; j < _count - 1; j++)
            {
                _meters[j] = _meters[j + 1];
            }

            _count--;
            return;
        }
    }

    public void Clear() => _count = 0;

    public EntityId TargetAt(int index) => (uint)index < (uint)_count ? _meters[index].Target : default;

    public int StageAt(int index) => (uint)index < (uint)_count ? _meters[index].Stage : 0;

    private readonly struct Meter(EntityId target, byte stage, uint expires)
    {
        public EntityId Target { get; } = target;

        public byte Stage { get; } = stage;

        public uint Expires { get; } = expires;
    }
}
