using DaScreenshotStudio.Engine.Rendering;
using DaScreenshotStudio.Engine.Ui;
using DaScreenshotStudio.Engine.Core;

namespace DaScreenshotStudio.Engine.World;

public sealed class SnowParticleArt
{
    private readonly Pixmap[][] _frames;

    private SnowParticleArt(Pixmap[][] frames) => _frames = frames;

    public static string EntryNameFor(int kind) => $"snowa{kind:D2}.epf";

    public static SnowParticleArt? TryLoad(UiAssets assets)
    {
        ArgumentNullException.ThrowIfNull(assets);

        var frames = new Pixmap[SnowWeather.KindCount][];

        for (var kind = 0; kind < frames.Length; kind++)
        {
            var name = EntryNameFor(kind);
            var count = assets.PaletteFrameCount(name);
            if (count <= 0)
            {
                return null;
            }

            var decoded = new Pixmap[count];
            for (var frame = 0; frame < count; frame++)
            {
                decoded[frame] = assets.LegendFrame(name, frame);
            }

            if (decoded[0].IsEmpty)
            {
                return null;
            }

            frames[kind] = decoded;
        }

        return new SnowParticleArt(frames);
    }

    public int FrameCount(int kind) => _frames[kind].Length;

    public Pixmap Frame(int kind, int frame) => _frames[kind][frame];

    public int Width(int kind) => _frames[kind][0].Width;

    public int Height(int kind) => _frames[kind][0].Height;
}

public sealed class SnowParticlePane : Pane
{
    private readonly SnowParticleArt _art;

    public SnowParticlePane(SnowParticleArt art, int kind, PixelBlender blender)
        : base(new PixelRect(0, 0, RequireArt(art).Width(kind), art.Height(kind)), blender)
    {
        _art = art;
        Kind = kind;
        TargetBlend = BlendMode.Transparent;
    }

    public int Kind { get; }

    public int Frame { get; private set; }

    public void MoveTo(int x, int y) => SetOrigin(x, y);

    public bool AdvanceFrame()
    {
        var count = _art.FrameCount(Kind);
        if (count <= 1)
        {
            return false;
        }

        Frame = (Frame + 1) % count;
        Invalidate();
        return true;
    }

    public void ResetFrame()
    {
        if (Frame == 0)
        {
            return;
        }

        Frame = 0;
        Invalidate();
    }

    protected override void OnDrawContent(Canvas canvas)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        canvas.Clear();

        var image = _art.Frame(Kind, Frame);
        if (!image.IsEmpty)
        {
            canvas.BlitPixmap(0, 0, in image, BlendMode.Transparent);
        }
    }

    private static SnowParticleArt RequireArt(SnowParticleArt art)
    {
        ArgumentNullException.ThrowIfNull(art);
        return art;
    }
}

public sealed class SnowWeather
{
    public const int IntervalMs = 100;

    public const int FieldHeight = 300;

    public const int FallStep = 2;

    public const int SpawnBias = 6;

    public const int KindCount = 4;

    private static readonly int[] Counts = [160, 120, 12, 12];

    public const int FieldCapacity = 304;

    private readonly Pane _view;
    private readonly PixelBlender _blender;
    private readonly SnowParticleArt _art;
    private readonly Random _random;
    private readonly Record[] _field = new Record[FieldCapacity];
    private readonly List<SnowParticlePane> _live = new(FieldCapacity * 2);
    private readonly List<SnowParticlePane>[] _pool = new List<SnowParticlePane>[KindCount];

    private int _fieldCount;
    private int _cursor;
    private int _scanLine;
    private bool _built;
    private bool _frameToggle;
    private uint _lastStep;

    public SnowWeather(Pane view, PixelBlender blender, SnowParticleArt art, Random? random = null)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(blender);
        ArgumentNullException.ThrowIfNull(art);

        _view = view;
        _blender = blender;
        _art = art;
        _random = random ?? new Random();

