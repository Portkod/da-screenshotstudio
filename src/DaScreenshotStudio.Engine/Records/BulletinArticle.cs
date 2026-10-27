namespace DaScreenshotStudio.Engine.Records;

public readonly record struct BulletinArticle(
    ushort EntryId, byte[] Author, byte[] Title, byte[] Content, byte Month, byte Day, byte NavigationFlags);
