using DaScreenshotStudio.Engine.Core;

namespace DaScreenshotStudio.Engine.Records;

public readonly struct EffectLayer
{
    public const ushort NoOpAnimation = 0x00FF;

    public const ushort MovingEffectBase = 10000;

    public const ushort MovingEffectLimit = 12000;

    public EffectLayer(ushort targetAnimation, ushort frameInterval, MapPoint position)
    {
        Target = default;
        Source = default;
        TargetAnimation = targetAnimation;
        SourceAnimation = 0;
        FrameInterval = frameInterval;
        Position = position;
    }

    public EffectLayer(
        EntityId target, EntityId source, ushort targetAnimation, ushort sourceAnimation, ushort frameInterval)
    {
        Target = target;
        Source = source;
        TargetAnimation = targetAnimation;
        SourceAnimation = sourceAnimation;
        FrameInterval = frameInterval;
        Position = default;
    }

    public EntityId Target { get; }

    public EntityId Source { get; }

    public ushort TargetAnimation { get; }

    public ushort SourceAnimation { get; }

    public ushort FrameInterval { get; }

    public MapPoint Position { get; }

    public bool IsCoordinateForm => Target.Value == 0;
}
