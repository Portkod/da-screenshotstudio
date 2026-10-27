using System.Numerics;
using DaScreenshotStudio.Assets.Imaging;

namespace DaScreenshotStudio.Engine.Rendering;

public sealed class Canvas
{
    private readonly PixelSurface? _own;
    private readonly Canvas? _parent;
    private int _originX;
    private int _originY;
    private int _width;
    private int _height;
    private PixelRect _clip;

    public DrawState State;

    public Canvas(int width, int height, PixelBlender blender)
    {
        ArgumentNullException.ThrowIfNull(blender);

        _own = new PixelSurface(width, height);
        _width = _own.Width;
        _height = _own.Height;
        _clip = new PixelRect(0, 0, _width, _height);
        Blender = blender;
        State = DrawState.Default;
    }

    private Canvas(Canvas parent, in PixelRect bounds)
    {
        _parent = parent;
        _originX = bounds.X;
        _originY = bounds.Y;
        _width = Math.Max(0, bounds.Width);
        _height = Math.Max(0, bounds.Height);
        _clip = new PixelRect(0, 0, _width, _height);
        Blender = parent.Blender;
        State = DrawState.Default;
    }

    public PixelBlender Blender { get; }

    public int Width => _width;

    public int Height => _height;

    public PixelRect Bounds => new(0, 0, _width, _height);

    public PixelRect Clip => _clip;

    public PixelSurface? Surface => _own;

    public void SetBounds(in PixelRect bounds)
    {
        _originX = bounds.X;
        _originY = bounds.Y;
        _width = Math.Max(0, bounds.Width);
        _height = Math.Max(0, bounds.Height);
        _own?.Resize(_width, _height);
        _clip = new PixelRect(0, 0, _width, _height);
    }

    public void SetClip(in PixelRect clip) => _clip = clip.Intersect(Bounds);

    public void ResetClip() => _clip = Bounds;

    public void SetDrawColor(ushort color) => State.PrimaryColor = color;

    public void ClearRect(in PixelRect rect) => FillRect(in rect, 0, BlendMode.Copy);

    public void Clear() => ClearRect(Bounds);

    public void FillRect(in PixelRect rect) => FillRect(in rect, State.PrimaryColor, State.Mode);

    public void FillRect(in PixelRect rect, ushort color, BlendMode mode)
    {
        if (!TryResolveRows(in rect, out var rows))
        {
            return;
        }

        var dithered = State.Dither == DitherMode.Halftone;
        var weight = State.AlphaWeight;

        for (var y = 0; y < rows.Height; y++)
        {
            var row = rows.Row(y);

            if (!dithered)
            {
                if (mode == BlendMode.Copy)
                {
                    row.Fill(color);
                }
                else
                {
                    for (var i = 0; i < row.Length; i++)
                    {
                        row[i] = Blender.Combine(mode, color, row[i], weight);
                    }
                }

                continue;
            }

            var absoluteY = rows.AbsoluteY + y;
            var first = ((rows.AbsoluteX & 1) == ((absoluteY / 2) & 1)) ? 0 : 1;

            if (mode == BlendMode.Copy)
            {
                for (var i = first; i < row.Length; i += 2)
                {
                    row[i] = color;
                }
            }
            else
            {
                for (var i = first; i < row.Length; i += 2)
                {
                    row[i] = Blender.Combine(mode, color, row[i], weight);
                }
            }
        }
    }

    public void DrawRectOutline(in PixelRect rect)
    {
        if (rect.IsEmpty)
        {
            return;
        }

        FillRect(new PixelRect(rect.X, rect.Y, rect.Width, 1));
        FillRect(new PixelRect(rect.X, rect.Bottom - 1, rect.Width, 1));
        FillRect(new PixelRect(rect.X, rect.Y, 1, rect.Height));
        FillRect(new PixelRect(rect.Right - 1, rect.Y, 1, rect.Height));
    }

    public void SetPixel(int x, int y, ushort color)
    {
        if (TryBeginPixels(out var writer))
        {
            writer.Set(x, y, color);
        }
    }

