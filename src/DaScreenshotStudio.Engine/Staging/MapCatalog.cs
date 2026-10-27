using System.Text.Json;
using System.Text.Json.Serialization;

namespace DaScreenshotStudio.Engine.Staging;

public sealed class MapCatalog
{
    private readonly SortedDictionary<int, MapEntry> _entries = [];

    public IEnumerable<MapEntry> Entries => _entries.Values;

    public MapEntry? Find(int id) => _entries.GetValueOrDefault(id);

    public void Set(MapEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        _entries[entry.Id] = entry;
    }

    public bool Remove(int id) => _entries.Remove(id);

    public static MapCatalog Load(string path)
    {
        var catalog = new MapCatalog();
        if (!File.Exists(path))
        {
            return catalog;
        }

        var entries = JsonSerializer.Deserialize(File.ReadAllText(path), MapCatalogJson.Default.ListMapEntry) ?? [];
        foreach (var entry in entries)
        {
            catalog.Set(entry);
        }

        return catalog;
    }

    public void Save(string path) =>
        File.WriteAllText(path, JsonSerializer.Serialize([.. _entries.Values], MapCatalogJson.Default.ListMapEntry));
}

public sealed class MapEntry
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public int Width { get; set; }

    public int Height { get; set; }
}

[JsonSourceGenerationOptions(WriteIndented = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true)]
[JsonSerializable(typeof(List<MapEntry>))]
internal sealed partial class MapCatalogJson : JsonSerializerContext;
