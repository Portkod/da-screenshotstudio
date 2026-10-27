namespace DaScreenshotStudio.Engine.World;

public static class MapDimensions
{
    public const int MaximumAxis = 255;

    public static bool TryInfer(int cellCount, out int width, out int height)
    {
        width = 0;
        height = 0;

        if (cellCount <= 0 || cellCount > MaximumAxis * MaximumAxis)
        {
            return false;
        }

        var root = (int)Math.Sqrt(cellCount);
        for (var candidate = root; candidate >= 1; candidate--)
        {
            if (cellCount % candidate != 0)
            {
                continue;
            }

            var other = cellCount / candidate;
            if (other > MaximumAxis)
            {
                continue;
            }

            width = other;
            height = candidate;
            return true;
        }

        return false;
    }
}
