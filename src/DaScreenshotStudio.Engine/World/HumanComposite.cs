using DaScreenshotStudio.Assets.Characters;
using DaScreenshotStudio.Assets.Imaging;
using DaScreenshotStudio.Assets.Palettes;
using DaScreenshotStudio.Engine.Rendering;

namespace DaScreenshotStudio.Engine.World;

public static class HumanComposite
{
    public const int AnchorX = 28;

    public const int CompanionAnchorX = 55;

    public const int FootY = 70;

    public const int CompanionOffsetX = AnchorX - CompanionAnchorX;

    public const int CanvasWidth = IsometricProjection.TileWidth;

    public const int StandingFramesPerView = 5;

    public readonly struct Result
    {
        public Result(ushort[] pixels, int width, int height, int anchorX, int anchorY)
        {
            Pixels = pixels;
            Width = width;
            Height = height;
            AnchorX = anchorX;
            AnchorY = anchorY;
        }

        public ushort[] Pixels { get; }

        public int Width { get; }

        public int Height { get; }

        public int AnchorX { get; }

        public int AnchorY { get; }

        public bool IsEmpty => Width <= 0 || Height <= 0;

        public WorldSprite ToSprite() =>
            IsEmpty
                ? default
                : new WorldSprite(
                    new Pixmap(new ReadOnlyMemory<ushort>(Pixels, 0, Width * Height), Width, Height),
                    AnchorX,
                    AnchorY);
    }

    public static bool IsCompanionLetter(char letter) => letter is 'w' or 'p' or 'c' or 'g';

    public static HumanIdleMotion BuildIdleMotion(CharacterArt art, in HumanState state)
    {
        ArgumentNullException.ThrowIfNull(art);

        Span<char> name = stackalloc char[HumanPartName.MaxNameLength];
        Span<int> frameCounts = stackalloc int[HumanPartCategory.Count];

        for (var category = 0; category < frameCounts.Length; category++)
        {
            if (HumanPartName.TryFormat(in state.Appearance, category, HumanIdleMotion.MotionIndex, name, out var length))
            {
                frameCounts[category] = art.Get(name[..length])?.FrameCount ?? 0;
            }
        }

        return HumanIdleMotion.Build(frameCounts);
    }

    public static int SelectFrame(EpfImage image, HumanDirection direction, int motionFrame, int framesPerView = 0)
    {
        ArgumentNullException.ThrowIfNull(image);

        var perView = framesPerView > 0 ? framesPerView : image.FrameCount / 2;
        var frame = HumanPartOrder.ViewFrame(motionFrame, perView, direction);

        return (uint)frame < (uint)image.FrameCount ? frame : -1;
    }

    public static Result Compose(
        CharacterArt art,
        in HumanState state,
        HumanDirection direction,
        int motion,
        int motionFrame,
        int framesPerView = StandingFramesPerView,
        in HumanIdleMotion idle = default,
        int idleStep = -1,
        int emoteFrame = 0,
        char variant = '\0',
        ushort[]? into = null)
    {
        ArgumentNullException.ThrowIfNull(art);

        Span<char> name = stackalloc char[HumanPartName.MaxNameLength];
        var emote = ResolveEmote(art, in state, emoteFrame);

        if (!Measure(art, in state, direction, motion, motionFrame, framesPerView, in idle, idleStep, in emote, variant, name, out var left, out var top, out var right, out var bottom, out var drawEmote))
        {
            return default;
        }

        var width = right - left;
        var height = bottom - top;
        var length = width * height;

        ushort[] pixels;
        if (into is not null && into.Length >= length)
        {
            pixels = into;
            Array.Clear(pixels, 0, length);
        }
        else
        {
            pixels = new ushort[length];
        }

        var walker = new HumanPartWalker(in state.Appearance, direction, motion, variant, in idle, idleStep, name);
        var female = state.Appearance.Body.Prefix == 'w';
        var pending = drawEmote;

        while (walker.MoveNext())
        {
            if (pending && walker.Category == HumanPartCategory.Head)
            {
                pending = false;
                DrawEmote(art, pixels, width, height, left, top, in emote, direction);
            }

            if (drawEmote && emote.SuppressesAccessory1 && walker.Category == HumanPartCategory.Accessory1Front)
            {
                continue;
            }

            if (art.Get(walker.Name) is not { } image)
            {
                continue;
            }

            var index = walker.AbsoluteFrame >= 0
                ? (walker.AbsoluteFrame < image.FrameCount ? walker.AbsoluteFrame : -1)
                : walker.Frame < 0
                    ? SelectFrame(image, direction, motionFrame, framesPerView)
                    : SelectFrame(image, direction, walker.Frame);
            if (index < 0)
            {
                continue;
            }

            var frame = image[index];
            if (frame.IsEmpty)
            {
                continue;
            }

            var paletteSpriteId =
                state.Appearance.GetPaletteSpriteId(walker.Category, walker.SpriteId);
            var palette = art.GetPalette(walker.Category, paletteSpriteId, female, walker.PaletteSelector);
            var dye = walker.Dye < 0 ? default : art.GetDye(walker.Dye);
            var source = image.GetPixels(index);
            var shift = IsCompanionLetter(walker.Letter) ? CompanionOffsetX : 0;

            Draw(pixels, width, height, left, top, in frame, source, palette, dye, shift, walker.IsMirrored);
        }

        return new Result(pixels, width, height, AnchorX - left, FootY - top);
    }

