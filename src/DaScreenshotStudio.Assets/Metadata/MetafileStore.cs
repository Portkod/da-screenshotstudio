namespace DaScreenshotStudio.Assets.Metadata;

public sealed class MetafileStore
{
    public const string DirectoryName = "metafile";

    public const string ItemInfoPrefix = "ItemInfo";

    private readonly List<Metafile> _files;
    private readonly Dictionary<string, Metafile> _byName;
    private readonly Dictionary<string, Metafile>.AlternateLookup<ReadOnlySpan<char>> _lookup;

    private MetafileStore(List<Metafile> files)
    {
        _files = files;
        _byName = new Dictionary<string, Metafile>(files.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            _byName[file.Name] = file;
        }

        _lookup = _byName.GetAlternateLookup<ReadOnlySpan<char>>();
    }

    public IReadOnlyList<Metafile> Files => _files;

    public static MetafileStore Load(string directory)
    {
        ArgumentException.ThrowIfNullOrEmpty(directory);

        if (!System.IO.Directory.Exists(directory))
        {
            return new MetafileStore([]);
        }

        var paths = System.IO.Directory.GetFiles(directory);
        Array.Sort(paths, StringComparer.OrdinalIgnoreCase);

        var files = new List<Metafile>(paths.Length);
        foreach (var path in paths)
        {
            var name = Path.GetFileName(path);
            try
            {
                files.Add(Metafile.Parse(name, File.ReadAllBytes(path)));
            }
            catch (InvalidDataException)
            {
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
            }
        }

        return new MetafileStore(files);
    }

    public Metafile? Find(scoped ReadOnlySpan<char> name) => _lookup.TryGetValue(name, out var file) ? file : null;
}
