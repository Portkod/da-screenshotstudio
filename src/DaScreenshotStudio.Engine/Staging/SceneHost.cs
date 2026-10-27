using DaScreenshotStudio.Assets.Imaging;
using DaScreenshotStudio.Assets.Maps;
using DaScreenshotStudio.Assets.Metadata;
using DaScreenshotStudio.Engine.App;
using DaScreenshotStudio.Engine.Core;
using DaScreenshotStudio.Engine.Present;
using DaScreenshotStudio.Engine.Rendering;
using DaScreenshotStudio.Engine.Ui;
using DaScreenshotStudio.Engine.World;

namespace DaScreenshotStudio.Engine.Staging;

public sealed class SceneHost : IDisposable
{
    public const int ScreenWidth = 640;
    public const int ScreenHeight = 480;

    public const byte AlternateArtFlag = 0x80;

    private readonly ClientDirectory _files;
    private readonly ClientAssets _assets;
    private readonly ScreenPane _screen;
    private readonly ScreenTree _tree;
    private bool _disposed;

    public SceneHost(string clientPath, string mapsPath, string? metafilePath = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(clientPath);
        ArgumentException.ThrowIfNullOrEmpty(mapsPath);

        MapsPath = mapsPath;
        _files = new ClientDirectory(clientPath);
        _assets = ClientAssets.Load(
            _files, Directory.Exists(metafilePath) ? MetafileStore.Load(metafilePath) : null);

        try
        {
            Packing = PixelPacking.For(RenderPixelFormat.Rgb565);
            Blender = new PixelBlender(Packing);

            Scene.MapLighting.Packing = Packing;
            Scene.MapLighting.Masks = _assets;
            Scene.ObjectArt = ClientWorldArt.TryLoad(_files, Packing);

            if (_assets.Metafiles.Find(MapLighting.MetafileName) is { } light)
            {
                Scene.MapLighting.SetProfiles(LightProfileTable.Parse(light));
            }

            Ui = UiAssets.TryLoad(_files, Packing, _assets.Metafiles)
                ?? throw new InvalidDataException($"'{clientPath}' has no setoa.dat and national.dat to draw the interface with.");

            _screen = new ScreenPane(ScreenWidth, ScreenHeight, Blender);
            _tree = new ScreenTree(_screen);

            World = new WorldPane(WorldPane.DefaultViewLayout, Blender, Scene);
            _screen.Add(World);

            World.Renderer.HoverCellColor = Ui.Palette[WorldRenderer.HoverCellPalette];
            World.Renderer.DropCellColor = Ui.Palette[WorldRenderer.DropCellPalette];
            World.Renderer.DragTargetColor = Ui.Palette[WorldRenderer.DropCellPalette];
            World.Renderer.ImpactColor = Ui.Palette[WorldRenderer.ImpactPalette];
            World.Renderer.EmptyWorldColor = Ui.Palette[WorldRenderer.EmptyWorldPalette];

            Balloons = new BalloonPane(World.Bounds, Blender, Scene, Ui.Font, Ui.Palette);
            World.AttachNameLabel(new ObjectNamePane(Blender, Ui.Font, Ui.Palette));

            var description = new DescriptionPane(Blender, Ui.Font, Ui.Palette);
            Hud = new GuiBackPane(
                Blender,
                Ui,
                Player,
                new LevelPointFlash(),
                description)
            {
                ViewLayoutChanged = ApplyViewLayout,
            };

            Notices = new GameMessagePane(GameMessagePane.ScreenBounds, Blender, Ui.Font, Ui.Palette);

            Profile = new EquipPane(Blender, Ui, Player, description) { Art = Scene.ObjectArt as ClientWorldArt };
            Hud.Add(Profile);
            ProfilePages = new UserInfoPagePane[UserInfoTabPane.CloseTab - 1];
            for (var page = 0; page < ProfilePages.Length; page++)
            {
                ProfilePages[page] = new UserInfoPagePane(Ui, Blender, page + UserInfoPagePane.FirstPage) { Visible = false };
            }

            for (var page = ProfilePages.Length - 1; page >= 0; page--)
            {
                Hud.Add(ProfilePages[page]);
            }

            ProfileTabs = new UserInfoTabPane(Ui, Blender, forUser: true) { Visible = false };
            Hud.Add(ProfileTabs);

            Options = new OptionPane(Ui, Blender);
            Hud.Add(Options);
            Users = new ShowUsersPane(Ui, Blender);
            Hud.Add(Users);

            Npc = new NpcDialogPane(Blender, Ui, _files) { Art = Scene.ObjectArt as ClientWorldArt };
            Hud.Add(Npc.ScreenFade);
            Hud.Add(Npc);

            Tooltip = new DescPane(Blender, Ui);
            Npc.Shop.TooltipChanged = (text, x, y) => Tooltip.ShowAt(Npc.Shop, x, y, text);

            _screen.Add(Balloons);
            _screen.Add(Hud);
            _screen.Add(Notices);
            _screen.Add(Tooltip);
            ApplyViewLayout(Hud.ViewLayout);
        }
        catch
        {
            _assets.Dispose();
            throw;
        }
    }

