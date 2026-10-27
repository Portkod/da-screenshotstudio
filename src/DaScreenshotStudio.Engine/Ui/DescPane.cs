using DaScreenshotStudio.Engine.Rendering;
using DaScreenshotStudio.Engine.Text;

namespace DaScreenshotStudio.Engine.Ui;

public sealed class DescPane : Pane
{
    public const int Width = 0x9C;

    public const int TextWidth = 0x90;

    public const int TextInset = 6;

    public const int Padding = 0xC;

    public const ushort FillColor = 1;

    public const int PointerOffsetX = 0xC;

    public const int PointerOffsetY = 0x11;

    public const int TileSize = 0x10;

    public const string FrameEntryName = "dlgframe.epf";

    public const int FrameCount = 8;

    public static ReadOnlySpan<byte> NamePrefix => "{=u"u8;

    public static ReadOnlySpan<byte> DurabilityPrefix => "\n{=e"u8;

    public static ReadOnlySpan<byte> DurabilitySeparator => " / "u8;

    public static ReadOnlySpan<byte> DescriptionPrefix => "\n{=s"u8;

    private readonly UiAssets _assets;
    private readonly TextBlock _text;
    private object? _owner;

    public DescPane(PixelBlender blender, UiAssets assets)
        : base(new PixelRect(0, 0, Width, Padding), blender)
    {
        ArgumentNullException.ThrowIfNull(assets);

        _assets = assets;

        TargetBlend = BlendMode.Transparent;

        _text = new TextBlock(assets.Font, assets.Palette, TextWidth, MaxLines)
        {
            Wraps = true,
            BreaksOnWordBoundary = true,
            DefaultPaletteIndex = DefaultInk,
        };

        Visible = false;
    }

    public const int MaxLines = 40;

    public const byte DefaultInk = 0x1F;

    public object? Owner => _owner;

    public ReadOnlySpan<byte> Label => _text.Value;

    public void Show(object owner, int pointerX, int pointerY, scoped ReadOnlySpan<byte> content) =>
        Place(owner, pointerX + PointerOffsetX, pointerY + PointerOffsetY, content, clamp: true);

    public void ShowAt(object owner, int anchorX, int anchorY, scoped ReadOnlySpan<byte> content) =>
        Place(owner, anchorX, anchorY, content, clamp: false);

    private void Place(object owner, int x, int y, scoped ReadOnlySpan<byte> content, bool clamp)
    {
        ArgumentNullException.ThrowIfNull(owner);

        if (content.IsEmpty)
        {
            Hide(owner);
            return;
        }

        var changed = _text.SetText(content) || !ReferenceEquals(_owner, owner) || !Visible;
        _owner = owner;

        var height = _text.Height + Padding;

        if (clamp && x + Width >= GuiBackPane.ScreenWidth)
        {
            x -= Width + PointerOffsetX;
        }

        if (clamp && y + height >= GuiBackPane.ScreenHeight)
        {
            y -= height + PointerOffsetY;
        }

        var box = new PixelRect(x, y, Width, height);
        if (Bounds != box)
        {
            SetBounds(in box);
            changed = true;
        }

        Visible = true;

        if (changed)
        {
            Invalidate();
        }
    }

    public void Hide(object owner)
    {
        if (!ReferenceEquals(_owner, owner))
        {
            return;
        }

        _owner = null;
        Visible = false;
    }

    public void HideForAnyOwner()
    {
        if (!Visible)
        {
            return;
        }

        _owner = null;
        Visible = false;
    }

    protected override bool OnKey(in KeyEvent key)
    {
        HideForAnyOwner();
        return false;
    }

    public static int Compose(
        scoped ReadOnlySpan<byte> name,
        uint currentDurability,
        uint maxDurability,
        Span<byte> buffer,
        scoped ReadOnlySpan<byte> description = default)
    {
        var text = new TextBuffer(buffer);
        text.Append(NamePrefix);
        text.Append(name);

        if (maxDurability > 0)
        {
            text.Append(DurabilityPrefix);
            text.Append(currentDurability);
            text.Append(DurabilitySeparator);
            text.Append(maxDurability);
        }

        if (!description.IsEmpty)
        {
            text.Append(DescriptionPrefix);
            text.Append(description);
        }

        return text.Length;
    }

    protected override bool OnPointer(in PointerEvent pointer)
    {
        if (pointer.Type is UiEventType.LeftDown or UiEventType.LeftDoubleClick
            or UiEventType.RightDown or UiEventType.RightDoubleClick)
        {
            _owner = null;
            Visible = false;
        }

        return false;
    }

    protected override void OnDrawContent(Canvas canvas)
    {
        canvas.FillRect(canvas.Bounds, FillColor, BlendMode.Copy);

        DrawFrame(canvas);
        _text.Draw(canvas, TextInset, TextInset);
    }

    private void DrawFrame(Canvas canvas)
    {
        var width = canvas.Width;
        var height = canvas.Height;

        for (var x = TileSize; x + TileSize < width - 1; x += TileSize)
        {
            Blit(canvas, 1, x, 0);
            Blit(canvas, 6, x, height - TileSize);
        }

        for (var y = TileSize; y + TileSize < height - 1; y += TileSize)
        {
            Blit(canvas, 3, 0, y);
            Blit(canvas, 4, width - TileSize, y);
        }

        Blit(canvas, 0, 0, 0);
        Blit(canvas, 2, width - TileSize, 0);
        Blit(canvas, 5, 0, height - TileSize);
        Blit(canvas, 7, width - TileSize, height - TileSize);
    }

    private void Blit(Canvas canvas, int frame, int x, int y)
    {
        var tile = _assets.PaletteFrame(FrameEntryName, frame);
        if (!tile.IsEmpty)
        {
            canvas.BlitPixmap(x, y, in tile, BlendMode.Transparent);
        }
    }
}
