using DaScreenshotStudio.Engine.Records;
using DaScreenshotStudio.Engine.Ui;

namespace DaScreenshotStudio.Engine.Staging;

public static class IconMatcher
{
    public readonly record struct Pick(int Slot, int? Icon, int Differing, CooldownKind Cooldown = CooldownKind.None, byte Dye = 0);

    public static List<Pick> MatchEffects(
        SceneHost host, SceneDocument scene, Screenshot reference, MapCatalog? maps = null, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(reference);

        var trial = scene.Clone();
        var effects = trial.Hud.StatusEffects;
        var boxes = new List<(int Slot, Rendering.PixelRect Box)>();
        for (var i = 0; i < Math.Min(effects.Count, SpelledViewPane.SlotCount); i++)
        {
            if (effects[i].Icon is not null)
            {
                boxes.Add((i, new Rendering.PixelRect(
                    SpelledViewPane.OriginX + SpelledViewPane.IconOffset,
                    SpelledViewPane.OriginY + (i * SpelledViewPane.SlotPitch) + SpelledViewPane.IconOffset,
                    SpelledViewPane.IconBoxSize,
                    SpelledViewPane.IconBoxSize)));
            }
        }

        var best = boxes.ToDictionary(b => b.Slot, b => (Icon: (int?)effects[b.Slot].Icon, Differing: int.MaxValue));
        for (var icon = 1; icon <= host.HighestAbilityIcon(spells: true); icon++)
        {
            cancellation.ThrowIfCancellationRequested();
            if (!host.TryGetSpellIcon(icon, out _))
            {
                continue;
            }

            foreach (var (slot, _) in boxes)
            {
                effects[slot].Icon = icon;
            }

            var frame = Render(host, trial, maps);
            foreach (var (slot, box) in boxes)
            {
                var differing = ScreenshotDiff.Compare(reference, frame).CountIn(box.X, box.Y, box.Width, box.Height);
                if (differing < best[slot].Differing)
                {
                    best[slot] = (icon, differing);
                }
            }
        }

        SceneApplier.Apply(host, scene, maps);
        return [.. boxes.Select(b => new Pick(b.Slot, best[b.Slot].Icon, best[b.Slot].Differing))];
    }

    public static List<Pick> MatchBook(
        SceneHost host, SceneDocument scene, bool spells, Screenshot reference, MapCatalog? maps = null, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(reference);

        var trial = scene.Clone();
        var book = spells ? trial.Hud.Spells : trial.Hud.Skills;
        book.Clear();
        SceneApplier.Apply(host, trial, maps);

        var cells = new List<(int Slot, Rendering.PixelRect Cell)>();
        for (var slot = 1; slot <= PlayerModel.AbilityCapacity; slot++)
        {
            if (host.TryGetBookSlot(spells, slot, out var cell))
            {
                cells.Add((slot, cell));
            }
        }

        var empty = Render(host, trial, maps);
        var best = cells.ToDictionary(
            c => c.Slot,
            c => (Icon: (int?)null, Differing: ScreenshotDiff.Compare(reference, empty).CountIn(c.Cell.X, c.Cell.Y, c.Cell.Width, c.Cell.Height), Cooldown: CooldownKind.None));

        for (var icon = 0; icon <= host.HighestAbilityIcon(spells); icon++)
        {
            foreach (var cooldown in (ReadOnlySpan<CooldownKind>)[CooldownKind.None, CooldownKind.Locked])
            {
                cancellation.ThrowIfCancellationRequested();
                book.Clear();
                book.AddRange(cells.Select(c => new SceneSlot { Slot = c.Slot, Icon = icon, Cooldown = cooldown }));

                var frame = Render(host, trial, maps);
                var diff = ScreenshotDiff.Compare(reference, frame);
                foreach (var (slot, cell) in cells)
                {
                    var differing = diff.CountIn(cell.X, cell.Y, cell.Width, cell.Height);
                    if (differing < best[slot].Differing)
                    {
                        best[slot] = (icon, differing, cooldown);
                    }
                }
            }
        }

        SceneApplier.Apply(host, scene, maps);
        return [.. cells.Select(c => new Pick(c.Slot, best[c.Slot].Icon, best[c.Slot].Differing, best[c.Slot].Cooldown))];
    }

