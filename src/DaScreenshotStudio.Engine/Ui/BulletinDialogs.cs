using DaScreenshotStudio.Engine.Rendering;
using DaScreenshotStudio.Engine.Records;

namespace DaScreenshotStudio.Engine.Ui;

public sealed class BoardListDialogPane : BulletinDialogPane
{
    public const string LayoutName = "_nbdlist.txt";

    public const string ViewControl = "View";

    public const string QuitControl = "Quit";

    public const string ListControl = "BoardList";

    public const int AnimationSteps = 3;

    public const int AnimationStepMilliseconds = 0x64;

    private readonly BulletinBoardListPane? _list;
    private readonly ScrollBarPane? _bar;
    private readonly ImageButtonPane? _view;
    private readonly PixelRect _fullBounds;

    private int _step;
    private int _direction;
    private bool _animating;
    private uint _nextStepAt;
    private uint _clock;

    public BoardListDialogPane(UiAssets assets, PixelBlender blender)
        : base(assets, blender, LayoutName)
    {
        _fullBounds = Bounds;

        _view = AddButton(ViewControl, _ => Answer(0));
        AddButton(QuitControl, _ => Answer(1));
        DefaultControl = _view;

        if (TryBuildScrolledControl(
                ListControl,
                content => new BulletinBoardListPane(in content, blender, assets),
                out var list,
                out var bar))
        {
            list.SelectionChanged = UpdateOpenButton;
            list.Activated = () => Answer(0);
            _list = list;
            _bar = bar;

            FocusControl(list);
        }

        UpdateOpenButton();
    }

    public override BulletinView View => BulletinView.BoardList;

    public BulletinBoardListPane? List => _list;

    public ScrollBarPane? ScrollBar => _bar;

    public void Apply(ReadOnlySpan<BulletinBoard> boards)
    {
        if (_list is null)
        {
            return;
        }

        _list.Clear();
        foreach (var board in boards)
        {
            _list.Append(board.BoardId, board.Name);
        }

        if (_bar is not null)
        {
            _bar.Maximum = _list.Maximum;
            _bar.PageStep = _list.PageStep;
        }

        UpdateOpenButton();
        Invalidate();
    }

    public void StartOpenAnimation(uint now)
    {
        _clock = now;
        _direction = 1;
        _animating = true;
        _step = 1;
        _nextStepAt = now + AnimationStepMilliseconds;
        ApplyStep();
    }

    public void StartCloseAnimation(uint now)
    {
        _clock = now;
        _direction = 0;
        _animating = true;
        _step = 2;
        _nextStepAt = now + AnimationStepMilliseconds;
        ApplyStep();
    }

    public void Tick(uint now)
    {
        _clock = now;

        if (!_animating || (int)(now - _nextStepAt) < 0)
        {
            return;
        }

        _step += _direction == 1 ? 1 : -1;

        if (_step is > 0 and < AnimationSteps)
        {
            _nextStepAt = now + AnimationStepMilliseconds;
        }
        else
        {
            _animating = false;

            if (_step == 0)
            {
                ApplyStep();
                CloseSession();
                return;
            }
        }

        ApplyStep();
    }

    public void Answer(int action)
    {
        switch (action)
        {
            case 1:
                StartCloseAnimation(_clock);
                break;

            default:
                break;
        }
    }

    protected override void OnCancel() => Answer(1);

    protected override void OnRefreshDefaultActionState() => UpdateOpenButton();

    private void UpdateOpenButton()
    {
        if (_view is not null)
        {
            _view.Enabled = _list is { Selected: >= 0 };
        }
    }

    private void ApplyStep()
    {
        if (_step >= AnimationSteps)
        {
            SetBounds(_fullBounds);
            return;
        }

        var width = _fullBounds.Width / AnimationSteps * _step;
        SetBounds(new PixelRect(_fullBounds.Right - width, _fullBounds.Y, Math.Max(1, width), _fullBounds.Height));
    }
}

public sealed class BulletinListDialogPane : BulletinDialogPane
{
    public const string ArticleLayoutName = "_narlist.txt";

    public const string MailLayoutName = "_nmaill.txt";

    private readonly bool _mailbox;
    private BulletinListPane? _list;
    private ScrollBarPane? _bar;
    private readonly ImageButtonPane? _view;
    private readonly ImageButtonPane? _reply;
    private readonly ImageButtonPane? _delete;
    private readonly ImageButtonPane? _highlight;
    private readonly ImageButtonPane? _close;

