using DaScreenshotStudio.Assets.Maps;

namespace DaScreenshotStudio.Engine.World;

public sealed class TileFrameTable
{
    private TileAnimationTable? _table;
    private ushort[] _frames = [];
    private long _ticks = long.MinValue;

    public void Update(TileAnimationTable table, long ticks)
    {
        ArgumentNullException.ThrowIfNull(table);

        if (ReferenceEquals(table, _table) && ticks == _ticks)
        {
            return;
        }

        if (!ReferenceEquals(table, _table))
        {
            _table = table;

            if (_frames.Length < table.IdLimit)
            {
                _frames = new ushort[table.IdLimit];
            }

            for (var id = 0; id < table.IdLimit; id++)
            {
                _frames[id] = (ushort)id;
            }
        }

        _ticks = ticks;
        table.WriteFrames(ticks, _frames);
    }

    public int Resolve(int tileId) =>
        _table is { } table && (uint)tileId < (uint)table.IdLimit ? _frames[tileId] : tileId;
}
