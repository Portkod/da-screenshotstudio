using DaScreenshotStudio.Assets.Gui;
using DaScreenshotStudio.Engine.App;
using DaScreenshotStudio.Engine.Rendering;

namespace DaScreenshotStudio.Engine.Ui;

public sealed class OptionPane : LayoutPane
{
    public override bool IsMovable => false;

    public const string LayoutName = "_noptdlg.txt";

    public const string AnchorLayoutName = "lback.txt";

    public const string AnchorControlName = "OptionRect";

    public const int CloseAction = 0;

    public const int FriendsAction = 1;

    public const int MacroAction = 2;

    public const int SettingAction = 3;

    public const int ExitGameAction = 4;

    public const int MaxVolumeLevel = 10;

    public const string TickControlName = "Tick";

    public const string SoundControlName = "SoundRect";

    public const string MusicControlName = "MusicRect";

    public const int SlideSteps = 3;

    public const uint SlideStepMs = 100;

    public const int SlideRight = 0x26A;

    public const int SlideTop = 2;

    private readonly ImageButtonPane?[] _rows = new ImageButtonPane?[5];
    private readonly Pixmap _tick;
    private readonly PixelRect _soundTrack;
    private readonly PixelRect _musicTrack;

    private readonly int _slideWidth;
    private readonly int _slideHeight;
    private int _soundLevel;
    private int _musicLevel;

    private bool _soundArmed;

    private bool _musicArmed;
    private int _phase;
    private bool _opening;
    private bool _stepPending;
    private uint _stepDue;
    private uint _now;

    public OptionPane(UiAssets assets, PixelBlender blender)
        : base(assets, blender, LayoutName, Anchor(assets, LayoutName))
    {
        ConsumesPointerAnywhere = true;
        Visible = false;

        if (Layout is { } layout
            && layout.TryFind(TickControlName, out var tickControl)
            && tickControl.Images.Count > 0)
        {
            var art = tickControl.Images[0];
            _tick = art.EntryName.EndsWith(".epf", StringComparison.OrdinalIgnoreCase)
                ? assets.PaletteFrame(art.EntryName, art.Frame)
                : assets.Frame(art.EntryName, art.Frame);
        }

        var placed = Place(assets, LayoutName);
        _slideWidth = placed.Width;
        _slideHeight = placed.Height;

        GuiLayoutGeometry.TryRect(Layout, TickControlName, out var tick);
        GuiLayoutGeometry.TryRect(Layout, SoundControlName, out var sound);

        var offsetX = sound.X - tick.X;
        var offsetY = sound.Y - tick.Y;

        _soundTrack = Track(Layout, SoundControlName, in tick, offsetX, offsetY);
        _musicTrack = Track(Layout, MusicControlName, in tick, offsetX, offsetY);

        _rows[CloseAction] = AddRow("CLOSE", CloseAction);
        _rows[FriendsAction] = AddRow("Friends", FriendsAction);
        _rows[MacroAction] = AddRow("Macro", MacroAction);
        _rows[SettingAction] = AddRow("Setting", SettingAction);
        _rows[ExitGameAction] = AddRow("ExitGame", ExitGameAction);
    }

    public int SoundLevel => _soundLevel;

    public int MusicLevel => _musicLevel;

    public ImageButtonPane? Row(int action) =>
        (uint)action < (uint)_rows.Length ? _rows[action] : null;

    public PixelRect TickRect(in PixelRect track, int level)
    {
        var span = Math.Max(0, track.Width - Math.Max(1, _tick.Width));
        var offset = MaxVolumeLevel <= 0 ? 0 : span * Math.Clamp(level, 0, MaxVolumeLevel) / MaxVolumeLevel;
        return new PixelRect(track.X + offset, track.Y, Math.Max(1, _tick.Width), Math.Max(1, _tick.Height));
    }

    public int SlidePhase => _phase;

    public bool IsSliding => _stepPending;

    public PixelRect SlideBounds(int phase)
    {
        var left = phase >= SlideSteps
            ? SlideRight - _slideWidth
            : SlideRight - (_slideWidth / SlideSteps) * phase;

        return PixelRect.FromEdges(left, SlideTop, SlideRight, _slideHeight);
    }

    public void Open(int soundLevel, int musicLevel)
    {
        _soundLevel = Math.Clamp(soundLevel, 0, MaxVolumeLevel);
        _musicLevel = Math.Clamp(musicLevel, 0, MaxVolumeLevel);
        Visible = true;
        BeginSlide(opening: true, phase: 1);
    }

    public void Close()
    {
        _stepPending = false;
        _phase = SlideSteps;
        SetBounds(SlideBounds(SlideSteps));
        Forget();

        if (!Visible)
        {
            return;
        }

        Visible = false;
        Invalidate();
    }

    public bool Tick(uint now)
    {
        _now = now;

        if (!_stepPending || !GameClock.IsDue(now, _stepDue))
        {
            return false;
        }

        _stepPending = false;
        _phase += _opening ? 1 : -1;

        if (_phase > 0 && _phase < SlideSteps)
        {
            Arm();
        }

        if (_phase == 0)
        {
            Vanish();
        }

        SetBounds(SlideBounds(_phase));
        Invalidate();
        return true;
    }

