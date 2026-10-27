using System.Globalization;
using System.Text;

namespace DaScreenshotStudio.Assets.Metadata;

public sealed class EventRecord
{
    internal EventRecord(
        string title,
        string id,
        int levelMask,
        int classMask,
        string summary,
        string result,
        string[] prerequisites,
        string reward)
    {
        Title = title;
        Id = id;
        LevelMask = levelMask;
        ClassMask = classMask;
        Summary = summary;
        Result = result;
        Prerequisites = prerequisites;
        Reward = reward;
    }

    public string Title { get; }

    public string Id { get; }

    public int LevelMask { get; }

    public int ClassMask { get; }

    public string Summary { get; }

    public string Result { get; }

    public IReadOnlyList<string> Prerequisites { get; }

    public string Reward { get; }
}

public sealed class EventTable
{
    public const string NamePrefix = "SEvent";

    public const int TableCount = 7;

    private const char FieldSeparator = '_';

    private const string StartField = "start";
    private const string EndField = "end";
    private const string TitleField = "title";
    private const string IdField = "id";
    private const string QualField = "qual";
    private const string SummaryField = "sum";
    private const string ResultField = "result";
    private const string PrerequisiteField = "sub";
    private const string RewardField = "reward";

    private const int MaxLevelDigit = 8;

    private const int MaxClassDigit = 5;

    private const int RequiredFields = 0x7F;

    private const int TitleSeen = 0x01;
    private const int IdSeen = 0x02;
    private const int QualSeen = 0x04;
    private const int SummarySeen = 0x08;
    private const int ResultSeen = 0x10;
    private const int PrerequisiteSeen = 0x20;
    private const int EndSeen = 0x40;

    private readonly EventRecord[] _records;

    private EventTable(string name, EventRecord[] records)
    {
        Name = name;
        _records = records;
    }

    public string Name { get; }

    public IReadOnlyList<EventRecord> Records => _records;

    public static EventTable Empty { get; } = new(string.Empty, []);

    public static string? TableNameFor(int index) =>
        (uint)index < TableCount
            ? NamePrefix + (index + 1).ToString(null, CultureInfo.InvariantCulture)
            : null;

    public static EventTable Read(Metafile table)
    {
        ArgumentNullException.ThrowIfNull(table);

        var records = new List<EventRecord>();
        var group = 0;

        while (TryReadRecord(table, ref group, out var record))
        {
            records.Add(record);
        }

        return new EventTable(table.Name, [.. records]);
    }

    public static EventTable Load(MetafileStore store, int index)
    {
        ArgumentNullException.ThrowIfNull(store);

        var name = TableNameFor(index);
        if (name is null || store.Find(name) is not { } file)
        {
            return Empty;
        }

        return Read(file);
    }

    public static int ReadDigitMask(scoped ReadOnlySpan<byte> value, int maxDigit)
    {
        var mask = 0;
        foreach (var character in value)
        {
            var digit = character - (byte)'0';
            if (digit >= 1 && digit <= maxDigit)
            {
                mask |= 1 << (digit - 1);
            }
        }

        return mask;
    }

    private static bool TryReadRecord(Metafile table, ref int group, out EventRecord record)
    {
        record = null!;

        while (group < table.GroupCount && FieldOf(table.GetGroupName(group)) != StartField)
        {
            group++;
        }

        if (group >= table.GroupCount)
        {
            return false;
        }

        group++;

        var seen = 0;
        var title = string.Empty;
        var id = string.Empty;
        var levelMask = 0;
        var classMask = 0;
        var summary = string.Empty;
        var result = string.Empty;
        var reward = string.Empty;
        List<string>? prerequisites = null;

        for (; group < table.GroupCount; group++)
        {
            var field = FieldOf(table.GetGroupName(group));
            if (field.Length == 0)
            {
                continue;
            }

            var values = table.GetValueCount(group);

            switch (field)
            {
                case TitleField when values >= 1:
                    seen |= TitleSeen;
                    title = Text(table.GetValue(group, 0));
                    break;

                case IdField when values >= 1:
                    seen |= IdSeen;
                    id = Text(table.GetValue(group, 0));
                    break;

                case QualField:
                    seen |= QualSeen;
                    if (values >= 1)
                    {
                        levelMask = ReadDigitMask(table.GetValue(group, 0), MaxLevelDigit);
                    }

                    if (values >= 2)
                    {
                        classMask = ReadDigitMask(table.GetValue(group, 1), MaxClassDigit);
                    }

                    break;

                case SummaryField when values >= 1:
                    seen |= SummarySeen;
                    summary = Text(table.GetValue(group, 0));
                    break;

                case ResultField when values >= 1:
                    seen |= ResultSeen;
                    result = Text(table.GetValue(group, 0));
                    break;

                case PrerequisiteField:
                    seen |= PrerequisiteSeen;
                    prerequisites ??= [];
                    for (var value = 0; value < values; value++)
                    {
                        prerequisites.Add(Text(table.GetValue(group, value)));
                    }

                    break;

                case RewardField when values >= 1:
                    reward = Text(table.GetValue(group, 0));
                    break;

                case EndField:
                    seen |= EndSeen;
                    break;
            }

            if ((seen & EndSeen) != 0)
            {
                group++;
                break;
            }
        }

        if ((seen & RequiredFields) != RequiredFields)
        {
            return false;
        }

        record = new EventRecord(
            title,
            id,
            levelMask,
            classMask,
            summary,
            result,
            prerequisites is null ? [] : [.. prerequisites],
            reward);

        return true;
    }

    private static string FieldOf(string groupName)
    {
        var separator = groupName.IndexOf(FieldSeparator, StringComparison.Ordinal);
        return separator < 0 ? string.Empty : groupName[(separator + 1)..];
    }

    private static string Text(scoped ReadOnlySpan<byte> value) => Encoding.Latin1.GetString(value);
}
