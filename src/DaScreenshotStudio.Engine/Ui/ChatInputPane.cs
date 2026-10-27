using DaScreenshotStudio.Engine.Rendering;
using DaScreenshotStudio.Engine.Text;
using DaScreenshotStudio.Engine.Core;
using DaScreenshotStudio.Engine.Records;

namespace DaScreenshotStudio.Engine.Ui;

public sealed class ChatInputPane : LineInputPane
{
    public const int MaxEditableBytes = 72;

    public const int SayLineBytes = 67;

    public const int MaxPrefixBytes = 64;

    public const byte SayPalette = TextMarkup.White;

    public const byte ShoutPalette = TextMarkup.Yellow;

    public const byte BackgroundPalette = TextPalette.ShadowIndex;

    public int PromptBandWidth => _prefixLength * CellWidth;

    private readonly ushort _background;
    private readonly byte[] _buffer = new byte[MaxEditableBytes];
    private readonly byte[] _prefix = new byte[MaxPrefixBytes];
    private int _prefixLength;
    private int _length;

    public ChatInputPane(in PixelRect bounds, PixelBlender blender, GameFont font, TextPalette palette)
        : base(in bounds, blender, font, palette)
    {
        _background = palette[BackgroundPalette];

        blender.ShadeKey = _background;
    }

    public SpeechMode Mode
    {
        get;
        private set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            Invalidate();
        }
    } = SpeechMode.Say;

    public bool IsFocused
    {
        get;
        set
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

    public ReadOnlySpan<byte> Prefix => _prefix.AsSpan(0, _prefixLength);

    public int Length => _length;

    public byte PromptPalette => Mode == SpeechMode.Shout ? ShoutPalette : SayPalette;

    public int Limit => Math.Clamp(SayLineBytes - _prefixLength, 0, MaxEditableBytes);

    public void Open(SpeechMode mode, scoped ReadOnlySpan<byte> localName, scoped ReadOnlySpan<byte> initialText = default)
    {
        Mode = mode;

        var text = new TextBuffer(_prefix);
        text.Append(localName);
        text.Append(mode == SpeechMode.Shout ? (byte)'!' : (byte)':');
        text.Append((byte)' ');
        _prefixLength = text.Length;

        _length = Math.Min(initialText.Length, Limit);
        initialText[.._length].CopyTo(_buffer);

        IsFocused = true;

        Invalidate();
    }

    public void Clear()
    {
        if (_length == 0)
        {
            return;
        }

        _length = 0;
        Invalidate();
    }

    public void Cancel()
    {
        Clear();
        IsFocused = false;
    }

    protected override bool AcceptsKeys => IsFocused;

    protected override void Append(byte character)
    {
        if (_length < Limit)
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

    protected override void CancelInput() => Cancel();

    protected override bool OnPointer(in PointerEvent pointer)
    {
        if (pointer.Type != UiEventType.LeftDown || !Contains(in pointer))
        {
            return false;
        }

        IsFocused = true;
        return true;
    }

    protected override void DrawLine(Canvas canvas) =>
        DrawShadedLine(canvas, _background, PromptPalette, PromptBandWidth, Prefix, Value);

    protected override void OnComposite(Canvas target, int absoluteX, int absoluteY) =>
        CompositeLine(target, absoluteX, absoluteY, Content, PromptBandWidth);

    protected override void Submit()
    {
        _length = 0;
        IsFocused = false;
        Invalidate();
    }
}