    public static List<Pick> MatchInventory(
        SceneHost host,
        SceneDocument scene,
        Screenshot reference,
        IReadOnlyList<byte> dyes,
        MapCatalog? maps = null,
        IProgress<string>? progress = null,
        CancellationToken cancellation = default) =>
        MatchItems(host, scene, reference, dyes, equipment: false, maps, progress, cancellation);

    public static List<Pick> MatchEquipment(
        SceneHost host,
        SceneDocument scene,
        Screenshot reference,
        IReadOnlyList<byte> dyes,
        MapCatalog? maps = null,
        IProgress<string>? progress = null,
        CancellationToken cancellation = default) =>
        MatchItems(host, scene, reference, dyes, equipment: true, maps, progress, cancellation);

    private static List<Pick> MatchItems(
        SceneHost host,
        SceneDocument scene,
        Screenshot reference,
        IReadOnlyList<byte> dyes,
        bool equipment,
        MapCatalog? maps,
        IProgress<string>? progress,
        CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(dyes);

        const int Shortlist = 6;
        var trial = scene.Clone();
        var items = equipment ? trial.Hud.Equipment : trial.Hud.Inventory;
        items.Clear();
        SceneApplier.Apply(host, trial, maps);

        var cells = new List<(int Slot, Rendering.PixelRect Cell)>();
        for (var slot = 1; slot <= (equipment ? PlayerModel.EquipmentCapacity : PlayerModel.InventoryCapacity); slot++)
        {
            if (equipment ? host.TryGetEquipmentSlot(slot, out var cell) : host.TryGetTraySlot(slot, out cell))
            {
                cells.Add((slot, cell));
            }
        }

        var empty = Render(host, trial, maps);
        var emptyDiff = ScreenshotDiff.Compare(reference, empty);
        var best = cells.ToDictionary(
            c => c.Slot,
            c => (Icon: (int?)null, Dye: (byte)0, Differing: emptyDiff.CountIn(c.Cell.X, c.Cell.Y, c.Cell.Width, c.Cell.Height)));
        var shortlists = cells.ToDictionary(c => c.Slot, _ => new List<(int Score, int Icon)>());

        var highest = host.HighestItemIcon();
        for (var icon = 1; icon <= highest; icon++)
        {
            cancellation.ThrowIfCancellationRequested();
            if (!host.HasItemIcon(icon))
            {
                continue;
            }

            if (icon % 500 == 0)
            {
                progress?.Report($"Item icon {icon} of {highest}");
            }

            items.Clear();
            items.AddRange(cells.Where(c => best[c.Slot].Differing > 0).Select(c => new SceneItem { Slot = c.Slot, Icon = icon }));
            if (items.Count == 0)
            {
                break;
            }

            var frame = Render(host, trial, maps);
            foreach (var item in items)
            {
                var cell = cells.First(c => c.Slot == item.Slot).Cell;
                var (shape, exact) = Score(reference, empty, frame, cell);
                if (exact < best[item.Slot].Differing)
                {
                    best[item.Slot] = (icon, 0, exact);
                }

                var list = shortlists[item.Slot];
                if (list.Count < Shortlist || shape < list[^1].Score)
                {
                    list.Add((shape, icon));
                    list.Sort();
                    if (list.Count > Shortlist)
                    {
                        list.RemoveAt(list.Count - 1);
                    }
                }
            }
        }

        for (var rank = 0; rank < Shortlist; rank++)
        {
            progress?.Report($"Dyeing the shapes ranked {rank + 1}");
            foreach (var dye in dyes.Where(d => d != 0))
            {
                cancellation.ThrowIfCancellationRequested();
                items.Clear();
                items.AddRange(cells
                    .Where(c => shortlists[c.Slot].Count > rank && best[c.Slot].Differing > 0)
                    .Select(c => new SceneItem { Slot = c.Slot, Icon = shortlists[c.Slot][rank].Icon, Dye = dye }));
                if (items.Count == 0)
                {
                    break;
                }

                var diff = ScreenshotDiff.Compare(reference, Render(host, trial, maps));
                foreach (var item in items)
                {
                    var cell = cells.First(c => c.Slot == item.Slot).Cell;
                    var differing = diff.CountIn(cell.X, cell.Y, cell.Width, cell.Height);
                    if (differing < best[item.Slot].Differing)
                    {
                        best[item.Slot] = (item.Icon, dye, differing);
                    }
                }
            }
        }

        SceneApplier.Apply(host, scene, maps);
        return [.. cells.Select(c => new Pick(c.Slot, best[c.Slot].Icon, best[c.Slot].Differing, Dye: best[c.Slot].Dye))];
    }

