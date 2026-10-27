namespace DaScreenshotStudio.Assets.Characters;

public static class HumanPartCategory
{
    public const int Count = 21;

    public const int Shield = 0;

    public const int Body = 1;

    public const int Pants = 2;

    public const int Boots = 3;

    public const int Head = 4;

    public const int Armor = 5;

    public const int InnerClothing = 6;

    public const int Arms = 7;

    public const int Weapon = 8;

    public const int Accessory1Front = 9;

    public const int Accessory1Back = 10;

    public const int Accessory2Front = 11;

    public const int Accessory2Back = 12;

    public const int Accessory3Front = 13;

    public const int Accessory3Back = 14;

    public const int WeaponAlternate = 15;

    public const int HeadAlternate1 = 16;

    public const int HeadAlternate2 = 17;

    public const int Overcoat = 18;

    public const int OvercoatArms = 19;

    public const int Face = 20;

    public const int ExtendedIdBase = 1000;

    public static ReadOnlySpan<char> NormalLetters => "SBNLHUDAWCGCGCGPEFUAO";

    public static ReadOnlySpan<char> ExtendedLetters => "SBNLHIDJWCGCGCGPEFIJO";

    public static bool TryResolve(int category, int spriteId, out char letter, out int resourceId)
    {
        if ((uint)category >= Count || spriteId <= 0)
        {
            letter = '\0';
            resourceId = 0;
            return false;
        }

        if (spriteId >= ExtendedIdBase)
        {
            resourceId = spriteId - ExtendedIdBase;
            letter = char.ToLowerInvariant(ExtendedLetters[category]);
        }
        else
        {
            resourceId = spriteId;
            letter = char.ToLowerInvariant(NormalLetters[category]);
        }

        return true;
    }

    private static ReadOnlySpan<byte> PaletteSlots =>
    [
        0, 1, 2, 3, 4, 5, 6, 8, 9, 11, 12, 11, 12, 11, 12, 17, 4, 4, 5, 8, 22,
    ];

    private static ReadOnlySpan<byte> SlotTables =>
    [
        0, 1, 2, 3, 4, 5, 6, 4, 8, 9, 0, 7, 10, 11, 12, 10, 8, 8, 13, 13, 0, 0, 0,
    ];

    public static ReadOnlySpan<char> TableLetters => "SBNLHUDCAWPEFI";

    private const int PaletteSlotCount = 23;

    private const int ExtendedArmorSlot = 20;

    private const int ExtendedArmsSlot = 21;

    private const int ArmorSlot = 5;

    private const int ArmsSlot = 8;

    public static bool TryResolvePaletteTable(int category, int spriteId, out char tableLetter, out int resourceId)
    {
        tableLetter = '\0';
        resourceId = 0;

        if ((uint)category >= Count || spriteId <= 0)
        {
            return false;
        }

        int slot = PaletteSlots[category];
        if (spriteId >= ExtendedIdBase)
        {
            slot = slot switch
            {
                ArmorSlot => ExtendedArmorSlot,
                ArmsSlot => ExtendedArmsSlot,
                _ => slot,
            };
        }

        resourceId = spriteId % ExtendedIdBase;
        if ((uint)slot >= PaletteSlotCount || (uint)resourceId >= CharacterPaletteTable.ResourceIdCount)
        {
            return false;
        }

        tableLetter = char.ToLowerInvariant(TableLetters[SlotTables[slot]]);
        return true;
    }
}
