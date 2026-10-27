namespace DaScreenshotStudio.Assets.Maps;

public sealed class SotpTable
{
    public const byte CollisionMask = 0x0F;

    public const byte ScreenBlend = 0x80;

    public const byte AlternateBlend = 0x40;

    private readonly byte[] _flags;

    private SotpTable(byte[] flags) => _flags = flags;

    public int HighestTileId => _flags.Length;

    public static SotpTable Empty { get; } = new([]);

    public static SotpTable Parse(ReadOnlySpan<byte> data) => new(data.ToArray());

    public byte GetFlags(int staticTileId)
    {
        var index = MapTileIds.SotpIndex(staticTileId);
        return (uint)index < (uint)_flags.Length ? _flags[index] : (byte)0;
    }

    public byte GetCollisionFlags(int staticTileId) =>
        staticTileId == MapTileIds.EmptyStaticTile ? (byte)0 : (byte)(GetFlags(staticTileId) & CollisionMask);

    public bool IsScreenBlend(int staticTileId) => (GetFlags(staticTileId) & ScreenBlend) != 0;

    public bool IsAlternateBlend(int staticTileId) =>
        (GetFlags(staticTileId) & (ScreenBlend | AlternateBlend)) == AlternateBlend;
}
