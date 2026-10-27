namespace DaScreenshotStudio.Engine.Records;

public readonly record struct BulletinListing(
    ushort SectionId, byte[] Heading, byte Permissions, IReadOnlyList<BulletinEntry> Entries);
