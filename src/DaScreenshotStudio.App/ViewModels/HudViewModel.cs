using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DaScreenshotStudio.Engine.Staging;
using DaScreenshotStudio.Engine.Ui;

namespace DaScreenshotStudio.App.ViewModels;

/// <summary>The scene's map and interface values as the inspector edits them.</summary>
public sealed partial class HudViewModel : ObservableObject
{
    private readonly IEditorHost _editor;
    private readonly SceneDocument _scene;
    private readonly Func<int, IBrush> _swatch;

    private readonly Func<int, Avalonia.Media.Imaging.Bitmap?> _spellIcon;
    private readonly Func<int, Avalonia.Media.Imaging.Bitmap?> _skillIcon;

    public HudViewModel(
        IEditorHost editor,
        SceneDocument scene,
        Func<int, IBrush> swatch,
        Func<int, Avalonia.Media.Imaging.Bitmap?> spellIcon,
        Func<int, Avalonia.Media.Imaging.Bitmap?> skillIcon)
    {
        _editor = editor;
        _scene = scene;
        _swatch = swatch;
        _spellIcon = spellIcon;
        _skillIcon = skillIcon;

        foreach (var line in scene.Hud.Notices)
        {
            Notices.Add(new ChatLineViewModel(editor, line, swatch, SceneApplier.NoticePalette));
        }

        foreach (var slot in scene.Hud.Spells)
        {
            Spells.Add(new BookSlotViewModel(editor, slot, spellIcon));
        }

        foreach (var slot in scene.Hud.Skills)
        {
            Skills.Add(new BookSlotViewModel(editor, slot, skillIcon));
        }

        foreach (var effect in scene.Hud.StatusEffects)
        {
            StatusEffects.Add(new StatusEffectViewModel(editor, effect, spellIcon));
        }

        foreach (var line in scene.Hud.Chat)
        {
            ChatLines.Add(new ChatLineViewModel(editor, line, swatch));
        }

        ItemFields = FieldEditors.For(editor, scene.Hud, ["Inventory", "Equipment"]);
        SheetFields = FieldEditors.For(
            editor,
            scene.Hud,
            ["Gold", "Level", "AbilityLevel", "Experience", "ToNextLevel", "AbilityExperience", "ToNextAbility", "GamePoints"]);
        RebuildWindowFields();
        RebuildNpcFields();
    }

    /// <summary>The character sheet's own numbers. Gold also fills a shop's purse.</summary>
    public IReadOnlyList<FieldViewModel> SheetFields { get; }

    /// <summary>Whether an NPC is talking. Turning it on shows the dialog's fields below.</summary>
    public bool HasNpc
    {
        get => Hud.Npc is not null;
        set
        {
            if (value == HasNpc)
            {
                return;
            }

            _editor.BeginEdit("npc");
            Hud.Npc = value ? new SceneNpc() : null;
            OnPropertyChanged();
            RebuildNpcFields();
            _editor.SceneChanged();
        }
    }

    public ObservableCollection<FieldViewModel> NpcFields { get; } = [];

    private void RebuildNpcFields()
    {
        NpcFields.Clear();
        if (Hud.Npc is not { } npc)
        {
            return;
        }

        foreach (var field in FieldEditors.For(_editor, npc))
        {
            NpcFields.Add(field);
        }
    }

    /// <summary>The inventory and the worn equipment, as lists of item records.</summary>
    public IReadOnlyList<FieldViewModel> ItemFields { get; }

    public static IReadOnlyList<SceneWindowKind> WindowKinds { get; } = Enum.GetValues<SceneWindowKind>();

    /// <summary>The window standing over the world. Changing it shows that window's own fields.</summary>
    public SceneWindowKind WindowKind
    {
        get => Hud.Window.Kind;
        set
        {
            if (value == Hud.Window.Kind)
            {
                return;
            }

            _editor.BeginEdit("window-kind");
            Hud.Window.Kind = value;
            OnPropertyChanged();
            RebuildWindowFields();
            _editor.SceneChanged();
        }
    }

    /// <summary>The fields of the window <see cref="WindowKind"/> shows, and only those.</summary>
    public ObservableCollection<FieldViewModel> WindowFields { get; } = [];

