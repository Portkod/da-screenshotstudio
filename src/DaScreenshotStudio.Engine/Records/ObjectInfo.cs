using DaScreenshotStudio.Engine.Core;

namespace DaScreenshotStudio.Engine.Records;

public sealed record ObjectInfo(
    EntityId Entity,
    IReadOnlyList<ObjectInfoEquipment> Equipment,
    UserState State,
    byte[] Name,
    Nation Nation,
    byte[] Title,
    bool IsGroupOpen,
    byte[] GuildRank,
    byte[] DisplayClass,
    byte[] Guild,
    byte[] Biography,
    byte[] Portrait);
