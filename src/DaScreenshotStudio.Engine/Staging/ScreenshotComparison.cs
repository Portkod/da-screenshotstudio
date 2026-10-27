using System.Buffers.Binary;
using DaScreenshotStudio.Engine.Present;

namespace DaScreenshotStudio.Engine.Staging;

public sealed class Screenshot
{
    private Screenshot(int width, int height, ushort[] pixels)
    {
        Width = width;
        Height = height;
        Pixels = pixels;
    }

    public int Width { get; }

    public int Height { get; }

    public ushort[] Pixels { get; }

    public ushort this[int x, int y] => Pixels[(y * Width) + x];

    public static Screenshot Read(string path) => Decode(File.ReadAllBytes(path));

    public static Screenshot Decode(ReadOnlySpan<byte> file)
    {
        if (file.Length < BmpImage.PixelOffset || BinaryPrimitives.ReadUInt16LittleEndian(file) != BmpImage.Signature)
        {
            throw new InvalidDataException("Not a bitmap.");
        }

        var offset = BinaryPrimitives.ReadInt32LittleEndian(file[10..]);
        var info = file[BmpImage.FileHeaderSize..];
        var width = BinaryPrimitives.ReadInt32LittleEndian(info[4..]);
        var height = BinaryPrimitives.ReadInt32LittleEndian(info[8..]);
        var bits = BinaryPrimitives.ReadUInt16LittleEndian(info[14..]);
        var compression = BinaryPrimitives.ReadInt32LittleEndian(info[16..]);

        if (bits != BmpImage.BitsPerPixel || compression != 0 || width <= 0 || height == 0)
        {
            throw new InvalidDataException($"Expected an uncompressed 16-bit bitmap, found {bits} bits with compression {compression}.");
        }

        var bottomUp = height > 0;
        height = Math.Abs(height);
        var stride = ((width * 2) + 3) & ~3;
        var pixels = new ushort[width * height];

        for (var y = 0; y < height; y++)
        {
            var row = file.Slice(offset + ((bottomUp ? height - 1 - y : y) * stride), width * 2);
            for (var x = 0; x < width; x++)
            {
                pixels[(y * width) + x] = BinaryPrimitives.ReadUInt16LittleEndian(row[(x * 2)..]);
            }
        }

        return new Screenshot(width, height, pixels);
    }

    public static (byte R, byte G, byte B) Expand(ushort pixel) => (
        (byte)(((pixel >> 10) & 0x1F) << 3),
        (byte)(((pixel >> 5) & 0x1F) << 3),
        (byte)((pixel & 0x1F) << 3));
}

public sealed class ScreenshotDiff
{
    private ScreenshotDiff(Screenshot expected, Screenshot actual, bool[] mismatch, int count)
    {
        Expected = expected;
        Actual = actual;
        Mismatch = mismatch;
        Count = count;
    }

    public Screenshot Expected { get; }

    public Screenshot Actual { get; }

    public bool[] Mismatch { get; }

    public int Count { get; }

    public bool IsExact => Count == 0;

    public static ScreenshotDiff Compare(Screenshot expected, Screenshot actual)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);

        if (expected.Width != actual.Width || expected.Height != actual.Height)
        {
            throw new ArgumentException(
                $"Sizes differ: {expected.Width}x{expected.Height} against {actual.Width}x{actual.Height}.");
        }

        var mismatch = new bool[expected.Pixels.Length];
        var count = 0;

        for (var i = 0; i < mismatch.Length; i++)
        {
            if (expected.Pixels[i] != actual.Pixels[i])
            {
                mismatch[i] = true;
                count++;
            }
        }

        return new ScreenshotDiff(expected, actual, mismatch, count);
    }

    public int CountIn(int left, int top, int width, int height)
    {
        var count = 0;
        var right = Math.Min(Expected.Width, left + width);
        var bottom = Math.Min(Expected.Height, top + height);

        for (var y = Math.Max(0, top); y < bottom; y++)
        {
            for (var x = Math.Max(0, left); x < right; x++)
            {
                if (Mismatch[(y * Expected.Width) + x])
                {
                    count++;
                }
            }
        }

        return count;
    }

    public (int Left, int Top, int Right, int Bottom)? Bounds()
    {
        int left = int.MaxValue, top = int.MaxValue, right = -1, bottom = -1;
        var width = Expected.Width;

        for (var i = 0; i < Mismatch.Length; i++)
        {
            if (!Mismatch[i])
            {
                continue;
            }

            var x = i % width;
            var y = i / width;
            left = Math.Min(left, x);
            right = Math.Max(right, x);
            top = Math.Min(top, y);
            bottom = Math.Max(bottom, y);
        }

        return right < 0 ? null : (left, top, right, bottom);
    }

    public byte[] EncodeHighlight()
    {
        var width = Expected.Width;
        var height = Expected.Height;
        var stride = ((width * 3) + 3) & ~3;
        var file = new byte[54 + (stride * height)];

        BinaryPrimitives.WriteUInt16LittleEndian(file, BmpImage.Signature);
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(2), file.Length);
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(10), 54);
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(14), 40);
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(18), width);
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(22), height);
        BinaryPrimitives.WriteUInt16LittleEndian(file.AsSpan(26), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(file.AsSpan(28), 24);
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(34), stride * height);

        for (var y = 0; y < height; y++)
        {
            var row = 54 + ((height - 1 - y) * stride);
            for (var x = 0; x < width; x++)
            {
                var i = (y * width) + x;
                var at = row + (x * 3);

                if (Mismatch[i])
                {
                    file[at] = 0xFF;
                    file[at + 1] = 0x00;
                    file[at + 2] = 0xFF;
                    continue;
                }

                var (r, g, b) = Screenshot.Expand(Expected.Pixels[i]);
                file[at] = (byte)(b / 3);
                file[at + 1] = (byte)(g / 3);
                file[at + 2] = (byte)(r / 3);
            }
        }

        return file;
    }
}
