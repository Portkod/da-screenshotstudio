using DaScreenshotStudio.Assets.Gui;
using DaScreenshotStudio.Engine.Rendering;
using DaScreenshotStudio.Engine.Text;
using DaScreenshotStudio.Engine.Core;

namespace DaScreenshotStudio.Engine.Ui;

public enum CharacterSheetPage
{
    Status = 0,

    Extra = 1,
}

public sealed class CharacterSheetPane : GuiChildPane
{
    public const string LayoutName = "_nstatus.txt";

    public const string ReducedLayoutName = "_nstatur.txt";

    public const string StatusControl = "Status";

    public const string ExtraStatusControl = "ExtraStatus";

    public const int LabelCount = 24;

    public const int StatButtonCount = 5;

    public const string FirstStatButtonControl = "LevelUp";

    public const string SecondStatButtonControl = "LevelUpBottom";

    private readonly UiAssets _assets;
    private readonly PlayerModel _player;
    private readonly LevelPointFlash _levelPoints;
    private readonly TextBlock _label;
    private readonly PixelRect[] _rects = new PixelRect[LabelCount];
    private readonly CharacterSheetPage[] _pages = new CharacterSheetPage[LabelCount];
    private readonly byte[] _values = new byte[LabelCount * ValueBytes];
    private readonly int[] _lengths = new int[LabelCount];

    private readonly Pixmap[] _statButtonFrames = new Pixmap[GuiLayoutControl.ImageButtonStateCount];
    private readonly PixelRect[] _statButtons = new PixelRect[StatButtonCount];

    private readonly byte[] _pointLine = new byte[0x100];
    private int _pressedStat = -1;

    private Pixmap _statusArt;
    private Pixmap _extraArt;
    private int _revision = -1;
    private int _shownStatPoints = -1;
    private bool _shownPhase;

    private const int ValueBytes = 24;

    private const int Strength = 0;
    private const int Intelligence = 1;
    private const int Wisdom = 2;
    private const int Constitution = 3;
    private const int Dexterity = 4;
    private const int Health = 5;
    private const int MaxHealth = 6;
    private const int Mana = 7;
    private const int MaxMana = 8;
    private const int Experience = 9;
    private const int Gold = 10;
    private const int GamePoints = 11;
    private const int Level = 12;
    private const int ToNextLevel = 13;
    private const int AbilityLevel = 14;
    private const int ToNextAbility = 15;
    private const int TotalAbility = 16;
    private const int AttackElement = 17;
    private const int DefenseElement = 18;
    private const int MagicResistance = 19;
    private const int ArmorClass = 20;
    private const int DamageModifier = 21;
    private const int HitModifier = 22;
    private const int StatPoints = 23;

    private static readonly string[] LabelControls =
    [
        "s_Str", "s_Int", "s_Wis", "s_Con", "s_Dex",
        "s_HP", "s_HPMax", "s_MP", "s_MPMax",
        "s_EXP", "s_Gold", "s_GP",
        "s_Lev", "s_nextLev", "s_Ab", "s_nextAb", "s_AEXP",
        "e_attack", "e_defense", "e_magic", "e_AC", "e_DMG", "e_HIT",
        StatPointsControl,
    ];

    public const string StatPointsControl = "s_LevUp";

    private static CharacterSheetPage PageOf(string controlName) =>
        controlName.StartsWith("e_", StringComparison.Ordinal)
            ? CharacterSheetPage.Extra
            : CharacterSheetPage.Status;

    private static readonly string[] ElementNames =
    [
        "None", "Fire", "Water", "Wind", "Earth", "Light", "Dark", "Wood", "Metal", "Undead",
    ];

    public CharacterSheetPane(
        in PixelRect bounds,
        PixelBlender blender,
        UiAssets assets,
        PlayerModel player,
        LevelPointFlash levelPoints,
        string layoutName = LayoutName)
        : base(in bounds, blender)
    {
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(levelPoints);
        ArgumentException.ThrowIfNullOrEmpty(layoutName);

        Layout = layoutName;
        _assets = assets;
        _player = player;
        _levelPoints = levelPoints;
        _label = new TextBlock(assets.Font, assets.Palette, Math.Max(1, bounds.Width), 1)
        {
            Wraps = false,
            Alignment = TextAlignment.Right,
            DefaultPaletteIndex = TextPalette.InterfaceIndex,
        };

        Visible = false;

        ApplyLayout();
    }

    public string Layout { get; }

    public static PixelRect PlaceIn(UiAssets assets, in PixelRect panel, string layoutName = LayoutName)
    {
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentException.ThrowIfNullOrEmpty(layoutName);

        var layout = assets.Layout(layoutName);
        var size = GuiLayoutGeometry.TryRect(layout, StatusControl, out var status) ? status : panel;

        var x = panel.X;
        var y = panel.Bottom - size.Height;
        return new PixelRect(Math.Max(0, x), Math.Max(0, y), size.Width, size.Height);
    }

