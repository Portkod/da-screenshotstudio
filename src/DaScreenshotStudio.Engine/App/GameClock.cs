namespace DaScreenshotStudio.Engine.App;

public static class GameClock
{
    public static bool IsDue(uint now, uint deadline) => unchecked(now - deadline) < int.MaxValue;
}
