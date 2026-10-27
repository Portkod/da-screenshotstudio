namespace DaScreenshotStudio.Engine.Records;

public readonly struct NpcMessage
{
    public NpcMessage(byte[] speakerName, byte[] content)
    {
        SpeakerName = speakerName;
        Content = content;
    }

    public byte[] SpeakerName { get; }

    public byte[] Content { get; }

    public ushort SpeakerSprite { get; init; }

    public byte SpeakerColor { get; init; }

    public byte Illustration { get; init; }

    public bool IsScreenMenu { get; init; } = true;

    public bool HasPrevious { get; init; }

    public bool HasNext { get; init; }
}
