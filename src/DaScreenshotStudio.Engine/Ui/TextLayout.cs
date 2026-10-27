namespace DaScreenshotStudio.Engine.Ui;

internal static class TextLayout
{
    public static int Top(int nearEdge, int farEdge, int contentHeight) =>
        Top(nearEdge, farEdge, contentHeight, contentHeight - (farEdge - nearEdge));

    public static int Top(int nearEdge, int farEdge, int contentHeight, int scrollOffset)
    {
        if (contentHeight <= farEdge - nearEdge)
        {
            return nearEdge;
        }

        return nearEdge - scrollOffset + contentHeight < farEdge
            ? farEdge - contentHeight
            : nearEdge - scrollOffset;
    }
}
