using DaScreenshotStudio.Assets.Archives;
using DaScreenshotStudio.Assets.Imaging;
using DaScreenshotStudio.Engine.App;
using DaScreenshotStudio.Engine.Core;
using DaScreenshotStudio.Engine.Records;
using DaScreenshotStudio.Engine.Rendering;
using DaScreenshotStudio.Engine.Text;
using DaScreenshotStudio.Engine.World;

namespace DaScreenshotStudio.Engine.Ui;

public sealed class NpcDialogPane : Pane
{
    public const string MessageLayoutName = "lnpcd.txt";

    public const string IllustrationArchivePath = "npc/npcbase.dat";

    public static PixelRect MessageBarRect(UiAssets assets)
    {
        ArgumentNullException.ThrowIfNull(assets);

        return GuiLayoutGeometry.TryRect(assets.Layout(MessageLayoutName), "MessageDialog", out var rect)
            ? rect
            : PixelRect.FromEdges(0, 372, GuiBackPane.ScreenWidth, GuiBackPane.ScreenHeight);
    }

    public const int ScrollUpAction = 2;

    public const int ScrollDownAction = 3;

    public const uint ArrowBlinkIntervalMs = 500;

    public const int PreviousAction = 4;

    public const int TopAction = 4;

    public const int NextAction = 5;

    public const int MerchantCloseAction = 5;

    public const int CloseAction = 6;

    public const int DisabledFrame = 2;

    public const byte FocusOutlineIndex = ImageButtonPane.FocusOutlineIndex;

    public const byte NameColor = 0x80;

    public const byte ContentColor = TextPalette.InterfaceIndex;

    public const int ContentLineHeight = 0xF;

    public const int TextInset = 2;

    public const int ItemSpriteLimit = 0xBFFF;

    public const int CreatureSpriteLimit = 0x7FFF;

    public const Direction SpeakerFacing = Direction.Right;

    public const long IllustrationBudgetBytes = 8L * 1024 * 1024;

    private readonly UiAssets _assets;
    private readonly PixelBlender _blender;
    private readonly TextBlock _name;
    private readonly TextBlock _content;

    private readonly BoundedCache<string, Illustration> _illustrations = new(
        BoundedCache<string, Illustration>.DefaultMaxEntries, IllustrationBudgetBytes);

    private DataArchive? _illustrationArchive;
    private ClientWorldArt? _art;
    private Illustration _illustration;
    private PixelRect _messageRect;
    private PixelRect _tileRect;
    private PixelRect _nameRect;
    private PixelRect _textRect;
    private Pixmap _messageArt;
    private Pixmap _tileArt;
    private Pixmap _prevArt;
    private Pixmap _nextArt;
    private Pixmap _prevDimArt;
    private Pixmap _nextDimArt;
    private Pixmap _topArt;
    private Pixmap _closeArt;
    private Pixmap _upArt;
    private Pixmap _downArt;
    private Pixmap _upBlinkArt;
    private Pixmap _downBlinkArt;
    private bool _blinkPhase;
    private bool _blinkArmed;
    private uint _blinkDue;
    private int _scroll;
    private int _speakerSprite;
    private byte _speakerColor;

    public NpcDialogPane(PixelBlender blender, UiAssets assets, ClientDirectory? clientDirectory)
        : base(new PixelRect(0, 0, GuiBackPane.ScreenWidth, GuiBackPane.ScreenHeight), blender)
    {
        ArgumentNullException.ThrowIfNull(blender);
        ArgumentNullException.ThrowIfNull(assets);

        _blender = blender;
        _assets = assets;
        TargetBlend = BlendMode.Transparent;
        Visible = false;

        ConsumesPointerAnywhere = true;

        _name = new TextBlock(assets.Font, assets.Palette, 1, 1)
        {
            DefaultPaletteIndex = NameColor,
        };

        _content = new TextBlock(assets.Font, assets.Palette, 1, 4)
        {
            DefaultPaletteIndex = ContentColor,
            LineHeight = ContentLineHeight,
        };

        Menu = new NpcMenuDialogPane(blender, assets);
        Add(Menu);

        Shop = new NpcShopPane(blender, assets);
        Add(Shop);

        ScreenFade = new AlphaScreenPane(
            GuiLayoutGeometry.TryRect(assets.Layout(MessageLayoutName), AlphaScreenPane.ControlName, out var fade)
                ? fade
                : PixelRect.FromEdges(0, 254, GuiBackPane.ScreenWidth, 372),
            blender,
            assets.Palette[AlphaScreenPane.ColorIndex]);

        _illustrationArchive = clientDirectory?.TryOpenArchive(IllustrationArchivePath);

        ApplyLayout();
    }

