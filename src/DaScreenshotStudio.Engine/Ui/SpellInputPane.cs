using DaScreenshotStudio.Engine.Rendering;
using DaScreenshotStudio.Engine.Text;
using DaScreenshotStudio.Engine.Records;

namespace DaScreenshotStudio.Engine.Ui;

public enum SpellInputMode
{
    None = 0,

    Text = 1,

    Numbers = 2,
}

public sealed class SpellInputPane : LineInputPane
{
    public const int LineWidth = 0x1B6;

    public const int LineHeight = 0xC;

    public const int MaxNumericBytes = 0xFF;

    public const byte ValueSeparator = (byte)',';

    public const int MaxPromptBytes = 128;

    public const byte BackgroundIndex = 0xFF;

    public const byte InkIndex = 0x1F;

    public const int ControlOffsetX = -1;

    public const int MaxLineBytes = MaxNumericBytes;

    private readonly byte[] _promptBytes = new byte[MaxPromptBytes];
    private readonly byte[] _line = new byte[MaxLineBytes];
    private readonly ushort[] _values = new ushort[AbilitySlot.MaxNumericValues];

    private int _promptLength;
    private int _length;
    private int _valueCount;

    public SpellInputPane(in PixelRect bounds, PixelBlender blender, GameFont font, TextPalette palette)
        : base(in bounds, blender, font, palette)
    {
    }

    public Action? Closed { get; set; }

    public SpellInputMode Mode { get; private set; }

    public byte Slot { get; private set; }

    public bool IsFocused => Mode != SpellInputMode.None;

    public int ExpectedValueCount { get; private set; }

    public ReadOnlySpan<byte> Prompt => _promptBytes.AsSpan(0, _promptLength);

    public ReadOnlySpan<byte> Value => _line.AsSpan(0, _length);

    public ReadOnlySpan<ushort> Values => _values.AsSpan(0, _valueCount);

    public void OpenText(byte slot, scoped ReadOnlySpan<byte> prompt) =>
        Open(SpellInputMode.Text, slot, prompt, expectedValues: 0);

    public void OpenNumbers(byte slot, scoped ReadOnlySpan<byte> prompt, int valueCount) =>
        Open(SpellInputMode.Numbers, slot, prompt, valueCount);

    public void Cancel()
    {
        if (Mode == SpellInputMode.None)
        {
            return;
        }

        Close();
        Closed?.Invoke();
    }

    protected override bool AcceptsKeys => IsFocused;

    protected override bool CanCancel => Mode == SpellInputMode.Text;

    protected override void Append(byte character)
    {
        if (_length < MaxLineBytes)
        {
            _line[_length++] = character;
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

    protected override void CancelInput() => Cancel();

    public bool TryRead()
    {
        _valueCount = 0;

        if (_length == 0)
        {
            return false;
        }

        if (Mode != SpellInputMode.Numbers)
        {
            return true;
        }

        var line = Value;
        var start = 0;
        for (var index = 0; index < ExpectedValueCount; index++)
        {
            if (start >= line.Length)
            {
                break;
            }

            int end;
            if (index == ExpectedValueCount - 1)
            {
                end = line.Length;
            }
            else
            {
                var separator = line[start..].IndexOf(ValueSeparator);
                if (separator < 0)
                {
                    _valueCount = 0;
                    return false;
                }

                end = start + separator;
            }

            _values[index] = (ushort)GoldAmountDialogPane.Parse(line[start..end]);
            start = end + 1;
        }

        _valueCount = ExpectedValueCount;
        return true;
    }

    protected override void DrawLine(Canvas canvas) =>
        DrawOpaqueLine(canvas, BackgroundIndex, InkIndex, ControlOffsetX, _promptLength, Prompt, Value);

    protected override bool Accepts(byte character) => Mode switch
    {
        SpellInputMode.Text => character >= 0x20 && character != 0x7F,
        SpellInputMode.Numbers => character is >= (byte)'0' and <= (byte)'9' or ValueSeparator or (byte)' ',
        _ => false,
    };

    private void Open(SpellInputMode mode, byte slot, scoped ReadOnlySpan<byte> prompt, int expectedValues)
    {
        _promptLength = Math.Min(prompt.Length, MaxPromptBytes);
        prompt[.._promptLength].CopyTo(_promptBytes);

        Mode = mode;
        Slot = slot;
        ExpectedValueCount = expectedValues;
        _length = 0;
        _valueCount = 0;
        Visible = true;

        Invalidate();
    }

    protected override void Submit()
    {
        Close();
        Closed?.Invoke();
    }

    private void Close()
    {
        Mode = SpellInputMode.None;
        Slot = 0;
        _length = 0;
        _valueCount = 0;
        Visible = false;
        Invalidate();
    }
}
