using System.Diagnostics.CodeAnalysis;
using DaScreenshotStudio.Assets.Gui;
using DaScreenshotStudio.Engine.Rendering;
using DaScreenshotStudio.Engine.Text;

namespace DaScreenshotStudio.Engine.Ui;

public abstract class LayoutPane : Pane
{
    private readonly List<TextEditPane> _edits = [];
    private PixelRect _dragBounds;
    private bool _hasDragBounds;
    private int _dragAnchorX;
    private int _dragAnchorY;

    protected LayoutPane(UiAssets assets, PixelBlender blender, string layoutName)
        : this(assets, blender, layoutName, Place(assets, layoutName))
    {
    }

    protected LayoutPane(UiAssets assets, PixelBlender blender, string layoutName, in PixelRect bounds)
        : base(bounds, blender)
    {
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentNullException.ThrowIfNull(blender);

        Assets = assets;
        Blender = blender;
        Layout = assets.Layout(layoutName);
        TargetBlend = BlendMode.Transparent;

        if (Layout is { ControlCount: > 0 } layout && layout[0].Images.Count > 0)
        {
            var image = layout[0].Images[0];
            Background = assets.LayoutFrame(image.EntryName, image.Frame);

            if (image.EntryName.EndsWith(".spf", StringComparison.OrdinalIgnoreCase))
            {
                var placed = assets.PlacedFrame(image.EntryName, image.Frame);
                BackgroundOrigin = new PixelRect(placed.Left, placed.Top, placed.Width, placed.Height);
            }
        }
    }

    protected UiAssets Assets { get; }

    protected PixelBlender Blender { get; }

    protected GuiLayout? Layout { get; }

    protected Pixmap Background { get; set; }

    protected PixelRect BackgroundOrigin { get; private set; }

    protected IReadOnlyList<TextEditPane> Edits => _edits;

    protected virtual bool DrawsFocusOutline => true;

    public virtual bool IsMovable => IsReferenceDialog;

    public bool IsDragging { get; private set; }

    protected void SetDragBounds(in PixelRect bounds)
    {
        _dragBounds = bounds;
        _hasDragBounds = true;
    }

    private void HandleDrag(in PointerEvent pointer)
    {
        switch (pointer.Type)
        {
            case UiEventType.LeftDown when !IsDragging && Contains(in pointer) && !OverAControl(in pointer):
                IsDragging = true;
                _dragAnchorX = pointer.X;
                _dragAnchorY = pointer.Y;
                break;

            case UiEventType.PointerMove when IsDragging && IsMovable:
                MoveBy(pointer.X - _dragAnchorX, pointer.Y - _dragAnchorY);
                break;

            case UiEventType.LeftUp when IsDragging:
                IsDragging = false;
                break;

            default:
                break;
        }
    }

    protected override void OnHidden() => IsDragging = false;

    private void MoveBy(int deltaX, int deltaY)
    {
        var x = Bounds.X + deltaX;
        var y = Bounds.Y + deltaY;

        if (_hasDragBounds)
        {
            if (x + Bounds.Width > _dragBounds.Right)
            {
                x = _dragBounds.Right - Bounds.Width;
            }

            if (x < _dragBounds.X)
            {
                x = _dragBounds.X;
            }

            if (y + Bounds.Height > _dragBounds.Bottom)
            {
                y = _dragBounds.Bottom - Bounds.Height;
            }

            if (y < _dragBounds.Y)
            {
                y = _dragBounds.Y;
            }
        }

        SetBounds(new PixelRect(x, y, Bounds.Width, Bounds.Height));
    }

    private bool OverAControl(in PointerEvent pointer)
    {
        for (var i = 0; i < Children.Count; i++)
        {
            var bounds = Children[i].Bounds;
            if (pointer.X >= bounds.X && pointer.X < bounds.Right
                && pointer.Y >= bounds.Y && pointer.Y < bounds.Bottom)
            {
                return true;
            }
        }

        return OverOwnControl(in pointer);
    }

    protected virtual bool OverOwnControl(in PointerEvent pointer) => false;

    public void Focus(TextEditPane? control) => FocusControl(control);

    protected virtual void SetFocusedControl(IFocusableControl? control) => FocusControl(control);

