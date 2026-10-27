using DaScreenshotStudio.Engine.Rendering;
using DaScreenshotStudio.Engine.Text;
using DaScreenshotStudio.Engine.Ui;
using DaScreenshotStudio.Engine.Core;

namespace DaScreenshotStudio.Engine.World;

public sealed class ObjectNamePane : Pane
{
    public const int CellWidth = 6;

    public const int LineHeight = 0xC;

    public const int OutlinePadding = 2;

    public const int OutlineDrop = 1;

    public const int MaxWidth = ((WorldObjectTable.NameCapacity - 1) * CellWidth) + OutlinePadding;

    public const int BoxHeight = LineHeight + OutlineDrop;

    public const byte OutlinePalette = TextPalette.ShadowIndex;

    public const byte MundanePalette = 0x58;

    public const byte PlayerPalette = TextPalette.InterfaceIndex;

    public const byte Style1Palette = 0x28;

    public const byte Style2Palette = 0x80;

    public const int HumanHeight = 0x49;

    public const int CreatureLift = 0xE;

    private readonly TextBlock _fill;
    private readonly TextBlock _outline;
    private EntityId _named;
    private EntityId _composed;
    private int _height;
    private bool _hasLabel;

    public ObjectNamePane(PixelBlender blender, GameFont font, TextPalette palette)
        : base(new PixelRect(0, 0, MaxWidth, BoxHeight), blender)
    {
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(palette);

        Visible = false;

        TargetBlend = BlendMode.Transparent;

        _fill = new TextBlock(font, palette, MaxWidth, 1) { Wraps = false };
        _outline = new TextBlock(font, palette, MaxWidth, 1)
        {
            Wraps = false,
            DefaultPaletteIndex = OutlinePalette,
        };
    }

    public void SetNamed(EntityId entity)
    {
        if (_named == entity)
        {
            return;
        }

        _named = entity;
        Visible = false;
    }

    public static byte PaletteFor(bool human, byte style) => style switch
    {
        1 => Style1Palette,
        _ when !human => MundanePalette,
        2 => Style2Palette,
        _ => PlayerPalette,
    };

    public void Place(WorldScene scene, int viewWidth, int viewHeight, int centerBiasY)
    {
        ArgumentNullException.ThrowIfNull(scene);

        if (_named.IsNone || !scene.Objects.TryGetSlot(_named, out var slot))
        {
            Visible = false;
            return;
        }

        ref readonly var entry = ref scene.Objects.EntryAt(slot);

        if (_composed != _named)
        {
            Compose(scene, _named, in entry);
        }

        if (!_hasLabel)
        {
            Visible = false;
            return;
        }

        WorldRenderer.ViewOffset(scene.Camera, viewWidth, viewHeight, centerBiasY, out var offsetX, out var offsetY);

        WorldRenderer.ObjectAnchor(
            in entry, scene.Objects.WalkAt(slot), offsetX, offsetY, out var anchorX, out var anchorY);

        var width = (_fill.Value.Length * CellWidth) + OutlinePadding;

        SetBounds(new PixelRect(
            anchorX - (width / 2),
            anchorY - _height,
            width,
            BoxHeight));

        Visible = true;
    }

    private void Compose(WorldScene scene, EntityId entity, in WorldObjectEntry entry)
    {
        _composed = entity;
        _hasLabel = scene.Objects.TryGetName(entity, out var name, out var human, out var style);

        if (!_hasLabel)
        {
            return;
        }

        _height = scene.ObjectArt?.NameLabelHeight(in entry) ?? HumanHeight;
        _fill.DefaultPaletteIndex = PaletteFor(human, style);
        _fill.SetText(name);
        _outline.SetText(name);
        Invalidate();
    }

    protected override void OnDrawContent(Canvas canvas)
    {
        canvas.Clear();

        if (!_hasLabel)
        {
            return;
        }

        _outline.Draw(canvas, OutlinePadding, OutlineDrop);
        _outline.Draw(canvas, 0, OutlineDrop);
        _fill.Draw(canvas, OutlineDrop, 0);
    }
}
