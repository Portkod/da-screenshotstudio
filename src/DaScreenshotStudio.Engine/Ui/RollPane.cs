using DaScreenshotStudio.Engine.Rendering;

namespace DaScreenshotStudio.Engine.Ui;

public abstract class RollPane : GuiChildPane, IFocusableControl
{
    private readonly int _rowHeight;
    private readonly int _entriesPerRow;
    private readonly int _cellWidth;
    private bool _pressed;
    private int _hovered = -1;

    protected RollPane(
        in PixelRect bounds, PixelBlender blender, int rowHeight, int entriesPerRow, int cellWidth = 0)
        : base(in bounds, blender)
    {
        _rowHeight = Math.Max(1, rowHeight);
        _entriesPerRow = Math.Max(1, entriesPerRow);
        _cellWidth = cellWidth;
    }

    public const int ContentInset = 5;

    public int Count { get; protected set; }

    public int Rows => Count == 0 ? 0 : ((Count - 1) / _entriesPerRow) + 1;

    public int Maximum =>
        Math.Clamp((((Rows * _rowHeight) - Bounds.Height - 1) / _rowHeight) + 1, 0, ScrollBarPane.MaximumLimit);

    public int PageStep => ((Bounds.Height / _rowHeight) * _entriesPerRow) - 1;

    public int Position
    {
        get;
        set
        {
            value = Math.Clamp(value, 0, Maximum);
            if (field == value)
            {
                return;
            }

            field = value;
            Invalidate();
            Scrolled?.Invoke();
        }
    }

    public int Selected
    {
        get;
        set
        {
            value = value >= 0 && value < Count ? value : -1;
            if (field == value)
            {
                return;
            }

            field = value;
            SelectionChanged?.Invoke();
            Invalidate();
        }
    } = -1;

    public bool RanOffTheEnd { get; private set; }

    public Action? Scrolled { get; set; }

    public Action? SelectionChanged { get; set; }

    public Action? Activated { get; set; }

    public Action<IFocusableControl>? FocusRequested { get; set; }

    public bool IsFocusable { get; set; } = true;

    public bool IsFocused { get; set; }

    ControlKind IFocusableControl.Kind => ControlKind.Scrollable;

    bool IFocusableControl.TakesEnter => false;

    public void SelectPrevious(InputModifiers modifiers = InputModifiers.None)
    {
        var selected = Selected;
        var next = selected >= 0 && selected < Count
            ? Math.Max(selected - 1, 0)
            : Count - 1;

        EnsureVisible(next);
        SelectRow(next, modifiers);
    }

    public void SelectNext(InputModifiers modifiers = InputModifiers.None)
    {
        RanOffTheEnd = false;

        var selected = Selected;
        int next;
        if (selected >= 0 && selected < Count)
        {
            next = selected + 1;
            if (Count <= next)
            {
                RanOffTheEnd = true;
                next = Count - 1;
            }
        }
        else
        {
            next = Count - 1 >= 0 ? 0 : -1;
        }

        EnsureVisible(next);
        SelectRow(next, modifiers);
    }

    public void EnsureVisible(int index)
    {
        if ((uint)index >= (uint)Count)
        {
            return;
        }

        var row = index / _entriesPerRow;
        var visible = Math.Max(1, Bounds.Height / _rowHeight);
        if (row < Position)
        {
            Position = row;
        }
        else if (row >= Position + visible)
        {
            Position = row - visible + 1;
        }
    }

    public int RowAt(int x, int y)
    {
        var top = -Position * _rowHeight;
        var bottom = Math.Max(top + (Rows * _rowHeight), Bounds.Height);

        y = y < top ? top : Math.Min(y, bottom - 1);
        x = Math.Max(x, 0);

        var column = x / (_cellWidth > 0 ? _cellWidth : Math.Max(1, Bounds.Width));
        if (column >= _entriesPerRow)
        {
            return -1;
        }

        var index = (((y - top) / _rowHeight) * _entriesPerRow) + column;
        return index < Count ? index : -1;
    }

    public void Select(int index, InputModifiers modifiers = InputModifiers.None) =>
        SelectRow(index, modifiers);