    public ClientWorldArt? Art
    {
        get => _art;
        set
        {
            _art = value;
            Invalidate();
        }
    }

    public bool HasPrevious { get; private set; }

    public bool HasNext { get; private set; }

    public bool NextEnabled => HasNext && !HasAnswerPane;

    public bool PreviousEnabled => HasPrevious;

    public bool HasAnswerPane => Menu.Form != NpcMenuForm.None;

    public int DefaultAction => HasAnswerPane ? -1 : HasNext ? NextAction : CloseAction;

    public bool IsScreenMenu { get; private set; }

    public NpcMenuDialogPane Menu { get; }

    public void Tick(uint now) => TickArrowBlink(now);

    private void TickArrowBlink(uint now)
    {
        if (!Visible || !(CanScrollUp || CanScrollDown))
        {
            _blinkArmed = false;
            if (_blinkPhase)
            {
                _blinkPhase = false;
                Invalidate();
            }

            return;
        }

        if (!_blinkArmed)
        {
            _blinkArmed = true;
            _blinkPhase = false;
            _blinkDue = now + ArrowBlinkIntervalMs;
            return;
        }

        if ((int)(now - _blinkDue) < 0)
        {
            return;
        }

        _blinkPhase = !_blinkPhase;
        _blinkDue = now + ArrowBlinkIntervalMs;
        Invalidate();
    }

    public NpcShopPane Shop { get; }

    public AlphaScreenPane ScreenFade { get; }

    public void Show(in NpcMessage message)
    {
        if (message.IsScreenMenu != IsScreenMenu || !Visible)
        {
            _focusedAction = -1;
        }

        IsScreenMenu = message.IsScreenMenu;
        HasPrevious = !message.IsScreenMenu && message.HasPrevious;
        HasNext = !message.IsScreenMenu && message.HasNext;

        _name.SetText(message.SpeakerName);
        SetContent(message.Content);
        _speakerSprite = message.SpeakerSprite;
        _speakerColor = message.SpeakerColor;
        SelectIllustration(message.SpeakerName, message.Illustration);
        Menu.Close();
        Shop.Close();

        ScreenFade.Visible = true;
        Visible = true;
        Invalidate();
    }

    public void Close()
    {
        if (!Visible)
        {
            return;
        }

        Visible = false;
        ScreenFade.Visible = false;
        Menu.Close();
        Shop.Close();
        Invalidate();
    }

    protected override bool OnPointer(in PointerEvent pointer)
    {
        if (!Visible)
        {
            return false;
        }

        if (pointer.Type == UiEventType.LeftDown && FocusableActionAt(pointer.X, pointer.Y) is var pressed
            && pressed >= 0 && pressed != _focusedAction)
        {
            _focusedAction = pressed;
            Invalidate();
        }

        switch (pointer.Type)
        {
            case UiEventType.LeftUp when CanScrollUp && Hit(in _upRect, pointer.X, pointer.Y):
                ScrollContent(-1);
                return true;

            case UiEventType.LeftUp when CanScrollDown && Hit(in _downRect, pointer.X, pointer.Y):
                ScrollContent(1);
                return true;

            case UiEventType.LeftUp when FocusableActionAt(pointer.X, pointer.Y) >= 0:
                return true;

            case UiEventType.LeftDown or UiEventType.LeftUp when _messageRect.Contains(pointer.X, pointer.Y):
                return true;

            default:
                return false;
        }
    }

    protected override bool OnKey(in KeyEvent key)
    {
        if (!Visible || key.Type != UiEventType.KeyDown)
        {
            return false;
        }

        if (key.Key == UiKeys.Escape)
        {
            return true;
        }

        return key.Key is UiKeys.Enter or UiKeys.Space && (IsScreenMenu || DefaultAction >= 0);
    }

