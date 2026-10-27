using DaScreenshotStudio.Engine.Rendering;

namespace DaScreenshotStudio.Engine.Ui;

public enum BulletinView
{
    BoardList,
    ArticleList,
    Article,
    MailList,
    Mail,
    NewArticle,
    NewMail,
}

public sealed class BulletinSession : Pane
{
    public const int HistoryLimit = 10;

    public const int NoCurrentIndex = -1;

    public static readonly PixelRect ScreenBounds = new(0, 0, 0x280, 0x1E0);

    private readonly BulletinDialogPane?[] _history = new BulletinDialogPane?[HistoryLimit];

    public BulletinSession(PixelBlender blender)
        : base(ScreenBounds, blender)
    {
        CurrentIndex = NoCurrentIndex;

        ConsumesPointerAnywhere = true;
    }

    public int Count { get; private set; }

    public int CurrentIndex { get; private set; }

    public ushort SectionId { get; set; }

    protected override bool OnKey(in KeyEvent key) => true;

    protected override void OnComposite(Canvas target, int absoluteX, int absoluteY)
    {
    }

    public BulletinDialogPane? Current => Dialog(CurrentIndex);

    public Action? Closed { get; set; }

    public BulletinDialogPane? Dialog(int index) =>
        (uint)index < (uint)HistoryLimit ? _history[index] : null;

    public void Push(BulletinDialogPane dialog)
    {
        ArgumentNullException.ThrowIfNull(dialog);

        for (var i = CurrentIndex + 1; i < Count; i++)
        {
            if (_history[i] is { } stale)
            {
                Discard(stale);
                _history[i] = null;
            }
        }

        Count = CurrentIndex + 1;

        var outgoing = Current;
        if (outgoing is not null)
        {
            outgoing.Visible = false;
        }

        if (outgoing is { ReplacedOnPush: true })
        {
            Discard(outgoing);
            _history[CurrentIndex] = dialog;
        }
        else
        {
            if (Count == HistoryLimit)
            {
                Discard(dialog);
                return;
            }

            CurrentIndex++;
            Count++;
            _history[CurrentIndex] = dialog;
        }

        dialog.Session = this;
        dialog.Visible = true;
        dialog.OnActivated();
    }

    public void GoBack()
    {
        if (Current is not { } current || CurrentIndex == 0)
        {
            Close();
            return;
        }

        current.Visible = false;
        CurrentIndex--;

        if (Current is { } previous)
        {
            previous.Visible = true;
            previous.OnActivated();
        }
    }

    public void Close()
    {
        for (var i = 0; i < _history.Length; i++)
        {
            if (_history[i] is { } dialog)
            {
                dialog.Visible = false;
                Discard(dialog);
                _history[i] = null;
            }
        }

        Count = 0;
        CurrentIndex = NoCurrentIndex;
        Closed?.Invoke();
    }

    private static void Discard(BulletinDialogPane dialog) => dialog.Session = null;
}
