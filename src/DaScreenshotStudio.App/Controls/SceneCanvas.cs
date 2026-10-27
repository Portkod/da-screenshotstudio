using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using DaScreenshotStudio.App.ViewModels;
using DaScreenshotStudio.Engine.Catalog;
using DaScreenshotStudio.Engine.Staging;

namespace DaScreenshotStudio.App.Controls;

/// <summary>What the panels drag onto the canvas.</summary>
public static class DragFormats
{
    public static readonly DataFormat<SavedCharacter> Character =
        DataFormat.CreateInProcessFormat<SavedCharacter>("dass-character");

    public static readonly DataFormat<CatalogItem> Item =
        DataFormat.CreateInProcessFormat<CatalogItem>("dass-item");

    public static readonly DataFormat<SpritePick> Sprite =
        DataFormat.CreateInProcessFormat<SpritePick>("dass-sprite");
}

/// <summary>
/// The rendered screen at a whole-number zoom, with the selected and hovered cells outlined. Objects
/// are picked by the client's own pointer rules and dragged from cell to cell.
/// </summary>
public sealed class SceneCanvas : Control
{
    private const int HalfWidth = 28;
    private const int HalfHeight = 14;

    public static readonly StyledProperty<MainViewModel?> EditorProperty =
        AvaloniaProperty.Register<SceneCanvas, MainViewModel?>(nameof(Editor));

    public static readonly StyledProperty<int> ZoomProperty =
        AvaloniaProperty.Register<SceneCanvas, int>(nameof(Zoom), 2);

    private static readonly IPen SelectionPen = new Pen(Brushes.Yellow, 1);
    private static readonly IPen HoverPen = new Pen(new SolidColorBrush(Color.FromArgb(160, 0, 255, 255)), 1);
    private static readonly IPen DropPen = new Pen(Brushes.Lime, 2);

    private (int X, int Y)? _hoverCell;
    private Point? _panFrom;
    private (int X, int Y) _panStart;
    private (int X, int Y)? _dropCell;
    private SceneObjectViewModel? _dragging;

    static SceneCanvas()
    {
        AffectsMeasure<SceneCanvas>(ZoomProperty);
        AffectsRender<SceneCanvas>(ZoomProperty);
        FocusableProperty.OverrideDefaultValue<SceneCanvas>(true);
    }

    public SceneCanvas()
    {
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DragLeaveEvent, (_, _) => SetDropCell(null));
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    public MainViewModel? Editor
    {
        get => GetValue(EditorProperty);
        set => SetValue(EditorProperty, value);
    }

