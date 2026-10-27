using DaScreenshotStudio.Engine.Rendering;
using DaScreenshotStudio.Engine.Text;
using DaScreenshotStudio.Engine.Records;

namespace DaScreenshotStudio.Engine.Ui;

public sealed class BulletinBodyPane : GuiTextPane, IFocusableControl
{
    public BulletinBodyPane(
        in PixelRect bounds, PixelBlender blender, GameFont font, TextPalette palette, int maxLines)
        : base(in bounds, blender, font, palette, maxLines)
    {
    }

    public bool IsFocusable { get; set; } = true;

    public bool IsFocused { get; set; }

    ControlKind IFocusableControl.Kind => ControlKind.Scrollable;

    bool IFocusableControl.TakesEnter => false;
}

public sealed class BulletinEntryDialogPane : BulletinDialogPane
{
    public const string ArticleLayoutName = "_narti.txt";

    public const string MailLayoutName = "_nmailr.txt";

    private readonly bool _mailbox;
    private readonly ImageButtonPane? _previous;
    private readonly GuiTextPane? _author;
    private readonly GuiTextPane? _title;
    private readonly GuiTextPane? _date;
    private readonly BulletinBodyPane? _content;
    private ScrollBarPane? _bar;
    private PixelRect _contentControl;

    private const byte CarriageReturn = 0x0D;

    private readonly byte[] _laidOut = new byte[MessagePopupPane.MaxRetainedBytes];

    protected override ScrollBarPane? WheelScrollBar(int index) => _bar;

    protected override PixelRect WheelControlArea(int index) => _contentControl;

    private readonly byte[] _sender = new byte[BulletinListPane.MaxAuthorLength];
    private int _senderLength;

    public BulletinEntryDialogPane(UiAssets assets, PixelBlender blender, bool mailbox)
        : base(assets, blender, mailbox ? MailLayoutName : ArticleLayoutName)
    {
        _mailbox = mailbox;

        _previous = AddButton("Prev", _ => Answer(0));
        AddButton("Next", _ => Answer(1));
        AddButton("New", _ => Answer(2));

        if (mailbox)
        {
            AddButton("Reply", _ => Answer(3));
            AddButton("Delete", _ => Answer(4));
            AddButton("Up", _ => Answer(5));
            AddButton("Quit", _ => Answer(6));
        }
        else
        {
            AddButton("Delete", _ => Answer(3));
            AddButton("Up", _ => Answer(4));
            AddButton("Close", _ => Answer(5));
        }

        _author = AddLabel("Author", paletteIndex: LabelColor("Author"));
        _title = AddLabel("Title", paletteIndex: LabelColor("Title"));
        _content = BuildContent(assets, blender);
        _date = AddLabel("Mmdd", paletteIndex: LabelColor("Mmdd"));

        if (!mailbox)
        {
            FocusControl(_content);
        }
    }

    public override BulletinView View => _mailbox ? BulletinView.Mail : BulletinView.Article;

    public override bool ReplacedOnPush => true;

    public ushort EntryId { get; private set; }

    public bool IsMailbox => _mailbox;

    public ScrollBarPane? ScrollBar => _bar;

    public BulletinBodyPane? Body => _content;

    public void Apply(in BulletinArticle reply)
    {
        EntryId = reply.EntryId;

        _senderLength = Math.Min(reply.Author.Length, _sender.Length);
        reply.Author.AsSpan(0, _senderLength).CopyTo(_sender);

        _author?.SetText(reply.Author);
        _title?.SetText(reply.Title);
        SetBody(reply.Content);

        Span<byte> date = stackalloc byte[8];
        _date?.SetText(FormatDate(reply.Month, reply.Day, date));

        if (_previous is not null)
        {
            _previous.Enabled = reply.NavigationFlags != 0;
        }

        RefreshScroll();

        Invalidate();
    }

    public void Answer(int action)
    {
        if (_mailbox)
        {
            switch (action)
            {
                case 2 or 3 or 5: GoBack(); break;
                case 6: CloseSession(); break;
                default: break;
            }

            return;
        }

        switch (action)
        {
            case 2 or 4: GoBack(); break;
            case 5: CloseSession(); break;
            default: break;
        }
    }

    protected override void OnCancel() => Answer(_mailbox ? 6 : 5);

    protected override bool ChainsToRailKeys => !_mailbox;

    protected override bool OnKey(in KeyEvent key)
    {
        if (!Visible || key.Type != UiEventType.KeyDown)
        {
            return base.OnKey(in key);
        }

        switch (key.Key)
        {
            case UiKeys.Escape:
                GoBack();
                return true;

            case 'N':
                GoBack();
                return true;

            case 'R' when _mailbox:
                GoBack();
                return true;

            case UiKeys.Delete or UiKeys.PageUp or UiKeys.PageDown:
                return true;

            default:
                return base.OnKey(in key);
        }
    }

    private void SetBody(scoped ReadOnlySpan<byte> content)
    {
        if (_content is null)
        {
            return;
        }

        var length = Math.Min(content.Length, _laidOut.Length);
        for (var i = 0; i < length; i++)
        {
            _laidOut[i] = content[i] == CarriageReturn ? TextBlock.LineBreak : content[i];
        }

        var laid = _laidOut.AsSpan(0, length);
        var visible = Math.Max(1, _content.Bounds.Height / Math.Max(1, _content.Text.LineHeight));

        _content.Text.MaxLines = Math.Max(visible, length);
        _content.Text.MaxLines = Math.Max(visible, _content.Text.CountLines(laid));
        _content.SetText(laid);
        _content.TopPadding = 0;
    }

