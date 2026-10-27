using DaScreenshotStudio.Assets.Characters;
using DaScreenshotStudio.Engine.Catalog;
using DaScreenshotStudio.Engine.Rendering;

namespace DaScreenshotStudio.Engine.Staging;

public enum LookSlot
{
    Armor,
    Head,
    Boots,
    Weapon,
    Shield,
    Accessory1,
    Accessory2,
    Accessory3,
    ArmorColor,
    HairColor,
    BootsColor,
    Accessory1Color,
    Accessory2Color,
    Accessory3Color,
    Face,
    Skin,
    Pants,
}

public static class LookMatcher
{
    public static List<LookSlot> Order(SceneObject figure)
    {
        ArgumentNullException.ThrowIfNull(figure);

        var look = figure.Look ?? new AislingLook();
        var body = HumanBody.FromPacked((byte)((look.Form << 4) | (look.Pants & 0x0F)));
        var drawn = HumanPartOrder.Select((HumanDirection)figure.Direction, body.BodySprite);

        var order = new List<LookSlot>();
        for (var i = drawn.Length - 1; i >= 0; i--)
        {
            foreach (var slot in SlotsOf(drawn[i]))
            {
                if (!order.Contains(slot))
                {
                    order.Add(slot);
                }
            }
        }

        foreach (var slot in Enum.GetValues<LookSlot>())
        {
            if (!order.Contains(slot))
            {
                order.Add(slot);
            }
        }

        return order;
    }

    private static LookSlot[] SlotsOf(int category) => category switch
    {
        HumanPartCategory.Shield => [LookSlot.Shield],
        HumanPartCategory.Body => [LookSlot.Skin],
        HumanPartCategory.Pants => [LookSlot.Pants],
        HumanPartCategory.Boots => [LookSlot.Boots, LookSlot.BootsColor],
        HumanPartCategory.Head or HumanPartCategory.HeadAlternate1 or HumanPartCategory.HeadAlternate2 =>
            [LookSlot.Head, LookSlot.HairColor],
        HumanPartCategory.Armor or HumanPartCategory.Arms => [LookSlot.Armor, LookSlot.ArmorColor],
        HumanPartCategory.Weapon or HumanPartCategory.WeaponAlternate => [LookSlot.Weapon],
        HumanPartCategory.Accessory1Front or HumanPartCategory.Accessory1Back => [LookSlot.Accessory1, LookSlot.Accessory1Color],
        HumanPartCategory.Accessory2Front or HumanPartCategory.Accessory2Back => [LookSlot.Accessory2, LookSlot.Accessory2Color],
        HumanPartCategory.Accessory3Front or HumanPartCategory.Accessory3Back => [LookSlot.Accessory3, LookSlot.Accessory3Color],
        HumanPartCategory.Face => [LookSlot.Face],
        _ => [],
    };

    public readonly record struct Candidate(AislingLook Look, string Label, bool IsEmpty = false);

    public readonly record struct Result(AislingLook Look, string Label, int Differing, int Tried);

