namespace DaScreenshotStudio.Engine.World;

public struct WalkState
{
    private bool _creature;
    private byte _index;
    private byte _remaining;
    private byte _frame;
    private short _offsetX;
    private short _offsetY;

    public readonly bool IsWalking => _remaining > 0;

    public readonly int OffsetX => _offsetX;

    public readonly int OffsetY => _offsetY;

    public readonly int MotionFrame => _frame;

    public readonly int Step => _remaining > 0 && _creature ? _index : -1;

    public static WalkState Posed(int offsetX, int offsetY, int motionFrame, int creatureStep = -1) => new()
    {
        _offsetX = (short)offsetX,
        _offsetY = (short)offsetY,
        _frame = (byte)motionFrame,
        _creature = creatureStep >= 0,
        _index = (byte)(creatureStep >= 0 ? creatureStep : 0),
        _remaining = (byte)(creatureStep >= 0 ? 1 : 0),
    };

    public void Cancel()
    {
        _creature = false;
        _index = 0;
        _remaining = 0;
        _frame = 0;
        _offsetX = 0;
        _offsetY = 0;
    }
}
