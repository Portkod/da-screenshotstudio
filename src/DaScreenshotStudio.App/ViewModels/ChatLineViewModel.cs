using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using DaScreenshotStudio.Engine.Staging;

namespace DaScreenshotStudio.App.ViewModels;

/// <summary>A text palette color, shown as a swatch with the markup code that selects it, if any.</summary>
public sealed class TextColorSwatch(int index, string code, IBrush brush)
{
    public int Index { get; } = index;

    public string Code { get; } = code;

    public IBrush Brush { get; } = brush;

    public string Label => Code.Length > 0 ? Code : $"#{Index}";

    public string Tip => $"Palette index {Index} (0x{Index:X2}){(Code.Length > 0 ? ", markup " + Code : "")}";
}

/// <summary>One filled slot of the spell or skill book.</summary>
public sealed class BookSlotViewModel : ObservableObject
{
    private readonly IEditorHost _editor;
    private readonly Func<int, Avalonia.Media.Imaging.Bitmap?> _icon;

    public BookSlotViewModel(IEditorHost editor, SceneSlot model, Func<int, Avalonia.Media.Imaging.Bitmap?> icon)
    {
        _editor = editor;
        Model = model;
        _icon = icon;
    }

    public SceneSlot Model { get; }

    public int Slot
    {
        get => Model.Slot;
        set
        {
            var clamped = Math.Clamp(value, 1, Engine.Ui.PlayerModel.AbilityCapacity);
            if (clamped == Model.Slot)
            {
                return;
            }

            _editor.BeginEdit($"bookslot:{GetHashCode()}");
            Model.Slot = clamped;
            OnPropertyChanged();
            _editor.SceneChanged();
        }
    }

    public int Icon
    {
        get => Model.Icon;
        set
        {
            var clamped = Math.Clamp(value, 0, ushort.MaxValue);
            if (clamped == Model.Icon)
            {
                return;
            }

            _editor.BeginEdit($"bookicon:{GetHashCode()}");
            Model.Icon = clamped;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Preview));
            _editor.SceneChanged();
        }
    }

    public Avalonia.Media.Imaging.Bitmap? Preview => _icon(Model.Icon);

    public static IReadOnlyList<CooldownKind> CooldownKinds { get; } = Enum.GetValues<CooldownKind>();

    public CooldownKind Cooldown
    {
        get => Model.Cooldown;
        set
        {
            if (value == Model.Cooldown)
            {
                return;
            }

            _editor.BeginEdit($"cooldown:{GetHashCode()}");
            Model.Cooldown = value;
            OnPropertyChanged();
            _editor.SceneChanged();
        }
    }

    public int CooldownProgress
    {
        get => Model.CooldownProgress;
        set
        {
            var clamped = Math.Clamp(value, 0, 30);
            if (clamped == Model.CooldownProgress)
            {
                return;
            }

            _editor.BeginEdit($"cooldownprogress:{GetHashCode()}");
            Model.CooldownProgress = clamped;
            OnPropertyChanged();
            _editor.SceneChanged();
        }
    }
}

/// <summary>One slot of the timed effect strip.</summary>
public sealed class StatusEffectViewModel : ObservableObject
{
    private readonly IEditorHost _editor;
    private readonly Func<int, Avalonia.Media.Imaging.Bitmap?> _icon;

    public StatusEffectViewModel(IEditorHost editor, SceneStatusEffect model, Func<int, Avalonia.Media.Imaging.Bitmap?> icon)
    {
        _editor = editor;
        Model = model;
        _icon = icon;
    }

    public SceneStatusEffect Model { get; }

    /// <summary>Blank for an empty slot.</summary>
    public int? Icon
    {
        get => Model.Icon;
        set
        {
            var clamped = value is { } v ? Math.Clamp(v, 0, short.MaxValue) : (int?)null;
            if (clamped == Model.Icon)
            {
                return;
            }

            _editor.BeginEdit($"effect:{GetHashCode()}");
            Model.Icon = clamped;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Preview));
            _editor.SceneChanged();
        }
    }

    public int Stage
    {
        get => Model.Stage;
        set
        {
            var clamped = (byte)Math.Clamp(value, 1, 6);
            if (clamped == Model.Stage)
            {
                return;
            }

            _editor.BeginEdit($"effectstage:{GetHashCode()}");
            Model.Stage = clamped;
            OnPropertyChanged();
            _editor.SceneChanged();
        }
    }

    public Avalonia.Media.Imaging.Bitmap? Preview => Model.Icon is { } icon ? _icon(icon) : null;
}

/// <summary>One chat entry: its text and the color the log draws it in.</summary>
public sealed class ChatLineViewModel : ObservableObject
{
    private readonly IEditorHost _editor;
    private readonly Func<int, IBrush> _swatch;

    private readonly int _defaultColor;

    public ChatLineViewModel(IEditorHost editor, SceneChatLine model, Func<int, IBrush> swatch, int defaultColor = DefaultColor)
    {
        _editor = editor;
        Model = model;
        _swatch = swatch;
        _defaultColor = defaultColor;
    }

    public SceneChatLine Model { get; }

    public string Text
    {
        get => Model.Text;
        set
        {
            if (value == Model.Text)
            {
                return;
            }

            _editor.BeginEdit($"chat:{GetHashCode()}");
            Model.Text = value;
            OnPropertyChanged();
            _editor.SceneChanged();
        }
    }

    /// <summary>A palette index, or blank for the log's default.</summary>
    public int? Color
    {
        get => Model.Color;
        set
        {
            var clamped = value is { } v ? (byte?)Math.Clamp(v, 0, 255) : null;
            if (clamped == Model.Color)
            {
                return;
            }

            _editor.BeginEdit($"chatcolor:{GetHashCode()}");
            Model.Color = clamped;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Swatch));
            _editor.SceneChanged();
        }
    }

    public IBrush Swatch => _swatch(Model.Color ?? _defaultColor);

    /// <summary>The chat log's own default palette index.</summary>
    public const int DefaultColor = 0x12;
}
