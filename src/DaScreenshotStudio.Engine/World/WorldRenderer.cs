using DaScreenshotStudio.Assets.Maps;
using DaScreenshotStudio.Engine.Rendering;
using DaScreenshotStudio.Engine.Core;
using DaScreenshotStudio.Engine.Records;

namespace DaScreenshotStudio.Engine.World;

public sealed class WorldRenderer
{
    public const int HumanMeterHeight = 0x3D;

    private const int ObjectSource = 0;

    private const int MovingEffectSource = 1;

    private const int EffectSource = 2;

    private const int MarkerSource = 3;

    private const int DyingSource = 4;

    private readonly int[] _visible = new int[WorldObjectTable.Capacity];

    private readonly TileFrameTable _groundFrames = new();
    private readonly TileFrameTable _staticFrames = new();
    private readonly long[] _order =
        new long[
            WorldObjectTable.Capacity + WorldEffects.Capacity + MovingEffects.Capacity + DyingObjects.Capacity];

    private readonly long[] _meterOrder = new long[DamageMeters.Capacity];

    private int _visibleCount;
    private int _orderCount;

    private static ReadOnlySpan<sbyte> MarkerOutline =>
    [
        6, 5, 7, 5, 8, 5, 9, 5, 10, 5, 11, 6, 12, 6,
        13, 7, 14, 7, 15, 8, 16, 8, 17, 9, 17, 10,
    ];

    private const int MarkerSpanY = 0x1A;

    public int GroundTilesDrawn { get; private set; }

    public int StaticTilesDrawn { get; private set; }

    public int BottomImagesDrawn { get; private set; }

    public int OccludersDrawn { get; private set; }

    public int ObjectsCollected { get; private set; }

    public int DyingObjectsDrawn { get; private set; }

    public int ObjectsDrawn { get; private set; }

    public int EffectsDrawn { get; private set; }

    public int MetersDrawn { get; private set; }

    public int MarkersDrawn { get; private set; }

    public int ViewCenterBiasY { get; set; } = WorldViewLayout.CompactCenterBiasY;

    public MapPoint HoverCell { get; private set; } = new(-1, -1);

    public ushort HoverCellColor { get; set; }

    public const byte HoverCellPalette = 0x97;

    public MapPoint DropCell { get; private set; } = new(-1, -1);

    public ushort DropCellColor { get; set; }

    public const byte DropCellPalette = 0x58;

    public ushort EmptyWorldColor { get; set; }

    public const byte EmptyWorldPalette = 0x1F;

    public bool IsBlinded { get; set; }

    public bool ReplaysOtherPlayers { get; set; }

    public bool ReplaysCreatures { get; set; }

    public bool ReplaysGroundItems { get; set; }

    public bool SetHoverCell(int cellX, int cellY)
    {
        if (cellX < 0 || cellY < 0)
        {
            cellX = -1;
            cellY = -1;
        }

        if (HoverCell.X == cellX && HoverCell.Y == cellY)
        {
            return false;
        }

        HoverCell = new MapPoint(cellX, cellY);
        return true;
    }

    public EntityId DragTarget { get; private set; }

    public ushort DragTargetColor { get; set; }

    public ushort ImpactColor { get; set; }

    public const byte ImpactPalette = 0x28;

    public const int ImpactDurationMs = 100;

    public const int TranslucentWeight = 8;

    public bool SetDragTarget(EntityId entity)
    {
        if (DragTarget == entity)
        {
            return false;
        }

        DragTarget = entity;
        return true;
    }

    public bool SetDropCell(int cellX, int cellY)
    {
        if (cellX < 0 || cellY < 0)
        {
            cellX = -1;
            cellY = -1;
        }

        if (DropCell.X == cellX && DropCell.Y == cellY)
        {
            return false;
        }

        DropCell = new MapPoint(cellX, cellY);
        return true;
    }

    public static void ViewOffset(
        WorldCamera camera, int viewWidth, int viewHeight, int centerBiasY, out int offsetX, out int offsetY)
    {
        ArgumentNullException.ThrowIfNull(camera);

        offsetX = (viewWidth / 2) - camera.WorldX;
        offsetY = (viewHeight / 2) + centerBiasY - camera.WorldY;
    }

    public void Draw(Canvas canvas, WorldScene scene)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(scene);

        scene.ObjectArt?.BeginFrame();

        GroundTilesDrawn = 0;
        StaticTilesDrawn = 0;
        BottomImagesDrawn = 0;
        OccludersDrawn = 0;
        ObjectsCollected = 0;
        ObjectsDrawn = 0;
        EffectsDrawn = 0;
        MetersDrawn = 0;
        MarkersDrawn = 0;
        DyingObjectsDrawn = 0;
        _visibleCount = 0;
        _orderCount = 0;

        var map = scene.Map;

        if (map is not null && !scene.Statics.Covers(map.Width, map.Height))
        {
            scene.Statics.Reset(map.Cells);
        }

        ViewOffset(scene.Camera, canvas.Width, canvas.Height, ViewCenterBiasY, out var offsetX, out var offsetY);

        var worldLeft = -offsetX;
        var worldTop = -offsetY;
        var worldRight = worldLeft + canvas.Width;
        var worldBottom = worldTop + canvas.Height;

        if (map is null || IsBlinded)
        {
            canvas.FillRect(canvas.Bounds, EmptyWorldColor, BlendMode.Copy);
        }
        else
        {
            canvas.Clear();

            DrawGroundLayer(canvas, map, scene.AnimationTicks, offsetX, offsetY, worldLeft, worldTop, worldRight, worldBottom);
            DrawBottomLayer(canvas, scene.BottomLayer, offsetX, offsetY);
        }

