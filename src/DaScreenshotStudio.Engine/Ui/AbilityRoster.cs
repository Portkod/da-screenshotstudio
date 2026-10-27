using DaScreenshotStudio.Assets.Metadata;
using DaScreenshotStudio.Assets.Tables;

namespace DaScreenshotStudio.Engine.Ui;

public enum AbilityRowState
{
    Learned = 1,
    Available = 2,
    Locked = 3,
}

public readonly struct AbilityRequirements
{
    public bool Level { get; init; }

    public bool Strength { get; init; }

    public bool Intelligence { get; init; }

    public bool Wisdom { get; init; }

    public bool Constitution { get; init; }

    public bool Dexterity { get; init; }

    public bool FirstPrerequisite { get; init; }

    public bool SecondPrerequisite { get; init; }

    public bool All =>
        Level && Strength && Intelligence && Wisdom && Constitution && Dexterity
        && FirstPrerequisite && SecondPrerequisite;
}

public static class AbilityRoster
{
    public const int LevelLineLength = 32;

    public const int NoLearnedLevel = -1;

    public static ReadOnlySpan<byte> SplitName(ReadOnlySpan<byte> wireName, out int level)
    {
        level = NoLearnedLevel;

        var close = wireName.LastIndexOf((byte)')');
        if (close < 0)
        {
            return wireName;
        }

        var slash = wireName[..close].LastIndexOf((byte)'/');
        if (slash < 0)
        {
            return wireName;
        }

        var open = wireName[..slash].LastIndexOf((byte)'(');
        if (open < 1 || wireName[open - 1] != (byte)' ')
        {
            return wireName;
        }

        level = ParseLeftValue(wireName[(open + 1)..slash]);
        return wireName[..(open - 1)];
    }

    public static bool TryFindLearned(SlotBook book, string name, out int level)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(name);

        level = NoLearnedLevel;

        for (var slot = 1; slot <= book.Capacity; slot++)
        {
            var record = book[slot];
            if (!record.IsOccupied)
            {
                continue;
            }

            var visible = SplitName(record.Name, out var learned);
            if (!Matches(visible, name))
            {
                continue;
            }

            level = learned;
            return true;
        }

        return false;
    }

    public static AbilityRowState StateOf(
        ClassAbilityRecord ability,
        PlayerModel player,
        SlotBook own,
        bool showAbilityMetadata,
        bool showMasterMetadata)
    {
        ArgumentNullException.ThrowIfNull(ability);

        if (TryFindLearned(own, ability.Name, out _))
        {
            return AbilityRowState.Learned;
        }

        return RequirementsMet(ability, player, showAbilityMetadata, showMasterMetadata)
            ? AbilityRowState.Available
            : AbilityRowState.Locked;
    }

    public static bool RequirementsMet(
        ClassAbilityRecord ability,
        PlayerModel player,
        bool showAbilityMetadata,
        bool showMasterMetadata) =>
        CheckRequirements(ability, player, showAbilityMetadata, showMasterMetadata).All;

    public static AbilityRequirements CheckRequirements(
        ClassAbilityRecord ability,
        PlayerModel player,
        bool showAbilityMetadata,
        bool showMasterMetadata)
    {
        ArgumentNullException.ThrowIfNull(ability);
        ArgumentNullException.ThrowIfNull(player);

        var level = player.Progression.Level >= ability.Level
            && (showAbilityMetadata ? 1 : 0) >= ability.AbilityMetadataStage
            && (ability.AbilityLevel == 0
                || (showMasterMetadata && player.Progression.AbilityLevel >= ability.AbilityLevel));

        var attributes = player.Attributes;
        return new AbilityRequirements
        {
            Level = level,
            Strength = attributes.Strength >= ability.Strength,
            Intelligence = attributes.Intelligence >= ability.Intelligence,
            Wisdom = attributes.Wisdom >= ability.Wisdom,
            Constitution = attributes.Constitution >= ability.Constitution,
            Dexterity = attributes.Dexterity >= ability.Dexterity,
            FirstPrerequisite = HasPrerequisite(
                player,
                ability.FirstPrerequisiteName,
                ability.FirstPrerequisiteLevel),
            SecondPrerequisite = HasPrerequisite(
                player,
                ability.SecondPrerequisiteName,
                ability.SecondPrerequisiteLevel),
        };
    }

    public static int FormatLevelLine(ClassAbilityRecord ability, MessageTable messages, Span<byte> destination)
    {
        ArgumentNullException.ThrowIfNull(ability);
        ArgumentNullException.ThrowIfNull(messages);

        if (ability.AbilityLevel > 0)
        {
            return Format(messages.Get(MessageTable.AbilityRequirementSlot), ability.AbilityLevel, destination);
        }

        if (ability.AbilityMetadataStage > 0)
        {
            return Format(messages.Get(MessageTable.MasterRequirementSlot), 0, destination);
        }

        return Format(messages.Get(MessageTable.LevelRequirementSlot), ability.Level, destination);
    }

    internal static int Format(scoped ReadOnlySpan<byte> format, int value, Span<byte> destination)
    {
        var written = 0;
        var substituted = false;

        for (var i = 0; i < format.Length && written < destination.Length; i++)
        {
            if (!substituted && format[i] == (byte)'%' && i + 1 < format.Length && format[i + 1] == (byte)'d')
            {
                substituted = true;
                i++;
                written += WriteNumber(value, destination[written..]);
                continue;
            }

            destination[written++] = format[i];
        }

        return written;
    }

    private static int WriteNumber(int value, Span<byte> destination)
    {
        Span<byte> digits = stackalloc byte[12];
        var count = 0;
        var magnitude = value < 0 ? -(long)value : value;

        do
        {
            digits[count++] = (byte)('0' + (int)(magnitude % 10));
            magnitude /= 10;
        }
        while (magnitude != 0 && count < digits.Length);

        if (value < 0 && count < digits.Length)
        {
            digits[count++] = (byte)'-';
        }

        var written = 0;
        for (var i = count - 1; i >= 0 && written < destination.Length; i--)
        {
            destination[written++] = digits[i];
        }

        return written;
    }

    private static bool HasPrerequisite(PlayerModel player, string name, int level)
    {
        if (name.Length == 0)
        {
            return true;
        }

        return (TryFindLearned(player.Spells, name, out var spellLevel) && spellLevel >= level)
            || (TryFindLearned(player.Skills, name, out var skillLevel) && skillLevel >= level);
    }

    private static bool Matches(scoped ReadOnlySpan<byte> value, string name)
    {
        if (value.Length != name.Length)
        {
            return false;
        }

        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] != (byte)name[i])
            {
                return false;
            }
        }

        return true;
    }

    private static int ParseLeftValue(scoped ReadOnlySpan<byte> field)
    {
        var start = 0;
        while (start < field.Length && (uint)(field[start] - (byte)'0') > 9)
        {
            start++;
        }

        var value = 0;
        for (var i = start; i < field.Length; i++)
        {
            var digit = (uint)(field[i] - (byte)'0');
            if (digit > 9)
            {
                break;
            }

            value = (value * 10) + (int)digit;
        }

        return value;
    }
}