    public string MapsPath { get; }

    public PixelPacking Packing { get; }

    public PixelBlender Blender { get; }

    public UiAssets Ui { get; }

    public WorldScene Scene { get; } = new();

    public WorldPane World { get; }

    public BalloonPane Balloons { get; }

    public GuiBackPane Hud { get; }

    public GameMessagePane Notices { get; }

    public OptionPane Options { get; }

    public NpcDialogPane Npc { get; }

    public DescPane Tooltip { get; }

    private BulletinSession? _bulletin;

    public BulletinSession OpenBulletin()
    {
        CloseBulletin();
        _bulletin = new BulletinSession(Blender);
        Hud.Add(_bulletin);
        return _bulletin;
    }

    public void CloseBulletin()
    {
        if (_bulletin is not null)
        {
            Hud.Remove(_bulletin);
            _bulletin = null;
        }
    }

    public ShowUsersPane Users { get; }

    public EquipPane Profile { get; }

    public UserInfoPagePane[] ProfilePages { get; }

    public UserInfoTabPane ProfileTabs { get; }

    public void OpenProfile(int page)
    {
        Hud.Raise(Profile);
        for (var i = ProfilePages.Length - 1; i >= 0; i--)
        {
            Hud.Raise(ProfilePages[i]);
        }

        Hud.Raise(ProfileTabs);

        ProfileTabs.Visible = true;
        ProfileTabs.CurrentTab = page;
        if (page == 0)
        {
            Profile.Show();
        }
        else
        {
            Profile.Hide();
        }

        foreach (var pane in ProfilePages)
        {
            pane.Visible = pane.Page == page;
        }

        Profile.Refresh();
    }

    public void CloseProfile()
    {
        Profile.Hide();
        ProfileTabs.Visible = false;
        foreach (var pane in ProfilePages)
        {
            pane.Visible = false;
        }
    }

    public PlayerModel Player { get; } = new();

    public int MapId { get; private set; }

    public int MapWidth { get; private set; }

    public int MapHeight { get; private set; }

    public byte MapFlags { get; private set; }

    public byte TimeStep { get; private set; }

    public PixelSurface Surface => _screen.Surface;

    public string MapPath(int mapId) => Path.Combine(MapsPath, $"lod{mapId}.map");

    public void LoadMap(int mapId, int width, int height, MapFlags flags = default, byte timeStep = 0)
    {
        var path = MapPath(mapId);
        var bytes = File.ReadAllBytes(path);

        if (width <= 0 || height <= 0)
        {
            if (!MapDimensions.TryInfer(bytes.Length / MapCell.Length, out width, out height))
            {
                throw new InvalidDataException($"'{path}' holds {bytes.Length} bytes, which is not a shape a map can have.");
            }
        }

        var cells = MapFile.Parse(bytes, width, height);
        Scene.Map = WorldMap.Load(_assets.Archives, _assets.Art, cells, ((byte)flags & AlternateArtFlag) != 0, Packing);
        MapId = mapId;
        MapWidth = width;
        MapHeight = height;
        MapFlags = (byte)flags;
        TimeStep = timeStep;

        Scene.MapLighting.SetTime(timeStep);
        Scene.MapLighting.SetMap(mapId, flags);
        Scene.BottomLayer.SetDimensions(width, height);
        Scene.BottomLayer.TryLoadFrom(_assets.Metafiles, _assets.Archives, mapId, Packing);
        World.Invalidate();
    }

    public void UnloadMap()
    {
        if (MapId == 0)
        {
            return;
        }

        Scene.Map = null;
        MapId = 0;
        MapWidth = 0;
        MapHeight = 0;
        World.Invalidate();
    }

    public void CenterOn(int x, int y, int offsetX = 0, int offsetY = 0)
    {
        Scene.Camera.CenterOnCell(x, y);
        Scene.Camera.Move(offsetX + PanX, offsetY + PanY);
        World.Invalidate();
    }

    public int PanX { get; set; }

    public int PanY { get; set; }

    public bool TryPickObject(int screenX, int screenY, out EntityId picked)
    {
        picked = default;
        var bounds = World.Bounds;
        var paneX = screenX - bounds.X;
        var paneY = screenY - bounds.Y;

        if (!Hud.MapRectangle.Contains(screenX, screenY))
        {
            return false;
        }

        if (World.Renderer.TryPickObject(Scene, bounds.Width, bounds.Height, paneX, paneY, out var entity)
            || World.Renderer.TryPickGroundItem(Scene, bounds.Width, bounds.Height, paneX, paneY, out entity))
        {
            picked = entity;
            return true;
        }

        return false;
    }

