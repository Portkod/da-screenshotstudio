namespace DaScreenshotStudio.Engine.Records;

public readonly struct ShopItem
{
    public const uint NoProgressLimit = 0xFFFFFFFF;

    public ShopItem(byte[] name, ushort sprite, byte color, uint value)
    {
        Name = name;
        Sprite = sprite;
        Color = color;
        Value = value;
    }

    public byte[] Name { get; }

    public ushort Sprite { get; }

    public byte Color { get; }

    public uint Value { get; }

    public bool HasQuantity { get; init; }

    public byte Quantity { get; init; }

    public uint ProgressCurrent { get; init; }

    public uint ProgressLimit { get; init; } = NoProgressLimit;
}
