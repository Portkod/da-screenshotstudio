using System.Globalization;
using DaScreenshotStudio.Assets.Characters;
using DaScreenshotStudio.Assets.Metadata;
using DaScreenshotStudio.Engine.Rendering;
using DaScreenshotStudio.Engine.Text;
using DaScreenshotStudio.Engine.Core;
using DaScreenshotStudio.Engine.Records;

namespace DaScreenshotStudio.Engine.Ui;

public sealed class UserInfoPagePane : LayoutPane
{
    public override bool IsMovable => false;

    public const int FirstPage = 1;

    public const int PageCount = 5;

    public const int LegendPage = 1;

    public const string LegendListControl = "LegendList";

    public const int SkillPage = 2;

    public const string SkillListControl = "SKILL";

    public const string SpellListControl = "SPELL";

    public const int EventPage = 3;

    public const string EventListControlFormat = "EV";

    public const int EventListCount = 2;

    public const int EventGroupCount = 3;

    public const int EventCategoryCount = EventGroupCount * EventListCount;

    public const string PreviousButtonControl = "PREV";

    public const string NextButtonControl = "NEXT";

    public const int AlbumPage = 4;

    public const string AlbumHalfControlFormat = "PGA";

    public const string AlbumCounterControlFormat = "PG";

    public const string AlbumBoundControl = "PGBOUND";

    public const int AlbumSlotCount = 100;

    public const int AlbumTilesPerHalf = 6;

    public const int AlbumTilesVisible = AlbumTilesPerHalf * AlbumHalfCount;

    public const int AlbumHalfCount = 2;

    public const int AlbumTileColumns = 3;

    public const int AlbumPageCount = 17;

    public const int AlbumPageStep = 2;

    public const int FamilyPage = 5;

    public const string SelfNameControl = "Self";

    public const string FamilyNameControl = "Family";

    public const string FamilySlotControlFormat = "Text";

    public const int FamilyFocusedSlot = 0;

    public const int AlbumLastPageBase = 15;

    private readonly LegendListPane? _legendList;
    private readonly ScrollBarPane? _legendBar;
    private readonly AbilityGridPane? _skillGrid;
    private readonly ScrollBarPane? _skillBar;
    private readonly AbilityGridPane? _spellGrid;
    private readonly ScrollBarPane? _spellBar;
    private readonly EventListPane[] _eventLists = [];
    private readonly ScrollBarPane[] _eventBars = [];
    private readonly EventCategory[] _eventCategories = [];
    private readonly Pixmap[] _eventBackgrounds = [];
    private readonly ImageButtonPane? _previousButton;
    private readonly ImageButtonPane? _nextButton;
    private readonly AlbumTilePane[] _albumTiles = [];
    private readonly GuiTextPane?[] _albumCounters = [];
    private readonly Pixmap[] _albumPictures = [];
    private readonly TextEditPane[] _familySlots = [];
    private readonly TextBlock? _selfName;
    private readonly TextBlock? _familyName;
    private PixelRect _selfNameRect;
    private PixelRect _familyNameRect;

    private ClassAbilityTable _abilities = ClassAbilityTable.Empty;
    private int _abilityClass = -1;

    private static readonly string[] PageLayouts =
    [
        "_nui_dr.txt", "_nui_sk.txt", "_nui_ev.txt", "_nui_al.txt", "_nui_fm.txt",
    ];

    private static readonly EventCategory EmptyCategory = new();

