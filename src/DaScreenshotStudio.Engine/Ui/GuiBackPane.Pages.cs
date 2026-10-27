using DaScreenshotStudio.Engine.Rendering;
using DaScreenshotStudio.Engine.Records;

namespace DaScreenshotStudio.Engine.Ui;

public sealed partial class GuiBackPane
{
    private readonly ImageButtonPane?[] _traySelectors = new ImageButtonPane[TraySelectorCount];

    private readonly PixelRect[] _selectorHome = new PixelRect[TraySelectorCount];

    private (GuiLayoutIndex Layout, bool Expanded)? _abilityRecord;

    private readonly int[] _pageStamps = new int[10];

    private int _pageStampSerial;

    private ImagePane? _selectorStrip;

    private ImagePane? _chatPanel;

    private ImageButtonPane? _expandButton;

    private SlotTrayPane? _spellHalf;

    private SlotKeyRowPane? _slotKeys;

    private ScrollBarPane? _chatScrollBar;

    private LowerPanel _panel = LowerPanel.Sheet;

    public const int TraySelectorCount = 6;

    public SlotTrayPane? Tray { get; private set; }

    public CharacterSheetPane? Sheet { get; private set; }

    public SlotTrayPane? GeneralSpellHalf => _spellHalf;

    public SlotKeyRowPane? SlotKeyRow => _slotKeys;

    public ScrollBarPane? ChatScrollBar => _chatScrollBar;

    public TrayMode TrayMode
    {
        get => Tray?.Mode ?? TrayMode.Items;
        set
        {
            if (Tray is null)
            {
                return;
            }

            Tray.Mode = value;
            ShowTray();
        }
    }

    public const int ItemsSelector = 0;

    public const int SkillsSelector = 1;

    public const int SpellsSelector = 2;

    public const int ChatSelector = 3;

    public const int StatusSelector = 4;

    public const int SixthSelector = 5;

    public bool SkillSpellSelectByToggle { get; set; }

    public const string ExpandedInventoryControl = "InventoryBackgroundExpanded";

    public const string ExpandedAbilityControl = "SkillSpellBackgroundExpanded";

    public const string GeneralControl = "LivingInventoryBackground";

    public const string ExpandedGeneralControl = "LivingInventoryBackgroundExpanded";

    public const string GeneralHalvesLayoutName = "lssback.txt";

    public const string GeneralSkillControl = "SkillInventoryRect";

    public const string GeneralSpellControl = "SpellInventoryRect";

    public const string ExpandedChatControl = "ChatBackgroundExpanded";

    public const string ExpandedChatTextControl = "ChattingRectExpanded";

    public const string SelectorStripControl = "BTN_INV_EXTENDED_FRAME";

    public const string SelectorStripFirstControl = "BTN_INVE0";

    public const int CompactNormalRows = 3;

    public const int MinimalNormalRows = 1;

    public const int ExpandedItemRows = 5;

    public const int ExpandedAbilityRows = 3;

    public const int MinimalNormalPadY = 7;

    public const int GridPad = 3;

    public const int InitialPage = 7;

    public const int ItemInventoryPage = 0;

    public bool IsPageExpanded { get; private set; }

    public int PageIndex => _panel switch
    {
        LowerPanel.Tray => TrayMode switch
        {
            TrayMode.SpellPage1 => 1,
            TrayMode.SpellPage2 => 2,
            TrayMode.SkillPage1 => 3,
            TrayMode.SkillPage2 => 4,
            TrayMode.Combined => 9,
            _ => 0,
        },
        LowerPanel.Chat => 5,
        LowerPanel.Sheet => Sheet is { Page: CharacterSheetPage.Extra } ? 8 : 7,
        _ => InitialPage,
    };

    public static bool AllowsExpansion(int page, GuiLayoutIndex layout) =>
        layout != GuiLayoutIndex.Compact || page is < 1 or > 9;

    public void ToggleExpanded() => SelectPage(!IsPageExpanded);

    public void ActivateSelectorFromKey(int index, InputModifiers modifiers) =>
        ActivateTraySelector(index, modifiers);

    public bool CollapseExpandedPage()
    {
        if (!IsPageExpanded)
        {
            return false;
        }

        SelectPage(false);
        return true;
    }

    private void SelectPage(bool expanded)
    {
        if (!AllowsExpansion(PageIndex, LayoutIndex))
        {
            expanded = false;
        }

        IsPageExpanded = expanded;

        if (_expandButton is not null)
        {
            _expandButton.IsChecked = expanded;
        }

        ApplyPanel();
        StampPage(PageIndex);
    }

