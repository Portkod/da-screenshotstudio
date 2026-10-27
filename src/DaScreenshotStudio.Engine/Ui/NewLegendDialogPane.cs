using DaScreenshotStudio.Engine.Rendering;
using DaScreenshotStudio.Assets.Fonts;
using DaScreenshotStudio.Engine.Records;

namespace DaScreenshotStudio.Engine.Ui;

public sealed class NewLegendDialogPane : LayoutPane
{
    public const string LayoutName = "llegend2.txt";

    public const string BackgroundControl = "Noname";

    public const string PortraitControl = "Portrait";

    public const string PortraitPictureControl = "PortraitPic";

    public const string ProfileControl = "ProfileText";

    public const string ListControl = "LegendList";

    public const string ChangeControl = "Change";

    public const int MaxProfileBytes = 0x172;

    public const int MaxProfileLines = 5;

    public const byte DefaultProfileInk = 20;

    private readonly bool _ownProfile;
    private readonly LftCodePage _codePage;
    private readonly PixelRect _portraitRect;
    private readonly Pixmap _placeholder;
    private readonly LegendListPane? _list;
    private readonly ScrollBarPane? _bar;
    private readonly ProfileTextEditPane? _profile;
    private readonly ImageButtonPane? _change;
    private readonly ImagePane? _received;

    private readonly byte[] _text = new byte[MaxProfileBytes + 1];
    private int _textLength;
    private bool _hasPortrait;

    public NewLegendDialogPane(UiAssets assets, PixelBlender blender, bool ownProfile, PixelRect? bounds = null)
        : base(assets, blender, LayoutName, bounds ?? Placement(assets))
    {
        ArgumentNullException.ThrowIfNull(assets);

        _ownProfile = ownProfile;
        ConsumesPointerAnywhere = true;
        _codePage = assets.Font.CodePage;

        GuiLayoutGeometry.TryRect(Layout, PortraitControl, out _portraitRect);
        _placeholder = ControlFrame(PortraitControl, 0);

        if (GuiLayoutGeometry.TryRect(Layout, PortraitPictureControl, out var picture))
        {
            _received = new ImagePane(in picture, blender) { Visible = false };
            Add(_received);
        }

        BuildRoll(assets, blender, out _list, out _bar);

        if (GuiLayoutGeometry.TryRect(Layout, ProfileControl, out var profileBox))
        {
            _profile = new ProfileTextEditPane(
                in profileBox,
                blender,
                assets.Font,
                assets.Palette,
                ProfileInk(Layout))
            {
                IsReadOnly = !ownProfile,
            };

            Add(_profile);
        }

        if (ownProfile && GuiLayoutGeometry.TryRect(Layout, ChangeControl, out var changeBox))
        {
            _change = new ImageButtonPane(in changeBox, blender);
            _change.SetFrames(
                ControlFrame(ChangeControl, 0),
                ControlFrame(ChangeControl, 1),
                ControlFrame(ChangeControl, 2));
            _change.Activated = _ => ApplyChange();
            Add(_change);
        }
    }

    public bool IsOwnProfile => _ownProfile;

    public LegendListPane? List => _list;

    public ScrollBarPane? Bar => _bar;

    public ProfileTextEditPane? Profile => _profile;

    public ImageButtonPane? Change => _change;

    public bool HasPortrait => _hasPortrait;

    public ReadOnlySpan<byte> ProfileText => _text.AsSpan(0, _textLength);

    public static PixelRect Placement(UiAssets assets)
    {
        ArgumentNullException.ThrowIfNull(assets);

        return GuiLayoutGeometry.TryRect(assets.Layout(LayoutName), BackgroundControl, out var window)
            ? window
            : new PixelRect(0, 0, GuiBackPane.ScreenWidth, GuiBackPane.ScreenHeight);
    }

    public void SetMarks(ReadOnlySpan<LegendMark> marks)
    {
        _list?.SetMarks(marks);
        Invalidate();
    }

    public void SetProfile(in Pixmap portrait, scoped ReadOnlySpan<byte> text)
    {
        _textLength = Math.Min(text.Length, MaxProfileBytes);
        text[.._textLength].CopyTo(_text);
        _text[_textLength] = 0;

        _profile?.SetValue(ProfileText);

        _hasPortrait = !portrait.IsEmpty;
        if (_received is not null)
        {
            _received.Image = portrait;
            _received.Visible = _hasPortrait;
        }

        Invalidate();
    }

    public ReadOnlySpan<byte> ApplyChange()
    {
        if (_profile is not null)
        {
            var value = _profile.Value;
            _textLength = Math.Min(value.Length, MaxProfileBytes);
            value[.._textLength].CopyTo(_text);
        }

        _textLength = FoldAndCutLines(_text.AsSpan(0, _textLength), _codePage);
        _text[_textLength] = 0;

        Invalidate();
        return ProfileText;
    }

    public static int FoldAndCutLines(Span<byte> text, LftCodePage codePage)
    {
        var breaks = 0;

        for (var i = 0; i < text.Length; i++)
        {
            if (codePage.IsLeadByte(text[i]) && i + 1 < text.Length)
            {
                i++;
                continue;
            }

            if (text[i] == (byte)'\r')
            {
                text[i] = (byte)'\n';
            }

            if (text[i] != (byte)'\n')
            {
                continue;
            }

            if (++breaks >= MaxProfileLines)
            {
                return i;
            }
        }

        return text.Length;
    }

    protected override void OnDrawContent(Canvas canvas)
    {
        base.OnDrawContent(canvas);

        if (_hasPortrait || _placeholder.IsEmpty)
        {
            return;
        }

        canvas.BlitPixmap(_portraitRect.X, _portraitRect.Y, in _placeholder, BlendMode.Transparent);
    }

    private void BuildRoll(UiAssets assets, PixelBlender blender, out LegendListPane? list, out ScrollBarPane? bar) =>
        TryBuildScrolledControl(
            ListControl, content => new LegendListPane(in content, blender, assets), out list, out bar);

    private static byte ProfileInk(Assets.Gui.GuiLayout? layout) =>
        layout is not null && layout.TryFind(ProfileControl, out var control) && control.Colors.Count > 0
            ? (byte)control.Colors[0]
            : DefaultProfileInk;
}