    public int Zoom
    {
        get => GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == EditorProperty)
        {
            if (change.OldValue is MainViewModel old)
            {
                old.FrameUpdated -= OnFrameUpdated;
                old.PropertyChanged -= OnEditorPropertyChanged;
            }

            if (change.NewValue is MainViewModel editor)
            {
                editor.FrameUpdated += OnFrameUpdated;
                editor.PropertyChanged += OnEditorPropertyChanged;
            }

            InvalidateVisual();
        }
    }

    private void OnFrameUpdated(object? sender, EventArgs e) => InvalidateVisual();

    private void OnEditorPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.Selected))
        {
            InvalidateVisual();
        }
    }

    private int Scale => Math.Max(1, Zoom);

    protected override Size MeasureOverride(Size availableSize) =>
        new(SceneHost.ScreenWidth * Scale, SceneHost.ScreenHeight * Scale);

    public override void Render(DrawingContext context)
    {
        if (Editor is not { } editor)
        {
            return;
        }

        var size = new Rect(0, 0, SceneHost.ScreenWidth * Scale, SceneHost.ScreenHeight * Scale);
        using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.None }))
        {
            context.DrawImage(editor.Frame, new Rect(0, 0, SceneHost.ScreenWidth, SceneHost.ScreenHeight), size);
        }

        if (editor.ViewRectangle is not { } view)
        {
            return;
        }

        using (context.PushClip(new Rect(view.X * Scale, view.Y * Scale, view.Width * Scale, view.Height * Scale)))
        {
            if (_hoverCell is { } hover && _dragging is null)
            {
                DrawDiamond(context, editor, hover, HoverPen);
            }

            if (editor.Selected is { } selected)
            {
                DrawDiamond(context, editor, (selected.X, selected.Y), SelectionPen);
            }

            if (_dropCell is { } drop)
            {
                DrawDiamond(context, editor, drop, DropPen);
            }
        }
    }

    private void DrawDiamond(DrawingContext context, MainViewModel editor, (int X, int Y) cell, IPen pen)
    {
        if (editor.CellCenter(cell.X, cell.Y) is not { } center)
        {
            return;
        }

        var cx = (center.X + 0.5) * Scale;
        var cy = (center.Y + 0.5) * Scale;
        var w = HalfWidth * Scale;
        var h = HalfHeight * Scale;

        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            g.BeginFigure(new Point(cx - w, cy), false);
            g.LineTo(new Point(cx, cy - h));
            g.LineTo(new Point(cx + w, cy));
            g.LineTo(new Point(cx, cy + h));
            g.EndFigure(true);
        }

        context.DrawGeometry(null, pen, geometry);
    }

    private (int X, int Y) ScreenPixel(Point position) =>
        ((int)Math.Floor(position.X / Scale), (int)Math.Floor(position.Y / Scale));

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();

        if (Editor is not { } editor)
        {
            return;
        }

        var buttons = e.GetCurrentPoint(this).Properties;
        if (buttons.IsRightButtonPressed || buttons.IsMiddleButtonPressed)
        {
            _panFrom = e.GetPosition(this);
            _panStart = (editor.PanX, editor.PanY);
            e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }

        if (!buttons.IsLeftButtonPressed)
        {
            return;
        }

        var (x, y) = ScreenPixel(e.GetPosition(this));

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            editor.ToggleDoorAt(x, y);
            e.Handled = true;
            return;
        }

        var picked = editor.ObjectAt(x, y);
        editor.Selected = picked;

        // The camera follows the player, so a dragged player would pull the map along under the pointer.
        if (picked is { IsPlayer: false })
        {
            _dragging = picked;
            e.Pointer.Capture(this);
        }

        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (Editor is not { } editor)
        {
            return;
        }

        // Dragging the view moves the world with the pointer, so the camera goes the other way.
        if (_panFrom is { } from)
        {
            var at = e.GetPosition(this);
            editor.SetPan(
                _panStart.X - (int)Math.Round((at.X - from.X) / Scale),
                _panStart.Y - (int)Math.Round((at.Y - from.Y) / Scale));
            return;
        }

        var (x, y) = ScreenPixel(e.GetPosition(this));
        var cell = editor.CellAt(x, y);
        if (cell != _hoverCell)
        {
            _hoverCell = cell;
            editor.HoverText = cell is { } c ? $"pixel {x},{y}   cell {c.X},{c.Y}" : "";
            InvalidateVisual();
        }

        if (_dragging is { } dragging && cell is { } target)
        {
            editor.MoveTo(dragging, target.X, target.Y);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _dragging = null;
        _panFrom = null;
        e.Pointer.Capture(null);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hoverCell = null;
        InvalidateVisual();
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && Editor is { } editor)
        {
            editor.Zoom = Math.Clamp(editor.Zoom + Math.Sign(e.Delta.Y), 1, 4);
            e.Handled = true;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (Editor is not { } editor)
        {
            return;
        }

        // The arrows step the way the client's own arrow keys walk.
        switch (e.Key)
        {
            case Key.Up:
                editor.Nudge(0, -1);
                break;
            case Key.Down:
                editor.Nudge(0, 1);
                break;
            case Key.Left:
                editor.Nudge(-1, 0);
                break;
            case Key.Right:
                editor.Nudge(1, 0);
                break;
            case Key.R:
                editor.Rotate();
                break;
            case Key.Delete:
                editor.DeleteSelectedCommand.Execute(null);
                break;
            case Key.Home:
                editor.ResetViewCommand.Execute(null);
                break;
            case Key.Escape:
                editor.Selected = null;
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    private void SetDropCell((int X, int Y)? cell)
    {
        if (cell != _dropCell)
        {
            _dropCell = cell;
            InvalidateVisual();
        }
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        var places = e.DataTransfer.Contains(DragFormats.Character) || e.DataTransfer.Contains(DragFormats.Sprite);
        var accepts = places || e.DataTransfer.Contains(DragFormats.Item);
        e.DragEffects = accepts ? DragDropEffects.Copy : DragDropEffects.None;

        if (accepts && Editor is { } editor)
        {
            var (x, y) = ScreenPixel(e.GetPosition(this));
            SetDropCell(places ? editor.CellAt(x, y) : null);
        }

        e.Handled = true;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        SetDropCell(null);

        if (Editor is not { } editor)
        {
            return;
        }

        var (x, y) = ScreenPixel(e.GetPosition(this));
        var onto = editor.ObjectAt(x, y);

        if (e.DataTransfer.TryGetValue(DragFormats.Item) is { } item)
        {
            if (onto is { IsAisling: true })
            {
                editor.Wear(onto, item);
            }
            else if (editor.Selected is { IsAisling: true } selected)
            {
                editor.Wear(selected, item);
            }

            e.Handled = true;
            return;
        }

        // A sprite dropped on a figure of its own kind replaces that figure's sprite, unless Shift asks for
        // a new figure on top, which is how items are stacked on one tile.
        if (e.DataTransfer.TryGetValue(DragFormats.Sprite) is { } sprite)
        {
            if (onto is not null && onto.Kind == sprite.Kind && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                editor.ApplySprite(onto, sprite);
            }
            else if (editor.CellAt(x, y) is { } target)
            {
                editor.PlaceSprite(sprite, target.X, target.Y);
            }

            e.Handled = true;
            return;
        }

        if (e.DataTransfer.TryGetValue(DragFormats.Character) is { } character && editor.CellAt(x, y) is { } cell)
        {
            editor.PlaceCharacter(character, cell.X, cell.Y, e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? onto : null);
            e.Handled = true;
        }
    }
}