    public void ShowTray()
    {
        if (Tray is null)
        {
            return;
        }

        _panel = LowerPanel.Tray;
        SelectPage(IsPageExpanded);
    }

    public void ShowItemInventory()
    {
        if (PageIndex == ItemInventoryPage)
        {
            return;
        }

        TrayMode = TrayMode.Items;
    }

    private void ApplyPanel() => ApplyPanel(IsPageExpanded);

    private void ApplyPanel(bool expanded)
    {
        var panel = _panel;

        ApplyExpansion(panel, expanded);

        if (Tray is not null)
        {
            Tray.Visible = panel == LowerPanel.Tray;
        }

        if (_spellHalf is not null)
        {
            _spellHalf.Visible = panel == LowerPanel.Tray && TrayMode == TrayMode.Combined;
        }

        if (panel == LowerPanel.Sheet)
        {
            Sheet?.Show();
        }
        else
        {
            Sheet?.Close();
        }

        if (Chat is not null)
        {
            Chat.Visible = panel == LowerPanel.Chat;

            if (Chat.Visible)
            {
                Chat.Invalidate();
            }
        }

        Check(panel switch
        {
            LowerPanel.Tray => SelectorFor(TrayMode),
            LowerPanel.Sheet => StatusSelector,
            _ => ChatSelector,
        });

        ApplyHistoryVisibility(expanded);
    }

    private void ApplyHistoryVisibility(bool expanded)
    {
        if (SystemMessage is { } history)
        {
            history.Visible = !(expanded && PageIndex == ItemInventoryPage);
        }
    }

    private void ApplyExpansion(LowerPanel panel, bool expanded)
    {
        var expandedPanel = PixelRect.Empty;

        var trayPanel = PixelRect.Empty;

        if (Tray is { } tray)
        {
            if (expanded && panel == LowerPanel.Tray
                && TryExpandedPanel(ExpandedTrayControl, out var rect, out var art))
            {
                tray.SetBounds(in rect);
                tray.Panel = art;
                tray.Rows = TrayMode == TrayMode.Items ? ExpandedItemRows : ExpandedAbilityRows;
                tray.PadX = GridPad;
                tray.PadY = GridPad;
                expandedPanel = rect;
                trayPanel = rect;
            }
            else
            {
                ApplyNormalTray(tray);
            }

            ApplyTrayGrid(tray, expanded);
        }

        var chatRegion = PixelRect.Empty;

        if (Chat is { } chat)
        {
            if (expanded && panel == LowerPanel.Chat
                && TryExpandedPanel(ExpandedChatControl, out var rect, out var art)
                && TryExpandedChatText(in rect, out var text))
            {
                chatRegion = text;
                chat.SetBounds(in text);

                chat.Background = default;
                expandedPanel = rect;

                if (_chatPanel is { } backdrop)
                {
                    backdrop.SetBounds(in rect);
                    backdrop.Image = art;
                    backdrop.Visible = true;
                }
            }
            else
            {
                if (GuiLayoutGeometry.TryRect(Layout, "ChattingRect", out var home))
                {
                    chatRegion = home;
                    chat.SetBounds(in home);
                }

                chat.Background = _background;

                if (_chatPanel is { } backdrop)
                {
                    backdrop.Visible = false;
                }
            }
        }

        if (Sheet is not null)
        {
            var sheetPanel = ApplySheetExpansion(expanded && panel == LowerPanel.Sheet);
            if (!sheetPanel.IsEmpty)
            {
                expandedPanel = sheetPanel;
            }
        }

        ApplySlotKeyRow(in trayPanel);
        ApplyChatScrollBar(panel, expanded, in chatRegion);

        ParkChatAtEnd();
        ApplySelectorStrip(in expandedPanel);
    }

    public static bool ShowsSlotKeyRow(int page) => page is 0 or 1 or 2 or 3 or 4 or 9;

    private void ApplySlotKeyRow(in PixelRect trayPanel)
    {
        if (_slotKeys is not { } row)
        {
            return;
        }

        row.Visible = ShowsSlotKeyRow(PageIndex);

        if (!row.Visible || !GuiLayoutGeometry.TryRect(Layout, "InventoryRect", out var grid))
        {
            return;
        }

        var padY = LayoutIndex == GuiLayoutIndex.Compact ? GridPad : MinimalNormalPadY;
        var top = trayPanel.IsEmpty ? grid.Y : trayPanel.Y;

        row.SetBounds(new PixelRect(
            grid.X + GridPad - SlotKeyRowPane.OriginInsetX,
            top + padY,
            row.Bounds.Width,
            row.Bounds.Height));
    }

