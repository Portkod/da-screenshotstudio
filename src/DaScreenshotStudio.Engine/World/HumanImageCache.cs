using DaScreenshotStudio.Assets.Characters;
using DaScreenshotStudio.Engine.Core;

namespace DaScreenshotStudio.Engine.World;

public sealed class HumanImageCache
{
    public const int MaxStates = 40;

    public const int SpritesPerState = 256;

    public const long PixelBudgetBytes = 24L * 1024 * 1024;

    private readonly CharacterArt _art;
    private readonly Bucket[] _buckets = new Bucket[MaxStates];
    private long _clock;
    private long _frame = 1;

    private ushort[] _overflowPixels = [];
    private HumanState _overflowState;
    private HumanIdleMotion _overflowIdle;
    private bool _overflowKnown;

    public HumanImageCache(CharacterArt art)
    {
        ArgumentNullException.ThrowIfNull(art);
        _art = art;

        for (var i = 0; i < _buckets.Length; i++)
        {
            _buckets[i] = new Bucket();
        }
    }

    public int SpriteCount { get; private set; }

    public int StateCount { get; private set; }

    public long RetainedBytes { get; private set; }

    public long Compositions { get; private set; }

    public long Hits { get; private set; }

    public long OverflowCompositions { get; private set; }

    public void BeginFrame() => _frame++;

    public static HumanDirection ToHumanDirection(Direction facing) => facing switch
    {
        Direction.Left => HumanDirection.Left,
        Direction.Down => HumanDirection.Down,
        Direction.Right => HumanDirection.Right,
        _ => HumanDirection.Up,
    };

    public void Clear()
    {
        for (var i = 0; i < _buckets.Length; i++)
        {
            _buckets[i].Reset();
        }

        SpriteCount = 0;
        StateCount = 0;
        RetainedBytes = 0;
        _overflowKnown = false;
    }

    public bool TryGet(
        in HumanState state,
        Direction facing,
        int motion,
        int motionFrame,
        int framesPerView,
        int idleStep,
        out WorldSprite sprite,
        int emoteFrame = 0,
        char variant = '\0')
    {
        var direction = ToHumanDirection(facing);
        var bucket = Select(in state, out var idle);
        var step = Reduce(in idle, idleStep);
        var key = Key(direction, motion, motionFrame, framesPerView, step, emoteFrame, variant);

        if (bucket is not null && bucket.TryGet(key, out sprite))
        {
            Hits++;
            return !sprite.IsEmpty;
        }

        var composed = HumanComposite.Compose(
            _art,
            in state,
            direction,
            motion,
            motionFrame,
            framesPerView,
            in idle,
            step,
            emoteFrame,
            variant,
            bucket is null ? _overflowPixels : null);
        Compositions++;

        if (bucket is null)
        {
            OverflowCompositions++;
            if (composed.Pixels is { } composedPixels && composedPixels.Length > _overflowPixels.Length)
            {
                _overflowPixels = composedPixels;
            }

            sprite = composed.ToSprite();
            return !sprite.IsEmpty;
        }

        sprite = composed.ToSprite();
        Store(bucket, key, in sprite);
        return !sprite.IsEmpty;
    }

    public int IdlePeriod(in HumanState state)
    {
        Select(in state, out var idle);
        return idle.Period;
    }

    private static int Reduce(in HumanIdleMotion idle, int idleStep) =>
        idleStep < 0 ? -1 : idleStep % idle.Period;

    private static long Key(
        HumanDirection direction,
        int motion,
        int motionFrame,
        int framesPerView,
        int idleStep,
        int emoteFrame,
        char variant) =>
        ((long)(variant & 0x1F) << 38)
        | ((long)(emoteFrame & 0x3F) << 32)
        | (uint)(((idleStep + 1) << 18)
            | ((framesPerView & 7) << 15)
            | ((motion & 0x1F) << 10)
            | ((motionFrame & 0xFF) << 2)
            | ((int)direction & 3));

    private Bucket? Select(in HumanState state, out HumanIdleMotion idle)
    {
        var free = -1;
        var oldest = -1;

        for (var i = 0; i < _buckets.Length; i++)
        {
            var bucket = _buckets[i];

            if (!bucket.InUse)
            {
                if (free < 0)
                {
                    free = i;
                }

                continue;
            }

            if (bucket.StateMatches(in state))
            {
                bucket.LastUsed = ++_clock;
                bucket.LastFrame = _frame;
                idle = bucket.Idle;
                return bucket;
            }

            if (bucket.LastFrame != _frame
                && (oldest < 0 || bucket.LastUsed < _buckets[oldest].LastUsed))
            {
                oldest = i;
            }
        }

        if (free >= 0)
        {
            StateCount++;
            var adopted = Reuse(_buckets[free], in state);
            idle = adopted.Idle;
            return adopted;
        }

        if (oldest >= 0)
        {
            var reused = Reuse(_buckets[oldest], in state);
            idle = reused.Idle;
            return reused;
        }

        idle = OverflowIdle(in state);
        return null;
    }

