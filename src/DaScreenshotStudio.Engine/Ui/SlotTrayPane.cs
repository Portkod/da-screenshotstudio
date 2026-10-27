using DaScreenshotStudio.Engine.Rendering;
using DaScreenshotStudio.Engine.Text;
using DaScreenshotStudio.Engine.Core;

namespace DaScreenshotStudio.Engine.Ui;

public enum TrayMode
{
    Items = 0,
    SpellPage1 = 1,
    SpellPage2 = 2,
    SkillPage1 = 3,
    SkillPage2 = 4,
    Combined = 5,
}

public sealed class SlotTrayPane : GuiChildPane
{
    public const int FullColumns = 12;

    public const int HalfColumns = 6;

    public const int AbilityPageStride = 36;

    public const int GeneralPageStart = 72;

    public const int GeneralSkillSlots = 16;

    public const int GeneralSpellSlots = 17;

    public const int AbilityPageSlots = 35;

    public const int ReservedItemSlot = 60;

    public const int CellHeight = 0x21;

    public const int CellWidth = 0x24;

    public const int CellSize = 0x20;

    public const int CellInsetX = 4;

    public const int CellInsetY = 2;

    public const int AbilityInsetX = 5;

    public const int AbilityInsetY = 3;

    public const int DropOutlineColorIndex = 0x1F;

    public const int DropOutlineInflate = 2;

    public const int ActionDelayColorIndex = 0x58;

    public const int ActionDelayWipeColorIndex = 0x18;

    private readonly UiAssets _assets;
    private readonly PlayerModel _player;
    private readonly TextBlock _label;
    private Pixmap _panel;
    private int _revision = -1;
    private int _dropCell;

    public SlotTrayPane(in PixelRect bounds, PixelBlender blender, UiAssets assets, PlayerModel player)
        : base(in bounds, blender)
    {
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentNullException.ThrowIfNull(player);

        _assets = assets;
        _player = player;
        _label = new TextBlock(assets.Font, assets.Palette, Math.Max(1, bounds.Width), 1) { Wraps = false };
    }

    public Pixmap Panel
    {
        get => _panel;
        set
        {
            _panel = value;
            Invalidate();
        }
    }

    public TrayMode Mode
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            _revision = -1;
            _dropCell = 0;

            FirstSlot = value switch
            {
                TrayMode.SpellPage2 or TrayMode.SkillPage2 => AbilityPageStride + 1,
                TrayMode.Combined => GeneralPageStart + 1,
                _ => 1,
            };

