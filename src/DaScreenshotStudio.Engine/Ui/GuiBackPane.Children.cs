using DaScreenshotStudio.Engine.Rendering;
using DaScreenshotStudio.Engine.Text;

namespace DaScreenshotStudio.Engine.Ui;

public sealed partial class GuiBackPane
{
    private readonly List<Pane> _managed = [];

    private const string SheetKey = "InventoryRect.sheet";

    private readonly Dictionary<string, Pane> _panes = [];

    private readonly List<Pane> _placed = [];

    private readonly HashSet<Pane> _retired = [];

    private CharacterSheetPane ReplaceSheet(string layoutName, in PixelRect home)
    {
        var previous = Sheet!;
        var pane = new CharacterSheetPane(
            CharacterSheetPane.PlaceIn(_assets, in home, layoutName),
            _blender,
            _assets,
            _player,
            _levelPoints,
            layoutName)
        {
            Background = _background,
            Page = previous.Page,
            StatIncreaseRequested = previous.StatIncreaseRequested,
            ConsumesPointerInside = true,
        };

        if (previous.Visible)
        {
            pane.Show();
        }

        Replace(previous, pane);

        var managed = _managed.IndexOf(previous);
        if (managed >= 0)
        {
            _managed[managed] = pane;
        }

        _panes[SheetKey] = pane;

        var placed = _placed.IndexOf(previous);
        if (placed >= 0)
        {
            _placed[placed] = pane;
        }

        Sheet = pane;
        return pane;
    }

    private Pixmap BackgroundFor(string layoutName)
    {
        if (Layout is not null && Layout.ControlCount > 0 && Layout[0].Images.Count > 0)
        {
            var image = Layout[0].Images[0];
            return _assets.Frame(image.EntryName, image.Frame);
        }

        return _assets.Frame(Path.ChangeExtension(layoutName, ".spf"), 0);
    }

    private GuiTextPane? AddLabel(
        string controlName,
        TextAlignment alignment = TextAlignment.Left,
        byte paletteIndex = TextPalette.InterfaceIndex)
    {
        if (!GuiLayoutGeometry.TryRect(Layout, controlName, out var rect))
        {
            return null;
        }

        var pane = Managed(controlName, () => new GuiTextPane(in rect, _blender, _assets.Font, _assets.Palette));
        pane.SetBounds(rect);
        pane.Alignment = alignment;
        pane.PaletteIndex = paletteIndex;
        pane.Background = _background;
        return pane;
    }

    private GuiTextPane? AddVitalLabel(string controlName, OrbPane? orb)
    {
        if (!GuiLayoutGeometry.TryRect(Layout, controlName, out var rect))
        {
            return null;
        }

        if (orb is not null && Contains(orb.Bounds, in rect))
        {
            orb.SetLabelBox(rect.Translate(-orb.Bounds.X, -orb.Bounds.Y), _assets.Font, _assets.Palette);
            return null;
        }

        orb?.ClearLabel();

        var pane = Managed(controlName, () => new GuiTextPane(in rect, _blender, _assets.Font, _assets.Palette));
        pane.SetBounds(rect);
        pane.Alignment = TextAlignment.Right;
        pane.PaletteIndex = TextPalette.InterfaceIndex;
        pane.Background = _background;
        return pane;
    }

    private static bool Contains(in PixelRect outer, in PixelRect inner) =>
        inner.X >= outer.X && inner.Y >= outer.Y && inner.Right <= outer.Right && inner.Bottom <= outer.Bottom;

    private ImagePane? AddPing()
    {
        Array.Clear(_pingFrames);

        if (Layout is null || !Layout.TryFind("PING", out var control) || control.Images.Count == 0)
        {
            return null;
        }

        var entry = control.Images[0].EntryName;
        for (var i = 0; i < NetworkIndicatorStages; i++)
        {
            _pingFrames[i] = _assets.Frame(entry, i);
        }

        var pane = Managed("PING", () => new ImagePane(GuiLayoutGeometry.Rect(control), _blender));
        pane.SetBounds(GuiLayoutGeometry.Rect(control));
        return pane;
    }

    private OrbPane? AddOrb(string controlName)
    {
        if (Layout is null || !Layout.TryFind(controlName, out var control) || control.Images.Count == 0)
        {
            return null;
        }

        var entry = control.Images[0].EntryName;

        var count = control.Images[0].Frame + 1;
        var frames = new Pixmap[count];
        for (var i = 0; i < count; i++)
        {
            frames[i] = _assets.Frame(entry, i);
        }

        var pane = Managed(controlName, () => new OrbPane(GuiLayoutGeometry.Rect(control), _blender, frames));
        pane.SetBounds(GuiLayoutGeometry.Rect(control));
        pane.SetFrames(frames);
        pane.Background = _background;
        return pane;
    }