    private static (int Shape, int Exact) Score(Screenshot reference, Screenshot empty, Screenshot candidate, Rendering.PixelRect cell)
    {
        int shape = 0, exact = 0;
        for (var y = cell.Y; y < cell.Bottom; y++)
        {
            for (var x = cell.X; x < cell.Right; x++)
            {
                var background = empty[x, y];
                var wanted = reference[x, y];
                var drawn = candidate[x, y];
                if ((wanted != background) != (drawn != background))
                {
                    shape++;
                }

                if (wanted != drawn)
                {
                    exact++;
                }
            }
        }

        return (shape, exact);
    }

    public sealed record Report(SceneDocument Scene, IReadOnlyList<string> Lines, int Differing);

    public static Report MatchAll(
        SceneHost host,
        SceneDocument scene,
        Screenshot reference,
        IReadOnlyList<byte> dyes,
        MapCatalog? maps = null,
        IProgress<string>? progress = null,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(scene);

        var result = scene.Clone();
        var lines = new List<string>();
        var differing = 0;

        progress?.Report("Matching the effect strip");
        foreach (var pick in MatchEffects(host, result, reference, maps, cancellation))
        {
            result.Hud.StatusEffects[pick.Slot].Icon = pick.Icon;
            lines.Add($"effect slot {pick.Slot}: icon {pick.Icon}, {pick.Differing} differing");
            differing += pick.Differing;
        }

        bool[] books = result.Hud.Panel switch
        {
            ScenePanel.SpellsPage1 or ScenePanel.SpellsPage2 => [true],
            ScenePanel.SkillsPage1 or ScenePanel.SkillsPage2 => [false],
            ScenePanel.General => [false, true],
            _ => [],
        };

        foreach (var spells in books)
        {
            progress?.Report(spells ? "Matching the spell book" : "Matching the skill book");
            var picks = MatchBook(host, result, spells, reference, maps, cancellation);
            var book = spells ? result.Hud.Spells : result.Hud.Skills;
            var shown = picks.Select(p => p.Slot).ToHashSet();
            book.RemoveAll(s => shown.Contains(s.Slot));
            book.AddRange(picks.Where(p => p.Icon is not null).Select(p => new SceneSlot { Slot = p.Slot, Icon = p.Icon!.Value, Cooldown = p.Cooldown }));
            book.Sort((a, b) => a.Slot.CompareTo(b.Slot));
            foreach (var pick in picks.Where(p => p.Icon is not null))
            {
                var cooled = pick.Cooldown == CooldownKind.None ? "" : $", {pick.Cooldown.ToString().ToLowerInvariant()}";
                lines.Add($"{(spells ? "spell" : "skill")} slot {pick.Slot}: icon {pick.Icon}{cooled}, {pick.Differing} differing");
            }

            differing += picks.Sum(p => p.Differing);
        }

        if (result.Hud.Panel == ScenePanel.Inventory)
        {
            var picks = MatchInventory(host, result, reference, dyes, maps, progress, cancellation);
            Replace(result.Hud.Inventory, picks, "item");
        }

        if (result.Hud.Window is { Kind: SceneWindowKind.Profile } window)
        {
            if (window.Profile.Page == 0)
            {
                var picks = MatchEquipment(host, result, reference, dyes, maps, progress, cancellation);
                Replace(result.Hud.Equipment, picks, "worn");
            }
            else if (window.Profile.Page == UserInfoPagePane.LegendPage)
            {
                progress?.Report("Matching the legend marks");
                foreach (var pick in MatchLegend(host, result, reference, maps, cancellation))
                {
                    window.Profile.Legend[pick.Row].Icon = pick.Icon;
                    window.Profile.Legend[pick.Row].Color = pick.Color;
                    lines.Add($"legend row {pick.Row}: {pick.Icon}, color {pick.Color}, {pick.Differing} differing");
                    differing += pick.Differing;
                }
            }
        }

        SceneApplier.Apply(host, scene, maps);
        return new Report(result, lines, differing);

        void Replace(List<SceneItem> items, List<Pick> picks, string noun)
        {
            var shown = picks.Select(p => p.Slot).ToHashSet();
            items.RemoveAll(i => shown.Contains(i.Slot));
            items.AddRange(picks.Where(p => p.Icon is not null).Select(p => new SceneItem { Slot = p.Slot, Icon = p.Icon!.Value, Dye = p.Dye }));
            items.Sort((a, b) => a.Slot.CompareTo(b.Slot));
            foreach (var pick in picks.Where(p => p.Icon is not null))
            {
                lines.Add($"{noun} slot {pick.Slot}: icon {pick.Icon}, dye {pick.Dye}, {pick.Differing} differing");
            }

            differing += picks.Sum(p => p.Differing);
        }
    }

