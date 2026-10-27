using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using DaScreenshotStudio.Engine.Core;
using DaScreenshotStudio.Engine.Records;
using DaScreenshotStudio.Engine.Ui;

namespace DaScreenshotStudio.Engine.Staging;

public sealed class SceneDocument
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;

    public string? Reference { get; set; }

    public bool Verified { get; set; }

    public SceneMap Map { get; set; } = new();

    public SceneHud Hud { get; set; } = new();

    public SceneObject Player { get; set; } = NewPlayer();

    public List<SceneObject> Objects { get; set; } = [];

    public static SceneObject NewPlayer() => new()
    {
        Kind = SceneObjectKind.Aisling,
        Look = new AislingLook { Head = 1, Armor = 1, Arms = 1, Boots = 1, BootsColor = 13, Pants = 1 },
    };

    public SceneDocument Clone() => FromJson(ToJson());

    public string ToJson() => JsonSerializer.Serialize(this, SceneJson.Default.SceneDocument);

    public static SceneDocument FromJson(string json) =>
        JsonSerializer.Deserialize(json, SceneJson.Default.SceneDocument) ?? throw new InvalidDataException("No scene.");

    public static SceneDocument Load(string path) =>
        JsonSerializer.Deserialize(File.ReadAllText(path), SceneJson.Default.SceneDocument)
        ?? throw new InvalidDataException($"'{path}' holds no scene.");

    public void Save(string path) => File.WriteAllText(path, JsonSerializer.Serialize(this, SceneJson.Default.SceneDocument));

    public string? ReferencePathFrom(string sceneDirectory) =>
        Reference is { Length: > 0 } reference
            ? Path.GetFullPath(Path.Combine(sceneDirectory, PortablePath(reference)))
            : null;

    public static string PortablePath(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return path.Replace('\\', '/');
    }

    public static JsonTypeInfo TypeInfo(Type type) =>
        SceneJson.Default.GetTypeInfo(type) ?? throw new ArgumentException($"{type.Name} is not part of a scene.", nameof(type));
}

public sealed class SceneMap
{
    public int Id { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    public byte Flags { get; set; }

    public byte TimeStep { get; set; }

    public long TileTick { get; set; }

    public long StaticTick { get; set; }

    public int? HoverX { get; set; }

    public int? HoverY { get; set; }

    public int? HoverObject { get; set; }

    public List<SceneDoor> Doors { get; set; } = [];
}

public sealed class SceneDoor
{
    public int X { get; set; }

    public int Y { get; set; }

    public bool Left { get; set; }

    public bool Open { get; set; }
}

public sealed class SceneHud
{
    public GuiLayoutIndex Layout { get; set; } = GuiLayoutIndex.Compact;

    public ScenePanel Panel { get; set; } = ScenePanel.Chat;

    public string Name { get; set; } = "";

    public string Server { get; set; } = "Dark Ages";

    public string Zone { get; set; } = "";

    public int? PositionX { get; set; }

    public int? PositionY { get; set; }

    public int Weight { get; set; }

    public int MaxWeight { get; set; }

    public uint Health { get; set; }

    public uint MaxHealth { get; set; }

    public uint Mana { get; set; }

    public uint MaxMana { get; set; }

    public List<string> History { get; set; } = [];

    public uint Latency { get; set; }

    public string Description { get; set; } = "";

    public List<SceneChatLine> Chat { get; set; } = [];

    public List<SceneStatusEffect> StatusEffects { get; set; } = [];

    public List<SceneItem> Inventory { get; set; } = [];

    public List<SceneItem> Equipment { get; set; } = [];

    public SceneWindow Window { get; set; } = new();

    public SceneNpc? Npc { get; set; }

    public uint Gold { get; set; }

    public byte Level { get; set; }

    public byte AbilityLevel { get; set; }

    public uint Experience { get; set; }

    public uint ToNextLevel { get; set; }

    public uint AbilityExperience { get; set; }

    public uint ToNextAbility { get; set; }

