using System.Runtime.CompilerServices;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using DaScreenshotStudio.Engine.Core;
using DaScreenshotStudio.Engine.Records;
using DaScreenshotStudio.Engine.Staging;

namespace DaScreenshotStudio.App.ViewModels;

/// <summary>One staged object as the inspector edits it. Every change is written straight to the model.</summary>
public sealed partial class SceneObjectViewModel : ObservableObject
{
    private readonly IEditorHost _editor;

    public SceneObjectViewModel(IEditorHost editor, SceneObject model, bool isPlayer = false)
    {
        _editor = editor;
        Model = model;
        IsPlayer = isPlayer;
    }

    /// <summary>The scene's player, whose cell and step the camera follows.</summary>
    public bool IsPlayer { get; }

    public SceneObject Model { get; }

    private AislingLook Look => Model.Look ??= new AislingLook();

    public bool IsAisling => Model.Kind == SceneObjectKind.Aisling;

    public bool IsCreature => Model.Kind == SceneObjectKind.Creature;

    public bool IsSprite => Model.Kind != SceneObjectKind.Aisling;

    public bool IsPosable => Model.Kind != SceneObjectKind.Item;

    public bool IsItem => Model.Kind == SceneObjectKind.Item;

    [ObservableProperty]
    public partial Bitmap? Preview { get; set; }

    public string Title
    {
        get
        {
            var name = Model.Name.Length > 0 ? Model.Name : Model.Kind.ToString();
            var player = IsPlayer ? " (player)" : "";
            return $"{name}{player}  [{Model.X},{Model.Y}]";
        }
    }

    public static IReadOnlyList<Direction> Directions { get; } = Enum.GetValues<Direction>();

    public static IReadOnlyList<SpeechMode> SpeechModes { get; } = Enum.GetValues<SpeechMode>();

    public static IReadOnlyList<CreatureType> CreatureTypes { get; } = Enum.GetValues<CreatureType>();

    public SceneObjectKind Kind => Model.Kind;

    public string Name { get => Model.Name; set => Set(Model.Name, value, v => Model.Name = v); }

    public int X { get => Model.X; set => Set(Model.X, value, v => Model.X = v); }

    public int Y { get => Model.Y; set => Set(Model.Y, value, v => Model.Y = v); }

    public Direction Direction { get => Model.Direction; set => Set(Model.Direction, value, v => Model.Direction = v); }

    public int Sprite { get => Model.Sprite; set => Set(Model.Sprite, (ushort)Math.Clamp(value, 0, ushort.MaxValue), v => Model.Sprite = v); }

    public int Color { get => Model.Color; set => Set(Model.Color, (byte)Math.Clamp(value, 0, 255), v => Model.Color = v); }

    public CreatureType CreatureType { get => Model.CreatureType; set => Set(Model.CreatureType, value, v => Model.CreatureType = v); }

    public int WalkFrame { get => Model.WalkFrame; set => Set(Model.WalkFrame, value, v => Model.WalkFrame = v); }

    public int OffsetX { get => Model.OffsetX; set => Set(Model.OffsetX, value, v => Model.OffsetX = v); }

    public int OffsetY { get => Model.OffsetY; set => Set(Model.OffsetY, value, v => Model.OffsetY = v); }

    /// <summary>Blank for the figure's own cell.</summary>
    public int? FromX { get => Model.FromX; set => Set(Model.FromX, value, v => Model.FromX = v); }

    public int? FromY { get => Model.FromY; set => Set(Model.FromY, value, v => Model.FromY = v); }

    public int Animation { get => Model.Animation; set => Set(Model.Animation, Byte(value), v => Model.Animation = v); }

    public int AnimationFrame { get => Model.AnimationFrame; set => Set(Model.AnimationFrame, Byte(value), v => Model.AnimationFrame = v); }

    public int IdleStep { get => Model.IdleStep; set => Set(Model.IdleStep, Math.Max(0, value), v => Model.IdleStep = v); }

    /// <summary>Blank for none, otherwise the effect drawn on the figure.</summary>
    public int? EffectId
    {
        get => Model.Effects.Count > 0 ? Model.Effects[0].Id : null;
        set => Set(EffectId, value is { } v ? Math.Max(0, v) : null, v =>
        {
            if (v is not { } id)
            {
                Model.Effects.Clear();
            }
            else if (Model.Effects.Count > 0)
            {
                Model.Effects[0].Id = id;
            }
            else
            {
                Model.Effects.Add(new SceneEffect { Id = id });
            }

            OnPropertyChanged(nameof(EffectStep));
        });
    }

    public int EffectStep
    {
        get => Model.Effects.Count > 0 ? Model.Effects[0].Step : 0;
        set
        {
            if (Model.Effects.Count > 0)
            {
                Set(EffectStep, Math.Max(0, value), v => Model.Effects[0].Step = v);
            }
        }
    }

