using DaScreenshotStudio.Assets.Tables;
using DaScreenshotStudio.Engine.Rendering;
using DaScreenshotStudio.Engine.Text;
using DaScreenshotStudio.Engine.Core;

namespace DaScreenshotStudio.Engine.Ui;

public abstract class GoldAmountDialogPane : LayoutPane
{
    public const string LayoutName = "_nmoney.txt";

    public const byte AmountBackgroundColor = 0x1F;

    public const byte AmountInkColor = TextPalette.InterfaceIndex;

    public const byte PromptInkColor = 0xFF;

    public const int TopOffset = 0x64;

    public const int CenterX = 0x140;

    private readonly TextEditPane? _amount;

    protected GoldAmountDialogPane(PixelBlender blender, UiAssets assets, int promptSlot)
        : base(assets, blender, LayoutName)
    {
        ConsumesPointerAnywhere = true;
        Visible = false;

        var ok = AddButton("OK", _ => Accept());
        Unfocusable(ok);
        Unfocusable(AddButton("Cancel", _ => Cancel()));

        DefaultControl = ok;

        var prompt = AddLabel("Title", paletteIndex: PromptInkColor);
        prompt?.SetText(assets.Messages.Get(promptSlot));

        _amount = AddEdit("Text", TextEditPane.MaxValueBytes);

        if (_amount is not null)
        {
            _amount.PaletteIndex = AmountInkColor;

            _amount.BackgroundIndex = AmountBackgroundColor;
            _amount.IsTransparent = true;
        }

        Place();
    }

    public Action? Closed { get; set; }

    protected abstract bool EscapeCancels { get; }

    protected void Open()
    {
        _amount?.Clear();
        Focus(_amount);
        Visible = true;
        Invalidate();
    }

    public void Cancel()
    {
        if (!Visible)
        {
            return;
        }

        Focus(null);
        _amount?.Clear();
        Visible = false;
        Invalidate();
        Closed?.Invoke();
    }

    private void Accept()
    {
        if (!Visible)
        {
            return;
        }

        Focus(null);
        _amount?.Clear();
        Visible = false;
        Invalidate();
        Closed?.Invoke();
    }

    public static uint Parse(scoped ReadOnlySpan<byte> text)
    {
        var index = 0;
        while (index < text.Length && text[index] is (byte)' ' or (byte)'\t')
        {
            index++;
        }

        var negative = false;
        if (index < text.Length && text[index] is (byte)'+' or (byte)'-')
        {
            negative = text[index] == (byte)'-';
            index++;
        }

        var value = 0;
        for (; index < text.Length && text[index] is >= (byte)'0' and <= (byte)'9'; index++)
        {
            value = unchecked((value * 10) + (text[index] - (byte)'0'));
        }

        return unchecked((uint)(negative ? -value : value));
    }

    protected override void OnCancel()
    {
        if (EscapeCancels)
        {
            Cancel();
        }
    }

    private void Place()
    {
        var window = Bounds;
        SetBounds(new PixelRect(CenterX - (window.Width / 2), TopOffset, window.Width, window.Height));
    }
}

public sealed class DropGoldDialogPane : GoldAmountDialogPane
{
    private MapPoint _target;

    public DropGoldDialogPane(PixelBlender blender, UiAssets assets)
        : base(blender, assets, MessageTable.DropGoldPromptSlot)
    {
    }

    public MapPoint Target => _target;

    protected override bool EscapeCancels => true;

    public void Open(in MapPoint target)
    {
        _target = target;
        Open();
    }
}

public sealed class GiveGoldDialogPane : GoldAmountDialogPane
{
    private EntityId _target;

    public GiveGoldDialogPane(PixelBlender blender, UiAssets assets)
        : base(blender, assets, MessageTable.GiveGoldPromptSlot)
    {
    }

    public EntityId Target => _target;

    protected override bool EscapeCancels => false;

    public void Open(EntityId target)
    {
        _target = target;
        Open();
    }
}