    public static bool ShowsChatScrollBar(GuiLayoutIndex layout, bool expanded) =>
        layout == GuiLayoutIndex.Compact || expanded;

    private void ApplyChatScrollBar(LowerPanel panel, bool expanded, in PixelRect chatRegion)
    {
        if (_chatScrollBar is not { } bar)
        {
            return;
        }

        var shown = panel == LowerPanel.Chat
            && !chatRegion.IsEmpty
            && ShowsChatScrollBar(LayoutIndex, expanded);

        bar.Visible = shown;

        if (!shown)
        {
            return;
        }

        var thickness = bar.Thickness;
        bar.SetBounds(new PixelRect(chatRegion.Right - thickness, chatRegion.Y, thickness, chatRegion.Height));
        Chat?.SetBounds(new PixelRect(
            chatRegion.X, chatRegion.Y, chatRegion.Width - thickness, chatRegion.Height));
    }

    private void ParkChatAtEnd()
    {
        if (Chat is not { } chat)
        {
            return;
        }

        chat.ScrollToEnd();

        if (_chatScrollBar is { } bar)
        {
            bar.Position = chat.Scroll;
        }
    }

    private string ExpandedTrayControl => TrayMode switch
    {
        TrayMode.Items => ExpandedInventoryControl,
        TrayMode.Combined => ExpandedGeneralControl,
        _ => ExpandedAbilityControl,
    };

    private void ApplyNormalTray(SlotTrayPane tray)
    {
        if (GuiLayoutGeometry.TryRect(Layout, "InventoryRect", out var home))
        {
            tray.SetBounds(in home);
        }

        var control = TrayMode == TrayMode.Combined ? GeneralControl : "InventoryBackground";
        if (Layout is not null && Layout.TryFind(control, out var art) && art.Images.Count > 0)
        {
            tray.Panel = _assets.Frame(art.Images[0].EntryName, art.Images[0].Frame);
        }

        var compact = LayoutIndex == GuiLayoutIndex.Compact;
        tray.Rows = compact ? CompactNormalRows : MinimalNormalRows;
        tray.PadX = GridPad;
        tray.PadY = compact ? GridPad : MinimalNormalPadY;
    }

    private void ApplyTrayGrid(SlotTrayPane tray) => ApplyTrayGrid(tray, IsPageExpanded);

    private void ApplyTrayGrid(SlotTrayPane tray, bool expanded)
    {
        var items = TrayMode == TrayMode.Items;
        var combined = TrayMode == TrayMode.Combined;

        tray.InsetX = items ? SlotTrayPane.CellInsetX : SlotTrayPane.AbilityInsetX;
        tray.InsetY = items ? SlotTrayPane.CellInsetY : SlotTrayPane.AbilityInsetY;
        tray.ReservesLastCell = items;
        tray.Columns = combined ? SlotTrayPane.HalfColumns : SlotTrayPane.FullColumns;

        switch (TrayMode)
        {
            case TrayMode.Items:
                tray.SlotCount = 0;
                break;

            case TrayMode.Combined:
                if (!tray.KeepsGeneralGrid)
                {
                    tray.SlotCount =
                        Math.Min(tray.Rows * SlotTrayPane.HalfColumns, SlotTrayPane.GeneralSkillSlots);
                    tray.KeepsGeneralGrid = true;
                }

                break;

            default:
                tray.SlotCount =
                    Math.Min(tray.Rows * SlotTrayPane.FullColumns, SlotTrayPane.AbilityPageSlots);

                if (TrayMode is TrayMode.SkillPage1 or TrayMode.SkillPage2)
                {
                    tray.KeepsGeneralGrid = false;
                }
                else if (_spellHalf is { } spellPage)
                {
                    spellPage.KeepsGeneralGrid = false;
                }

                break;
        }

        tray.GridClip = combined
            && GuiLayoutGeometry.TryRect(
                _assets.Layout(GeneralHalvesLayoutName), GeneralSkillControl, out var skillHalf)
            ? skillHalf
            : PixelRect.Empty;

        if (combined && _spellHalf is { } half)
        {
            half.Rows = tray.Rows;
            half.PadX = tray.PadX;
            half.PadY = tray.PadY;
            half.InsetX = SlotTrayPane.AbilityInsetX;
            half.InsetY = SlotTrayPane.AbilityInsetY;
            half.Columns = SlotTrayPane.HalfColumns;

            if (!half.KeepsGeneralGrid)
            {
                half.SlotCount =
                    Math.Min(half.Rows * SlotTrayPane.HalfColumns, SlotTrayPane.GeneralSpellSlots);
                half.KeepsGeneralGrid = true;
            }

            var bounds = tray.Bounds;
            if (TryGeneralHalf(GeneralSpellControl, in bounds, out var rect))
            {
                half.SetBounds(in rect);
            }
        }

        ApplyAbilityRecord(tray, expanded);
    }