    protected virtual void SelectRow(int index, InputModifiers modifiers) => Selected = index;

    protected virtual void HoverRow(int index)
    {
    }

    protected virtual void ActivateRow() => Activated?.Invoke();

    protected virtual void AfterConsumed(bool fromKeyboard)
    {
    }

    protected override bool OnPointer(in PointerEvent pointer)
    {
        var consumed = pointer.Type switch
        {
            UiEventType.LeftDown => OnPress(in pointer),
            UiEventType.PointerMove => OnMove(in pointer),
            UiEventType.LeftDoubleClick => OnDoublePress(in pointer),
            UiEventType.LeftUp => OnRelease(in pointer),
            _ => false,
        };

        if (consumed)
        {
            AfterConsumed(false);
        }

        return consumed;
    }

    protected override bool OnKey(in KeyEvent key)
    {
        if (!Visible || !IsFocused || key.Type != UiEventType.KeyDown)
        {
            return false;
        }

        RanOffTheEnd = false;

        switch (key.Key)
        {
            case UiKeys.Left or UiKeys.Up:
                SelectPrevious(key.Modifiers);
                break;

            case UiKeys.Right or UiKeys.Down:
                SelectNext(key.Modifiers);
                break;

            default:
                return false;
        }

        AfterConsumed(true);
        return true;
    }

    protected override void OnBoundsChanged() => Position = Math.Min(Position, Maximum);

    protected override void OnComposite(Canvas target, int absoluteX, int absoluteY) =>
        target.BlitCanvas(
            absoluteX,
            absoluteY,
            Content,
            new PixelRect(0, 0, Math.Max(0, Bounds.Width - ContentInset), Bounds.Height),
            TargetBlend);

    protected override void OnDrawContent(Canvas canvas)
    {
        PaintBackground(canvas);

        var origin = -Position * _rowHeight;

        for (var i = 0; i < Count; i++)
        {
            var top = origin + ((i / _entriesPerRow) * _rowHeight);
            if (top >= canvas.Height)
            {
                break;
            }

            if (top + _rowHeight <= 0)
            {
                continue;
            }

            DrawRow(canvas, i, top);
        }
    }

    protected abstract void DrawRow(Canvas canvas, int index, int top);

    protected int CheckIndex(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Count);
        return index;
    }

    private bool OnPress(in PointerEvent pointer)
    {
        if (_pressed || !Contains(in pointer))
        {
            return false;
        }

        if (IsFocusable)
        {
            FocusRequested?.Invoke(this);
        }

        var row = RowAt(pointer.X, pointer.Y);
        if (row < 0)
        {
            return false;
        }

        SelectRow(row, pointer.Modifiers);
        _pressed = true;
        return true;
    }

    private bool OnMove(in PointerEvent pointer)
    {
        if (_pressed)
        {
            var dragged = RowAt(pointer.X, pointer.Y);
            if (dragged >= 0)
            {
                SelectRow(dragged, pointer.Modifiers);
                EnsureVisible(dragged);
            }

            return true;
        }

        if (!Contains(in pointer))
        {
            return false;
        }

        var row = RowAt(pointer.X, pointer.Y);
        if (row != _hovered)
        {
            _hovered = row;
            HoverRow(row);
        }

        return true;
    }

    private bool OnDoublePress(in PointerEvent pointer)
    {
        if (!Contains(in pointer))
        {
            return false;
        }

        var row = RowAt(pointer.X, pointer.Y);
        if (row < 0)
        {
            return false;
        }

        SelectRow(row, pointer.Modifiers);
        ActivateRow();
        return true;
    }

    private bool OnRelease(in PointerEvent pointer)
    {
        if (!_pressed)
        {
            return false;
        }

        if (Contains(in pointer))
        {
            var row = RowAt(pointer.X, pointer.Y);
            if (row >= 0)
            {
                SelectRow(row, pointer.Modifiers);
                EnsureVisible(row);
            }
        }

        _pressed = false;
        return true;
    }

    protected override void ScrollByNotches(int notches) => Position -= notches;
}
