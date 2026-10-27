namespace DaScreenshotStudio.Assets.Maps;

public static class StaticStatePairs
{
    public const int ColumnCount = 2;

    public const byte Column1State = 0;

    public const byte Column0State = 1;

    private static ReadOnlySpan<ushort> Tiles =>
    [
        1994, 1997, 2000, 2003, 2163, 4519, 2164, 4520, 2165, 4521, 2196, 4532,
        2197, 4533, 2198, 4534, 2227, 4527, 2228, 4528, 2229, 4529, 2260, 4540,
        2261, 4541, 2262, 4542, 2291, 4523, 2292, 4524, 2293, 4525, 2328, 4536,
        2329, 4537, 2330, 4538, 2436, 2432, 2461, 2465, 2673, 2680, 2674, 2681,
        2675, 2682, 2687, 2694, 2688, 2695, 2689, 2696, 2714, 2721, 2715, 2722,
        2727, 2734, 2728, 2735, 2761, 2768, 2762, 2769, 2776, 2783, 2777, 2784,
        2850, 2857, 2851, 2858, 2852, 2859, 2874, 2881, 2875, 2882, 2876, 2883,
        2897, 2903, 2898, 2904, 2929, 2923, 2930, 2924, 2945, 2951, 2946, 2952,
        2971, 2977, 2972, 2978, 2993, 2999, 2994, 3000, 3019, 3025, 3020, 3026,
        3058, 3066, 3059, 3067, 3090, 3098, 3091, 3099, 3118, 3126, 3119, 3127,
        3150, 3158, 3159, 3151, 3178, 3186, 3179, 3187, 3210, 3218, 3211, 3219,
    ];

    public static ushort Tile(int row, int column) => Tiles[(row * ColumnCount) + column];

    public static int ColumnFor(byte state) => state == Column1State ? 1 : 0;

    public static byte StateFor(int column) => column == 1 ? Column1State : Column0State;

    public static bool TryFind(int staticTileId, out int row, out int column)
    {
        var tiles = Tiles;
        for (var i = 0; i < tiles.Length; i++)
        {
            if (tiles[i] == staticTileId)
            {
                row = i / ColumnCount;
                column = i % ColumnCount;
                return true;
            }
        }

        row = -1;
        column = -1;
        return false;
    }

    public static bool TryApplyState(int staticTileId, byte state, out ushort tile)
    {
        if (!TryFind(staticTileId, out var row, out _))
        {
            tile = 0;
            return false;
        }

        tile = Tile(row, ColumnFor(state));
        return true;
    }

    public static bool TryGetState(int staticTileId, out byte state)
    {
        if (!TryFind(staticTileId, out _, out var column))
        {
            state = Column0State;
            return false;
        }

        state = StateFor(column);
        return true;
    }
}
