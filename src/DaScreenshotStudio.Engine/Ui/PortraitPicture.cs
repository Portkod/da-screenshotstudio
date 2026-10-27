using DaScreenshotStudio.Assets.Imaging;
using DaScreenshotStudio.Engine.Rendering;
using DaScreenshotStudio.Engine.Text;

namespace DaScreenshotStudio.Engine.Ui;

public sealed class PortraitPicture
{
    public const int EpfPixelOffset = 12;

    public const int EpfStride = 0x30;

    private readonly ushort[] _pixels = new ushort[PortraitFile.Width * PortraitFile.Height];

    private bool _hasPicture;

    public bool HasPicture => _hasPicture;

    public Pixmap Picture =>
        _hasPicture ? new Pixmap(_pixels, PortraitFile.Width, PortraitFile.Height) : default;

    public void Clear() => _hasPicture = false;

    public bool TrySet(ReadOnlySpan<byte> file, in PixelPacking packing, TextPalette? palette)
    {
        _hasPicture = PortraitFile.Classify(file) switch
        {
            PortraitFormat.Jpeg => TrySetJpeg(file, in packing),
            PortraitFormat.Epf => TrySetEpf(file, palette),
            _ => false,
        };

        return _hasPicture;
    }

    private bool TrySetJpeg(ReadOnlySpan<byte> file, in PixelPacking packing)
    {
        if (!JpegDecoder.TryDecode(file, PortraitFile.Width, PortraitFile.Height, out var rgb, out _, out _))
        {
            return false;
        }

        for (var i = 0; i < _pixels.Length; i++)
        {
            var source = i * 3;
            _pixels[i] = packing.Pack(rgb[source], rgb[source + 1], rgb[source + 2]);
        }

        return true;
    }

    private bool TrySetEpf(ReadOnlySpan<byte> file, TextPalette? palette)
    {
        if (palette is null)
        {
            return false;
        }

        var block = file.Slice(EpfPixelOffset, PortraitFile.EpfPixelBlockLength);

        for (var y = 0; y < PortraitFile.Height; y++)
        {
            var row = block.Slice(y * EpfStride, PortraitFile.Width);
            for (var x = 0; x < PortraitFile.Width; x++)
            {
                var index = row[x];
                _pixels[(y * PortraitFile.Width) + x] =
                    index == 0 ? PalettePacker.TransparentKey : palette[index];
            }
        }

        return true;
    }
}