    private void ApplyAbilityRecord(SlotTrayPane tray, bool expanded)
    {
        if (PageIndex is not (1 or 2 or 3 or 4 or 9))
        {
            return;
        }

        var record = (Layout: LayoutIndex, Expanded: expanded);
        if (_abilityRecord == record)
        {
            return;
        }

        _abilityRecord = record;
        tray.SlotCount = Math.Min(tray.Rows * tray.Columns, SlotTrayPane.AbilityPageSlots);

        if (_spellHalf is { } half)
        {
            half.SlotCount = Math.Min(half.Rows * half.Columns, SlotTrayPane.AbilityPageSlots);
        }
    }

    private bool TryGeneralHalf(string controlName, in PixelRect panel, out PixelRect rect)
    {
        if (GuiLayoutGeometry.TryRect(_assets.Layout(GeneralHalvesLayoutName), controlName, out var half))
        {
            rect = new PixelRect(panel.X + half.X, panel.Y + half.Y, half.Width, half.Height);
            return true;
        }

        rect = PixelRect.Empty;
        return false;
    }

    private PixelRect ApplySheetExpansion(bool expanded)
    {
        if (Sheet is not { } sheet || !GuiLayoutGeometry.TryRect(Layout, "InventoryRect", out var home))
        {
            return PixelRect.Empty;
        }

        var compact = LayoutIndex == GuiLayoutIndex.Compact;
        var name = expanded || compact ? CharacterSheetPane.LayoutName : CharacterSheetPane.ReducedLayoutName;

        if (sheet.Layout != name)
        {
            sheet = ReplaceSheet(name, in home);
        }

        return expanded && !compact ? sheet.Bounds : PixelRect.Empty;
    }

    private bool TryExpandedPanel(string controlName, out PixelRect rect, out Pixmap art)
    {
        art = default;
        rect = PixelRect.Empty;

        if (Layout is null
            || !Layout.TryFind(controlName, out var control)
            || control.Images.Count == 0
            || !GuiLayoutGeometry.TryRect(Layout, "InventoryRect", out var panel))
        {
            return false;
        }

        art = _assets.Frame(control.Images[0].EntryName, control.Images[0].Frame);
        if (art.IsEmpty)
        {
            return false;
        }

        rect = new PixelRect(panel.X, panel.Bottom - art.Height, art.Width, art.Height);
        return true;
    }

    private bool TryExpandedChatText(in PixelRect panel, out PixelRect text)
    {
        if (GuiLayoutGeometry.TryRect(Layout, ExpandedChatControl, out var authored)
            && GuiLayoutGeometry.TryRect(Layout, ExpandedChatTextControl, out var authoredText))
        {
            text = new PixelRect(
                panel.X + authoredText.X - authored.X,
                panel.Y + authoredText.Y - authored.Y,
                authoredText.Width,
                authoredText.Height);

            return true;
        }

        text = PixelRect.Empty;
        return false;
    }

    private void ApplySelectorStrip(in PixelRect expandedPanel)
    {
        var strip = _selectorStrip;

        if (strip is not null
            && !expandedPanel.IsEmpty
            && !strip.Image.IsEmpty
            && GuiLayoutGeometry.TryRect(Layout, SelectorStripControl, out var frame)
            && GuiLayoutGeometry.TryRect(Layout, SelectorStripFirstControl, out var first)
            && !_selectorHome[0].IsEmpty)
        {
            var originX = _selectorHome[0].X - (first.X - frame.X);
            var originY = expandedPanel.Y - strip.Image.Height;

            strip.SetBounds(new PixelRect(originX, originY, strip.Image.Width, strip.Image.Height));
            strip.Visible = true;

            for (var i = 0; i < _traySelectors.Length; i++)
            {
                if (_traySelectors[i] is { } selector)
                {
                    selector.SetBounds(new PixelRect(
                        originX + (first.X - frame.X) + (_selectorHome[i].X - _selectorHome[0].X),
                        originY + (first.Y - frame.Y) + (_selectorHome[i].Y - _selectorHome[0].Y),
                        selector.Bounds.Width,
                        selector.Bounds.Height));
                }
            }

            return;
        }

        if (strip is not null)
        {
            strip.Visible = false;
        }

        for (var i = 0; i < _traySelectors.Length; i++)
        {
            if (_traySelectors[i] is { } selector && !_selectorHome[i].IsEmpty)
            {
                selector.SetBounds(new PixelRect(
                    _selectorHome[i].X,
                    _selectorHome[i].Y,
                    selector.Bounds.Width,
                    selector.Bounds.Height));
            }
        }
    }