    public BulletinListDialogPane(UiAssets assets, PixelBlender blender, bool mailbox, bool privileged)
        : base(assets, blender, mailbox ? MailLayoutName : ArticleLayoutName)
    {
        _mailbox = mailbox;
        IsPrivileged = privileged;

        _view = AddButton("View", _ => Answer(0));
        AddButton("New", _ => Answer(1));
        DefaultControl = _view;

        if (mailbox)
        {
            _reply = AddButton("Reply", _ => Answer(2));
            _delete = AddButton("Delete", _ => Answer(3));
            AddButton("Up", _ => Answer(4));
            _close = AddButton("Quit", _ => Answer(5));
        }
        else
        {
            _delete = AddButton("Delete", _ => Answer(2));
            AddButton("Up", _ => Answer(3));
            _close = AddButton("Close", _ => Answer(4));
        }

        BuildList(assets, blender, mailbox);

        if (!mailbox && privileged)
        {
            _highlight = AddButton("Hilight", _ => Answer(6));
        }

        FocusControl(_list);

        UpdateActionControls();
    }

    public override BulletinView View => _mailbox ? BulletinView.MailList : BulletinView.ArticleList;

    public bool IsMailbox => _mailbox;

    public bool IsPrivileged { get; }

    public ushort ListSectionId { get; private set; }

    public BulletinListPane? List => _list;

    public ScrollBarPane? ScrollBar => _bar;

    public ReadOnlySpan<byte> Heading => _heading.AsSpan(0, _headingLength);

    private readonly byte[] _heading = new byte[byte.MaxValue];
    private int _headingLength;

    public bool Apply(in BulletinListing reply, bool first)
    {
        if (_list is null)
        {
            return false;
        }

        if (!first && reply.SectionId != ListSectionId)
        {
            return false;
        }

        ListSectionId = reply.SectionId;
        _headingLength = Math.Min(reply.Heading.Length, _heading.Length);
        reply.Heading.AsSpan(0, _headingLength).CopyTo(_heading);

        if (first)
        {
            if (_close is not null)
            {
                _close.Enabled = reply.Permissions != 0;
            }
        }

        foreach (var entry in reply.Entries)
        {
            _list.Upsert(entry.Flags, entry.EntryId, entry.Author, entry.Month, entry.Day, entry.Title);
        }

        if (_bar is not null)
        {
            _bar.Maximum = _list.Maximum;
            _bar.PageStep = _list.PageStep;
        }

        UpdateActionControls();
        Invalidate();

        return true;
    }

    public void Answer(int action)
    {
        if (_mailbox)
        {
            switch (action)
            {
                case 4: GoBack(); break;
                case 5: CloseSession(); break;
                default: break;
            }

            return;
        }

        switch (action)
        {
            case 3: GoBack(); break;
            case 4: CloseSession(); break;
            default: break;
        }
    }

    protected override void OnCancel() => Answer(_mailbox ? 5 : 4);

    protected override bool ChainsToRailKeys => !_mailbox;

    protected override bool OnKey(in KeyEvent key)
    {
        if (!Visible || key.Type != UiEventType.KeyDown)
        {
            return base.OnKey(in key);
        }

        var selected = _list is { Selected: >= 0 };

        switch (key.Key)
        {
            case UiKeys.Escape:
                GoBack();
                return true;

            case 'N':
            case 'V' when !_mailbox || selected:
            case 'R' when _mailbox && selected:
            case UiKeys.Delete when !_mailbox || selected:
                return true;

            default:
                return base.OnKey(in key);
        }
    }

    protected override void OnRefreshDefaultActionState() => UpdateActionControls();

    private void UpdateActionControls()
    {
        var selected = _list is { Selected: >= 0 };

        if (_view is not null)
        {
            _view.Enabled = selected;
        }

        if (_reply is not null)
        {
            _reply.Enabled = selected;
        }

        if (_delete is not null)
        {
            _delete.Enabled = selected;
        }

        if (_highlight is not null)
        {
            _highlight.Enabled = selected;
        }
    }

    private void BuildList(UiAssets assets, PixelBlender blender, bool mailbox)
    {
        var controlName = mailbox ? "MailList" : "ArticleList";
        if (!TryBuildScrolledControl(
                controlName,
                content => new BulletinListPane(in content, blender, assets, replacesDuplicates: !mailbox)
                {
                    IsMailbox = mailbox,
                    MultiSelection = !mailbox && IsPrivileged ? new IndexSelection() : null,
                },
                out var list,
                out var bar))
        {
            return;
        }

        list.SelectionChanged = UpdateActionControls;
        _list = list;
        _bar = bar;
    }
}
