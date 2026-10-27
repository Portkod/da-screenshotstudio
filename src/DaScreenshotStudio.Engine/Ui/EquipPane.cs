using DaScreenshotStudio.Engine.Rendering;
using DaScreenshotStudio.Engine.Text;
using DaScreenshotStudio.Engine.World;
using DaScreenshotStudio.Engine.Core;
using DaScreenshotStudio.Engine.Records;

namespace DaScreenshotStudio.Engine.Ui;

public sealed class EquipPane : LayoutPane
{
    public override bool IsMovable => false;

    public const string LayoutName = "_nui_eq.txt";

    public const string OtherLayoutName = "_nui_eqa.txt";

    public const int SlotCount = PlayerModel.EquipmentCapacity;

    public const string StateIconEntry = "_nemots.spf";

    public const string NationEntry = "_nui_nat.spf";

    public const string GroupControl = "GroupBtn";

    public const string GroupClosedControl = "GroupBtn_Disabled";

    public const string PortraitControl = "Portrait";

    public const byte PortraitFillIndex = TextPalette.ShadowIndex;

    public const string ProfileTextControl = "PortraitText";

    public const uint GroupPressedMs = 0x1F4;

    public const string NationMetafile = "NationDesc";

    public const int FigureAnchorInsetY = 0x14;

    public const int StateLabelGap = 2;

    public const int TooltipOffsetY = 0x0F;

    private readonly DescriptionPane _tooltipPane;

    private static readonly string[] SlotControls =
    [
        "WEAPON", "ARMOR", "SHIELD", "HEAD", "EAR", "NECK", "LHAND", "RHAND", "LARM",
        "RARM", "BELT", "LEG", "FOOT", "CAPE", "ARMOR2", "HEAD2", "CAPE2", "CAPE3",
    ];

    private readonly PlayerModel _player;
    private readonly bool _forUser;
    private readonly PixelRect[] _slotRects = new PixelRect[SlotCount];
    private readonly Pixmap[] _slotPlaceholders = new Pixmap[SlotCount];
    private readonly byte[] _numbers = new byte[16];

    private readonly ushort[] _wornSprites = new ushort[SlotCount];

    private readonly byte[] _wornDyes = new byte[SlotCount];

    private readonly byte[] _subjectName = new byte[SlotRecord.NameLength];
    private int _subjectNameLength;

    private readonly GuiTextPane? _name;
    private readonly GuiTextPane? _title;
    private readonly GuiTextPane? _class;
    private readonly GuiTextPane? _clan;
    private readonly GuiTextPane? _clanTitle;
    private readonly TextBlock _state;
    private readonly GuiTextPane? _nationText;
    private readonly GuiTextPane? _portraitText;
    private readonly GuiTextPane?[] _stats = new GuiTextPane[6];
    private readonly byte[] _profile = new byte[ProfileTextFile.MaxBytes];
    private readonly byte[] _profileDisplay = new byte[ProfileTextFile.MaxBytes];
    private int _profileLength;

    private readonly PixelRect _figureRect;
    private readonly PixelRect _stateRect;
    private readonly PixelRect _stateIconRect;
    private readonly PixelRect _nationRect;

    private readonly PixelRect _groupRect;
    private readonly PixelRect _portraitTextRect;
    private readonly PixelRect _portraitRect;

    private readonly PortraitPicture _portrait = new();

    private readonly IconFrame[] _groupFrames = new IconFrame[4];

    private bool _groupOpen;
    private bool _groupArmed;
    private int _groupFrame;
    private uint _groupPressedAt;
    private uint _now;

    private ClientWorldArt? _art;
    private HumanState _figure;
    private bool _hasFigure;

    private Nation _nation;

    private UserState _userState;

    private int _revision = -1;
    private int _statRevision = -1;
    private int _hovered;

    public EquipPane(
        PixelBlender blender,
        UiAssets assets,
        PlayerModel player,
        DescriptionPane description,
        bool forUser = true)
        : base(assets, blender, LayoutFor(forUser), Placement(assets, forUser))
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(description);

        _player = player;
        _forUser = forUser;
        Visible = false;

        ConsumesPointerInside = true;

        _tooltipPane = description;

