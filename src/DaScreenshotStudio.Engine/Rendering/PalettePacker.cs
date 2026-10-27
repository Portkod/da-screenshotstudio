using DaScreenshotStudio.Assets.Palettes;

namespace DaScreenshotStudio.Engine.Rendering;

public static class PalettePacker
{
    public const ushort TransparentKey = 0;

    public const ushort BumpedBlack = 1;

    public const int ColorCount = Palette.ColorCount;

    public const int GuiFixupIndex = 255;

    public const int NamedWhiteIndex = 255;

    public static void Pack(Palette palette, in PixelPacking packing, Span<ushort> destination)
    {
        PackWithoutKey(palette, in packing, destination);
        destination[0] = TransparentKey;
    }

    public static void PackNamed(Palette palette, in PixelPacking packing, Span<ushort> destination)
    {
        PackWithoutKey(palette, in packing, destination);

        destination[NamedWhiteIndex] = packing.Pack(byte.MaxValue, byte.MaxValue, byte.MaxValue);
        destination[0] = TransparentKey;
    }

    public static void PackGui(Palette palette, in PixelPacking packing, Span<ushort> destination)
    {
        PackWithoutKey(palette, in packing, destination);

        destination[GuiFixupIndex] = destination[0];
        if (destination[GuiFixupIndex] == TransparentKey)
        {
            destination[GuiFixupIndex] = BumpedBlack;
        }

        destination[0] = TransparentKey;
    }

    private static void PackWithoutKey(Palette palette, in PixelPacking packing, Span<ushort> destination)
    {
        ArgumentNullException.ThrowIfNull(palette);

        if (destination.Length < ColorCount)
        {
            throw new ArgumentException($"A packed palette needs {ColorCount} entries.", nameof(destination));
        }

        var colors = palette.Colors;
        for (var i = 0; i < ColorCount; i++)
        {
            var color = colors[i];
            var packed = packing.Pack(color.Red, color.Green, color.Blue);
            destination[i] = packed == TransparentKey ? BumpedBlack : packed;
        }
    }
}
