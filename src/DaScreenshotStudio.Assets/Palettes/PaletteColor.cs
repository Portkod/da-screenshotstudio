using System.Runtime.InteropServices;

namespace DaScreenshotStudio.Assets.Palettes;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct PaletteColor : IEquatable<PaletteColor>
{
    public PaletteColor(byte red, byte green, byte blue)
    {
        Red = red;
        Green = green;
        Blue = blue;
    }

    public byte Red { get; }

    public byte Green { get; }

    public byte Blue { get; }

    public bool Equals(PaletteColor other) => Red == other.Red && Green == other.Green && Blue == other.Blue;

    public override bool Equals(object? obj) => obj is PaletteColor other && Equals(other);

    public override int GetHashCode() => (Red << 16) | (Green << 8) | Blue;

    public override string ToString() => $"#{Red:X2}{Green:X2}{Blue:X2}";

    public static bool operator ==(PaletteColor left, PaletteColor right) => left.Equals(right);

    public static bool operator !=(PaletteColor left, PaletteColor right) => !left.Equals(right);
}
