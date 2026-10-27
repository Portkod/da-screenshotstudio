using DaScreenshotStudio.Engine.Catalog;
using DaScreenshotStudio.Engine.Rendering;
using DaScreenshotStudio.Engine.World;

namespace DaScreenshotStudio.Engine.Staging;

public static class ItemMatcher
{
    public const int Shortlist = 40;

    public readonly record struct Result(ushort Sprite, byte Color, int Differing, int Ceiling, int Rendered);

    public readonly record struct Loose(int X, int Y, ushort Sprite, byte Color, int Before, int After);

    public const int LooseThreshold = 16;

    public static List<Loose> FindLoose(
        SceneHost host,
        SceneDocument scene,
        Screenshot reference,
        IReadOnlyList<byte> dyes,
        MapCatalog? maps = null,
        IProgress<string>? progress = null,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(dyes);

        var probe = scene.Clone();
        probe.Objects.Add(new SceneObject { Kind = SceneObjectKind.Item, X = scene.Player.X, Y = scene.Player.Y, Sprite = 1 });
        SceneApplier.Apply(host, probe, maps);
        host.TryGetObjectAnchor(SceneApplier.EntityOf(probe.Objects.Count - 1), out var probeX, out var probeY);
        var (centerX, centerY) = host.CellCenter(scene.Player.X, scene.Player.Y);
        var liftX = probeX - centerX;
        var liftY = probeY - centerY;

        var bare = BareFrame(host, scene, maps);
        SceneApplier.Apply(host, scene, maps);
        var current = Screenshot.Decode(host.EncodeBmp());
        var view = host.ViewRectangle;
        var ceiling = host.HighestItemSprite();
        var occupied = scene.Objects.Where(o => o.Kind == SceneObjectKind.Item).Select(o => (o.X, o.Y)).ToHashSet();

        var cells = new List<(int X, int Y, int AnchorX, int AnchorY)>();
        for (var y = scene.Player.Y - 24; y <= scene.Player.Y + 24; y++)
        {
            for (var x = scene.Player.X - 24; x <= scene.Player.X + 24; x++)
            {
                if (x < 0 || y < 0 || occupied.Contains((x, y)))
                {
                    continue;
                }

                var (cx, cy) = host.CellCenter(x, y);
                if (view.Contains(cx + liftX, cy + liftY))
                {
                    cells.Add((x, y, cx + liftX, cy + liftY));
                }
            }
        }

        var found = new List<Loose>();
        var checkedCells = 0;

        foreach (var (x, y, anchorX, anchorY) in cells)
        {
            cancellation.ThrowIfCancellationRequested();
            if (++checkedCells % 50 == 0)
            {
                progress?.Report($"Looking for loose items... {checkedCells} of {cells.Count} cells, {found.Count} found");
            }

            var canvas = Clip(
                new PixelRect(anchorX - GroundItemArt.Anchor, anchorY - GroundItemArt.Anchor, GroundItemArt.CanvasSize, GroundItemArt.CanvasSize),
                view);
            var outline = Outline(reference, current, canvas);
            var before = outline.Count(d => d);
            if (before < LooseThreshold)
            {
                continue;
            }

            var shapes = Rank(host, ceiling, anchorX, anchorY, canvas, outline, Occluders(current, bare, canvas), cancellation);
            if (shapes.Count == 0 || shapes[0].Score * 2 > before)
            {
                continue;
            }

            var trial = scene.Clone();
            var item = new SceneObject { Kind = SceneObjectKind.Item, X = x, Y = y };
            trial.Objects.Add(item);

            var best = (Sprite: 0, Color: (byte)0, Differing: int.MaxValue);
            foreach (var (_, sprite) in shapes.Take(10))
            {
                item.Sprite = (ushort)sprite;
                var differing = FigureRegion.Score(host, trial, reference, canvas, maps);
                if (differing < best.Differing)
                {
                    best = (sprite, 0, differing);
                }
            }

            item.Sprite = (ushort)best.Sprite;
            foreach (var dye in dyes.Skip(1))
            {
                item.Color = dye;
                var differing = FigureRegion.Score(host, trial, reference, canvas, maps);
                if (differing < best.Differing)
                {
                    best = best with { Color = dye, Differing = differing };
                }
            }

            if (best.Differing * 2 <= before)
            {
                found.Add(new Loose(x, y, (ushort)best.Sprite, best.Color, before, best.Differing));
            }
        }

        SceneApplier.Apply(host, scene, maps);
        return found;
    }

    private static List<(int Score, int Sprite)> Rank(
        SceneHost host, int ceiling, int anchorX, int anchorY, PixelRect canvas, bool[] outline, bool[] occluders, CancellationToken cancellation)
    {
        var shapes = new List<(int Score, int Sprite)>();
        for (var sprite = 1; sprite <= ceiling; sprite++)
        {
            cancellation.ThrowIfCancellationRequested();
            if (host.TryGetItemSprite(sprite, 0, out var art) && !art.IsEmpty)
            {
                shapes.Add((ShapeScore(in art, anchorX, anchorY, canvas, outline, occluders), sprite));
            }
        }

        shapes.Sort();
        return shapes;
    }