    protected override void OnDrawContent(Canvas canvas)
    {
        canvas.Clear();

        if (!Visible)
        {
            return;
        }

        DrawIllustration(canvas);

        var message = _messageArt;
        if (!message.IsEmpty)
        {
            canvas.BlitPixmap(_messageRect.X, _messageRect.Y, in message, BlendMode.Transparent);
        }
        else
        {
            canvas.FillRect(in _messageRect, _assets.Palette[0x1C], BlendMode.Copy);
        }

        _name.DrawIn(canvas, in _nameRect);

        canvas.SetClip(_textRect);
        _content.DrawIn(canvas, new PixelRect(_textRect.X, ContentTop, _textRect.Width, _textRect.Height));
        canvas.ResetClip();

        DrawActions(canvas);
        DrawFocusOutline(canvas);
    }

    private PixelRect _prevRect;
    private PixelRect _nextRect;
    private PixelRect _topRect;
    private PixelRect _closeRect;
    private PixelRect _upRect;
    private PixelRect _downRect;

    private int _focusedAction = -1;

    private static bool Hit(in PixelRect rect, int x, int y) => rect.Contains(x, y);

    private int FocusableActionAt(int x, int y)
    {
        if (CanScrollUp && Hit(in _upRect, x, y))
        {
            return ScrollUpAction;
        }

        if (CanScrollDown && Hit(in _downRect, x, y))
        {
            return ScrollDownAction;
        }

        if (Hit(in _closeRect, x, y))
        {
            return IsScreenMenu ? MerchantCloseAction : CloseAction;
        }

        if (IsScreenMenu)
        {
            return Hit(in _topRect, x, y) ? TopAction : -1;
        }

        if (NextEnabled && Hit(in _nextRect, x, y))
        {
            return NextAction;
        }

        return PreviousEnabled && Hit(in _prevRect, x, y) ? PreviousAction : -1;
    }

    private bool ActionEnabled(int action, out PixelRect rect)
    {
        switch (action)
        {
            case ScrollUpAction:
                rect = _upRect;
                return CanScrollUp;

            case ScrollDownAction:
                rect = _downRect;
                return CanScrollDown;

            default:
                break;
        }

        if (IsScreenMenu)
        {
            rect = action == TopAction ? _topRect : _closeRect;
            return action is TopAction or MerchantCloseAction;
        }

        switch (action)
        {
            case PreviousAction:
                rect = _prevRect;
                return HasPrevious;

            case NextAction:
                rect = _nextRect;
                return NextEnabled;

            case CloseAction:
                rect = _closeRect;
                return true;

            default:
                rect = PixelRect.Empty;
                return false;
        }
    }

    public int ContentScrollMax
    {
        get
        {
            var lineHeight = _content.LineHeight;
            var overflow = (_content.LineCount * lineHeight) - _textRect.Height;
            return overflow <= 0 || lineHeight <= 0 ? 0 : ((overflow - 1) / lineHeight) + 1;
        }
    }

    public bool CanScrollUp => _scroll > 0;

    public bool CanScrollDown => _scroll < ContentScrollMax;

    public int ContentTop => TextLayout.Top(
        _textRect.Y,
        _textRect.Bottom,
        _content.LineCount * _content.LineHeight,
        _scroll * _content.LineHeight);

    private void SetContent(scoped ReadOnlySpan<byte> content)
    {
        _content.MaxLines = Math.Max(1, content.Length);
        _content.SetText(content);
        _scroll = 0;

        _blinkArmed = false;
        _blinkPhase = false;
    }

    private void ScrollContent(int lines)
    {
        var next = Math.Clamp(_scroll + lines, 0, ContentScrollMax);
        if (next == _scroll)
        {
            return;
        }

        _scroll = next;
        Invalidate();
    }