        for (var kind = 0; kind < _pool.Length; kind++)
        {
            _pool[kind] = new List<SnowParticlePane>(Counts[kind] * 2);
        }
    }

    public bool IsRunning { get; private set; }

    public int PanesBuilt { get; private set; }

    public void SetMode(MapFlags mode, uint now)
    {
        Stop();

        if (mode == MapFlags.Snow)
        {
            Start(now);
        }
    }

    public void Start(uint now)
    {
        if (IsRunning)
        {
            return;
        }

        IsRunning = true;
        _lastStep = now;
        Spawn(_view.Bounds.Height);
        Reserve();
    }

    private void Reserve()
    {
        Span<int> live = stackalloc int[KindCount];

        for (var i = 0; i < _live.Count; i++)
        {
            live[_live[i].Kind]++;
        }

        for (var kind = 0; kind < KindCount; kind++)
        {
            var pool = _pool[kind];
            var wanted = Math.Max(live[kind], MaxRecords(kind));

            while (pool.Count < wanted)
            {
                PanesBuilt++;
                pool.Add(new SnowParticlePane(_art, kind, _blender));
            }
        }
    }

    private static int MaxRecords(int kind) => kind switch
    {
        0 => Counts[0],
        1 => Counts[0] + Counts[1],
        _ => Counts[kind],
    };

    public void Stop()
    {
        if (!IsRunning)
        {
            return;
        }

        IsRunning = false;

        for (var i = 0; i < _live.Count; i++)
        {
            Retire(_live[i]);
        }

        _live.Clear();
    }

    public bool Tick(uint now)
    {
        if (!IsRunning || unchecked(now - _lastStep) < IntervalMs)
        {
            return false;
        }

        _lastStep = now;
        Step();
        return true;
    }

    private void Step()
    {
        var view = ViewBounds();
        var kept = 0;

        for (var i = 0; i < _live.Count; i++)
        {
            var particle = _live[i];
            particle.MoveTo(particle.Bounds.X, particle.Bounds.Y + FallStep);

            var bounds = particle.Bounds;
            if (bounds.X < view.X || bounds.X >= view.Right || bounds.Y >= view.Bottom)
            {
                Retire(particle);
                continue;
            }

            if (_frameToggle)
            {
                particle.AdvanceFrame();
            }

            _frameToggle = !_frameToggle;
            _live[kept++] = particle;
        }

        _live.RemoveRange(kept, _live.Count - kept);

        if (view.Width != 0)
        {
            Spawn(FallStep);
        }
    }

    private void Spawn(int rows)
    {
        var view = ViewBounds();
        if (view.Width <= 0)
        {
            return;
        }

        for (var row = 0; row < rows; row++)
        {
            AdvanceField(view.Width);

            while (TryTakeRecord(out var x, out var y, out var kind))
            {
                var particle = Rent(kind);
                particle.MoveTo(x, y + rows - row - SpawnBias);
                _view.Add(particle);
                _live.Add(particle);
            }
        }
    }

    private PixelRect ViewBounds() => new(0, 0, _view.Bounds.Width, _view.Bounds.Height);

    private void AdvanceField(int width)
    {
        if (_scanLine <= 0)
        {
            BuildField(width);
        }

        _scanLine--;
    }

    private void BuildField(int width)
    {
        _fieldCount = 0;

        for (var kind = 0; kind < KindCount; kind++)
        {
            var records = Counts[kind];

            for (var n = 0; n < records; n++)
            {
                var y = _random.Next(FieldHeight);
                var x = _random.Next(width);

                var at = 0;
                while (at < _fieldCount && _field[at].Y >= y)
                {
                    at++;
                }

                for (var move = _fieldCount; move > at; move--)
                {
                    _field[move] = _field[move - 1];
                }

                var drawn = kind == 0 && (_random.Next() & 1) != 0 ? 1 : kind;
                _field[at] = new Record(x, y, drawn);
                _fieldCount++;
            }
        }

        _built = true;
        _scanLine = FieldHeight;
        _cursor = 0;
    }

    private bool TryTakeRecord(out int x, out int y, out int kind)
    {
        x = 0;
        y = 0;
        kind = 0;

        if (!_built || _cursor >= _fieldCount || _field[_cursor].Y < _scanLine)
        {
            return false;
        }

        var record = _field[_cursor];
        x = record.X;
        y = record.Y - _scanLine;
        kind = record.Kind;
        _cursor++;
        return true;
    }

    private SnowParticlePane Rent(int kind)
    {
        var pool = _pool[kind];
        if (pool.Count == 0)
        {
            PanesBuilt++;
            return new SnowParticlePane(_art, kind, _blender);
        }

        var particle = pool[^1];
        pool.RemoveAt(pool.Count - 1);
        particle.ResetFrame();
        return particle;
    }

    private void Retire(SnowParticlePane particle)
    {
        _view.Remove(particle);
        _pool[particle.Kind].Add(particle);
    }

    private readonly record struct Record(int X, int Y, int Kind);
}
