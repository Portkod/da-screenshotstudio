namespace DaScreenshotStudio.Engine.Records;

public readonly ref struct EquippedItem
{
    public EquippedItem(
        EquipmentSlot slot,
        ushort sprite,
        byte dyeColor,
        ReadOnlySpan<byte> name,
        uint maxDurability,
        uint currentDurability)
    {
        Slot = slot;
        Sprite = sprite;
        DyeColor = dyeColor;
        Name = name;
        IgnoredAfterName = 0;
        MaxDurability = maxDurability;
        CurrentDurability = currentDurability;
    }

    public EquipmentSlot Slot { get; }

    public ushort Sprite { get; }

    public byte DyeColor { get; }

    public ReadOnlySpan<byte> Name { get; }

    public byte IgnoredAfterName { get; init; }

    public uint MaxDurability { get; }

    public uint CurrentDurability { get; }
}
