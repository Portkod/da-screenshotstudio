using DaScreenshotStudio.Assets.Fonts;
using DaScreenshotStudio.Engine.Rendering;
using DaScreenshotStudio.Engine.Text;

namespace DaScreenshotStudio.Engine.Ui;

public sealed class BulletinListPane : RollPane
{
    public const int RowHeight = 0x12;

    public const int EntriesPerRow = 1;

    public const int MaxAuthorLength = 0x100;

    public const int MaxTitleLength = 0x100;

    public const int RowLimit = 0x1000;

    public const byte SelectedPaletteIndex = 0x58;

    public const byte FlaggedPaletteIndex = 0x45;

    public const byte PaletteIndex = 0x14;

    public const int BaselineOffset = 6;

    public const int IdColumn = 0;

    public const int AuthorColumn = 0x3C;

    public const int DateColumn = 0x96;

    public const int TitleColumn = 0xC8;

    public const int EllipsisGuardSlack = 6;

    public const int AuthorBudget = 0x50;

    public const int TitleBudget = 0x6E;

    public const int EllipsisWidth = 0x12;

    private readonly TextBlock _text;
    private readonly bool _replacesDuplicates;

    private short[] _id = new short[64];
    private byte[] _flags = new byte[64];
    private byte[] _month = new byte[64];
    private byte[] _day = new byte[64];
    private byte[] _author = new byte[64 * MaxAuthorLength];
    private int[] _authorLength = new int[64];
    private byte[] _title = new byte[64 * MaxTitleLength];
    private int[] _titleLength = new int[64];

    public BulletinListPane(in PixelRect bounds, PixelBlender blender, UiAssets assets, bool replacesDuplicates)
        : base(in bounds, blender, RowHeight, EntriesPerRow)
    {
        ArgumentNullException.ThrowIfNull(assets);

        _replacesDuplicates = replacesDuplicates;
        _text = new TextBlock(assets.Font, assets.Palette, Math.Max(1, bounds.Width), 1)
        {
            Wraps = false,
            Alignment = TextAlignment.Left,
        };
    }

    public int VisibleRows => Math.Max(1, Bounds.Height / RowHeight);

    public bool IsMailbox { get; init; }

    public short IdAt(int index) => _id[CheckIndex(index)];

    public ReadOnlySpan<byte> AuthorAt(int index) =>
        _author.AsSpan(CheckIndex(index) * MaxAuthorLength, _authorLength[index]);

    public ReadOnlySpan<byte> TitleAt(int index) =>
        _title.AsSpan(CheckIndex(index) * MaxTitleLength, _titleLength[index]);

    public int Find(short id)
    {
        for (var i = 0; i < Count; i++)
        {
            if (_id[i] == id)
            {
                return i;
            }
        }

        return -1;
    }

    public bool Upsert(
        byte flags,
        ushort id,
        scoped ReadOnlySpan<byte> author,
        byte month,
        byte day,
        scoped ReadOnlySpan<byte> title)
    {
        var signed = unchecked((short)id);
        var existing = Find(signed);

        if (existing >= 0)
        {
            if (!_replacesDuplicates)
            {
                return false;
            }

            Store(existing, flags, signed, author, month, day, title);
            Invalidate();
            return true;
        }

        if (Count == RowLimit)
        {
            return false;
        }

        if (Count == _id.Length)
        {
            Grow();
        }

        var index = InsertPosition(signed);
        for (var i = Count; i > index; i--)
        {
            Copy(i - 1, i);
        }

        Count++;
        Store(index, flags, signed, author, month, day, title);

        if (Selected >= index)
        {
            Selected++;
        }

        Invalidate();
        return true;
    }

    public void RemoveAt(int index)
    {
        if ((uint)index >= (uint)Count)
        {
            return;
        }

        for (var i = index; i < Count - 1; i++)
        {
            Copy(i + 1, i);
        }

        Count--;

        if (Selected > index)
        {
            Selected--;
        }
        else if (Selected == index)
        {
            Selected = -1;
        }

        Position = Math.Min(Position, Maximum);
        Invalidate();
    }

    public void Clear()
    {
        Count = 0;
        Selected = -1;
        Position = 0;
        Invalidate();
    }

    public int InsertPosition(short id)
    {
        for (var i = 0; i < Count; i++)
        {
            if (_id[i] < id)
            {
                return i;
            }
        }

        return Count;
    }

    protected override void OnBoundsChanged()
    {
        _text.MaxWidth = Math.Max(1, Bounds.Width);
        base.OnBoundsChanged();
    }

    protected override void DrawRow(Canvas canvas, int index, int top)
    {
        Span<byte> scratch = stackalloc byte[16];

        _text.DefaultPaletteIndex = PaletteFor(index);
        var pen = top + (RowHeight / 2) + BaselineOffset - _text.LineHeight;

        Draw(canvas, FormatId(_id[index], scratch), IdColumn, pen);
        Draw(canvas, AuthorAt(index), AuthorColumn, pen, AuthorBudget);
        Draw(canvas, FormatDate(_month[index], _day[index], scratch), DateColumn, pen);
        Draw(canvas, TitleAt(index), TitleColumn, pen, TitleBudget);
    }

    private byte PaletteFor(int row)
    {
        if (row == Selected || IsMultiSelected(row))
        {
            return SelectedPaletteIndex;
        }

        return _flags[row] != 0 ? FlaggedPaletteIndex : PaletteIndex;
    }

    private bool IsMultiSelected(int row) => MultiSelection?.Contains(row) ?? false;

    public IndexSelection? MultiSelection { get; set; }

