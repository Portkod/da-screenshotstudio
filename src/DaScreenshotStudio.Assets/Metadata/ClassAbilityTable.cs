using System.Globalization;
using System.Text;

namespace DaScreenshotStudio.Assets.Metadata;

public enum ClassAbilityKind
{
    Skill,
    Spell,
}

public sealed class ClassAbilityRecord
{
    internal ClassAbilityRecord(
        string name,
        ClassAbilityKind kind,
        int level,
        int abilityMetadataStage,
        int abilityLevel,
        int icon,
        int strength,
        int intelligence,
        int wisdom,
        int constitution,
        int dexterity,
        string firstPrerequisiteName,
        int firstPrerequisiteLevel,
        string secondPrerequisiteName,
        int secondPrerequisiteLevel,
        string description)
    {
        Name = name;
        Kind = kind;
        Level = level;
        AbilityMetadataStage = abilityMetadataStage;
        AbilityLevel = abilityLevel;
        Icon = icon;
        Strength = strength;
        Intelligence = intelligence;
        Wisdom = wisdom;
        Constitution = constitution;
        Dexterity = dexterity;
        FirstPrerequisiteName = firstPrerequisiteName;
        FirstPrerequisiteLevel = firstPrerequisiteLevel;
        SecondPrerequisiteName = secondPrerequisiteName;
        SecondPrerequisiteLevel = secondPrerequisiteLevel;
        Description = description;
    }

    public string Name { get; }

    public ClassAbilityKind Kind { get; }

    public int Level { get; }

    public int AbilityMetadataStage { get; }

    public int AbilityLevel { get; }

    public int Icon { get; }

    public int Strength { get; }

    public int Intelligence { get; }

    public int Wisdom { get; }

    public int Constitution { get; }

    public int Dexterity { get; }

    public string FirstPrerequisiteName { get; }

    public int FirstPrerequisiteLevel { get; }

    public string SecondPrerequisiteName { get; }

    public int SecondPrerequisiteLevel { get; }

    public string Description { get; }
}

public sealed class ClassAbilityTable
{
    public const string NamePrefix = "SClass";

    public const int ClassCount = 6;

    private const string SkillMarker = "Skill";
    private const string SkillEndMarker = "Skill_End";
    private const string SpellMarker = "Spell";
    private const string SpellEndMarker = "Spell_End";

    private const int ValuesPerAbility = 6;

    private readonly ClassAbilityRecord[] _records;
    private readonly Dictionary<string, ClassAbilityRecord> _byName;
    private readonly Dictionary<string, ClassAbilityRecord>.AlternateLookup<ReadOnlySpan<char>> _lookup;

    private ClassAbilityTable(string name, ClassAbilityRecord[] records)
    {
        Name = name;
        _records = records;

        _byName = new Dictionary<string, ClassAbilityRecord>(records.Length, StringComparer.Ordinal);
        foreach (var record in records)
        {
            _byName[record.Name] = record;
        }

        _lookup = _byName.GetAlternateLookup<ReadOnlySpan<char>>();
    }

    public string Name { get; }

    public IReadOnlyList<ClassAbilityRecord> Records => _records;

    public static ClassAbilityTable Empty { get; } = new(string.Empty, []);

    public static string? TableNameFor(int characterClass) =>
        (uint)characterClass < ClassCount
            ? NamePrefix + characterClass.ToString(null, CultureInfo.InvariantCulture)
            : null;

    public static ClassAbilityTable Read(Metafile table)
    {
        ArgumentNullException.ThrowIfNull(table);

        var records = new List<ClassAbilityRecord>(table.GroupCount);
        var kind = default(ClassAbilityKind);
        var inside = false;

        for (var group = 0; group < table.GroupCount; group++)
        {
            switch (table.GetGroupName(group))
            {
                case SkillMarker:
                    kind = ClassAbilityKind.Skill;
                    inside = true;
                    continue;

                case SpellMarker:
                    kind = ClassAbilityKind.Spell;
                    inside = true;
                    continue;

                case SkillEndMarker or SpellEndMarker:
                    inside = false;
                    continue;
            }

            if (inside && TryReadRecord(table, group, kind, out var record))
            {
                records.Add(record);
            }
        }

        return new ClassAbilityTable(table.Name, [.. records]);
    }

    public static ClassAbilityTable Load(MetafileStore store, int characterClass)
    {
        ArgumentNullException.ThrowIfNull(store);

        var name = TableNameFor(characterClass);
        if (name is null || store.Find(name) is not { } file)
        {
            return Empty;
        }

        return Read(file);
    }

    private static bool TryReadRecord(Metafile table, int group, ClassAbilityKind kind, out ClassAbilityRecord record)
    {
        record = null!;

        var name = table.GetGroupName(group);
        if (name.Length == 0 || table.GetValueCount(group) < ValuesPerAbility)
        {
            return false;
        }

        Span<int> progression = stackalloc int[3];
        ReadFields(table.GetValue(group, 0), progression);

        Span<int> graphic = stackalloc int[1];
        if (ReadFields(table.GetValue(group, 1), graphic) == 0)
        {
            return false;
        }

        Span<int> attributes = stackalloc int[5];
        ReadFields(table.GetValue(group, 2), attributes);

        ReadPrerequisite(table.GetValue(group, 3), out var firstName, out var firstLevel);
        ReadPrerequisite(table.GetValue(group, 4), out var secondName, out var secondLevel);

        record = new ClassAbilityRecord(
            name,
            kind,
            progression[0],
            progression[1],
            progression[2],
            graphic[0],
            attributes[0],
            attributes[1],
            attributes[2],
            attributes[4],
            attributes[3],
            firstName,
            firstLevel,
            secondName,
            secondLevel,
            Encoding.Latin1.GetString(table.GetValue(group, 5)));

        return true;
    }

    private static int ReadFields(scoped ReadOnlySpan<byte> value, scoped Span<int> values)
    {
        values.Clear();

        var count = 0;
        var position = 0;
        while (count < values.Length && position < value.Length)
        {
            var digits = 0;
            var number = 0;
            while (position < value.Length && (uint)(value[position] - (byte)'0') <= 9)
            {
                number = (number * 10) + (value[position] - (byte)'0');
                position++;
                digits++;
            }

            if (digits != 0)
            {
                values[count++] = number;
            }

            while (position < value.Length && (uint)(value[position] - (byte)'0') > 9)
            {
                position++;
            }
        }

        return count;
    }

    private static void ReadPrerequisite(scoped ReadOnlySpan<byte> value, out string name, out int level)
    {
        name = string.Empty;
        level = 0;

        var slash = value.LastIndexOf((byte)'/');
        if (slash < 0)
        {
            return;
        }

        var text = value[..slash];
        if (text.Length == 0 || (text.Length == 1 && text[0] == (byte)'0'))
        {
            return;
        }

        Span<int> parsed = stackalloc int[1];
        ReadFields(value[(slash + 1)..], parsed);

        name = Encoding.Latin1.GetString(text);
        level = parsed[0];
    }
}
