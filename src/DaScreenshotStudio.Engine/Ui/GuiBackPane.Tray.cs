namespace DaScreenshotStudio.Engine.Ui;

public sealed partial class GuiBackPane
{
    public bool TryOfferSlotDrop(DragRecordKind kind, int sourceSlot, int screenX, int screenY, bool release)
    {
        var localX = screenX - Bounds.X;
        var localY = screenY - Bounds.Y;

        if (_spellHalf is { Visible: true } half
            && half.TryOfferDrop(kind, sourceSlot, localX - half.Bounds.X, localY - half.Bounds.Y, release))
        {
            return true;
        }

        return Tray is { Visible: true } tray
            && tray.TryOfferDrop(kind, sourceSlot, localX - tray.Bounds.X, localY - tray.Bounds.Y, release);
    }
}
