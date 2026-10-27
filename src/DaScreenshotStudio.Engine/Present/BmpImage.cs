using System.Buffers.Binary;
using DaScreenshotStudio.Engine.Rendering;

namespace DaScreenshotStudio.Engine.Present;

public static class BmpImage
{
    public const int FileHeaderSize = 0x0E;

    public const int InfoHeaderSize = 0x28;

    public const int PixelOffset = FileHeaderSize + InfoHeaderSize;

    public const int BitsPerPixel = 16;

    public const int PixelsPerMeter = 0x0EC4;

    public const ushort Signature = 0x4D42;

    public static byte[] Encode(PixelSurface surface, in PixelPacking packing)
    {
        ArgumentNullException.ThrowIfNull(surface);

        var width = surface.Width;
        var height = surface.Height;
        var payload = width * height * 2;
        var file = new byte[PixelOffset + payload];

        BinaryPrimitives.WriteUInt16LittleEndian(file, Signature);
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(2), PixelOffset + payload);

        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(10), PixelOffset);

        var info = file.AsSpan(FileHeaderSize);
        BinaryPrimitives.WriteInt32LittleEndian(info, InfoHeaderSize);
        BinaryPrimitives.WriteInt32LittleEndian(info[4..], width);
        BinaryPrimitives.WriteInt32LittleEndian(info[8..], height);
        BinaryPrimitives.WriteUInt16LittleEndian(info[12..], 1);
        BinaryPrimitives.WriteUInt16LittleEndian(info[14..], BitsPerPixel);

        BinaryPrimitives.WriteInt32LittleEndian(info[20..], payload);
        BinaryPrimitives.WriteInt32LittleEndian(info[24..], PixelsPerMeter);
        BinaryPrimitives.WriteInt32LittleEndian(info[28..], PixelsPerMeter);

        WritePixels(surface, in packing, file.AsSpan(PixelOffset));
        return file;
    }

    private static void WritePixels(PixelSurface surface, in PixelPacking packing, Span<byte> payload)
    {
        var width = surface.Width;
        var pixels = surface.ReadOnlyPixels;
        var native = packing.Format == RenderPixelFormat.Rgb555;
        var write = 0;

        for (var y = surface.Height - 1; y >= 0; y--)
        {
            var row = pixels.Slice(y * surface.Pitch, width);

            for (var x = 0; x < width; x++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(
                    payload[write..], native ? row[x] : ToRgb555(row[x]));

                write += 2;
            }
        }
    }

    public static ushort ToRgb555(ushort pixel) => (ushort)(
        ((pixel & 0xF800) >> 1) | (((pixel & 0x07E0) >> 6) << 5) | (pixel & 0x001F));
}
