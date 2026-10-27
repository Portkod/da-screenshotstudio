namespace DaScreenshotStudio.Engine.Rendering;

public readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    public static PixelRect Empty => default;

    public int Right => X + Width;

    public int Bottom => Y + Height;

    public bool IsEmpty => Width <= 0 || Height <= 0;

    public static PixelRect FromEdges(int left, int top, int right, int bottom) =>
        new(left, top, right - left, bottom - top);

    public PixelRect Intersect(in PixelRect other)
    {
        var left = Math.Max(X, other.X);
        var top = Math.Max(Y, other.Y);
        var right = Math.Min(Right, other.Right);
        var bottom = Math.Min(Bottom, other.Bottom);

        return right <= left || bottom <= top ? default : FromEdges(left, top, right, bottom);
    }

    public bool IntersectsWith(in PixelRect other) =>
        X < other.Right && other.X < Right && Y < other.Bottom && other.Y < Bottom;

    public PixelRect Translate(int deltaX, int deltaY) => new(X + deltaX, Y + deltaY, Width, Height);

    public PixelRect Inflate(int horizontal, int vertical) =>
        new(X - horizontal, Y - vertical, Width + (horizontal * 2), Height + (vertical * 2));

    public bool Contains(int x, int y) => x >= X && y >= Y && x < Right && y < Bottom;
}
