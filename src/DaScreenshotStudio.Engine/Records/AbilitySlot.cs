namespace DaScreenshotStudio.Engine.Records;

public readonly ref struct AbilitySlot
{
    public const byte MaxPaneSlot = 90;

    public const int MaxNumericValues = 4;

    public AbilitySlot(
        byte slot,
        ushort icon,
        byte argumentsType,
        ReadOnlySpan<byte> name,
        ReadOnlySpan<byte> prompt,
        byte castLines)
    {
        Slot = slot;
        Icon = icon;
        ArgumentsType = argumentsType;
        Name = name;
        Prompt = prompt;
        CastLines = castLines;
    }

    public byte Slot { get; }

    public ushort Icon { get; }

    public byte ArgumentsType { get; }

    public ReadOnlySpan<byte> Name { get; }

    public ReadOnlySpan<byte> Prompt { get; }

    public byte CastLines { get; }
}
