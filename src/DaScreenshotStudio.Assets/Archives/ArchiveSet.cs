namespace DaScreenshotStudio.Assets.Archives;

public sealed class ArchiveSet : IAssetArchive, IDisposable
{
    private readonly List<IAssetArchive> _sources = [];
    private readonly Dictionary<string, IAssetArchive> _owners = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IAssetArchive>.AlternateLookup<ReadOnlySpan<char>> _lookup;
    private readonly List<DataArchiveEntry> _entries = [];
    private readonly bool _ownsSources;
    private bool _disposed;

    public string Name { get; }

    public IReadOnlyList<DataArchiveEntry> Entries => _entries;

    public ArchiveSet(string name, bool ownsSources = false)
    {
        Name = name;
        _ownsSources = ownsSources;
        _lookup = _owners.GetAlternateLookup<ReadOnlySpan<char>>();
    }

    public void Add(IAssetArchive archive)
    {
        ArgumentNullException.ThrowIfNull(archive);
        ObjectDisposedException.ThrowIf(_disposed, this);

        _sources.Add(archive);

        var entries = archive.Entries;
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (_owners.TryAdd(entry.Name, archive))
            {
                _entries.Add(entry);
            }
        }
    }

    public bool Contains(scoped ReadOnlySpan<char> name) => _lookup.ContainsKey(name);

    public ReadOnlySpan<byte> Read(scoped ReadOnlySpan<char> name)
    {
        if (!TryRead(name, out var data))
        {
            throw new KeyNotFoundException($"'{Name}' has no entry named '{name}'.");
        }

        return data;
    }

    public bool TryRead(scoped ReadOnlySpan<char> name, out ReadOnlySpan<byte> data)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_lookup.TryGetValue(name, out var owner))
        {
            return owner.TryRead(name, out data);
        }

        data = default;
        return false;
    }

    public bool TryReadMemory(scoped ReadOnlySpan<char> name, out ReadOnlyMemory<byte> data)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_lookup.TryGetValue(name, out var owner))
        {
            return owner.TryReadMemory(name, out data);
        }

        data = default;
        return false;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (!_ownsSources)
        {
            return;
        }

        foreach (var source in _sources)
        {
            (source as IDisposable)?.Dispose();
        }
    }
}
