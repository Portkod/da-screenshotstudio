using System.Text;

namespace DaScreenshotStudio.Assets.Metadata;

public sealed class NpcIllustrationTable
{
    private readonly Dictionary<string, string[]> _byName;
    private readonly Dictionary<string, string[]>.AlternateLookup<ReadOnlySpan<char>> _lookup;

    private NpcIllustrationTable(Dictionary<string, string[]> byName)
    {
        _byName = byName;
        _lookup = byName.GetAlternateLookup<ReadOnlySpan<char>>();
    }

    public const string LocalEntryName = "npci.tbl";

    public static NpcIllustrationTable Empty { get; } = new(new Dictionary<string, string[]>(StringComparer.Ordinal));

    public static NpcIllustrationTable Build(ReadOnlySpan<byte> localTable, Metafile? metadata)
    {
        var byName = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        ReadLocalTable(localTable, byName);

        if (metadata is not null)
        {
            Overlay(metadata, byName);
        }

        var built = new Dictionary<string, string[]>(byName.Count, StringComparer.Ordinal);
        foreach (var (name, entries) in byName)
        {
            built[name] = [.. entries];
        }

        return new NpcIllustrationTable(built);
    }

    public string? Find(scoped ReadOnlySpan<char> npcName, int index) =>
        index >= 0 && _lookup.TryGetValue(npcName, out var entries) && index < entries.Length
            ? entries[index]
            : null;

    private static void ReadLocalTable(ReadOnlySpan<byte> table, Dictionary<string, List<string>> byName)
    {
        List<string>? current = null;

        while (!table.IsEmpty)
        {
            var b = table[0];

            if (b == (byte)'\n')
            {
                current = null;
                table = table[1..];
                continue;
            }

            if (b <= (byte)' ')
            {
                table = table[1..];
                continue;
            }

            if (b == (byte)'#')
            {
                var comment = table.IndexOf((byte)'\n');
                table = comment < 0 ? default : table[comment..];
                continue;
            }

            var length = 0;
            while (length < table.Length && table[length] > (byte)' ')
            {
                length++;
            }

            var token = Encoding.Latin1.GetString(table[..length]);
            table = table[length..];

            if (current is null)
            {
                if (!byName.TryGetValue(token, out current))
                {
                    current = [];
                    byName[token] = current;
                }
            }
            else
            {
                current.Add(token);
            }
        }
    }

    private static void Overlay(Metafile metadata, Dictionary<string, List<string>> byName)
    {
        for (var group = 0; group < metadata.GroupCount; group++)
        {
            var name = metadata.GetGroupName(group);
            if (name.Length == 0)
            {
                continue;
            }

            if (!byName.TryGetValue(name, out var entries))
            {
                entries = [];
                byName[name] = entries;
            }

            var count = metadata.GetValueCount(group);
            for (var value = 0; value < count; value++)
            {
                entries.Clear();
                entries.Add(Encoding.Latin1.GetString(metadata.GetValue(group, value)));
            }
        }
    }
}