    protected override void SelectRow(int index, InputModifiers modifiers)
    {
        var previous = Selected;
        Selected = index;
        ApplyMultiSelection(previous, modifiers);
    }

    private void ApplyMultiSelection(int previous, InputModifiers modifiers)
    {
        if (MultiSelection is not { } selection)
        {
            return;
        }

        if ((modifiers & (InputModifiers.Alt | InputModifiers.Control | InputModifiers.Shift)) == 0)
        {
            selection.Clear();
        }

        var removing = (modifiers & InputModifiers.Alt) != 0;

        if ((modifiers & InputModifiers.Shift) != 0 && previous >= 0 && Selected >= 0)
        {
            var step = previous < Selected ? 1 : -1;
            for (var walked = previous; walked != Selected; walked += step)
            {
                Apply(selection, walked, removing);
            }
        }

        if (Selected >= 0)
        {
            Apply(selection, Selected, removing);
        }

        static void Apply(IndexSelection selection, int row, bool removing)
        {
            if (removing)
            {
                selection.Remove(row);
            }
            else
            {
                selection.Add(row);
            }
        }
    }

    private void Draw(Canvas canvas, scoped ReadOnlySpan<byte> text, int x, int y, int budget = 0)
    {
        if (text.Length == 0)
        {
            return;
        }

        if (budget > 0 && budget < text.Length + EllipsisGuardSlack)
        {
            Span<byte> shortened = stackalloc byte[64];
            _text.SetText(Ellipsize(text, budget, shortened, _text.Font.CodePage));
        }
        else
        {
            _text.SetText(text);
        }

        _text.Draw(canvas, x, y);
    }

    public static ReadOnlySpan<byte> Ellipsize(
        scoped ReadOnlySpan<byte> text, int budget, Span<byte> destination, LftCodePage codePage)
    {
        var room = budget - EllipsisWidth;
        var limit = Math.Min(text.Length, destination.Length - 3);

        var width = 0;
        var kept = limit;

        for (var i = 0; i < limit;)
        {
            var step = codePage.IsLeadByte(text[i]) && i + 1 < limit ? 2 : 1;
            if (room < width + (step * GameFont.FixedCellWidth))
            {
                kept = i;
                break;
            }

            width += step * GameFont.FixedCellWidth;
            i += step;
        }

        text[..kept].CopyTo(destination);
        destination[kept] = (byte)'.';
        destination[kept + 1] = (byte)'.';
        destination[kept + 2] = (byte)'.';
        return destination[..(kept + 3)];
    }

    private static ReadOnlySpan<byte> FormatId(short id, Span<byte> destination)
    {
        var written = 0;
        var digits = 0;
        var value = id < 0 ? 0 : (int)id;

        Span<byte> reversed = stackalloc byte[8];
        do
        {
            reversed[digits++] = (byte)('0' + (value % 10));
            value /= 10;
        }
        while (value > 0);

        for (var i = digits; i < 5; i++)
        {
            destination[written++] = (byte)' ';
        }

        for (var i = digits - 1; i >= 0; i--)
        {
            destination[written++] = reversed[i];
        }

        return destination[..written];
    }

    private static ReadOnlySpan<byte> FormatDate(byte month, byte day, Span<byte> destination)
    {
        var written = Pad2(month, destination, 0);
        destination[written++] = (byte)'/';
        written = Pad2(day, destination, written);
        return destination[..written];
    }

    private static int Pad2(byte value, Span<byte> destination, int offset)
    {
        if (value >= 10)
        {
            destination[offset++] = (byte)('0' + (value / 10));
        }
        else
        {
            destination[offset++] = (byte)' ';
        }

        destination[offset++] = (byte)('0' + (value % 10));
        return offset;
    }

    private void Store(
        int index,
        byte flags,
        short id,
        scoped ReadOnlySpan<byte> author,
        byte month,
        byte day,
        scoped ReadOnlySpan<byte> title)
    {
        _id[index] = id;
        _flags[index] = flags;
        _month[index] = month;
        _day[index] = day;

        var keptAuthor = author.Length > MaxAuthorLength ? author[..MaxAuthorLength] : author;
        _authorLength[index] = keptAuthor.Length;
        keptAuthor.CopyTo(_author.AsSpan(index * MaxAuthorLength));

        var keptTitle = title.Length > MaxTitleLength ? title[..MaxTitleLength] : title;
        _titleLength[index] = keptTitle.Length;
        keptTitle.CopyTo(_title.AsSpan(index * MaxTitleLength));
    }

    private void Copy(int from, int to)
    {
        _id[to] = _id[from];
        _flags[to] = _flags[from];
        _month[to] = _month[from];
        _day[to] = _day[from];
        _authorLength[to] = _authorLength[from];
        _author.AsSpan(from * MaxAuthorLength, MaxAuthorLength).CopyTo(_author.AsSpan(to * MaxAuthorLength));
        _titleLength[to] = _titleLength[from];
        _title.AsSpan(from * MaxTitleLength, MaxTitleLength).CopyTo(_title.AsSpan(to * MaxTitleLength));
    }

    private void Grow()
    {
        var wanted = Math.Min(_id.Length * 2, RowLimit);
        Array.Resize(ref _id, wanted);
        Array.Resize(ref _flags, wanted);
        Array.Resize(ref _month, wanted);
        Array.Resize(ref _day, wanted);
        Array.Resize(ref _authorLength, wanted);
        Array.Resize(ref _author, wanted * MaxAuthorLength);
        Array.Resize(ref _titleLength, wanted);
        Array.Resize(ref _title, wanted * MaxTitleLength);
    }
}
