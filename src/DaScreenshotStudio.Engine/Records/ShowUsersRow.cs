using DaScreenshotStudio.Engine.Core;

namespace DaScreenshotStudio.Engine.Records;

public readonly record struct ShowUsersRow(byte ClassAndFlags, byte Color, UserState State, byte[] Title, byte IsMaster, byte[] Name)
{
    public const byte ClassMask = 0x07;

    public const byte GuildFlag = 0x08;

    public CharacterClass CharacterClass => (CharacterClass)(ClassAndFlags & ClassMask);

    public bool SameGuild => (ClassAndFlags & GuildFlag) != 0;
}
