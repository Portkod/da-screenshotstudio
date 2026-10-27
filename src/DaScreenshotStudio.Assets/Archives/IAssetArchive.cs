namespace DaScreenshotStudio.Assets.Archives;

public interface IAssetArchive
{
    string Name { get; }

    IReadOnlyList<DataArchiveEntry> Entries { get; }

    bool Contains(scoped ReadOnlySpan<char> name);

    ReadOnlySpan<byte> Read(scoped ReadOnlySpan<char> name);

    bool TryRead(scoped ReadOnlySpan<char> name, out ReadOnlySpan<byte> data);

    bool TryReadMemory(scoped ReadOnlySpan<char> name, out ReadOnlyMemory<byte> data);
}
