using DaScreenshotStudio.Assets.Characters;
using DaScreenshotStudio.Assets.Maps;
using DaScreenshotStudio.Engine.Core;
using DaScreenshotStudio.Engine.Records;

namespace DaScreenshotStudio.Engine.World;

public enum WorldObjectKind : byte
{
    Unknown = 0,
    Creature,
    Mundane,
    Item,
    Human,

    DisguisedHuman,
}

public readonly struct WorldObjectEntry
{
    public readonly HumanState Appearance;

    public readonly HumanState ActiveAppearance;

    public readonly GroundPaint Paint;

    public WorldObjectEntry(EntityId entity, MapPoint position, Direction direction, ushort sprite, WorldObjectKind kind)
        : this(entity, position, direction, sprite, kind, CreatureType.Monster, default, false)
    {
    }

    public WorldObjectEntry(
        EntityId entity,
        MapPoint position,
        Direction direction,
        ushort sprite,
        WorldObjectKind kind,
        CreatureType creatureType,
        byte dyeColor = 0)
        : this(entity, position, direction, sprite, kind, creatureType, default, false, dyeColor)
    {
    }

    public WorldObjectEntry(
        EntityId entity,
        MapPoint position,
        Direction direction,
        ushort sprite,
        WorldObjectKind kind,
        in HumanState appearance,
        bool isLocalPlayer,
        byte dyeColor = 0)
        : this(
            entity, position, direction, sprite, kind, CreatureType.Monster, in appearance, isLocalPlayer, dyeColor)
    {
    }

    public WorldObjectEntry(
        EntityId entity,
        MapPoint position,
        Direction direction,
        ushort sprite,
        WorldObjectKind kind,
        CreatureType creatureType,
        in HumanState appearance,
        bool isLocalPlayer,
        byte dyeColor = 0)
        : this(entity, position, direction, sprite, kind, creatureType, in appearance, isLocalPlayer, dyeColor, false, in appearance)
    {
    }

    private WorldObjectEntry(
        EntityId entity,
        MapPoint position,
        Direction direction,
        ushort sprite,
        WorldObjectKind kind,
        CreatureType creatureType,
        in HumanState appearance,
        bool isLocalPlayer,
        byte dyeColor,
        bool onSpecialGround,
        in HumanState activeAppearance,
        in GroundPaint paint = default)
    {
        Entity = entity;
        Position = position;
        Direction = direction;
        Sprite = sprite;
        Kind = kind;
        CreatureType = creatureType;
        Appearance = appearance;
        IsLocalPlayer = isLocalPlayer;
        DyeColor = dyeColor;
        OnSpecialGround = onSpecialGround;
        ActiveAppearance = activeAppearance;
        Paint = paint;
    }

    public EntityId Entity { get; }

    public MapPoint Position { get; }

    public Direction Direction { get; }

    public ushort Sprite { get; }

    public WorldObjectKind Kind { get; }

    public CreatureType CreatureType { get; }

    public bool IsLocalPlayer { get; }

    public bool OnSpecialGround { get; }

    public byte DyeColor { get; }

    internal WorldObjectEntry KeepingGroundClone(in HumanState clone) =>
        new(Entity, Position, Direction, Sprite, Kind, CreatureType, in Appearance, IsLocalPlayer, DyeColor,
            true, in clone, in Paint);

    public WorldObjectEntry WithGroundPaint(in GroundPaint paint) =>
        paint == Paint
            ? this
            : new(Entity, Position, Direction, Sprite, Kind, CreatureType, in Appearance, IsLocalPlayer,
                DyeColor, OnSpecialGround, in ActiveAppearance, in paint);

    public WorldObjectEntry WithGroundState(bool onSpecialGround)
    {
        if (onSpecialGround == OnSpecialGround)
        {
            return this;
        }

        var active = onSpecialGround
            ? new HumanState(
                SpecialGroundAppearance.Clone(in Appearance.Appearance), Appearance.IsHiddenPlaceholder)
            : Appearance;

        return new(Entity, Position, Direction, Sprite, Kind, CreatureType, in Appearance, IsLocalPlayer, DyeColor,
            onSpecialGround, in active, in Paint);
    }

    public int DrawLayer => Kind switch
    {
        WorldObjectKind.Item => WorldDrawLayer.GroundItem,
        WorldObjectKind.Creature or WorldObjectKind.Mundane => WorldDrawLayer.Monster,
        _ => WorldDrawLayer.Human,
    };

    public int ScreenOffsetY => Kind == WorldObjectKind.Item ? GroundItemLift : 0;

    public const int GroundItemLift = -2;
}

