namespace DaScreenshotStudio.Engine.Records;

[Flags]
public enum StatusFields : byte
{
    None = 0x00,

    MailState = 0x01,

    StandaloneState = 0x02,

    Modifiers = 0x04,

    Progression = 0x08,

    Vitals = 0x10,

    CoreStats = 0x20,

    PrivilegeMask = 0xC0,
}