    private static int SelectorFor(TrayMode mode) => mode switch
    {
        TrayMode.SkillPage1 or TrayMode.SkillPage2 => SkillsSelector,
        TrayMode.SpellPage1 or TrayMode.SpellPage2 => SpellsSelector,
        TrayMode.Combined => SixthSelector,
        _ => ItemsSelector,
    };

    private void Check(int index)
    {
        for (var i = 0; i < _traySelectors.Length; i++)
        {
            if (_traySelectors[i] is { } selector)
            {
                selector.IsChecked = i == index;
            }
        }
    }

    public void ShowSheet()
    {
        if (Sheet is null)
        {
            return;
        }

        _panel = LowerPanel.Sheet;
        SelectPage(IsPageExpanded);
    }

    public void ShowChat()
    {
        _panel = LowerPanel.Chat;
        SelectPage(IsPageExpanded);
    }

    public void SelectWindow(byte windowCode)
    {
        switch (windowCode)
        {
            case WindowChange.ItemsWindow:
                TrayMode = TrayMode.Items;
                break;

            case WindowChange.SkillsWindow:
                TrayMode = TrayMode.SkillPage1;
                break;

            case WindowChange.SpellsWindow:
                TrayMode = TrayMode.SpellPage1;
                break;

            case WindowChange.ChatWindow:
                ShowChat();
                break;

            case WindowChange.StatusWindow:
                ActivateStatusSelector(alternate: false);
                break;

            default:
                break;
        }
    }

    private static bool KeepsPage(bool control, int page, int first, int second) =>
        control && (page == first || page == second);

    private bool TakesSecondPage(bool alternate, int page, int first, int second) =>
        alternate
        || (SkillSpellSelectByToggle
            && (page == first || (page != second && ShownMoreRecentlyThan(second, first))));

    private bool ShownMoreRecentlyThan(int page, int other) => _pageStamps[page] > _pageStamps[other];

    private void StampPage(int page)
    {
        if ((uint)page < (uint)_pageStamps.Length)
        {
            _pageStamps[page] = ++_pageStampSerial;
        }
    }

    private void ActivateTraySelector(int index, InputModifiers modifiers)
    {
        var alternate = (modifiers & InputModifiers.Shift) != 0;
        var control = (modifiers & InputModifiers.Control) != 0;

        var expanded = IsPageExpanded;
        var page = PageIndex;

        if (index == ItemsSelector && page == 0 && !alternate && !control)
        {
            return;
        }

        switch (index)
        {
            case SkillsSelector:
                if (!KeepsPage(control, page, 3, 4))
                {
                    TrayMode = TakesSecondPage(alternate, page, 3, 4)
                        ? TrayMode.SkillPage2
                        : TrayMode.SkillPage1;
                }

                break;

            case SpellsSelector:
                if (!KeepsPage(control, page, 1, 2))
                {
                    TrayMode = TakesSecondPage(alternate, page, 1, 2)
                        ? TrayMode.SpellPage2
                        : TrayMode.SpellPage1;
                }

                break;

            case ChatSelector:
                if (!KeepsPage(control, page, 5, 6))
                {
                    ShowChat();
                }

                break;

            case StatusSelector:
                if (!KeepsPage(control, page, 7, 8))
                {
                    ActivateStatusSelector(alternate);
                }

                break;

            case SixthSelector:
                TrayMode = TrayMode.Combined;
                break;

            default:
                TrayMode = TrayMode.Items;
                break;
        }

        if (control || (index == ItemsSelector && alternate))
        {
            expanded = !expanded;
        }

        SelectPage(expanded);
    }

    private void ActivateStatusSelector(bool alternate)
    {
        if (Sheet is { } sheet)
        {
            sheet.Page = alternate ? CharacterSheetPage.Extra : CharacterSheetPage.Status;
        }

        ShowSheet();
    }
}