public static class WorldDrawLayer
{
    public const int GroundItem = 4;

    public const int Human = 7;

    public const int Monster = 10;

    public const int ObjectEffect = 13;

    public const int StaticMapArt = 16;

    public const int StaticEffect = 24;

    public const int GroupAdMarker = 27;

    public const int Count = 32;
}

public sealed class WorldObjectTable
{
    public const int Capacity = 1024;

    public const int NameCapacity = 0x40;

    public const int GroupAdCapacity = 0x100;

    private readonly WorldObjectEntry[] _entries = new WorldObjectEntry[Capacity];
    private readonly WalkState[] _walks = new WalkState[Capacity];
    private readonly MotionState[] _motions = new MotionState[Capacity];
    private readonly CreatureStanding[] _standing = new CreatureStanding[Capacity];
    private readonly byte[] _lightMasks = new byte[Capacity];
    private readonly CreatureColoring[] _coloring = new CreatureColoring[Capacity];
    private readonly uint[] _impactUntil = new uint[Capacity];
    private readonly bool[] _impactLit = new bool[Capacity];
    private int _impactLitCount;
    private readonly SpriteBounds[] _spriteBounds = new SpriteBounds[Capacity];
    private readonly bool[] _occupied = new bool[Capacity];
    private readonly bool[] _inWorldList = new bool[Capacity];
    private readonly MapPoint[] _committed = new MapPoint[Capacity];

    private readonly int[] _arrivals = new int[Capacity];
    private readonly byte[] _names = new byte[Capacity * NameCapacity];
    private readonly byte[] _nameLengths = new byte[Capacity];
    private readonly byte[] _nameStyles = new byte[Capacity];
    private readonly bool[] _nameIsHuman = new bool[Capacity];
    private readonly byte[] _groupAds = new byte[Capacity * GroupAdCapacity];
    private readonly byte[] _groupAdLengths = new byte[Capacity];

    private static readonly WorldObjectEntry Empty;
    private int _groupAdCount;

    private readonly byte[] _nameScratch = new byte[NameCapacity];

    private readonly byte[] _groupAdScratch = new byte[GroupAdCapacity];

    private int _walking;
    private int _arrival;
    private int _reindexes;
    private int _moving;
    private int _humans;
    private int _creatures;

    public int Count { get; private set; }

    public int ReindexCount => _reindexes;

    public int GroupAdCount => _groupAdCount;

    public void SetLocalCrossing(int offsetX, int offsetY, int motionFrame, int creatureStep = -1) =>
        _localCrossing = WalkState.Posed(offsetX, offsetY, motionFrame, creatureStep);

    private WalkState _localCrossing;

    public void Clear()
    {
        _groupAdCount = 0;
        Array.Clear(_occupied);
        Array.Clear(_inWorldList);
        Array.Clear(_motions);
        Array.Clear(_standing);
        Array.Clear(_lightMasks);
        Array.Clear(_coloring);
        Array.Clear(_impactLit);
        _impactLitCount = 0;
        Array.Clear(_spriteBounds);
        Array.Clear(_nameLengths);
        Array.Clear(_groupAdLengths);
        Array.Clear(_arrivals);
        Array.Clear(_committed);
        _arrival = 0;
        _reindexes = 0;
        Count = 0;
        _walking = 0;
        _moving = 0;
        _humans = 0;
        _creatures = 0;
        _localCrossing = default;
    }

    public int ArrivalAt(int slot) => (uint)slot < (uint)Capacity ? _arrivals[slot] : 0;

    public MapPoint CommittedPositionAt(int slot) =>
        (uint)slot < (uint)Capacity && _occupied[slot] ? _committed[slot] : default;

    public bool InWorldObjectList(int slot) =>
        (uint)slot < (uint)Capacity && _occupied[slot] && _inWorldList[slot];

    public bool SetWorldObjectListMembership(EntityId entity, bool member)
    {
        var slot = FindSlot(entity, out var existing);
        if (slot < 0 || !existing)
        {
            return false;
        }

        _inWorldList[slot] = member;
        return true;
    }

    private void Reindex(int slot, in MapPoint cell, bool existing)
    {
        var moved = !existing || _committed[slot] != cell;

        if (existing && moved)
        {
            _reindexes++;
        }

        _committed[slot] = cell;

        if (moved)
        {
            _arrival++;
            _arrivals[slot] = _arrival;
        }
    }

    public bool SetLightMask(EntityId entity, byte selector)
    {
        var slot = FindSlot(entity, out var existing);
        if (slot < 0 || !existing)
        {
            return false;
        }

        _lightMasks[slot] = selector;
        return true;
    }

