namespace DaScreenshotStudio.Engine.Records;

public readonly struct LegendMark
{
    public const int MaxKeyLength = 69;

    public const int MaxTextLength = 68;

    public LegendMark(LegendMarkIcon icon, byte color, byte[] key, byte[] text)
    {
        Icon = icon;
        Color = color;
        Key = key;
        Text = text;
    }

    public LegendMarkIcon Icon { get; }

    public byte Color { get; }

    public byte[] Key { get; }

    public byte[] Text { get; }
}