    private void RefreshScroll()
    {
        if (_bar is null || _content is null)
        {
            return;
        }

        var lineHeight = Math.Max(1, _content.Text.LineHeight);
        var overflow = (_content.Text.LineCount * lineHeight) - _content.Bounds.Height;

        _bar.Maximum = overflow <= 0 ? 0 : ((overflow - 1) / lineHeight) + 1;
        _bar.PageStep = Math.Max(1, (_content.Bounds.Height / lineHeight) - 1);
        _bar.Position = 0;
    }

    private BulletinBodyPane? BuildContent(UiAssets assets, PixelBlender blender)
    {
        if (!GuiLayoutGeometry.TryRect(Layout, "Content", out var rect)
            || ScrollBarPane.LoadFrames(assets) is not { } frames)
        {
            return null;
        }

        _contentControl = rect;
        var thickness = ScrollBarPane.ThicknessOf(frames);
        var width = Math.Max(1, rect.Width - thickness);

        var content = new BulletinBodyPane(
            new PixelRect(rect.X, rect.Y, width, rect.Height),
            blender,
            assets.Font,
            assets.Palette,
            maxLines: Math.Max(1, rect.Height / assets.Font.LineHeight))
        {
            Background = Background,
            Wraps = true,

            IsScrolled = true,
            PaletteIndex = GuiLayoutGeometry.Color(Layout, "Content", TextPalette.DefaultIndex),
        };

        var bar = new ScrollBarPane(
            new PixelRect(rect.Right - thickness, rect.Y, thickness, rect.Height),
            blender,
            frames,
            horizontal: false);

        bar.Scrolled = () =>
        {
            content.TopPadding = -bar.Position * Math.Max(1, content.Text.LineHeight);
            content.Invalidate();
        };

        bar.FocusRequested = () => FocusControl(content);

        Add(content);
        Add(bar);
        _bar = bar;
        return content;
    }

    private static ReadOnlySpan<byte> FormatDate(byte month, byte day, Span<byte> destination)
    {
        var written = Pad2(month, destination, 0);
        destination[written++] = (byte)'/';
        written = Pad2(day, destination, written);
        return destination[..written];
    }

    private static int Pad2(byte value, scoped Span<byte> destination, int offset)
    {
        destination[offset++] = value >= 10 ? (byte)('0' + (value / 10)) : (byte)' ';
        destination[offset++] = (byte)('0' + (value % 10));
        return offset;
    }
}

public sealed class BulletinComposeDialogPane : BulletinDialogPane
{
    public const string ArticleLayoutName = "_nartin.txt";

    public const string MailLayoutName = "_nmails.txt";

    public const int MaxTitleLength = 0x3C;

    public const int MaxContentLength = 0xBB8;

    private readonly bool _mailbox;
    private readonly GuiTextPane? _staticRecipient;
    private readonly TextEditPane? _recipientEdit;
    private readonly TextEditPane? _title;
    private readonly TextEditPane? _content;

    public BulletinComposeDialogPane(
        UiAssets assets, PixelBlender blender, bool mailbox, scoped ReadOnlySpan<byte> recipient)
        : base(assets, blender, mailbox ? MailLayoutName : ArticleLayoutName)
    {
        _mailbox = mailbox;

        AddButton("Send", _ => Answer(0));
        AddButton("Cancel", _ => Answer(1));

        if (!mailbox)
        {
            _staticRecipient = AddLabel("Author", paletteIndex: LabelColor("Author"));
        }
        else if (!recipient.IsEmpty)
        {
            _staticRecipient = AddLabel("Receiver", paletteIndex: LabelColor("Receiver"));
            _staticRecipient?.SetText(recipient);
        }
        else
        {
            _recipientEdit = AddEdit("ReceiverEdit", TextEditPane.DefaultMaxLength);
        }

        _title = AddEdit("Title", MaxTitleLength);
        _content = AddEdit("Content", MaxContentLength, verticalBar: true);

        if (_content is not null)
        {
            _content.TakesEnter = true;
        }

        Focus(_recipientEdit ?? _title);
    }

    public override BulletinView View => _mailbox ? BulletinView.NewMail : BulletinView.NewArticle;

    public bool IsMailbox => _mailbox;

    public void SetAuthor(scoped ReadOnlySpan<byte> author)
    {
        if (!_mailbox)
        {
            _staticRecipient?.SetText(author);
        }
    }

    public void Answer(int action)
    {
        switch (action)
        {
            case 1:
                GoBack();
                break;

            default:
                break;
        }
    }

    protected override void OnCancel() => Answer(1);

    protected override bool ChainsToRailKeys => false;

    protected override bool OnKey(in KeyEvent key)
    {
        if (!Visible || key.Type != UiEventType.KeyDown)
        {
            return base.OnKey(in key);
        }

        if (key.Key == UiKeys.Escape)
        {
            Answer(1);
            return true;
        }

        return _mailbox || key.Key != UiKeys.F12 || base.OnKey(in key);
    }
}
