using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using DaScreenshotStudio.Engine.Core;
using DaScreenshotStudio.Engine.Staging;

namespace DaScreenshotStudio.App.ViewModels;

public enum SpriteBank
{
    Creatures,
    Items,
}

/// <summary>A sprite dragged or picked from the browser, with the dye and facing it was previewed in.</summary>
public sealed record SpritePick(SceneObjectKind Kind, int Sprite, byte Dye, Direction Facing);

/// <summary>One thumbnail in the browser. Its preview is drawn the first time the list shows it.</summary>
public sealed partial class SpriteTileViewModel : ObservableObject
{
    private readonly Func<SpriteTileViewModel, Bitmap?> _render;
    private Bitmap? _preview;
    private bool _rendered;

    public SpriteTileViewModel(SceneObjectKind kind, int sprite, Func<SpriteTileViewModel, Bitmap?> render)
    {
        Kind = kind;
        Sprite = sprite;
        _render = render;
    }

    public SceneObjectKind Kind { get; }

    public int Sprite { get; }

    public string Label => Sprite.ToString(CultureInfo.InvariantCulture);

    public Bitmap? Preview
    {
        get
        {
            if (!_rendered)
            {
                _rendered = true;
                _preview = _render(this);
            }

            return _preview;
        }
    }

    [ObservableProperty]
    public partial bool IsPicked { get; set; }

    public void Invalidate()
    {
        if (_rendered)
        {
            _rendered = false;
            _preview = null;
            OnPropertyChanged(nameof(Preview));
        }
    }
}

/// <summary>A row of thumbnails, so the list can virtualize a grid.</summary>
public sealed class SpriteRowViewModel(IReadOnlyList<SpriteTileViewModel> tiles)
{
    public IReadOnlyList<SpriteTileViewModel> Tiles { get; } = tiles;
}

/// <summary>Every creature or ground item sprite the client holds, as a filterable grid of thumbnails.</summary>
public sealed partial class SpriteBrowserViewModel : ObservableObject
{
    public const int Columns = 4;

    private readonly Func<SceneHost?> _host;
    private readonly List<SpriteTileViewModel> _shown = [];
    private IReadOnlyList<int> _creatures = [];
    private IReadOnlyList<int> _items = [];

    public SpriteBrowserViewModel(Func<SceneHost?> host) => _host = host;

    /// <summary>Raised with the row a revealed sprite sits in, for the view to scroll to.</summary>
    public event Action<SpriteRowViewModel>? RevealRequested;

    public static IReadOnlyList<SpriteBank> Banks { get; } = Enum.GetValues<SpriteBank>();

    public ObservableCollection<SpriteRowViewModel> Rows { get; } = [];

    [ObservableProperty]
    public partial SpriteBank Bank { get; set; }

    [ObservableProperty]
    public partial string Filter { get; set; } = "";

    [ObservableProperty]
    public partial int Dye { get; set; }

    [ObservableProperty]
    public partial Direction Facing { get; set; } = Direction.Down;

    [ObservableProperty]
    public partial string Summary { get; set; } = "";

    [ObservableProperty]
    public partial SpriteTileViewModel? Picked { get; set; }

    public bool IsCreatures => Bank == SpriteBank.Creatures;

    public SceneObjectKind Kind => KindOf(Bank);

    private IReadOnlyList<int> All => Bank == SpriteBank.Creatures ? _creatures : _items;

    public static SceneObjectKind KindOf(SpriteBank bank) =>
        bank == SpriteBank.Creatures ? SceneObjectKind.Creature : SceneObjectKind.Item;

    public SpritePick PickOf(SpriteTileViewModel tile) => new(tile.Kind, tile.Sprite, (byte)Math.Clamp(Dye, 0, 255), Facing);

    /// <summary>Reads the sprite lists afresh from the client, as opening it does.</summary>
    public void Reload()
    {
        var host = _host();
        _creatures = host?.CreatureSprites() ?? [];
        _items = host?.ItemSprites() ?? [];
        Refresh();
    }

    /// <summary>The sprite after or before one in the bank, skipping numbers with no art.</summary>
    public int? Step(SceneObjectKind kind, int sprite, int direction)
    {
        var all = kind == SceneObjectKind.Creature ? _creatures : _items;
        if (all.Count == 0)
        {
            return null;
        }

        var index = BinarySearch(all, sprite);
        if (index >= 0)
        {
            index += direction;
        }
        else
        {
            index = direction > 0 ? ~index : ~index - 1;
        }

        return index >= 0 && index < all.Count ? all[index] : null;
    }