    private void ApplyLayout()
    {
        var layout = _assets.Layout(MessageLayoutName);

        _messageRect = MessageBarRect(_assets);

        _tileRect = GuiLayoutGeometry.TryRect(layout, "NPCTile", out var tile) ? tile : PixelRect.Empty;
        _nameRect = Inset(GuiLayoutGeometry.TryRect(layout, "Name", out var name) ? name : PixelRect.Empty);
        _textRect = Inset(GuiLayoutGeometry.TryRect(layout, "Text", out var text) ? text : PixelRect.Empty);

        _ = GuiLayoutGeometry.TryRect(layout, "PrevBtn", out _prevRect);
        _ = GuiLayoutGeometry.TryRect(layout, "NextBtn", out _nextRect);
        _ = GuiLayoutGeometry.TryRect(layout, "TopBtn", out _topRect);
        _ = GuiLayoutGeometry.TryRect(layout, "CloseBtn", out _closeRect);
        _ = GuiLayoutGeometry.TryRect(layout, "UpArrow", out _upRect);
        _ = GuiLayoutGeometry.TryRect(layout, "DownArrow", out _downRect);

        _upArt = ArtFor(layout, "UpArrow");
        _downArt = ArtFor(layout, "DownArrow");

        _upBlinkArt = ArtFor(layout, "UpArrow", 1);
        _downBlinkArt = ArtFor(layout, "DownArrow", 1);

        _prevArt = ArtFor(layout, "PrevBtn");
        _nextArt = ArtFor(layout, "NextBtn");
        _topArt = ArtFor(layout, "TopBtn");
        _closeArt = ArtFor(layout, "CloseBtn");

        _prevDimArt = ArtFor(layout, "PrevBtn", DisabledFrame);
        _nextDimArt = ArtFor(layout, "NextBtn", DisabledFrame);

        if (layout is not null && layout.TryFind("MessageDialog", out var messageControl) && messageControl.Images.Count > 0)
        {
            _messageArt = _assets.Frame(messageControl.Images[0].EntryName, messageControl.Images[0].Frame);
        }

        if (layout is not null && layout.TryFind("NPCTile", out var tileControl) && tileControl.Images.Count > 0)
        {
            _tileArt = _assets.Frame(tileControl.Images[0].EntryName, tileControl.Images[0].Frame);
        }

        _name.MaxWidth = Math.Max(1, _nameRect.Width);
        _name.MaxLines = Math.Max(1, _nameRect.Height / _name.LineHeight);
        _content.MaxWidth = Math.Max(1, _textRect.Width);
    }

    private void SelectIllustration(scoped ReadOnlySpan<byte> speakerName, byte index)
    {
        _illustration = default;

        if (_illustrationArchive is null || speakerName.IsEmpty)
        {
            return;
        }

        Span<char> name = stackalloc char[Math.Min(speakerName.Length, 128)];
        for (var i = 0; i < name.Length; i++)
        {
            name[i] = (char)speakerName[i];
        }

        if (_assets.NpcIllustrations.Find(name, index) is not { } entry)
        {
            return;
        }

        if (_illustrations.TryTouch(entry, out var cached))
        {
            _illustration = cached;
            return;
        }

        _illustration = Decode(entry);
        _illustrations.Store(
            entry,
            _illustration,
            (long)_illustration.Image.Width * _illustration.Image.Height * sizeof(ushort));
    }

    private Illustration Decode(string entry)
    {
        if (_illustrationArchive is null || !_illustrationArchive.TryReadMemory(entry, out var data))
        {
            return default;
        }

        SpfImage image;
        try
        {
            image = SpfImage.Parse(data);
        }
        catch (InvalidDataException)
        {
            return default;
        }

        if (image.FrameCount == 0)
        {
            return default;
        }

        var frame = image[0];
        var pixels = image.GetPixels(0);
        var colors = _blender.Packing.Format == RenderPixelFormat.Rgb555
            ? image.Rgb555Palette
            : image.Rgb565Palette;

        if (frame.IsEmpty || pixels.IsEmpty || colors.IsEmpty)
        {
            return default;
        }

        var width = frame.Width;
        var height = frame.Height;
        var decoded = new ushort[width * height];

        for (var y = 0; y < height; y++)
        {
            var start = y * frame.Pitch;
            if (start + width > pixels.Length)
            {
                break;
            }

            var row = pixels.Slice(start, width);
            for (var x = 0; x < width; x++)
            {
                int index = row[x];
                if (index != 0)
                {
                    decoded[(y * width) + x] = colors[index];
                }
            }
        }

        return new Illustration(new Pixmap(decoded, width, height), frame.Left, frame.Top, frame.Bottom);
    }

    private readonly record struct Illustration(Pixmap Image, int Left, int Top, int Bottom);

    private static PixelRect Inset(in PixelRect rect) => rect.IsEmpty
        ? rect
        : new PixelRect(
            rect.X + TextInset,
            rect.Y + TextInset,
            Math.Max(1, rect.Width - (TextInset * 2)),
            Math.Max(1, rect.Height - (TextInset * 2)));