    private SlotTrayPane? AddTray()
    {
        if (!GuiLayoutGeometry.TryRect(Layout, "InventoryRect", out var rect))
        {
            return null;
        }

        var pane = Managed("InventoryRect.tray", () => new SlotTrayPane(in rect, _blender, _assets, _player)
        {
            Mode = Ui.TrayMode.Items,
        });

        pane.SetBounds(rect);
        pane.Background = _background;
        pane.ConsumesPointerInside = true;

        ApplyNormalTray(pane);
        ApplyTrayGrid(pane);

        return pane;
    }

    private SlotTrayPane? AddSpellHalf()
    {
        if (Tray is null)
        {
            return null;
        }

        var panel = Tray.Bounds;
        if (!TryGeneralHalf(GeneralSpellControl, in panel, out var rect))
        {
            return null;
        }

        var pane = Managed(GeneralSpellControl, () => new SlotTrayPane(in rect, _blender, _assets, _player)
        {
            Mode = Ui.TrayMode.SpellPage1,
            FirstSlot = SlotTrayPane.GeneralPageStart + 1,
            Columns = SlotTrayPane.HalfColumns,
            InsetX = SlotTrayPane.AbilityInsetX,
            InsetY = SlotTrayPane.AbilityInsetY,
        });

        pane.SetBounds(rect);
        pane.Visible = false;
        return pane;
    }

    private ImagePane? AddChatPanel()
    {
        if (Layout is null || !Layout.TryFind(ExpandedChatControl, out var control))
        {
            return null;
        }

        var pane = Managed(ExpandedChatControl, () => new ImagePane(GuiLayoutGeometry.Rect(control), _blender));
        pane.SetBounds(GuiLayoutGeometry.Rect(control));
        pane.Visible = false;

        pane.ConsumesPointerInside = true;
        return pane;
    }

    private ImagePane? AddSelectorStrip()
    {
        if (Layout is null || !Layout.TryFind(SelectorStripControl, out var control)
            || control.Images.Count == 0)
        {
            return null;
        }

        var pane = Managed(SelectorStripControl, () => new ImagePane(GuiLayoutGeometry.Rect(control), _blender));
        pane.SetBounds(GuiLayoutGeometry.Rect(control));
        pane.Image = _assets.Frame(control.Images[0].EntryName, control.Images[0].Frame);
        pane.Visible = false;
        return pane;
    }

    private SlotKeyRowPane? AddSlotKeyRow()
    {
        var sprite = _assets.Sprite(SlotKeyRowPane.ArtEntryName);
        if (sprite is null || sprite.FrameCount == 0)
        {
            return null;
        }

        var art = _assets.Frame(SlotKeyRowPane.ArtEntryName, 0);
        if (art.IsEmpty)
        {
            return null;
        }

        var frame = sprite[0];
        var pane = Managed(
            "SlotKeyRow", () => new SlotKeyRowPane(new PixelRect(0, 0, frame.Right, frame.Bottom), _blender));

        pane.SetBounds(new PixelRect(0, 0, frame.Right, frame.Bottom));
        pane.Visible = false;
        pane.SetArt(in art, frame.Left, frame.Top);
        return pane;
    }

    private ScrollBarPane? AddChatScrollBar()
    {
        if (Chat is null || ScrollBarPane.LoadFrames(_assets) is not { } frames)
        {
            return null;
        }

        var pane = Managed(
            "ChatScrollBar", () => new ScrollBarPane(Chat.Bounds, _blender, frames, horizontal: false));

        pane.SetBounds(Chat.Bounds);
        pane.Visible = false;
        pane.Scrolled = () =>
        {
            if (Chat is { } chat)
            {
                chat.Scroll = pane.Position;
            }
        };

        return pane;
    }

    public const int ChatContentWidth = 0x198;

    private LogViewPane? AddChat()
    {
        if (!GuiLayoutGeometry.TryRect(Layout, "ChattingRect", out var rect))
        {
            return null;
        }

        var pane = Managed(
            "ChattingRect",
            () => new LogViewPane(in rect, _blender, _assets.Font, _assets.Palette, ChatLog, ChatContentWidth));

        pane.SetBounds(rect);
        pane.Background = _background;

        return pane;
    }

    private SystemMessagePane? AddSystemMessagePane()
    {
        if (!GuiLayoutGeometry.TryRect(Layout, SystemMessagePane.WrapControlName, out var wrap)
            || !GuiLayoutGeometry.TryRect(Layout, SystemMessagePane.TextControlName, out var text)
            || Layout is null
            || !Layout.TryFind(SystemMessagePane.ArtControlName, out var art)
            || art.Images.Count == 0)
        {
            return null;
        }

        var skin = _assets.Frame(art.Images[0].EntryName, art.Images[0].Frame);
        if (skin.IsEmpty)
        {
            return null;
        }

        var pane = Managed(
            "SystemMessage",
            () => new SystemMessagePane(in wrap, in text, in skin, _blender, _assets.Font, _assets.Palette, History));

        pane.SetVisibleLines(1);
        return pane;
    }

    private ChatInputPane? AddInput()
    {
        if (!GuiLayoutGeometry.TryRect(Layout, "SAY", out var rect))
        {
            return null;
        }

        var pane = Managed("SAY.input", () => new ChatInputPane(in rect, _blender, _assets.Font, _assets.Palette));
        pane.SetBounds(rect);
        pane.Background = _background;
        return pane;
    }

