namespace DaScreenshotStudio.Engine.World;

internal static class WalkTable
{
    public const int MaxRow = 8;

    public static ReadOnlySpan<byte> RowStart => [0, 0, 1, 3, 6, 10, 15, 21, 28];

    public static ReadOnlySpan<byte> StepX =>
    [
        28,
        14, 14,
        8, 10, 10,
        6, 8, 6, 8,
        4, 6, 6, 6, 6,
        4, 4, 6, 4, 4, 6,
        4, 4, 4, 4, 4, 4, 4,
        2, 4, 4, 4, 2, 4, 4, 4,
    ];

    public static ReadOnlySpan<byte> StepY =>
    [
        14,
        7, 7,
        4, 5, 5,
        3, 4, 3, 4,
        2, 3, 3, 3, 3,
        2, 2, 3, 2, 2, 3,
        2, 2, 2, 2, 2, 2, 2,
        1, 2, 2, 2, 1, 2, 2, 2,
    ];

    public static ReadOnlySpan<byte> Frames =>
    [
        0,
        2, 4,
        2, 3, 4,
        1, 2, 3, 4,
        1, 2, 2, 3, 4,
        1, 1, 2, 2, 3, 4,
        1, 1, 2, 2, 3, 3, 4,
        1, 1, 2, 2, 3, 3, 4, 4,
    ];

    public static ReadOnlySpan<ushort> DelaysA => DelaysAStorage;

    private static readonly ushort[] DelaysAStorage =
    [
        400,
        200, 200,
        130, 130, 140,
        100, 100, 100, 100,
        80, 80, 80, 80, 80,
        60, 70, 70, 60, 70, 70,
        60, 60, 50, 60, 60, 50, 60,
        50, 50, 50, 50, 50, 50, 50, 50,
    ];

    public static bool HasRow(int row) => (uint)(row - 1) < (uint)MaxRow;

    public static int At(int row, int step) => RowStart[row] + step;
}
