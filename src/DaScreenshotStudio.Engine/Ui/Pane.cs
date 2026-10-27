using DaScreenshotStudio.Engine.Rendering;

namespace DaScreenshotStudio.Engine.Ui;

public class Pane
{
    private readonly List<Pane> _children = [];

    private const int MaxActiveWalks = 16;

    private readonly int[] _walks = new int[MaxActiveWalks];
    private int _walkCount;
    private PixelRect _bounds;

    private Pane? _pointerCapture;

    private IFocusableControl? _focused;

    private PixelRect _damage;

    public Pane(in PixelRect bounds, PixelBlender blender)
    {
        ArgumentNullException.ThrowIfNull(blender);

        _bounds = bounds;
        Content = new Canvas(bounds.Width, bounds.Height, blender);
        IsDirty = true;
        HasDirtySubtree = true;

        _damage = bounds;
    }

    public PixelRect Bounds => _bounds;

    public Canvas Content { get; }

    public bool Visible
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            Invalidate();

            if (!value && Parent is not null)
            {
                ReleasePointer();
            }

            if (value)
            {
                OnShown();
            }
            else
            {
                OnHidden();
            }

            if (value && RaisesWhenShown)
            {
                Parent?.Raise(this);
            }
        }
    } = true;

    public bool RaisesWhenShown { get; set; }

    public BlendMode TargetBlend
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            InvalidateComposite();
        }
    } = BlendMode.Copy;

    public int TargetAlphaWeight
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            InvalidateComposite();
        }
    } = PixelBlender.MaxAlphaWeight;

    public bool IsDirty { get; private set; }

    public bool HasDirtySubtree { get; private set; }

    public IReadOnlyList<Pane> Children => _children;

    internal List<Pane> ChildList => _children;

    public Pane? Parent { get; private set; }

    protected virtual void OnTick(uint now)
    {
    }

    public void TickTree(uint now)
    {
        OnTick(now);

        for (var i = 0; i < _children.Count; i++)
        {
            _children[i].TickTree(now);
        }
    }

    public void Add(Pane child)
    {
        ArgumentNullException.ThrowIfNull(child);

        if (child.Parent is not null)
        {
            throw new InvalidOperationException("That pane already belongs to a screen hierarchy.");
        }

        child.Parent = this;
        _children.Add(child);

        Invalidate();
    }

    public void AddUnderTop(Pane child)
    {
        ArgumentNullException.ThrowIfNull(child);

        if (child.Parent is not null)
        {
            throw new InvalidOperationException("That pane already belongs to a screen hierarchy.");
        }

        child.Parent = this;
        var at = Math.Max(0, _children.Count - 1);
        _children.Insert(at, child);
        ShiftWalks(removedAt: -1, insertedAt: at);
        Invalidate();
    }

    public void Remove(Pane child)
    {
        ArgumentNullException.ThrowIfNull(child);

        var at = _children.IndexOf(child);
        if (at < 0)
        {
            return;
        }

        _children.RemoveAt(at);
        ShiftWalks(removedAt: at, insertedAt: -1);

        child.Parent = null;
        DropDetachedCapture();
        Invalidate();
    }

    public void DropDeadPointerCapture() => DropDetachedCapture();

    private void DropDetachedCapture()
    {
        var root = Root;
        if (root._pointerCapture is { } captured && !ReferenceEquals(captured.Root, root))
        {
            root._pointerCapture = null;
        }
    }

    public void Raise(Pane child)
    {
        ArgumentNullException.ThrowIfNull(child);

        var at = _children.IndexOf(child);
        if (at < 0 || at == _children.Count - 1)
        {
            return;
        }

        _children.RemoveAt(at);
        _children.Add(child);

        ShiftWalks(removedAt: at, insertedAt: -1);
        Invalidate();
    }

    private void ShiftWalks(int removedAt, int insertedAt)
    {
        for (var w = 0; w < _walkCount; w++)
        {
            var i = _walks[w];

            if (removedAt >= 0 && removedAt <= i)
            {
                i--;
            }

            if (insertedAt >= 0 && insertedAt <= i)
            {
                i++;
            }

            _walks[w] = i;
        }
    }

    public void SetChildOrder(IReadOnlyList<Pane> order)
    {
        ArgumentNullException.ThrowIfNull(order);

        if (order.Count > _children.Count)
        {
            throw new InvalidOperationException(
                $"That order names {order.Count} panes and this one has {_children.Count} children.");
        }

        for (var i = 0; i < order.Count; i++)
        {
            if (order[i] is not { } child || !ReferenceEquals(child.Parent, this) || IndexOf(order, child) != i)
            {
                throw new InvalidOperationException(
                    "That order names something that is not a child of this pane, or names one twice.");
            }
        }

        var others = _orderScratch ??= [];
        others.Clear();
        for (var i = 0; i < _children.Count; i++)
        {
            if (IndexOf(order, _children[i]) < 0)
            {
                others.Add(_children[i]);
            }
        }

        _children.Clear();
        for (var i = 0; i < order.Count; i++)
        {
            _children.Add(order[i]);
        }

        for (var i = 0; i < others.Count; i++)
        {
            _children.Add(others[i]);
        }

        others.Clear();

        for (var w = 0; w < _walkCount; w++)
        {
            _walks[w] = -1;
        }

        Invalidate();
    }

    private static int IndexOf(IReadOnlyList<Pane> order, Pane pane)
    {
        for (var i = 0; i < order.Count; i++)
        {
            if (ReferenceEquals(order[i], pane))
            {
                return i;
            }
        }

        return -1;
    }

    private List<Pane>? _orderScratch;

    protected Pane Root
    {
        get
        {
            var pane = this;
            while (pane.Parent is not null)
            {
                pane = pane.Parent;
            }

            return pane;
        }
    }

    public void Replace(Pane existing, Pane replacement)
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(replacement);

        var index = _children.IndexOf(existing);
        if (index < 0)
        {
            throw new InvalidOperationException("That pane is not a child of this one.");
        }

        if (replacement.Parent is not null)
        {
            throw new InvalidOperationException("That pane already belongs to a screen hierarchy.");
        }

        existing.Parent = null;
        replacement.Parent = this;
        _children[index] = replacement;
        DropDetachedCapture();
        Invalidate();
    }

    public void Invalidate()
    {
        IsDirty = true;
        MarkForPresentation(in _bounds);
    }

    public void InvalidateComposite() => MarkForPresentation(in _bounds);

    internal PixelRect Damage => _damage;

    internal void ClearDamage() => _damage = PixelRect.Empty;

    internal void ClearSubtreeDirty() => HasDirtySubtree = false;

    private void MarkForPresentation(in PixelRect rect)
    {
        var x = rect.X;
        var y = rect.Y;
        var pane = this;

        while (true)
        {
            pane.HasDirtySubtree = true;

            if (pane.Parent is not { } parent)
            {
                break;
            }

            pane = parent;
            x += pane._bounds.X;
            y += pane._bounds.Y;
        }

        if (rect.IsEmpty)
        {
            return;
        }

        var absolute = new PixelRect(x, y, rect.Width, rect.Height);
        pane._damage = pane._damage.IsEmpty
            ? absolute
            : PixelRect.FromEdges(
                Math.Min(pane._damage.X, absolute.X),
                Math.Min(pane._damage.Y, absolute.Y),
                Math.Max(pane._damage.Right, absolute.Right),
                Math.Max(pane._damage.Bottom, absolute.Bottom));
    }

    protected void SetOrigin(int x, int y)
    {
        if (_bounds.X == x && _bounds.Y == y)
        {
            return;
        }

        MarkForPresentation(in _bounds);
        _bounds = new PixelRect(x, y, _bounds.Width, _bounds.Height);
        MarkForPresentation(in _bounds);
    }

    public void SetBounds(in PixelRect bounds)
    {
        if (_bounds == bounds)
        {
            return;
        }

        if (_bounds.Width == bounds.Width && _bounds.Height == bounds.Height)
        {
            SetOrigin(bounds.X, bounds.Y);
            OnBoundsChanged();
            return;
        }

        MarkForPresentation(in _bounds);
        _bounds = bounds;
        Content.SetBounds(new PixelRect(0, 0, bounds.Width, bounds.Height));
        OnBoundsChanged();
        Invalidate();
    }

    internal void DrawToTarget(Canvas target, int absoluteX, int absoluteY, in PixelRect clip)
    {
        if (DrawsNothing)
        {
            return;
        }

        if (IsDirty)
        {
            OnDrawContent(Content);
            IsDirty = false;
        }

        if (ReferenceEquals(target, Content))
        {
            return;
        }

        var previousClip = target.Clip;
        var previousWeight = target.State.AlphaWeight;

        target.SetClip(in clip);
        target.State.AlphaWeight = TargetAlphaWeight;
        OnComposite(target, absoluteX, absoluteY);
        target.State.AlphaWeight = previousWeight;
        target.SetClip(in previousClip);
    }

    protected virtual void OnComposite(Canvas target, int absoluteX, int absoluteY) =>
        target.BlitCanvas(absoluteX, absoluteY, Content, Content.Bounds, TargetBlend);

    protected virtual bool DrawsNothing => false;

    protected virtual void OnDrawContent(Canvas canvas)
    {
    }

    protected virtual void OnHidden()
    {
    }

    protected virtual void OnShown()
    {
    }

    protected virtual void OnBoundsChanged()
    {
    }

    public bool DispatchPointer(in PointerEvent pointer) => Visible && DispatchPointer(in pointer, Root);

    public IFocusableControl? FocusedControl => _focused;

    public void FocusControl(IFocusableControl? control)
    {
        if (ReferenceEquals(_focused, control))
        {
            return;
        }

        if (_focused is { } previous)
        {
            previous.IsFocused = false;
        }

        _focused = control;

        if (control is not null)
        {
            control.IsFocused = true;
        }
    }

    protected void ResetFocus()
    {
        if (_focused is { } previous)
        {
            previous.IsFocused = false;
        }

        _focused = null;
    }

    private bool DispatchPointer(in PointerEvent pointer, Pane root)
    {
        if (!Visible)
        {
            return false;
        }

        var local = pointer.Translate(-_bounds.X, -_bounds.Y);

        if (RoutesWheelToControls && local.Type == UiEventType.Wheel)
        {
            return DispatchWheelToControls(in local, root);
        }

        var walk = BeginWalk();
        try
        {
            while (NextChild(walk) is { } child)
            {
                if (child.DispatchPointer(in local, root))
                {
                    return true;
                }
            }
        }
        finally
        {
            EndWalk(walk);
        }

        return DispatchToSelf(in local, root, runWheelArm: true);
    }

    private bool DispatchWheelToControls(in PointerEvent local, Pane root)
    {
        var named = false;
        for (var i = 0; i < _children.Count; i++)
        {
            var child = _children[i];
            if (!child.Visible || !child.Bounds.Contains(local.X, local.Y))
            {
                continue;
            }

            child.DispatchPointer(in local, root);
            named = true;
            break;
        }

        for (var i = 0; i < WheelScrollBarCount; i++)
        {
            if (WheelControlArea(i).Contains(local.X, local.Y))
            {
                ScrollOneBarByNotches(i, local.Wheel);
            }
        }

        var self = DispatchToSelf(in local, root, runWheelArm: false);
        return named || self;
    }

    protected virtual bool RoutesWheelToControls => false;

    protected virtual PixelRect WheelControlArea(int index) => default;

    public Pane? PointerCapture => Root._pointerCapture;

    public void CapturePointer() => Root._pointerCapture = this;

    public void ReleasePointer()
    {
        var root = Root;
        if (ReferenceEquals(root._pointerCapture, this))
        {
            root._pointerCapture = null;
        }
    }

    public bool DispatchCaptured(in PointerEvent pointer)
    {
        if (!Visible)
        {
            return false;
        }

        var local = pointer;
        var root = this;
        for (var pane = this; pane is not null; pane = pane.Parent)
        {
            local = local.Translate(-pane._bounds.X, -pane._bounds.Y);
            root = pane;
        }

        if (RoutesWheelToControls && local.Type == UiEventType.Wheel)
        {
            return DispatchWheelToControls(in local, root);
        }

        return DispatchToSelf(in local, root, runWheelArm: true);
    }

    public bool DispatchCapturedKey(in KeyEvent key)
    {
        if (!Visible)
        {
            return false;
        }

        var armed = OnKeyDispatching(in key);
        var handled = OnKey(in key);

        if (armed)
        {
            OnKeyDispatched(in key);
        }

        return handled;
    }

    private bool DispatchToSelf(in PointerEvent local, Pane root, bool runWheelArm)
    {
        if (runWheelArm && local.Type == UiEventType.Wheel && local.Wheel != 0)
        {
            ScrollByNotches(local.Wheel);
        }

        var handled = OnPointer(in local);
        var inside = Contains(in local);

        UpdateCapture(root, in local, inside);

        return handled || ConsumesPointerAnywhere || (ConsumesPointerInside && inside);
    }

    private void UpdateCapture(Pane root, in PointerEvent local, bool inside)
    {
        if (local.Type == UiEventType.LeftDown)
        {
            if (inside && Visible)
            {
                root._pointerCapture = this;
            }
        }
        else if (local.Type == UiEventType.LeftUp && ReferenceEquals(root._pointerCapture, this))
        {
            root._pointerCapture = null;
        }
    }

    protected bool Contains(in PointerEvent local) =>
        local.X >= 0 && local.Y >= 0 && local.X < _bounds.Width && local.Y < _bounds.Height;

    protected virtual int WheelScrollBarCount => 1;

    protected virtual ScrollBarPane? WheelScrollBar(int index) => null;

    protected virtual void ScrollByNotches(int notches)
    {
        for (var i = 0; i < WheelScrollBarCount; i++)
        {
            ScrollOneBarByNotches(i, notches);
        }
    }

    private void ScrollOneBarByNotches(int index, int notches)
    {
        if (notches == 0 || WheelScrollBar(index) is not { } bar)
        {
            return;
        }

        var before = bar.Position;
        bar.Position -= notches;

        if (bar.Position != before)
        {
            bar.Scrolled?.Invoke();
        }
    }

    public bool DispatchKey(in KeyEvent key)
    {
        if (!Visible)
        {
            return false;
        }

        var armed = OnKeyDispatching(in key);

        var consumed = false;
        var walk = BeginWalk();
        try
        {
            while (NextChild(walk) is { } child)
            {
                if (child.DispatchKey(in key))
                {
                    consumed = true;
                    break;
                }
            }
        }
        finally
        {
            EndWalk(walk);
        }

        if (!consumed)
        {
            consumed = OnKey(in key);
        }

        if (armed)
        {
            OnKeyDispatched(in key);
        }

        return consumed;
    }

    protected virtual bool OnKeyDispatching(in KeyEvent key) => false;

    protected virtual void OnKeyDispatched(in KeyEvent key)
    {
    }

    protected virtual bool OnPointer(in PointerEvent pointer) => false;

    protected virtual bool OnKey(in KeyEvent key) => false;

    private int BeginWalk()
    {
        if (_walkCount >= MaxActiveWalks)
        {
            return -1;
        }

        _walks[_walkCount] = _children.Count - 1;
        return _walkCount++;
    }

    private void EndWalk(int walk)
    {
        if (walk >= 0)
        {
            _walkCount = walk;
        }
    }

    private Pane? NextChild(int walk)
    {
        if (walk < 0)
        {
            return null;
        }

        var i = _walks[walk];
        if (i > _children.Count - 1)
        {
            i = _children.Count - 1;
        }

        if (i < 0)
        {
            return null;
        }

        _walks[walk] = i - 1;
        return _children[i];
    }

    public bool ConsumesPointerAnywhere { get; set; }

    public bool ConsumesPointerInside { get; set; }
}