    public static List<Candidate> Candidates(LookSlot slot, AislingLook current, ItemCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(catalog);

        var gender = Dressing.GenderOf(current);
        var candidates = new List<Candidate> { new(current.Clone(), "as staged") };

        void Item(ItemCategory category, int? accessorySlot = null, byte dye = 0)
        {
            foreach (var item in catalog.InCategory(category))
            {
                if (!item.Fits(gender))
                {
                    continue;
                }

                var look = current.Clone();
                Dressing.Wear(look, item, dye, accessorySlot);
                candidates.Add(new Candidate(look, $"{item.Name} (#{item.FileId})"));
            }
        }

        void Value(Action<AislingLook> assign, string label, bool isEmpty = false)
        {
            var look = current.Clone();
            assign(look);
            candidates.Add(new Candidate(look, label, isEmpty));
        }

        switch (slot)
        {
            case LookSlot.Armor:
                Item(ItemCategory.Armor, dye: current.ArmorColor);
                Value(l => { l.Armor = 0; l.Arms = 0; }, "no armor", true);
                break;

            case LookSlot.Head:
                Item(ItemCategory.Hairstyle);
                Item(ItemCategory.Hat);
                Value(l => l.Head = 0, "no head", true);
                break;

            case LookSlot.Boots:
                Item(ItemCategory.Boots, dye: current.BootsColor);
                Value(l => { l.Boots = 0; l.BootsColor = 0; }, "no boots", true);
                break;

            case LookSlot.Weapon:
                Item(ItemCategory.Weapon);
                Value(l => l.Weapon = 0, "no weapon", true);
                break;

            case LookSlot.Shield:
                Item(ItemCategory.Shield);
                Value(l => l.Shield = 0, "no shield", true);
                break;

            case LookSlot.Accessory1:
                Item(ItemCategory.Accessory, 1, current.Accessory1Color);
                Value(l => l.Accessory1 = 0, "no accessory", true);
                break;

            case LookSlot.Accessory2:
                Item(ItemCategory.Accessory, 2, current.Accessory2Color);
                Value(l => l.Accessory2 = 0, "no accessory", true);
                break;

            case LookSlot.Accessory3:
                Item(ItemCategory.Accessory, 3, current.Accessory3Color);
                Value(l => l.Accessory3 = 0, "no accessory", true);
                break;

            case LookSlot.ArmorColor:
                Dyes(catalog, ItemCategory.ArmorDye, (l, d) => l.ArmorColor = d, current, candidates);
                break;

            case LookSlot.HairColor:
                Dyes(catalog, ItemCategory.HairDye, (l, d) => l.HairColor = d, current, candidates);
                break;

            case LookSlot.BootsColor:
                Dyes(catalog, null, (l, d) => l.BootsColor = d, current, candidates);
                break;

            case LookSlot.Accessory1Color:
                Dyes(catalog, null, (l, d) => l.Accessory1Color = d, current, candidates);
                break;

            case LookSlot.Accessory2Color:
                Dyes(catalog, null, (l, d) => l.Accessory2Color = d, current, candidates);
                break;

            case LookSlot.Accessory3Color:
                Dyes(catalog, null, (l, d) => l.Accessory3Color = d, current, candidates);
                break;

            case LookSlot.Face:
                Item(ItemCategory.FaceShape);
                Value(l => l.Face = 0, "no face", true);
                break;

            case LookSlot.Skin:
                Item(ItemCategory.SkinColor);
                break;

            case LookSlot.Pants:
                for (var pants = 0; pants < 16; pants++)
                {
                    var dye = (byte)pants;
                    Value(l => l.Pants = dye, pants == 0 ? "no pants" : $"pants dye {pants}", pants == 0);
                }

                break;
        }

        var empty = candidates.FindIndex(1, c => c.IsEmpty);
        if (empty > 1)
        {
            var moved = candidates[empty];
            candidates.RemoveAt(empty);
            candidates.Insert(1, moved);
        }

        var seen = new HashSet<string>();
        return [.. candidates.Where(c => seen.Add(LookKey(c.Look)))];
    }

    private static void Dyes(
        ItemCatalog catalog, ItemCategory? category, Action<AislingLook, byte> assign, AislingLook current, List<Candidate> into)
    {
        var dyes = catalog.Items
            .Where(i => category is { } c ? i.Category == c : i.Category is ItemCategory.ArmorDye or ItemCategory.HairDye)
            .GroupBy(i => (byte)i.FileId)
            .Select(g => (Dye: g.Key, Name: g.First().Name))
            .Prepend(((byte)0, "undyed"));

        foreach (var (dye, name) in dyes)
        {
            var look = current.Clone();
            assign(look, dye);
            into.Add(new Candidate(look, $"{name} (dye {dye})"));
        }
    }

    private static string LookKey(AislingLook look) =>
        string.Join(',', look.Form, look.Pants, look.Head, look.HairColor, look.Face, look.Skin, look.Armor, look.Arms,
            look.ArmorColor, look.Overcoat, look.OvercoatColor, look.Boots, look.BootsColor, look.Weapon, look.Shield,
            look.Accessory1, look.Accessory1Color, look.Accessory2, look.Accessory2Color, look.Accessory3,
            look.Accessory3Color, look.Translucent);

