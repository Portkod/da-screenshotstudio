using DaScreenshotStudio.Engine.Rendering;

namespace DaScreenshotStudio.Engine.World;

public sealed class DamageEffectAtlas
{
    public const int StageCount = 26;

    public const int CellWidth = 27;

    public const int CellHeight = 5;

    public const int AtlasHeight = StageCount * CellHeight;

    public const byte BorderIndex = 0x1F;

    public const byte LowIndex = 0x28;

    public const byte MediumIndex = 0x97;

    public const byte HighIndex = 0x89;

    private const int InteriorRight = CellWidth - 1;

    private readonly ushort[] _pixels = new ushort[CellWidth * AtlasHeight];

    public DamageEffectAtlas(ReadOnlySpan<ushort> palette)
    {
        var border = Color(palette, BorderIndex);
        _pixels.AsSpan().Fill(border);

        for (var stage = 0; stage < StageCount; stage++)
        {
            var fill = Color(palette, IndexForStage(stage));
            var top = (stage * CellHeight) + 1;

            for (var row = top; row < top + CellHeight - 2; row++)
            {
                var start = row * CellWidth;

                for (var column = 1; column < InteriorRight; column++)
                {
                    _pixels[start + column] = column <= stage ? fill : (ushort)0;
                }
            }
        }
    }

    public static byte IndexForStage(int stage) => stage switch
    {
        > 12 => HighIndex,
        > 5 => MediumIndex,
        _ => LowIndex,
    };

    public Pixmap Cell(int stage)
    {
        var clamped = Math.Clamp(stage, 0, StageCount - 1);
        return new Pixmap(_pixels.AsMemory(clamped * CellHeight * CellWidth), CellWidth, CellHeight);
    }

    private static ushort Color(ReadOnlySpan<ushort> palette, byte index) =>
        (uint)index < (uint)palette.Length ? palette[index] : PalettePacker.BumpedBlack;
}
