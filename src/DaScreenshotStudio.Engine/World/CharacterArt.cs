using DaScreenshotStudio.Assets.Archives;
using DaScreenshotStudio.Assets.Characters;
using DaScreenshotStudio.Assets.Imaging;
using DaScreenshotStudio.Assets.Palettes;
using DaScreenshotStudio.Engine.Rendering;
using DaScreenshotStudio.Engine.App;

namespace DaScreenshotStudio.Engine.World;

public sealed class CharacterArt : IDisposable
{
    public static readonly string[] ArchiveNames =
    [
        "misc.dat",
        "khanpal.dat",
        "khanmad.dat",
        "khanmeh.dat",
        "khanmim.dat",
        "khanmns.dat",
        "khanmtz.dat",
        "khanwad.dat",
        "khanweh.dat",
        "khanwim.dat",
        "khanwns.dat",
        "khanwtz.dat",
        "Legend.dat",
    ];

    private readonly IAssetArchive _archive;
    private readonly CharacterPalettes _palettes;
    private readonly PackedPaletteCache _packed;
    private readonly PackedColoringTable? _dyes;
    private readonly HumanSkillMotions _skillMotions;
    private readonly Dictionary<string, EpfImage?> _images = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, EpfImage?>.AlternateLookup<ReadOnlySpan<char>> _lookup;
    private ArchiveSet? _owned;

    public CharacterArt(IAssetArchive archive, CharacterPalettes palettes, in PixelPacking packing)
    {
        ArgumentNullException.ThrowIfNull(archive);
        ArgumentNullException.ThrowIfNull(palettes);

        _archive = archive;
        _palettes = palettes;
        _packed = new PackedPaletteCache(in packing);
        _lookup = _images.GetAlternateLookup<ReadOnlySpan<char>>();
        _dyes = PackedColoringTable.TryLoad(archive, in packing);
        _skillMotions = HumanSkillMotions.Load(archive);
    }

    public CharacterPalettes Palettes => _palettes;

    public HumanSkillMotions SkillMotions => _skillMotions;

    public int LoadedResources { get; private set; }

    public static CharacterArt? TryLoad(ClientDirectory clientDirectory, in PixelPacking packing)
    {
        ArgumentNullException.ThrowIfNull(clientDirectory);

        var archives = new ArchiveSet(clientDirectory.Path, ownsSources: true);
        try
        {
            var opened = 0;
            foreach (var name in ArchiveNames)
            {
                if (clientDirectory.TryOpenArchive(name) is { } archive)
                {
                    archives.Add(archive);
                    opened++;
                }
            }

            if (opened == 0)
            {
                archives.Dispose();
                return null;
            }

            return new CharacterArt(archives, CharacterPalettes.Load(archives), in packing) { _owned = archives };
        }
        catch
        {
            archives.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        _owned?.Dispose();
        _owned = null;
    }

    public EpfImage? Get(scoped ReadOnlySpan<char> name)
    {
        if (_lookup.TryGetValue(name, out var existing))
        {
            return existing;
        }

        EpfImage? parsed = null;
        if (_archive.TryReadMemory(name, out var data))
        {
            try
            {
                parsed = EpfImage.Parse(data);
                LoadedResources++;
            }
            catch (InvalidDataException)
            {
            }
        }

        _images[new string(name)] = parsed;
        return parsed;
    }

    public PackedPalette GetPalette(int category, int spriteId, bool female, int paletteSelector)
    {
        var palette = HumanPartCategory.TryResolvePaletteTable(category, spriteId, out var letter, out var resourceId)
            ? _palettes.ResolveDyedPalette(letter, resourceId, female, paletteSelector)
            : _palettes.ResolveDyedPalette('\0', 0, female, paletteSelector);

        return palette is null ? _packed.Fallback : _packed.Get(palette);
    }

    public PackedPalette GetSkinPalette(int selector)
    {
        var palette = selector >= 0 ? _palettes.GetPalette(CharacterPalettes.BodyLetter, selector) : null;
        return _packed.Get(palette ?? _palettes.Fallback);
    }

    public const int ColoringTableNumber = 0;

    public ReadOnlySpan<ushort> GetDye(int dye) => _dyes is null ? default : _dyes.Get(dye);

    public PackedPalette Pack(Palette? palette) => _packed.Get(palette);
}