    public void FocusStep(bool backward)
    {
        var count = Children.Count;
        if (count == 0)
        {
            return;
        }

        var start = IndexOfFocused();
        var index = start;

        for (var examined = 0; examined <= count; examined++)
        {
            int next;
            if (index < 0)
            {
                next = backward ? count - 1 : 0;
            }
            else
            {
                if (start < 0 && (backward ? index == 0 : index + 1 >= count))
                {
                    return;
                }

                next = backward ? (index + count - 1) % count : (index + 1) % count;
            }

            if (next == start || Children[next] is IFocusableControl { IsFocusable: true, IsEnabled: true })
            {
                SetFocusedControl(Children[next] as IFocusableControl);
                return;
            }

            index = next;
        }
    }

    private int IndexOfFocused()
    {
        if (FocusedControl is not { } focused)
        {
            return -1;
        }

        for (var i = 0; i < Children.Count; i++)
        {
            if (ReferenceEquals(Children[i], focused))
            {
                return i;
            }
        }

        return -1;
    }

    protected TextEditPane? AddEdit(
        string controlName,
        int maxLength = TextEditPane.DefaultMaxLength,
        bool masked = false,
        bool verticalBar = false,
        bool horizontalBar = false)
    {
        if (Layout is null || !Layout.TryFind(controlName, out var control))
        {
            return null;
        }

        var rect = GuiLayoutGeometry.Rect(control);
        var frames = verticalBar || horizontalBar ? ScrollBarPane.LoadFrames(Assets) : null;
        var pane = new TextEditPane(in rect, Blender, Assets.Font, Assets.Palette, frames, verticalBar, horizontalBar)
        {
            Background = Background,
            BackgroundOrigin = BackgroundOrigin,
            ActionId = _edits.Count,
            MaxLength = maxLength,
            IsMasked = masked,
            FocusRequested = Focus,
            TabRequested = FocusStep,
        };

        if (control.Colors.Count > 0)
        {
            pane.PaletteIndex = (byte)control.Colors[0];
        }

        _edits.Add(pane);
        Add(pane);
        return pane;
    }

    protected TextEditPane AddEdit(in PixelRect rect, int maxLength, byte paletteIndex, byte backgroundIndex)
    {
        var pane = new TextEditPane(in rect, Blender, Assets.Font, Assets.Palette)
        {
            Background = Background,
            BackgroundOrigin = BackgroundOrigin,
            ActionId = _edits.Count,
            MaxLength = maxLength,
            PaletteIndex = paletteIndex,
            BackgroundIndex = backgroundIndex,
            FocusRequested = Focus,
            TabRequested = FocusStep,
        };

        _edits.Add(pane);
        Add(pane);
        return pane;
    }

    protected Pixmap ControlFrame(string controlName, int index = 0) =>
        GuiLayoutGeometry.ControlFrame(Assets, Layout, controlName, index);

    protected bool TryBuildScrolledControl<TContent>(
        string controlName,
        Func<PixelRect, TContent> create,
        [NotNullWhen(true)] out TContent? content,
        [NotNullWhen(true)] out ScrollBarPane? bar)
        where TContent : RollPane
    {
        ArgumentNullException.ThrowIfNull(create);

        content = null;
        bar = null;

        if (!GuiLayoutGeometry.TryRect(Layout, controlName, out var rect)
            || ScrollBarPane.LoadFrames(Assets) is not { } frames)
        {
            return false;
        }

        var thickness = ScrollBarPane.ThicknessOf(frames);
        var scrollBar = new ScrollBarPane(
            new PixelRect(rect.Right - thickness, rect.Y, thickness, rect.Height),
            Blender,
            frames,
            horizontal: false);

        var roll = create(new PixelRect(
            rect.X, rect.Y, Math.Max(1, rect.Width - thickness), rect.Height));
        roll.Background = Background;

        scrollBar.Scrolled = () => roll.Position = scrollBar.Position;
        roll.Scrolled = () => scrollBar.Position = roll.Position;
        roll.FocusRequested = FocusControl;
        scrollBar.FocusRequested = () => FocusControl(roll);

        Add(roll);
        Add(scrollBar);

        content = roll;
        bar = scrollBar;
        return true;
    }