    private static bool Measure(
        CharacterArt art,
        in HumanState state,
        HumanDirection direction,
        int motion,
        int motionFrame,
        int framesPerView,
        in HumanIdleMotion idle,
        int idleStep,
        in EmoteLayer emote,
        char variant,
        Span<char> name,
        out int left,
        out int top,
        out int right,
        out int bottom,
        out bool drawEmote)
    {
        left = int.MaxValue;
        top = int.MaxValue;
        right = int.MinValue;
        bottom = int.MinValue;

        var walker = new HumanPartWalker(in state.Appearance, direction, motion, variant, in idle, idleStep, name);
        var any = false;
        var heads = 0;

        var hasAccessory = false;
        var accessoryLeft = 0;
        var accessoryRight = 0;
        var accessoryTop = 0;
        var accessoryBottom = 0;

        while (walker.MoveNext())
        {
            if (art.Get(walker.Name) is not { } image)
            {
                continue;
            }

            var index = walker.AbsoluteFrame >= 0
                ? (walker.AbsoluteFrame < image.FrameCount ? walker.AbsoluteFrame : -1)
                : walker.Frame < 0
                    ? SelectFrame(image, direction, motionFrame, framesPerView)
                    : SelectFrame(image, direction, walker.Frame);
            if (index < 0)
            {
                continue;
            }

            var frame = image[index];
            if (frame.IsEmpty)
            {
                continue;
            }

            if (walker.Category is HumanPartCategory.Head
                or HumanPartCategory.HeadAlternate1
                or HumanPartCategory.HeadAlternate2)
            {
                heads++;
            }

            var shift = IsCompanionLetter(walker.Letter) ? CompanionOffsetX : 0;
            var boxLeft = frame.Left + shift;
            var boxRight = frame.Right + shift;

            if (walker.IsMirrored)
            {
                Mirror(boxLeft, boxRight, out boxLeft, out boxRight);
            }

            if (emote.SuppressesAccessory1 && walker.Category == HumanPartCategory.Accessory1Front)
            {
                hasAccessory = true;
                accessoryLeft = boxLeft;
                accessoryRight = boxRight;
                accessoryTop = frame.Top;
                accessoryBottom = frame.Bottom;
                continue;
            }

            left = Math.Min(left, boxLeft);
            right = Math.Max(right, boxRight);
            top = Math.Min(top, (int)frame.Top);
            bottom = Math.Max(bottom, (int)frame.Bottom);
            any = true;
        }

        drawEmote = emote.Image is not null && heads > 0;

        if (drawEmote)
        {
            var box = emote.Image![emote.Frame];
            var boxLeft = (int)box.Left;
            var boxRight = (int)box.Right;

            if (HumanPartOrder.IsMirrored(direction))
            {
                Mirror(boxLeft, boxRight, out boxLeft, out boxRight);
            }

            left = Math.Min(left, boxLeft);
            right = Math.Max(right, boxRight);
            top = Math.Min(top, (int)box.Top);
            bottom = Math.Max(bottom, (int)box.Bottom);
        }
        else if (hasAccessory)
        {
            left = Math.Min(left, accessoryLeft);
            right = Math.Max(right, accessoryRight);
            top = Math.Min(top, accessoryTop);
            bottom = Math.Max(bottom, accessoryBottom);
            any = true;
        }

        return any && right > left && bottom > top;
    }