    public static Result MatchSlot(
        SceneHost host,
        SceneDocument scene,
        int index,
        LookSlot slot,
        ItemCatalog catalog,
        Screenshot reference,
        MapCatalog? maps = null,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(scene);

        var trial = scene.Clone();
        var figure = FigureOf(trial, index);
        var candidates = Candidates(slot, figure.Look ??= new AislingLook(), catalog);

        SceneApplier.Apply(host, trial, maps);
        var region = FigureRegion.Around(host, figure);

        ushort[]? emptyPixels = null;
        if (candidates.Find(c => c.IsEmpty) is { IsEmpty: true } emptyCandidate)
        {
            figure.Look = emptyCandidate.Look;
            emptyPixels = FigureRegion.Render(host, trial, region, maps);
        }

        var best = new Result(candidates[0].Look, candidates[0].Label, int.MaxValue, candidates.Count);
        foreach (var candidate in candidates)
        {
            cancellation.ThrowIfCancellationRequested();
            figure.Look = candidate.Look;
            var pixels = FigureRegion.Render(host, trial, region, maps);

            if (emptyPixels is not null && !candidate.IsEmpty && pixels.AsSpan().SequenceEqual(emptyPixels))
            {
                continue;
            }

            var differing = FigureRegion.CountDiffering(reference, region, pixels);
            if (differing < best.Differing)
            {
                best = best with { Look = candidate.Look, Label = candidate.Label, Differing = differing };
            }
        }

        return best;
    }

    public static Result MatchLook(
        SceneHost host,
        SceneDocument scene,
        int index,
        ItemCatalog catalog,
        Screenshot reference,
        MapCatalog? maps = null,
        IProgress<string>? progress = null,
        int maxPasses = 3,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(scene);

        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(reference);

        var working = scene.Clone();
        var figure = FigureOf(working, index);
        figure.Look ??= new AislingLook();

        SceneApplier.Apply(host, working, maps);
        var region = FigureRegion.Around(host, figure);
        var best = new Result(figure.Look.Clone(), "as staged", FigureRegion.Score(host, working, reference, region, maps), 1);
        var tried = 1;

        for (var pass = 0; pass < maxPasses && best.Differing > 0; pass++)
        {
            var improved = false;
            foreach (var slot in Order(figure))
            {
                var result = MatchSlot(host, working, index, slot, catalog, reference, maps, cancellation);
                tried += result.Tried;

                if (result.Differing < best.Differing)
                {
                    improved = true;
                    best = result;
                    figure.Look = result.Look.Clone();
                    progress?.Report($"{slot}: {result.Label}, {result.Differing} pixels differ around the figure");

                    if (best.Differing == 0)
                    {
                        break;
                    }
                }
            }

            if (!improved)
            {
                break;
            }
        }

        return best with { Tried = tried };
    }

    private static SceneObject FigureOf(SceneDocument scene, int index)
    {
        var figure = index < 0 ? scene.Player : scene.Objects[index];
        if (figure.Kind != SceneObjectKind.Aisling)
        {
            throw new ArgumentException("Only an aisling has a look to match.");
        }

        return figure;
    }
}

public static class FigureRegion
{
    public const int HalfWidth = 64;

    public const int Above = 112;

    public const int Below = 24;

    public static PixelRect Around(SceneHost host, SceneObject figure)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(figure);

        var (x, y) = host.CellCenter(figure.X, figure.Y);
        x += figure.OffsetX;
        y += figure.OffsetY;

        var view = host.ViewRectangle;
        var left = Math.Max(view.X, x - HalfWidth);
        var top = Math.Max(view.Y, y - Above);
        var right = Math.Min(view.Right, x + HalfWidth);
        var bottom = Math.Min(view.Bottom, y + Below);
        return right <= left || bottom <= top ? view : PixelRect.FromEdges(left, top, right, bottom);
    }

    public static int Score(SceneHost host, SceneDocument scene, Screenshot reference, PixelRect region, MapCatalog? maps) =>
        CountDiffering(reference, region, Render(host, scene, region, maps));

    public static ushort[] Render(SceneHost host, SceneDocument scene, PixelRect region, MapCatalog? maps)
    {
        ArgumentNullException.ThrowIfNull(host);

        SceneApplier.Apply(host, scene, maps);
        var frame = Screenshot.Decode(host.EncodeBmp());
        var pixels = new ushort[Math.Max(0, region.Width * region.Height)];

        for (var y = 0; y < region.Height; y++)
        {
            for (var x = 0; x < region.Width; x++)
            {
                pixels[(y * region.Width) + x] = frame[region.X + x, region.Y + y];
            }
        }

        return pixels;
    }

    public static int CountDiffering(Screenshot reference, PixelRect region, ReadOnlySpan<ushort> pixels)
    {
        ArgumentNullException.ThrowIfNull(reference);

        var count = 0;
        for (var y = 0; y < region.Height; y++)
        {
            for (var x = 0; x < region.Width; x++)
            {
                if (pixels[(y * region.Width) + x] != reference[region.X + x, region.Y + y])
                {
                    count++;
                }
            }
        }

        return count;
    }
}
