using DaScreenshotStudio.Engine.Core;

namespace DaScreenshotStudio.Engine.Records;

public readonly struct StatusUpdate
{
    public StatusFields Fields { get; init; }

    public CharacterProgression Progression { get; init; }

    public Vitals Vitals { get; init; }

    public CharacterAttributes Attributes { get; init; }

    public bool HasStatPoints { get; init; }

    public byte StatPoints { get; init; }

    public ushort MaxWeight { get; init; }

    public ushort Weight { get; init; }

    public StatusModifiers Modifiers { get; init; }

    public bool Has(StatusFields block) => (Fields & block) != 0;
}