    private void RebuildWindowFields()
    {
        var window = Hud.Window;
        var fields = window.Kind switch
        {
            SceneWindowKind.Options => FieldEditors.For(_editor, window, ["SoundLevel", "MusicLevel"]),
            SceneWindowKind.Profile => FieldEditors.For(_editor, window.Profile),
            SceneWindowKind.UserList => FieldEditors.For(_editor, window.Users),
            SceneWindowKind.BoardList => FieldEditors.For(_editor, window.Bulletin, ["Boards"]),
            SceneWindowKind.PostList => FieldEditors.For(
                _editor, window.Bulletin, ["Mailbox", "Privileged", "SectionId", "Heading", "Permissions", "Posts"]),
            SceneWindowKind.Post => FieldEditors.For(_editor, window.Bulletin, ["Mailbox", "Post"]),
            _ => [],
        };

        WindowFields.Clear();
        foreach (var field in fields)
        {
            WindowFields.Add(field);
        }
    }

    public ObservableCollection<ChatLineViewModel> ChatLines { get; } = [];

    public ObservableCollection<StatusEffectViewModel> StatusEffects { get; } = [];

    public ObservableCollection<ChatLineViewModel> Notices { get; } = [];

    public ObservableCollection<BookSlotViewModel> Spells { get; } = [];

    public ObservableCollection<BookSlotViewModel> Skills { get; } = [];

    [RelayCommand]
    private void AddNotice()
    {
        _editor.BeginEdit("notice:add");
        var line = new SceneChatLine();
        Hud.Notices.Add(line);
        Notices.Add(new ChatLineViewModel(_editor, line, _swatch, SceneApplier.NoticePalette));
        _editor.SceneChanged();
    }

    [RelayCommand]
    private void RemoveNotice(ChatLineViewModel? line)
    {
        if (line is null)
        {
            return;
        }

        _editor.BeginEdit("notice:remove");
        Hud.Notices.Remove(line.Model);
        Notices.Remove(line);
        _editor.SceneChanged();
    }

    [RelayCommand]
    private void AddSpell() => AddSlot(Hud.Spells, Spells, _spellIcon);

    [RelayCommand]
    private void AddSkill() => AddSlot(Hud.Skills, Skills, _skillIcon);

    private void AddSlot(List<SceneSlot> book, ObservableCollection<BookSlotViewModel> rows, Func<int, Avalonia.Media.Imaging.Bitmap?> icon)
    {
        _editor.BeginEdit("book:add");
        var slot = new SceneSlot { Slot = book.Count == 0 ? 1 : book.Max(s => s.Slot) + 1, Icon = 1 };
        book.Add(slot);
        rows.Add(new BookSlotViewModel(_editor, slot, icon));
        _editor.SceneChanged();
    }

    [RelayCommand]
    private void RemoveBookSlot(BookSlotViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        _editor.BeginEdit("book:remove");
        if (Spells.Remove(row))
        {
            Hud.Spells.Remove(row.Model);
        }
        else if (Skills.Remove(row))
        {
            Hud.Skills.Remove(row.Model);
        }

        _editor.SceneChanged();
    }

    [RelayCommand]
    private void AddStatusEffect()
    {
        if (Hud.StatusEffects.Count >= Engine.Ui.SpelledViewPane.SlotCount)
        {
            return;
        }

        _editor.BeginEdit("effect:add");
        var effect = new SceneStatusEffect { Icon = 1, Stage = 6 };
        Hud.StatusEffects.Add(effect);
        StatusEffects.Add(new StatusEffectViewModel(_editor, effect, _spellIcon));
        _editor.SceneChanged();
    }

    [RelayCommand]
    private void RemoveStatusEffect(StatusEffectViewModel? effect)
    {
        if (effect is null)
        {
            return;
        }

        _editor.BeginEdit("effect:remove");
        Hud.StatusEffects.Remove(effect.Model);
        StatusEffects.Remove(effect);
        _editor.SceneChanged();
    }

    [RelayCommand]
    private void AddChatLine()
    {
        _editor.BeginEdit("chat:add");
        var line = new SceneChatLine { Color = ChatLines.Count > 0 ? ChatLines[^1].Model.Color : null };
        Hud.Chat.Add(line);
        ChatLines.Add(new ChatLineViewModel(_editor, line, _swatch));
        _editor.SceneChanged();
    }

    [RelayCommand]
    private void RemoveChatLine(ChatLineViewModel? line)
    {
        if (line is null)
        {
            return;
        }

        _editor.BeginEdit("chat:remove");
        Hud.Chat.Remove(line.Model);
        ChatLines.Remove(line);
        _editor.SceneChanged();
    }

    private SceneMap Map => _scene.Map;

    private SceneHud Hud => _scene.Hud;

    public static IReadOnlyList<GuiLayoutIndex> Layouts { get; } = Enum.GetValues<GuiLayoutIndex>();

    public static IReadOnlyList<ScenePanel> Panels { get; } = Enum.GetValues<ScenePanel>();

