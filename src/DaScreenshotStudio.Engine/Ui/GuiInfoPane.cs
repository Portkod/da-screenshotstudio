using DaScreenshotStudio.Assets.Metadata;
using DaScreenshotStudio.Assets.Tables;
using DaScreenshotStudio.Engine.Rendering;
using DaScreenshotStudio.Engine.Text;

namespace DaScreenshotStudio.Engine.Ui;

public sealed record GuiInfoEntry(byte[] Title, PixelRect Hotspot, byte[] Body, int ImageIndex)
{
    public const int NoImage = -1;

    public bool HasImage => ImageIndex >= 0;
}

public sealed class GuiInfoPane : Pane
{
    public const string LayoutName = "llgui.txt";

    public const int NoEntry = -1;

    public const int CaptionSlot = KeyboardInfoPane.CaptionSlot;

    public const byte Ink = 0xFF;

    public const int LineHeight = 0xC;

    public const int CellWidth = 6;

    public const int PictureGap = 3;

    public const string BackgroundControl = "Noname";

    public const string PictureControl = "GUI";

    public const string PanelControl = "DESC";

    public const string TitleControl = "DESC_TITLE";

    public const string BodyControl = "DESC_CONTENT";

    private readonly Pixmap _background;
    private readonly Pixmap _picture;
    private readonly PixelRect _pictureRect;
    private readonly PixelRect _panelRect;
    private readonly PixelRect _bodyRect;
    private readonly int _titleGap;
    private readonly TextBlock _caption;
    private readonly TextBlock _title;
    private readonly TextBlock _body;
    private readonly TextPalette _palette;
    private readonly MessageTable _messages;
    private readonly List<GuiInfoEntry> _entries = [];
    private readonly List<Pixmap> _images = [];

    private int _selected = NoEntry;

    public GuiInfoPane(UiAssets assets, PixelBlender blender, in PixelRect bounds)
        : base(in bounds, blender)
    {
        ArgumentNullException.ThrowIfNull(assets);

        _messages = assets.Messages;
        _palette = assets.Palette;

        var layout = assets.Layout(LayoutName);
        _background = GuiLayoutGeometry.ControlFrame(assets, layout, BackgroundControl);
        _picture = GuiLayoutGeometry.ControlFrame(assets, layout, PictureControl);

        GuiLayoutGeometry.TryRect(layout, PictureControl, out _pictureRect);
        GuiLayoutGeometry.TryRect(layout, PanelControl, out _panelRect);
        GuiLayoutGeometry.TryRect(layout, BodyControl, out _bodyRect);

        _titleGap = GuiLayoutGeometry.TryRect(layout, TitleControl, out var title)
            ? _bodyRect.Y - title.Bottom
            : 0;

        _caption = new TextBlock(assets.Font, assets.Palette, Math.Max(1, _panelRect.Width), 1)
        {
            DefaultPaletteIndex = Ink,
        };

        _title = new TextBlock(assets.Font, assets.Palette, Math.Max(1, _panelRect.Width), 1)
        {
            DefaultPaletteIndex = Ink,
        };

        _body = new TextBlock(assets.Font, assets.Palette, Math.Max(1, _bodyRect.Width), int.MaxValue / 64)
        {
            Wraps = true,
            DefaultPaletteIndex = BodyInk,
        };
    }

    public const byte BodyInk = 0x58;

    public IReadOnlyList<GuiInfoEntry> Entries => _entries;

    public IReadOnlyList<Pixmap> Images => _images;

    public bool Load(Metafile? source, UiAssets assets)
    {
        ArgumentNullException.ThrowIfNull(assets);

        _entries.Clear();
        _images.Clear();

        if (source is null || source.GroupCount == 0)
        {
            Invalidate();
            return false;
        }

        var group = 0;
        while (TryReadEntry(source, assets, ref group) is { } entry)
        {
            _entries.Add(entry);
        }

        if ((uint)_selected >= (uint)_entries.Count)
        {
            _selected = NoEntry;
        }

        Invalidate();
        return _entries.Count > 0;
    }

    public int EntryAt(int x, int y)
    {
        for (var i = 0; i < _entries.Count; i++)
        {
            var r = _entries[i].Hotspot;
            if (x >= r.X && x < r.Right && y >= r.Y && y < r.Bottom)
            {
                return i;
            }
        }

        return NoEntry;
    }

    public void SelectEntry(int index)
    {
        var value = (uint)index < (uint)_entries.Count ? index : NoEntry;
        if (value == _selected)
        {
            return;
        }

        _selected = value;
        _body.SetText(value == NoEntry ? default : _entries[value].Body);
        Invalidate();
    }

    protected override bool OnPointer(in PointerEvent pointer)
    {
        if (!Contains(in pointer))
        {
            return false;
        }

        if (pointer.Type != UiEventType.LeftDown)
        {
            return true;
        }

        SelectEntry(EntryAt(pointer.X, pointer.Y));
        return true;
    }

    protected override void OnDrawContent(Canvas canvas)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        canvas.Clear();