    public UserInfoPagePane(UiAssets assets, PixelBlender blender, int page)
        : base(assets, blender, LayoutFor(page), Placement(assets, LayoutFor(page)))
    {
        Page = page;
        Visible = false;

        ConsumesPointerInside = true;

        if (page == LegendPage)
        {
            BuildLegendList(assets, blender, out _legendList, out _legendBar);
        }

        if (page == SkillPage)
        {
            BuildAbilityGrid(assets, blender, SkillListControl, spells: false, out _skillGrid, out _skillBar);
            BuildAbilityGrid(assets, blender, SpellListControl, spells: true, out _spellGrid, out _spellBar);
        }

        if (page == EventPage)
        {
            _eventBackgrounds = LoadEventBackgrounds(assets);
            _eventCategories = new EventCategory[EventCategoryCount];
            for (var i = 0; i < _eventCategories.Length; i++)
            {
                _eventCategories[i] = new EventCategory();
            }

            _previousButton = AddButton(PreviousButtonControl, _ => StepEventGroup(-1));
            _nextButton = AddButton(NextButtonControl, _ => StepEventGroup(1));

            BuildEventLists(assets, blender, out _eventLists, out _eventBars);
            SelectEventGroup(0);
        }

        if (page == AlbumPage)
        {
            _previousButton = AddButton(PreviousButtonControl, _ => StepAlbumPage(0));
            _nextButton = AddButton(NextButtonControl, _ => StepAlbumPage(1));

            _albumCounters = new GuiTextPane?[AlbumHalfCount];
            for (var i = 0; i < _albumCounters.Length; i++)
            {
                var control = string.Concat(
                    AlbumCounterControlFormat,
                    (i + 1).ToString(null, CultureInfo.InvariantCulture));

                _albumCounters[i] = AddLabel(control, TextAlignment.Center, 1, TextPalette.WhiteIndex);
            }

            _albumPictures = new Pixmap[AlbumSlotCount];
            _albumTiles = BuildAlbumTiles(assets, blender);
            ShowAlbumPagePair(0);
        }

        if (page == FamilyPage)
        {
            GuiLayoutGeometry.TryRect(Layout, SelfNameControl, out _selfNameRect);
            GuiLayoutGeometry.TryRect(Layout, FamilyNameControl, out _familyNameRect);

            _selfName = NameBlock(assets, _selfNameRect);
            _familyName = NameBlock(assets, _familyNameRect);
            _familySlots = BuildFamilySlots();

            if (_familySlots.Length > FamilyFocusedSlot)
            {
                Focus(_familySlots[FamilyFocusedSlot]);
            }
        }
    }

    public int Page { get; }

    public LegendListPane? LegendList => _legendList;

    public int EventGroup { get; private set; }

    public void SetAbilities(
        CharacterClass characterClass,
        PlayerModel player,
        bool showAbilityMetadata,
        bool showMasterMetadata)
    {
        if (_skillGrid is null || _spellGrid is null)
        {
            return;
        }

        if (_abilityClass != (int)characterClass)
        {
            _abilities = ClassAbilityTable.Load(Assets.Metafiles, (int)characterClass);
            _abilityClass = (int)characterClass;
        }

        _skillGrid.SetAbilities(_abilities, player, Assets.Messages, showAbilityMetadata, showMasterMetadata);
        _spellGrid.SetAbilities(_abilities, player, Assets.Messages, showAbilityMetadata, showMasterMetadata);

        RefreshRange(_skillGrid, _skillBar);
        RefreshRange(_spellGrid, _spellBar);
    }

    public void SetLegendMarks(ReadOnlySpan<LegendMark> marks)
    {
        if (_legendList is null)
        {
            return;
        }

        _legendList.SetMarks(marks);
        RefreshLegendRange();
    }

    public void SetEvents(
        CharacterClass characterClass,
        PlayerModel player,
        LegendListPane? legend,
        bool showAbilityMetadata,
        bool showMasterMetadata)
    {
        ArgumentNullException.ThrowIfNull(player);

        if (_eventCategories.Length == 0)
        {
            return;
        }

        var bucket = EventRoster.BucketFor(
            player.Progression.Level,
            showAbilityMetadata,
            showMasterMetadata);

        foreach (var category in _eventCategories)
        {
            category.Clear();
        }

        for (var table = 0; table < EventTable.TableCount; table++)
        {
            var category = _eventCategories[Math.Min(table, EventCategoryCount - 1)];
            category.AddTable(
                EventTable.Load(Assets.Metafiles, table),
                table,
                Assets.Messages,
                legend,
                bucket,
                characterClass);
        }

        SelectEventGroup(EventGroup);
    }

    public void SelectEventGroup(int group)
    {
        if (_eventLists.Length == 0)
        {
            return;
        }

        EventGroup = group;

        for (var i = 0; i < _eventLists.Length; i++)
        {
            var list = _eventLists[i];
            list.Position = 0;
            _eventBars[i].Position = 0;

            var index = (group * EventListCount) + i;
            list.SetEvents((uint)index < (uint)_eventCategories.Length ? _eventCategories[index] : EmptyCategory);

            _eventBars[i].PageStep = list.PageStep;
            _eventBars[i].Maximum = list.Maximum;
            _eventBars[i].Position = list.Position;
        }

        if ((uint)group < (uint)_eventBackgrounds.Length)
        {
            Background = _eventBackgrounds[group];
            for (var i = 0; i < _eventLists.Length; i++)
            {
                _eventLists[i].Background = Background;
            }
        }

        Invalidate();
    }

    public int AlbumPageBase { get; private set; }

    public void SetAlbum(AlbumFile? album)
    {
        if (_albumTiles.Length == 0)
        {
            return;
        }

        Array.Clear(_albumPictures);

        if (album is not null)
        {
            var slots = Math.Min(album.Capacity, AlbumSlotCount);
            for (var slot = 0; slot < slots; slot++)
            {
                _albumPictures[slot] = DecodePortrait(album, slot, Assets.Packing);
            }
        }

        ShowAlbumPagePair(AlbumPageBase);
    }

