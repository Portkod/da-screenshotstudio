using DaScreenshotStudio.Engine.App;
using DaScreenshotStudio.Engine.Rendering;

namespace DaScreenshotStudio.Engine.Ui;

internal struct WindowSlide
{
    public const int Steps = 3;

    public const uint StepMs = 0x64;

    private bool _opening;
    private bool _pending;
    private uint _due;

    public int Phase { get; private set; } = Steps;

    public readonly bool IsSliding => _pending;

    public readonly bool IsAway => Phase <= 0;

    public WindowSlide()
    {
    }

    public void Begin(bool opening, uint now)
    {
        _opening = opening;
        Phase = opening ? 1 : 2;
        _pending = true;
        _due = unchecked(now + StepMs);
    }

    public void Rest()
    {
        _pending = false;
        Phase = Steps;
    }

    public bool Step(uint now)
    {
        if (!_pending || !GameClock.IsDue(now, _due))
        {
            return false;
        }

        _pending = false;
        Phase += _opening ? 1 : -1;

        if (Phase > 0 && Phase < Steps)
        {
            _pending = true;
            _due = unchecked(now + StepMs);
        }

        return true;
    }

    public static PixelRect Bounds(in PixelRect resting, int phase)
    {
        if (phase >= Steps)
        {
            return resting;
        }

        var left = resting.Right - (resting.Width * Math.Max(phase, 0) / Steps);
        return PixelRect.FromEdges(left, resting.Y, resting.Right, resting.Bottom);
    }
}
