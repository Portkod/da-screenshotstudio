using DaScreenshotStudio.Engine.Text;
using DaScreenshotStudio.Engine.Core;
using DaScreenshotStudio.Engine.Records;

namespace DaScreenshotStudio.Engine.Ui;

public sealed class SlotRecord
{
    public const int NameLength = 128;

    public const int PromptLength = 128;

    private readonly byte[] _name = new byte[NameLength];
    private readonly byte[] _prompt = new byte[PromptLength];
    private int _nameLength;
    private int _wireNameLength;
    private int _promptLength;

    public bool IsOccupied { get; private set; }

    public ushort Sprite { get; private set; }

    public byte DyeColor { get; private set; }

    public uint Quantity { get; private set; }

    public bool CanStack { get; private set; }

    public uint CurrentDurability { get; private set; }

    public uint MaxDurability { get; private set; }

    public byte ArgumentsType { get; private set; }

    public ReadOnlySpan<byte> Prompt => _prompt.AsSpan(0, _promptLength);

    public byte CastLines { get; private set; }

    public bool IsActionDelayed { get; internal set; }

    public bool IsActionDelayWiping { get; internal set; }

    public int ActionDelayProgress { get; internal set; }

    public uint ActionDelayStart { get; internal set; }

    public uint ActionDelayEnd { get; internal set; }

    public ReadOnlySpan<byte> Name => _name.AsSpan(0, _nameLength);

    public void Set(
        ushort sprite,
        byte dyeColor,
        scoped ReadOnlySpan<byte> name,
        uint quantity = 0,
        bool canStack = false,
        uint currentDurability = 0,
        uint maxDurability = 0,
        byte argumentsType = 0,
        scoped ReadOnlySpan<byte> prompt = default,
        byte castLines = 0)
    {
        Sprite = sprite;
        DyeColor = dyeColor;
        Quantity = quantity;
        CanStack = canStack;
        CurrentDurability = currentDurability;
        MaxDurability = maxDurability;
        ArgumentsType = argumentsType;
        CastLines = castLines;
        IsOccupied = true;

        ClearActionDelay();

        _nameLength = canStack ? StackedName(name, quantity, _name) : Math.Min(name.Length, NameLength);
        if (!canStack)
        {
            name[.._nameLength].CopyTo(_name);
        }

        _wireNameLength = Math.Min(Math.Min(name.Length, NameLength), _nameLength);

        _promptLength = Math.Min(prompt.Length, PromptLength);
        prompt[.._promptLength].CopyTo(_prompt);
    }

    private static int StackedName(scoped ReadOnlySpan<byte> name, uint quantity, Span<byte> destination)
    {
        var text = new TextBuffer(destination);
        text.Append(name);
        text.Append("[ "u8);
        text.Append(quantity);
        text.Append(" ]"u8);
        return text.Length;
    }

    public void Clear()
    {
        IsOccupied = false;
        Sprite = 0;
        _nameLength = 0;
        _wireNameLength = 0;
        _promptLength = 0;
        Quantity = 0;
        CanStack = false;
        CurrentDurability = 0;
        MaxDurability = 0;
        ArgumentsType = 0;
        CastLines = 0;
        ClearActionDelay();
    }

    internal void ClearActionDelay()
    {
        IsActionDelayed = false;
        IsActionDelayWiping = false;
        ActionDelayProgress = 0;
        ActionDelayStart = 0;
        ActionDelayEnd = 0;
    }
}

public sealed class SlotBook
{
    private readonly SlotRecord[] _slots;

    public SlotBook(int capacity, bool keepsCooldownWipe = false)
    {
        _slots = new SlotRecord[Math.Max(1, capacity)];
        for (var i = 0; i < _slots.Length; i++)
        {
            _slots[i] = new SlotRecord();
        }

        KeepsCooldownWipe = keepsCooldownWipe;
    }

    public int Capacity => _slots.Length;

    public bool KeepsCooldownWipe { get; }

    public int Revision { get; private set; }

    public SlotRecord this[int slot] =>
        (uint)(slot - 1) < (uint)_slots.Length ? _slots[slot - 1] : Empty;

    private static SlotRecord Empty { get; } = new();

    public bool IsActionDelayed(int slot) => this[slot].IsActionDelayed;

    public void PoseActionDelay(int slot, bool delayed, int? progress)
    {
        if ((uint)(slot - 1) >= (uint)_slots.Length || !_slots[slot - 1].IsOccupied)
        {
            return;
        }

        var record = _slots[slot - 1];
        record.IsActionDelayed = delayed;
        record.IsActionDelayWiping = delayed && progress is not null;
        record.ActionDelayProgress = Math.Clamp(progress ?? 0, 0, GameTiming.ActionDelayProgressSteps);
        Revision++;
    }

    public void Set(int slot, ushort icon, scoped ReadOnlySpan<byte> name)
    {
        if ((uint)(slot - 1) >= (uint)_slots.Length)
        {
            return;
        }

        _slots[slot - 1].Set(icon, 0, name);
        Revision++;
    }

    public void Set(in AbilitySlot spell)
    {
        if ((uint)(spell.Slot - 1) >= (uint)_slots.Length)
        {
            return;
        }

        _slots[spell.Slot - 1].Set(
            spell.Icon,
            0,
            spell.Name,
            argumentsType: spell.ArgumentsType,
            prompt: spell.Prompt,
            castLines: spell.CastLines);
        Revision++;
    }

