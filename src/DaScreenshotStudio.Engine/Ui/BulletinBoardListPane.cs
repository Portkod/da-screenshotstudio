using DaScreenshotStudio.Engine.Rendering;
using DaScreenshotStudio.Engine.Text;

namespace DaScreenshotStudio.Engine.Ui;

public sealed class BulletinBoardListPane : RollPane
{
    public const int RowHeight = 0x12;

    public const int EntriesPerRow = 1;

    public const int MaxNameLength = 0x100;

    private readonly TextBlock _text;

    private short[] _id = new short[32];
    private byte[] _name = new byte[32 * MaxNameLength];
    private int[] _nameLength = new int[32];

    public BulletinBoardListPane(in PixelRect bounds, PixelBlender blender, UiAssets assets)
        : base(in bounds, blender, RowHeight, EntriesPerRow)
    {
        ArgumentNullException.ThrowIfNull(assets);

        _text = new TextBlock(assets.Font, assets.Palette, Math.Max(1, bounds.Width), 1)
        {
            Wraps = false,
            Alignment = TextAlignment.Left,
        };
    }

    public const byte SelectedPaletteIndex = 0x58;

    public const byte PaletteIndex = 0x14;

    public const int BaselineOffset = 6;

    public short IdAt(int index) => _id[CheckIndex(index)];

    public ReadOnlySpan<byte> NameAt(int index) =>
        _name.AsSpan(CheckIndex(index) * MaxNameLength, _nameLength[index]);

    public void Clear()
    {
        Count = 0;
        Selected = -1;
        Position = 0;
        Invalidate();
    }

    public void Append(ushort id, scoped ReadOnlySpan<byte> name)
    {
        if (Count == _id.Length)
        {
            Grow();
        }

        _id[Count] = unchecked((short)id);

        var kept = name.Length > MaxNameLength ? name[..MaxNameLength] : name;
        _nameLength[Count] = kept.Length;
        kept.CopyTo(_name.AsSpan(Count * MaxNameLength));

        Count++;
        Invalidate();
    }

    protected override void OnBoundsChanged()
    {
        _text.MaxWidth = Math.Max(1, Bounds.Width);
        base.OnBoundsChanged();
    }

    protected override void DrawRow(Canvas canvas, int index, int top)
    {
        if (_nameLength[index] == 0)
        {
            return;
        }

        _text.DefaultPaletteIndex = index == Selected ? SelectedPaletteIndex : PaletteIndex;
        _text.SetText(NameAt(index));
        _text.Draw(canvas, 0, top + (RowHeight / 2) + BaselineOffset - _text.LineHeight);
    }

    private void Grow()
    {
        var wanted = _id.Length * 2;
        Array.Resize(ref _id, wanted);
        Array.Resize(ref _nameLength, wanted);
        Array.Resize(ref _name, wanted * MaxNameLength);
    }
}
