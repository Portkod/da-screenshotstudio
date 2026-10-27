using DaScreenshotStudio.Assets.Fonts;
using DaScreenshotStudio.Engine.Rendering;
using DaScreenshotStudio.Engine.Text;
using DaScreenshotStudio.Engine.Core;

namespace DaScreenshotStudio.Engine.Ui;

public sealed class EmoticonSelectPane : LayoutPane
{
    protected override bool ConsumesKeyboardEvents => false;

    public override bool IsMovable => false;

    public const string LayoutName = "lemot.txt";

    public const string AnchorLayoutName = "lback.txt";

    public const string AnchorControlName = "EmoticonDialog";

    public const string FirstOptionControl = "Emot0";

    public const string SecondOptionControl = "Emot1";

    public const string DescriptionControl = "Description";

    public const int OptionCount = 8;

    public const int HoverImage = 1;

    public const int SelectedImage = 2;

    public const byte DescriptionPaletteIndex = 0x14;

    public const int FirstOptionDigit = '1';

    private readonly PixelRect[] _options = new PixelRect[OptionCount];
    private readonly Pixmap[] _hover = new Pixmap[OptionCount];
    private readonly Pixmap[] _picked = new Pixmap[OptionCount];
    private readonly PixelRect _description;
    private readonly TextBlock? _label;
    private int _hovered = -1;
    private int _selected;

    public EmoticonSelectPane(UiAssets assets, PixelBlender blender)
        : base(assets, blender, LayoutName, Anchor(assets, LayoutName))
    {
        Visible = false;

        TargetBlend = BlendMode.Copy;

        GuiLayoutGeometry.TryRect(Layout, DescriptionControl, out _description);
        if (!_description.IsEmpty)
        {
            _label = new TextBlock(Assets.Font, Assets.Palette, _description.Width, 1)
            {
                Alignment = TextAlignment.Center,
                DefaultPaletteIndex = DescriptionPaletteIndex,
            };
        }

        BuildOptions();
    }

    public bool IsOpen => Visible;

    public UserState Selected => (UserState)_selected;

    public Action? Closed { get; set; }

    public void Open()
    {
        SetHovered(-1);

        if (Visible)
        {
            return;
        }

        Visible = true;
        Invalidate();
    }

    public void Close()
    {
        if (!Visible)
        {
            return;
        }

        Visible = false;
        Closed?.Invoke();
    }

    public void SetSelected(UserState state)
    {
        var option = (byte)state;
        _selected = option < OptionCount ? option : 0;
        SetDescription(_selected);
        Invalidate();
    }

    protected override bool OnPointer(in PointerEvent pointer)
    {
        if (!Visible)
        {
            return false;
        }

        switch (pointer.Type)
        {
            case UiEventType.PointerMove:
                SetHovered(HitTest(pointer.X, pointer.Y));
                return true;

            case UiEventType.LeftDown:
                var option = HitTest(pointer.X, pointer.Y);
                if (option >= 0)
                {
                    Commit(option);
                }

                return true;

            default:
                return false;
        }
    }

    protected override bool OnKey(in KeyEvent key)
    {
        if (!Visible || key.Type != UiEventType.KeyDown)
        {
            return false;
        }

        if (key.Key == UiKeys.Escape)
        {
            Close();
            return true;
        }

        if (key.Key is >= '0' and <= '9')
        {
            var option = key.Key - FirstOptionDigit;
            if ((uint)option < OptionCount)
            {
                Commit(option);
            }

            return true;
        }

        switch (key.Key)
        {
            case 'R':
                Close();
                return true;
            case 'Q' or 'W' or 'E' or UiKeys.F7:
                Close();
                return false;
            default:
                return false;
        }
    }

    protected override void OnDrawContent(Canvas canvas)
    {
        base.OnDrawContent(canvas);

        for (var i = 0; i < OptionCount; i++)
        {
            var frame = _hovered == i ? _hover[i] : _selected == i ? _picked[i] : default;
            if (!frame.IsEmpty)
            {
                canvas.BlitPixmap(_options[i].X, _options[i].Y, in frame, BlendMode.Transparent);
            }
        }

        if (_label is not null && !_description.IsEmpty)
        {
            _label.DrawIn(canvas, in _description);
        }
    }

    private void Commit(int option)
    {
        SetDescription(option);
        Close();
    }

    private void SetHovered(int option)
    {
        if (_hovered == option)
        {
            return;
        }

        _hovered = option;
        SetDescription(option);
        Invalidate();
    }

    private void SetDescription(int option)
    {
        if (_label is null || (uint)option >= OptionCount)
        {
            return;
        }

        _label.SetText(UserStateLabels.Name(Assets, (UserState)option));
    }

    private int HitTest(int x, int y)
    {
        for (var i = 0; i < OptionCount; i++)
        {
            if (_options[i].Contains(x, y))
            {
                return i;
            }
        }

        return -1;
    }

    private void BuildOptions()
    {
        if (Layout is not { } layout
            || !layout.TryFind(FirstOptionControl, out var first)
            || !layout.TryFind(SecondOptionControl, out var second)
            || first.Images.Count <= SelectedImage
            || second.Images.Count == 0)
        {
            return;
        }

        var origin = GuiLayoutGeometry.Rect(first);
        var next = GuiLayoutGeometry.Rect(second);
        var stepX = next.X - origin.X;
        var stepY = next.Y - origin.Y;
        var stepFrame = second.Images[0].Frame - first.Images[0].Frame;
        var entry = first.Images[0].EntryName;

        for (var i = 0; i < OptionCount; i++)
        {
            _options[i] = new PixelRect(origin.X + (stepX * i), origin.Y + (stepY * i), origin.Width, origin.Height);
            _hover[i] = Assets.LayoutFrame(entry, first.Images[HoverImage].Frame + (stepFrame * i));
            _picked[i] = Assets.LayoutFrame(entry, first.Images[SelectedImage].Frame + (stepFrame * i));
        }
    }

    private static PixelRect Anchor(UiAssets assets, string layoutName)
    {
        ArgumentNullException.ThrowIfNull(assets);

        return GuiLayoutGeometry.TryRect(assets.Layout(AnchorLayoutName), AnchorControlName, out var anchor)
            ? anchor
            : Place(assets, layoutName);
    }
}

public static class UserStateLabels
{
    public const int FirstMessageSlot = 0x24;

    public const byte Separator = (byte)';';

    public static ReadOnlySpan<byte> Name(UiAssets assets, UserState state) => Part(assets, state, before: true);

    public static ReadOnlySpan<byte> Detail(UiAssets assets, UserState state) => Part(assets, state, before: false);

    private static ReadOnlySpan<byte> Part(UiAssets assets, UserState state, bool before)
    {
        ArgumentNullException.ThrowIfNull(assets);

        var row = assets.Messages.Get(FirstMessageSlot + (int)state);
        var at = IndexOfSeparator(row, assets.Font.CodePage);
        if (at < 0)
        {
            return row;
        }

        return before ? row[..at] : row[(at + 1)..];
    }

    private static int IndexOfSeparator(scoped ReadOnlySpan<byte> row, LftCodePage codePage)
    {
        for (var i = 0; i < row.Length; i++)
        {
            if (codePage.IsLeadByte(row[i]))
            {
                i++;
                continue;
            }

            if (row[i] == Separator)
            {
                return i;
            }
        }

        return -1;
    }
}