    public void CellAt(int screenX, int screenY, out int cellX, out int cellY)
    {
        var bounds = World.Bounds;
        WorldRenderer.ViewOffset(
            Scene.Camera, bounds.Width, bounds.Height, World.Renderer.ViewCenterBiasY, out var offsetX, out var offsetY);
        IsometricProjection.CellAt(screenX - bounds.X - offsetX, screenY - bounds.Y - offsetY, out cellX, out cellY);
    }

    public void SetAnimationTicks(long tiles, long statics)
    {
        if (Scene.AnimationTicks != tiles)
        {
            Scene.AnimationTicks = tiles;
            Scene.Map?.ApplyPaletteCycles(tiles);
        }

        Scene.StaticAnimationTicks = statics;
    }

    public void ResetDoors()
    {
        if (Scene.Map is { } map)
        {
            Scene.Statics.Reset(map.Cells);
        }
    }

    public bool SetDoor(int x, int y, bool left, bool open) =>
        Scene.Map is not null
        && Scene.Statics.Apply(x, y, left, open ? StaticStatePairs.Column1State : StaticStatePairs.Column0State);

    public bool TryGetDoor(int x, int y, bool left, out bool openInFile, out bool openNow)
    {
        openInFile = openNow = false;
        if (Scene.Map is not { } map || (uint)x >= (uint)map.Width || (uint)y >= (uint)map.Height)
        {
            return false;
        }

        var cell = map.Cells.GetCell(x, y);
        var fileTile = left ? cell.LeftStaticTile : cell.RightStaticTile;
        var nowTile = left ? Scene.Statics.Left(x, y) : Scene.Statics.Right(x, y);

        if (!StaticStatePairs.TryGetState(fileTile, out var fileState)
            || !StaticStatePairs.TryGetState(nowTile, out var nowState))
        {
            return false;
        }

        openInFile = fileState == StaticStatePairs.Column1State;
        openNow = nowState == StaticStatePairs.Column1State;
        return true;
    }

    public bool TryPickWall(int screenX, int screenY, out int x, out int y, out bool left)
    {
        x = y = 0;
        left = false;
        var bounds = World.Bounds;

        if (!Hud.MapRectangle.Contains(screenX, screenY)
            || !World.Renderer.TryPickStatic(
                Scene, bounds.Width, bounds.Height, screenX - bounds.X, screenY - bounds.Y, out var tile, out left))
        {
            return false;
        }

        x = tile.X;
        y = tile.Y;
        return true;
    }

    public int HighestAbilityIcon(bool spells) =>
        (spells ? Ui.SpellIcons : Ui.SkillIcons)?.HighestIcon() ?? 0;

    public int HighestItemIcon() =>
        Ui.ItemIcons?.HighestIcon() is > IconArt.ItemTag and var highest ? highest - IconArt.ItemTag : 0;

    public bool HasItemIcon(int icon) =>
        Ui.ItemIcons is { } bank && !bank.Frame(IconArt.ItemTag + icon).IsEmpty;

    public static PixelRect ScreenRect(Pane pane)
    {
        ArgumentNullException.ThrowIfNull(pane);

        var rect = pane.Bounds;
        for (var parent = pane.Parent; parent is not null; parent = parent.Parent)
        {
            rect = new PixelRect(rect.X + parent.Bounds.X, rect.Y + parent.Bounds.Y, rect.Width, rect.Height);
        }

        return rect;
    }

    public bool TryGetEquipmentSlot(int slot, out PixelRect cell)
    {
        cell = default;
        if (!Profile.Visible)
        {
            return false;
        }

        var local = Profile.SlotRect((Records.EquipmentSlot)slot);
        if (local.IsEmpty)
        {
            return false;
        }

        cell = new PixelRect(Profile.Bounds.X + local.X, Profile.Bounds.Y + local.Y, local.Width, local.Height);
        return true;
    }

    public bool TryGetBookSlot(bool spells, int slot, out PixelRect cell)
    {
        if (spells && Hud.TrayMode == TrayMode.Combined)
        {
            cell = default;
            if (Hud.GeneralSpellHalf is not { Visible: true } half || !half.TryCellOf(slot, out var local))
            {
                return false;
            }

            var origin = ScreenRect(half);
            cell = new PixelRect(origin.X + local.X, origin.Y + local.Y, local.Width, local.Height);
            return true;
        }

        return TryGetTraySlot(slot, out cell);
    }