    public int MapId { get => Map.Id; set => Set(Map.Id, Math.Max(0, value), v => Map.Id = v); }

    public int MapWidth { get => Map.Width; set => Set(Map.Width, Math.Clamp(value, 0, 255), v => Map.Width = v); }

    public int MapHeight { get => Map.Height; set => Set(Map.Height, Math.Clamp(value, 0, 255), v => Map.Height = v); }

    public int MapFlags { get => Map.Flags; set => Set(Map.Flags, (byte)Math.Clamp(value, 0, 255), v => Map.Flags = v); }

    public int TimeStep { get => Map.TimeStep; set => Set(Map.TimeStep, (byte)Math.Clamp(value, 0, 255), v => Map.TimeStep = v); }

    /// <summary>Blank for no ring.</summary>
    public int? HoverX { get => Map.HoverX; set => Set(Map.HoverX, value, v => Map.HoverX = v); }

    public int? HoverY { get => Map.HoverY; set => Set(Map.HoverY, value, v => Map.HoverY = v); }

    /// <summary>Blank for none, -1 for the player, otherwise the object's index.</summary>
    public int? HoverObject { get => Map.HoverObject; set => Set(Map.HoverObject, value, v => Map.HoverObject = v); }

    public int NoticeFade { get => Hud.NoticeFade; set => Set(Hud.NoticeFade, Math.Clamp(value, 0, 33), v => Hud.NoticeFade = v); }

    public decimal TileTick { get => Map.TileTick; set => Set(Map.TileTick, (long)Math.Max(0, value), v => Map.TileTick = v); }

    public decimal StaticTick { get => Map.StaticTick; set => Set(Map.StaticTick, (long)Math.Max(0, value), v => Map.StaticTick = v); }

    public GuiLayoutIndex Layout { get => Hud.Layout; set => Set(Hud.Layout, value, v => Hud.Layout = v); }

    public ScenePanel Panel { get => Hud.Panel; set => Set(Hud.Panel, value, v => Hud.Panel = v); }

    public string Name { get => Hud.Name; set => Set(Hud.Name, value, v => Hud.Name = v); }

    public string Server { get => Hud.Server; set => Set(Hud.Server, value, v => Hud.Server = v); }

    public string Zone { get => Hud.Zone; set => Set(Hud.Zone, value, v => Hud.Zone = v); }

    /// <summary>Blank shows the player's cell.</summary>
    public int? PositionX { get => Hud.PositionX; set => Set(Hud.PositionX, value, v => Hud.PositionX = v); }

    public int? PositionY { get => Hud.PositionY; set => Set(Hud.PositionY, value, v => Hud.PositionY = v); }

    public int Weight { get => Hud.Weight; set => Set(Hud.Weight, value, v => Hud.Weight = v); }

    public int MaxWeight { get => Hud.MaxWeight; set => Set(Hud.MaxWeight, value, v => Hud.MaxWeight = v); }

    public decimal Health { get => Hud.Health; set => Set(Hud.Health, Unsigned(value), v => Hud.Health = v); }

    public decimal MaxHealth { get => Hud.MaxHealth; set => Set(Hud.MaxHealth, Unsigned(value), v => Hud.MaxHealth = v); }

    public decimal Mana { get => Hud.Mana; set => Set(Hud.Mana, Unsigned(value), v => Hud.Mana = v); }

    public decimal MaxMana { get => Hud.MaxMana; set => Set(Hud.MaxMana, Unsigned(value), v => Hud.MaxMana = v); }

    public decimal Latency { get => Hud.Latency; set => Set(Hud.Latency, Unsigned(value), v => Hud.Latency = v); }

    public string Description { get => Hud.Description; set => Set(Hud.Description, value, v => Hud.Description = v); }

    /// <summary>The message strip, one entry per line.</summary>
    public string HistoryText
    {
        get => string.Join('\n', Hud.History);
        set => Set(HistoryText, value, v => Hud.History = Lines(v));
    }

    public void RefreshAll() => OnPropertyChanged(string.Empty);

    private static uint Unsigned(decimal value) => (uint)Math.Clamp(value, 0, uint.MaxValue);

    private static List<string> Lines(string text) =>
        text.Length == 0 ? [] : [.. text.Replace("\r", "", StringComparison.Ordinal).Split('\n')];

    private void Set<T>(T current, T value, Action<T> assign, [CallerMemberName] string property = "")
    {
        if (EqualityComparer<T>.Default.Equals(current, value))
        {
            return;
        }

        _editor.BeginEdit($"hud:{property}");
        assign(value);
        OnPropertyChanged(property);
        _editor.SceneChanged();
    }
}