    public uint GamePoints { get; set; }

    public List<SceneChatLine> Notices { get; set; } = [];

    public int NoticeFade { get; set; }

    public List<SceneSlot> Spells { get; set; } = [];

    public List<SceneSlot> Skills { get; set; } = [];
}

public sealed class SceneSlot
{
    public int Slot { get; set; } = 1;

    public int Icon { get; set; }

    public string Name { get; set; } = "";

    public CooldownKind Cooldown { get; set; }

    public int CooldownProgress { get; set; }
}

public enum CooldownKind
{
    None,
    Locked,
    Wipe,
}

public sealed class SceneStatusEffect
{
    public int? Icon { get; set; }

    public byte Stage { get; set; } = 6;
}

public sealed class SceneItem
{
    public int Slot { get; set; } = 1;

    public int Icon { get; set; }

    public byte Dye { get; set; }

    public string Name { get; set; } = "";

    public uint Quantity { get; set; } = 1;
}

public enum SceneWindowKind
{
    None,

    Options,

    Profile,

    UserList,

    BoardList,

    PostList,

    Post,
}

public sealed class SceneBulletin
{
    public List<SceneBoard> Boards { get; set; } = [];

    public bool Mailbox { get; set; }

    public bool Privileged { get; set; }

    public int SectionId { get; set; }

    public string Heading { get; set; } = "";

    public byte Permissions { get; set; } = 1;

    public List<ScenePostRow> Posts { get; set; } = [];

    public ScenePost Post { get; set; } = new();
}

public sealed class SceneBoard
{
    public int Id { get; set; }

    public string Name { get; set; } = "";
}

public sealed class ScenePostRow
{
    public int Id { get; set; }

    public byte Flags { get; set; }

    public string Author { get; set; } = "";

    public byte Month { get; set; }

    public byte Day { get; set; }

    public string Title { get; set; } = "";
}

public sealed class ScenePost
{
    public int Id { get; set; }

    public string Author { get; set; } = "";

    public string Title { get; set; } = "";

    public string Body { get; set; } = "";

    public byte Month { get; set; }

    public byte Day { get; set; }

    public byte NavigationFlags { get; set; } = 1;
}

public sealed class SceneUserList
{
    public int WorldCount { get; set; }

    public int Filter { get; set; }

    public List<SceneUserRow> Rows { get; set; } = [];
}

public sealed class SceneUserRow
{
    public string Name { get; set; } = "";

    public string Title { get; set; } = "";

    public CharacterClass Class { get; set; }

    public bool Master { get; set; }

    public bool Guild { get; set; }

    public byte Color { get; set; }

    public UserState State { get; set; }

    public int Repeat { get; set; } = 1;
}

public sealed class SceneProfile
{
    public int Page { get; set; }

    public UserState State { get; set; }

    public Nation Nation { get; set; }

    public string NationName { get; set; } = "";

    public string Title { get; set; } = "";

    public string DisplayClass { get; set; } = "";

    public string Guild { get; set; } = "";

    public string GuildRank { get; set; } = "";

    public bool GroupOpen { get; set; }

    public string Text { get; set; } = "";

    public byte[]? Portrait { get; set; }

    public byte Strength { get; set; }

    public byte Intelligence { get; set; }

    public byte Wisdom { get; set; }

    public byte Constitution { get; set; }

    public byte Dexterity { get; set; }

    public sbyte ArmorClass { get; set; }

    public List<SceneLegendMark> Legend { get; set; } = [];
}

public sealed class SceneLegendMark
{
    public LegendMarkIcon Icon { get; set; }

    public byte Color { get; set; }

    public string Text { get; set; } = "";

    public string Key { get; set; } = "";
}

public sealed class SceneWindow
{
    public SceneWindowKind Kind { get; set; }

    public int SoundLevel { get; set; }

    public int MusicLevel { get; set; }

    public SceneProfile Profile { get; set; } = new();

    public SceneUserList Users { get; set; } = new();

