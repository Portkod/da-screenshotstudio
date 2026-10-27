using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DaScreenshotStudio.Engine.Rendering;

public class BoundedCache<TKey, TValue>
    where TKey : notnull
{
    public const int DefaultMaxEntries = 4096;

    public const int MaxFrames = 16384;

    public const long DefaultBudgetBytes = 24L * 1024 * 1024;

    private const int None = -1;

    private readonly Dictionary<TKey, int> _index = [];
    private readonly int _maxEntries;
    private readonly long _budgetBytes;
    private Node[] _nodes;
    private int _allocated;
    private int _free = None;

    private int _oldest = None;

    private int _newest = None;

    public BoundedCache(int maxEntries = DefaultMaxEntries, long budgetBytes = DefaultBudgetBytes)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxEntries, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(budgetBytes);

        _maxEntries = maxEntries;
        _budgetBytes = budgetBytes;
        _nodes = new Node[Math.Min(maxEntries, 16)];
    }

    public long RetainedBytes { get; private set; }

    public int Evictions { get; private set; }

    public bool TryTouch(TKey key, out TValue value)
    {
        ref var slot = ref CollectionsMarshal.GetValueRefOrNullRef(_index, key);
        if (Unsafe.IsNullRef(ref slot))
        {
            value = default!;
            return false;
        }

        MoveToNewest(slot);
        value = _nodes[slot].Value;
        return true;
    }

    public void Store(TKey key, TValue value, long bytes = 0)
    {
        ref var slot = ref CollectionsMarshal.GetValueRefOrNullRef(_index, key);
        if (!Unsafe.IsNullRef(ref slot))
        {
            ref var existing = ref _nodes[slot];
            RetainedBytes += bytes - existing.Bytes;
            existing.Value = value;
            existing.Bytes = bytes;
            MoveToNewest(slot);
            return;
        }

        while (_index.Count > 0 && (_index.Count + 1 > _maxEntries || RetainedBytes + bytes > _budgetBytes))
        {
            EvictOldest();
        }

        var index = Take();
        ref var node = ref _nodes[index];
        node.Key = key;
        node.Value = value;
        node.Bytes = bytes;
        node.Previous = None;
        node.Next = None;

        _index[key] = index;
        Link(index);
        RetainedBytes += bytes;
    }

    private void EvictOldest()
    {
        var index = _oldest;
        ref var node = ref _nodes[index];

        _index.Remove(node.Key);
        Unlink(index);
        RetainedBytes -= node.Bytes;
        Evictions++;

        node = default;
        node.Next = _free;
        _free = index;
    }

    private int Take()
    {
        if (_free != None)
        {
            var index = _free;
            _free = _nodes[index].Next;
            return index;
        }

        if (_allocated == _nodes.Length)
        {
            Array.Resize(ref _nodes, Math.Min(_maxEntries, _nodes.Length * 2));
        }

        return _allocated++;
    }

    private void MoveToNewest(int index)
    {
        if (_newest == index)
        {
            return;
        }

        Unlink(index);
        Link(index);
    }

    private void Link(int index)
    {
        ref var node = ref _nodes[index];
        node.Previous = _newest;
        node.Next = None;

        if (_newest != None)
        {
            _nodes[_newest].Next = index;
        }
        else
        {
            _oldest = index;
        }

        _newest = index;
    }

    private void Unlink(int index)
    {
        ref var node = ref _nodes[index];

        if (node.Previous != None)
        {
            _nodes[node.Previous].Next = node.Next;
        }
        else
        {
            _oldest = node.Next;
        }

        if (node.Next != None)
        {
            _nodes[node.Next].Previous = node.Previous;
        }
        else
        {
            _newest = node.Previous;
        }

        node.Previous = None;
        node.Next = None;
    }

    private struct Node
    {
        public TKey Key;

        public TValue Value;

        public long Bytes;

        public int Previous;

        public int Next;
    }
}

public sealed class BoundedCache<TValue>(
    int maxEntries = BoundedCache<long, TValue>.DefaultMaxEntries,
    long budgetBytes = BoundedCache<long, TValue>.DefaultBudgetBytes)
    : BoundedCache<long, TValue>(maxEntries, budgetBytes);