    public byte LightMaskAt(int slot) => (uint)slot < (uint)Capacity ? _lightMasks[slot] : (byte)0;

    public bool SetCreatureColoring(EntityId entity, in CreatureColoring coloring)
    {
        var slot = FindSlot(entity, out var existing);
        if (slot < 0 || !existing)
        {
            return false;
        }

        _coloring[slot] = coloring;
        return true;
    }

    public CreatureColoring ColoringAt(int slot) =>
        (uint)slot < (uint)Capacity ? _coloring[slot] : CreatureColoring.None;

    public bool SetImpactTint(EntityId entity, uint expiresAt)
    {
        var slot = FindSlot(entity, out var existing);
        if (slot < 0 || !existing)
        {
            return false;
        }

        _impactUntil[slot] = expiresAt;

        if (!_impactLit[slot])
        {
            _impactLit[slot] = true;
            _impactLitCount++;
        }

        return true;
    }

    public bool IsImpactLit(int slot) => (uint)slot < (uint)Capacity && _impactLit[slot];

    public bool SweepImpactTints(uint now)
    {
        if (_impactLitCount == 0)
        {
            return false;
        }

        var cleared = false;

        for (var slot = 0; slot < Capacity && _impactLitCount > 0; slot++)
        {
            if (_impactLit[slot] && App.GameClock.IsDue(now, _impactUntil[slot]))
            {
                ClearImpactTint(slot);
                cleared = true;
            }
        }

        return cleared;
    }

    private void ClearImpactTint(int slot)
    {
        if (_impactLit[slot])
        {
            _impactLit[slot] = false;
            _impactLitCount--;
        }
    }

    public void SetSpriteBounds(int slot, in WorldSprite sprite)
    {
        if ((uint)slot < (uint)Capacity)
        {
            _spriteBounds[slot] = SpriteBounds.FromSprite(in sprite);
        }
    }

    public bool TryGetSpriteBounds(EntityId entity, out SpriteBounds bounds)
    {
        var slot = FindSlot(entity, out var existing);
        return TryGetSpriteBoundsAt(slot < 0 || !existing ? -1 : slot, out bounds);
    }

    public bool TryGetSpriteBoundsAt(int slot, out SpriteBounds bounds)
    {
        if ((uint)slot >= (uint)Capacity || !_occupied[slot] || !_spriteBounds[slot].IsKnown)
        {
            bounds = default;
            return false;
        }

        bounds = _spriteBounds[slot].IsDegenerate ? SpriteBounds.Minimum : _spriteBounds[slot];
        return true;
    }

    public bool SetName(EntityId entity, scoped ReadOnlySpan<byte> name, bool human, byte style)
    {
        var slot = FindSlot(entity, out var existing);
        if (slot < 0 || !existing)
        {
            return false;
        }

        var kept = Math.Min(name.Length, NameCapacity - 1);
        name[..kept].CopyTo(_names.AsSpan(slot * NameCapacity, kept));
        _nameLengths[slot] = (byte)kept;
        _nameStyles[slot] = style;
        _nameIsHuman[slot] = human;
        return true;
    }

    public bool TryGetName(EntityId entity, out ReadOnlySpan<byte> name, out bool human, out byte style)
    {
        var slot = FindSlot(entity, out var existing);
        if (slot < 0 || !existing || _nameLengths[slot] == 0)
        {
            name = default;
            human = false;
            style = 0;
            return false;
        }

        name = _names.AsSpan(slot * NameCapacity, _nameLengths[slot]);
        human = _nameIsHuman[slot];
        style = _nameStyles[slot];
        return true;
    }

    public bool SetGroupAd(EntityId entity, scoped ReadOnlySpan<byte> text)
    {
        var slot = FindSlot(entity, out var existing);
        if (slot < 0 || !existing)
        {
            return false;
        }

        var kept = Math.Min(text.Length, GroupAdCapacity - 1);
        var had = _groupAdLengths[slot] != 0;

        text[..kept].CopyTo(_groupAds.AsSpan(slot * GroupAdCapacity, kept));
        _groupAdLengths[slot] = (byte)kept;

        if (had != (kept != 0))
        {
            _groupAdCount += kept != 0 ? 1 : -1;
        }

        return true;
    }

    public bool Set(in WorldObjectEntry entry) => Set(in entry, redescribed: false, committed: null);

    public bool Set(in WorldObjectEntry entry, in MapPoint committed) =>
        Set(in entry, redescribed: false, committed);

    public bool Replace(in WorldObjectEntry entry) => Set(in entry, redescribed: true, committed: null);

