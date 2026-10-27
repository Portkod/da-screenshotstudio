using DaScreenshotStudio.Engine.Rendering;
using DaScreenshotStudio.Engine.Text;

namespace DaScreenshotStudio.Engine.Ui;

public sealed class NumberLinePane : LineInputPane
{
    public const int LineWidth = 0x1B6;

    public const int LineHeight = 0xC;

    public const byte InkIndex = 0x1F;

    public const byte BackgroundIndex = 0xFF;

    public const int ControlOffsetX = SpellInputPane.ControlOffsetX;

    public const int MaxDigits = 0x1F;

    public const int MaxPromptBytes = 128;

    private readonly byte[] _digits = new byte[MaxDigits];
    private readonly byte[] _promptBytes = new byte[MaxPromptBytes];
    private int _promptLength;
    private int _length;

    public NumberLinePane(in PixelRect bounds, PixelBlender blender, GameFont font, TextPalette palette)
        : base(in bounds, blender, font, palette)
    {
    }

    public bool IsFocused { get; private set; }

    public uint Maximum { get; private set; }

    public ReadOnlySpan<byte> Prompt => _promptBytes.AsSpan(0, _promptLength);

    public ReadOnlySpan<byte> Value => _digits.AsSpan(0, _length);

    public void Open(scoped ReadOnlySpan<byte> message, uint maximum)
    {
        var text = new TextBuffer(_promptBytes);
        text.Append(message);
        text.Append(" [ 0 - "u8);
        text.Append(maximum);
        text.Append(" ]: "u8);
        _promptLength = text.Length;

        Maximum = maximum;
        _length = 0;
        IsFocused = true;
        Visible = true;

        Invalidate();
    }

    protected override bool AcceptsKeys => IsFocused;

    protected override bool CanCancel => false;

    protected override bool Accepts(byte character) => character is >= (byte)'0' and <= (byte)'9';

    protected override void Append(byte character)
    {
        if (_length < MaxDigits)
        {
            _digits[_length++] = character;
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

    protected override void Submit()
    {
        IsFocused = false;
        Visible = false;
        _length = 0;
        Invalidate();
    }

    protected override void DrawLine(Canvas canvas) =>
        DrawOpaqueLine(canvas, BackgroundIndex, InkIndex, ControlOffsetX, _promptLength, Prompt, Value);
}
