using DaScreenshotStudio.Assets.Archives;
using DaScreenshotStudio.Assets.Palettes;
using DaScreenshotStudio.Engine.Rendering;
using DaScreenshotStudio.Engine.Core;

namespace DaScreenshotStudio.Engine.Text;

public sealed class TextPalette
{
    public const string EntryName = "legend.pal";

    public const byte DefaultIndex = 0x12;

    public const byte InterfaceIndex = 0x14;

    public const byte OutOfSyncIndex = 0x16;

    public const byte WhiteIndex = TextMarkup.White;

    public const byte ZoneIndex = WhiteIndex;

    public const byte ShadowIndex = 0x1F;

    private readonly ushort[] _colors = new ushort[Palette.ColorCount];

    public TextPalette(Palette palette, in PixelPacking packing) =>
        PalettePacker.PackNamed(palette, in packing, _colors);

    private TextPalette(in PixelPacking packing, bool _)
    {
        for (var i = 0; i < _colors.Length; i++)
        {
            var level = (byte)i;
            var packed = packing.Pack(level, level, level);
            _colors[i] = i == 0 || packed != PalettePacker.TransparentKey ? packed : PalettePacker.BumpedBlack;
        }
    }

    public ReadOnlySpan<ushort> Colors => _colors;

    public ushort this[int index] => _colors[(uint)index < (uint)_colors.Length ? index : 0];

    public static TextPalette? TryLoad(IAssetArchive archive, in PixelPacking packing)
    {
        ArgumentNullException.ThrowIfNull(archive);
        return archive.TryRead(EntryName, out var data) ? new TextPalette(Palette.Parse(data), in packing) : null;
    }

    public static TextPalette Fallback(in PixelPacking packing) => new(in packing, false);
}
