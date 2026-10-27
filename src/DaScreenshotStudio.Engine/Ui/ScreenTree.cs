using DaScreenshotStudio.Engine.Rendering;

namespace DaScreenshotStudio.Engine.Ui;

public sealed class ScreenPane(int width, int height, PixelBlender blender)
    : Pane(new PixelRect(0, 0, width, height), blender)
{
    private readonly ushort[] _cursorSaveUnder = new ushort[CursorPane.CanvasSize * CursorPane.CanvasSize];
    private PixelRect _cursorSaved;

    public PixelSurface Surface => Content.Surface!;

    public CursorPane? Cursor { get; set; }

    public void ComposeCursor()
    {
        if (LogoVisible || Cursor is not { Visible: true } cursor)
        {
            return;
        }

        var surface = Surface;
        var covered = cursor.Bounds.Intersect(new PixelRect(0, 0, surface.Width, surface.Height));
        if (covered.IsEmpty)
        {
            return;
        }

        var pixels = surface.Pixels;
        for (var y = 0; y < covered.Height; y++)
        {
            pixels.Slice(((covered.Y + y) * surface.Pitch) + covered.X, covered.Width)
                .CopyTo(_cursorSaveUnder.AsSpan(y * covered.Width, covered.Width));
        }

        _cursorSaved = covered;
        cursor.DrawToTarget(Content, cursor.Bounds.X, cursor.Bounds.Y, Content.Bounds);
    }

    public void RestoreCursor()
    {
        var covered = _cursorSaved;
        if (covered.IsEmpty)
        {
            return;
        }

        _cursorSaved = default;

        var surface = Surface;
        var pixels = surface.Pixels;
        for (var y = 0; y < covered.Height; y++)
        {
            _cursorSaveUnder.AsSpan(y * covered.Width, covered.Width)
                .CopyTo(pixels.Slice(((covered.Y + y) * surface.Pitch) + covered.X, covered.Width));
        }
    }

    public bool LogoVisible { get; set; }
}

public sealed class ScreenTree(ScreenPane root)
{
    public ScreenPane Root { get; } = root ?? throw new ArgumentNullException(nameof(root));

    public bool ForceRedraw { get; set; }

    public long FrameCount { get; private set; }

    public PixelRect LastComposited { get; private set; }

    public bool RenderFrame()
    {
        if (Root.LogoVisible)
        {
            return false;
        }

        if (!ForceRedraw && !Root.HasDirtySubtree)
        {
            return false;
        }

        var clip = ForceRedraw ? Root.Content.Bounds : Root.Damage.Intersect(Root.Content.Bounds);
        ForceRedraw = false;
        Root.ClearDamage();

        if (clip.IsEmpty)
        {
            ClearSubtreeDirty(Root);
            return false;
        }

        LastComposited = clip;
        RenderSubtree(Root, Root.Content, 0, 0, in clip);
        FrameCount++;
        return true;
    }

    private static void RenderSubtree(Pane pane, Canvas target, int parentX, int parentY, in PixelRect clip)
    {
        pane.ClearSubtreeDirty();

        var children = pane.ChildList;
        var absoluteX = parentX + pane.Bounds.X;
        var absoluteY = parentY + pane.Bounds.Y;
        var paneClip = clip.Intersect(new PixelRect(absoluteX, absoluteY, pane.Bounds.Width, pane.Bounds.Height));

        if (!pane.Visible || paneClip.IsEmpty)
        {
            for (var i = 0; i < children.Count; i++)
            {
                ClearSubtreeDirty(children[i]);
            }

            return;
        }

        pane.DrawToTarget(target, absoluteX, absoluteY, in paneClip);

        for (var i = 0; i < children.Count; i++)
        {
            RenderSubtree(children[i], target, absoluteX, absoluteY, in paneClip);
        }
    }

    private static void ClearSubtreeDirty(Pane pane)
    {
        if (!pane.HasDirtySubtree)
        {
            return;
        }

        pane.ClearSubtreeDirty();

        var children = pane.ChildList;
        for (var i = 0; i < children.Count; i++)
        {
            ClearSubtreeDirty(children[i]);
        }
    }
}
