using DaScreenshotStudio.Engine.Records;

namespace DaScreenshotStudio.Engine.Ui;

public sealed partial class GuiBackPane
{
    private static void SeedChatSpacer(TextLog log) => log.Append(ReadOnlySpan<byte>.Empty);

    private static void SeedHistorySpacers(TextLog log)
    {
        for (var i = 0; i < HistorySpacerEntries; i++)
        {
            log.Append(ReadOnlySpan<byte>.Empty);
        }
    }

    public const int HistorySpacerEntries = 10;

    private readonly byte[] _historyLine = new byte[TextLog.MaxAcceptedMessageLength];

    public void AppendHistory(scoped ReadOnlySpan<byte> text)
    {
        if (text.Length > TextLog.MaxAcceptedMessageLength)
        {
            return;
        }

        var line = _historyLine.AsSpan(0, text.Length);
        text.CopyTo(line);

        for (var i = 0; i + 1 < line.Length; i++)
        {
            if (line[i] == (byte)'\r')
            {
                line[i] = (byte)' ';
            }
        }

        History.Append(line);
    }

    public void ResetLogs()
    {
        ChatLog.Clear();
        History.Clear();
        SeedChatSpacer(ChatLog);
        SeedHistorySpacers(History);
        Refresh();
    }

    public TextLog ChatLog { get; }

    public TextLog History { get; }

    public LogViewPane? Chat { get; private set; }

    public ChatInputPane? Input { get; private set; }

    public NumberLinePane? NumberLine { get; private set; }

    public SpellInputPane? SpellInput { get; private set; }

    public BlockListenPane? BlockListen { get; private set; }

    public bool IsLineInputOpen =>
        Input is { IsFocused: true }
        || Tell is { IsFocused: true }
        || NumberLine is { IsFocused: true }
        || SpellInput is { IsFocused: true }
        || BlockListen is { IsOpen: true };

    public bool TryTakeLineInputPointer(in PointerEvent pointer)
    {
        if (!IsLineInputOpen)
        {
            return false;
        }

        _ = Input?.DispatchPointer(in pointer) ?? false;
        _ = Tell?.DispatchPointer(in pointer) ?? false;
        _ = NumberLine?.DispatchPointer(in pointer) ?? false;
        _ = SpellInput?.DispatchPointer(in pointer) ?? false;
        _ = BlockListen?.DispatchPointer(in pointer) ?? false;

        return true;
    }

    public bool TryTakeLineInputKey(in KeyEvent key)
    {
        if (Input is { IsFocused: true } say)
        {
            return say.DispatchKey(in key);
        }

        if (Tell is { IsFocused: true } tell)
        {
            return tell.DispatchKey(in key);
        }

        if (NumberLine is { IsFocused: true } number)
        {
            return number.DispatchKey(in key);
        }

        if (SpellInput is { IsFocused: true } spell)
        {
            return spell.DispatchKey(in key);
        }

        if (BlockListen is { IsOpen: true } block)
        {
            return block.DispatchKey(in key);
        }

        return false;
    }

    public TellInputPane? Tell { get; private set; }

    public TellRecipients TellRecipients { get; } = new();

    public SystemMessagePane? SystemMessage { get; private set; }

    public void OpenSayInput(SpeechMode mode, scoped ReadOnlySpan<byte> initialText = default)
    {
        ClearDescription(0);
        Input?.Open(mode, _name.AsSpan(0, _nameLength), initialText);
    }

    public void OpenBlockListenInput() => BlockListen?.Open();

    private void RefreshChatScrollRange()
    {
        if (_chatScrollBar is not { } bar || Chat is not { } chat)
        {
            return;
        }

        bar.PageStep = chat.Composer.PageStep;
        bar.Maximum = chat.Maximum;
        bar.Position = chat.Scroll;
    }

    public void OpenTellInput()
    {
        if (Tell is null)
        {
            return;
        }

        ClearDescription(0);
        Input?.Cancel();
        Tell.Open();
    }

    public void OpenTellInput(scoped ReadOnlySpan<byte> receiver)
    {
        if (Tell is null)
        {
            return;
        }

        ClearDescription(0);
        Input?.Cancel();
        Tell.Open(receiver);
    }
}
