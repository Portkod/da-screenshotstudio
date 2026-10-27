using DaScreenshotStudio.Engine.App;
using DaScreenshotStudio.Engine.Records;

namespace DaScreenshotStudio.Engine.Ui;

public sealed class LevelPointFlash
{
    public const int IntervalMs = 500;

    private uint _due;

    public bool HasStatPoints { get; private set; }

    public byte StatPoints { get; private set; }

    public bool Phase { get; private set; }

    public bool IsFlashing { get; private set; }

    public int Toggles { get; private set; }

    public void Apply(in LevelPoint points, uint now) =>
        Apply(points.HasStatPoints, points.StatPoints, now);

    public void ApplyStatus(bool hasStatPoints, byte statPoints, uint now) =>
        Apply(hasStatPoints, statPoints, now);

    private void Apply(bool hasStatPoints, byte statPoints, uint now)
    {
        HasStatPoints = hasStatPoints;
        StatPoints = statPoints;

        IsFlashing = statPoints != 0;
        _due = unchecked(now + IntervalMs);
    }

    public bool Tick(uint now)
    {
        if (!IsFlashing || !GameClock.IsDue(now, _due))
        {
            return false;
        }

        if (StatPoints == 0)
        {
            IsFlashing = false;
            return false;
        }

        Phase = !Phase;
        Toggles++;
        _due = unchecked(now + IntervalMs);
        return true;
    }
}