    public const string PresetButtonArt = "butt001.epf";

    public static int PresetButtonFrame(int value) => value is >= 1 and <= 14 ? (value - 1) * 3 : -1;

    protected ImageButtonPane AddImageButton(
        in PixelRect rect,
        Pixmap normal,
        Pixmap pressed,
        Pixmap disabled,
        Action<ImageButtonPane> activated,
        bool clipsToRect)
    {
        var pane = new ImageButtonPane(in rect, Blender)
        {
            Activated = activated,
            FocusRequested = FocusControl,

            PointerActionCompleted = _ => RefreshDefaultActionState(),
            DrawsFocusOutline = DrawsFocusOutline,
            Palette = Assets.Palette,
            States = ImageButtonStates.DialogPress,
        };

        pane.SetFrames(normal, pressed, disabled);

        if (clipsToRect)
        {
            pane.SetHitBounds(in rect);
        }

        Add(pane);
        return pane;
    }

    protected ImageButtonPane AddPresetButton(
        in PixelRect rect, int frameBase, Action<ImageButtonPane> activated, bool clipsToRect) =>
        AddImageButton(
            in rect,
            Assets.PaletteFrame(PresetButtonArt, frameBase),
            Assets.PaletteFrame(PresetButtonArt, frameBase + 1),
            Assets.PaletteFrame(PresetButtonArt, frameBase + 2),
            activated,
            clipsToRect);

    protected ImageButtonPane? AddButton(string controlName, Action<ImageButtonPane> activated) =>
        AddButton(controlName, 0, activated);

    protected ImageButtonPane? AddButton(
        string controlName,
        int value,
        Action<ImageButtonPane> activated)
    {
        if (Layout is null || !Layout.TryFind(controlName, out var control))
        {
            return null;
        }

        var preset = value > 0
            ? PresetButtonFrame(value)
            : control.Images.Count > 0 || control.Values.Count == 0
                ? -1
                : PresetButtonFrame(control.Values[0]);

        var rect = GuiLayoutGeometry.Rect(control);

        if (preset < 0)
        {
            return AddImageButton(
                in rect,
                Assets.LayoutFrame(control, 0),
                Assets.LayoutFrame(control, 1),
                Assets.LayoutFrame(control, 2),
                activated,
                clipsToRect: false);
        }

        if (Assets.PaletteFrame(PresetButtonArt, 0) is { IsEmpty: false } shape)
        {
            rect = new PixelRect(rect.X, rect.Y, shape.Width, shape.Height);
        }

        return AddPresetButton(in rect, preset, activated, clipsToRect: false);
    }

    protected ImageButtonPane? AddButton(string artControlName, in PixelRect rect, Action<ImageButtonPane> activated)
    {
        if (Layout is null || !Layout.TryFind(artControlName, out var control))
        {
            return null;
        }

        return AddImageButton(
            in rect,
            Assets.LayoutFrame(control, 0),
            Assets.LayoutFrame(control, 1),
            Assets.LayoutFrame(control, 2),
            activated,
            clipsToRect: false);
    }

    protected GuiTextPane AddLabel(
        in PixelRect rect, TextAlignment alignment = TextAlignment.Left, byte paletteIndex = TextPalette.DefaultIndex)
    {
        var pane = new GuiTextPane(in rect, Blender, Assets.Font, Assets.Palette, 1)
        {
            Alignment = alignment,
            Background = Background,
            BackgroundOrigin = BackgroundOrigin,
            PaletteIndex = paletteIndex,
            Wraps = true,
        };

        Add(pane);
        return pane;
    }

    protected static void Unfocusable(IFocusableControl? control)
    {
        if (control is not null)
        {
            control.IsFocusable = false;
        }
    }

    protected GuiTextPane? AddLabel(
        string controlName,
        TextAlignment alignment = TextAlignment.Left,
        int maxLines = 1,
        byte paletteIndex = TextPalette.DefaultIndex)
    {
        if (!GuiLayoutGeometry.TryRect(Layout, controlName, out var rect))
        {
            return null;
        }

        var pane = new GuiTextPane(in rect, Blender, Assets.Font, Assets.Palette, maxLines)
        {
            Alignment = alignment,
            Background = Background,
            BackgroundOrigin = BackgroundOrigin,
            PaletteIndex = paletteIndex,

            Wraps = true,
        };

        Add(pane);
        return pane;
    }