    public SceneBulletin Bulletin { get; set; } = new();
}

public enum ScenePanel
{
    Chat,
    Inventory,
    Sheet,
    SpellsPage1,
    SpellsPage2,
    SkillsPage1,
    SkillsPage2,

    General,
}

public sealed class SceneChatLine
{
    public string Text { get; set; } = "";

    public byte? Color { get; set; }
}

public enum SceneObjectKind
{
    Aisling,
    Creature,
    Item,
}

public sealed class SceneObject
{
    public SceneObjectKind Kind { get; set; }

    public string Name { get; set; } = "";

    public int X { get; set; }

    public int Y { get; set; }

    public Direction Direction { get; set; } = Direction.Down;

    public AislingLook? Look { get; set; }

    public ushort Sprite { get; set; }

    public byte Color { get; set; }

    public CreatureType CreatureType { get; set; } = CreatureType.Monster;

    public int WalkFrame { get; set; }

    public int OffsetX { get; set; }

    public int OffsetY { get; set; }

    public int? FromX { get; set; }

    public int? FromY { get; set; }

    public byte Animation { get; set; }

    public byte AnimationFrame { get; set; }

    public int? IdleSegment { get; set; }

    public int IdleStep { get; set; }

    public SceneBalloon? Balloon { get; set; }

    public List<SceneEffect> Effects { get; set; } = [];
}

public sealed class SceneNpc
{
    public string Name { get; set; } = "";

    public string Text { get; set; } = "";

    public ushort Sprite { get; set; }

    public byte Color { get; set; }

    public byte Illustration { get; set; }

    public bool Pursuit { get; set; }

    public bool HasPrevious { get; set; }

    public bool HasNext { get; set; }

    public List<SceneNpcChoice> Choices { get; set; } = [];

    public int? HoverChoice { get; set; }

    public SceneShop Shop { get; set; } = new();
}

public sealed class SceneNpcChoice
{
    public string Text { get; set; } = "";
}

public sealed class SceneShop
{
    public bool Open { get; set; }

    public List<SceneShopItem> Items { get; set; } = [];

    public int Category { get; set; }

    public int Page { get; set; } = 1;

    public int? SelectedRow { get; set; }

    public int? HoverRow { get; set; }

    public int PointerX { get; set; }

    public int PointerY { get; set; }
}

public sealed class SceneShopItem
{
    public string Name { get; set; } = "";

    public int Icon { get; set; }

    public byte Color { get; set; }

    public uint Price { get; set; }
}

public sealed class SceneEffect
{
    public int Id { get; set; }

    public int Step { get; set; }
}

public sealed class SceneBalloon
{
    public string Text { get; set; } = "";

    public SpeechMode Mode { get; set; } = SpeechMode.Say;
}

public sealed class AislingLook
{
    public byte Form { get; set; } = 1;

    public byte Pants { get; set; }

    public ushort Head { get; set; }

    public byte HairColor { get; set; }

    public byte Face { get; set; }

    public byte Skin { get; set; }

    public ushort Armor { get; set; }

    public ushort Arms { get; set; }

    public byte ArmorColor { get; set; }

    public ushort Overcoat { get; set; }

    public byte OvercoatColor { get; set; }

    public ushort Boots { get; set; }

    public byte BootsColor { get; set; }

    public ushort Weapon { get; set; }

    public ushort Shield { get; set; }

    public ushort Accessory1 { get; set; }

    public byte Accessory1Color { get; set; }

    public ushort Accessory2 { get; set; }

    public byte Accessory2Color { get; set; }

    public ushort Accessory3 { get; set; }

    public byte Accessory3Color { get; set; }

    public bool Translucent { get; set; }

    public AislingLook Clone() => (AislingLook)MemberwiseClone();
}

[JsonSourceGenerationOptions(
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(SceneDocument))]
[JsonSerializable(typeof(AislingLook))]
internal sealed partial class SceneJson : JsonSerializerContext;