            Invalidate();
        }
    }

    public int Rows
    {
        get;
        set
        {
            value = Math.Max(1, value);
            if (field == value)
            {
                return;
            }

            field = value;
            Invalidate();
        }
    } = 1;

    public int PadX
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
        }
    } = 3;

    public int PadY
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
        }
    } = 3;

    public int Columns
    {
        get;
        set
        {
            value = Math.Max(1, value);
            if (field == value)
            {
                return;
            }

            field = value;
            Invalidate();
        }
    } = FullColumns;

    public int InsetX
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
        }
    } = CellInsetX;

    public int InsetY
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
        }
    } = CellInsetY;

    public bool KeepsGeneralGrid { get; set; }

    public PixelRect GridClip
    {
        get;
        set
        {
            if (field.Equals(value))
            {
                return;
            }

            field = value;
            Invalidate();
        }
    }

    public int SlotCount
    {
        get;
        set
        {
            value = Math.Max(0, value);
            if (field == value)
            {
                return;
            }

            field = value;
            Invalidate();
        }
    }

    public bool ReservesLastCell
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
        }
    }

    public PixelRect CellRect(int row, int column) => new(
        (column * CellWidth) + PadX + InsetX,
        (row * CellHeight) + PadY + InsetY,
        CellSize,
        CellSize);

    public int SlotAt(int x, int y)
    {
        var localX = x - PadX - InsetX;
        var localY = y - PadY - InsetY;

        if (localX < 0 || localY < 0)
        {
            return 0;
        }

        var column = localX / CellWidth;
        var row = localY / CellHeight;

        if (column >= Columns || row >= Rows
            || localX - (column * CellWidth) >= CellSize
            || localY - (row * CellHeight) >= CellSize)
        {
            return 0;
        }

        return SlotInCell(row, column);
    }

    private int SlotInCell(int row, int column)
    {
        if (ReservesLastCell && row == Rows - 1 && column == Columns - 1)
        {
            return ReservedItemSlot;
        }

        var index = (row * Columns) + column;
        var count = SlotCount;
        return count > 0 && index >= count ? 0 : FirstSlot + index;
    }

    public DragRecordKind BookKind => Mode switch
    {
        TrayMode.SpellPage1 or TrayMode.SpellPage2 => DragRecordKind.Spell,
        TrayMode.SkillPage1 or TrayMode.SkillPage2 or TrayMode.Combined => DragRecordKind.Skill,
        _ => DragRecordKind.Item,
    };

    public int DropCell => _dropCell;

    public bool TryOfferDrop(DragRecordKind kind, int sourceSlot, int x, int y, bool release)
    {
        if (kind != BookKind || sourceSlot == 0)
        {
            return false;
        }

        var slot = SlotAt(x, y);
        var inside = (uint)x < (uint)Bounds.Width && (uint)y < (uint)Bounds.Height;

        if (!release)
        {
            SetDropCell(inside ? slot : 0);
            return inside;
        }

        SetDropCell(0);

        return inside && (kind != DragRecordKind.Item || slot != 0);
    }

    private void SetDropCell(int slot)
    {
        if (_dropCell == slot)
        {
            return;
        }

        _dropCell = slot;
        Invalidate();
    }

    public const int DragThreshold = 3;

    private int _pressSlot;
    private int _pressX;
    private int _pressY;

    public int FirstSlot
    {
        get;
        set
        {
            value = Math.Max(1, value);
            if (field == value)
            {
                return;
            }

            field = value;
            _dropCell = 0;
            Invalidate();
        }
    } = 1;

    public static SlotBook BookFor(PlayerModel player, TrayMode mode)
    {
        ArgumentNullException.ThrowIfNull(player);

        return mode switch
        {
            TrayMode.SpellPage1 or TrayMode.SpellPage2 => player.Spells,
            TrayMode.SkillPage1 or TrayMode.SkillPage2 or TrayMode.Combined => player.Skills,
            _ => player.Inventory,
        };
    }

    public SlotBook Book => BookFor(_player, Mode);

    private IconArt? Icons => Mode switch
    {
        TrayMode.SpellPage1 or TrayMode.SpellPage2 => _assets.SpellIcons,
        TrayMode.SkillPage1 or TrayMode.SkillPage2 or TrayMode.Combined => _assets.SkillIcons,
        _ => _assets.ItemIcons,
    };

    public void Refresh()
    {
        if (_revision == Book.Revision)
        {
            return;
        }

        _revision = Book.Revision;
        Invalidate();
    }

    protected override bool OnPointer(in PointerEvent pointer)
    {
        var slot = SlotAt(pointer.X, pointer.Y);

        switch (pointer.Type)
        {
            case UiEventType.PointerMove:
                return TryStartDrag(pointer.X, pointer.Y);

            case UiEventType.LeftDown when slot != 0 && Book[slot].IsOccupied:
                _pressSlot = slot;
                _pressX = pointer.X;
                _pressY = pointer.Y;
                return true;

            case UiEventType.LeftDoubleClick when slot != 0 && Book[slot].IsOccupied:
                _pressSlot = 0;
                return true;

            case UiEventType.LeftUp:
            {
                var armed = _pressSlot != 0;
                _pressSlot = 0;
                return armed;
            }

            case UiEventType.RightDown when slot != 0 && Book[slot].IsOccupied:
                return true;

            default:
                return false;
        }
    }

    private bool TryStartDrag(int x, int y)
    {
        if (_pressSlot == 0)
        {
            return false;
        }

        if (x >= _pressX - DragThreshold && x < _pressX + DragThreshold
            && y >= _pressY - DragThreshold && y < _pressY + DragThreshold)
        {
            return false;
        }

        var slot = _pressSlot;
        _pressSlot = 0;

        return Book[slot].IsOccupied && TryCellOf(slot, out _);
    }

    public bool TryCellOf(int slot, out PixelRect cell)
    {
        for (var row = 0; row < Rows; row++)
        {
            for (var column = 0; column < Columns; column++)
            {
                if (SlotInCell(row, column) == slot)
                {
                    cell = CellRect(row, column);
                    return true;
                }
            }
        }

        cell = default;
        return false;
    }

    protected override void OnBoundsChanged()
    {
        _label.MaxWidth = Math.Max(1, Bounds.Width);
        _revision = -1;
    }

    protected override void OnDrawContent(Canvas canvas)
    {
        PaintBackground(canvas);
        _revision = Book.Revision;

        var panel = _panel;
        if (!panel.IsEmpty)
        {
            canvas.BlitPixmap(0, 0, in panel, BlendMode.Transparent);
        }

        if (!GridClip.IsEmpty)
        {
            canvas.SetClip(GridClip);
        }

        DrawDropOutline(canvas);

        var icons = Icons;
        var book = Book;

        for (var row = 0; row < Rows; row++)
        {
            for (var column = 0; column < Columns; column++)
            {
                var slot = SlotInCell(row, column);
                if (slot == 0)
                {
                    continue;
                }

                var record = book[slot];
                if (!record.IsOccupied)
                {
                    continue;
                }

                var cell = CellRect(row, column);

                if (icons is null)
                {
                    _label.SetText(record.Name);
                    _label.Draw(canvas, cell.X + 1, cell.Y + ((cell.Height - _label.LineHeight) / 2));
                }
                else if (icons.Frame(record.Sprite, record.DyeColor) is { IsEmpty: false } icon)
                {
                    if (record.IsActionDelayed)
                    {
                        DrawDelayedIcon(canvas, record, in icon, in cell);
                    }
                    else
                    {
                        canvas.BlitPixmap(cell.X + icon.Left, cell.Y + icon.Top, in icon.Image, BlendMode.Transparent);
                    }
                }
            }
        }

        if (!GridClip.IsEmpty)
        {
            canvas.ResetClip();
        }
    }

    private void DrawDelayedIcon(Canvas canvas, SlotRecord record, in IconFrame icon, in PixelRect cell)
    {
        var x = cell.X + icon.Left;
        var y = cell.Y + icon.Top;

        if (!record.IsActionDelayWiping)
        {
            canvas.BlitPixmapAveragedWith(
                x, y, in icon.Image, BlendMode.Transparent, _assets.Palette[ActionDelayColorIndex]);
            return;
        }

        var boundary = cell.Y
            + (cell.Height * record.ActionDelayProgress / GameTiming.ActionDelayProgressSteps);

        var clip = canvas.Clip;

        canvas.SetClip(new PixelRect(cell.X, boundary, cell.Width, cell.Bottom - boundary).Intersect(clip));
        canvas.BlitPixmapAveragedWith(
            x, y, in icon.Image, BlendMode.Transparent, _assets.Palette[ActionDelayWipeColorIndex]);

        canvas.SetClip(new PixelRect(cell.X, cell.Y, cell.Width, boundary - cell.Y).Intersect(clip));
        canvas.BlitPixmapAveragedWith(
            x, y, in icon.Image, BlendMode.Transparent, _assets.Palette[ActionDelayColorIndex]);

        canvas.SetClip(in clip);
    }

    private void DrawDropOutline(Canvas canvas)
    {
        var slot = _dropCell;
        if (slot == 0 || !TryCellOf(slot, out var cell))
        {
            return;
        }

        canvas.SetDrawColor(_assets.Palette[DropOutlineColorIndex]);

        for (var inflate = DropOutlineInflate; inflate >= 0; inflate--)
        {
            canvas.DrawRectOutline(new PixelRect(
                cell.X - inflate,
                cell.Y - inflate,
                cell.Width + (inflate * 2),
                cell.Height + (inflate * 2)));
        }
    }
}
