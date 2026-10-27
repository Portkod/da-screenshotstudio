using DaScreenshotStudio.Assets.Archives;
using DaScreenshotStudio.Assets.Maps;
using DaScreenshotStudio.Assets.Metadata;
using DaScreenshotStudio.Engine.Rendering;
using DaScreenshotStudio.Engine.World;

namespace DaScreenshotStudio.Engine.App;

public sealed class ClientAssets : IDisposable, IMapLightMasks
{
    public static readonly string[] MapArchiveNames = ["misc.dat", "ia.dat", "seo.dat"];

    private const int LightMaskNameLength = 10;

    private readonly ArchiveSet _archives;
    private readonly BoundedCache<HeaLightMask?> _lightMasks = new();
    private MetafileStore? _metafiles;
    private bool _disposed;

    private ClientAssets(ClientDirectory directory, ArchiveSet archives, MapArt art, MetafileStore? metafiles)
    {
        Files = directory;
        Directory = directory.Path;
        _archives = archives;
        Art = art;
        _metafiles = metafiles;
    }

    public ClientDirectory Files { get; }

    public string Directory { get; }

    public IAssetArchive Archives => _archives;

    public MapArt Art { get; }

    public MetafileStore Metafiles =>
        _metafiles ??= MetafileStore.Load(Files.Subdirectory(MetafileStore.DirectoryName).Path);

    public static ClientAssets Load(ClientDirectory clientDirectory, MetafileStore? metafiles = null)
    {
        ArgumentNullException.ThrowIfNull(clientDirectory);

        var archives = new ArchiveSet(clientDirectory.Path, ownsSources: true);
        try
        {
            var opened = 0;
            foreach (var name in MapArchiveNames)
            {
                if (clientDirectory.TryOpenArchive(name) is { } archive)
                {
                    archives.Add(archive);
                    opened++;
                }
            }

            if (opened == 0)
            {
                throw new FileNotFoundException(
                    $"'{clientDirectory.Path}' holds none of {string.Join(", ", MapArchiveNames)}.");
            }

            return new ClientAssets(clientDirectory, archives, MapArt.Load(archives), metafiles);
        }
        catch
        {
            archives.Dispose();
            throw;
        }
    }

    public HeaLightMask? LoadLightMask(int mapId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_lightMasks.TryTouch(mapId, out var cached))
        {
            return cached;
        }

        HeaLightMask? mask = null;
        Span<char> name = stackalloc char[LightMaskNameLength];

        if (mapId >= 0 && mapId.TryFormat(name, out var digits, "D6") && digits == 6)
        {
            ".hea".CopyTo(name[6..]);

            if (_archives.TryReadMemory(name, out var data))
            {
                try
                {
                    mask = HeaLightMask.Parse(data);
                }
                catch (InvalidDataException)
                {
                    mask = null;
                }
            }
        }

        _lightMasks.Store(mapId, mask, mask?.ByteLength ?? 0);
        return mask;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _archives.Dispose();
    }
}
