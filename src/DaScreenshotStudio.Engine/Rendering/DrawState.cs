namespace DaScreenshotStudio.Engine.Rendering;

public enum BlendMode
{
    Copy,

    Transparent,

    Average,

    Screen,

    Alpha,

    AlphaPlane,

    Additive,

    SourceWeighted,

    Shade,

    Darken,

    Veil,
}

public enum DitherMode
{
    None,

    Halftone,
}

public struct DrawState
{
    public ushort PrimaryColor;
    public BlendMode Mode;
    public DitherMode Dither;

    public int AlphaWeight;

    public static DrawState Default => new()
    {
        PrimaryColor = 1,
        Mode = BlendMode.Copy,
        Dither = DitherMode.None,
        AlphaWeight = PixelBlender.MaxAlphaWeight,
    };
}