    private bool Set(in WorldObjectEntry entry, bool redescribed, MapPoint? committed)
    {
        var slot = Place(in entry, out var existing, out var turned, committed);
        if (slot < 0)
        {
            return false;
        }

        if (redescribed)
        {
            _coloring[slot] = CreatureColoring.None;

            _inWorldList[slot] = true;
        }

        if (turned)
        {
            if (_motions[slot].IsActive)
            {
                _motions[slot].Cancel();
                _moving--;
            }

            _motions[slot].RestartStanding();
            _standing[slot].Restart();
        }

        if ((turned || redescribed) && _walks[slot].IsWalking)
        {
            _walks[slot].Cancel();
            _walking--;
        }

        if ((!existing || redescribed) && _motions[slot].IsActive)
        {
            _motions[slot].Cancel();
            _moving--;
        }

        if (redescribed)
        {
            _nameLengths[slot] = 0;
        }

        return true;
    }

    public bool SetPose(EntityId entity, in WalkState walk, in MotionState motion, in CreatureStanding standing = default)
    {
        if (!TryGetSlot(entity, out var slot))
        {
            return false;
        }

        _walks[slot] = walk;
        _motions[slot] = motion;
        _standing[slot] = standing;
        return true;
    }

    public MotionState MotionAt(int slot) => (uint)slot < (uint)Capacity ? _motions[slot] : default;

    public CreatureStanding StandingAt(int slot) =>
        (uint)slot < (uint)Capacity ? _standing[slot] : default;

    public bool TryGet(EntityId entity, out WorldObjectEntry entry)
    {
        var slot = FindSlot(entity, out var existing);
        if (slot < 0 || !existing)
        {
            entry = default;
            return false;
        }

        entry = _entries[slot];
        return true;
    }

    public bool TryGetSlot(EntityId entity, out int slot)
    {
        slot = FindSlot(entity, out var existing);
        if (slot < 0 || !existing)
        {
            slot = -1;
            return false;
        }

        return true;
    }

    public ref readonly WorldObjectEntry EntryAt(int slot) =>
        ref (uint)slot < (uint)Capacity && _occupied[slot] ? ref _entries[slot] : ref Empty;

    public bool TryGetGroupAdAt(int slot, out ReadOnlySpan<byte> text)
    {
        if ((uint)slot >= (uint)Capacity || !_occupied[slot] || _groupAdLengths[slot] == 0)
        {
            text = default;
            return false;
        }

        text = _groupAds.AsSpan(slot * GroupAdCapacity, _groupAdLengths[slot]);
        return true;
    }

    public bool IsOccupied(int slot) => (uint)slot < (uint)Capacity && _occupied[slot];

    public SpriteBounds SpriteBoundsAt(int slot)
    {
        if ((uint)slot >= (uint)Capacity)
        {
            return SpriteBounds.Minimum;
        }

        ref readonly var bounds = ref _spriteBounds[slot];
        return bounds.IsKnown && !bounds.IsDegenerate ? bounds : SpriteBounds.Minimum;
    }

    public bool TryGetAt(int slot, out WorldObjectEntry entry)
    {
        if ((uint)slot >= (uint)Capacity || !_occupied[slot])
        {
            entry = default;
            return false;
        }

        entry = _entries[slot];
        return true;
    }

    public WalkState WalkAt(int slot)
    {
        if ((uint)slot >= (uint)Capacity)
        {
            return default;
        }

        return _occupied[slot] && _entries[slot].IsLocalPlayer ? _localCrossing : _walks[slot];
    }