    public bool TryBeginPixels(out PixelWriter writer)
    {
        var surface = ResolveBacking(out var originX, out var originY);
        var clip = AbsoluteClip(originX, originY).Intersect(new PixelRect(0, 0, surface.Width, surface.Height));

        writer = new PixelWriter(surface.Pixels, surface.Pitch, in clip, originX, originY, Blender, in State);
        return !clip.IsEmpty;
    }

    public readonly ref struct PixelWriter
    {
        private readonly Span<ushort> _pixels;
        private readonly int _pitch;
        private readonly PixelRect _clip;
        private readonly int _originX;
        private readonly int _originY;
        private readonly PixelBlender _blender;
        private readonly BlendMode _mode;
        private readonly bool _halftone;
        private readonly int _weight;

        internal PixelWriter(
            Span<ushort> pixels,
            int pitch,
            scoped in PixelRect clip,
            int originX,
            int originY,
            PixelBlender blender,
            scoped in DrawState state)
        {
            _pixels = pixels;
            _pitch = pitch;
            _clip = clip;
            _originX = originX;
            _originY = originY;
            _blender = blender;
            _mode = state.Mode;
            _halftone = state.Dither == DitherMode.Halftone;
            _weight = state.AlphaWeight;
        }

        public void Set(int x, int y, ushort color)
        {
            x += _originX;
            y += _originY;

            if (!_clip.Contains(x, y) || (_halftone && (x & 1) != ((y / 2) & 1)))
            {
                return;
            }

            ref var pixel = ref _pixels[(y * _pitch) + x];
            pixel = _mode == BlendMode.Copy ? color : _blender.Combine(_mode, color, pixel, _weight);
        }
    }

    public void DrawHorizontalSpan(int x, int y, int length, ushort color)
    {
        if (length < 0)
        {
            x += length;
            length = -length;
        }

        FillRect(new PixelRect(x, y, length, 1), color, State.Mode);
    }

    public void DrawLine(int x0, int y0, int x1, int y1, ushort color)
    {
        var deltaX = Math.Abs(x1 - x0);
        var deltaY = -Math.Abs(y1 - y0);
        var stepX = x0 < x1 ? 1 : -1;
        var stepY = y0 < y1 ? 1 : -1;
        var error = deltaX + deltaY;

        var inside = TryBeginPixels(out var writer);

        while (true)
        {
            if (inside)
            {
                writer.Set(x0, y0, color);
            }

            if (x0 == x1 && y0 == y1)
            {
                break;
            }

            var doubled = error * 2;
            if (doubled >= deltaY)
            {
                error += deltaY;
                x0 += stepX;
            }

            if (doubled <= deltaX)
            {
                error += deltaX;
                y0 += stepY;
            }
        }
    }

    public void BlendRect(in PixelRect rect, ushort color, int weight)
    {
        if (!TryResolve(in rect, out var target))
        {
            return;
        }

        weight = Math.Clamp(weight, 0, PixelBlender.MaxAlphaWeight);
        var pixels = target.Surface.Pixels;
        var pitch = target.Surface.Pitch;
        var area = target.Area;

        for (var y = area.Y; y < area.Bottom; y++)
        {
            var row = pixels.Slice((y * pitch) + area.X, area.Width);
            for (var i = 0; i < row.Length; i++)
            {
                row[i] = Blender.Alpha(color, row[i], weight);
            }
        }
    }

    public void LightRect(in PixelRect rect, ushort color, int weight)
    {
        if (!TryResolve(in rect, out var target))
        {
            return;
        }

        weight = Math.Clamp(weight, 0, PixelBlender.MaxAlphaWeight);
        var pixels = target.Surface.Pixels;
        var pitch = target.Surface.Pitch;
        var area = target.Area;

        for (var y = area.Y; y < area.Bottom; y++)
        {
            Blender.LightSpan(pixels.Slice((y * pitch) + area.X, area.Width), color, weight);
        }
    }