    public readonly record struct LegendPick(int Row, LegendMarkIcon Icon, byte Color, int Differing);

    public static List<LegendPick> MatchLegend(
        SceneHost host, SceneDocument scene, Screenshot reference, MapCatalog? maps = null, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(reference);

        var trial = scene.Clone();
        var marks = trial.Hud.Window.Profile.Legend;
        SceneApplier.Apply(host, trial, maps);
        if (host.ProfilePages[UserInfoPagePane.LegendPage - UserInfoPagePane.FirstPage].LegendList is not { Visible: true } list)
        {
            return [];
        }

        var area = SceneHost.ScreenRect(list);
        var rows = Math.Min(marks.Count, area.Height / LegendListPane.RowHeight);
        Rendering.PixelRect Icon(int row) =>
            new(area.X, area.Y + (row * LegendListPane.RowHeight), LegendListPane.TextIndent, LegendListPane.RowHeight);
        Rendering.PixelRect Text(int row) =>
            new(area.X + LegendListPane.TextIndent, area.Y + (row * LegendListPane.RowHeight), area.Width - LegendListPane.TextIndent, LegendListPane.RowHeight);

        var icons = new (LegendMarkIcon Icon, int Differing)[rows];
        Array.Fill(icons, (LegendMarkIcon.Aisling, int.MaxValue));
        for (var icon = 0; icon < (int)LegendMarkIcon.NoneSentinel; icon++)
        {
            cancellation.ThrowIfCancellationRequested();
            marks.ForEach(m => m.Icon = (LegendMarkIcon)icon);
            var diff = ScreenshotDiff.Compare(reference, Render(host, trial, maps));
            for (var row = 0; row < rows; row++)
            {
                var box = Icon(row);
                var differing = diff.CountIn(box.X, box.Y, box.Width, box.Height);
                if (differing < icons[row].Differing)
                {
                    icons[row] = ((LegendMarkIcon)icon, differing);
                }
            }
        }

        for (var row = 0; row < rows; row++)
        {
            marks[row].Icon = icons[row].Icon;
        }

        var colors = new (byte Color, int Differing)[rows];
        Array.Fill(colors, ((byte)0, int.MaxValue));
        for (var color = 0; color <= byte.MaxValue; color++)
        {
            cancellation.ThrowIfCancellationRequested();
            for (var row = 0; row < rows; row++)
            {
                marks[row].Color = (byte)color;
            }

            var diff = ScreenshotDiff.Compare(reference, Render(host, trial, maps));
            for (var row = 0; row < rows; row++)
            {
                var box = Text(row);
                var differing = diff.CountIn(box.X, box.Y, box.Width, box.Height);
                if (differing < colors[row].Differing)
                {
                    colors[row] = ((byte)color, differing);
                }
            }
        }

        SceneApplier.Apply(host, scene, maps);
        return [.. Enumerable.Range(0, rows).Select(r => new LegendPick(r, icons[r].Icon, colors[r].Color, icons[r].Differing + colors[r].Differing))];
    }

    private static Screenshot Render(SceneHost host, SceneDocument scene, MapCatalog? maps)
    {
        SceneApplier.Apply(host, scene, maps);
        return Screenshot.Decode(host.EncodeBmp());
    }
}