    public void Set(in InventoryItem item)
    {
        if ((uint)(item.Slot - 1) >= (uint)_slots.Length)
        {
            return;
        }

        _slots[item.Slot - 1].Set(
            item.Sprite,
            item.DyeColor,
            item.Name,
            item.Quantity,
            item.CanStack,
            item.CurrentDurability,
            item.MaxDurability);

        Revision++;
    }

    public void Set(in EquippedItem item)
    {
        var slot = (int)item.Slot;
        if ((uint)(slot - 1) >= (uint)_slots.Length)
        {
            return;
        }

        _slots[slot - 1].Set(item.Sprite, item.DyeColor, item.Name, 0, false, item.CurrentDurability, item.MaxDurability);
        Revision++;
    }

    public void Remove(int slot)
    {
        if ((uint)(slot - 1) >= (uint)_slots.Length)
        {
            return;
        }

        _slots[slot - 1].Clear();

        Revision++;
    }

    public void Remove(EquipmentSlot slot) => Remove((int)slot);

    public void SetGold(uint amount)
    {
        if (_slots.Length < GoldSlot)
        {
            return;
        }

        Span<byte> name = stackalloc byte[GoldNameBytes];
        var text = new TextBuffer(name);
        text.Append("Gold( "u8);
        text.Append(amount);
        text.Append(" )"u8);

        _slots[GoldSlot - 1].Set(GoldSprite, 0, name[..text.Length], 1);
        Revision++;
    }

    public const int GoldSlot = 0x3C;

    public const ushort GoldSprite = 0x8088;

    private const int GoldNameBytes = 0x20;

    public void Clear()
    {
        foreach (var record in _slots)
        {
            record.Clear();
        }

        Revision++;
    }
}

public sealed class PlayerModel
{
    public const int InventoryCapacity = InventoryItem.MaxSlot;

    public const int EquipmentCapacity = (int)EquipmentSlot.Accessory3;

    public const int AbilityCapacity = AbilitySlot.MaxPaneSlot;

    public SlotBook Inventory { get; } = new(InventoryCapacity);

    public SlotBook Equipment { get; } = new(EquipmentCapacity);

    public SlotBook Spells { get; } = new(AbilityCapacity);

    public SlotBook Skills { get; } = new(AbilityCapacity, keepsCooldownWipe: true);

    public GroupRoster Group { get; } = new();

    public Vitals Vitals { get; private set; }

    public CharacterProgression Progression { get; private set; }

    public CharacterAttributes Attributes { get; private set; }

    public StatusModifiers Modifiers { get; private set; }

    public bool HasStatPoints { get; private set; }

    public byte StatPoints { get; private set; }

    public ushort Weight { get; private set; }

    public ushort MaxWeight { get; private set; }

    public int Revision { get; private set; }

    public void PoseStats(in CharacterAttributes attributes, sbyte armorClass)
    {
        Attributes = attributes;
        Modifiers = Modifiers with { ArmorClass = armorClass };
        Revision++;
    }

    public void PoseProgression(in CharacterProgression progression)
    {
        Progression = progression;
        Revision++;
    }

    public void PoseVitals(in Vitals vitals)
    {
        Vitals = vitals;
        Revision++;
    }

    public void Apply(in StatusUpdate status)
    {
        if (status.Has(StatusFields.CoreStats))
        {
            Attributes = status.Attributes;
            HasStatPoints = status.HasStatPoints;
            StatPoints = status.StatPoints;
            Weight = status.Weight;
            MaxWeight = status.MaxWeight;
        }

        var maxHealth = status.Has(StatusFields.CoreStats) ? status.Vitals.MaxHealth : Vitals.MaxHealth;
        var maxMana = status.Has(StatusFields.CoreStats) ? status.Vitals.MaxMana : Vitals.MaxMana;
        var health = status.Has(StatusFields.Vitals) ? status.Vitals.Health : Vitals.Health;
        var mana = status.Has(StatusFields.Vitals) ? status.Vitals.Mana : Vitals.Mana;
        Vitals = new Vitals(health, maxHealth, mana, maxMana);

        var level = status.Has(StatusFields.CoreStats) ? status.Progression.Level : Progression.Level;
        var abilityLevel = status.Has(StatusFields.CoreStats)
            ? status.Progression.AbilityLevel
            : Progression.AbilityLevel;

        Progression = status.Has(StatusFields.Progression)
            ? new CharacterProgression(
                level,
                abilityLevel,
                status.Progression.TotalExperience,
                status.Progression.ToNextLevel,
                status.Progression.TotalAbility,
                status.Progression.ToNextAbility,
                status.Progression.GamePoints,
                status.Progression.Gold)
            : new CharacterProgression(
                level,
                abilityLevel,
                Progression.TotalExperience,
                Progression.ToNextLevel,
                Progression.TotalAbility,
                Progression.ToNextAbility,
                Progression.GamePoints,
                Progression.Gold);

        if (status.Has(StatusFields.Modifiers))
        {
            Modifiers = status.Modifiers;
        }

        if (status.Has(StatusFields.Progression))
        {
            Inventory.SetGold(Progression.Gold);
        }

        Revision++;
    }

    public void Clear()
    {
        Inventory.Clear();
        Equipment.Clear();
        Spells.Clear();
        Skills.Clear();
        Group.Clear();
        Vitals = default;
        Progression = default;
        Attributes = default;
        Modifiers = default;
        Revision++;
    }
}
