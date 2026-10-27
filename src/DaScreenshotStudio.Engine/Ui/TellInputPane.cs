using DaScreenshotStudio.Engine.Rendering;
using DaScreenshotStudio.Engine.Text;
using DaScreenshotStudio.Engine.Core;

namespace DaScreenshotStudio.Engine.Ui;

public enum TellStage
{
    None = 0,

    Receiver = 1,

    Message = 2,
}

public sealed class TellInputPane : LineInputPane
{
    public const int MaxReceiverBytes = 255;

    public const int MaxTypedReceiverBytes = 13;

    public const int MaxSubmittedReceiverBytes = TellRecipients.MaxNameBytes;

    public const int MaxMessageBytes = 100;

    public const int MessageLineBytes = 70;

    public const int MaxPromptBytes = 32;

    public const byte TextPaletteIndex = TextMarkup.Chant;

    public const byte BackgroundPalette = ChatInputPane.BackgroundPalette;

    private static ReadOnlySpan<byte> ReceiverPromptHead => "to ["u8;

    private static ReadOnlySpan<byte> ReceiverPromptTail => "]? "u8;

    private static ReadOnlySpan<byte> MessagePromptHead => "-> "u8;

    private static ReadOnlySpan<byte> MessagePromptTail => ": "u8;

    private readonly TellRecipients _recipients;
    private readonly byte[] _receiver = new byte[MaxReceiverBytes];
    private readonly byte[] _message = new byte[MaxMessageBytes];
    private readonly byte[] _promptText = new byte[MaxPromptBytes];
    private readonly ushort _background;
    private int _bandWidth;

    private int _receiverLength;
    private int _messageLength;

    public TellInputPane(
        in PixelRect bounds, PixelBlender blender, GameFont font, TextPalette palette, TellRecipients recipients)
        : base(in bounds, blender, font, palette)
    {
        ArgumentNullException.ThrowIfNull(recipients);

        _recipients = recipients;
        _background = palette[BackgroundPalette];
    }

    public Action? Closed { get; set; }

    public TellStage Stage
    {
        get;
        private set
        {
            if (field == value)
            {
                return;
            }

            field = value;

            Visible = value != TellStage.None;
            Invalidate();
        }
    }

    public bool IsFocused => Stage != TellStage.None;

    public ReadOnlySpan<byte> Receiver => _receiver.AsSpan(0, _receiverLength);

    public ReadOnlySpan<byte> Message => _message.AsSpan(0, _messageLength);

    public ReadOnlySpan<byte> Value => Stage == TellStage.Receiver ? Receiver : Message;

    public ReadOnlySpan<byte> Prompt => BuildPrompt();

    public void Open()
    {
        _receiverLength = 0;
        _messageLength = 0;
        _recipients.ResetCursor();
        Stage = TellStage.Receiver;

        Invalidate();
    }

    public void Open(scoped ReadOnlySpan<byte> receiver)
    {
        _receiverLength = Math.Min(receiver.Length, MaxReceiverBytes);
        receiver[.._receiverLength].CopyTo(_receiver);
        _messageLength = 0;
        Stage = TellStage.Message;
        Invalidate();
    }

    public void Cancel()
    {
        if (Stage == TellStage.None)
        {
            return;
        }

        _receiverLength = 0;
        _messageLength = 0;
        Stage = TellStage.None;
        Closed?.Invoke();
        Invalidate();
    }

    protected override bool AcceptsKeys => IsFocused;

    protected override void Append(byte character)
    {
        if (Stage == TellStage.Receiver)
        {
            if (_receiverLength < MaxTypedReceiverBytes)
            {
                _receiver[_receiverLength++] = character;
                Invalidate();
            }

            return;
        }

        if (_messageLength < MessageLimit)
        {
            _message[_messageLength++] = character;
            Invalidate();
        }
    }

    protected override void Backspace()
    {
        if (Stage == TellStage.Receiver)
        {
            if (_receiverLength > 0)
            {
                _receiverLength--;
                Invalidate();
            }

            return;
        }

        if (_messageLength > 0)
        {
            _messageLength--;
            Invalidate();
        }
    }

    protected override void CancelInput() => Cancel();

    protected override void OnOtherKey(in KeyEvent key)
    {
        if (key.Type != UiEventType.KeyDown || Stage != TellStage.Receiver)
        {
            return;
        }

        if (key.Key == UiKeys.Up)
        {
            Recall(older: true);
        }
        else if (key.Key == UiKeys.Down)
        {
            Recall(older: false);
        }
    }

    protected override void DrawLine(Canvas canvas)
    {
        var prompt = BuildPrompt();
        _bandWidth = prompt.Length * CellWidth;
        DrawShadedLine(canvas, _background, TextPaletteIndex, _bandWidth, prompt, Value);
    }

    protected override void OnComposite(Canvas target, int absoluteX, int absoluteY) =>
        CompositeLine(target, absoluteX, absoluteY, Content, _bandWidth);

    private ReadOnlySpan<byte> BuildPrompt()
    {
        var text = new TextBuffer(_promptText);

        if (Stage == TellStage.Receiver)
        {
            text.Append(ReceiverPromptHead);
            text.Append(_recipients.MostRecent);
            text.Append(ReceiverPromptTail);
            return text.Written;
        }

        text.Append(MessagePromptHead);
        text.Append(Receiver);
        text.Append(MessagePromptTail);
        return text.Written;
    }

    private int MessageLimit
    {
        get
        {
            var prompt = Math.Min(
                MessagePromptHead.Length + _receiverLength + MessagePromptTail.Length, MaxPromptBytes - 1);
            return Math.Clamp(MessageLineBytes - prompt, 0, MaxMessageBytes);
        }
    }

    private void Recall(bool older)
    {
        var found = older ? _recipients.TryOlder(out var name) : _recipients.TryNewer(out name);
        if (!found)
        {
            return;
        }

        _receiverLength = Math.Min(name.Length, MaxTypedReceiverBytes);
        name[.._receiverLength].CopyTo(_receiver);
        Invalidate();
    }

    protected override void Submit()
    {
        if (Stage == TellStage.Receiver)
        {
            var typed = Math.Min(_receiverLength, MaxSubmittedReceiverBytes);
            _recipients.Remember(_receiver.AsSpan(0, typed));

            var target = _recipients.MostRecent;
            _receiverLength = target.Length;
            target.CopyTo(_receiver);

            _messageLength = 0;
            Stage = TellStage.Message;

            return;
        }

        _receiverLength = 0;
        _messageLength = 0;
        Stage = TellStage.None;
        Closed?.Invoke();
        Invalidate();
    }
}
