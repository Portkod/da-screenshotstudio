using DaScreenshotStudio.Engine.Rendering;
using DaScreenshotStudio.Engine.Text;

namespace DaScreenshotStudio.Engine.Ui;

public abstract class LineInputPane : GuiChildPane
{
    public const int CellWidth = 6;

    public const int CaretWidth = 2;

    public const int PromptLeft = -1;

    protected LineInputPane(in PixelRect bounds, PixelBlender blender, GameFont font, TextPalette palette)
        : base(in bounds, blender)
    {
        ArgumentNullException.ThrowIfNull(palette);

        Palette = palette;
        PromptBlock = new TextBlock(font, palette, Math.Max(1, bounds.Width), 1) { Wraps = false };
        LineBlock = new TextBlock(font, palette, Math.Max(1, bounds.Width), 1) { Wraps = false };

        Visible = false;
    }

    protected TextPalette Palette { get; }

    protected TextBlock PromptBlock { get; }

    protected TextBlock LineBlock { get; }

    protected abstract bool AcceptsKeys { get; }

    protected virtual bool CanCancel => true;

    protected virtual bool Accepts(byte character) => character >= 0x20 && character != 0x7F;

    protected override bool OnKey(in KeyEvent key)
    {
        if (!AcceptsKeys)
        {
            return false;
        }

        switch (key.Type)
        {
            case UiEventType.Character when Accepts(key.Character):
                if (!TextEditing.IsModified(key.Modifiers))
                {
                    Append(key.Character);
                }

                return true;

            case UiEventType.KeyDown when key.Key == UiKeys.Backspace:
                Backspace();
                return true;

            case UiEventType.KeyDown when key.Key == UiKeys.Enter:
                Submit();
                return true;

            case UiEventType.KeyDown when CanCancel && key.Key == UiKeys.Escape:
                CancelInput();
                return true;

            default:
                OnOtherKey(in key);
                return true;
        }
    }

    protected abstract void Append(byte character);

    protected abstract void Backspace();

    protected abstract void Submit();

    protected virtual void CancelInput()
    {
    }

    protected virtual void OnOtherKey(in KeyEvent key)
    {
    }

    protected override void OnBoundsChanged()
    {
        PromptBlock.MaxWidth = Math.Max(1, Bounds.Width);
        LineBlock.MaxWidth = Math.Max(1, Bounds.Width);
    }

    protected override void OnDrawContent(Canvas canvas)
    {
        PaintBackground(canvas);

        if (!AcceptsKeys)
        {
            return;
        }

        DrawLine(canvas);
    }

    protected abstract void DrawLine(Canvas canvas);

    protected void DrawShadedLine(
        Canvas canvas,
        ushort fill,
        byte ink,
        int bandWidth,
        scoped ReadOnlySpan<byte> prompt,
        scoped ReadOnlySpan<byte> value)
    {
        canvas.FillRect(canvas.Bounds, fill, BlendMode.Copy);

        PromptBlock.DefaultPaletteIndex = ink;
        PromptBlock.SetText(prompt);
        PromptBlock.Draw(canvas, PromptLeft, 0);

        LineBlock.DefaultPaletteIndex = ink;
        LineBlock.SetText(value);
        LineBlock.Draw(canvas, bandWidth, 0);

        var caret = Math.Min(bandWidth + Math.Max(LineBlock.Width - 1, 0), canvas.Width - CaretWidth);
        canvas.FillRect(
            new PixelRect(caret, 0, CaretWidth, canvas.Height), Palette[ink], BlendMode.Copy);
    }

    protected void DrawOpaqueLine(
        Canvas canvas,
        byte backgroundIndex,
        byte ink,
        int controlOffsetX,
        int promptLength,
        scoped ReadOnlySpan<byte> prompt,
        scoped ReadOnlySpan<byte> value)
    {
        canvas.FillRect(
            new PixelRect(0, 0, canvas.Width, canvas.Height), Palette[backgroundIndex], BlendMode.Copy);

        PromptBlock.DefaultPaletteIndex = ink;
        PromptBlock.SetText(prompt);
        PromptBlock.Draw(canvas, controlOffsetX, 0);

        var left = (promptLength * CellWidth) + 1 + controlOffsetX;
        LineBlock.DefaultPaletteIndex = ink;
        LineBlock.SetText(value);
        LineBlock.Draw(canvas, left, 0);

        var caret = Math.Clamp(
            left + Math.Max(LineBlock.Width - 1, 0), 0, Math.Max(0, canvas.Width - CaretWidth));
        canvas.FillRect(
            new PixelRect(caret, 0, CaretWidth, LineBlock.LineHeight), Palette[ink], BlendMode.Copy);
    }

    public static void CompositeLine(Canvas target, int absoluteX, int absoluteY, Canvas content, int bandWidth)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(content);

        var width = content.Bounds.Width;
        var height = content.Bounds.Height;
        var band = Math.Clamp(bandWidth, 0, width);

        if (band > 0)
        {
            target.BlitCanvas(
                absoluteX, absoluteY, content, new PixelRect(0, 0, band, height), BlendMode.Copy);
        }

        if (band < width)
        {
            target.BlitCanvas(
                absoluteX + band,
                absoluteY,
                content,
                new PixelRect(band, 0, width - band, height),
                BlendMode.Shade);
        }
    }
}
