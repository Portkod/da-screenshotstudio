using DaScreenshotStudio.Assets.Archives;

namespace DaScreenshotStudio.Assets.Gui;

public sealed class GuiLayoutCatalog
{
    public const string NamePrefix = "_n";

    public const string Extension = ".txt";

    private readonly GuiLayout[] _layouts;
    private readonly Dictionary<string, GuiLayout> _byName;
    private readonly Dictionary<string, GuiLayout>.AlternateLookup<ReadOnlySpan<char>> _lookup;

    private GuiLayoutCatalog(GuiLayout[] layouts)
    {
        _layouts = layouts;
        _byName = new Dictionary<string, GuiLayout>(layouts.Length, StringComparer.OrdinalIgnoreCase);
        foreach (var layout in layouts)
        {
            _byName[layout.Name] = layout;
        }

        _lookup = _byName.GetAlternateLookup<ReadOnlySpan<char>>();
    }

    public static GuiLayoutCatalog Load(IAssetArchive archive)
    {
        ArgumentNullException.ThrowIfNull(archive);

        var layouts = new List<GuiLayout>();
        var entries = archive.Entries;

        for (var i = 0; i < entries.Count; i++)
        {
            var name = entries[i].Name;
            if (!name.StartsWith(NamePrefix, StringComparison.OrdinalIgnoreCase)
                || !name.EndsWith(Extension, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (archive.TryRead(name, out var data))
            {
                layouts.Add(GuiLayout.Parse(name, data));
            }
        }

        return new GuiLayoutCatalog([.. layouts]);
    }

    public GuiLayout? Find(scoped ReadOnlySpan<char> layoutName) =>
        _lookup.TryGetValue(layoutName, out var layout) ? layout : null;
}
