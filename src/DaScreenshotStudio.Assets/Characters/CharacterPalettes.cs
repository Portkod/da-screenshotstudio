using DaScreenshotStudio.Assets.Archives;
using DaScreenshotStudio.Assets.Palettes;

namespace DaScreenshotStudio.Assets.Characters;

public sealed class CharacterPalettes
{
    public const string DefaultPaletteEntry = "legend.pal";

    private const int LetterCount = 26;

    public const char BodyLetter = 'm';

    public const int PaletteNumberOffset = 0;

    private readonly CharacterPaletteTable?[] _tables;
    private readonly Palette?[][] _palettes;
    private readonly Palette? _fallback;

    private CharacterPalettes(CharacterPaletteTable?[] tables, Palette?[][] palettes, Palette? fallback)
    {
        _tables = tables;
        _palettes = palettes;
        _fallback = fallback;
    }

    public Palette? Fallback => _fallback;

    public static CharacterPalettes Load(IAssetArchive archive)
    {
        ArgumentNullException.ThrowIfNull(archive);

        var tables = new CharacterPaletteTable?[LetterCount];
        var found = new Dictionary<int, Palette>[LetterCount];
        for (var i = 0; i < LetterCount; i++)
        {
            found[i] = [];
        }

        Palette? fallback = null;

        var entries = archive.Entries;
        for (var i = 0; i < entries.Count; i++)
        {
            var name = entries[i].Name.AsSpan();

            if (name.Equals(DefaultPaletteEntry, StringComparison.OrdinalIgnoreCase)
                && archive.TryRead(name, out var globalData))
            {
                fallback = Palette.Parse(globalData);
                continue;
            }

            if (name.Length < 5 || !name.StartsWith("pal", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var slot = char.ToLowerInvariant(name[3]) - 'a';
            if ((uint)slot >= LetterCount)
            {
                continue;
            }

            var dot = name.LastIndexOf('.');
            if (dot < 4)
            {
                continue;
            }

            var extension = name[dot..];
            var stem = name[4..dot];

            if (extension.Equals(".tbl", StringComparison.OrdinalIgnoreCase))
            {
                if (stem.IsEmpty && archive.TryRead(name, out var tableData))
                {
                    tables[slot] = CharacterPaletteTable.Parse(tableData);
                }
            }
            else if (extension.Equals(".pal", StringComparison.OrdinalIgnoreCase)
                && TryParseNumber(stem, out var number)
                && archive.TryRead(name, out var paletteData))
            {
                if (stem.Length == 3 || !found[slot].ContainsKey(number))
                {
                    found[slot][number] = Palette.Parse(paletteData);
                }
            }
        }

        var palettes = new Palette?[LetterCount][];
        for (var slot = 0; slot < LetterCount; slot++)
        {
            var capacity = 0;
            foreach (var number in found[slot].Keys)
            {
                capacity = Math.Max(capacity, number + 1);
            }

            var byNumber = new Palette?[capacity];
            foreach (var pair in found[slot])
            {
                byNumber[pair.Key] = pair.Value;
            }

            palettes[slot] = byNumber;
        }

        return new CharacterPalettes(tables, palettes, fallback);
    }

    public CharacterPaletteTable? GetTable(char letter)
    {
        var slot = char.ToLowerInvariant(letter) - 'a';
        return (uint)slot < LetterCount ? _tables[slot] : null;
    }

    public Palette? GetPalette(char letter, int number)
    {
        var slot = char.ToLowerInvariant(letter) - 'a';
        if ((uint)slot >= LetterCount)
        {
            return null;
        }

        var slots = _palettes[slot];
        return (uint)number < (uint)slots.Length ? slots[number] : null;
    }

    public int LoadedPaletteCount(char letter)
    {
        var slot = char.ToLowerInvariant(letter) - 'a';
        if ((uint)slot >= LetterCount)
        {
            return 0;
        }

        var slots = _palettes[slot];
        var count = 0;
        while (count < slots.Length && slots[count] is not null)
        {
            count++;
        }

        return count;
    }

    public Palette? ResolveAssetPalette(char tableLetter, int resourceId, bool female)
    {
        var count = LoadedPaletteCount(tableLetter);
        if (count == 0 || (uint)resourceId >= CharacterPaletteTable.ResourceIdCount)
        {
            return _fallback;
        }

        var number = GetTable(tableLetter) is { } table
            ? table.GetPaletteNumber(resourceId, female)
            : CharacterPaletteTable.DefaultPaletteNumber;

        var index = number - PaletteNumberOffset;
        if ((uint)index >= (uint)count)
        {
            index = 0;
        }

        return GetPalette(tableLetter, index) ?? _fallback;
    }

    public Palette? ResolveDyedPalette(char tableLetter, int resourceId, bool female, int paletteSelector) =>
        paletteSelector >= 0 && GetPalette(BodyLetter, paletteSelector) is { } dyed
            ? dyed
            : ResolveAssetPalette(tableLetter, resourceId, female);

    private static bool TryParseNumber(scoped ReadOnlySpan<char> stem, out int number) =>
        AssetNumbers.TryParseDigits(stem, maxDigits: 3, out number);
}
