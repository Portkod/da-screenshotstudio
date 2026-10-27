namespace DaScreenshotStudio.Assets.Characters;

public enum HumanDirection : byte
{
    Up = 0,
    Right = 1,
    Down = 2,
    Left = 3,
}

public static class HumanPartOrder
{
    private static readonly byte[] UpOrLeft =
        [0, 10, 12, 14, 1, 20, 2, 3, 5, 18, 6, 17, 8, 7, 19, 4, 16, 15, 9, 11, 13];

    private static readonly byte[] RightOrDown =
        [17, 10, 12, 14, 1, 20, 2, 3, 4, 5, 18, 6, 8, 7, 19, 16, 15, 0, 9, 11, 13];

    private static readonly byte[] RightOrDownBody5 =
        [17, 10, 12, 14, 8, 1, 20, 2, 3, 4, 5, 18, 6, 7, 19, 16, 15, 0, 9, 11, 13];

    public const int HeadOnlyBodySprite = 5;

    public static int FrameGroup(HumanDirection direction) =>
        direction is HumanDirection.Right or HumanDirection.Down ? 1 : 0;

    public static bool IsMirrored(HumanDirection direction) =>
        direction is HumanDirection.Down or HumanDirection.Left;

    public static int ViewFrame(int motionFrame, int framesPerView, HumanDirection direction) =>
        motionFrame + (FrameGroup(direction) * framesPerView);

    public static ReadOnlySpan<byte> Select(HumanDirection direction, int bodySprite)
    {
        if (direction is HumanDirection.Up or HumanDirection.Left)
        {
            return UpOrLeft;
        }

        return bodySprite == HeadOnlyBodySprite ? RightOrDownBody5 : RightOrDown;
    }
}
