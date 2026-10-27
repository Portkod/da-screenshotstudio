using DaScreenshotStudio.Engine.Rendering;

namespace DaScreenshotStudio.Engine.Ui;

public enum DragRecordKind
{
    Item = 0,

    Spell = 3,

    Skill = 4,
}

public sealed class DraggedItemPane : Pane
{
    public const int CellSize = 0x20;

    private IconFrame _icon;

    public DraggedItemPane(PixelBlender blender)
        : base(new PixelRect(0, 0, CellSize, CellSize), blender)
    {
        TargetBlend = BlendMode.Average;
        Visible = false;
    }

    public Action<int, int, bool>? DropOffered { get; set; }

    public bool IsDragging { get; private set; }

    public int SourceSlot { get; private set; }

    public DragRecordKind Kind { get; private set; }

    public int GrabX { get; private set; }

    public int GrabY { get; private set; }

    public void Begin(
        DragRecordKind kind,
        int slot,
        in IconFrame icon,
        int grabX,
        int grabY,
        int pointerX,
        int pointerY)
    {
        Kind = kind;

        SourceSlot = slot;
        GrabX = grabX;
        GrabY = grabY;
        _icon = icon;
        IsDragging = true;
        Visible = true;
        Invalidate();
        MoveTo(pointerX, pointerY);
        CapturePointer();
    }

    public void MoveTo(int pointerX, int pointerY)
    {
        var bounds = new PixelRect(pointerX - GrabX, pointerY - GrabY, CellSize, CellSize);
        if (bounds != Bounds)
        {
            SetBounds(in bounds);
        }
    }

    public void End()
    {
        if (!IsDragging)
        {
            return;
        }

        IsDragging = false;
        SourceSlot = 0;
        Kind = DragRecordKind.Item;
        _icon = default;
        Visible = false;
        ReleasePointer();
    }

    protected override bool OnPointer(in PointerEvent pointer)
    {
        if (!IsDragging)
        {
            return false;
        }

        var screenX = pointer.X + Bounds.X;
        var screenY = pointer.Y + Bounds.Y;

        switch (pointer.Type)
        {
            case UiEventType.PointerMove:
                MoveTo(screenX, screenY);
                DropOffered?.Invoke(screenX, screenY, false);
                return true;

            case UiEventType.LeftUp:
                DropOffered?.Invoke(screenX, screenY, true);
                End();
                return true;

            default:
                return true;
        }
    }

    protected override void OnDrawContent(Canvas canvas)
    {
        canvas.Clear();

        if (_icon.IsEmpty)
        {
            return;
        }

        canvas.BlitPixmap(_icon.Left, _icon.Top, in _icon.Image, BlendMode.Transparent);
    }
}
