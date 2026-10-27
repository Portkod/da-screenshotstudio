namespace DaScreenshotStudio.Engine.Ui;

public enum UiEventType
{
    PointerMove = 0x00,
    LeftDown = 0x01,
    LeftDoubleClick = 0x02,
    LeftUp = 0x03,
    RightDown = 0x04,
    RightDoubleClick = 0x05,
    RightUp = 0x06,
    Wheel = 0x07,
    KeyDown = 0x08,
    KeyUp = 0x09,
    ImeComposition = 0x0A,
    Character = 0x0B,
    TranslatedKey = 0x0C,
}

[Flags]
public enum InputModifiers
{
    None = 0x00,
    Alt = 0x01,
    Control = 0x02,
    Shift = 0x04,
    LeftHeld = 0x10,
    RightHeld = 0x20,
}

public readonly struct PointerEvent(
    UiEventType type, int x, int y, InputModifiers modifiers, int wheel = 0, uint time = 0)
{
    public UiEventType Type { get; } = type;

    public int X { get; } = x;

    public int Y { get; } = y;

    public InputModifiers Modifiers { get; } = modifiers;

    public int Wheel { get; } = wheel;

    public uint Time { get; } = time;

    public PointerEvent Translate(int deltaX, int deltaY) =>
        new(Type, X + deltaX, Y + deltaY, Modifiers, Wheel, Time);
}

public readonly struct KeyEvent(UiEventType type, int key, byte character, InputModifiers modifiers)
{
    public UiEventType Type { get; } = type;

    public int Key { get; } = key;

    public byte Character { get; } = character;

    public InputModifiers Modifiers { get; } = modifiers;
}

public static class UiKeys
{
    public const int Backspace = 0x08;
    public const int Tab = 0x09;
    public const int Enter = 0x0D;

    public const int Shift = 0x10;

    public const int Control = 0x11;

    public const int Alt = 0x12;

    public const int CapsLock = 0x14;

    public const int NumLock = 0x90;

    public const int ScrollLock = 0x91;

    public const int Escape = 0x1B;
    public const int Space = 0x20;
    public const int PageUp = 0x21;
    public const int PageDown = 0x22;
    public const int End = 0x23;
    public const int Home = 0x24;
    public const int Left = 0x25;
    public const int Up = 0x26;
    public const int Right = 0x27;
    public const int Down = 0x28;
    public const int Delete = 0x2E;

    public const int Minus = 0xBD;

    public const int Grave = 0xC0;

    public const int Slash = 0xBF;

    public const int Equal = 0xBB;

    public const int F1 = 0x70;

    public const int F2 = 0x71;

    public const int F3 = 0x72;

    public const int F4 = 0x73;

    public const int F5 = 0x74;

    public const int F7 = 0x76;

    public const int F8 = 0x77;

    public const int F9 = 0x78;

    public const int F10 = 0x79;

    public const int F12 = 0x7B;

    public const int Pickup = 'B';
}
