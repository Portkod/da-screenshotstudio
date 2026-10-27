namespace DaScreenshotStudio.Assets.Characters;

public static class HumanPartName
{
    public const int MaxNameLength = 11;

    public const int VariantNameLength = MaxNameLength - 1;

    private const string Extension = ".epf";

    public const char BodyMotionFamilyLetter = 'm';

    public static bool IsMaleWeaponSprite(int spriteId) => spriteId is 130 or 131;

    public const int MaleHeadSprite = 103;

    public static bool ForcesMalePrefix(int category, int spriteId) => category switch
    {
        HumanPartCategory.Shield => true,
        HumanPartCategory.Weapon => IsMaleWeaponSprite(spriteId),
        HumanPartCategory.Head => spriteId == MaleHeadSprite,
        _ => false,
    };

    public static bool TryFormat(
        scoped in HumanAppearance appearance,
        int category,
        int motion,
        scoped Span<char> destination,
        out int written) =>
        TryFormat(in appearance, category, motion, '\0', destination, out written);

    public static bool TryFormat(
        scoped in HumanAppearance appearance,
        int category,
        int motion,
        char variant,
        scoped Span<char> destination,
        out int written)
    {
        written = 0;

        var spriteId = appearance.GetPartSprite(category);
        if (!HumanPartCategory.TryResolve(category, spriteId, out var letter, out var resourceId))
        {
            return false;
        }

        var prefix = appearance.Body.Prefix;

        if (category == HumanPartCategory.Body && appearance.Body.UsesBodyMotionFamily)
        {
            letter = BodyMotionFamilyLetter;
        }
        else if (ForcesMalePrefix(category, spriteId))
        {
            prefix = 'm';
        }

        return variant == '\0'
            ? TryFormat(prefix, letter, resourceId, motion, destination, out written)
            : TryFormatVariant(prefix, letter, resourceId, variant, destination, out written);
    }

    public static bool TryFormat(char prefix, char letter, int resourceId, int motion, scoped Span<char> destination, out int written)
    {
        written = 0;
        var suffix = motion + 1;
        if (destination.Length < MaxNameLength || (uint)resourceId > 999 || (uint)suffix > 99)
        {
            return false;
        }

        destination[0] = prefix;
        destination[1] = letter;
        WriteDigits(destination.Slice(2, 3), resourceId);
        WriteDigits(destination.Slice(5, 2), suffix);
        Extension.CopyTo(destination[7..]);

        written = MaxNameLength;
        return true;
    }

    public static bool TryFormatVariant(char prefix, char letter, int resourceId, char variant, scoped Span<char> destination, out int written)
    {
        written = 0;
        if (destination.Length < VariantNameLength || (uint)resourceId > 999)
        {
            return false;
        }

        destination[0] = prefix;
        destination[1] = letter;
        WriteDigits(destination.Slice(2, 3), resourceId);
        destination[5] = variant;
        Extension.CopyTo(destination[6..]);

        written = VariantNameLength;
        return true;
    }

    private static void WriteDigits(scoped Span<char> destination, int value)
    {
        for (var i = destination.Length - 1; i >= 0; i--)
        {
            destination[i] = (char)('0' + (value % 10));
            value /= 10;
        }
    }
}
