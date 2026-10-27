namespace DaScreenshotStudio.Engine.Records;

public readonly record struct BulletinEntry(byte Flags, ushort EntryId, byte[] Author, byte Month, byte Day, byte[] Title);
