using DaScreenshotStudio.Engine.Rendering;
using DaScreenshotStudio.Engine.Ui;
using DaScreenshotStudio.Engine.Core;

namespace DaScreenshotStudio.Engine.World;

public sealed class WorldPane : Pane
{
    private readonly WorldRenderer _renderer = new();
    private EntityId _hovered;
    private bool _selectingTarget;
    private ObjectNamePane? _nameLabel;

    public WorldPane(in WorldViewLayout layout, PixelBlender blender, WorldScene scene)
        : base(layout.MapRectangle, blender)
    {
        ArgumentNullException.ThrowIfNull(scene);
        Scene = scene;
        _renderer.ViewCenterBiasY = layout.CenterBiasY;
    }

    public WorldScene Scene { get; }

    public WorldRenderer Renderer => _renderer;

    public void SetBlinded(bool blinded)
    {
        if (_renderer.IsBlinded == blinded)
        {
            return;
        }

        _renderer.IsBlinded = blinded;
        Invalidate();
    }

    public void SetHovered(EntityId entity)
    {
        _hovered = entity;
        _nameLabel?.SetNamed(entity);
        PlaceNameLabel();
        Invalidate();
    }

    public void AttachNameLabel(ObjectNamePane label)
    {
        ArgumentNullException.ThrowIfNull(label);

        _nameLabel = label;
        Add(label);
        label.SetNamed(_hovered);
    }

    public Action<CursorMode>? CursorModeChanged { get; set; }

    public bool IsSelectingTarget
    {
        get => _selectingTarget;

        set
        {
            if (_selectingTarget == value)
            {
                return;
            }

            _selectingTarget = value;

            if (!value)
            {
                HighlightDragTarget(default);
            }
        }
    }

    public bool TryOfferDrop(DragRecordKind kind, int paneX, int paneY, bool commit)
    {
        if (kind is not (DragRecordKind.Item or DragRecordKind.Spell))
        {
            HighlightDragTarget(default);
            return false;
        }

        var spell = kind == DragRecordKind.Spell;

        if (Scene.IsReady
            && _renderer.TryPickDropTarget(
                Scene, Bounds.Width, Bounds.Height, paneX, paneY, includeLocalPlayer: spell, out var living))
        {
            if (spell)
            {
                HighlightDragTarget(commit ? default : living);
                return true;
            }

            ClearDropCell();
            HighlightDragTarget(commit ? default : living);
            return true;
        }

        if (spell)
        {
            HighlightDragTarget(default);
            return false;
        }

        HighlightDragTarget(default);

        if (paneX < 0 || paneY < 0 || paneX >= Bounds.Width || paneY >= Bounds.Height)
        {
            ClearDropCell();
            return false;
        }

        if (Scene.Map is not { } map)
        {
            return true;
        }

        WorldRenderer.ViewOffset(
            Scene.Camera, Bounds.Width, Bounds.Height, _renderer.ViewCenterBiasY, out var offsetX, out var offsetY);

        IsometricProjection.CellAt(paneX - offsetX, paneY - offsetY, out var cellX, out var cellY);

        if ((uint)cellX >= (uint)map.Width || (uint)cellY >= (uint)map.Height)
        {
            ClearDropCell();
            return true;
        }

        if (!commit)
        {
            if (_renderer.SetDropCell(cellX, cellY))
            {
                Invalidate();
            }

            return true;
        }

        ClearDropCell();
        return true;
    }

    public void ClearDropCell()
    {
        if (_renderer.SetDropCell(-1, -1))
        {
            Invalidate();
        }
    }

    public void HighlightDragTarget(EntityId entity)
    {
        if (_renderer.SetDragTarget(entity))
        {
            Invalidate();
        }
    }

    public static WorldViewLayout DefaultViewLayout { get; } =
        new(PixelRect.FromEdges(3, 3, 619, 311), WorldViewLayout.CompactCenterBiasY);

