using DaScreenshotStudio.Assets.Archives;
using DaScreenshotStudio.Assets.Imaging;
using DaScreenshotStudio.Engine.Rendering;

namespace DaScreenshotStudio.Engine.World;

public sealed class ObjectLightMask
{
    private readonly byte[] _pixels;

    private ObjectLightMask(byte[] pixels, int width, int height)
    {
        _pixels = pixels;
        Width = width;
        Height = height;
    }

    public int Width { get; }

    public int Height { get; }

    public int OffsetX => -(Width / 2);

    public int OffsetY => -(Height / 2);

    public static string NameFor(int selector) => $"mask1{selector:D2}.epf";

    public ReadOnlySpan<byte> Row(int y) => _pixels.AsSpan(y * Width, Width);

    public static ObjectLightMask? FromEpf(EpfImage image)
    {
        ArgumentNullException.ThrowIfNull(image);

        if (image.FrameCount == 0)
        {
            return null;
        }

        var frame = image[0];
        var pixels = image.GetPixels(0);

        if (frame.IsEmpty || pixels.Length < frame.PixelCount)
        {
            return null;
        }

        return new ObjectLightMask(pixels[..(int)frame.PixelCount].ToArray(), frame.Width, frame.Height);
    }
}

public interface IObjectLightMasks
{
    ObjectLightMask? LoadObjectLightMask(int selector);
}

public sealed class ObjectLightMasks : IObjectLightMasks
{
    public const int NoMask = 0;

    private readonly IAssetArchive _archive;
    private readonly BoundedCache<ObjectLightMask?> _masks = new();

    public ObjectLightMasks(IAssetArchive archive)
    {
        ArgumentNullException.ThrowIfNull(archive);
        _archive = archive;
    }

    public int DecodedMasks { get; private set; }

    public ObjectLightMask? LoadObjectLightMask(int selector)
    {
        if (selector <= NoMask)
        {
            return null;
        }

        if (_masks.TryTouch(selector, out var cached))
        {
            return cached;
        }

        ObjectLightMask? mask = null;

        if (_archive.TryReadMemory(ObjectLightMask.NameFor(selector), out var data))
        {
            try
            {
                mask = ObjectLightMask.FromEpf(EpfImage.Parse(data));
                if (mask is not null)
                {
                    DecodedMasks++;
                }
            }
            catch (InvalidDataException)
            {
                mask = null;
            }
        }

        _masks.Store(selector, mask);
        return mask;
    }
}
