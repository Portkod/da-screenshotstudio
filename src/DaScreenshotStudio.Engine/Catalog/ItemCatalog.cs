using System.Text.Json;
using System.Text.Json.Serialization;
using DaScreenshotStudio.Engine.Staging;

namespace DaScreenshotStudio.Engine.Catalog;

public enum ItemCategory
{
    SkinColor,
    FaceShape,
    Hairstyle,
    Hat,
    HairDye,
    Armor,
    ArmorDye,
    Weapon,
    Accessory,
    Shield,
    Boots,
}

public enum ItemGender
{
    Any = 0,
    Male = 1,
    Female = 2,
}

public enum ItemBodyStyle
{
    Naked = 0,
    BodySuit = 1,
    Saddle = 17,
}

public sealed class CatalogItem
{
    public string Key { get; set; } = "";

    public string Name { get; set; } = "";

    public string[]? AlternativeNames { get; set; }

    public int FileId { get; set; }

    public ItemCategory Category { get; set; }

    public int Color { get; set; }

    public ItemBodyStyle BodyStyle { get; set; }

    [JsonPropertyName("Sex")]
    public ItemGender Gender { get; set; }

    public bool Dyeable { get; set; } = true;

    public bool TakesRequestedDye => Color <= 0 && Dyeable;

    public byte DrawnColor(byte requested) => TakesRequestedDye ? requested : (byte)Color;

    public bool Fits(ItemGender figure) => Gender == ItemGender.Any || Gender == figure;

    public override string ToString() => Name;
}

public sealed class ItemCatalog
{
    private ItemCatalog(IReadOnlyList<CatalogItem> items) => Items = items;

    public static ItemCatalog Empty { get; } = new([]);

    public IReadOnlyList<CatalogItem> Items { get; }

    public IEnumerable<CatalogItem> InCategory(ItemCategory category) => Items.Where(item => item.Category == category);

    public static ItemCatalog Load(string path)
    {
        var records = JsonSerializer.Deserialize(File.ReadAllText(path), ItemCatalogJson.Default.ListCatalogItem) ?? [];
        records.RemoveAll(item =>
            string.IsNullOrEmpty(item.Key)
            || string.IsNullOrEmpty(item.Name)
            || !Enum.IsDefined(item.Category)
            || !Enum.IsDefined(item.Gender));

        return new ItemCatalog(records);
    }
}

public static class Dressing
{
    public static ItemGender GenderOf(AislingLook look)
    {
        ArgumentNullException.ThrowIfNull(look);
        return look.Form is 2 or 4 or 6 or 9 or 0xB ? ItemGender.Female : ItemGender.Male;
    }

    public static void Wear(AislingLook look, CatalogItem item, byte dye = 0, int? accessorySlot = null)
    {
        ArgumentNullException.ThrowIfNull(look);
        ArgumentNullException.ThrowIfNull(item);

        var sprite = (ushort)item.FileId;
        var color = item.DrawnColor(dye);

        switch (item.Category)
        {
            case ItemCategory.SkinColor:
                look.Skin = (byte)item.FileId;
                break;

            case ItemCategory.FaceShape:
                look.Face = (byte)(item.FileId + 1);
                break;

            case ItemCategory.Hairstyle:
            case ItemCategory.Hat:
                look.Head = sprite;
                break;

            case ItemCategory.HairDye:
                look.HairColor = (byte)item.FileId;
                break;

            case ItemCategory.Armor:
                look.Armor = sprite;
                look.Arms = sprite;
                look.ArmorColor = color;
                ApplyBodyStyle(look, item.BodyStyle);
                break;

            case ItemCategory.ArmorDye:
                look.ArmorColor = (byte)item.FileId;
                break;

            case ItemCategory.Weapon:
                look.Weapon = sprite;
                break;

            case ItemCategory.Shield:
                look.Shield = sprite;
                break;

            case ItemCategory.Boots:
                look.Boots = sprite;
                look.BootsColor = color;
                break;

            case ItemCategory.Accessory:
                switch (accessorySlot ?? FirstFreeAccessory(look))
                {
                    case 2:
                        look.Accessory2 = sprite;
                        look.Accessory2Color = color;
                        break;

                    case 3:
                        look.Accessory3 = sprite;
                        look.Accessory3Color = color;
                        break;

                    default:
                        look.Accessory1 = sprite;
                        look.Accessory1Color = color;
                        break;
                }

                break;
        }
    }

    private static int FirstFreeAccessory(AislingLook look) =>
        look.Accessory1 == 0 ? 1 : look.Accessory2 == 0 ? 2 : look.Accessory3 == 0 ? 3 : 1;

    private static void ApplyBodyStyle(AislingLook look, ItemBodyStyle style)
    {
        var female = GenderOf(look) == ItemGender.Female;

        if (style == ItemBodyStyle.Saddle)
        {
            look.Form = (byte)(female ? 9 : 8);
            look.Pants = 0;
            look.Boots = 0;
            look.BootsColor = 0;
            return;
        }

        if (look.Form is 8 or 9)
        {
            look.Form = (byte)(female ? 2 : 1);
        }

        look.Pants = (byte)(style == ItemBodyStyle.BodySuit ? 1 : 0);
    }
}

[JsonSourceGenerationOptions(ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true)]
[JsonSerializable(typeof(List<CatalogItem>))]
internal sealed partial class ItemCatalogJson : JsonSerializerContext;