    public void ApplyViewLayout(in WorldViewLayout layout)
    {
        _renderer.ViewCenterBiasY = layout.CenterBiasY;
        SetBounds(layout.PaneBounds);

        Minimap?.SetBounds(layout.MapRectangle);
    }

    public WorldMinimapPane? Minimap { get; set; }

    protected override bool OnPointer(in PointerEvent pointer)
    {
        if (!Scene.IsReady)
        {
            return false;
        }

        var shift = (pointer.Modifiers & InputModifiers.Shift) != 0;

        if (!shift)
        {
            UpdateHover(pointer.X, pointer.Y);
        }

        if (pointer.Type == UiEventType.LeftDoubleClick
            && _renderer.TryPickGroundItem(Scene, Bounds.Width, Bounds.Height, pointer.X, pointer.Y, out _))
        {
            return ObjectPassConsumed();
        }

        if (!shift)
        {
            if (IsSelectingTarget
                && pointer.Type == UiEventType.LeftDown
                && _renderer.TryPickObject(Scene, Bounds.Width, Bounds.Height, pointer.X, pointer.Y, out _))
            {
                IsSelectingTarget = false;
                return ObjectPassConsumed();
            }

            if (pointer.Type is UiEventType.LeftDown or UiEventType.LeftDoubleClick
                && _renderer.TryPickObject(Scene, Bounds.Width, Bounds.Height, pointer.X, pointer.Y, out var pressed)
                && Scene.Objects.TryGet(pressed, out _))
            {
                return ObjectPassConsumed();
            }

            if (pointer.Type == UiEventType.RightDoubleClick
                && _renderer.TryPickObject(Scene, Bounds.Width, Bounds.Height, pointer.X, pointer.Y, out _))
            {
                return ObjectPassConsumed();
            }

            if (pointer.Type == UiEventType.LeftDown
                && _renderer.TryPickStatic(Scene, Bounds.Width, Bounds.Height, pointer.X, pointer.Y, out _, out _))
            {
                return ObjectPassConsumed();
            }
        }

        if (!Contains(in pointer))
        {
            return false;
        }

        WorldRenderer.ViewOffset(
            Scene.Camera, Bounds.Width, Bounds.Height, _renderer.ViewCenterBiasY, out var offsetX, out var offsetY);

        IsometricProjection.CellAt(pointer.X - offsetX, pointer.Y - offsetY, out var cellX, out var cellY);

        var map = Scene.Map;
        var inside = map is not null
            && (uint)cellX < (uint)map.Width
            && (uint)cellY < (uint)map.Height;

        if (_renderer.SetHoverCell(inside ? cellX : -1, inside ? cellY : -1))
        {
            Invalidate();
        }

        return pointer.Type == UiEventType.RightDown;
    }

    private bool ObjectPassConsumed()
    {
        if (_renderer.SetHoverCell(-1, -1))
        {
            Invalidate();
        }

        return true;
    }

    private void UpdateHover(int paneX, int paneY)
    {
        var picked = _renderer.TryPickObject(Scene, Bounds.Width, Bounds.Height, paneX, paneY, out var hit);

        if (_selectingTarget)
        {
            HighlightDragTarget(picked ? hit : default);
        }

        if (picked)
        {
            if (_hovered != hit)
            {
                _hovered = hit;
                CursorModeChanged?.Invoke(CursorMode.Target);
                _nameLabel?.SetNamed(hit);
                PlaceNameLabel();
            }

            return;
        }

        if (_hovered != default && Scene.Objects.TryGet(_hovered, out _))
        {
            _hovered = default;
            CursorModeChanged?.Invoke(CursorMode.Normal);
            _nameLabel?.SetNamed(default);
        }
    }

    private void PlaceNameLabel() =>
        _nameLabel?.Place(Scene, Bounds.Width, Bounds.Height, _renderer.ViewCenterBiasY);

    protected override void OnDrawContent(Canvas canvas)
    {
        _renderer.Draw(canvas, Scene);
        PlaceNameLabel();
    }
}