    public bool Remove(EntityId entity)
    {
        var slot = FindSlot(entity, out var existing);
        if (slot < 0 || !existing)
        {
            return false;
        }

        _occupied[slot] = false;
        _nameLengths[slot] = 0;

        if (_groupAdLengths[slot] != 0)
        {
            _groupAdLengths[slot] = 0;
            _groupAdCount--;
        }

        ClearImpactTint(slot);
        Count--;

        if (_entries[slot].Kind == WorldObjectKind.Human)
        {
            _humans--;
        }

        if (HasCreatureSheet(_entries[slot].Kind))
        {
            _creatures--;
        }

        _standing[slot].Restart();

        if (_walks[slot].IsWalking)
        {
            _walks[slot].Cancel();
            _walking--;
        }

        if (_motions[slot].IsActive)
        {
            _motions[slot].Cancel();
            _moving--;
        }

        var probe = Next(slot);
        while (_occupied[probe])
        {
            var moved = _entries[probe];
            var committed = _committed[probe];
            var walk = _walks[probe];
            var motion = _motions[probe];
            var standing = _standing[probe];
            var light = _lightMasks[probe];
            var coloring = _coloring[probe];
            var bounds = _spriteBounds[probe];
            var nameLength = _nameLengths[probe];
            var nameStyle = _nameStyles[probe];
            var nameIsHuman = _nameIsHuman[probe];
            _names.AsSpan(probe * NameCapacity, nameLength).CopyTo(_nameScratch);
            var groupAdLength = _groupAdLengths[probe];
            _groupAds.AsSpan(probe * GroupAdCapacity, groupAdLength).CopyTo(_groupAdScratch);

            _committed[probe] = default;
            _walks[probe] = default;
            _motions[probe] = default;
            _standing[probe] = default;
            _lightMasks[probe] = 0;
            _coloring[probe] = CreatureColoring.None;
            _spriteBounds[probe] = default;
            _nameLengths[probe] = 0;
            _groupAdLengths[probe] = 0;
            _occupied[probe] = false;
            Count--;

            if (moved.Kind == WorldObjectKind.Human)
            {
                _humans--;
            }

            if (HasCreatureSheet(moved.Kind))
            {
                _creatures--;
            }

            var target = Place(in moved, out _);
            if (target >= 0)
            {
                _committed[target] = committed;
                _walks[target] = walk;
                _motions[target] = motion;
                _standing[target] = standing;
                _lightMasks[target] = light;
                _coloring[target] = coloring;
                _spriteBounds[target] = bounds;
                _nameScratch.AsSpan(0, nameLength).CopyTo(_names.AsSpan(target * NameCapacity));
                _nameLengths[target] = nameLength;
                _nameStyles[target] = nameStyle;
                _nameIsHuman[target] = nameIsHuman;
                _groupAdScratch.AsSpan(0, groupAdLength).CopyTo(_groupAds.AsSpan(target * GroupAdCapacity));
                _groupAdLengths[target] = groupAdLength;

                if (walk.IsWalking && target != probe)
                {
                }
            }
            else
            {
                if (groupAdLength != 0)
                {
                    _groupAdCount--;
                }

                if (walk.IsWalking)
                {
                    _walking--;
                }

                if (motion.IsActive)
                {
                    _moving--;
                }
            }

            probe = Next(probe);
        }

        return true;
    }

    private static int Next(int slot) => (slot + 1) & (Capacity - 1);

    private static bool HasCreatureSheet(WorldObjectKind kind) =>
        kind is WorldObjectKind.Creature or WorldObjectKind.Mundane or WorldObjectKind.DisguisedHuman;

    private int Place(in WorldObjectEntry entry, out bool existing) =>
        Place(in entry, out existing, out _, null);

    private int Place(in WorldObjectEntry entry, out bool existing, out bool turned, MapPoint? committed)
    {
        existing = false;
        turned = false;

        if (entry.Entity.IsNone)
        {
            return -1;
        }

        var slot = FindSlot(entry.Entity, out existing);
        if (slot < 0)
        {
            return -1;
        }

        turned = !existing || _entries[slot].Direction != entry.Direction;

        if (!existing)
        {
            _occupied[slot] = true;

            _inWorldList[slot] = true;
            _lightMasks[slot] = 0;
            _coloring[slot] = CreatureColoring.None;
            ClearImpactTint(slot);
            _spriteBounds[slot] = default;

            _nameLengths[slot] = 0;
            Count++;
        }
        else
        {
            if (_entries[slot].Kind == WorldObjectKind.Human)
            {
                _humans--;
            }

            if (HasCreatureSheet(_entries[slot].Kind))
            {
                _creatures--;
            }
        }

        if (entry.Kind == WorldObjectKind.Human)
        {
            _humans++;
        }

        if (HasCreatureSheet(entry.Kind))
        {
            _creatures++;
        }

        Reindex(slot, committed ?? entry.Position, existing);

        _entries[slot] = existing && entry.OnSpecialGround && _entries[slot].OnSpecialGround
            ? entry.KeepingGroundClone(in _entries[slot].ActiveAppearance)
            : entry;
        return slot;
    }

    private int FindSlot(EntityId entity, out bool existing)
    {
        var slot = (int)(entity.Value * 2654435761u >> 22) & (Capacity - 1);

        for (var probe = 0; probe < Capacity; probe++)
        {
            if (!_occupied[slot])
            {
                existing = false;
                return slot;
            }

            if (_entries[slot].Entity == entity)
            {
                existing = true;
                return slot;
            }

            slot = Next(slot);
        }

        existing = false;
        return -1;
    }
}