    public void ShowAlbumPagePair(int pageBase)
    {
        if (_albumTiles.Length == 0)
        {
            return;
        }

        AlbumPageBase = pageBase;

        foreach (var tile in _albumTiles)
        {
            tile.Visible = false;
        }

        foreach (var counter in _albumCounters)
        {
            counter?.SetText(default);
        }

        Span<byte> number = stackalloc byte[16];
        var first = pageBase * AlbumTilesPerHalf;

        for (var record = first; record < first + AlbumTilesVisible; record++)
        {
            if (record >= AlbumSlotCount)
            {
                continue;
            }

            var half = (record - first) / AlbumTilesPerHalf;
            if (half < _albumCounters.Length && _albumCounters[half] is { } counter)
            {
                var text = new TextBuffer(number);
                text.Append(pageBase + half + 1);
                text.Append('/');
                text.Append(AlbumPageCount);
                counter.SetText(text.Written);
            }

            var tile = _albumTiles[record % AlbumTilesVisible];
            tile.SetPicture(_albumPictures[record], record);
            tile.Visible = true;
        }

        Invalidate();
    }

    public void StepAlbumPage(int action)
    {
        if (_albumTiles.Length == 0)
        {
            return;
        }

        switch (action)
        {
            case 0:
                ShowAlbumPagePair(Math.Max(AlbumPageBase - AlbumPageStep, 0));
                break;

            case 1:
                var forward = AlbumPageBase + AlbumPageStep;
                ShowAlbumPagePair(forward < AlbumPageCount ? forward : AlbumLastPageBase);
                break;

            default:
                break;
        }
    }

    public void SetSelfName(scoped ReadOnlySpan<byte> name)
    {
        if (_selfName is null || !_selfName.SetText(name))
        {
            return;
        }

        Invalidate();
    }

    public void SetFamilyNames(FamilyList list)
    {
        ArgumentNullException.ThrowIfNull(list);

        for (var slot = 0; slot < _familySlots.Length; slot++)
        {
            _familySlots[slot].SetValue(list[slot]);
        }
    }

    private void RefreshLegendRange()
    {
        if (_legendList is not { } list || _legendBar is not { } bar)
        {
            return;
        }

        bar.PageStep = list.PageStep;
        bar.Maximum = list.Maximum;
        bar.Position = list.Position;
    }

    private void BuildLegendList(
        UiAssets assets,
        PixelBlender blender,
        out LegendListPane? list,
        out ScrollBarPane? bar) =>
        TryBuildScrolledControl(
            LegendListControl, content => new LegendListPane(in content, blender, assets), out list, out bar);

    private void BuildAbilityGrid(
        UiAssets assets,
        PixelBlender blender,
        string control,
        bool spells,
        out AbilityGridPane? grid,
        out ScrollBarPane? bar)
    {
        if (!TryBuildScrolledControl(
                control,
                content => new AbilityGridPane(in content, blender, assets, spells),
                out grid,
                out bar))
        {
            return;
        }
    }

    private void StepEventGroup(int delta)
    {
        var wanted = EventGroup + delta;
        if (wanted < 0 || wanted >= EventGroupCount)
        {
            return;
        }

        SelectEventGroup(wanted);
    }

    private void BuildEventLists(
        UiAssets assets,
        PixelBlender blender,
        out EventListPane[] lists,
        out ScrollBarPane[] bars)
    {
        var built = new List<EventListPane>(EventListCount);
        var builtBars = new List<ScrollBarPane>(EventListCount);

        for (var i = 0; i < EventListCount; i++)
        {
            var control = string.Concat(
                EventListControlFormat,
                (i + 1).ToString(null, CultureInfo.InvariantCulture));

            if (!TryBuildScrolledControl(
                    control,
                    content => new EventListPane(in content, blender, assets),
                    out var list,
                    out var bar))
            {
                continue;
            }

            built.Add(list);
            builtBars.Add(bar);
        }

        lists = [.. built];
        bars = [.. builtBars];
    }

    private Pixmap[] LoadEventBackgrounds(UiAssets assets)
    {
        if (Layout is not { ControlCount: > 0 } layout)
        {
            return [];
        }

        var images = layout[0].Images;
        var backgrounds = new Pixmap[Math.Min(images.Count, EventGroupCount)];
        for (var i = 0; i < backgrounds.Length; i++)
        {
            backgrounds[i] = assets.Frame(images[i].EntryName, images[i].Frame);
        }

        return backgrounds;
    }