    public void Answer(int action)
    {
        if (!Visible)
        {
            return;
        }

        if (action == CloseAction)
        {
            BeginSlide(opening: false, phase: 2);
        }

        Invalidate();
    }

    private void Vanish()
    {
        Visible = false;
        Forget();
    }

    private void Forget()
    {
        ResetFocus();

        for (var i = 0; i < _rows.Length; i++)
        {
            _rows[i]?.ResetState();
        }
    }

    private void BeginSlide(bool opening, int phase)
    {
        _opening = opening;
        _phase = phase;
        Arm();
        SetBounds(SlideBounds(phase));
        Invalidate();
    }

    private void Arm()
    {
        _stepPending = true;
        _stepDue = unchecked(_now + SlideStepMs);
    }

    protected override void OnCancel() => Answer(CloseAction);

    protected override bool OnKey(in KeyEvent key)
    {
        if (!Visible)
        {
            return false;
        }

        if (key.Type != UiEventType.KeyDown)
        {
            return base.OnKey(in key);
        }

        switch (key.Key)
        {
            case 'X':
                return true;

            case 'Q':
                Answer(CloseAction);
                return true;

            case 'W' or 'E' or 'R' or UiKeys.F7:
                Answer(CloseAction);
                return false;

            case '1':
                Answer(SettingAction);
                return true;

            case UiKeys.Escape or UiKeys.Enter or UiKeys.Space:
                Answer(CloseAction);
                return true;

            default:
                return base.OnKey(in key);
        }
    }

    public int LevelAt(in PixelRect track, int x)
    {
        var span = Math.Max(0, track.Width - Math.Max(1, _tick.Width));
        if (span == 0)
        {
            return 0;
        }

        var half = (track.Width - span) / 2;
        var start = track.X + half;

        if (x < start)
        {
            return 0;
        }

        if (x >= start + span)
        {
            return MaxVolumeLevel;
        }

        return (x - start) * MaxVolumeLevel / span;
    }

    protected override bool OnPointer(in PointerEvent pointer)
    {
        if (!Visible)
        {
            return false;
        }

        switch (pointer.Type)
        {
            case UiEventType.LeftDown:
                _soundArmed = _soundTrack.Contains(pointer.X, pointer.Y);
                _musicArmed = _musicTrack.Contains(pointer.X, pointer.Y);
                break;

            case UiEventType.PointerMove:
                if (_soundArmed)
                {
                    ApplySound(pointer.X);
                }
                else if (_musicArmed)
                {
                    ApplyMusic(pointer.X);
                }

                break;

            case UiEventType.LeftUp:
                if (_soundArmed)
                {
                    _soundArmed = false;
                    ApplySound(pointer.X);
                }
                else if (_musicArmed)
                {
                    _musicArmed = false;
                    ApplyMusic(pointer.X);
                }

                break;
        }

        return base.OnPointer(in pointer);
    }

    private void ApplySound(int x)
    {
        var level = LevelAt(in _soundTrack, x);
        if (level == _soundLevel)
        {
            return;
        }

        _soundLevel = level;
        Invalidate();
    }

    private void ApplyMusic(int x)
    {
        var level = LevelAt(in _musicTrack, x);
        if (level == _musicLevel)
        {
            return;
        }

        _musicLevel = level;
        Invalidate();
    }

    protected override void OnDrawContent(Canvas canvas)
    {
        base.OnDrawContent(canvas);

        if (_tick.IsEmpty)
        {
            return;
        }

        var sound = TickRect(in _soundTrack, _soundLevel);
        var music = TickRect(in _musicTrack, _musicLevel);
        canvas.BlitPixmap(sound.X, sound.Y, in _tick, BlendMode.Transparent);
        canvas.BlitPixmap(music.X, music.Y, in _tick, BlendMode.Transparent);
    }

    private ImageButtonPane? AddRow(string controlName, int action)
    {
        var pane = AddButton(controlName, _ => Answer(action));
        if (pane is not null)
        {
            pane.ActionId = action;
        }

        return pane;
    }

    private static PixelRect Track(
        GuiLayout? layout, string controlName, in PixelRect tick, int offsetX, int offsetY)
    {
        if (!GuiLayoutGeometry.TryRect(layout, controlName, out var track))
        {
            return default;
        }

        return new PixelRect(
            track.X - offsetX,
            track.Y - offsetY,
            track.Width + tick.Width,
            Math.Max(1, tick.Height));
    }

    private static PixelRect Anchor(UiAssets assets, string layoutName)
    {
        ArgumentNullException.ThrowIfNull(assets);

        var placed = Place(assets, layoutName);

        if (!GuiLayoutGeometry.TryRect(assets.Layout(AnchorLayoutName), AnchorControlName, out var anchor))
        {
            return placed;
        }

        return PixelRect.FromEdges(anchor.Right - placed.Width, anchor.Y, anchor.Right, placed.Height);
    }
}
