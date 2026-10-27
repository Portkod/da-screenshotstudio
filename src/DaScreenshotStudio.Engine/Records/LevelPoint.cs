namespace DaScreenshotStudio.Engine.Records;

public readonly struct LevelPoint
{
    public LevelPoint(bool hasStatPoints, byte statPoints)
    {
        HasStatPoints = hasStatPoints;
        StatPoints = statPoints;
    }

    public bool HasStatPoints { get; }

    public byte StatPoints { get; }
}
