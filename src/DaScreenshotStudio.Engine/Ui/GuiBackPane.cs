using DaScreenshotStudio.Assets.Gui;
using DaScreenshotStudio.Engine.Rendering;
using DaScreenshotStudio.Engine.Text;
using DaScreenshotStudio.Engine.World;
using DaScreenshotStudio.Engine.Core;

namespace DaScreenshotStudio.Engine.Ui;

public enum GuiLayoutIndex
{
    Compact = 0,

    Large = 1,
}

internal enum LowerPanel
{
    Chat,
    Tray,
    Sheet,
}

public sealed partial class GuiBackPane : Pane
{
    public const string CompactLayoutName = "_nbk_s.txt";

    public const string LargeLayoutName = "_nbk_l.txt";

    private readonly UiAssets _assets;
    private readonly PixelBlender _blender;
    private readonly PlayerModel _player;
    private readonly LevelPointFlash _levelPoints;
    private readonly DescriptionPane _tooltip;
    private uint _now;
    private Pixmap _background;

    public GuiBackPane(
        PixelBlender blender,
        UiAssets assets,
        PlayerModel player,
        LevelPointFlash levelPoints,
        DescriptionPane description)
        : base(new PixelRect(0, 0, ScreenWidth, ScreenHeight), blender)
    {
        ArgumentNullException.ThrowIfNull(blender);
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(levelPoints);
        ArgumentNullException.ThrowIfNull(description);

        _tooltip = description;

        _blender = blender;
        _assets = assets;
        _player = player;
        _levelPoints = levelPoints;
        TargetBlend = BlendMode.Transparent;

        ChatLog = new TextLog(200, TextLog.DefaultMaxBytes);
        History = new TextLog(TextLog.DefaultMaxLines, TextLog.DefaultMaxBytes) { DefaultPalette = TextMarkup.MessageHistory };
        SeedChatSpacer(ChatLog);
        SeedHistorySpacers(History);

        ApplyLayout();
    }

    public const int ScreenWidth = 640;

    public const int ScreenHeight = 480;

    public GuiLayoutIndex LayoutIndex { get; private set; } = GuiLayoutIndex.Compact;

    public GuiLayout? Layout { get; private set; }

    public SpelledViewPane? Spelled { get; private set; }

    public Action<WorldViewLayout>? ViewLayoutChanged { get; set; }

    public UserState EmoteState
    {
        get => _emoteState;
        set
        {
            if (_emoteState == value)
            {
                return;
            }

            _emoteState = value;
            ApplyEmoteStateFrames();
            Emoticon?.SetSelected(value);
        }
    }

    public PixelRect MapRectangle =>
        GuiLayoutGeometry.TryRect(Layout, "MAP", out var rect) ? rect : WorldFallbackRectangle;

    public WorldViewLayout ViewLayout => new(
        MapRectangle,
        LayoutIndex == GuiLayoutIndex.Compact
            ? WorldViewLayout.CompactCenterBiasY
            : WorldViewLayout.MinimalCenterBiasY);

    private static PixelRect WorldFallbackRectangle => PixelRect.FromEdges(3, 3, 619, 311);

    public void ToggleLayout() =>
        SetLayout(LayoutIndex == GuiLayoutIndex.Compact ? GuiLayoutIndex.Large : GuiLayoutIndex.Compact);

    public void SetLayout(GuiLayoutIndex index)
    {
        if (LayoutIndex == index && Layout is not null)
        {
            return;
        }

        LayoutIndex = index;
        ApplyLayout();
    }

    public void Refresh()
    {
        Chat?.Refresh();
        RefreshChatScrollRange();
        SystemMessage?.Refresh();
        Tray?.Refresh();
        _spellHalf?.Refresh();
        Sheet?.Refresh();
    }

    public void Tick(uint now)
    {
        _now = now;

        for (var i = 0; i < RailTooltipCount; i++)
        {
            if (_iconButtons[i] is { IsChecked: true } button && (int)(now - _iconFlashUntil[i]) >= 0)
            {
                button.IsChecked = false;
            }
        }

        if (_stripPressed[ShotStripButton] && (int)(now - _shotPressUntil) >= 0)
        {
            SetStripPressed(ShotStripButton, false);
        }

        if (_hasNewMail && (int)(now - _mailFlashDue) >= 0)
        {
            _mailFlashPhase = !_mailFlashPhase;
            _mailFlashDue = now + MailFlashMilliseconds;
            ApplyMailFlash();
        }

        Refresh();
    }

