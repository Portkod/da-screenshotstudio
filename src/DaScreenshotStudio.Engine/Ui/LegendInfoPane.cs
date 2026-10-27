using DaScreenshotStudio.Engine.Rendering;

namespace DaScreenshotStudio.Engine.Ui;

public sealed class LegendInfoPane : SessionLayeredTabPane
{
    public const string LayoutName = "llinfo.txt";

    public const string BackgroundControl = "Noname";

    public const string TabColumnControl = "MainTab";

    public const string ContentControl = "Content";

    public const string CaptionControl = "SSHelp";

    public static readonly string[] TabControls =
    [
        "TabLegend", "TabSpell", "TabSkill", "TabEvent", "TabMap",
        "TabAlbum", "TabMonster", "TabHelp", "TabQuit",
    ];

    public const int QuitPage = 8;

    public const int QuitShortcut = 0x1B;

    private readonly bool _ownProfile;
    private readonly ImagePane? _background;
    private readonly PixelRect _contentRect;
    private readonly PixelRect _captionRect;
    private readonly Pixmap _captionPlain;
    private readonly Pixmap _captionEmphasised;
    private readonly ImagePane? _caption;

    private readonly NewLegendDialogPane? _legend;

    private SpellSkillInfoPane? _spells;
    private SpellSkillInfoPane? _skills;
    private EventInfoPane? _events;
    private WorldMapInfoPane? _worldMap;
    private AlbumInfoPane? _album;
    private HelpInfoPane? _help;
    private MonsterInfoPane? _monsters;

    public LegendInfoPane(UiAssets assets, PixelBlender blender, bool ownProfile)
        : base(assets, blender, LayoutName, Placement(assets, TabColumnControl))
    {
        ArgumentNullException.ThrowIfNull(assets);

        _ownProfile = ownProfile;

        GuiLayoutGeometry.TryRect(Layout, ContentControl, out _contentRect);
        GuiLayoutGeometry.TryRect(Layout, CaptionControl, out _captionRect);

        _captionPlain = ControlFrame(CaptionControl, 0);
        _captionEmphasised = ControlFrame(CaptionControl, 1);

        var backgroundRect = Placement(assets, BackgroundControl);
        _background = new ImagePane(in backgroundRect, blender);
        _background.Image = ControlFrame(BackgroundControl, 0);

        AddTabs(assets);

        _legend = new NewLegendDialogPane(assets, blender, ownProfile, _contentRect) { Visible = false };

        if (ownProfile)
        {
            _caption = new ImagePane(in _captionRect, blender) { Visible = false };

            _spells = new SpellSkillInfoPane(assets, blender, isSpell: true) { Visible = false };
            _skills = new SpellSkillInfoPane(assets, blender, isSpell: false) { Visible = false };
            _events = new EventInfoPane(assets, blender) { Visible = false };

            _worldMap = new WorldMapInfoPane(assets, blender) { Visible = false };
            _album = new AlbumInfoPane(assets, blender) { Visible = false };

            _help = new HelpInfoPane(assets, blender) { Visible = false };
            _monsters = new MonsterInfoPane(assets, blender) { Visible = false };
        }

        ApplyTabEnables();
    }

    public bool IsOwnProfile => _ownProfile;

    public ImagePane? Caption => _caption;

    public MonsterInfoPane? Monsters => _monsters;

    public NewLegendDialogPane? Legend => _legend;

    public SpellSkillInfoPane? Spells => _spells;

    public SpellSkillInfoPane? Skills => _skills;

    public WorldMapInfoPane? WorldMap => _worldMap;

    public PixelRect ContentRect => _contentRect;

    public static PixelRect Placement(UiAssets assets, string control) =>
        GuiLayoutGeometry.ControlRect(assets, LayoutName, control);

