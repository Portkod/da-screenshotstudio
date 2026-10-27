namespace DaScreenshotStudio.Engine.Core;

[Flags]
public enum MapFlags : byte
{
    None = 0x00,
    Snow = 0x01,
    Rain = 0x02,
    Darkness = 0x03,
    NoMap = 0x40,
    Winter = 0x80,
}

public static class MapFlagsExtensions
{
    public static MapFlags GetMode(this MapFlags flags) => (MapFlags)((byte)flags & 0x0F);
}