    public int LightRowWithMask(int x, int y, ushort color, ReadOnlySpan<byte> weights)
    {
        if (weights.IsEmpty || !TryResolve(new PixelRect(x, y, weights.Length, 1), out var target))
        {
            return 0;
        }

        var area = target.Area;
        var pixels = target.Surface.Pixels;
        var row = pixels.Slice((area.Y * target.Surface.Pitch) + area.X, area.Width);
        var source = weights.Slice(target.TrimX, area.Width);

        return Blender.LightSpanWithMask(row, color, source);
    }

    public void TilePixmap(in PixelRect rect, in Pixmap pixmap, BlendMode mode)
    {
        if (pixmap.IsEmpty || !TryResolveRows(in rect, out var rows))
        {
            return;
        }

        var anchorX = rect.X + rows.OriginX;
        var anchorY = rect.Y + rows.OriginY;
        var startColumn = Modulo(rows.AbsoluteX - anchorX, pixmap.Width);
        var source = pixmap.Pixels;

        for (var y = 0; y < rows.Height; y++)
        {
            var sourceRow = source.Slice(Modulo(rows.AbsoluteY + y - anchorY, pixmap.Height) * pixmap.Stride, pixmap.Width);
            var destination = rows.Row(y);

            var column = startColumn;
            var i = 0;
            while (i < destination.Length)
            {
                var run = Math.Min(pixmap.Width - column, destination.Length - i);
                var from = sourceRow.Slice(column, run);
                var to = destination.Slice(i, run);

                if (mode == BlendMode.Copy)
                {
                    from.CopyTo(to);
                }
                else if (mode == BlendMode.Transparent)
                {
                    CopyOverColorKey(from, to);
                }
                else
                {
                    for (var k = 0; k < to.Length; k++)
                    {
                        to[k] = Blender.Combine(mode, from[k], to[k], State.AlphaWeight);
                    }
                }

                i += run;
                column = 0;
            }
        }
    }

    private static void CopyOverColorKey(ReadOnlySpan<ushort> source, Span<ushort> destination)
    {
        var i = 0;

        if (Vector.IsHardwareAccelerated && destination.Length >= Vector<ushort>.Count)
        {
            for (; i <= destination.Length - Vector<ushort>.Count; i += Vector<ushort>.Count)
            {
                var pixels = new Vector<ushort>(source[i..]);

                Vector.ConditionalSelect(
                        Vector.Equals(pixels, Vector<ushort>.Zero),
                        new Vector<ushort>(destination[i..]),
                        pixels)
                    .CopyTo(destination[i..]);
            }
        }

        for (; i < destination.Length; i++)
        {
            if (source[i] != 0)
            {
                destination[i] = source[i];
            }
        }
    }

    public void BlitPixmap(int destinationX, int destinationY, in Pixmap pixmap, BlendMode mode)
    {
        if (pixmap.IsEmpty
            || !TryResolveRows(new PixelRect(destinationX, destinationY, pixmap.Width, pixmap.Height), out var rows))
        {
            return;
        }

        var source = SourceOrigin(in pixmap, in rows);

        for (var y = 0; y < rows.Height; y++)
        {
            BlendRowOverKey(source.Slice(y * pixmap.Stride, rows.Width), rows.Row(y), mode, keyed: mode != BlendMode.Copy);
        }
    }

    public void BlitPixmapAlphaPlane(int destinationX, int destinationY, in Pixmap pixmap, ReadOnlySpan<byte> weights)
    {
        if (pixmap.IsEmpty
            || weights.Length < pixmap.Width * pixmap.Height
            || !TryResolveRows(new PixelRect(destinationX, destinationY, pixmap.Width, pixmap.Height), out var rows))
        {
            return;
        }

        var source = SourceOrigin(in pixmap, in rows);
        var plane = weights[((rows.TrimY * pixmap.Width) + rows.TrimX)..];

        for (var y = 0; y < rows.Height; y++)
        {
            var row = source.Slice(y * pixmap.Stride, rows.Width);
            var weight = plane.Slice(y * pixmap.Width, rows.Width);
            var destination = rows.Row(y);

            for (var i = 0; i < destination.Length; i++)
            {
                destination[i] = Blender.Alpha(row[i], destination[i], weight[i]);
            }
        }
    }