        for (var i = 0; i < SlotCount; i++)
        {
            if (Layout is not null && Layout.TryFind(SlotControls[i], out var control))
            {
                _slotRects[i] = GuiLayoutGeometry.Rect(control);
                _slotPlaceholders[i] = Assets.Frame(control, 0);
            }
        }

        GuiLayoutGeometry.TryRect(Layout, "HumanImage", out _figureRect);
        GuiLayoutGeometry.TryRect(Layout, "HumanIcon", out _stateIconRect);
        GuiLayoutGeometry.TryRect(Layout, "Nation", out _nationRect);
        GuiLayoutGeometry.TryRect(Layout, PortraitControl, out _portraitRect);

        _name = AddLabel("NAME", TextAlignment.Left, paletteIndex: TextPalette.WhiteIndex);
        _title = AddLabel("TITLETEXT", TextAlignment.Left, paletteIndex: TextPalette.WhiteIndex);
        _class = AddLabel("CLASSTEXT", TextAlignment.Left, paletteIndex: TextPalette.WhiteIndex);
        _clan = AddLabel("CLANTEXT", TextAlignment.Left, paletteIndex: TextPalette.WhiteIndex);
        _clanTitle = AddLabel("CLANTITLETEXT", TextAlignment.Left, paletteIndex: TextPalette.WhiteIndex);

        _stats[0] = AddLabel("N_STR", TextAlignment.Right, paletteIndex: TextPalette.WhiteIndex);
        _stats[1] = AddLabel("N_INT", TextAlignment.Right, paletteIndex: TextPalette.WhiteIndex);
        _stats[2] = AddLabel("N_WIS", TextAlignment.Right, paletteIndex: TextPalette.WhiteIndex);
        _stats[3] = AddLabel("N_CON", TextAlignment.Right, paletteIndex: TextPalette.WhiteIndex);
        _stats[4] = AddLabel("N_DEX", TextAlignment.Right, paletteIndex: TextPalette.WhiteIndex);
        _stats[5] = AddLabel("N_AC", TextAlignment.Right, paletteIndex: TextPalette.WhiteIndex);

        GuiLayoutGeometry.TryRect(Layout, "HumanState", out _stateRect);
        _state = new TextBlock(Assets.Font, Assets.Palette, Math.Max(1, _stateRect.Width), 1)
        {
            Alignment = TextAlignment.Center,
            Wraps = false,
            DefaultPaletteIndex = TextPalette.WhiteIndex,
        };

        GuiLayoutGeometry.TryRect(Layout, "NationText", out var nationTextRect);
        _nationText = AddLabel(
            "NationText",
            TextAlignment.Left,
            Math.Max(1, nationTextRect.Height / Assets.Font.LineHeight),
            TextPalette.WhiteIndex);

        GuiLayoutGeometry.TryRect(Layout, ProfileTextControl, out _portraitTextRect);
        _portraitText = AddLabel(
            ProfileTextControl,
            TextAlignment.Left,
            Math.Max(1, _portraitTextRect.Height / Assets.Font.LineHeight),
            TextPalette.WhiteIndex);

        GuiLayoutGeometry.TryRect(Layout, GroupControl, out _groupRect);
        LoadGroupFrame(0, GroupControl, 0);
        LoadGroupFrame(1, GroupControl, 1);
        LoadGroupFrame(2, GroupClosedControl, 0);
        LoadGroupFrame(3, GroupClosedControl, 1);

