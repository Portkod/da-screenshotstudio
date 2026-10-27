using DaScreenshotStudio.Assets.Tables;
using DaScreenshotStudio.Engine.Rendering;
using DaScreenshotStudio.Engine.Text;

namespace DaScreenshotStudio.Engine.Ui;

public enum BlockListenPrompt
{
    Select,

    Add,

    Remove,
}

public sealed class BlockListenPane : LineInputPane
{
    public const int MaxNameBytes = 15;

    public const byte TextPaletteIndex = TextPalette.InterfaceIndex;

    public const byte BackgroundPalette = TextPalette.ShadowIndex;

    public const InputModifiers ListModifiers = InputModifiers.Shift;

    private readonly MessageTable _messages;
    private readonly ushort _background;
    private readonly byte[] _buffer = new byte[MaxNameBytes];
    private int _length;
    private int _promptCells;

    public BlockListenPane(
        in PixelRect bounds, PixelBlender blender, GameFont font, TextPalette palette, MessageTable messages)
        : base(in bounds, blender, font, palette)
    {
        _messages = messages;
        _background = palette[BackgroundPalette];
    }

    public BlockListenPrompt Prompt { get; private set; }

    public bool IsOpen
    {
        get;
        private set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            Visible = value;
            Invalidate();
        }
    }

    public ReadOnlySpan<byte> Value => _buffer.AsSpan(0, _length);

    public void Open()
    {
        if (IsOpen)
        {
            return;
        }

        Show(BlockListenPrompt.Select);
    }

    public void Close() => IsOpen = false;

    protected override bool AcceptsKeys => IsOpen;

    protected override bool OnKey(in KeyEvent key)
    {
        if (!AcceptsKeys)
        {
            return false;
        }

        return Prompt == BlockListenPrompt.Select ? OnSelectKey(in key) : base.OnKey(in key);
    }

    private bool OnSelectKey(in KeyEvent key)
    {
        if (key.Type != UiEventType.KeyDown || key.Key is UiKeys.Shift or UiKeys.Control or UiKeys.Alt)
        {
            return true;
        }

        if (key.Key == UiKeys.Slash && key.Modifiers == ListModifiers)
        {
            return true;
        }

        Select(key.Key);
        return true;
    }

    private void Select(int character)
    {
        switch (character)
        {
            case (byte)'A' or (byte)'a':
                Show(BlockListenPrompt.Add);
                return;
            case (byte)'D' or (byte)'d':
                Show(BlockListenPrompt.Remove);
                return;
            default:
                Close();
                return;
        }
    }

    protected override void Append(byte character)
    {
        if (_length < MaxNameBytes)
        {
            _buffer[_length++] = character;
            Invalidate();
        }
    }

    protected override void Backspace()
    {
        if (_length > 0)
        {
            _length--;
            Invalidate();
        }
    }

    protected override void CancelInput() => Close();

    protected override void Submit() => Close();

    private void Show(BlockListenPrompt prompt)
    {
        Prompt = prompt;
        _length = 0;
        _promptCells = _messages.Get(PromptSlot(prompt)).Length;
        IsOpen = true;

        Invalidate();
    }

    private static int PromptSlot(BlockListenPrompt prompt) => prompt switch
    {
        BlockListenPrompt.Add => MessageTable.BlockListenAddPromptSlot,
        BlockListenPrompt.Remove => MessageTable.BlockListenRemovePromptSlot,
        _ => MessageTable.BlockListenPromptSlot,
    };

    private int PromptBandWidth => _promptCells * CellWidth;

    protected override void OnComposite(Canvas target, int absoluteX, int absoluteY) =>
        CompositeLine(target, absoluteX, absoluteY, Content, PromptBandWidth);

    protected override void DrawLine(Canvas canvas) => DrawShadedLine(
        canvas, _background, TextPaletteIndex, PromptBandWidth, _messages.Get(PromptSlot(Prompt)), Value);
}