        if (!_background.IsEmpty)
        {
            canvas.BlitPixmap(0, 0, in _background, BlendMode.Transparent);
        }

        if (!_picture.IsEmpty)
        {
            canvas.BlitPixmap(_pictureRect.X, _pictureRect.Y, in _picture, BlendMode.Transparent);
        }

        if ((uint)_selected < (uint)_entries.Count)
        {
            DrawSelected(canvas, _entries[_selected]);
            return;
        }

        DrawCaption(canvas);
    }

    public PixelRect CaptionRect()
    {
        var width = _messages.Get(CaptionSlot).Length * CellWidth;
        var penX = ((_panelRect.Width - width) / 2) + _panelRect.X;
        var penY = ((_panelRect.Height - LineHeight) / 2) + _panelRect.Y;
        return new PixelRect(penX, penY - LineHeight, Math.Max(0, width), LineHeight);
    }

    public int BlockHeight(int lines, in Pixmap picture) =>
        _titleGap + (Math.Max(0, lines) * LineHeight) + LineHeight
        + (picture.IsEmpty ? 0 : picture.Height + PictureGap);

    private void DrawSelected(Canvas canvas, GuiInfoEntry entry)
    {
        var picture = entry.HasImage && entry.ImageIndex < _images.Count
            ? _images[entry.ImageIndex]
            : default;

        _body.SetText(entry.Body);
        var height = BlockHeight(_body.LineCount, in picture);
        var y = ((_panelRect.Height - height) / 2) + _panelRect.Y;

        _title.SetText(entry.Title);
        var titleWidth = entry.Title.Length * CellWidth;
        _title.Draw(canvas, ((_panelRect.Width - titleWidth) / 2) + _panelRect.X, y);

        y += _titleGap + LineHeight;

        if (!picture.IsEmpty)
        {
            var x = ((_panelRect.Width - picture.Width) / 2) + _panelRect.X;
            canvas.BlitPixmap(x, y, in picture, BlendMode.Transparent);
            y += picture.Height + PictureGap;
        }

        _body.Draw(canvas, _panelRect.X, y);

        canvas.State.PrimaryColor = _palette[Ink];
        canvas.DrawRectOutline(entry.Hotspot);
    }

    private void DrawCaption(Canvas canvas)
    {
        var text = _messages.Get(CaptionSlot);
        if (text.IsEmpty)
        {
            return;
        }

        var rect = CaptionRect();
        _caption.SetText(text);
        _caption.Draw(canvas, rect.X, rect.Y);
    }

    private GuiInfoEntry? TryReadEntry(Metafile source, UiAssets assets, ref int group)
    {
        byte[]? title = null;
        var body = new List<byte>();
        var hotspot = PixelRect.Empty;
        var image = GuiInfoEntry.NoImage;
        var started = false;
        var seen = 0;

        for (; group < source.GroupCount; group++)
        {
            var name = source.GetGroupName(group);
            var underscore = name.IndexOf('_', StringComparison.Ordinal);
            if (underscore < 0)
            {
                continue;
            }

            var key = name[(underscore + 1)..];
            var values = source.GetValueCount(group);

            switch (key)
            {
                case "start":
                    started = true;
                    seen |= 1;
                    break;

                case "name" when started && values > 0:
                    title = source.GetValue(group, 0).ToArray();
                    seen |= 2;
                    break;

                case "xywh" when started && values >= 4:
                    var x = GuiLayoutGeometry.ParseInt(source.GetValue(group, 0)) + _pictureRect.X;
                    var y = GuiLayoutGeometry.ParseInt(source.GetValue(group, 1)) + _pictureRect.Y;
                    hotspot = new PixelRect(
                        x, y,
                        Math.Max(0, GuiLayoutGeometry.ParseInt(source.GetValue(group, 2))),
                        Math.Max(0, GuiLayoutGeometry.ParseInt(source.GetValue(group, 3))));
                    seen |= 4;
                    break;

                case "desc" when started && values > 0:
                    body.AddRange(source.GetValue(group, 0).ToArray());
                    body.Add((byte)'\n');
                    seen |= 8;
                    break;

                case "image" when started && values > 0:
                {
                    var file = Text(source.GetValue(group, 0));
                    if (file.EndsWith(".epf", StringComparison.OrdinalIgnoreCase))
                    {
                        var art = assets.LayoutFrame(file, 0);
                        if (!art.IsEmpty)
                        {
                            _images.Add(art);
                            image = _images.Count - 1;
                        }
                    }

                    break;
                }

                case "end" when (seen & 0xF) == 0xF:
                    group++;
                    return new GuiInfoEntry(title ?? [], hotspot, [.. body], image);
            }
        }

        return null;
    }

    private static string Text(scoped ReadOnlySpan<byte> value)
    {
        Span<char> chars = value.Length <= 128 ? stackalloc char[value.Length] : new char[value.Length];
        for (var i = 0; i < value.Length; i++)
        {
            chars[i] = (char)value[i];
        }

        return new string(chars);
    }
}