    /// <summary>Blank for none, otherwise the standing segment's index.</summary>
    public int? IdleSegment
    {
        get => Model.IdleSegment;
        set => Set(Model.IdleSegment, value is { } v ? Math.Max(0, v) : null, v => Model.IdleSegment = v);
    }

    /// <summary>What the creature's sheet offers to idle with, shown beside the fields.</summary>
    [ObservableProperty]
    public partial string IdleInfo { get; set; } = "";

    public string BalloonText
    {
        get => Model.Balloon?.Text ?? "";
        set => Set(BalloonText, value, v =>
        {
            if (v.Length == 0)
            {
                Model.Balloon = null;
                return;
            }

            Model.Balloon ??= new SceneBalloon();
            Model.Balloon.Text = v;
        });
    }

    public SpeechMode BalloonMode
    {
        get => Model.Balloon?.Mode ?? SpeechMode.Say;
        set => Set(BalloonMode, value, v =>
        {
            Model.Balloon ??= new SceneBalloon();
            Model.Balloon.Mode = v;
        });
    }

    public int Form { get => Look.Form; set => Set(Look.Form, (byte)Math.Clamp(value, 0, 15), v => Look.Form = v); }

    public int Pants { get => Look.Pants; set => Set(Look.Pants, (byte)Math.Clamp(value, 0, 15), v => Look.Pants = v); }

    public int Head { get => Look.Head; set => Set(Look.Head, Word(value), v => Look.Head = v); }

    public int HairColor { get => Look.HairColor; set => Set(Look.HairColor, Byte(value), v => Look.HairColor = v); }

    public int Face { get => Look.Face; set => Set(Look.Face, Byte(value), v => Look.Face = v); }

    public int Skin { get => Look.Skin; set => Set(Look.Skin, Byte(value), v => Look.Skin = v); }

    public int Armor { get => Look.Armor; set => Set(Look.Armor, Word(value), v => Look.Armor = v); }

    public int Arms { get => Look.Arms; set => Set(Look.Arms, Word(value), v => Look.Arms = v); }

    public int ArmorColor { get => Look.ArmorColor; set => Set(Look.ArmorColor, Byte(value), v => Look.ArmorColor = v); }

    public int Overcoat { get => Look.Overcoat; set => Set(Look.Overcoat, Word(value), v => Look.Overcoat = v); }

    public int OvercoatColor { get => Look.OvercoatColor; set => Set(Look.OvercoatColor, Byte(value), v => Look.OvercoatColor = v); }

    public int Boots { get => Look.Boots; set => Set(Look.Boots, Word(value), v => Look.Boots = v); }

    public int BootsColor { get => Look.BootsColor; set => Set(Look.BootsColor, Byte(value), v => Look.BootsColor = v); }

    public int Weapon { get => Look.Weapon; set => Set(Look.Weapon, Word(value), v => Look.Weapon = v); }

    public int Shield { get => Look.Shield; set => Set(Look.Shield, Word(value), v => Look.Shield = v); }

    public int Accessory1 { get => Look.Accessory1; set => Set(Look.Accessory1, Word(value), v => Look.Accessory1 = v); }

    public int Accessory1Color { get => Look.Accessory1Color; set => Set(Look.Accessory1Color, Byte(value), v => Look.Accessory1Color = v); }

    public int Accessory2 { get => Look.Accessory2; set => Set(Look.Accessory2, Word(value), v => Look.Accessory2 = v); }

    public int Accessory2Color { get => Look.Accessory2Color; set => Set(Look.Accessory2Color, Byte(value), v => Look.Accessory2Color = v); }

    public int Accessory3 { get => Look.Accessory3; set => Set(Look.Accessory3, Word(value), v => Look.Accessory3 = v); }

    public int Accessory3Color { get => Look.Accessory3Color; set => Set(Look.Accessory3Color, Byte(value), v => Look.Accessory3Color = v); }

    public bool Translucent { get => Look.Translucent; set => Set(Look.Translucent, value, v => Look.Translucent = v); }

    /// <summary>Tells every binding the model may have changed under it, after a move or a dressing.</summary>
    public void RefreshAll() => OnPropertyChanged(string.Empty);

    private static byte Byte(int value) => (byte)Math.Clamp(value, 0, byte.MaxValue);

    private static ushort Word(int value) => (ushort)Math.Clamp(value, 0, ushort.MaxValue);

    private void Set<T>(T current, T value, Action<T> assign, [CallerMemberName] string property = "")
    {
        if (EqualityComparer<T>.Default.Equals(current, value))
        {
            return;
        }

        _editor.BeginEdit($"{property}:{GetHashCode()}");
        assign(value);
        OnPropertyChanged(property);
        OnPropertyChanged(nameof(Title));
        _editor.SceneChanged();
    }
}

/// <summary>What a view model reports its edits to.</summary>
public interface IEditorHost
{
    /// <summary>
    /// Called before a change, so the editor can record an undo point. Edits sharing a key in quick
    /// succession share one point, which is what keeps typing from filling the history.
    /// </summary>
    void BeginEdit(string key);

    void SceneChanged();
}
