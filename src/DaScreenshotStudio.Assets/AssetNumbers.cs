namespace DaScreenshotStudio.Assets;

internal static class AssetNumbers
{
    public static bool TryParseDigits(scoped ReadOnlySpan<char> text, out int value)
    {
        value = 0;
        if (text.IsEmpty)
        {
            return false;
        }

        long result = 0;
        foreach (var digit in text)
        {
            if (!char.IsAsciiDigit(digit))
            {
                return false;
            }

            result = (result * 10) + (digit - '0');
            if (result > int.MaxValue)
            {
                return false;
            }
        }

        value = (int)result;
        return true;
    }

    public static bool TryParseDigits(scoped ReadOnlySpan<char> text, int maxDigits, out int value)
    {
        value = 0;
        return text.Length <= maxDigits && TryParseDigits(text, out value);
    }
}