    protected IFocusableControl? DefaultControl { get; set; }

    private void DispatchDefaultAction()
    {
        if (DefaultControl is { IsEnabled: true } control)
        {
            control.DispatchAction();
        }
    }

    protected virtual void OnRefreshDefaultActionState()
    {
    }

    protected void RefreshDefaultActionState() => OnRefreshDefaultActionState();

    protected virtual void OnCancel()
    {
    }

    protected override bool OnPointer(in PointerEvent pointer)
    {
        HandleDrag(in pointer);

        if (pointer.Type != UiEventType.RightDown || !Contains(in pointer))
        {
            return false;
        }

        OnCancel();
        return true;
    }

    protected virtual bool ConsumesKeyboardEvents => true;

    protected virtual bool IsReferenceDialog => ConsumesKeyboardEvents;

    protected override bool RoutesWheelToControls => IsReferenceDialog;

    protected override bool OnKey(in KeyEvent key)
    {
        if (key.Type == UiEventType.KeyDown)
        {
            if (key.Key == UiKeys.Tab)
            {
                FocusStep((key.Modifiers & InputModifiers.Shift) != 0);
                return true;
            }

            if (key.Key == UiKeys.Enter)
            {
                if (FocusedControl is { TakesEnter: true } focused)
                {
                    if (focused.IsEnabled)
                    {
                        focused.DispatchAction();
                    }
                }
                else
                {
                    DispatchDefaultAction();
                }

                return true;
            }

            if (key.Key == UiKeys.Space)
            {
                if (FocusedControl is not { Kind: ControlKind.TextEdit })
                {
                    DispatchDefaultAction();
                    return true;
                }
            }
            else if (key.Key == UiKeys.Escape)
            {
                OnCancel();
                return true;
            }
            else if (key.Key == UiKeys.F12 && IsReferenceDialog)
            {
                return true;
            }
        }

        return ConsumesKeyboardEvents;
    }

    protected override bool OnKeyDispatching(in KeyEvent key)
    {
        return FocusedControl is { IsEnabled: true } && ForwardsToFocusedControl(in key);
    }

    protected override void OnKeyDispatched(in KeyEvent key) => RefreshDefaultActionState();

    private bool ForwardsToFocusedControl(in KeyEvent key) => key.Type switch
    {
        UiEventType.KeyDown => key.Key switch
        {
            UiKeys.Tab or UiKeys.Escape => false,
            UiKeys.F12 => !IsReferenceDialog,
            UiKeys.Enter => FocusedControl is { TakesEnter: true },
            UiKeys.Space => FocusedControl is { Kind: ControlKind.TextEdit },
            _ => true,
        },

        UiEventType.KeyUp
            or UiEventType.ImeComposition
            or UiEventType.Character
            or UiEventType.TranslatedKey => true,

        _ => false,
    };

    protected override void OnDrawContent(Canvas canvas)
    {
        RefreshDefaultActionState();

        canvas.Clear();

        if (!Background.IsEmpty)
        {
            canvas.BlitPixmap(BackgroundOrigin.X, BackgroundOrigin.Y, Background, BlendMode.Transparent);
        }
    }

    protected static PixelRect Place(UiAssets assets, string layoutName)
    {
        ArgumentNullException.ThrowIfNull(assets);

        var layout = assets.Layout(layoutName);
        if (layout is not { ControlCount: > 0 })
        {
            return new PixelRect(0, 0, GuiBackPane.ScreenWidth, GuiBackPane.ScreenHeight);
        }

        var window = GuiLayoutGeometry.Rect(layout[0]);
        var width = Math.Clamp(window.Width, 1, GuiBackPane.ScreenWidth);
        var height = Math.Clamp(window.Height, 1, GuiBackPane.ScreenHeight);

        return new PixelRect(
            (GuiBackPane.ScreenWidth - width) / 2,
            (GuiBackPane.ScreenHeight - height) / 2,
            width,
            height);
    }
}
