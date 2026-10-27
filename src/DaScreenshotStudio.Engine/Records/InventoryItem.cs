namespace DaScreenshotStudio.Engine.Records;

public readonly ref struct InventoryItem
{
    public const byte MaxSlot = 60;

    public InventoryItem(
        byte slot,
        ushort sprite,
        byte dyeColor,
        ReadOnlySpan<byte> name,
        uint quantity,
        bool canStack,
        uint maxDurability,
        uint currentDurability)
    {
        Slot = slot;
        Sprite = sprite;
        DyeColor = dyeColor;
        Name = name;
        Quantity = quantity;
        CanStack = canStack;
        MaxDurability = maxDurability;
        CurrentDurability = currentDurability;
    }

    public byte Slot { get; }

    public ushort Sprite { get; }

    public byte DyeColor { get; }

    public ReadOnlySpan<byte> Name { get; }

    public uint Quantity { get; }

    public bool CanStack { get; }

    public uint MaxDurability { get; }

    public uint CurrentDurability { get; }
}
