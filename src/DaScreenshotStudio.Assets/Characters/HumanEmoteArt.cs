namespace DaScreenshotStudio.Assets.Characters;

public static class HumanEmoteArt
{
    public const string DefaultSheet = "emot01.epf";

    public static string SelectSheet(int accessory1Sprite) => accessory1Sprite switch
    {
        0x20 => "emot02.epf",
        0x1F => "emot03.epf",
        0x1E => "emot04.epf",
        _ => DefaultSheet,
    };

    public static bool IsAlternateSheet(int accessory1Sprite) => accessory1Sprite is 0x1E or 0x1F or 0x20;
}