    public bool TryGetTraySlot(int slot, out PixelRect cell)
    {
        cell = default;
        if (Hud.Tray is not { Visible: true } tray || !tray.TryCellOf(slot, out var local))
        {
            return false;
        }

        cell = new PixelRect(tray.Bounds.X + local.X, tray.Bounds.Y + local.Y, local.Width, local.Height);
        return true;
    }

    public bool TryGetSpellIcon(int icon, out Pixmap art)
    {
        art = Ui.SpellIcons is { } bank ? bank.Frame(icon).Image : default;
        return !art.IsEmpty;
    }

    public int HighestItemSprite() =>
        Scene.ObjectArt is ClientWorldArt { Items: { } items } ? items.HighestSprite() : 0;

    public IReadOnlyList<int> ItemSprites()
    {
        if (Scene.ObjectArt is not ClientWorldArt { Items: { } items })
        {
            return [];
        }

        var sprites = new List<int>();
        for (int sprite = 1, highest = items.HighestSprite(); sprite <= highest; sprite++)
        {
            if (items.HasArt(sprite))
            {
                sprites.Add(sprite);
            }
        }

        return sprites;
    }

    public IReadOnlyList<int> CreatureSprites() =>
        Scene.ObjectArt is ClientWorldArt { Monsters: { } monsters } ? monsters.Sprites() : [];

    public bool TryGetItemSprite(int sprite, byte dye, out WorldSprite art)
    {
        art = default;
        return Scene.ObjectArt is ClientWorldArt { Items: { } items } && items.TryGet(sprite, dye, out art);
    }

    public bool TryGetObjectAnchor(EntityId entity, out int x, out int y)
    {
        x = y = 0;
        var objects = Scene.Objects;
        if (!objects.TryGetSlot(entity, out var slot))
        {
            return false;
        }

        var bounds = World.Bounds;
        WorldRenderer.ViewOffset(
            Scene.Camera, bounds.Width, bounds.Height, World.Renderer.ViewCenterBiasY, out var offsetX, out var offsetY);
        WorldRenderer.ObjectAnchor(in objects.EntryAt(slot), objects.WalkAt(slot), offsetX, offsetY, out x, out y);
        x += bounds.X;
        y += bounds.Y;
        return true;
    }

    public ReadOnlySpan<MpfStandingSegment> StandingSegments(int sprite) =>
        Scene.ObjectArt is ClientWorldArt { Monsters: { } monsters } ? monsters.StandingSegments(sprite) : default;

    public bool TryGetStandingSegment(int sprite, int index, out MpfStandingSegment segment)
    {
        var segments = StandingSegments(sprite);
        if ((uint)index < (uint)segments.Length)
        {
            segment = segments[index];
            return true;
        }

        segment = default;
        return false;
    }

    public (int X, int Y) CellCenter(int cellX, int cellY)
    {
        var bounds = World.Bounds;
        WorldRenderer.ViewOffset(
            Scene.Camera, bounds.Width, bounds.Height, World.Renderer.ViewCenterBiasY, out var offsetX, out var offsetY);
        IsometricProjection.CellAnchor(cellX, cellY, out var worldX, out var worldY);
        return (bounds.X + offsetX + worldX, bounds.Y + offsetY + worldY);
    }

    public PixelRect ViewRectangle => Hud.MapRectangle;

    public bool TryPreview(SceneObject staged, out Pixmap art)
    {
        ArgumentNullException.ThrowIfNull(staged);
        art = default;

        if (Scene.ObjectArt is not { } objectArt)
        {
            return false;
        }

        WorldSprite sprite;
        var found = staged.Kind switch
        {
            SceneObjectKind.Aisling => objectArt.TryGetHuman(
                new HumanState(SceneApplier.ToAppearance(staged.Look ?? new AislingLook())),
                staged.Direction,
                HumanMotion.StandingMotionIndex,
                0,
                out sprite),
            SceneObjectKind.Creature => objectArt.TryGetCreature(
                staged.Sprite, staged.Color, staged.Direction, 0, CreatureColoring.From([]), out sprite),
            _ => objectArt.TryGetItem(staged.Sprite, staged.Color, out sprite),
        };

        if (!found || sprite.IsEmpty)
        {
            return false;
        }

        art = sprite.Art;
        return true;
    }

    public PixelSurface Render()
    {
        Hud.Refresh();
        World.Invalidate();
        Balloons.Invalidate();
        _tree.ForceRedraw = true;
        _tree.RenderFrame();
        return _screen.Surface;
    }

    public byte[] EncodeBmp() => BmpImage.Encode(Render(), Packing);

    private void ApplyViewLayout(WorldViewLayout layout)
    {
        World?.ApplyViewLayout(in layout);
        Balloons?.ApplyViewLayout(in layout);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Npc.CloseArchive();
        _assets.Dispose();
    }
}
