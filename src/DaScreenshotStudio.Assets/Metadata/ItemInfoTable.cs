using System.Text;
using DaScreenshotStudio.Assets.Tables;

namespace DaScreenshotStudio.Assets.Metadata;

public sealed class ItemInfoRecord
{
    public const int LabelLength = 48;

    public const int DetailLength = 128;

    public const int MaxCategory = 5;

    internal ItemInfoRecord(string name, int category, int rangeMin, int rangeMax, int numericValue, string label, string detail)
    {
        Name = name;
        Category = category;
        RangeMin = rangeMin;
        RangeMax = rangeMax;
        NumericValue = numericValue;
        Label = label;
        Detail = detail;
    }

    public string Name { get; }

    public int Category { get; }

    public int RangeMin { get; }

    public int RangeMax { get; }

    public int NumericValue { get; }

    public string Label { get; }

    public string Detail { get; }

    public bool HasRange => RangeMin >= 0;

    public bool HasUsableCategory => (uint)Category <= MaxCategory;
}

public sealed class ItemInfoTable
{
    private readonly Dictionary<string, ItemInfoRecord> _byName = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ItemInfoRecord>.AlternateLookup<ReadOnlySpan<char>> _lookup;

    public ItemInfoTable() => _lookup = _byName.GetAlternateLookup<ReadOnlySpan<char>>();

    public int Count => _byName.Count;

    public IEnumerable<ItemInfoRecord> Records => _byName.Values;

    public int Add(Metafile table)
    {
        ArgumentNullException.ThrowIfNull(table);

        var added = 0;
        for (var group = 0; group < table.GroupCount; group++)
        {
            if (TryReadRecord(table, group, out var record) && _byName.TryAdd(record.Name, record))
            {
                added++;
            }
        }

        return added;
    }

    public static ItemInfoTable Load(MetafileStore store)
    {
        ArgumentNullException.ThrowIfNull(store);

        var table = new ItemInfoTable();
        foreach (var file in store.Files)
        {
            if (file.Name.StartsWith(MetafileStore.ItemInfoPrefix, StringComparison.OrdinalIgnoreCase))
            {
                table.Add(file);
            }
        }

        return table;
    }

    public ItemInfoRecord? Find(scoped ReadOnlySpan<char> itemName) =>
        _lookup.TryGetValue(itemName, out var record) ? record : null;

    private static bool TryReadRecord(Metafile table, int group, out ItemInfoRecord record)
    {
        record = null!;

        var name = table.GetGroupName(group);
        if (name.Length == 0 || table.GetValueCount(group) < 3)
        {
            return false;
        }

        var range = new TableReader(table.GetValue(group, 0));
        if (!range.TryReadLine(out var rangeLine) || !rangeLine.TryReadInt(out var rangeMax))
        {
            return false;
        }

        var rangeMin = rangeLine.TryReadInt(out var second) ? second : -1;

        if (!TryReadSingleInt(table.GetValue(group, 1), out var category)
            || !TryReadSingleInt(table.GetValue(group, 2), out var numericValue))
        {
            return false;
        }

        var label = table.GetValueCount(group) > 3 ? ReadText(table.GetValue(group, 3), ItemInfoRecord.LabelLength) : string.Empty;
        var detail = table.GetValueCount(group) > 4 ? ReadText(table.GetValue(group, 4), ItemInfoRecord.DetailLength) : string.Empty;

        record = new ItemInfoRecord(name, category, rangeMin, rangeMax, numericValue, label, detail);
        return true;
    }

    private static bool TryReadSingleInt(ReadOnlySpan<byte> value, out int result)
    {
        var reader = new TableReader(value);
        result = 0;
        return reader.TryReadLine(out var line) && line.TryReadInt(out result);
    }

    private static string ReadText(ReadOnlySpan<byte> value, int capacity)
    {
        if (value.Length > 0 && value[^1] == (byte)'\r')
        {
            value = value[..^1];
        }

        if (value.Length >= capacity)
        {
            value = value[..(capacity - 1)];
        }

        return Encoding.Latin1.GetString(value);
    }
}