    /// <summary>Shows one sprite in the grid, clearing a filter that hides it, and picks it.</summary>
    public void Reveal(SceneObjectKind kind, int sprite)
    {
        Bank = kind == SceneObjectKind.Creature ? SpriteBank.Creatures : SpriteBank.Items;
        if (_shown.All(tile => tile.Sprite != sprite))
        {
            Filter = "";
        }

        var index = _shown.FindIndex(tile => tile.Sprite == sprite);
        if (index < 0)
        {
            return;
        }

        Pick(_shown[index]);
        RevealRequested?.Invoke(Rows[index / Columns]);
    }

    public void Pick(SpriteTileViewModel? tile)
    {
        Picked?.IsPicked = false;
        Picked = tile;
        tile?.IsPicked = true;
    }

    partial void OnBankChanged(SpriteBank value)
    {
        OnPropertyChanged(nameof(IsCreatures));
        Refresh();
    }

    partial void OnFilterChanged(string value) => Refresh();

    partial void OnDyeChanged(int value) => InvalidatePreviews();

    partial void OnFacingChanged(Direction value) => InvalidatePreviews();

    private void InvalidatePreviews()
    {
        foreach (var tile in _shown)
        {
            tile.Invalidate();
        }
    }

    private void Refresh()
    {
        var picked = Picked?.Sprite;
        Picked = null;
        _shown.Clear();
        Rows.Clear();

        var all = All;
        var kind = Kind;
        var ranges = ParseFilter(Filter);
        foreach (var sprite in all)
        {
            if (ranges is null || ranges.Any(range => range.Matches(sprite)))
            {
                _shown.Add(new SpriteTileViewModel(kind, sprite, Render));
            }
        }

        for (var start = 0; start < _shown.Count; start += Columns)
        {
            Rows.Add(new SpriteRowViewModel(_shown.GetRange(start, Math.Min(Columns, _shown.Count - start))));
        }

        if (picked is { } previous && _shown.Find(tile => tile.Sprite == previous) is { } again)
        {
            Pick(again);
        }

        var noun = Bank == SpriteBank.Creatures ? "creature" : "item";
        Summary = all.Count == 0
            ? "Open the client on the Settings tab to browse sprites."
            : _shown.Count == all.Count
                ? $"{all.Count:N0} {noun} sprites, {all[0]} to {all[^1]}."
                : $"{_shown.Count:N0} of {all.Count:N0} {noun} sprites.";
    }

    private WriteableBitmap? Render(SpriteTileViewModel tile)
    {
        var staged = new SceneObject
        {
            Kind = tile.Kind,
            Sprite = (ushort)tile.Sprite,
            Color = (byte)Math.Clamp(Dye, 0, 255),
            Direction = Facing,
        };

        return _host() is { } host && host.TryPreview(staged, out var art) ? FrameBitmaps.FromPixmap(art) : null;
    }

    private static int BinarySearch(IReadOnlyList<int> sorted, int value)
    {
        int low = 0, high = sorted.Count - 1;
        while (low <= high)
        {
            var middle = (low + high) >>> 1;
            if (sorted[middle] == value)
            {
                return middle;
            }

            if (sorted[middle] < value)
            {
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        return ~low;
    }

    private readonly record struct FilterRange(int From, int To, string? Prefix)
    {
        public bool Matches(int sprite) => Prefix is { } prefix
            ? sprite.ToString(CultureInfo.InvariantCulture).StartsWith(prefix, StringComparison.Ordinal)
            : sprite >= From && sprite <= To;
    }

    /// <summary>
    /// Null for no filter. Terms are separated by commas or spaces: a number matches every sprite it
    /// begins, and <c>a-b</c> matches the range.
    /// </summary>
    private static List<FilterRange>? ParseFilter(string text)
    {
        var terms = text.Split([',', ' ', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (terms.Length == 0)
        {
            return null;
        }

        var ranges = new List<FilterRange>();
        foreach (var term in terms)
        {
            var dash = term.IndexOf('-', StringComparison.Ordinal);
            if (dash > 0
                && int.TryParse(term.AsSpan(0, dash), NumberStyles.None, CultureInfo.InvariantCulture, out var from)
                && int.TryParse(term.AsSpan(dash + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var to))
            {
                ranges.Add(new FilterRange(Math.Min(from, to), Math.Max(from, to), null));
            }
            else if (term.All(char.IsAsciiDigit))
            {
                ranges.Add(new FilterRange(0, 0, term));
            }
        }

        return ranges;
    }
}