        ApplyState();
        ApplyNationText();
    }

    private void LoadGroupFrame(int index, string controlName, int imageIndex)
    {
        if (Layout is null
            || !Layout.TryFind(controlName, out var control)
            || (uint)imageIndex >= (uint)control.Images.Count)
        {
            return;
        }

        var image = control.Images[imageIndex];
        _groupFrames[index] = Assets.PlacedFrame(image.EntryName, image.Frame);
    }

    public static string LayoutFor(bool forUser) => forUser ? LayoutName : OtherLayoutName;

    private static PixelRect Placement(UiAssets assets, bool forUser)
    {
        var content = UserInfoShell.Content(assets);
        return content.IsEmpty ? Place(assets, LayoutFor(forUser)) : content;
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

    public Action? Closed { get; set; }

    public int SlotAt(int x, int y)
    {
        if (!_forUser)
        {
            return 0;
        }

        for (var i = 0; i < SlotCount; i++)
        {
            if (_slotRects[i].Contains(x, y) && _player.Equipment[i + 1].IsOccupied)
            {
                return i + 1;
            }
        }

        return 0;
    }

    private ushort WornSprite(int index) =>
        _forUser
            ? _player.Equipment[index + 1] is { IsOccupied: true } record ? record.Sprite : (ushort)0
            : _wornSprites[index];

    public byte WornDye(int index) =>
        (uint)index >= (uint)SlotCount
            ? (byte)0
            : _forUser
                ? _player.Equipment[index + 1].DyeColor
                : _wornDyes[index];

    public PixelRect SlotRect(EquipmentSlot slot) =>
        (uint)((int)slot - 1) < (uint)SlotCount ? _slotRects[(int)slot - 1] : PixelRect.Empty;

    public void Show()
    {
        if (Visible)
        {
            return;
        }

        Visible = true;
        _revision = -1;
        _statRevision = -1;
        Invalidate();
    }

    public void Close()
    {
        if (!Visible)
        {
            return;
        }

        Hide();
        Closed?.Invoke();
    }

    public void Hide()
    {
        if (!Visible)
        {
            return;
        }

        Visible = false;
        _hovered = 0;
        _tooltipPane.Hide(this);
        Invalidate();
    }

    public void Refresh()
    {
        if (!Visible || !_forUser)
        {
            return;
        }

        if (_revision != _player.Equipment.Revision)
        {
            _revision = _player.Equipment.Revision;
            Invalidate();
        }

        if (_statRevision == _player.Revision)
        {
            return;
        }

        _statRevision = _player.Revision;
        ApplyStats();
    }

    private void ApplyStats()
    {
        var attributes = _player.Attributes;
        SetNumber(_stats[0], attributes.Strength);
        SetNumber(_stats[1], attributes.Intelligence);
        SetNumber(_stats[2], attributes.Wisdom);
        SetNumber(_stats[3], attributes.Constitution);
        SetNumber(_stats[4], attributes.Dexterity);
        SetNumber(_stats[5], _player.Modifiers.ArmorClass);
    }

    private void SetNumber(GuiTextPane? label, int value)
    {
        if (label is null)
        {
            return;
        }

        var text = new TextBuffer(_numbers);
        text.Append(value);
        label.SetText(text.Written);
    }

    public void SetName(scoped ReadOnlySpan<byte> name)
    {
        _subjectNameLength = Math.Min(name.Length, _subjectName.Length);
        name[.._subjectNameLength].CopyTo(_subjectName);
        _name?.SetText(name);
    }

    public EntityId Subject { get; private set; }

    public void ApplyObjectInfo(ObjectInfo info)
    {
        Subject = info.Entity;

        Array.Clear(_wornSprites);
        Array.Clear(_wornDyes);

        foreach (var worn in info.Equipment)
        {
            var index = (int)worn.Slot - 1;
            if ((uint)index >= (uint)SlotCount)
            {
                continue;
            }

            _wornSprites[index] = worn.Sprite;
            _wornDyes[index] = worn.DyeColor;
        }

        SetUserState(info.State);
        SetName(info.Name);
        SetNation(info.Nation);
        SetProfile(info.Title, info.DisplayClass, info.Guild, info.GuildRank);
        SetGroupOpen(info.IsGroupOpen);

        SetProfileText(info.Biography);
        SetPortrait(info.Portrait);
        Invalidate();
    }

    public void SetFigure(in HumanState figure)
    {
        if (_hasFigure && _figure.Equals(figure))
        {
            return;
        }

        _figure = figure;
        _hasFigure = !figure.IsEmpty;
        Invalidate();
    }

    public void SetNation(Nation nation)
    {
        if (_nation == nation)
        {
            return;
        }

        _nation = nation;
        ApplyNationText();
        Invalidate();
    }

    public void SetUserState(UserState state)
    {
        if (_userState == state)
        {
            return;
        }

        _userState = state;
        ApplyState();
        Invalidate();
    }

    private void ApplyState() => _state.SetText(UserStateLabels.Detail(Assets, _userState));

    public bool IsGroupOpen => _groupOpen;

    public void SetGroupOpen(bool open)
    {
        _groupOpen = open;

        var frame = open ? 0 : 2;
        if (_groupFrame == frame)
        {
            return;
        }

        _groupFrame = frame;
        Invalidate();
    }

    public void Tick(uint now)
    {
        _now = now;

        if ((_groupFrame & 1) == 0 || now - _groupPressedAt < GroupPressedMs)
        {
            return;
        }

        _groupFrame &= ~1;
        Invalidate();
    }

    private void HandleGroupButton(in PointerEvent pointer)
    {
        var inside = !_groupRect.IsEmpty && _groupRect.Contains(pointer.X, pointer.Y);

        if (pointer.Type == UiEventType.LeftDown)
        {
            _groupArmed = inside;
            return;
        }

        if (pointer.Type == UiEventType.LeftDoubleClick)
        {
            return;
        }

        if (!_groupArmed)
        {
            return;
        }

        _groupArmed = false;

        if (!inside)
        {
            return;
        }

        _groupFrame |= 1;
        _groupPressedAt = _now;
        Invalidate();
    }

    public void SetProfileText(scoped ReadOnlySpan<byte> text)
    {
        _profileLength = Math.Min(text.Length, ProfileTextFile.MaxBytes);
        text[.._profileLength].CopyTo(_profile);
        _portraitText?.SetText(_profileDisplay.AsSpan(0, ProfileTextFile.ToDisplay(ProfileText, _profileDisplay)));
    }

    public ReadOnlySpan<byte> ProfileText => _profile.AsSpan(0, _profileLength);

    public void SetPortrait(scoped ReadOnlySpan<byte> file)
    {
        if (file.IsEmpty || !_portrait.TrySet(file, Assets.Packing, Assets.Palette))
        {
            _portrait.Clear();
        }

        Invalidate();
    }

    public bool HasPortrait => _portrait.HasPicture;

    public UserState UserState => _userState;

    private void ApplyNationText()
    {
        if (_nationText is null)
        {
            return;
        }

        Span<char> name = stackalloc char[16];
        "nation_".CopyTo(name);
        ((int)_nation).TryFormat(name["nation_".Length..], out var digits);
        _nationText.SetText(NationName(name[..("nation_".Length + digits)]));
    }

    private ReadOnlySpan<byte> NationName(scoped ReadOnlySpan<char> record) =>
        Assets.Metafiles.Find(NationMetafile) is { } file
        && file.TryFindGroup(record, out var group)
        && file.GetValueCount(group) > 0
            ? file.GetValue(group, 0)
            : _nationName;

    private byte[] _nationName = [];

    public void SetNationName(scoped ReadOnlySpan<byte> name)
    {
        _nationName = name.ToArray();
        ApplyNationText();
        Invalidate();
    }

    public void SetProfile(
        scoped ReadOnlySpan<byte> title,
        scoped ReadOnlySpan<byte> displayClass,
        scoped ReadOnlySpan<byte> guild,
        scoped ReadOnlySpan<byte> guildRank)
    {
        _title?.SetText(title);
        _class?.SetText(displayClass);
        _clan?.SetText(guild);
        _clanTitle?.SetText(guildRank);
    }

    protected override void OnCancel() => Close();

    protected override bool OnPointer(in PointerEvent pointer)
    {
        if (!Visible)
        {
            return false;
        }

        if (pointer.Type is UiEventType.LeftDown or UiEventType.LeftUp or UiEventType.LeftDoubleClick)
        {
            HandleGroupButton(in pointer);

            return false;
        }

        if (pointer.Type != UiEventType.PointerMove)
        {
            return false;
        }

        var slot = SlotAt(pointer.X, pointer.Y);
        if (slot == _hovered && slot == 0)
        {
            return false;
        }

        _hovered = slot;

        if (slot == 0)
        {
            _tooltipPane.Hide(this);
        }
        else
        {
            _tooltipPane.Show(
                this,
                Root,
                Bounds.X + pointer.X,
                Bounds.Y + pointer.Y + TooltipOffsetY,
                _player.Equipment[slot].Name);
        }

        Invalidate();

        return slot != 0;
    }

    protected override void OnDrawContent(Canvas canvas)
    {
        base.OnDrawContent(canvas);

        if (!Visible)
        {
            return;
        }

        DrawFigure(canvas);

        var icons = Assets.ItemIcons;

        for (var i = 0; i < SlotCount; i++)
        {
            var rect = _slotRects[i];
            if (rect.IsEmpty)
            {
                continue;
            }

            var sprite = WornSprite(i);
            if (sprite == 0)
            {
                Draw(canvas, in rect, _slotPlaceholders[i]);
                continue;
            }

            var icon = icons?.Frame(sprite, WornDye(i)) ?? default;
            if (icon.IsEmpty)
            {
                Draw(canvas, in rect, _slotPlaceholders[i]);
                continue;
            }

            canvas.BlitPixmap(rect.X + icon.Left, rect.Y + icon.Top, in icon.Image, BlendMode.Transparent);
        }

        DrawPortrait(canvas);
        DrawNation(canvas);
        DrawStateIcon(canvas);
        DrawStateLabel(canvas);
        DrawGroupButton(canvas);
    }

    private void DrawGroupButton(Canvas canvas)
    {
        if (_groupRect.IsEmpty || (uint)_groupFrame >= (uint)_groupFrames.Length)
        {
            return;
        }

        var frame = _groupFrames[_groupFrame];
        if (!frame.IsEmpty)
        {
            canvas.BlitPixmap(
                _groupRect.X + frame.Left, _groupRect.Y + frame.Top, in frame.Image, BlendMode.Transparent);
        }
    }

    private void DrawStateLabel(Canvas canvas)
    {
        if (_stateRect.IsEmpty)
        {
            return;
        }

        _state.DefaultPaletteIndex = DrawsPortrait ? PortraitFillIndex : TextPalette.WhiteIndex;

        var floor = _stateIconRect.IsEmpty ? int.MinValue : _stateIconRect.Right + StateLabelGap;
        _state.DrawIn(canvas, in _stateRect, floor);
    }

    private bool DrawsPortrait => _portrait.HasPicture && !_portraitRect.IsEmpty;

    private void DrawFigure(Canvas canvas)
    {
        if (!_hasFigure || _figureRect.IsEmpty || _art is not { } art)
        {
            return;
        }

        if (!art.TryGetHuman(
            in _figure,
            Direction.Down,
            HumanMotion.StandingMotionIndex,
            0,
            HumanComposite.StandingFramesPerView,
            -1,
            out var sprite)
            || sprite.IsEmpty)
        {
            return;
        }

        var anchorX = (_figureRect.Right + _figureRect.X) / 2;
        var anchorY = _figureRect.Bottom - FigureAnchorInsetY;
        var figure = sprite.Art;

        canvas.BlitPixmap(anchorX - sprite.AnchorX, anchorY - sprite.AnchorY, in figure, BlendMode.Transparent);
    }

    private void DrawPortrait(Canvas canvas)
    {
        if (!DrawsPortrait)
        {
            return;
        }

        var picture = _portrait.Picture;
        var box = new PixelRect(_portraitRect.X, _portraitRect.Y, picture.Width, picture.Height);

        canvas.FillRect(in box, Assets.Palette[PortraitFillIndex], BlendMode.Copy);
        canvas.BlitPixmap(box.X, box.Y, in picture, BlendMode.Transparent);
    }

    private void DrawNation(Canvas canvas)
    {
        if (_nation == Nation.None || _nationRect.IsEmpty)
        {
            return;
        }

        var frame = Assets.PlacedFrame(NationEntry, (int)_nation - 1);
        if (!frame.IsEmpty)
        {
            canvas.BlitPixmap(
                _nationRect.X + frame.Left, _nationRect.Y + frame.Top, in frame.Image, BlendMode.Transparent);
        }
    }

    private void DrawStateIcon(Canvas canvas)
    {
        if (_stateIconRect.IsEmpty)
        {
            return;
        }

        var frame = Assets.PlacedFrame(StateIconEntry, (int)_userState);
        if (!frame.IsEmpty)
        {
            canvas.BlitPixmap(
                _stateIconRect.X + frame.Left,
                _stateIconRect.Y + frame.Top,
                in frame.Image,
                BlendMode.Transparent);
        }
    }

    private static void Draw(Canvas canvas, in PixelRect rect, Pixmap art)
    {
        if (!art.IsEmpty)
        {
            canvas.BlitPixmap(rect.X, rect.Y, in art, BlendMode.Transparent);
        }
    }
}
