namespace DaScreenshotStudio.Engine.Records;

public readonly struct WindowChange
{
    public const byte ItemsWindow = 0;

    public const byte SkillsWindow = 1;

    public const byte SpellsWindow = 2;

    public const byte ChatWindow = 3;

    public const byte StatusWindow = 4;

    public WindowChange(byte windowCode) => WindowCode = windowCode;

    public byte WindowCode { get; }
}