    private AlbumTilePane[] BuildAlbumTiles(UiAssets assets, PixelBlender blender)
    {
        if (!GuiLayoutGeometry.TryRect(Layout, AlbumBoundControl, out var bound))
        {
            return [];
        }

        var halves = new PixelRect[AlbumHalfCount];
        for (var half = 0; half < halves.Length; half++)
        {
            var control = string.Concat(
                AlbumHalfControlFormat,
                (half + 1).ToString(null, CultureInfo.InvariantCulture));

            if (!GuiLayoutGeometry.TryRect(Layout, control, out halves[half]))
            {
                return [];
            }
        }

        var tiles = new AlbumTilePane[AlbumTilesVisible];
        for (var position = 0; position < tiles.Length; position++)
        {
            var half = halves[position / AlbumTilesPerHalf];
            var cell = position % AlbumTilesPerHalf;
            var row = cell / AlbumTileColumns;
            var column = cell % AlbumTileColumns;

            var tile = new AlbumTilePane(
                assets,
                blender,
                new PixelRect(
                    half.X + (column * bound.Width),
                    half.Y + (row * bound.Height),
                    bound.Width,
                    bound.Height))
            {
                Visible = false,
            };

            tiles[position] = tile;
            Add(tile);
        }

        return tiles;
    }

    private static Pixmap DecodePortrait(AlbumFile album, int slot, in PixelPacking packing)
    {
        var record = album[slot];
        if (!record.IsActive || record.Width <= 0 || record.Height <= 0)
        {
            return default;
        }

        var bytes = album.Decode(slot);
        if (bytes.Length < record.DecodedLength)
        {
            return default;
        }

        var pixels = new ushort[record.Width * record.Height];
        var rgb555 = packing.Format == RenderPixelFormat.Rgb555;

        for (var i = 0; i < pixels.Length; i++)
        {
            var value = (ushort)(bytes[i * 2] | (bytes[(i * 2) + 1] << 8));
            pixels[i] = rgb555 ? (ushort)((value & 0x1F) | ((value & 0xFFC0) >> 1)) : value;
        }

        return new Pixmap(pixels, record.Width, record.Height);
    }

    private static void RefreshRange(AbilityGridPane grid, ScrollBarPane? bar)
    {
        if (bar is null)
        {
            return;
        }

        bar.PageStep = grid.PageStep;
        bar.Maximum = grid.Maximum;
        bar.Position = grid.Position;
    }

    private TextEditPane[] BuildFamilySlots()
    {
        var built = new List<TextEditPane>(FamilyList.SlotCount);
        for (var slot = 0; slot < FamilyList.SlotCount; slot++)
        {
            var control = string.Concat(
                FamilySlotControlFormat,
                slot.ToString(null, CultureInfo.InvariantCulture));

            if (AddEdit(control, FamilyList.MaxCommittedBytes) is not { } pane)
            {
                break;
            }

            pane.PaletteIndex = TextPalette.InterfaceIndex;
            built.Add(pane);
        }

        return [.. built];
    }

    private static TextBlock? NameBlock(UiAssets assets, in PixelRect rect) =>
        rect.IsEmpty
            ? null
            : new TextBlock(assets.Font, assets.Palette, Math.Max(1, rect.Width), 1)
            {
                Wraps = false,
                DefaultPaletteIndex = TextPalette.InterfaceIndex,
            };

    private static void DrawCenteredName(Canvas canvas, TextBlock? block, in PixelRect rect)
    {
        if (block is null || rect.IsEmpty || block.Width <= 0)
        {
            return;
        }

        var pen = ((rect.X + rect.Right) / 2) - (block.Width / 2);
        block.Draw(canvas, pen, rect.Bottom - block.LineHeight);
    }

    public static string LayoutFor(int page) =>
        (uint)(page - FirstPage) < (uint)PageCount ? PageLayouts[page - FirstPage] : string.Empty;

    protected override bool OnKey(in KeyEvent key)
    {
        if (key.Type == UiEventType.KeyDown && key.Key is UiKeys.Escape or UiKeys.Space)
        {
            return false;
        }

        return base.OnKey(in key);
    }

    protected override void OnDrawContent(Canvas canvas)
    {
        base.OnDrawContent(canvas);

        if (Page != FamilyPage)
        {
            return;
        }

        DrawCenteredName(canvas, _selfName, in _selfNameRect);
        DrawCenteredName(canvas, _familyName, in _familyNameRect);
    }

    private static PixelRect Placement(UiAssets assets, string layoutName)
    {
        var content = UserInfoShell.Content(assets);
        return content.IsEmpty ? Place(assets, layoutName) : content;
    }
}
