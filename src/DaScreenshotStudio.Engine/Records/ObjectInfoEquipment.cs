namespace DaScreenshotStudio.Engine.Records;

public readonly struct ObjectInfoEquipment
{
    public ObjectInfoEquipment(EquipmentSlot slot, ushort sprite, byte dyeColor)
    {
        Slot = slot;
        Sprite = sprite;
        DyeColor = dyeColor;
    }

    public EquipmentSlot Slot { get; }

    public ushort Sprite { get; }

    public byte DyeColor { get; }
}
