using System.Buffers;
using DaScreenshotStudio.Assets.Archives;

namespace DaScreenshotStudio.Engine.App;

public sealed class ClientDirectory
{
    private readonly string _path;

    public ClientDirectory(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        _path = path;
    }

    public string Path => _path;

    public ClientDirectory Subdirectory(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        return new ClientDirectory(System.IO.Path.Join(_path, name));
    }

    public string PathOf(string fileName)
    {
        ArgumentException.ThrowIfNullOrEmpty(fileName);

        var exact = System.IO.Path.Join(_path, fileName);
        if (File.Exists(exact) || Directory.Exists(exact))
        {
            return exact;
        }

        var folder = System.IO.Path.GetDirectoryName(exact);
        var name = System.IO.Path.GetFileName(exact);
        try
        {
            if (folder is not null && Directory.Exists(folder))
            {
                foreach (var candidate in Directory.EnumerateFileSystemEntries(folder))
                {
                    if (string.Equals(System.IO.Path.GetFileName(candidate), name, StringComparison.OrdinalIgnoreCase))
                    {
                        return candidate;
                    }
                }
            }
        }
        catch (Exception error) when (IsRefusal(error))
        {
        }

        return exact;
    }

    public byte[]? TryReadAll(string fileName)
    {
        var path = PathOf(fileName);

        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length > int.MaxValue)
            {
                return null;
            }

            if (info.Length == 0)
            {
                return [];
            }

            var bytes = new byte[info.Length];
            var read = Read(path, bytes);
            if (read < 0)
            {
                return null;
            }

            if (read < bytes.Length)
            {
                Array.Resize(ref bytes, read);
            }

            return bytes;
        }
        catch (Exception error) when (IsRefusal(error))
        {
            return null;
        }
    }

    public bool TryRent(string fileName, out RentedFile file)
    {
        file = default;
        var path = PathOf(fileName);

        byte[]? buffer = null;
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length > int.MaxValue)
            {
                return false;
            }

            if (info.Length == 0)
            {
                return true;
            }

            buffer = ArrayPool<byte>.Shared.Rent((int)info.Length);
            var read = Read(path, buffer.AsSpan(0, (int)info.Length));
            if (read < 0)
            {
                return false;
            }

            file = new RentedFile(buffer, read);
            buffer = null;
            return true;
        }
        catch (Exception error) when (IsRefusal(error))
        {
            return false;
        }
        finally
        {
            if (buffer is not null)
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }

    public int TryReadInto(string fileName, Span<byte> destination)
    {
        var path = PathOf(fileName);

        try
        {
            return File.Exists(path) ? Read(path, destination) : -1;
        }
        catch (Exception error) when (IsRefusal(error))
        {
            return -1;
        }
    }

    public DataArchive? TryOpenArchive(string fileName)
    {
        var path = PathOf(fileName);
        return File.Exists(path) ? DataArchive.Open(path) : null;
    }

    public string[] ListFileNames(string searchPattern)
    {
        ArgumentException.ThrowIfNullOrEmpty(searchPattern);

        string[] matches;
        try
        {
            if (!Directory.Exists(_path))
            {
                return [];
            }

            matches = Directory.GetFiles(_path, searchPattern);
        }
        catch (Exception error) when (IsRefusal(error))
        {
            return [];
        }

        for (var i = 0; i < matches.Length; i++)
        {
            matches[i] = System.IO.Path.GetFileName(matches[i]);
        }

        Array.Sort(matches, StringComparer.OrdinalIgnoreCase);
        return matches;
    }

    public override string ToString() => _path;

    private static bool IsRefusal(Exception error) =>
        error is IOException or UnauthorizedAccessException or NotSupportedException;

    private static int Read(string path, Span<byte> destination)
    {
        using var handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

        var total = 0;
        while (total < destination.Length)
        {
            var read = RandomAccess.Read(handle, destination[total..], total);
            if (read <= 0)
            {
                break;
            }

            total += read;
        }

        return total;
    }
}

public ref struct RentedFile
{
    private byte[]? _buffer;

    internal RentedFile(byte[] buffer, int length)
    {
        _buffer = buffer;
        Length = length;
    }

    public int Length { get; }

    public readonly Span<byte> Span => _buffer is null ? default : _buffer.AsSpan(0, Length);

    public void Dispose()
    {
        if (_buffer is { } buffer)
        {
            _buffer = null;
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