    private HumanIdleMotion OverflowIdle(in HumanState state)
    {
        if (_overflowKnown && _overflowState.Matches(in state))
        {
            return _overflowIdle;
        }

        _overflowState = state;
        _overflowIdle = HumanComposite.BuildIdleMotion(_art, in state);
        _overflowKnown = true;
        return _overflowIdle;
    }

    private Bucket Reuse(Bucket bucket, in HumanState state)
    {
        Release(bucket);
        bucket.Adopt(in state, ++_clock, _frame, HumanComposite.BuildIdleMotion(_art, in state));
        return bucket;
    }

    private void Release(Bucket bucket)
    {
        SpriteCount -= bucket.Count;
        RetainedBytes -= bucket.Bytes;
        bucket.Reset();
    }

    private void Store(Bucket bucket, long key, in WorldSprite sprite)
    {
        var bytes = (long)sprite.Art.Width * sprite.Art.Height * sizeof(ushort);

        while (RetainedBytes + bytes > PixelBudgetBytes)
        {
            var oldest = -1;
            for (var i = 0; i < _buckets.Length; i++)
            {
                if (_buckets[i].InUse
                    && _buckets[i].LastFrame != _frame
                    && !ReferenceEquals(_buckets[i], bucket)
                    && (oldest < 0 || _buckets[i].LastUsed < _buckets[oldest].LastUsed))
                {
                    oldest = i;
                }
            }

            if (oldest < 0)
            {
                break;
            }

            Release(_buckets[oldest]);
            StateCount--;
        }

        var evicted = bucket.Store(key, in sprite, bytes);
        RetainedBytes += bytes - evicted;
        if (evicted == 0)
        {
            SpriteCount++;
        }
    }

    private sealed class Bucket
    {
        private const int ChainCount = SpritesPerState * 2;

        private readonly long[] _keys = new long[SpritesPerState];
        private readonly WorldSprite[] _sprites = new WorldSprite[SpritesPerState];
        private readonly long[] _used = new long[SpritesPerState];
        private readonly long[] _bytes = new long[SpritesPerState];

        private readonly short[] _chainHead = new short[ChainCount];
        private readonly short[] _chainNext = new short[SpritesPerState];
        private long _stamp;

        public Bucket()
        {
            Array.Fill(_chainHead, (short)-1);
        }

        private HumanState _state;

        public bool StateMatches(in HumanState state) => _state.Matches(in state);

        public HumanIdleMotion Idle { get; private set; }

        public bool InUse { get; private set; }

        public long LastUsed { get; set; }

        public long LastFrame { get; set; }

        public int Count { get; private set; }

        public long Bytes { get; private set; }

        public void Adopt(in HumanState state, long clock, long frame, in HumanIdleMotion idle)
        {
            _state = state;
            Idle = idle;
            InUse = true;
            LastUsed = clock;
            LastFrame = frame;
        }

        public void Reset()
        {
            InUse = false;
            Idle = default;
            LastFrame = 0;
            Bytes = 0;
            _stamp = 0;

            for (var i = 0; i < Count; i++)
            {
                Unlink(i);
            }

            Array.Clear(_keys, 0, Count);
            Array.Clear(_sprites, 0, Count);
            Array.Clear(_used, 0, Count);
            Array.Clear(_bytes, 0, Count);
            Count = 0;
        }

        public bool TryGet(long key, out WorldSprite sprite)
        {
            for (var i = _chainHead[ChainOf(key)]; i >= 0; i = _chainNext[i])
            {
                if (_keys[i] == key)
                {
                    _used[i] = ++_stamp;
                    sprite = _sprites[i];
                    return true;
                }
            }

            sprite = default;
            return false;
        }

        private static int ChainOf(long key)
        {
            var hash = (ulong)key * 0x9E3779B97F4A7C15UL;
            return (int)(hash >> (64 - 9)) & (ChainCount - 1);
        }

        private void Link(int slot)
        {
            var chain = ChainOf(_keys[slot]);
            _chainNext[slot] = _chainHead[chain];
            _chainHead[chain] = (short)slot;
        }

        private void Unlink(int slot)
        {
            var chain = ChainOf(_keys[slot]);
            var at = _chainHead[chain];

            if (at == slot)
            {
                _chainHead[chain] = _chainNext[slot];
                return;
            }

            while (at >= 0)
            {
                var next = _chainNext[at];
                if (next == slot)
                {
                    _chainNext[at] = _chainNext[slot];
                    return;
                }

                at = next;
            }
        }

        public long Store(long key, in WorldSprite sprite, long bytes)
        {
            var slot = Count;
            long released = 0;

            if (slot == SpritesPerState)
            {
                slot = 0;
                for (var i = 1; i < SpritesPerState; i++)
                {
                    if (_used[i] < _used[slot])
                    {
                        slot = i;
                    }
                }

                released = _bytes[slot];
                Unlink(slot);
            }
            else
            {
                Count++;
            }

            _keys[slot] = key;
            _sprites[slot] = sprite;
            _bytes[slot] = bytes;
            _used[slot] = ++_stamp;
            Link(slot);
            Bytes += bytes - released;
            return released;
        }
    }
}
