namespace DaScreenshotStudio.Assets.Maps;

public static class StaticTileAnimations
{
    public const int GroupCount = 18;

    public const int MaximumFrames = 10;

    private static ReadOnlySpan<byte> Counts => [8, 8, 8, 4, 4, 4, 4, 4, 4, 7, 6, 6, 10, 10, 9, 9, 10, 10];

    private static ReadOnlySpan<ushort> Frames =>
    [
        7049, 7052, 7055, 7058, 7061, 7064, 7067, 7070, 0, 0,
        7050, 7053, 7056, 7059, 7062, 7065, 7068, 7071, 0, 0,
        7051, 7054, 7057, 7060, 7063, 7066, 7069, 7072, 0, 0,
        6849, 6852, 6855, 6858, 0, 0, 0, 0, 0, 0,
        6850, 6853, 6856, 6859, 0, 0, 0, 0, 0, 0,
        6851, 6854, 6857, 6860, 0, 0, 0, 0, 0, 0,
        7037, 7040, 7043, 7046, 0, 0, 0, 0, 0, 0,
        7038, 7041, 7044, 7047, 0, 0, 0, 0, 0, 0,
        7039, 7042, 7045, 7048, 0, 0, 0, 0, 0, 0,
        6842, 6843, 6844, 6845, 6846, 6847, 6848, 0, 0, 0,
        7297, 7299, 7301, 7303, 7305, 7307, 0, 0, 0, 0,
        7298, 7300, 7302, 7304, 7306, 7308, 0, 0, 0, 0,
        7309, 7311, 7313, 7315, 7317, 7319, 7321, 7323, 7325, 7327,
        7310, 7312, 7314, 7316, 7318, 7320, 7322, 7324, 7326, 7328,
        7329, 7331, 7333, 7335, 7337, 7339, 7341, 7343, 7345, 0,
        7330, 7332, 7334, 7336, 7338, 7340, 7342, 7344, 7346, 0,
        7347, 7349, 7351, 7353, 7355, 7357, 7359, 7361, 7363, 7365,
        7348, 7350, 7352, 7354, 7356, 7358, 7360, 7362, 7364, 7366,
    ];

    public const int LowestTileId = 6842;

    public const int HighestTileId = 7366;

    private static readonly short[] Placement = BuildPlacement();

    public static int FrameCount(int group) => Counts[group];

    public static ushort Frame(int group, int frame) => Frames[(group * MaximumFrames) + frame];

    public static bool TryFind(int staticTileId, out int group, out int frame)
    {
        var offset = staticTileId - LowestTileId;
        if ((uint)offset < (uint)Placement.Length && Placement[offset] >= 0)
        {
            group = Placement[offset] >> 4;
            frame = Placement[offset] & 0xF;
            return true;
        }

        group = -1;
        frame = -1;
        return false;
    }

    private static short[] BuildPlacement()
    {
        var placement = new short[HighestTileId - LowestTileId + 1];
        placement.AsSpan().Fill(-1);

        for (var group = 0; group < GroupCount; group++)
        {
            var count = Counts[group];
            for (var frame = 0; frame < count; frame++)
            {
                var offset = Frames[(group * MaximumFrames) + frame] - LowestTileId;
                if (placement[offset] < 0)
                {
                    placement[offset] = (short)((group << 4) | frame);
                }
            }
        }

        return placement;
    }

    public static bool TryResolve(int staticTileId, long elapsedFrames, out int animatedTileId)
    {
        if (!TryFind(staticTileId, out var group, out var frame))
        {
            animatedTileId = staticTileId;
            return false;
        }

        var count = Counts[group];
        var step = (int)(((elapsedFrames % count) + count + frame) % count);
        animatedTileId = Frames[(group * MaximumFrames) + step];
        return true;
    }
}
