namespace DaScreenshotStudio.Engine.Core;

public static class DirectionExtensions
{
    public static int DeltaX(this Direction direction) => direction switch
    {
        Direction.Right => 1,
        Direction.Left => -1,
        _ => 0,
    };

    public static int DeltaY(this Direction direction) => direction switch
    {
        Direction.Up => -1,
        Direction.Down => 1,
        _ => 0,
    };
}
