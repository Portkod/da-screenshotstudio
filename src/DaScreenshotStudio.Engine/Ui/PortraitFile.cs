using System.Buffers.Binary;
using DaScreenshotStudio.Engine.App;

namespace DaScreenshotStudio.Engine.Ui;

public enum PortraitFormat
{
    None,

    Jpeg,

    Epf,
}

public static class PortraitFile
{
    public const int Width = 0x30;

    public const int Height = 0x38;

    public const int MaxJpegLength = 0x1F40;

    public const int EpfLength = 0xB1C;

    public const int EpfPixelBlockLength = 0xAF0;

    private const int JfifMarkerOffset = 6;

    public const string WildcardCandidate = "*.epf";

    private static ReadOnlySpan<byte> JfifMarker => "JFIF"u8;

    public static byte[] Read(ClientDirectory? directory, string? characterName, out PortraitFormat format)
    {
        format = PortraitFormat.None;

        if (directory is null || characterName is not { Length: > 0 })
        {
            return [];
        }

        var file = TryOpenCandidate(directory, characterName);
        if (file is null)
        {
            return [];
        }

        format = Classify(file);
        return file;
    }

    public static PortraitFormat Classify(ReadOnlySpan<byte> file)
    {
        if (file.Length > JfifMarkerOffset + JfifMarker.Length
            && file.Slice(JfifMarkerOffset, JfifMarker.Length).SequenceEqual(JfifMarker))
        {
            return file.Length < MaxJpegLength ? PortraitFormat.Jpeg : PortraitFormat.None;
        }

        return IsPortraitEpf(file) ? PortraitFormat.Epf : PortraitFormat.None;
    }

    public static bool IsPortraitEpf(ReadOnlySpan<byte> file)
    {
        if (file.Length != EpfLength)
        {
            return false;
        }

        if (BinaryPrimitives.ReadUInt32LittleEndian(file[8..]) != EpfPixelBlockLength)
        {
            return false;
        }

        var header = file[(12 + EpfPixelBlockLength)..];
        if (header.Length < 8)
        {
            return false;
        }

        var top = (short)BinaryPrimitives.ReadUInt16LittleEndian(header);
        var left = (short)BinaryPrimitives.ReadUInt16LittleEndian(header[2..]);
        var bottom = (short)BinaryPrimitives.ReadUInt16LittleEndian(header[4..]);
        var right = (short)BinaryPrimitives.ReadUInt16LittleEndian(header[6..]);

        return bottom - top == Height && right - left == Width;
    }

    private static byte[]? TryOpenCandidate(ClientDirectory directory, string characterName) =>
        directory.TryReadAll(characterName)
        ?? directory.TryReadAll(characterName + ".jpg")
        ?? directory.TryReadAll(characterName + ".epf")
        ?? TryReadFirstWildcard(directory);

    private static byte[]? TryReadFirstWildcard(ClientDirectory directory)
    {
        foreach (var name in directory.ListFileNames(WildcardCandidate))
        {
            if (directory.TryReadAll(name) is { } found)
            {
                return found;
            }
        }

        return null;
    }
}
