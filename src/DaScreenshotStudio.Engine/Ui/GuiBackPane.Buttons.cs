using DaScreenshotStudio.Engine.Rendering;
using DaScreenshotStudio.Engine.Core;
using DaScreenshotStudio.Engine.Records;

namespace DaScreenshotStudio.Engine.Ui;

public sealed partial class GuiBackPane
{
    private readonly PixelRect[] _tooltipRects = new PixelRect[RailTooltipCount];

    private readonly ImageButtonPane?[] _iconButtons = new ImageButtonPane[RailTooltipCount];

    private readonly uint[] _iconFlashUntil = new uint[RailTooltipCount];

    private readonly ImageButtonPane?[] _stripButtons = new ImageButtonPane[StripButtonCount];

    private readonly bool[] _stripPressed = new bool[StripButtonCount];

    private uint _shotPressUntil;

    private bool _hasNewMail;

    private bool _mailFlashPhase;

    private uint _mailFlashDue;

    public const int RailTooltipCount = 6;

    public const string ScreenshotControl = "BTN_SCREENSHOT";

    public const string TownMapControl = "BTN_TOWNMAP";

    public const string HotKeyControl = "BTN_HELP";

    public const string GroupViewControl = "BTN_GROUP";

    public const string LegendControl = "BTN_LEGEND";

    public const string GameSettingControl = "BTN_SETTING";

    public const int RailTooltipOffsetY = 0x0C;

    public const int HoverLabelOffsetY = 0x0C;

    public const uint IconFlashMilliseconds = 0xC8;

    public const uint MailFlashMilliseconds = 0x1F4;

    public const int MailFlashColorIndex = 0x97;

    public void ApplyMailState(in StatusUpdate status)
    {
        var valid = status.Has(StatusFields.Modifiers) && status.Has(StatusFields.MailState);
        SetNewMail(valid && status.Modifiers.HasHighMailNibble);
    }

    public void SetNewMail(bool hasNewMail)
    {
        if (_hasNewMail == hasNewMail)
        {
            return;
        }

        _hasNewMail = hasNewMail;

        if (hasNewMail)
        {
            _mailFlashDue = _now + MailFlashMilliseconds;
        }
        else
        {
            _mailFlashPhase = false;
        }

        ApplyMailFlash();
    }

    private void ApplyMailFlash()
    {
        if (_stripButtons[MailStripButton] is { } button)
        {
            button.Palette = _assets.Palette;
            button.FlashOutlineIndex = _mailFlashPhase ? MailFlashColorIndex : -1;
        }
    }

    public static readonly (string Control, int Message)[] RailTooltips =
    [
        (HotKeyControl, 0x57),
        (LegendControl, 0x7D),
        (ScreenshotControl, 0x7E),
        (GameSettingControl, 0x7F),
        (TownMapControl, 0x58),
        (GroupViewControl, 0x59),
    ];

    public EmoticonSelectPane? Emoticon { get; private set; }

    public const string EmoteButtonControl = "BTN_EMOT";

    public const int EmoteButton = 3;

    public const int EmoteStateFrames = 3;

    public void SetSystemButton(int index)
    {
        _systemButton = index;

        for (var i = 0; i < _systemButtons.Length; i++)
        {
            if (_systemButtons[i] is { } rail)
            {
                rail.IsChecked = i == index;
            }
        }
    }

    public const string MailButtonControl = "CMail";

    public const string GroupButtonControl = "CGroup";

    public const string GroupClosedButtonControl = "CGroup0";

    public const string ShotButtonControl = "CShot";

    private ImageButtonPane? _groupButton;

    public const int MailStripButton = 0;

    public const int GroupStripButton = 1;

    public const int ShotStripButton = 2;

    public const int StripButtonCount = 3;

    public const uint ShotPressMilliseconds = 0xC8;

    private void SetStripPressed(int index, bool pressed)
    {
        _stripPressed[index] = pressed;

        if (_stripButtons[index] is { } button)
        {
            button.IsChecked = pressed;
        }
    }

    public void ActivateMailButton()
    {
        if (_stripPressed[MailStripButton])
        {
            return;
        }

        SetStripPressed(MailStripButton, true);
    }

    public void ReleaseMailButton() => SetStripPressed(MailStripButton, false);

    public void ActivateGroupButton() => SetStripPressed(GroupStripButton, true);

    public void ActivateShotButton()
    {
        SetStripPressed(ShotStripButton, true);
        _shotPressUntil = _now + ShotPressMilliseconds;
    }

    private bool _groupOpen = true;

    public bool IsGroupOpen => _groupOpen;

    public void SetGroupOpen(bool open)
    {
        SetStripPressed(GroupStripButton, false);

        if (_groupOpen == open)
        {
            return;
        }

        _groupOpen = open;
        ApplyGroupFrames();
    }

    private void ApplyGroupFrames()
    {
        var name = _groupOpen ? GroupButtonControl : GroupClosedButtonControl;

        if (_groupButton is not { } button || Layout is null || !Layout.TryFind(name, out var control))
        {
            return;
        }

        button.SetFrames(
            _assets.Frame(control, 0),
            _assets.Frame(control, 1),
            _assets.Frame(control, 2));
    }

    private readonly ImageButtonPane?[] _systemButtons = new ImageButtonPane?[4];

    private int _systemButton = -1;

    private UserState _emoteState;

    private void ApplyEmoteStateFrames()
    {
        if (_systemButtons[EmoteButton] is not { } button
            || Layout is not { } layout
            || !layout.TryFind(EmoteButtonControl, out var control)
            || control.Images.Count == 0)
        {
            return;
        }

        var entry = control.Images[0].EntryName;
        var first = (int)_emoteState * EmoteStateFrames;

        button.SetFrames(
            _assets.LayoutFrame(entry, first),
            _assets.LayoutFrame(entry, first + 1),
            _assets.LayoutFrame(entry, first + 2));
    }

    private void ActivateIcon(int index)
    {
        if (_iconButtons[index] is not { IsChecked: false } button)
        {
            return;
        }

        button.IsChecked = true;
        _iconFlashUntil[index] = _now + IconFlashMilliseconds;
    }

    private void ApplyRailTooltipRects()
    {
        Array.Clear(_tooltipRects);

        for (var i = 0; i < RailTooltipCount; i++)
        {
            if (GuiLayoutGeometry.TryRect(Layout, RailTooltips[i].Control, out var rect))
            {
                _tooltipRects[i] = rect;
            }
        }
    }

    protected override bool OnPointer(in PointerEvent pointer)
    {
        if (pointer.Type != UiEventType.PointerMove)
        {
            return !MapRectangle.Contains(pointer.X, pointer.Y);
        }

        for (var i = 0; i < RailTooltipCount; i++)
        {
            if (!_tooltipRects[i].Contains(pointer.X, pointer.Y))
            {
                continue;
            }

            _tooltip.Show(
                this,
                this,
                _tooltipRects[i].X,
                _tooltipRects[i].Y - RailTooltipOffsetY,
                _assets.Messages.Get(RailTooltips[i].Message));

            return false;
        }

        _tooltip.Hide(this);
        return false;
    }
}
