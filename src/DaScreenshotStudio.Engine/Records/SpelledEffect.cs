namespace DaScreenshotStudio.Engine.Records;

public readonly struct SpelledEffect
{
    public const byte StageNone = 0;

    public const int IndicatorSlotCount = 10;

    public SpelledEffect(ushort icon, byte durationStage)
    {
        Icon = icon;
        DurationStage = durationStage;
    }

    public ushort Icon { get; }

    public byte DurationStage { get; }
}