    public void BlitPixmapPainted(
        int destinationX,
        int destinationY,
        in Pixmap pixmap,
        BlendMode mode,
        int firstPaintedRow,
        ushort color,
        int weight)
    {
        if (pixmap.IsEmpty
            || !TryResolveRows(new PixelRect(destinationX, destinationY, pixmap.Width, pixmap.Height), out var rows))
        {
            return;
        }

        var source = SourceOrigin(in pixmap, in rows);

        for (var y = 0; y < rows.Height; y++)
        {
            var row = source.Slice(y * pixmap.Stride, rows.Width);
            var destination = rows.Row(y);

            if (rows.TrimY + y < firstPaintedRow)
            {
                BlendRowOverKey(row, destination, mode, keyed: true);
                continue;
            }

            for (var i = 0; i < destination.Length; i++)
            {
                if (row[i] != 0)
                {
                    destination[i] = Blender.Alpha(row[i], color, weight);
                }
            }
        }
    }

    public void BlitPixmapCombed(int destinationX, int destinationY, in Pixmap pixmap, BlendMode mode, int shift)
    {
        if (pixmap.IsEmpty)
        {
            return;
        }

        if (shift <= 0)
        {
            BlitPixmap(destinationX, destinationY, in pixmap, mode);
            return;
        }

        var box = new PixelRect(destinationX, destinationY, pixmap.Width + shift, pixmap.Height);
        if (!TryResolveRows(in box, out var rows))
        {
            return;
        }

        var pixels = pixmap.Pixels;

        for (var y = 0; y < rows.Height; y++)
        {
            var sourceY = rows.TrimY + y;
            var displacement = (sourceY & 1) == 1 ? shift : 0;

            var first = Math.Max(0, displacement - rows.TrimX);
            var last = Math.Min(rows.Width, pixmap.Width + displacement - rows.TrimX);
            if (last <= first)
            {
                continue;
            }

            var source = pixels.Slice((sourceY * pixmap.Stride) + rows.TrimX + first - displacement, last - first);
            BlendRowOverKey(source, rows.Row(y)[first..last], mode, keyed: mode != BlendMode.Copy);
        }
    }

    public void BlitMasked(
        int destinationX, int destinationY, in Pixmap pixmap, ReadOnlySpan<byte> coverage, BlendMode mode)
    {
        if (pixmap.IsEmpty
            || !TryResolveRows(new PixelRect(destinationX, destinationY, pixmap.Width, pixmap.Height), out var rows))
        {
            return;
        }

        var source = SourceOrigin(in pixmap, in rows);
        var ink = coverage[((rows.TrimY * pixmap.Stride) + rows.TrimX)..];

        for (var y = 0; y < rows.Height; y++)
        {
            BlendRowOverMask(
                source.Slice(y * pixmap.Stride, rows.Width),
                ink.Slice(y * pixmap.Stride, rows.Width),
                rows.Row(y),
                mode);
        }
    }

    public void FillMasked(
        int destinationX, int destinationY, int width, int height, ReadOnlySpan<byte> coverage, int stride, ushort color)
    {
        if (width <= 0 || height <= 0
            || !TryResolveRows(new PixelRect(destinationX, destinationY, width, height), out var rows))
        {
            return;
        }

        var ink = coverage[((rows.TrimY * stride) + rows.TrimX)..];

        for (var y = 0; y < rows.Height; y++)
        {
            var row = ink.Slice(y * stride, rows.Width);
            var destination = rows.Row(y);

            for (var i = 0; i < destination.Length; i++)
            {
                if (row[i] != 0)
                {
                    destination[i] = color;
                }
            }
        }
    }

    public void BlitPixmapAveragedWith(int destinationX, int destinationY, in Pixmap pixmap, BlendMode mode, ushort color)
    {
        if (pixmap.IsEmpty
            || !TryResolveRows(new PixelRect(destinationX, destinationY, pixmap.Width, pixmap.Height), out var rows))
        {
            return;
        }

        var source = SourceOrigin(in pixmap, in rows);

        for (var y = 0; y < rows.Height; y++)
        {
            BlendRowTintedOverKey(source.Slice(y * pixmap.Stride, rows.Width), rows.Row(y), mode, color);
        }
    }