    private static EmoteLayer ResolveEmote(CharacterArt art, in HumanState state, int emoteFrame)
    {
        var frame = emoteFrame - 1;
        if (frame < 0)
        {
            return default;
        }

        var accessory = state.Appearance.Accessory1Sprite;
        var alternate = HumanEmoteArt.IsAlternateSheet(accessory);

        if (art.Get(HumanEmoteArt.SelectSheet(accessory)) is not { } image
            || (uint)frame >= (uint)image.FrameCount
            || image[frame].IsEmpty)
        {
            return default;
        }

        return new EmoteLayer(
            image,
            frame,
            alternate ? HumanAppearance.NoPaletteSelector : state.Appearance.SkinColor,
            alternate);
    }

    private static void DrawEmote(
        CharacterArt art,
        ushort[] pixels,
        int width,
        int height,
        int left,
        int top,
        in EmoteLayer emote,
        HumanDirection direction)
    {
        var image = emote.Image!;
        var frame = image[emote.Frame];

        Draw(
            pixels,
            width,
            height,
            left,
            top,
            in frame,
            image.GetPixels(emote.Frame),
            art.GetSkinPalette(emote.PaletteSelector),
            default,
            0,
            HumanPartOrder.IsMirrored(direction));
    }

    private readonly struct EmoteLayer(EpfImage image, int frame, int paletteSelector, bool suppressesAccessory1)
    {
        public EpfImage? Image { get; } = image;

        public int Frame { get; } = frame;

        public int PaletteSelector { get; } = paletteSelector;

        public bool SuppressesAccessory1 { get; } = suppressesAccessory1;
    }

    public static void Mirror(int boxLeft, int boxRight, out int mirroredLeft, out int mirroredRight)
    {
        mirroredLeft = (2 * AnchorX) - boxRight;
        mirroredRight = (2 * AnchorX) - boxLeft;
    }

    private static void Draw(
        ushort[] destination,
        int width,
        int height,
        int originLeft,
        int originTop,
        in EpfFrame frame,
        ReadOnlySpan<byte> source,
        PackedPalette palette,
        ReadOnlySpan<ushort> dye,
        int shift,
        bool mirrored)
    {
        var boxLeft = frame.Left + shift;
        var boxRight = frame.Right + shift;

        if (mirrored)
        {
            Mirror(boxLeft, boxRight, out boxLeft, out _);
        }

        var frameWidth = frame.Width;
        var startX = boxLeft - originLeft;
        var startY = frame.Top - originTop;
        var colors = palette.Colors;

        for (var y = 0; y < frame.Height; y++)
        {
            var destinationY = startY + y;
            if ((uint)destinationY >= (uint)height)
            {
                continue;
            }

            var rowStart = y * frameWidth;
            if (rowStart >= source.Length)
            {
                break;
            }

            var row = source.Slice(rowStart, Math.Min(frameWidth, source.Length - rowStart));
            var destinationRow = destinationY * width;

            for (var x = 0; x < row.Length; x++)
            {
                int index = row[x];
                if (index == 0)
                {
                    continue;
                }

                var destinationX = startX + (mirrored ? frameWidth - 1 - x : x);
                if ((uint)destinationX >= (uint)width)
                {
                    continue;
                }

                var dyed = index - ColoringTable.DyeFirstIndex;
                destination[destinationRow + destinationX] =
                    (uint)dyed < (uint)dye.Length ? dye[dyed] : colors[index];
            }
        }
    }
}
