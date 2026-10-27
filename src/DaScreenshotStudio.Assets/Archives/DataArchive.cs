using System.Buffers;
using System.Buffers.Binary;
using System.IO.MemoryMappedFiles;
using System.Text;

namespace DaScreenshotStudio.Assets.Archives;

public sealed class DataArchive : IAssetArchive, IDisposable
{
    private const uint ExtendedLayoutMarker = 0xFFFFFFFF;

    private readonly MemoryMappedFile _file;
    private readonly MemoryMappedViewAccessor _view;
    private readonly MappedMemoryManager _memory;
    private readonly DataArchiveEntry[] _entries;
    private readonly Dictionary<string, int> _index;
    private readonly Dictionary<string, int>.AlternateLookup<ReadOnlySpan<char>> _lookup;
    private bool _disposed;

    public string Name { get; }

    public IReadOnlyList<DataArchiveEntry> Entries => _entries;

    public int Length { get; }

    private DataArchive(string path, MemoryMappedFile file, MemoryMappedViewAccessor view, int length)
    {
        Name = Path.GetFileName(path);
        Length = length;
        _file = file;
        _view = view;
        _memory = new MappedMemoryManager(view, length);

        try
        {
            var whole = _memory.GetSpan();
            _entries = ReadIndex(whole, Name);
        }
        catch
        {
            ((IDisposable)_memory).Dispose();
            throw;
        }

        _index = new Dictionary<string, int>(_entries.Length, StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < _entries.Length; i++)
        {
            _index[_entries[i].Name] = i;
        }

        _lookup = _index.GetAlternateLookup<ReadOnlySpan<char>>();
    }

    public static DataArchive Open(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        var info = new FileInfo(path);
        if (!info.Exists)
        {
            throw new FileNotFoundException("Archive not found.", path);
        }

        if (info.Length is < 4 or > int.MaxValue)
        {
            throw new InvalidDataException($"'{path}' is {info.Length} bytes, which is not a readable DAT archive.");
        }

        var file = MemoryMappedFile.CreateFromFile(path, FileMode.Open, mapName: null, 0, MemoryMappedFileAccess.Read);
        try
        {
            var view = file.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
            try
            {
                return new DataArchive(path, file, view, (int)info.Length);
            }
            catch
            {
                view.Dispose();
                throw;
            }
        }
        catch
        {
            file.Dispose();
            throw;
        }
    }

    public bool Contains(scoped ReadOnlySpan<char> name) => _lookup.ContainsKey(name);

    public bool TryFind(scoped ReadOnlySpan<char> name, out DataArchiveEntry entry)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_lookup.TryGetValue(name, out var position))
        {
            entry = _entries[position];
            return true;
        }

        entry = default;
        return false;
    }

    public ReadOnlySpan<byte> Read(scoped in DataArchiveEntry entry)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _memory.GetSpan().Slice(entry.Offset, entry.Length);
    }

    public ReadOnlyMemory<byte> ReadMemory(scoped in DataArchiveEntry entry)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _memory.Memory.Slice(entry.Offset, entry.Length);
    }

    public ReadOnlySpan<byte> Read(scoped ReadOnlySpan<char> name)
    {
        if (!TryFind(name, out var entry))
        {
            throw new KeyNotFoundException($"'{Name}' has no entry named '{name}'.");
        }

        return Read(in entry);
    }

    public bool TryRead(scoped ReadOnlySpan<char> name, out ReadOnlySpan<byte> data)
    {
        if (!TryFind(name, out var entry))
        {
            data = default;
            return false;
        }

        data = Read(in entry);
        return true;
    }

    public bool TryReadMemory(scoped ReadOnlySpan<char> name, out ReadOnlyMemory<byte> data)
    {
        if (!TryFind(name, out var entry))
        {
            data = default;
            return false;
        }

        data = ReadMemory(in entry);
        return true;
    }

    private static DataArchiveEntry[] ReadIndex(ReadOnlySpan<byte> archive, string name)
    {
        var rowCount = BinaryPrimitives.ReadUInt32LittleEndian(archive);
        if (rowCount == ExtendedLayoutMarker)
        {
            throw new NotSupportedException(
                $"'{name}' uses the extended DAT layout. Its header fields are not fully known and no sample exists to verify a reader against.");
        }

        if (rowCount < 1 || (long)rowCount * DataArchiveEntry.RowLength + 4 > archive.Length)
        {
            throw new InvalidDataException($"'{name}' declares {rowCount} index rows, which does not fit the file.");
        }

        var count = (int)rowCount - 1;
        var entries = new DataArchiveEntry[count];
        if (count == 0)
        {
            return entries;
        }

        var index = archive.Slice(4, (int)rowCount * DataArchiveEntry.RowLength);
        var previousOffset = 0;

        var names = ArrayPool<char>.Shared.Rent(DataArchiveEntry.NameLength);
        try
        {
            for (var i = 0; i < count; i++)
            {
                var row = index.Slice(i * DataArchiveEntry.RowLength, DataArchiveEntry.RowLength);
                var offset = ReadOffset(row, archive.Length, name);
                var next = ReadOffset(index.Slice((i + 1) * DataArchiveEntry.RowLength, DataArchiveEntry.RowLength), archive.Length, name);

                if (offset < previousOffset || next < offset)
                {
                    throw new InvalidDataException($"'{name}' index row {i} is out of order.");
                }

                previousOffset = offset;

                var raw = row.Slice(4, DataArchiveEntry.NameLength);
                var terminator = raw.IndexOf((byte)0);
                var written = Encoding.ASCII.GetChars(terminator < 0 ? raw : raw[..terminator], names);

                entries[i] = new DataArchiveEntry(new string(names, 0, written), offset, next - offset);
            }
        }
        finally
        {
            ArrayPool<char>.Shared.Return(names);
        }

        return entries;
    }

    private static int ReadOffset(ReadOnlySpan<byte> row, int archiveLength, string name)
    {
        var offset = BinaryPrimitives.ReadUInt32LittleEndian(row);
        if (offset > (uint)archiveLength)
        {
            throw new InvalidDataException($"'{name}' has an index offset of {offset} past its {archiveLength} byte end.");
        }

        return (int)offset;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ((IDisposable)_memory).Dispose();
        _view.Dispose();
        _file.Dispose();
    }

    private sealed unsafe class MappedMemoryManager : MemoryManager<byte>
    {
        private readonly MemoryMappedViewAccessor _view;
        private readonly int _length;
        private byte* _pointer;

        public MappedMemoryManager(MemoryMappedViewAccessor view, int length)
        {
            _view = view;
            _length = length;
            view.SafeMemoryMappedViewHandle.AcquirePointer(ref _pointer);
            _pointer += view.PointerOffset;
        }

        public override Span<byte> GetSpan()
        {
            ObjectDisposedException.ThrowIf(_pointer is null, typeof(DataArchive));
            return new(_pointer, _length);
        }

        public override MemoryHandle Pin(int elementIndex = 0) => new(_pointer + elementIndex);

        public override void Unpin()
        {
        }

        protected override void Dispose(bool disposing)
        {
            if (_pointer is not null)
            {
                _pointer = null;
                _view.SafeMemoryMappedViewHandle.ReleasePointer();
            }
        }
    }
}
