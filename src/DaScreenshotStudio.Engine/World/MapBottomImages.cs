using DaScreenshotStudio.Assets.Archives;
using DaScreenshotStudio.Assets.Imaging;
using DaScreenshotStudio.Assets.Maps;
using DaScreenshotStudio.Assets.Palettes;
using DaScreenshotStudio.Engine.Rendering;

namespace DaScreenshotStudio.Engine.World;

public sealed class MapBottomImages
{
    private readonly IAssetArchive _archive;
    private readonly PixelPacking _packing;
    private readonly List<int> _ids = [];
    private readonly List<Pixmap> _images = [];
    private readonly ushort[] _colors = new ushort[Palette.ColorCount];

    public MapBottomImages(IAssetArchive archive, in PixelPacking packing)
    {
        ArgumentNullException.ThrowIfNull(archive);
        _archive = archive;
        _packing = packing;
    }

    public int Count => _images.Count;

    public Pixmap this[int index] =>
        (uint)index < (uint)_images.Count ? _images[index] : default;

    public int Resolve(int imageId)
    {
        for (var i = 0; i < _ids.Count; i++)
        {
            if (_ids[i] == imageId)
            {
                return i;
            }
        }

        if (Decode(imageId) is not { } decoded)
        {
            return -1;
        }

        _ids.Add(imageId);
        _images.Add(decoded);
        return _images.Count - 1;
    }

    public void Clear()
    {
        _ids.Clear();
        _images.Clear();
    }

    private Pixmap? Decode(int imageId)
    {
        Span<char> name = stackalloc char[MapBottomLayer.ImageNameLength];
        if (!MapBottomLayer.TryFormatImageName(imageId, name) || !_archive.TryReadMemory(name, out var data))
        {
            return null;
        }

        PcxImage image;
        try
        {
            image = PcxImage.Parse(data);
        }
        catch (InvalidDataException)
        {
            return null;
        }

        try
        {
            return IndexedPixmap.FromPcx(image, in _packing, _colors);
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }
}