    public CharacterSheetPage Page
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            Invalidate();
        }
    } = CharacterSheetPage.Status;

    public Action<int>? StatIncreaseRequested { get; set; }

    public delegate void DescriptionHandler(ReadOnlySpan<byte> text);

    public DescriptionHandler? DescriptionRequested { get; set; }

    public static ReadOnlySpan<byte> LevelUpPointPrefix => "Level Up Point : "u8;

    public void Show()
    {
        if (Visible)
        {
            return;
        }

        Visible = true;
        _revision = -1;
        _shownStatPoints = -1;
        Invalidate();
    }

    public void Close()
    {
        if (!Visible)
        {
            return;
        }

        Visible = false;
        Invalidate();
    }

    public void Refresh()
    {
        if (!Visible)
        {
            return;
        }

        if (_shownPhase != _levelPoints.Phase)
        {
            _shownPhase = _levelPoints.Phase;
            Invalidate();
        }

        if (_shownStatPoints != _levelPoints.StatPoints)
        {
            _shownStatPoints = _levelPoints.StatPoints;
            SetNumber(StatPoints, _levelPoints.StatPoints);
            Invalidate();
        }

        if (_revision == _player.Revision)
        {
            return;
        }

        _revision = _player.Revision;
        ApplyValues();
        Invalidate();
    }

    protected override bool OnPointer(in PointerEvent pointer)
    {
        if (!Visible)
        {
            return false;
        }

        var inside = Contains(in pointer);

        if (pointer.Type == UiEventType.PointerMove)
        {
            if (inside && _levelPoints.StatPoints != 0)
            {
                RaiseLevelUpPointLine();
            }

            return false;
        }

        if (pointer.Type is not (UiEventType.LeftDown or UiEventType.LeftUp))
        {
            return false;
        }

        var overStat = inside && Page == CharacterSheetPage.Status && _levelPoints.HasStatPoints
            ? StatButtonAt(pointer.X, pointer.Y)
            : -1;

        if (pointer.Type == UiEventType.LeftDown)
        {
            if (overStat < 0)
            {
                return false;
            }

            _pressedStat = overStat;
            Invalidate();
            return true;
        }

        if (_pressedStat >= 0)
        {
            var pressed = _pressedStat;
            _pressedStat = -1;
            Invalidate();

            if (pressed == overStat)
            {
                StatIncreaseRequested?.Invoke(pressed);
            }

            return true;
        }

        return false;
    }

    private void RaiseLevelUpPointLine()
    {
        if (DescriptionRequested is not { } handler)
        {
            return;
        }

        var text = new TextBuffer(_pointLine);
        text.Append(LevelUpPointPrefix);
        text.Append((uint)_levelPoints.StatPoints);
        handler(_pointLine.AsSpan(0, text.Length));
    }

    private int StatButtonAt(int x, int y)
    {
        for (var i = 0; i < StatButtonCount; i++)
        {
            var rect = _statButtons[i];
            if (!rect.IsEmpty && x >= rect.X && y >= rect.Y && x < rect.Right && y < rect.Bottom)
            {
                return i;
            }
        }

        return -1;
    }

    protected override void OnDrawContent(Canvas canvas)
    {
        PaintBackground(canvas);

        var page = Page;
        var art = page == CharacterSheetPage.Status ? _statusArt : _extraArt;
        if (!art.IsEmpty)
        {
            canvas.BlitPixmap(0, 0, in art, BlendMode.Transparent);
        }

        for (var i = 0; i < LabelCount; i++)
        {
            if (_pages[i] != page || _rects[i].IsEmpty || _lengths[i] == 0)
            {
                continue;
            }

            if (i == StatPoints && _levelPoints.Phase && _levelPoints.StatPoints != 0)
            {
                continue;
            }

            _label.SetText(_values.AsSpan(i * ValueBytes, _lengths[i]));
            _label.DrawIn(canvas, in _rects[i]);
        }

        if (page == CharacterSheetPage.Status)
        {
            DrawStatButtons(canvas);
        }
    }

    private void DrawStatButtons(Canvas canvas)
    {
        if (!_levelPoints.HasStatPoints)
        {
            return;
        }

        for (var i = 0; i < StatButtonCount; i++)
        {
            var rect = _statButtons[i];
            if (rect.IsEmpty)
            {
                continue;
            }

            var frame = i == _pressedStat ? _statButtonFrames[2] : _statButtonFrames[_levelPoints.Phase ? 1 : 0];
            if (frame.IsEmpty)
            {
                frame = _statButtonFrames[0];
            }

            if (!frame.IsEmpty)
            {
                canvas.BlitPixmap(rect.X, rect.Y, in frame, BlendMode.Transparent);
            }
        }
    }

    private void ApplyLayout()
    {
        var layout = _assets.Layout(Layout);

        var status = GuiLayoutGeometry.TryRect(layout, StatusControl, out var statusRect)
            ? statusRect
            : PixelRect.Empty;
        var extra = GuiLayoutGeometry.TryRect(layout, ExtraStatusControl, out var extraRect)
            ? extraRect
            : PixelRect.Empty;

        _statusArt = ArtOf(layout, StatusControl);
        _extraArt = ArtOf(layout, ExtraStatusControl);

        for (var i = 0; i < LabelCount; i++)
        {
            _pages[i] = PageOf(LabelControls[i]);
            var owner = _pages[i] == CharacterSheetPage.Status ? status : extra;
            _rects[i] = GuiLayoutGeometry.TryRect(layout, LabelControls[i], out var rect)
                ? rect.Translate(-owner.X, -owner.Y)
                : PixelRect.Empty;
        }

        ApplyStatButtons(layout, in status);

        if (layout is not null && layout.TryFind(FirstStatButtonControl, out var control))
        {
            for (var i = 0; i < _statButtonFrames.Length && i < control.Images.Count; i++)
            {
                _statButtonFrames[i] = _assets.PaletteFrame(control.Images[i].EntryName, control.Images[i].Frame);
            }
        }
    }

    private void ApplyStatButtons(GuiLayout? layout, in PixelRect status)
    {
        Array.Clear(_statButtons);

        if (!GuiLayoutGeometry.TryRect(layout, FirstStatButtonControl, out var first))
        {
            return;
        }

        first = first.Translate(-status.X, -status.Y);
        _statButtons[0] = first;

        if (!GuiLayoutGeometry.TryRect(layout, SecondStatButtonControl, out var second))
        {
            return;
        }

        second = second.Translate(-status.X, -status.Y);
        var stepX = second.X - first.X;
        var stepY = second.Y - first.Y;

        for (var i = 1; i < StatButtonCount; i++)
        {
            _statButtons[i] = first.Translate(stepX * i, stepY * i);
        }
    }

    private Pixmap ArtOf(GuiLayout? layout, string controlName) =>
        layout is not null && layout.TryFind(controlName, out var control) && control.Images.Count > 0
            ? _assets.Frame(control.Images[0].EntryName, control.Images[0].Frame)
            : default;

    private void ApplyValues()
    {
        var attributes = _player.Attributes;
        SetNumber(Strength, attributes.Strength);
        SetNumber(Intelligence, attributes.Intelligence);
        SetNumber(Wisdom, attributes.Wisdom);
        SetNumber(Constitution, attributes.Constitution);
        SetNumber(Dexterity, attributes.Dexterity);

        var vitals = _player.Vitals;
        SetNumber(Health, vitals.Health);
        SetNumber(MaxHealth, vitals.MaxHealth);
        SetNumber(Mana, vitals.Mana);
        SetNumber(MaxMana, vitals.MaxMana);

        var progression = _player.Progression;
        SetNumber(Experience, progression.TotalExperience);
        SetNumber(Gold, progression.Gold);
        SetNumber(GamePoints, progression.GamePoints);
        SetNumber(Level, progression.Level);
        SetNumber(ToNextLevel, progression.ToNextLevel);
        SetNumber(AbilityLevel, progression.AbilityLevel);
        SetNumber(ToNextAbility, progression.ToNextAbility);
        SetNumber(TotalAbility, progression.TotalAbility);

        var modifiers = _player.Modifiers;
        SetElement(AttackElement, modifiers.AttackElement);
        SetElement(DefenseElement, modifiers.DefenseElement);
        SetMagicResistance(modifiers.MagicResistUnits);
        SetNumber(ArmorClass, modifiers.ArmorClass);
        SetNumber(DamageModifier, modifiers.DamageModifier);
        SetNumber(HitModifier, modifiers.HitModifier);
    }

    private void SetNumber(int index, uint value)
    {
        var text = new TextBuffer(_values.AsSpan(index * ValueBytes, ValueBytes));
        text.Append(value);
        _lengths[index] = text.Length;
    }

    private void SetNumber(int index, int value)
    {
        var text = new TextBuffer(_values.AsSpan(index * ValueBytes, ValueBytes));
        text.Append(value);
        _lengths[index] = text.Length;
    }

    private void SetElement(int index, Element element)
    {
        var name = (uint)element < (uint)ElementNames.Length ? ElementNames[(int)element] : ElementNames[0];
        var text = new TextBuffer(_values.AsSpan(index * ValueBytes, ValueBytes));
        text.Append(name.AsSpan());
        _lengths[index] = text.Length;
    }

    private void SetMagicResistance(byte units)
    {
        var text = new TextBuffer(_values.AsSpan(MagicResistance * ValueBytes, ValueBytes));
        text.Append(units * 10);
        text.Append('%');
        _lengths[MagicResistance] = text.Length;
    }
}
