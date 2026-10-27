using DaScreenshotStudio.Assets.Metadata;
using DaScreenshotStudio.Engine.Rendering;

namespace DaScreenshotStudio.Engine.Ui;

public sealed record MonsterInfoEntry(byte[] Name, ushort Sprite, byte LowLevel, byte HighLevel, byte[] Description)
{
    public bool IsRange => LowLevel != HighLevel;
}

public sealed class MonsterInfoPane : LayoutPane
{
    public const string LayoutName = "lldi.txt";

    public const int CategoryCount = 13;

    public const int FirstCategoryCode = 2;

    public const int LastCategoryCode = FirstCategoryCode + CategoryCount - 1;

    public const int PreviousCode = 0;

    public const int NextCode = 1;

    public const int Columns = 6;

    public const byte RowInk = 0x1F;

    public const int CreatureBand = 0x4000;

    public const int RowPadding = 6;

    public const int LineHeight = 0xC;

    public const int CellWidth = 6;

    public const int SeparatorLift = 5;

    private readonly List<MonsterInfoEntry>?[] _categories = new List<MonsterInfoEntry>?[CategoryCount];
    private readonly PixelRect _contentRect;
    private readonly PixelRect _nameColumn;
    private readonly PixelRect _pictureColumn;
    private readonly PixelRect _descriptionColumn;
    private readonly PixelRect _levelColumn;
    private readonly PixelRect[] _categoryRects = new PixelRect[CategoryCount];
    private readonly PixelRect _previousRect;
    private readonly PixelRect _nextRect;

    private int _selected = FirstCategoryCode;

    public MonsterInfoPane(UiAssets assets, PixelBlender blender)
        : base(assets, blender, LayoutName, LegendInfoPane.Placement(assets, LegendInfoPane.ContentControl))
    {
        ArgumentNullException.ThrowIfNull(assets);

        GuiLayoutGeometry.TryRect(Layout, "CONTENT", out _contentRect);
        GuiLayoutGeometry.TryRect(Layout, "CNAME", out _nameColumn);
        GuiLayoutGeometry.TryRect(Layout, "CPIC", out _pictureColumn);
        GuiLayoutGeometry.TryRect(Layout, "CDESC", out _descriptionColumn);
        GuiLayoutGeometry.TryRect(Layout, "CLEV", out _levelColumn);
        GuiLayoutGeometry.TryRect(Layout, "PREV", out _previousRect);
        GuiLayoutGeometry.TryRect(Layout, "NEXT", out _nextRect);

        BuildCategoryRects();
        Visible = false;
    }

    protected override bool OverOwnControl(in PointerEvent pointer)
    {
        if (_previousRect.Contains(pointer.X, pointer.Y)
            || _nextRect.Contains(pointer.X, pointer.Y)
            || _contentRect.Contains(pointer.X, pointer.Y))
        {
            return true;
        }

        for (var i = 0; i < _categoryRects.Length; i++)
        {
            if (_categoryRects[i].Contains(pointer.X, pointer.Y))
            {
                return true;
            }
        }

        return false;
    }

    public IReadOnlyList<MonsterInfoEntry>? Category(int index) =>
        (uint)index < CategoryCount ? _categories[index] : null;

    public int DescriptionCharactersPerLine =>
        _descriptionColumn.Width <= 0 ? 0 : _descriptionColumn.Width / CellWidth;

    public int RowHeight(int pictureHeight, int descriptionLength)
    {
        var perLine = DescriptionCharactersPerLine;
        var lines = perLine <= 0 ? 1 : (descriptionLength / perLine) + 1;
        return Math.Max(Math.Max(0, pictureHeight), lines * LineHeight) + RowPadding;
    }

    public void SelectPrevious()
    {
        var code = _selected - 1;
        SelectCode(code < FirstCategoryCode ? LastCategoryCode : code);
    }

    public void SelectNext()
    {
        var code = (sbyte)(_selected + 1);
        SelectCode(code > LastCategoryCode ? FirstCategoryCode : code);
    }

    public void SelectCode(int code)
    {
        if (code < FirstCategoryCode || code > LastCategoryCode || code == _selected)
        {
            return;
        }

        _selected = code;
        Invalidate();
    }

    public bool Load(int index, Metafile? source)
    {
        if ((uint)index >= CategoryCount)
        {
            return false;
        }

        if (_categories[index] is { Count: > 0 })
        {
            return true;
        }

        var rows = new List<MonsterInfoEntry>();
        if (source is not null)
        {
            for (var group = 0; group < source.GroupCount; group++)
            {
                if (source.GetValueCount(group) < 4)
                {
                    continue;
                }

                rows.Add(new MonsterInfoEntry(
                    System.Text.Encoding.Latin1.GetBytes(source.GetGroupName(group)),
                    (ushort)(GuiLayoutGeometry.ParseInt(source.GetValue(group, 0)) + CreatureBand),
                    (byte)GuiLayoutGeometry.ParseInt(source.GetValue(group, 1)),
                    (byte)GuiLayoutGeometry.ParseInt(source.GetValue(group, 2)),
                    source.GetValue(group, 3).ToArray()));
            }
        }

        _categories[index] = rows;
        Invalidate();
        return true;
    }

    public void Enter()
    {
        Visible = true;
        Invalidate();
    }

    public void Leave()
    {
        Visible = false;
        Invalidate();
    }

    protected override bool OnKey(in KeyEvent key)
    {
        switch (key.Key)
        {
            case 0x80:
                SelectPrevious();
                return true;

            case 0x81:
                SelectNext();
                return true;

            case 0x82:
            case 0x83:
                return true;

            default:
                return base.OnKey(in key);
        }
    }

    private void BuildCategoryRects()
    {
        if (!GuiLayoutGeometry.TryRect(Layout, "TOPLEFT", out var anchor)
            || !GuiLayoutGeometry.TryRect(Layout, "TOPRIGHT", out var right)
            || !GuiLayoutGeometry.TryRect(Layout, "BOTTOMLEFT", out var below))
        {
            return;
        }

        var columnStep = right.X - anchor.X;
        var rowStep = below.Y - anchor.Y;

        for (var i = 0; i < CategoryCount; i++)
        {
            var row = i / Columns;
            var column = i % Columns;
            _categoryRects[i] = new PixelRect(
                anchor.X + (column * columnStep),
                anchor.Y + (row * rowStep),
                anchor.Width,
                anchor.Height);
        }
    }
}
