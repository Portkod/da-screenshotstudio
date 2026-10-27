using DaScreenshotStudio.Assets.Gui;
using DaScreenshotStudio.Engine.Rendering;
using DaScreenshotStudio.Engine.Text;

namespace DaScreenshotStudio.Engine.Ui;

public sealed class ShowUsersListPane : RollPane
{
    public const int RowHeight = 0xC;

    public const int EntriesPerRow = 1;

    public const int CharacterWidth = 6;

    public const int TitleColumns = 0x15;

    public const int NameColumns = 0xF;

    public const int NameOffset = 0x86;

    public const int StateOffset = 0xE3;

    public const byte TitlePaletteIndex = 0x14;

    public const int StateFrameCount = 8;

    public const int MaxTitleLength = 0x30;

    public const int MaxNameLength = 0x18;

    private readonly Pixmap[] _stateFrames = new Pixmap[StateFrameCount];
    private readonly TextBlock _text;

    private byte[] _state = new byte[64];
    private byte[] _palette = new byte[64];
    private byte[] _title = new byte[64 * MaxTitleLength];
    private byte[] _titleLength = new byte[64];
    private byte[] _name = new byte[64 * MaxNameLength];
    private byte[] _nameLength = new byte[64];

    public ShowUsersListPane(in PixelRect bounds, PixelBlender blender, UiAssets assets)
        : base(in bounds, blender, RowHeight, EntriesPerRow)
    {
        ArgumentNullException.ThrowIfNull(assets);

        _text = new TextBlock(assets.Font, assets.Palette, Math.Max(1, bounds.Width), 1)
        {
            Wraps = false,
            Alignment = TextAlignment.Left,
        };
    }

    public int Capacity => _state.Length;

    public byte StateAt(int index) => _state[CheckIndex(index)];

    public byte PaletteAt(int index) => _palette[CheckIndex(index)];

    public ReadOnlySpan<byte> TitleAt(int index) =>
        _title.AsSpan(CheckIndex(index) * MaxTitleLength, _titleLength[index]);

    public ReadOnlySpan<byte> NameAt(int index) =>
        _name.AsSpan(CheckIndex(index) * MaxNameLength, _nameLength[index]);

    public void SetStateFrames(UiAssets assets, GuiLayoutControl? control)
    {
        ArgumentNullException.ThrowIfNull(assets);

        if (control is not { Images.Count: > 1 })
        {
            return;
        }

        var first = control.Images[0].Frame;
        var step = control.Images[1].Frame - first;
        for (var i = 0; i < _stateFrames.Length; i++)
        {
            _stateFrames[i] = assets.LayoutFrame(control.Images[0].EntryName, first + (step * i));
        }

        Invalidate();
    }

    public void Clear()
    {
        Count = 0;
        Selected = -1;
        Invalidate();
    }

    public void Append(byte state, byte palette, scoped ReadOnlySpan<byte> title, scoped ReadOnlySpan<byte> name)
    {
        if (Count == Capacity)
        {
            Grow();
        }

        _state[Count] = state < StateFrameCount ? state : (byte)0;
        _palette[Count] = palette;

        var keptTitle = title.Length >= MaxTitleLength ? default : title;
        var keptName = name.Length >= MaxNameLength ? default : name;

        _titleLength[Count] = (byte)keptTitle.Length;
        keptTitle.CopyTo(_title.AsSpan(Count * MaxTitleLength));
        _nameLength[Count] = (byte)keptName.Length;
        keptName.CopyTo(_name.AsSpan(Count * MaxNameLength));

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
        DrawField(canvas, TitleAt(index), TitlePaletteIndex, 0, TitleColumns, top);
        DrawField(canvas, NameAt(index), _palette[index], NameOffset, NameColumns, top);

        var frame = _stateFrames[_state[index]];
        if (!frame.IsEmpty)
        {
            canvas.BlitPixmap(StateOffset, top, in frame, BlendMode.Transparent);
        }
    }

    private void DrawField(
        Canvas canvas, scoped ReadOnlySpan<byte> text, byte palette, int offset, int columns, int top)
    {
        if (text.Length == 0)
        {
            return;
        }

        _text.DefaultPaletteIndex = palette;
        _text.SetText(text);

        var x = offset + ((columns - text.Length) * CharacterWidth);
        _text.Draw(canvas, x, top + RowHeight - _text.LineHeight);
    }

    private void Grow()
    {
        var wanted = Capacity * 2;
        Array.Resize(ref _state, wanted);
        Array.Resize(ref _palette, wanted);
        Array.Resize(ref _title, wanted * MaxTitleLength);
        Array.Resize(ref _titleLength, wanted);
        Array.Resize(ref _name, wanted * MaxNameLength);
        Array.Resize(ref _nameLength, wanted);
    }
}
