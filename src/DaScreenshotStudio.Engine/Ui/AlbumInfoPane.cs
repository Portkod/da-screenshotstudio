using DaScreenshotStudio.Engine.Rendering;

namespace DaScreenshotStudio.Engine.Ui;

public sealed record AlbumEntry(Pixmap Picture)
{
    public const int PictureWidth = 0x30;

    public const int PictureHeight = 0x38;

    public bool HasPicture =>
        !Picture.IsEmpty && Picture.Width == PictureWidth && Picture.Height == PictureHeight;
}

public sealed class AlbumViewPane : LayoutPane
{
    protected override bool ConsumesKeyboardEvents => false;

    public const string LayoutName = "llalbum.txt";

    public const string ListControl = "List";

    public const string CellControl = "AlbumBase";

    public const string ColumnStepControl = "AlbumBaseX";

    public const string RowStepControl = "AlbumBaseY";

    public const string PortraitControl = "Portrait";

    public const string DeleteControl = "Del";

    public const string SaveControl = "Save";

    public const int CellsPerRow = 7;

    public const byte SelectionInk = 0x5C;

    public const int DeleteHoverFlag = 0x01000000;

    public const int SaveHoverFlag = 0x02000000;

    public const int NoHover = -1;

    private readonly PixelRect _cellRect;
    private readonly PixelRect _portraitRect;
    private readonly PixelRect _deleteRect;
    private readonly PixelRect _saveRect;
    private readonly int _columnStep;
    private readonly int _rowStep;
    private readonly Pixmap _plate;
    private readonly Pixmap _deleteArt;
    private readonly Pixmap _saveArt;

    private readonly List<AlbumEntry> _entries = [];
    private int _scrollRow;

    public AlbumViewPane(UiAssets assets, PixelBlender blender, PixelRect? bounds = null)
        : base(assets, blender, LayoutName, bounds ?? Placement(assets, ListControl))
    {
        ArgumentNullException.ThrowIfNull(assets);

        GuiLayoutGeometry.TryRect(Layout, ListControl, out var list);
        GuiLayoutGeometry.TryRect(Layout, CellControl, out var cell);
        GuiLayoutGeometry.TryRect(Layout, ColumnStepControl, out var columnStep);
        GuiLayoutGeometry.TryRect(Layout, RowStepControl, out var rowStep);

        _columnStep = columnStep.X - cell.X;
        _rowStep = rowStep.Y - cell.Y;

        _portraitRect = Rebased(PortraitControl, cell);
        _deleteRect = Rebased(DeleteControl, cell);
        _saveRect = Rebased(SaveControl, cell);
        _cellRect = new PixelRect(cell.X - list.X, cell.Y - list.Y, cell.Width, cell.Height);

        _plate = ControlFrame(CellControl, 0);
        _deleteArt = ControlFrame(DeleteControl, 0);
        _saveArt = ControlFrame(SaveControl, 0);
    }

    public IReadOnlyList<AlbumEntry> Entries => _entries;

    public int Selected { get; set; } = -1;

    public int Hover { get; set; } = NoHover;

    public int ScrollRow
    {
        get => _scrollRow;
        set
        {
            var clamped = Math.Max(0, value);
            if (_scrollRow == clamped)
            {
                return;
            }

            _scrollRow = clamped;
            Invalidate();
        }
    }

    public int ContentWidth => CellsPerRow * _columnStep;

    public static PixelRect Placement(UiAssets assets, string control) =>
        GuiLayoutGeometry.ControlRect(assets, LayoutName, control);

    public void SetEntries(IEnumerable<AlbumEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        _entries.Clear();
        _entries.AddRange(entries);
        Invalidate();
    }

    public PixelRect CellOf(int index) =>
        new(_cellRect.X + ((index % CellsPerRow) * _columnStep),
            _cellRect.Y + ((index / CellsPerRow) * _rowStep) - (_scrollRow * _rowStep),
            _cellRect.Width,
            _cellRect.Height);

    public PixelRect PortraitOf(int index)
    {
        var cell = CellOf(index);
        return new PixelRect(
            cell.X + _portraitRect.X, cell.Y + _portraitRect.Y, _portraitRect.Width, _portraitRect.Height);
    }

    public static int DeleteHover(int index) => index | DeleteHoverFlag;

    public static int SaveHover(int index) => index | SaveHoverFlag;

    protected override void OnDrawContent(Canvas canvas)
    {
        base.OnDrawContent(canvas);

        for (var i = 0; i < _entries.Count; i++)
        {
            var cell = CellOf(i);
            if (cell.Y >= canvas.Height || cell.Y + cell.Height <= 0)
            {
                continue;
            }

            if (!_plate.IsEmpty)
            {
                canvas.BlitPixmap(cell.X, cell.Y, in _plate, BlendMode.Transparent);
            }

            var entry = _entries[i];
            var portrait = PortraitOf(i);

            if (entry.HasPicture)
            {
                canvas.BlitPixmap(portrait.X, portrait.Y, entry.Picture, BlendMode.Transparent);

                if (Selected == i)
                {
                    canvas.FillRect(in portrait, Assets.Palette[SelectionInk], BlendMode.Copy);
                }
            }
            else if (Selected == i)
            {
                canvas.SetDrawColor(Assets.Palette[SelectionInk]);
                canvas.DrawRectOutline(in portrait);
            }

            DrawHoverButtons(canvas, i, in cell);
        }
    }

    private void DrawHoverButtons(Canvas canvas, int index, in PixelRect cell)
    {
        if (Hover == DeleteHover(index) && !_deleteArt.IsEmpty)
        {
            canvas.BlitPixmap(cell.X + _deleteRect.X, cell.Y + _deleteRect.Y, in _deleteArt, BlendMode.Transparent);
        }

        if (Hover == SaveHover(index) && !_saveArt.IsEmpty)
        {
            canvas.BlitPixmap(cell.X + _saveRect.X, cell.Y + _saveRect.Y, in _saveArt, BlendMode.Transparent);
        }
    }

    private PixelRect Rebased(string control, in PixelRect basis)
    {
        GuiLayoutGeometry.TryRect(Layout, control, out var rect);
        return new PixelRect(rect.X - basis.X, rect.Y - basis.Y, rect.Width, rect.Height);
    }
}

public sealed class AlbumInfoPane : LayoutPane
{
    protected override bool ConsumesKeyboardEvents => false;

    public const string LayoutName = "llalbum.txt";

    public const string BackgroundControl = "Noname";

    private readonly Pixmap _background;
    private readonly AlbumViewPane _view;

    public AlbumInfoPane(UiAssets assets, PixelBlender blender, PixelRect? bounds = null)
        : base(assets, blender, LayoutName, bounds ?? LegendInfoPane.Placement(assets, LegendInfoPane.ContentControl))
    {
        ArgumentNullException.ThrowIfNull(assets);

        _background = ControlFrame(BackgroundControl, 0);

        _view = new AlbumViewPane(assets, blender, AlbumViewPane.Placement(assets, AlbumViewPane.ListControl));
        Add(_view);
    }

    public AlbumViewPane View => _view;

    public void Show()
    {
        Visible = true;
        _view.Visible = true;
        Invalidate();
    }

    public void Hide()
    {
        _view.Visible = false;
        Visible = false;
        Invalidate();
    }

    protected override void OnDrawContent(Canvas canvas)
    {
        base.OnDrawContent(canvas);

        if (!_background.IsEmpty)
        {
            canvas.BlitPixmap(0, 0, in _background, BlendMode.Transparent);
        }
    }
}