    public void AttachTo(Pane parent)
    {
        ArgumentNullException.ThrowIfNull(parent);

        if (_background is null)
        {
            return;
        }

        parent.Add(_background);
        _background.Add(this);

        SetStripParent(_background);

        if (_legend is not null)
        {
            _background.Add(_legend);
        }

        if (_caption is not null)
        {
            _background.Add(_caption);
        }

        if (_spells is not null)
        {
            _background.Add(_spells);
            _background.Add(_spells.View);
            _spells.SetStripParent(_background);
        }

        if (_skills is not null)
        {
            _background.Add(_skills);
            _background.Add(_skills.View);
            _skills.SetStripParent(_background);
        }

        if (_events is not null)
        {
            _background.Add(_events);
            _background.Add(_events.View);
            _events.SetStripParent(_background);
        }

        if (_worldMap is not null)
        {
            _background.Add(_worldMap);
            _background.Add(_worldMap.View);
            _worldMap.SetStripParent(_background);
        }

        if (_album is not null)
        {
            _background.Add(_album);
        }

        if (_help is not null)
        {
            _background.Add(_help);
            _help.SetStripParent(_background);
        }

        if (_monsters is not null)
        {
            _background.Add(_monsters);
        }
    }

    public void Show()
    {
        if (_background is not null)
        {
            _background.Visible = true;
        }

        Visible = true;

        Select(0);
        Invalidate();
    }

    public void Quit()
    {
        LeaveCurrentPage();
        Visible = false;

        if (_background is not null)
        {
            _background.Visible = false;
        }

        Invalidate();
    }

    public Pixmap CaptionFrameFor(int page) => page switch
    {
        1 or 2 => _captionPlain,
        3 => _captionEmphasised,
        _ => default,
    };

    protected override void OnPageEnter(int index)
    {
        if (index == QuitPage)
        {
            Quit();
            return;
        }

        if (index == 0)
        {
            if (_legend is not null)
            {
                _legend.Visible = true;
            }

            return;
        }

        if (!_ownProfile)
        {
            return;
        }

        if (_caption is not null && CaptionFrameFor(index) is { IsEmpty: false } frame)
        {
            _caption.Image = frame;
            _caption.Visible = true;
        }

        switch (index)
        {
            case 1:
                _spells?.Show();
                break;

            case 2:
                _skills?.Show();
                break;

            case 3:
                _events?.Show();
                break;

            case 4:
                _worldMap?.Show();
                break;

            case 5:
                _album?.Show();
                break;

            case 6:
                _monsters?.Enter();
                break;

            case 7:
                _help?.Show();
                break;

            default:
                break;
        }
    }

    protected override void OnPageLeave(int index)
    {
        if (index is < 0 or QuitPage)
        {
            return;
        }

        if (index != 0 && !_ownProfile)
        {
            return;
        }

        if (index == 0 && _legend is not null)
        {
            _legend.Visible = false;
        }

        if (index is 1 or 2 or 3 && _caption is not null)
        {
            _caption.Visible = false;
        }

        switch (index)
        {
            case 1:
                _spells?.Hide();
                break;

            case 2:
                _skills?.Hide();
                break;

            case 3:
                _events?.Hide();
                break;

            case 4:
                _worldMap?.Hide();
                break;

            case 5:
                _album?.Hide();
                break;

            case 6:
                _monsters?.Leave();
                break;

            case 7:
                _help?.Hide();
                break;

            default:
                break;
        }
    }

    private void AddTabs(UiAssets assets)
    {
        var column = Placement(assets, TabColumnControl);

        for (var i = 0; i < TabControls.Length; i++)
        {
            var name = TabControls[i];
            GuiLayoutGeometry.TryRect(Layout, name, out var tab);
            GuiLayoutGeometry.TryRect(Layout, $"{name}R", out var strip);

            var art = ControlFrame(name, 0);
            AddTab(
                new PixelRect(tab.X - column.X, tab.Y - column.Y, tab.Width, tab.Height),
                art,
                art,
                default,
                in strip,
                ControlFrame($"{name}R", 0),
                i == QuitPage ? QuitShortcut : LayeredTab.NoShortcut);
        }
    }

    private void ApplyTabEnables()
    {
        if (_ownProfile)
        {
            for (var i = 0; i < TabCount; i++)
            {
                SetTabEnabled(i, true);
            }

            return;
        }

        for (var i = 0; i < TabCount; i++)
        {
            SetTabEnabled(i, false);
        }

        SetTabEnabled(0, true);
        SetTabEnabled(QuitPage, true);
    }
}