    private void DrawIllustration(Canvas canvas)
    {
        var illustration = _illustration;
        if (!illustration.Image.IsEmpty)
        {
            var image = illustration.Image;
            canvas.BlitPixmap(
                illustration.Left,
                _messageRect.Y - illustration.Bottom + illustration.Top,
                in image,
                BlendMode.Transparent);
            return;
        }

        var tile = _tileArt;
        if (!tile.IsEmpty && !_tileRect.IsEmpty)
        {
            canvas.BlitPixmap(_tileRect.X, _tileRect.Y, in tile, BlendMode.Transparent);
        }

        DrawSpeakerSprite(canvas);
    }

    private void DrawSpeakerSprite(Canvas canvas)
    {
        if (_tileRect.IsEmpty)
        {
            return;
        }

        if (_speakerSprite == 0
            || (_speakerSprite >= TaggedSprite.CreatureTag && _speakerSprite < CreatureSpriteLimit))
        {
            DrawSpeakerCreature(canvas);
            return;
        }

        DrawSpeakerIcon(canvas);
    }

    private void DrawSpeakerCreature(Canvas canvas)
    {
        if (_art is null)
        {
            return;
        }

        var sprite = _speakerSprite == 0 ? 0 : _speakerSprite - TaggedSprite.CreatureTag;
        if (!_art.TryPickCreatureStanding(sprite, 0, out var segment)
            || !_art.TryGetCreatureStanding(
                sprite, 0, SpeakerFacing, in segment, 0, CreatureColoring.None, out var frame)
            || frame.Art.IsEmpty)
        {
            return;
        }

        Center(canvas, frame.Art);
    }

    private void DrawSpeakerIcon(Canvas canvas)
    {
        if (_speakerSprite <= IconArt.ItemTag || _speakerSprite >= ItemSpriteLimit
            || _assets.ItemIcons is not { } icons)
        {
            return;
        }

        var frame = icons.Frame(_speakerSprite, _speakerColor);
        if (frame.Image.IsEmpty)
        {
            return;
        }

        Center(canvas, frame.Image);
    }

    private void Center(Canvas canvas, in Pixmap image)
    {
        var previous = canvas.Clip;
        canvas.SetClip(_tileRect);
        canvas.BlitPixmap(
            _tileRect.X + ((_tileRect.Width - image.Width) / 2),
            _tileRect.Y + ((_tileRect.Height - image.Height) / 2),
            in image,
            BlendMode.Transparent);
        canvas.SetClip(in previous);
    }

    private Pixmap ArtFor(Assets.Gui.GuiLayout? layout, string controlName, int image = 0) =>
        layout is not null && layout.TryFind(controlName, out var control) && control.Images.Count > image
            ? _assets.Frame(control.Images[image].EntryName, control.Images[image].Frame)
            : default;

    private void DrawActions(Canvas canvas)
    {
        if (CanScrollUp)
        {
            DrawAction(canvas, in _upRect, _blinkPhase ? _upBlinkArt : _upArt);
        }

        if (CanScrollDown)
        {
            DrawAction(canvas, in _downRect, _blinkPhase ? _downBlinkArt : _downArt);
        }

        DrawAction(canvas, in _closeRect, _closeArt);

        if (IsScreenMenu)
        {
            DrawAction(canvas, in _topRect, _topArt);
            return;
        }

        DrawAction(canvas, in _nextRect, NextEnabled ? _nextArt : _nextDimArt);
        DrawAction(canvas, in _prevRect, HasPrevious ? _prevArt : _prevDimArt);
    }

    private void DrawFocusOutline(Canvas canvas)
    {
        if (_focusedAction is ScrollUpAction or ScrollDownAction)
        {
            return;
        }

        if (_focusedAction < 0 || !ActionEnabled(_focusedAction, out var rect) || rect.IsEmpty)
        {
            return;
        }

        canvas.SetDrawColor(_assets.Palette[FocusOutlineIndex]);
        canvas.DrawRectOutline(in rect);
    }

    private void DrawAction(Canvas canvas, in PixelRect rect, Pixmap art)
    {
        if (rect.IsEmpty)
        {
            return;
        }

        if (art.IsEmpty)
        {
            canvas.SetDrawColor(_assets.Palette[0x18]);
            canvas.DrawRectOutline(in rect);
            return;
        }

        canvas.BlitPixmap(rect.X, rect.Y, in art, BlendMode.Transparent);
    }

    public void CloseArchive()
    {
        _illustrationArchive?.Dispose();
        _illustrationArchive = null;
    }
}