    public void BlitCanvas(int destinationX, int destinationY, Canvas source, in PixelRect sourceRect, BlendMode mode)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (!source.TryResolve(in sourceRect, out var from))
        {
            return;
        }

        if (!TryResolve(
            new PixelRect(destinationX + from.TrimX, destinationY + from.TrimY, from.Area.Width, from.Area.Height),
            out var to))
        {
            return;
        }

        var readX = from.Area.X + to.TrimX;
        var readY = from.Area.Y + to.TrimY;

        var read = from.Surface.ReadOnlyPixels[((readY * from.Surface.Pitch) + readX)..];
        var rows = new CanvasRows(to.Surface.Pixels, to.Surface.Pitch, to.Area, to.TrimX, to.TrimY, to.OriginX, to.OriginY);

        for (var y = 0; y < rows.Height; y++)
        {
            BlendRowOverKey(read.Slice(y * from.Surface.Pitch, rows.Width), rows.Row(y), mode, keyed: mode != BlendMode.Copy);
        }
    }

    public void DrawGroundDiamond(int destinationX, int destinationY, ReadOnlySpan<ushort> diamond)
    {
        if (diamond.Length < GroundTileBank.VisiblePixelCount)
        {
            return;
        }

        if (!TryResolve(
            new PixelRect(destinationX, destinationY, GroundTileBank.TileWidth, GroundTileBank.TileHeight),
            out var target))
        {
            return;
        }

        var absoluteX = destinationX + target.OriginX;
        var absoluteY = destinationY + target.OriginY;

        var pixels = target.Surface.Pixels;
        var pitch = target.Surface.Pitch;
        var clip = target.Area;
        var read = 0;

        for (var row = 0; row < GroundTileBank.TileHeight; row++)
        {
            var left = GroundTileBank.RowLeft(row);
            var visible = GroundTileBank.TileWidth - (left * 2);
            var sourceRow = diamond.Slice(read, visible);
            read += visible;

            var y = absoluteY + row;
            if (y < clip.Y || y >= clip.Bottom)
            {
                continue;
            }

            var start = Math.Max(clip.X, absoluteX + left);
            var end = Math.Min(clip.Right, absoluteX + left + visible);
            if (end <= start)
            {
                continue;
            }

            sourceRow.Slice(start - absoluteX - left, end - start)
                .CopyTo(pixels.Slice((y * pitch) + start, end - start));
        }
    }

    private static int Modulo(int value, int length)
    {
        var result = value % length;
        return result < 0 ? result + length : result;
    }

    private static ReadOnlySpan<ushort> SourceOrigin(in Pixmap pixmap, in CanvasRows rows) =>
        pixmap.Pixels[((rows.TrimY * pixmap.Stride) + rows.TrimX)..];

    private void BlendRowOverKey(ReadOnlySpan<ushort> source, Span<ushort> destination, BlendMode mode, bool keyed)
    {
        switch (mode)
        {
            case BlendMode.Copy when !keyed:
                source.CopyTo(destination);
                break;

            case BlendMode.Copy or BlendMode.Transparent:
                CopyOverColorKey(source, destination);
                break;

            case BlendMode.Average:
                BlendRowOverKey(source, destination, new PixelBlender.AverageKernel(Blender));
                break;

            case BlendMode.Screen:
                BlendRowOverKey(source, destination, new PixelBlender.ScreenKernel(Blender));
                break;

            case BlendMode.Alpha or BlendMode.AlphaPlane:
                BlendRowOverKey(source, destination, new PixelBlender.AlphaKernel(Blender, State.AlphaWeight));
                break;

            case BlendMode.Additive:
                BlendRowOverKey(source, destination, new PixelBlender.AdditiveKernel(Blender));
                break;

            case BlendMode.SourceWeighted:
                BlendRowOverKey(source, destination, new PixelBlender.SourceWeightedKernel(Blender));
                break;

            case BlendMode.Shade:
                BlendRowOverKey(source, destination, new PixelBlender.ShadeKernel(Blender));
                break;

            case BlendMode.Darken:
                BlendRowOverKey(source, destination, new PixelBlender.DarkenKernel(Blender));
                break;

            case BlendMode.Veil:
                BlendRowOverKey(source, destination, new PixelBlender.VeilKernel(Blender));
                break;

            default:
                CopyOverColorKey(source, destination);
                break;
        }
    }

    private void BlendRowTintedOverKey(ReadOnlySpan<ushort> source, Span<ushort> destination, BlendMode mode, ushort color)
    {
        switch (mode)
        {
            case BlendMode.Copy or BlendMode.Transparent:
                BlendRowOverKey(source, destination, new PixelBlender.TintedKernel<PixelBlender.CopyKernel>(Blender, color, default));
                break;

            case BlendMode.Average:
                BlendRowOverKey(source, destination, new PixelBlender.TintedKernel<PixelBlender.AverageKernel>(Blender, color, new(Blender)));
                break;

            case BlendMode.Screen:
                BlendRowOverKey(source, destination, new PixelBlender.TintedKernel<PixelBlender.ScreenKernel>(Blender, color, new(Blender)));
                break;

            case BlendMode.Alpha or BlendMode.AlphaPlane:
                BlendRowOverKey(
                    source,
                    destination,
                    new PixelBlender.TintedKernel<PixelBlender.AlphaKernel>(Blender, color, new(Blender, State.AlphaWeight)));
                break;

            case BlendMode.Additive:
                BlendRowOverKey(source, destination, new PixelBlender.TintedKernel<PixelBlender.AdditiveKernel>(Blender, color, new(Blender)));
                break;

            case BlendMode.SourceWeighted:
                BlendRowOverKey(
                    source, destination, new PixelBlender.TintedKernel<PixelBlender.SourceWeightedKernel>(Blender, color, new(Blender)));
                break;

            case BlendMode.Shade:
                BlendRowOverKey(source, destination, new PixelBlender.TintedKernel<PixelBlender.ShadeKernel>(Blender, color, new(Blender)));
                break;

            case BlendMode.Darken:
                BlendRowOverKey(source, destination, new PixelBlender.TintedKernel<PixelBlender.DarkenKernel>(Blender, color, new(Blender)));
                break;

            case BlendMode.Veil:
                BlendRowOverKey(source, destination, new PixelBlender.TintedKernel<PixelBlender.VeilKernel>(Blender, color, new(Blender)));
                break;

            default:
                BlendRowOverKey(source, destination, new PixelBlender.TintedKernel<PixelBlender.CopyKernel>(Blender, color, default));
                break;
        }
    }

    private void BlendRowOverMask(
        ReadOnlySpan<ushort> source, ReadOnlySpan<byte> coverage, Span<ushort> destination, BlendMode mode)
    {
        switch (mode)
        {
            case BlendMode.Copy or BlendMode.Transparent:
                BlendRowOverMask(source, coverage, destination, default(PixelBlender.CopyKernel));
                break;

            case BlendMode.Average:
                BlendRowOverMask(source, coverage, destination, new PixelBlender.AverageKernel(Blender));
                break;

            case BlendMode.Screen:
                BlendRowOverMask(source, coverage, destination, new PixelBlender.ScreenKernel(Blender));
                break;

            case BlendMode.Alpha or BlendMode.AlphaPlane:
                BlendRowOverMask(source, coverage, destination, new PixelBlender.AlphaKernel(Blender, State.AlphaWeight));
                break;

            case BlendMode.Additive:
                BlendRowOverMask(source, coverage, destination, new PixelBlender.AdditiveKernel(Blender));
                break;

            case BlendMode.SourceWeighted:
                BlendRowOverMask(source, coverage, destination, new PixelBlender.SourceWeightedKernel(Blender));
                break;

            case BlendMode.Shade:
                BlendRowOverMask(source, coverage, destination, new PixelBlender.ShadeKernel(Blender));
                break;

            case BlendMode.Darken:
                BlendRowOverMask(source, coverage, destination, new PixelBlender.DarkenKernel(Blender));
                break;

            case BlendMode.Veil:
                BlendRowOverMask(source, coverage, destination, new PixelBlender.VeilKernel(Blender));
                break;

            default:
                BlendRowOverMask(source, coverage, destination, default(PixelBlender.CopyKernel));
                break;
        }
    }

    private static void BlendRowOverKey<TKernel>(ReadOnlySpan<ushort> source, Span<ushort> destination, TKernel kernel)
        where TKernel : struct, PixelBlender.IPixelKernel
    {
        for (var i = 0; i < destination.Length; i++)
        {
            if (source[i] != 0)
            {
                destination[i] = kernel.Apply(source[i], destination[i]);
            }
        }
    }

    private static void BlendRowOverMask<TKernel>(
        ReadOnlySpan<ushort> source, ReadOnlySpan<byte> coverage, Span<ushort> destination, TKernel kernel)
        where TKernel : struct, PixelBlender.IPixelKernel
    {
        for (var i = 0; i < destination.Length; i++)
        {
            if (coverage[i] != 0)
            {
                destination[i] = kernel.Apply(source[i], destination[i]);
            }
        }
    }

    public bool TryResolveRows(in PixelRect local, out CanvasRows rows)
    {
        if (!TryResolve(in local, out var target))
        {
            rows = default;
            return false;
        }

        rows = new CanvasRows(
            target.Surface.Pixels, target.Surface.Pitch, target.Area, target.TrimX, target.TrimY, target.OriginX, target.OriginY);
        return true;
    }

    public readonly ref struct CanvasRows
    {
        private readonly Span<ushort> _pixels;
        private readonly int _pitch;
        private readonly PixelRect _area;

        internal CanvasRows(
            Span<ushort> pixels, int pitch, scoped in PixelRect area, int trimX, int trimY, int originX, int originY)
        {
            _pixels = pixels;
            _pitch = pitch;
            _area = area;
            TrimX = trimX;
            TrimY = trimY;
            OriginX = originX;
            OriginY = originY;
        }

        public int Width => _area.Width;

        public int Height => _area.Height;

        public int TrimX { get; }

        public int TrimY { get; }

        public int OriginX { get; }

        public int OriginY { get; }

        public int AbsoluteX => _area.X;

        public int AbsoluteY => _area.Y;

        public Span<ushort> Row(int y) => _pixels.Slice(((_area.Y + y) * _pitch) + _area.X, _area.Width);
    }

    private bool TryResolve(in PixelRect local, out DrawTarget target)
    {
        var surface = ResolveBacking(out var originX, out var originY);
        var clip = AbsoluteClip(originX, originY);
        var requested = local.Translate(originX, originY);
        var area = requested.Intersect(clip).Intersect(new PixelRect(0, 0, surface.Width, surface.Height));

        target = new DrawTarget(surface, area, area.IsEmpty ? 0 : area.X - requested.X, area.IsEmpty ? 0 : area.Y - requested.Y, originX, originY);
        return !area.IsEmpty;
    }

    private PixelSurface ResolveBacking(out int originX, out int originY)
    {
        originX = 0;
        originY = 0;

        var canvas = this;
        while (canvas._parent is { } parent)
        {
            originX += canvas._originX;
            originY += canvas._originY;
            canvas = parent;
        }

        return canvas._own!;
    }

    private PixelRect AbsoluteClip(int originX, int originY)
    {
        var canvas = this;
        var clip = canvas._clip.Translate(originX, originY);

        while (canvas._parent is { } parent)
        {
            originX -= canvas._originX;
            originY -= canvas._originY;
            canvas = parent;
            clip = clip.Intersect(canvas._clip.Translate(originX, originY));

            if (clip.IsEmpty)
            {
                break;
            }
        }

        return clip;
    }

    private readonly struct DrawTarget(PixelSurface surface, PixelRect area, int trimX, int trimY, int originX, int originY)
    {
        public PixelSurface Surface { get; } = surface;

        public PixelRect Area { get; } = area;

        public int TrimX { get; } = trimX;

        public int TrimY { get; } = trimY;

        public int OriginX { get; } = originX;

        public int OriginY { get; } = originY;
    }
}