    private void ApplyLayout()
    {
        var name = LayoutIndex == GuiLayoutIndex.Compact ? CompactLayoutName : LargeLayoutName;
        Layout = _assets.Layout(name);

        _placed.Clear();

        _background = BackgroundFor(name);
        PlaceStatusControls();
        PlaceButtons();
        PlaceLowerPanel();
        PlaceSelectors();

        SelectPage(IsPageExpanded);

        RetireUnplaced();
        SetChildOrder(_placed);

        ReapplyValues();
        Invalidate();
        ViewLayoutChanged?.Invoke(ViewLayout);
    }

    private void PlaceStatusControls()
    {
        _healthOrb = AddOrb("ORB_HP");
        _manaOrb = AddOrb("ORB_MP");
        _ping = AddPing();

        _id = AddLabel("SZ_ID", TextAlignment.Center);
        _hp = AddVitalLabel("NUM_HP", _healthOrb);
        _mp = AddVitalLabel("NUM_MP", _manaOrb);
        _zone = AddLabel("SZ_ZONE", TextAlignment.Center, TextPalette.ZoneIndex);
        _position = AddLabel("SZ_XY", TextAlignment.Center);
        _weightLabel = AddLabel("SZ_WEIGHT", TextAlignment.Center);
        _server = AddLabel("SZ_SERVER", TextAlignment.Center);
        _descriptionLabel = AddLabel("SZ_DESCRIPTION");

        Input = AddInput();
        Tell = AddTell();
        NumberLine = AddNumberLine();
        SpellInput = AddSpellInput();
        BlockListen = AddBlockListen();
    }

    private void PlaceButtons()
    {
        if (AddButton("BTN_CHANGELAYOUT", _ => ToggleLayout()) is { } changeLayout)
        {
            changeLayout.IsChecked = LayoutIndex != GuiLayoutIndex.Compact;
        }

        _expandButton = AddButton("BTN_EXPAND", _ => ToggleExpanded());
        if (_expandButton is not null)
        {
            _expandButton.IsChecked = IsPageExpanded;
        }

        _systemButtons[0] = AddButton("BTN_OPTION");
        _systemButtons[1] = AddButton("BTN_BULLETIN");
        _systemButtons[2] = AddButton("BTN_USERS");
        _systemButtons[3] = AddButton(EmoteButtonControl);
        ApplyEmoteStateFrames();

        for (var i = 0; i < _systemButtons.Length; i++)
        {
            if (_systemButtons[i] is { } rail)
            {
                rail.IsChecked = _systemButton == i;
            }
        }

        for (var i = 0; i < RailTooltipCount; i++)
        {
            AddIconButton(i);
        }

        _stripButtons[MailStripButton] = AddButton(MailButtonControl, _ => ActivateMailButton());
        _groupButton = AddButton(GroupButtonControl, _ => ActivateGroupButton());
        _stripButtons[GroupStripButton] = _groupButton;
        _stripButtons[ShotStripButton] = AddButton(ShotButtonControl, _ => ActivateShotButton());

        for (var i = 0; i < StripButtonCount; i++)
        {
            SetStripPressed(i, _stripPressed[i]);
        }

        ApplyMailFlash();

        ApplyGroupFrames();
    }

    private void PlaceLowerPanel()
    {
        Spelled = Managed("Spelled", () => new SpelledViewPane(_assets, _blender));

        _chatPanel = AddChatPanel();
        Tray = AddTray();
        _spellHalf = AddSpellHalf();

        _abilityRecord ??= (LayoutIndex, false);

        _slotKeys = AddSlotKeyRow();

        Chat = AddChat();

        _chatScrollBar = AddChatScrollBar();

        Sheet = AddSheet();

        SystemMessage = AddSystemMessagePane();

        if (Tray is not null)
        {
            Tray.Visible = false;
        }
    }

    private void PlaceSelectors()
    {
        _selectorStrip = AddSelectorStrip();

        for (var i = 0; i < TraySelectorCount; i++)
        {
            AddTraySelector(i);
        }

        ApplyRailTooltipRects();

        Emoticon = Managed("Emoticon", () => new EmoticonSelectPane(_assets, _blender));
    }

    private void RetireUnplaced()
    {
        for (var i = 0; i < _managed.Count; i++)
        {
            if (!_placed.Contains(_managed[i]))
            {
                _placed.Add(_managed[i]);
                _retired.Add(_managed[i]);
                _managed[i].Visible = false;
                _managed[i].SetBounds(new PixelRect(_managed[i].Bounds.X, _managed[i].Bounds.Y, 0, 0));
            }
        }
    }

    protected override void OnDrawContent(Canvas canvas)
    {
        canvas.Clear();

        if (!_background.IsEmpty)
        {
            canvas.BlitPixmap(0, 0, in _background, BlendMode.Transparent);
        }
    }
}