    private BlockListenPane? AddBlockListen()
    {
        if (!GuiLayoutGeometry.TryRect(Layout, "SAY", out var rect))
        {
            return null;
        }

        var pane = Managed(
            "SAY.block",
            () => new BlockListenPane(in rect, _blender, _assets.Font, _assets.Palette, _assets.Messages));

        pane.SetBounds(rect);
        pane.Background = _background;
        return pane;
    }

    private NumberLinePane? AddNumberLine()
    {
        if (!GuiLayoutGeometry.TryRect(Layout, "SAY", out var say))
        {
            return null;
        }

        var rect = new PixelRect(say.X, say.Y, NumberLinePane.LineWidth, NumberLinePane.LineHeight);

        var pane = Managed("SAY.number", () => new NumberLinePane(in rect, _blender, _assets.Font, _assets.Palette)
        {
            Visible = false,
        });

        pane.SetBounds(rect);
        pane.Background = _background;
        return pane;
    }

    private SpellInputPane? AddSpellInput()
    {
        if (!GuiLayoutGeometry.TryRect(Layout, "SAY", out var say))
        {
            return null;
        }

        var rect = new PixelRect(say.X, say.Y, SpellInputPane.LineWidth, SpellInputPane.LineHeight);

        var pane = Managed("SAY.spell", () => new SpellInputPane(in rect, _blender, _assets.Font, _assets.Palette)
        {
            Visible = false,
        });

        pane.SetBounds(rect);
        pane.Background = _background;
        return pane;
    }

    private CharacterSheetPane? AddSheet()
    {
        if (!GuiLayoutGeometry.TryRect(Layout, "InventoryRect", out var panel))
        {
            return null;
        }

        var name = LayoutIndex == GuiLayoutIndex.Compact
            ? CharacterSheetPane.LayoutName
            : CharacterSheetPane.ReducedLayoutName;

        var pane = Managed(SheetKey, () => new CharacterSheetPane(
            CharacterSheetPane.PlaceIn(_assets, in panel, name), _blender, _assets, _player, _levelPoints, name));

        if (!string.Equals(pane.Layout, name, StringComparison.OrdinalIgnoreCase))
        {
            Sheet = pane;
            pane = ReplaceSheet(name, in panel);
        }

        pane.SetBounds(CharacterSheetPane.PlaceIn(_assets, in panel, name));
        pane.Background = _background;
        pane.ConsumesPointerInside = true;
        return pane;
    }

    private TellInputPane? AddTell()
    {
        if (!GuiLayoutGeometry.TryRect(Layout, "SAY", out var rect))
        {
            return null;
        }

        var pane = Managed(
            "SAY.tell", () => new TellInputPane(in rect, _blender, _assets.Font, _assets.Palette, TellRecipients));

        pane.SetBounds(rect);
        pane.Background = _background;
        return pane;
    }

    private ImageButtonPane? AddButton(string controlName, Action<ImageButtonPane>? activated = null)
    {
        if (Layout is null || !Layout.TryFind(controlName, out var control))
        {
            return null;
        }

        var pane = Managed(controlName, () => new ImageButtonPane(GuiLayoutGeometry.Rect(control), _blender));
        pane.SetHitBounds(GuiLayoutGeometry.Rect(control));
        pane.States = ImageButtonStates.Retained;
        pane.Activated = activated;
        pane.SetFrames(
            _assets.Frame(control, 0),
            _assets.Frame(control, 1),
            _assets.Frame(control, 2));

        return pane;
    }

    private void AddTraySelector(int index)
    {
        Span<char> name = stackalloc char[8];
        "BTN_INV".CopyTo(name);
        name[7] = (char)('0' + index);

        var selector = AddButton(new string(name), button => ActivateTraySelector(index, button.ActivationModifiers));
        if (selector is not null)
        {
            selector.States = ImageButtonStates.RetainedWithHover;
        }

        _traySelectors[index] = selector;

        _selectorHome[index] = selector?.Bounds ?? PixelRect.Empty;
    }

    private void AddIconButton(int index) =>
        _iconButtons[index] = AddButton(RailTooltips[index].Control, _ => ActivateIcon(index));

    private T Managed<T>(string key, Func<T> create)
        where T : Pane
    {
        if (!_panes.TryGetValue(key, out var existing))
        {
            existing = create();
            _panes.Add(key, existing);
            _managed.Add(existing);
            Add(existing);
        }

        if (!_placed.Contains(existing))
        {
            _placed.Add(existing);
        }

        if (_retired.Remove(existing))
        {
            existing.Visible = true;
        }

        return (T)existing;
    }

    private void Retire(string key)
    {
        if (!_panes.TryGetValue(key, out var pane))
        {
            return;
        }

        if (!_placed.Contains(pane))
        {
            _placed.Add(pane);
        }

        _retired.Add(pane);
        pane.Visible = false;
        pane.SetBounds(new PixelRect(pane.Bounds.X, pane.Bounds.Y, 0, 0));
    }
}