        if (!IsBlinded)
        {
            DrawCellMarkers(canvas, offsetX, offsetY);
        }

        Collect(scene, map, offsetX, offsetY, canvas.Width, canvas.Height);

        if (!IsBlinded)
        {
            DrawCellOrder(canvas, scene, map, scene.AnimationTicks, offsetX, offsetY, worldLeft, worldTop, worldRight, worldBottom);
        }

        CollectObjectLights(scene, offsetX, offsetY);

        if (!IsBlinded)
        {
            DrawDamageMeters(canvas, scene, offsetX, offsetY);
        }

        ReplaySelf(canvas, scene, offsetX, offsetY);

        scene.Lighting.Apply(canvas, offsetX, offsetY);
    }

    private void DrawCellMarkers(Canvas canvas, int offsetX, int offsetY)
    {
        DrawCellMarker(canvas, HoverCell, HoverCellColor, offsetX, offsetY);
        DrawCellMarker(canvas, DropCell, DropCellColor, offsetX, offsetY);
    }

    private static void DrawCellMarker(Canvas canvas, MapPoint cell, ushort color, int offsetX, int offsetY)
    {
        if (cell.X < 0 || cell.Y < 0 || color == 0)
        {
            return;
        }

        IsometricProjection.Project(cell.X, cell.Y, out var worldX, out var worldY);

        var centerX = worldX + offsetX + IsometricProjection.HalfTileWidth;
        var top = worldY + offsetY;
        var outline = MarkerOutline;

        if (!canvas.TryBeginPixels(out var pixel))
        {
            return;
        }

        for (var i = 0; i < outline.Length; i += 2)
        {
            int across = outline[i];
            int down = outline[i + 1];
            var mirrored = top + MarkerSpanY - down;

            pixel.Set(centerX - across, top + down, color);
            pixel.Set(centerX + across, top + down, color);
            pixel.Set(centerX - across, mirrored, color);
            pixel.Set(centerX + across, mirrored, color);
        }
    }

    public bool TryPickObject(
        WorldScene scene, int viewWidth, int viewHeight, int paneX, int paneY, out EntityId entity)
    {
        ArgumentNullException.ThrowIfNull(scene);

        entity = default;

        var objects = scene.Objects;
        ViewOffset(scene.Camera, viewWidth, viewHeight, ViewCenterBiasY, out var offsetX, out var offsetY);

        for (var i = _orderCount - 1; i >= 0; i--)
        {
            var key = _order[i];
            if (((key >> SourceShift) & SourceMask) != ObjectSource)
            {
                continue;
            }

            var slot = (int)(key & IndexMask);
            if (!objects.TryGetAt(slot, out var candidate)
                || !IsPointerTarget(in candidate)
                || !objects.TryGetSpriteBounds(candidate.Entity, out var bounds))
            {
                continue;
            }

            ObjectAnchor(in candidate, objects.WalkAt(slot), offsetX, offsetY, out var anchorX, out var anchorY);

            var localX = paneX - anchorX;
            var localY = paneY - anchorY;

            if (Contains(in bounds, localX, localY))
            {
                entity = candidate.Entity;
                return true;
            }
        }

        return false;
    }

    public bool TryPickDropTarget(
        WorldScene scene,
        int viewWidth,
        int viewHeight,
        int paneX,
        int paneY,
        bool includeLocalPlayer,
        out EntityId entity)
    {
        ArgumentNullException.ThrowIfNull(scene);

        entity = default;

        var objects = scene.Objects;
        ViewOffset(scene.Camera, viewWidth, viewHeight, ViewCenterBiasY, out var offsetX, out var offsetY);

        for (var i = _orderCount - 1; i >= 0; i--)
        {
            var key = _order[i];
            if (((key >> SourceShift) & SourceMask) != ObjectSource)
            {
                continue;
            }

            var slot = (int)(key & IndexMask);
            if (!objects.TryGetAt(slot, out var candidate)
                || (candidate.IsLocalPlayer && !includeLocalPlayer)
                || !IsLiving(candidate.Kind)
                || !objects.TryGetSpriteBounds(candidate.Entity, out var bounds))
            {
                continue;
            }

            ObjectAnchor(in candidate, objects.WalkAt(slot), offsetX, offsetY, out var anchorX, out var anchorY);

            var localX = paneX - anchorX;
            var localY = paneY - anchorY;

            if (ContainsDropBand(in bounds, localX, localY))
            {
                entity = candidate.Entity;
                return true;
            }
        }

        return false;
    }

    private static bool ContainsDropBand(in SpriteBounds bounds, int localX, int localY) =>
        localX >= -InteractionHalfWidth
        && localX < InteractionHalfWidth
        && localY >= bounds.Bottom - DropBandHeight
        && localY < bounds.Bottom;

    private const int DropBandHeight = 0x3C;

    private static bool IsLiving(WorldObjectKind kind) => kind
        is WorldObjectKind.Human
        or WorldObjectKind.DisguisedHuman
        or WorldObjectKind.Creature
        or WorldObjectKind.Mundane;

    private static bool Contains(in SpriteBounds bounds, int localX, int localY)
    {
        if (localX < -InteractionHalfWidth || localX >= InteractionHalfWidth)
        {
            return false;
        }

        var top = bounds.Top;
        var height = bounds.Bottom - bounds.Top;

        if (height > 0xC8)
        {
            top += 0x46;
        }
        else if (height > 0x30)
        {
            top += 0x18;
        }

        return localY >= top && localY < bounds.Bottom;
    }

    private const int InteractionHalfWidth = 0xE;

    public const int ItemInteractionHalf = 0x10;

    public bool TryPickGroundItem(
        WorldScene scene, int viewWidth, int viewHeight, int paneX, int paneY, out EntityId entity)
    {
        ArgumentNullException.ThrowIfNull(scene);

        entity = default;

        var objects = scene.Objects;
        ViewOffset(scene.Camera, viewWidth, viewHeight, ViewCenterBiasY, out var offsetX, out var offsetY);

        for (var i = _orderCount - 1; i >= 0; i--)
        {
            var key = _order[i];
            if (((key >> SourceShift) & SourceMask) != ObjectSource)
            {
                continue;
            }

            var slot = (int)(key & IndexMask);
            if (!objects.TryGetAt(slot, out var candidate) || candidate.Kind != WorldObjectKind.Item)
            {
                continue;
            }

            ObjectAnchor(in candidate, objects.WalkAt(slot), offsetX, offsetY, out var anchorX, out var anchorY);

            var localX = paneX - anchorX;
            var localY = paneY - anchorY;

            if (localX >= -ItemInteractionHalf && localX < ItemInteractionHalf
                && localY >= -ItemInteractionHalf && localY < ItemInteractionHalf)
            {
                entity = candidate.Entity;
                return true;
            }
        }

        return false;
    }

    public bool TryPickStatic(
        WorldScene scene, int viewWidth, int viewHeight, int paneX, int paneY, out MapPoint tile, out bool leftSide)
    {
        ArgumentNullException.ThrowIfNull(scene);

        tile = default;
        leftSide = false;

        if (scene.Map is not { } map || !scene.Statics.Covers(map.Width, map.Height))
        {
            return false;
        }

        ViewOffset(scene.Camera, viewWidth, viewHeight, ViewCenterBiasY, out var offsetX, out var offsetY);

        IsometricProjection.VisibleCellRange(
            -offsetX,
            -offsetY,
            -offsetX + viewWidth,
            -offsetY + viewHeight + map.Statics.MaximumHeight,
            out var minX, out var minY, out var maxX, out var maxY);

        minX = Math.Max(0, minX);
        minY = Math.Max(0, minY);
        maxX = Math.Min(map.Width - 1, maxX);
        maxY = Math.Min(map.Height - 1, maxY);

        var animations = map.Art.StaticAnimations;
        var statics = scene.Statics;
        var cursor = _orderCount - 1;

        for (var depth = maxX + maxY; depth >= minX + minY; depth--)
        {
            for (var cellX = Math.Min(maxX, depth - minY); cellX >= Math.Max(minX, depth - maxY); cellX--)
            {
                var cellY = depth - cellX;
                var left = statics.Left(cellX, cellY);
                var right = statics.Right(cellX, cellY);
                if (left == MapTileIds.Empty && right == MapTileIds.Empty)
                {
                    continue;
                }

                var key = DrawKey(cellY, cellX, WorldDrawLayer.StaticMapArt, 0, 0);
                while (cursor >= 0 && _order[cursor] > key)
                {
                    if (HitsCollected(scene, _order[cursor], paneX, paneY, offsetX, offsetY))
                    {
                        return false;
                    }

                    cursor--;
                }

                IsometricProjection.Project(cellX, cellY, out var worldX, out var worldY);
                var destinationX = worldX + offsetX;
                var destinationY = worldY + offsetY;
                var ticks = scene.AnimationTicks;

                var hitRight = HitsStatic(
                    map,
                    ResolveStatic(scene, animations, right, ticks),
                    destinationX + IsometricProjection.StaticWidth,
                    destinationY,
                    paneX,
                    paneY);

                if (hitRight
                    || HitsStatic(
                        map, ResolveStatic(scene, animations, left, ticks), destinationX, destinationY, paneX, paneY))
                {
                    tile = new MapPoint(cellX, cellY);
                    leftSide = !hitRight;
                    return true;
                }
            }
        }

        return false;
    }

    private static bool HitsStatic(
        WorldMap map, int staticTileId, int destinationX, int destinationY, int paneX, int paneY)
    {
        if (!map.Statics.TryGet(staticTileId, out var art, out _))
        {
            return false;
        }

        var top = IsometricProjection.StaticTop(destinationY, art.Height);
        return paneX >= destinationX && paneX < destinationX + art.Width
            && paneY >= top && paneY < top + art.Height;
    }

    private static bool HitsCollected(WorldScene scene, long key, int paneX, int paneY, int offsetX, int offsetY)
    {
        if (((key >> SourceShift) & SourceMask) != ObjectSource)
        {
            return false;
        }

        var objects = scene.Objects;
        var slot = (int)(key & IndexMask);
        if (!objects.TryGetAt(slot, out var candidate))
        {
            return false;
        }

        ObjectAnchor(in candidate, objects.WalkAt(slot), offsetX, offsetY, out var anchorX, out var anchorY);

        if (candidate.Kind == WorldObjectKind.Item)
        {
            var itemX = paneX - anchorX;
            var itemY = paneY - anchorY;
            return itemX >= -ItemInteractionHalf && itemX < ItemInteractionHalf
                && itemY >= -ItemInteractionHalf && itemY < ItemInteractionHalf;
        }

        if (!IsPointerTarget(in candidate) || !objects.TryGetSpriteBounds(candidate.Entity, out var bounds))
        {
            return false;
        }

        return Contains(in bounds, paneX - anchorX, paneY - anchorY);
    }

    private static bool IsPointerTarget(in WorldObjectEntry entry) => entry.Kind switch
    {
        WorldObjectKind.Human => !entry.Appearance.HasNoBody,
        WorldObjectKind.Creature or WorldObjectKind.Mundane or WorldObjectKind.DisguisedHuman => true,
        _ => false,
    };

    private void CollectObjectLights(WorldScene scene, int offsetX, int offsetY)
    {
        var lighting = scene.Lighting;
        lighting.ClearObjectLights();

        if (scene.MapLighting.Mode != MapFlags.Darkness || scene.ObjectArt is not { } art)
        {
            return;
        }

        var objects = scene.Objects;

        for (var i = 0; i < _visibleCount; i++)
        {
            var slot = _visible[i];
            var selector = objects.LightMaskAt(slot);

            if (selector == 0)
            {
                continue;
            }

            ref readonly var entry = ref objects.EntryAt(slot);

            if (art.ObjectLightMaskFor(selector) is not { } mask)
            {
                continue;
            }

            if (entry.IsLocalPlayer)
            {
                ObjectAnchor(in entry, objects.WalkAt(slot), offsetX, offsetY, out var localX, out var localY);
                lighting.AddObjectLight(localX, localY, mask);
                continue;
            }

            var committed = objects.CommittedPositionAt(slot);
            IsometricProjection.CellAnchor(committed.X, committed.Y, out var worldX, out var worldY);

            lighting.AddObjectLight(
                worldX + offsetX, worldY + entry.ScreenOffsetY + offsetY, mask);
        }
    }

    private void DrawDamageMeters(Canvas canvas, WorldScene scene, int offsetX, int offsetY)
    {
        var meters = scene.Meters;
        if (meters.Count == 0 || scene.ObjectArt is not { } objectArt || objectArt.DamageMeters is not { } atlas)
        {
            return;
        }

        var objects = scene.Objects;

        var found = 0;
        for (var i = 0; i < meters.Count; i++)
        {
            if (objects.TryGetSlot(meters.TargetAt(i), out var slot))
            {
                _meterOrder[found] = ((long)slot << 32) | (uint)meters.StageAt(i);
                found++;
            }
        }

        for (var i = 1; i < found; i++)
        {
            var key = _meterOrder[i];
            var j = i - 1;
            while (j >= 0 && _meterOrder[j] > key)
            {
                _meterOrder[j + 1] = _meterOrder[j];
                j--;
            }

            _meterOrder[j + 1] = key;
        }

        for (var i = 0; i < found; i++)
        {
            var slot = (int)(_meterOrder[i] >> 32);
            var stage = (int)(uint)_meterOrder[i];

            ref readonly var entry = ref objects.EntryAt(slot);

            ObjectAnchor(in entry, objects.WalkAt(slot), offsetX, offsetY, out var anchorX, out var anchorY);

            var art = atlas.Cell(stage);
            var destinationX = anchorX - (DamageEffectAtlas.CellWidth / 2);
            var destinationY = anchorY - objectArt.MeterHeight(in entry);

            if (destinationX + art.Width <= 0
                || destinationY + art.Height <= 0
                || destinationX >= canvas.Width
                || destinationY >= canvas.Height)
            {
                continue;
            }

            canvas.BlitPixmap(destinationX, destinationY, art, BlendMode.Transparent);
            MetersDrawn++;
        }
    }

    private void DrawGroupAdMarker(Canvas canvas, WorldScene scene, int slot, int offsetX, int offsetY)
    {
        if (scene.GroupAdArt is not { } markers || !scene.Objects.TryGetGroupAdAt(slot, out var text))
        {
            return;
        }

        var box = GroupAdMarkerBox(scene.Objects, slot, markers.Plate, offsetX, offsetY);
        markers.DrawMarker(canvas, in box, text);
        MarkersDrawn++;
    }

    public static PixelRect GroupAdMarkerBox(
        WorldObjectTable objects,
        int slot,
        in PixelRect plate,
        int offsetX,
        int offsetY)
    {
        ArgumentNullException.ThrowIfNull(objects);

        ref readonly var owner = ref objects.EntryAt(slot);
        ObjectAnchor(in owner, objects.WalkAt(slot), offsetX, offsetY, out var anchorX, out var anchorY);

        return new PixelRect(
            anchorX - (plate.Width / 2),
            anchorY - GroupAdMarkerPane.Lift - plate.Height,
            plate.Width,
            plate.Height);
    }

    private void DrawEffect(Canvas canvas, WorldScene scene, int index, int offsetX, int offsetY)
    {
        if (scene.ObjectArt is not { } art)
        {
            return;
        }

        var effect = scene.Effects[index];
        var position = effect.Position;
        var walkX = 0;
        var walkY = 0;

        if (effect.IsAttached)
        {
            if (!scene.Objects.TryGetSlot(effect.Target, out var slot)
                || !scene.Objects.TryGetAt(slot, out var owner))
            {
                return;
            }

            position = owner.Position;
            var walk = scene.Objects.WalkAt(slot);
            walkX = walk.OffsetX;
            walkY = walk.OffsetY;
        }

        var flipX = false;
        var flipY = false;
        if (effect.IsAttached && art.EffectFlipsWithDirection(effect.EffectId))
        {
            WorldEffects.FlipsFor(effect.Direction, out flipX, out flipY);
        }

        if (!art.TryGetEffect(effect.EffectId, effect.Frame, flipX, flipY, out var sprite))
        {
            return;
        }

        IsometricProjection.CellAnchor(position.X, position.Y, out var worldX, out var worldY);

        var destinationX = worldX + walkX + offsetX - sprite.AnchorX;
        var destinationY = worldY + walkY + offsetY - sprite.AnchorY;

        if (destinationX + sprite.Art.Width <= 0
            || destinationY + sprite.Art.Height <= 0
            || destinationX >= canvas.Width
            || destinationY >= canvas.Height)
        {
            return;
        }

        var blend = art.EffectBlend(effect.EffectId);
        if (blend == BlendMode.AlphaPlane)
        {
            canvas.BlitPixmapAlphaPlane(destinationX, destinationY, sprite.Art, sprite.AlphaWeights.Span);
        }
        else
        {
            canvas.BlitPixmap(destinationX, destinationY, sprite.Art, blend);
        }

        EffectsDrawn++;
    }

    private void DrawMovingEffect(Canvas canvas, WorldScene scene, int index, int offsetX, int offsetY)
    {
        if (scene.ObjectArt is not { } art)
        {
            return;
        }

        var effect = scene.MovingEffects[index];
        if (!art.TryGetMotionEffect(effect.MotionIndex, effect.SheetFrame, out var sprite))
        {
            return;
        }

        var destinationX = effect.X + offsetX - sprite.AnchorX;
        var destinationY = effect.Y + offsetY - sprite.AnchorY;

        if (destinationX + sprite.Art.Width <= 0
            || destinationY + sprite.Art.Height <= 0
            || destinationX >= canvas.Width
            || destinationY >= canvas.Height)
        {
            return;
        }

        canvas.BlitPixmap(destinationX, destinationY, sprite.Art, BlendMode.Transparent);
        EffectsDrawn++;
    }

    private void DrawDying(Canvas canvas, WorldScene scene, int index, int offsetX, int offsetY)
    {
        if (scene.ObjectArt is not { } art)
        {
            return;
        }

        ref readonly var corpse = ref scene.Dying[index];
        if (!DyingObjects.TryResolve(art, in corpse, out var sprite) || sprite.Art.IsEmpty)
        {
            return;
        }

        IsometricProjection.CellAnchor(corpse.Cell.X, corpse.Cell.Y, out var worldX, out var worldY);

        var destinationX = worldX + corpse.WalkOffsetX + offsetX - sprite.AnchorX - corpse.AnchorShift;
        var destinationY = worldY + corpse.WalkOffsetY + offsetY - sprite.AnchorY;

        if (destinationX + sprite.Art.Width + corpse.RowShift <= 0
            || destinationY + sprite.Art.Height <= 0
            || destinationX >= canvas.Width
            || destinationY >= canvas.Height)
        {
            return;
        }

        var restore = canvas.State.AlphaWeight;
        canvas.State.AlphaWeight = corpse.Weight;
        canvas.BlitPixmapCombed(destinationX, destinationY, sprite.Art, BlendMode.Alpha, corpse.RowShift);
        canvas.State.AlphaWeight = restore;

        DyingObjectsDrawn++;
    }

    private void DrawBottomLayer(Canvas canvas, MapBtmLayer layer, int offsetX, int offsetY)
    {
        if (!layer.IsActive)
        {
            return;
        }

        MapBottomLayer.CanvasOrigin(layer.MapHeight, out var canvasOriginX, out var canvasOriginY);

        var originX = canvasOriginX - offsetX - IsometricProjection.HalfTileWidth;
        var originY = canvasOriginY - offsetY - IsometricProjection.HalfTileHeight;

        layer.UpdateVisiblePlacements(canvas.Clip.Translate(originX, originY));
        BottomImagesDrawn = layer.Draw(canvas, originX, originY);
    }

    private void DrawGroundLayer(
        Canvas canvas,
        WorldMap map,
        long ticks,
        int offsetX,
        int offsetY,
        int worldLeft,
        int worldTop,
        int worldRight,
        int worldBottom)
    {
        IsometricProjection.VisibleCellRange(
            worldLeft, worldTop, worldRight, worldBottom,
            out var minX, out var minY, out var maxX, out var maxY);

        minX = Math.Max(0, minX);
        minY = Math.Max(0, minY);
        maxX = Math.Min(map.Width - 1, maxX);
        maxY = Math.Min(map.Height - 1, maxY);

        _groundFrames.Update(map.Art.GroundAnimations, ticks);
        var frames = _groundFrames;
        var cells = map.Cells;

        for (var cellY = minY; cellY <= maxY; cellY++)
        {
            var row = cells.GetRowBytes(cellY);

            for (var cellX = minX; cellX <= maxX; cellX++)
            {
                var groundTile = MapFile.ReadGroundTile(row[(cellX * MapCell.Length)..]);
                if (groundTile == MapTileIds.Empty)
                {
                    continue;
                }

                IsometricProjection.Project(cellX, cellY, out var worldX, out var worldY);
                var destinationX = worldX + offsetX;
                var destinationY = worldY + offsetY;

                if (destinationX + IsometricProjection.TileWidth <= 0
                    || destinationY + IsometricProjection.TileHeight <= 0
                    || destinationX >= canvas.Width
                    || destinationY >= canvas.Height)
                {
                    continue;
                }

                var diamond = map.Ground.Get(frames.Resolve(groundTile));
                if (diamond.IsEmpty)
                {
                    continue;
                }

                canvas.DrawGroundDiamond(destinationX, destinationY, diamond);
                GroundTilesDrawn++;
            }
        }
    }

    private void DrawCellOrder(
        Canvas canvas,
        WorldScene scene,
        WorldMap? map,
        long ticks,
        int offsetX,
        int offsetY,
        int worldLeft,
        int worldTop,
        int worldRight,
        int worldBottom)
    {
        if (map is null)
        {
            DrawCollectedBefore(canvas, scene, long.MaxValue, 0, offsetX, offsetY);
            return;
        }

        IsometricProjection.VisibleCellRange(
            worldLeft, worldTop, worldRight, worldBottom + map.Statics.MaximumHeight,
            out var minX, out var minY, out var maxX, out var maxY);

        minX = Math.Max(0, minX);
        minY = Math.Max(0, minY);
        maxX = Math.Min(map.Width - 1, maxX);
        maxY = Math.Min(map.Height - 1, maxY);

        _staticFrames.Update(map.Art.StaticAnimations, ticks);
        var frames = _staticFrames;
        var statics = scene.Statics;
        var cursor = 0;

        for (var depth = minX + minY; depth <= maxX + maxY; depth++)
        {
            for (var cellX = Math.Max(minX, depth - maxY); cellX <= Math.Min(maxX, depth - minY); cellX++)
            {
                var cellY = depth - cellX;
                var left = statics.Left(cellX, cellY);
                var right = statics.Right(cellX, cellY);
                if (left == MapTileIds.Empty && right == MapTileIds.Empty)
                {
                    continue;
                }

                cursor = DrawCollectedBefore(
                    canvas, scene, DrawKey(cellY, cellX, WorldDrawLayer.StaticMapArt, 0, 0), cursor, offsetX, offsetY);

                IsometricProjection.Project(cellX, cellY, out var worldX, out var worldY);
                var destinationX = worldX + offsetX;
                var destinationY = worldY + offsetY;

                DrawStatic(canvas, map, ResolveStatic(scene, frames, left), destinationX, destinationY);
                DrawStatic(
                    canvas,
                    map,
                    ResolveStatic(scene, frames, right),
                    destinationX + IsometricProjection.StaticWidth,
                    destinationY);
            }
        }

        DrawCollectedBefore(canvas, scene, long.MaxValue, cursor, offsetX, offsetY);
    }

    private int DrawCollectedBefore(Canvas canvas, WorldScene scene, long limit, int cursor, int offsetX, int offsetY)
    {
        while (cursor < _orderCount && _order[cursor] < limit)
        {
            DrawCollected(canvas, scene, _order[cursor], offsetX, offsetY);
            cursor++;
        }

        return cursor;
    }

    private void DrawCollected(Canvas canvas, WorldScene scene, long key, int offsetX, int offsetY)
    {
        var index = (int)(key & IndexMask);

        switch ((int)((key >> SourceShift) & SourceMask))
        {
            case MovingEffectSource:
                DrawMovingEffect(canvas, scene, index, offsetX, offsetY);
                return;

            case EffectSource:
                DrawEffect(canvas, scene, index, offsetX, offsetY);
                return;

            case MarkerSource:
                DrawGroupAdMarker(canvas, scene, index, offsetX, offsetY);
                return;

            case DyingSource:
                DrawDying(canvas, scene, index, offsetX, offsetY);
                return;

            default:
                if (scene.ObjectArt is not { } art || !scene.Objects.IsOccupied(index))
                {
                    return;
                }

                var objects = scene.Objects;
                DrawObject(
                    canvas,
                    art,
                    objects,
                    index,
                    in objects.EntryAt(index),
                    objects.WalkAt(index),
                    objects.MotionAt(index),
                    offsetX,
                    offsetY,
                    BlendMode.Transparent);
                return;
        }
    }

    private static int ResolveStatic(WorldScene scene, TileFrameTable frames, int staticTileId)
    {
        if (scene.AnimatesStaticObjects
            && StaticTileAnimations.TryResolve(staticTileId, scene.StaticAnimationTicks, out var animated))
        {
            staticTileId = animated;
        }

        return frames.Resolve(staticTileId);
    }

    private static int ResolveStatic(WorldScene scene, TileAnimationTable animations, int staticTileId, long ticks)
    {
        if (scene.AnimatesStaticObjects
            && StaticTileAnimations.TryResolve(staticTileId, scene.StaticAnimationTicks, out var animated))
        {
            staticTileId = animated;
        }

        return animations.ResolveFrame(staticTileId, ticks);
    }

    private void DrawStatic(Canvas canvas, WorldMap map, int staticTileId, int destinationX, int destinationY)
    {
        if (!map.Statics.TryGet(staticTileId, out var art, out var blend))
        {
            return;
        }

        var top = IsometricProjection.StaticTop(destinationY, art.Height);

        if (destinationX + art.Width <= 0
            || destinationX >= canvas.Width
            || top + art.Height <= 0
            || top >= canvas.Height)
        {
            return;
        }

        canvas.BlitPixmap(destinationX, top, art, blend);

        StaticTilesDrawn++;
        if (blend == BlendMode.Screen)
        {
            OccludersDrawn++;
        }
    }

    private const int DepthShift = 47;

    private const int CellXShift = 31;

    private const int LayerShift = 25;

    private const int SourceShift = 22;

    private const long SourceMask = 0x7;

    private const long IndexMask = 0x3FFFFF;

    private static bool IsAfter(long left, long right, WorldObjectTable objects)
    {
        var group = left & ~IndexMask;
        if (group != (right & ~IndexMask))
        {
            return group > (right & ~IndexMask);
        }

        var leftIndex = (int)(left & IndexMask);
        var rightIndex = (int)(right & IndexMask);

        var source = (left >> SourceShift) & SourceMask;
        if (source != ObjectSource && source != MarkerSource)
        {
            return leftIndex > rightIndex;
        }

        var leftArrival = objects.ArrivalAt(leftIndex);
        var rightArrival = objects.ArrivalAt(rightIndex);

        return leftArrival != rightArrival ? leftArrival < rightArrival : leftIndex > rightIndex;
    }

    private static long DrawKey(int cellY, int cellX, int layer, int source, int index) =>
        ((long)(cellX + cellY) << DepthShift)
        | ((long)cellX << CellXShift)
        | ((long)layer << LayerShift)
        | ((long)source << SourceShift)
        | (uint)index;

    private void Collect(WorldScene scene, WorldMap? map, int offsetX, int offsetY, int width, int height)
    {
        var objects = scene.Objects;
        _visibleCount = 0;
        _orderCount = 0;

        var plate = scene.GroupAdArt is { Enabled: true } markers && objects.GroupAdCount > 0
            ? markers.Plate
            : PixelRect.Empty;

        for (var slot = 0; slot < WorldObjectTable.Capacity; slot++)
        {
            if (!objects.IsOccupied(slot))
            {
                continue;
            }

            ref readonly var entry = ref objects.EntryAt(slot);

            var cell = objects.CommittedPositionAt(slot);
            if (IsOffMap(map, cell.X, cell.Y))
            {
                continue;
            }

            if (!plate.IsEmpty && objects.TryGetGroupAdAt(slot, out _))
            {
                var box = GroupAdMarkerBox(objects, slot, in plate, offsetX, offsetY);
                if (box.Right > 0 && box.Bottom > 0 && box.X < width && box.Y < height)
                {
                    _order[_orderCount] = DrawKey(
                        cell.Y, cell.X, WorldDrawLayer.GroupAdMarker, MarkerSource, slot);
                    _orderCount++;
                }
            }

            if (!ReachesView(objects, slot, in entry, offsetX, offsetY, width, height))
            {
                continue;
            }

            _visible[_visibleCount] = slot;
            _visibleCount++;
            _order[_orderCount] = DrawKey(cell.Y, cell.X, entry.DrawLayer, ObjectSource, slot);
            _orderCount++;
        }

        var moving = scene.MovingEffects;
        for (var i = 0; i < moving.Count; i++)
        {
            var effect = moving[i];
            if (!effect.IsVisible)
            {
                continue;
            }

            IsometricProjection.Unproject(
                effect.SourceX - IsometricProjection.HalfTileWidth,
                effect.SourceY - IsometricProjection.HalfTileHeight,
                out var cellX,
                out var cellY);

            if (IsOffMap(map, cellX, cellY))
            {
                continue;
            }

            _order[_orderCount] = DrawKey(cellY, cellX, MovingEffects.DrawLayer, MovingEffectSource, i);
            _orderCount++;
        }

        var dying = scene.Dying;
        for (var i = 0; i < dying.Count; i++)
        {
            ref readonly var corpse = ref dying[i];
            if (IsOffMap(map, corpse.Cell.X, corpse.Cell.Y))
            {
                continue;
            }

            _order[_orderCount] = DrawKey(corpse.Cell.Y, corpse.Cell.X, DyingObjects.DrawLayer, DyingSource, i);
            _orderCount++;
        }

        var effects = scene.Effects;
        for (var i = 0; i < effects.Count; i++)
        {
            var effect = effects[i];
            var position = effect.Position;

            if (effect.IsAttached)
            {
                if (!objects.TryGetSlot(effect.Target, out var owner))
                {
                    continue;
                }

                position = objects.CommittedPositionAt(owner);
            }

            if (IsOffMap(map, position.X, position.Y))
            {
                continue;
            }

            _order[_orderCount] = DrawKey(position.Y, position.X, effect.DrawLayer, EffectSource, i);
            _orderCount++;
        }

        for (var i = 1; i < _orderCount; i++)
        {
            var key = _order[i];
            var j = i - 1;

            while (j >= 0 && IsAfter(_order[j], key, objects))
            {
                _order[j + 1] = _order[j];
                j--;
            }

            _order[j + 1] = key;
        }

        ObjectsCollected = _visibleCount;
    }

    private static bool IsOffMap(WorldMap? map, int cellX, int cellY) =>
        map is not null && ((uint)cellX >= (uint)map.Width || (uint)cellY >= (uint)map.Height);

    internal static void ObjectAnchor(
        in WorldObjectEntry entry, WalkState walk, int offsetX, int offsetY, out int x, out int y)
    {
        IsometricProjection.CellAnchor(entry.Position.X, entry.Position.Y, out var worldX, out var worldY);

        x = worldX + walk.OffsetX + offsetX;
        y = worldY + walk.OffsetY + entry.ScreenOffsetY + offsetY;
    }

    private static bool ReachesView(
        WorldObjectTable objects,
        int slot,
        in WorldObjectEntry entry,
        int offsetX,
        int offsetY,
        int width,
        int height)
    {
        var bounds = objects.SpriteBoundsAt(slot);
        ObjectAnchor(in entry, objects.WalkAt(slot), offsetX, offsetY, out var x, out var y);

        return x + bounds.Right > 0 && y + bounds.Bottom > 0 && x + bounds.Left < width && y + bounds.Top < height;
    }

    private void ReplaySelf(Canvas canvas, WorldScene scene, int offsetX, int offsetY)
    {
        if (scene.ObjectArt is not { } art)
        {
            return;
        }

        var objects = scene.Objects;

        for (var i = 0; i < _orderCount; i++)
        {
            var key = _order[i];
            if (((key >> SourceShift) & SourceMask) != ObjectSource)
            {
                continue;
            }

            var slot = (int)(key & IndexMask);

            ref readonly var entry = ref objects.EntryAt(slot);
            if (!entry.IsLocalPlayer && !Replays(in entry))
            {
                continue;
            }

            var restore = canvas.State.AlphaWeight;
            canvas.State.AlphaWeight = PixelBlender.MaxAlphaWeight / 2;
            DrawObject(
                canvas,
                art,
                objects,
                slot,
                in entry,
                objects.WalkAt(slot),
                objects.MotionAt(slot),
                offsetX,
                offsetY,
                IsBlinded ? BlendMode.Transparent : BlendMode.Alpha);
            canvas.State.AlphaWeight = restore;
        }
    }

    private bool Replays(in WorldObjectEntry entry) => entry.Kind switch
    {
        WorldObjectKind.Human or WorldObjectKind.DisguisedHuman => ReplaysOtherPlayers,
        WorldObjectKind.Creature or WorldObjectKind.Mundane => ReplaysCreatures,
        WorldObjectKind.Item => ReplaysGroundItems,
        _ => false,
    };

    private void DrawObject(
        Canvas canvas,
        IWorldObjectArt art,
        WorldObjectTable objects,
        int slot,
        in WorldObjectEntry entry,
        WalkState walk,
        MotionState motion,
        int offsetX,
        int offsetY,
        BlendMode mode)
    {
        if (!TryResolve(
            art, in entry, walk, motion, objects.StandingAt(slot), objects.ColoringAt(slot), out var sprite))
        {
            objects.SetSpriteBounds(slot, default);
            return;
        }

        objects.SetSpriteBounds(slot, in sprite);

        ObjectAnchor(in entry, walk, offsetX, offsetY, out var anchorX, out var anchorY);

        var destinationX = anchorX - sprite.AnchorX;
        var destinationY = anchorY - sprite.AnchorY;

        if (destinationX + sprite.Art.Width <= 0
            || destinationY + sprite.Art.Height <= 0
            || destinationX >= canvas.Width
            || destinationY >= canvas.Height)
        {
            return;
        }

        var translucent = DrawsTranslucent(in entry);
        var restoreWeight = 0;

        if (translucent)
        {
            restoreWeight = canvas.State.AlphaWeight;
            canvas.State.AlphaWeight = TranslucentWeight;
            mode = BlendMode.Alpha;
        }

        if (DragTarget != default && entry.Entity == DragTarget)
        {
            canvas.BlitPixmapAveragedWith(destinationX, destinationY, sprite.Art, mode, DragTargetColor);
        }
        else if (objects.IsImpactLit(slot))
        {
            canvas.BlitPixmapAveragedWith(destinationX, destinationY, sprite.Art, mode, ImpactColor);
        }
        else if (entry.Paint.Height > 0)
        {
            entry.Paint.GetTint(out var red, out var green, out var blue);
            canvas.BlitPixmapPainted(
                destinationX,
                destinationY,
                sprite.Art,
                mode,
                Math.Max(0, sprite.AnchorY - entry.Paint.Height),
                canvas.Blender.Packing.Pack(red, green, blue),
                entry.Paint.TintWeight);
        }
        else
        {
            canvas.BlitPixmap(destinationX, destinationY, sprite.Art, mode);
        }

        if (translucent)
        {
            canvas.State.AlphaWeight = restoreWeight;
        }

        ObjectsDrawn++;
    }

    private static bool DrawsTranslucent(in WorldObjectEntry entry) =>
        entry.Appearance.Appearance.IsTranslucent || entry.Appearance.Appearance.Body.IsNonblocking;

    private static bool TryResolve(
        IWorldObjectArt art,
        in WorldObjectEntry entry,
        WalkState walk,
        MotionState motion,
        CreatureStanding standing,
        in CreatureColoring coloring,
        out WorldSprite sprite)
    {
        var motionFrame = walk.MotionFrame;

        switch (entry.Kind)
        {
            case WorldObjectKind.Human:
                if (motion.IsActive
                    && HumanMotion.TryGetBodyMotion(
                        motion.Animation, in entry.ActiveAppearance, art.SkillMotions, out var body))
                {
                    return body.Variant == '\0'
                        ? art.TryGetHuman(
                            in entry.ActiveAppearance,
                            entry.Direction,
                            body.MotionIndex,
                            body.FrameAt(motion.Frame),
                            body.FramesPerView,
                            -1,
                            out sprite)
                        : art.TryGetHumanVariant(
                            in entry.ActiveAppearance,
                            entry.Direction,
                            body.FrameAt(motion.Frame),
                            body.FramesPerView,
                            body.Variant,
                            out sprite);
                }

                if (motion.IsActive
                    && HumanEmotes.TryGetMotion(motion.Animation, entry.Direction, out var emote))
                {
                    return art.TryGetHuman(
                        in entry.ActiveAppearance,
                        entry.Direction,
                        HumanMotion.StandingMotionIndex,
                        0,
                        HumanComposite.StandingFramesPerView,
                        -1,
                        emote.FrameAt(motion.Frame),
                        out sprite);
                }

                var idleStep = motionFrame == 0 && !motion.IsActive ? motion.StandingStep : -1;
                return art.TryGetHuman(
                    in entry.ActiveAppearance,
                    entry.Direction,
                    HumanMotion.StandingMotionIndex,
                    motionFrame,
                    HumanComposite.StandingFramesPerView,
                    idleStep,
                    out sprite);

            case WorldObjectKind.Creature:
            case WorldObjectKind.Mundane:
            case WorldObjectKind.DisguisedHuman:
                var creature = Untag(entry.Sprite, TaggedSprite.CreatureTag);
                var dye = entry.DyeColor;

                if (motion.IsActive
                    && art.TryGetCreatureAttack(
                        creature,
                        dye,
                        entry.Direction,
                        CreatureMotion.AttackRangeFor(motion.Animation),
                        motion.Frame,
                        in coloring,
                        out sprite))
                {
                    return true;
                }

                if (walk.Step >= 0)
                {
                    return art.TryGetCreature(creature, dye, entry.Direction, walk.Step, in coloring, out sprite);
                }

                if (standing.HasSegment)
                {
                    return art.TryGetCreatureStanding(
                        creature, dye, entry.Direction, standing.Segment, standing.Step, in coloring, out sprite);
                }

                return art.TryGetCreature(creature, dye, entry.Direction, 0, in coloring, out sprite);

            case WorldObjectKind.Item:
                return art.TryGetItem(Untag(entry.Sprite, TaggedSprite.ItemTag), entry.DyeColor, out sprite);

            default:
                sprite = default;
                return false;
        }
    }

    private static int Untag(ushort sprite, ushort tag) => sprite >= tag ? sprite - tag : sprite;
}
