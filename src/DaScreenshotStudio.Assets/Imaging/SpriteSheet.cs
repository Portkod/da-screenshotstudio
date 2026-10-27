namespace DaScreenshotStudio.Assets.Imaging;

public readonly struct SpriteSheetFrame : IEquatable<SpriteSheetFrame>
{
    public const int NoAnchor = -1;

    internal SpriteSheetFrame(int left, int top, int width, int height, int anchorRow)
    {
        Left = left;
        Top = top;
        Width = width;
        Height = height;
        AnchorRow = anchorRow;
    }

    public int Left { get; }

    public int Top { get; }

    public int Width { get; }

    public int Height { get; }

    public int AnchorRow { get; }

    public bool Equals(SpriteSheetFrame other) =>
        Left == other.Left && Top == other.Top && Width == other.Width
        && Height == other.Height && AnchorRow == other.AnchorRow;

    public override bool Equals(object? obj) => obj is SpriteSheetFrame other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Left, Top, Width, Height, AnchorRow);

    public static bool operator ==(SpriteSheetFrame left, SpriteSheetFrame right) => left.Equals(right);

    public static bool operator !=(SpriteSheetFrame left, SpriteSheetFrame right) => !left.Equals(right);
}

public sealed class SpriteSheet
{
    public const byte Separator = 0xFF;

    public const byte AnchorMarker = 0xFE;

    public const byte FirstDrawn = 2;

    public const byte LastDrawn = 0xF7;

    public static bool IsReserved(byte index) => index < FirstDrawn || index > LastDrawn;

    private readonly SpriteSheetFrame[] _frames;
    private readonly byte[] _indexes;

    private SpriteSheet(
        int width,
        int height,
        int stride,
        int cellWidth,
        int cellHeight,
        int columns,
        int rows,
        SpriteSheetFrame[] frames,
        byte[] indexes)
    {
        Width = width;
        Height = height;
        Stride = stride;
        CellWidth = cellWidth;
        CellHeight = cellHeight;
        Columns = columns;
        Rows = rows;
        _frames = frames;
        _indexes = indexes;
    }

    public int Width { get; }

    public int Height { get; }

    public int Stride { get; }

    public int CellWidth { get; }

    public int CellHeight { get; }

    public int Columns { get; }

    public int Rows { get; }

    public int FrameCount => _frames.Length;

    public ReadOnlySpan<byte> Indexes => _indexes;

    public SpriteSheetFrame this[int index] => _frames[index];

    public static SpriteSheet Cut(PcxImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        return Cut(image.Decode(), image.Width, image.Height, image.BytesPerLine);
    }

    public static SpriteSheet Cut(byte[] indexes, int width, int height, int stride)
    {
        ArgumentNullException.ThrowIfNull(indexes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfLessThan(stride, width);

        if (indexes.Length < stride * height)
        {
            throw new InvalidDataException(
                $"A {width} by {height} sheet of stride {stride} needs {stride * height} indexes; got {indexes.Length}.");
        }

        if (indexes[0] == Separator)
        {
            var whole = new[] { new SpriteSheetFrame(0, 0, width, height, SpriteSheetFrame.NoAnchor) };
            return new SpriteSheet(width, height, stride, width, height, 1, 1, whole, indexes);
        }

        var cellWidth = FirstSeparatorAcross(indexes, width, stride);
        var cellHeight = FirstSeparatorDown(indexes, height, stride);

        if (cellWidth <= 0 || cellHeight <= 0)
        {
            throw new InvalidDataException(
                "A sheet whose first pixel is not the separator has to carry one along row zero and down column zero.");
        }

        var columns = width / (cellWidth + 1);
        var rows = height / (cellHeight + 1);

        if (columns <= 0 || rows <= 0)
        {
            throw new InvalidDataException(
                $"A {width} by {height} sheet cannot hold a {cellWidth} by {cellHeight} cell and its separator.");
        }

        var frames = new SpriteSheetFrame[columns * rows];
        for (var frame = 0; frame < frames.Length; frame++)
        {
            var left = (cellWidth + 1) * (frame % columns);
            var top = (cellHeight + 1) * (frame / columns);
            frames[frame] = new SpriteSheetFrame(
                left, top, cellWidth, cellHeight, AnchorRow(indexes, stride, left, top, cellWidth, cellHeight));
        }

        return new SpriteSheet(width, height, stride, cellWidth, cellHeight, columns, rows, frames, indexes);
    }

    public ReadOnlySpan<byte> Row(int frame, int row)
    {
        var cell = _frames[frame];
        ArgumentOutOfRangeException.ThrowIfNegative(row);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(row, cell.Height);

        return Indexes.Slice(((cell.Top + row) * Stride) + cell.Left, cell.Width);
    }

    private static int FirstSeparatorAcross(ReadOnlySpan<byte> indexes, int width, int stride)
    {
        var row = indexes[..Math.Min(width, stride)];
        var found = row.IndexOf(Separator);
        return found < 0 ? -1 : found;
    }

    private static int FirstSeparatorDown(ReadOnlySpan<byte> indexes, int height, int stride)
    {
        for (var y = 0; y < height; y++)
        {
            if (indexes[y * stride] == Separator)
            {
                return y;
            }
        }

        return -1;
    }

    private static int AnchorRow(
        ReadOnlySpan<byte> indexes, int stride, int left, int top, int cellWidth, int cellHeight)
    {
        for (var y = 0; y < cellHeight; y++)
        {
            if (indexes[((top + y) * stride) + left] == AnchorMarker)
            {
                return y;
            }
        }

        for (var y = 0; y < cellHeight; y++)
        {
            if (indexes[((top + y) * stride) + left + cellWidth - 1] == AnchorMarker)
            {
                return y;
            }
        }

        return SpriteSheetFrame.NoAnchor;
    }
}