    public static List<byte> Dyes(ItemCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return [.. catalog.Items
            .Where(i => i.Category is ItemCategory.ArmorDye or ItemCategory.HairDye)
            .Select(i => (byte)i.FileId)
            .Prepend((byte)0)
            .Distinct()];
    }

    public static Result Find(
        SceneHost host,
        SceneDocument scene,
        int index,
        Screenshot reference,
        IReadOnlyList<byte> dyes,
        MapCatalog? maps = null,
        IProgress<string>? progress = null,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(dyes);

        var trial = scene.Clone();
        var item = trial.Objects[index];
        if (item.Kind != SceneObjectKind.Item)
        {
            throw new ArgumentException("Only a ground item has an item sprite to match.");
        }

        SceneApplier.Apply(host, trial, maps);
        if (!host.TryGetObjectAnchor(SceneApplier.EntityOf(index), out var anchorX, out var anchorY))
        {
            throw new ArgumentException("The item is not in the world.");
        }

        var canvas = Clip(
            new PixelRect(anchorX - GroundItemArt.Anchor, anchorY - GroundItemArt.Anchor, GroundItemArt.CanvasSize, GroundItemArt.CanvasSize),
            host.ViewRectangle);

        var without = trial.Clone();
        without.Objects.RemoveAt(index);
        SceneApplier.Apply(host, without, maps);
        var background = Screenshot.Decode(host.EncodeBmp());
        var outline = Outline(reference, background, canvas);
        var occluders = Occluders(background, BareFrame(host, without, maps), canvas);

        var ceiling = host.HighestItemSprite();
        progress?.Report($"Comparing the shapes of {ceiling} item sprites...");
        var shapes = Rank(host, ceiling, anchorX, anchorY, canvas, outline, occluders, cancellation);

        var shortlist = shapes.Take(Shortlist).Select(s => s.Sprite).ToList();
        if (!shortlist.Contains(item.Sprite) && item.Sprite > 0)
        {
            shortlist.Insert(0, item.Sprite);
        }

        progress?.Report($"Drawing the {shortlist.Count} closest shapes...");

        var best = new Result(item.Sprite, item.Color, int.MaxValue, ceiling, 0);
        var rendered = 0;

        foreach (var sprite in shortlist)
        {
            cancellation.ThrowIfCancellationRequested();
            item.Sprite = (ushort)sprite;
            var differing = FigureRegion.Score(host, trial, reference, canvas, maps);
            rendered++;

            if (differing < best.Differing)
            {
                best = best with { Sprite = (ushort)sprite, Differing = differing };
            }
        }

        item.Sprite = best.Sprite;
        foreach (var dye in dyes)
        {
            cancellation.ThrowIfCancellationRequested();
            item.Color = dye;
            var differing = FigureRegion.Score(host, trial, reference, canvas, maps);
            rendered++;

            if (differing < best.Differing)
            {
                best = best with { Color = dye, Differing = differing };
            }
        }

        return best with { Rendered = rendered };
    }

    private static PixelRect Clip(PixelRect rect, PixelRect view) =>
        PixelRect.FromEdges(
            Math.Max(rect.X, view.X), Math.Max(rect.Y, view.Y), Math.Min(rect.Right, view.Right), Math.Min(rect.Bottom, view.Bottom));

    private static bool[] Outline(Screenshot reference, Screenshot background, PixelRect canvas)
    {
        var outline = new bool[Math.Max(0, canvas.Width * canvas.Height)];
        for (var y = 0; y < canvas.Height; y++)
        {
            for (var x = 0; x < canvas.Width; x++)
            {
                outline[(y * canvas.Width) + x] = reference[canvas.X + x, canvas.Y + y] != background[canvas.X + x, canvas.Y + y];
            }
        }

        return outline;
    }

    private static Screenshot BareFrame(SceneHost host, SceneDocument scene, MapCatalog? maps)
    {
        var bare = scene.Clone();
        bare.Objects.Clear();
        bare.Player.Look = new AislingLook { Form = 0 };
        bare.Player.Balloon = null;
        SceneApplier.Apply(host, bare, maps);
        return Screenshot.Decode(host.EncodeBmp());
    }

    private static bool[] Occluders(Screenshot staged, Screenshot bare, PixelRect canvas) => Outline(staged, bare, canvas);

    private static int ShapeScore(in WorldSprite art, int anchorX, int anchorY, PixelRect canvas, bool[] outline, bool[] occluders)
    {
        var left = anchorX - art.AnchorX;
        var top = anchorY - art.AnchorY;
        var score = 0;

        for (var y = 0; y < canvas.Height; y++)
        {
            var spriteY = canvas.Y + y - top;
            var row = (uint)spriteY < (uint)art.Art.Height ? art.Art.Row(spriteY) : default;

            for (var x = 0; x < canvas.Width; x++)
            {
                var spriteX = canvas.X + x - left;
                var covers = (uint)spriteX < (uint)row.Length && row[spriteX] != 0;
                var at = (y * canvas.Width) + x;
                if (outline[at] ? !covers : covers && !occluders[at])
                {
                    score++;
                }
            }
        }

        return score;
    }
}
