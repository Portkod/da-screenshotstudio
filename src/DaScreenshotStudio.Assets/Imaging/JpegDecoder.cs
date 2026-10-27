namespace DaScreenshotStudio.Assets.Imaging;

public static class JpegDecoder
{
    public const int MaxDimension = 65500;

    private const int BlockSide = 8;
    private const int BlockCells = BlockSide * BlockSide;

    private static ReadOnlySpan<byte> NaturalOrder =>
    [
         0,  1,  8, 16,  9,  2,  3, 10,
        17, 24, 32, 25, 18, 11,  4,  5,
        12, 19, 26, 33, 40, 48, 41, 34,
        27, 20, 13,  6,  7, 14, 21, 28,
        35, 42, 49, 56, 57, 50, 43, 36,
        29, 22, 15, 23, 30, 37, 44, 51,
        58, 59, 52, 45, 38, 31, 39, 46,
        53, 60, 61, 54, 47, 55, 62, 63,
    ];

    private const int ConstBits = 13;
    private const int PassOneBits = 2;

    private const int RangeMask = 0x3FF;

    private const int Fix0_298631336 = 2446;
    private const int Fix0_390180644 = 3196;
    private const int Fix0_541196100 = 4433;
    private const int Fix0_765366865 = 6270;
    private const int Fix0_899976223 = 7373;
    private const int Fix1_175875602 = 9633;
    private const int Fix1_501321110 = 12299;
    private const int Fix1_847759065 = 15137;
    private const int Fix1_961570560 = 16069;
    private const int Fix2_053119869 = 16819;
    private const int Fix2_562915447 = 20995;
    private const int Fix3_072711026 = 25172;

    private const int ColorScaleBits = 16;
    private const int ColorOneHalf = 1 << (ColorScaleBits - 1);

    private const int FixCrToRed = 91881;
    private const int FixCbToBlue = 116130;
    private const int FixCrToGreen = 46802;
    private const int FixCbToGreen = 22554;

    private const int SampleLimit = 256;

    private const int IdctLimit = SampleLimit + 128;

    private static readonly byte[] RangeLimitTable = BuildRangeLimitTable();
    private static readonly int[] CrToRed = BuildCrToRed();
    private static readonly int[] CbToBlue = BuildCbToBlue();
    private static readonly int[] CrToGreen = BuildCrToGreen();
    private static readonly int[] CbToGreen = BuildCbToGreen();

    private enum Upsampler
    {
        Fullsize,

        WideBox,

        WideFancy,

        QuarterBox,

        QuarterFancy,
    }

    private enum JpegColorSpace
    {
        Grayscale,
        YCbCr,
        Rgb,
    }

    public static bool TryDecode(
        ReadOnlySpan<byte> file,
        int expectedWidth,
        int expectedHeight,
        out byte[] rgb,
        out int width,
        out int height)
    {
        rgb = [];
        width = 0;
        height = 0;

        var decoder = new Decoder(file, expectedWidth, expectedHeight);
        if (!decoder.TryReadFile() || !decoder.TryConvert(out rgb))
        {
            rgb = [];
            return false;
        }

        width = decoder.Width;
        height = decoder.Height;
        return true;
    }

    private static int CeilDiv(int value, int divisor) => (value + divisor - 1) / divisor;

    private static int Descale(int value, int shift) => (value + (1 << (shift - 1))) >> shift;

    private static byte Limit(int value) => RangeLimitTable[SampleLimit + value];

    private static byte[] BuildRangeLimitTable()
    {
        var table = new byte[(5 * 256) + 128];

        for (var i = 0; i <= 255; i++)
        {
            table[SampleLimit + i] = (byte)i;
        }

        for (var i = 128; i < 512; i++)
        {
            table[IdctLimit + i] = 255;
        }

        for (var i = 0; i < 128; i++)
        {
            table[IdctLimit + 1024 - 128 + i] = table[SampleLimit + i];
        }

        return table;
    }

    private static int[] BuildCrToRed()
    {
        var table = new int[256];
        for (var i = 0; i < 256; i++)
        {
            table[i] = ((FixCrToRed * (i - 128)) + ColorOneHalf) >> ColorScaleBits;
        }

        return table;
    }

    private static int[] BuildCbToBlue()
    {
        var table = new int[256];
        for (var i = 0; i < 256; i++)
        {
            table[i] = ((FixCbToBlue * (i - 128)) + ColorOneHalf) >> ColorScaleBits;
        }

        return table;
    }

    private static int[] BuildCrToGreen()
    {
        var table = new int[256];
        for (var i = 0; i < 256; i++)
        {
            table[i] = -FixCrToGreen * (i - 128);
        }

        return table;
    }

    private static int[] BuildCbToGreen()
    {
        var table = new int[256];
        for (var i = 0; i < 256; i++)
        {
            table[i] = (-FixCbToGreen * (i - 128)) + ColorOneHalf;
        }

        return table;
    }

    private sealed class HuffmanTable
    {
        public readonly int[] MinCode = new int[17];
        public readonly int[] MaxCode = new int[18];
        public readonly int[] ValuePointer = new int[17];
        public readonly byte[] Values = new byte[256];
    }

    private sealed class Component
    {
        public int Id;
        public int Horizontal;
        public int Vertical;
        public int QuantizerIndex;
        public int DcTableIndex;
        public int AcTableIndex;
        public int Predictor;
        public int WidthInBlocks;
        public int HeightInBlocks;
        public int DownsampledWidth;
        public int DownsampledHeight;
        public int Stride;
        public Upsampler Upsampler;
        public byte[] Plane = [];
    }

    private ref struct Decoder
    {
        private readonly ReadOnlySpan<byte> _file;
        private readonly int[] _quantizers;
        private readonly bool[] _quantizerPresent;
        private readonly HuffmanTable?[] _dcTables;
        private readonly HuffmanTable?[] _acTables;
        private readonly int[] _coefficients;
        private readonly int[] _workspace;

        private Component[] _components;
        private int[] _scanComponents;
        private int _scanCount;

        private int _at;
        private int _restartInterval;
        private int _maxHorizontal;
        private int _maxVertical;
        private bool _sawJfif;
        private bool _sawAdobe;
        private int _adobeTransform;
        private bool _frameSeen;
        private bool _scanSeen;

        private long _bits;
        private int _bitCount;
        private int _scanAt;
        private bool _markerHit;
        private int _markerAt;

        public int Width;
        public int Height;

        private readonly int _expectedWidth;
        private readonly int _expectedHeight;

        public Decoder(ReadOnlySpan<byte> file, int expectedWidth, int expectedHeight)
        {
            _file = file;
            _expectedWidth = expectedWidth;
            _expectedHeight = expectedHeight;
            _quantizers = new int[4 * BlockCells];
            _quantizerPresent = new bool[4];
            _dcTables = new HuffmanTable?[4];
            _acTables = new HuffmanTable?[4];
            _coefficients = new int[BlockCells];
            _workspace = new int[BlockCells];
            _components = [];
            _scanComponents = [];
            _markerAt = file.Length;
        }

        public bool TryReadFile()
        {
            if (_file.Length < 2 || _file[0] != 0xFF || _file[1] != 0xD8)
            {
                return false;
            }

            _at = 2;

            while (true)
            {
                if (!TryNextMarker(out var marker))
                {
                    return _scanSeen;
                }

                switch (marker)
                {
                    case 0xD8:
                        break;

                    case 0xD9:
                        return _scanSeen;

                    case 0xC0:
                        if (_frameSeen || !TryReadFrame())
                        {
                            return false;
                        }

                        break;

                    case 0xC4:
                        if (!TryReadHuffmanTables())
                        {
                            return false;
                        }

                        break;

                    case 0xDB:
                        if (!TryReadQuantizers())
                        {
                            return false;
                        }

                        break;

                    case 0xDD:
                        if (!TryReadRestartInterval())
                        {
                            return false;
                        }

                        break;

                    case 0xDA:
                        if (!TryReadScanHeader() || !TryDecodeScan())
                        {
                            return false;
                        }

                        break;

                    case 0x01:
                    case >= 0xD0 and <= 0xD7:
                        break;

                    case >= 0xC1 and <= 0xCF:
                        return false;

                    default:
                        if (!TrySegment(out var body))
                        {
                            return _scanSeen;
                        }

                        ReadApplicationSegment(marker, body);
                        break;
                }
            }
        }

        public bool TryConvert(out byte[] rgb)
        {
            rgb = [];

            if (!_frameSeen || !_scanSeen)
            {
                return false;
            }

            var total = (long)Width * Height * 3;
            if (total > int.MaxValue)
            {
                return false;
            }

            if (!TryChooseColorSpace(out var space))
            {
                return false;
            }

            var output = new byte[(int)total];
            var rows = new byte[_components.Length][];
            for (var i = 0; i < rows.Length; i++)
            {
                rows[i] = new byte[Width + 2];
            }

            var at = 0;
            for (var y = 0; y < Height; y++)
            {
                for (var i = 0; i < _components.Length; i++)
                {
                    UpsampleRow(_components[i], y, rows[i]);
                }

                switch (space)
                {
                    case JpegColorSpace.YCbCr:
                        ConvertYccToRgb(rows[0], rows[1], rows[2], output, at);
                        break;

                    case JpegColorSpace.Rgb:
                        ConvertRgbToRgb(rows[0], rows[1], rows[2], output, at);
                        break;

                    default:
                        ConvertGrayToRgb(rows[0], output, at);
                        break;
                }

                at += Width * 3;
            }

            rgb = output;
            return true;
        }

        private bool TryChooseColorSpace(out JpegColorSpace space)
        {
            space = JpegColorSpace.Grayscale;

            if (_components.Length == 1)
            {
                return true;
            }

            if (_components.Length != 3)
            {
                return false;
            }

            if (_sawJfif)
            {
                space = JpegColorSpace.YCbCr;
                return true;
            }

            if (_sawAdobe)
            {
                space = _adobeTransform == 0 ? JpegColorSpace.Rgb : JpegColorSpace.YCbCr;
                return true;
            }

            space = _components[0].Id == 82 && _components[1].Id == 71 && _components[2].Id == 66
                ? JpegColorSpace.Rgb
                : JpegColorSpace.YCbCr;
            return true;
        }

        private void ConvertYccToRgb(byte[] luma, byte[] blue, byte[] red, byte[] output, int at)
        {
            for (var x = 0; x < Width; x++)
            {
                int y = luma[x];
                int cb = blue[x];
                int cr = red[x];

                output[at] = Limit(y + CrToRed[cr]);
                output[at + 1] = Limit(y + ((CbToGreen[cb] + CrToGreen[cr]) >> ColorScaleBits));
                output[at + 2] = Limit(y + CbToBlue[cb]);
                at += 3;
            }
        }

        private void ConvertRgbToRgb(byte[] red, byte[] green, byte[] blue, byte[] output, int at)
        {
            for (var x = 0; x < Width; x++)
            {
                output[at] = red[x];
                output[at + 1] = green[x];
                output[at + 2] = blue[x];
                at += 3;
            }
        }

        private void ConvertGrayToRgb(byte[] gray, byte[] output, int at)
        {
            for (var x = 0; x < Width; x++)
            {
                var value = gray[x];
                output[at] = value;
                output[at + 1] = value;
                output[at + 2] = value;
                at += 3;
            }
        }

        private void UpsampleRow(Component component, int y, byte[] destination)
        {
            var plane = component.Plane;
            var stride = component.Stride;
            var last = component.DownsampledWidth - 1;

            switch (component.Upsampler)
            {
                case Upsampler.Fullsize:
                {
                    Array.Copy(plane, y * stride, destination, 0, Width);
                    return;
                }

                case Upsampler.WideBox:
                {
                    var source = y * stride;
                    for (var x = 0; x < Width; x++)
                    {
                        destination[x] = plane[source + (x >> 1)];
                    }

                    return;
                }

                case Upsampler.QuarterBox:
                {
                    var source = (y >> 1) * stride;
                    for (var x = 0; x < Width; x++)
                    {
                        destination[x] = plane[source + (x >> 1)];
                    }

                    return;
                }

                case Upsampler.WideFancy:
                {
                    var source = y * stride;

                    destination[0] = plane[source];
                    destination[1] = (byte)(((plane[source] * 3) + plane[source + 1] + 2) >> 2);

                    for (var i = 1; i < last; i++)
                    {
                        var center = plane[source + i] * 3;
                        destination[i * 2] = (byte)((center + plane[source + i - 1] + 1) >> 2);
                        destination[(i * 2) + 1] = (byte)((center + plane[source + i + 1] + 2) >> 2);
                    }

                    destination[last * 2] = (byte)(((plane[source + last] * 3) + plane[source + last - 1] + 1) >> 2);
                    destination[(last * 2) + 1] = plane[source + last];
                    return;
                }

                default:
                {
                    var row = y >> 1;
                    var near = row * stride;
                    var far = Math.Clamp((y & 1) == 0 ? row - 1 : row + 1, 0, component.DownsampledHeight - 1)
                        * stride;

                    var thisSum = (plane[near] * 3) + plane[far];
                    var nextSum = (plane[near + 1] * 3) + plane[far + 1];
                    destination[0] = (byte)(((thisSum * 4) + 8) >> 4);
                    destination[1] = (byte)(((thisSum * 3) + nextSum + 7) >> 4);

                    var lastSum = thisSum;
                    thisSum = nextSum;

                    for (var i = 1; i < last; i++)
                    {
                        nextSum = (plane[near + i + 1] * 3) + plane[far + i + 1];
                        destination[i * 2] = (byte)(((thisSum * 3) + lastSum + 8) >> 4);
                        destination[(i * 2) + 1] = (byte)(((thisSum * 3) + nextSum + 7) >> 4);
                        lastSum = thisSum;
                        thisSum = nextSum;
                    }

                    destination[last * 2] = (byte)(((thisSum * 3) + lastSum + 8) >> 4);
                    destination[(last * 2) + 1] = (byte)(((thisSum * 4) + 7) >> 4);
                    return;
                }
            }
        }

        private bool TryNextMarker(out int marker)
        {
            while (true)
            {
                while (_at < _file.Length && _file[_at] != 0xFF)
                {
                    _at++;
                }

                while (_at < _file.Length && _file[_at] == 0xFF)
                {
                    _at++;
                }

                if (_at >= _file.Length)
                {
                    marker = 0;
                    return false;
                }

                marker = _file[_at++];

                if (marker != 0)
                {
                    return true;
                }
            }
        }

        private bool TrySegment(out ReadOnlySpan<byte> body)
        {
            body = default;

            if (_at + 2 > _file.Length)
            {
                return false;
            }

            var length = (_file[_at] << 8) | _file[_at + 1];
            if (length < 2 || _at + length > _file.Length)
            {
                return false;
            }

            body = _file.Slice(_at + 2, length - 2);
            _at += length;
            return true;
        }

        private void ReadApplicationSegment(int marker, ReadOnlySpan<byte> body)
        {
            if (marker == 0xE0 && body.Length >= 14 && body[..5].SequenceEqual("JFIF\0"u8))
            {
                _sawJfif = true;
                return;
            }

            if (marker == 0xEE && body.Length >= 12 && body[..5].SequenceEqual("Adobe"u8))
            {
                _sawAdobe = true;
                _adobeTransform = body[11];
            }
        }

        private bool TryReadQuantizers()
        {
            if (!TrySegment(out var body))
            {
                return false;
            }

            var at = 0;
            while (at < body.Length)
            {
                var header = body[at++];
                var index = header & 0x0F;
                var sixteenBit = (header >> 4) != 0;
                if (index > 3 || (header >> 4) > 1)
                {
                    return false;
                }

                var needed = sixteenBit ? BlockCells * 2 : BlockCells;
                if (at + needed > body.Length)
                {
                    return false;
                }

                var destination = index * BlockCells;
                for (var i = 0; i < BlockCells; i++)
                {
                    int value;
                    if (sixteenBit)
                    {
                        value = (body[at] << 8) | body[at + 1];
                        at += 2;
                    }
                    else
                    {
                        value = body[at++];
                    }

                    _quantizers[destination + NaturalOrder[i]] = value;
                }

                _quantizerPresent[index] = true;
            }

            return true;
        }

        private bool TryReadHuffmanTables()
        {
            if (!TrySegment(out var body))
            {
                return false;
            }

            Span<int> counts = stackalloc int[17];
            var at = 0;
            while (at < body.Length)
            {
                var header = body[at++];
                var index = header & 0x0F;
                var alternating = (header & 0x10) != 0;
                if (index > 3 || (header & ~0x1F) != 0)
                {
                    return false;
                }

                if (at + 16 > body.Length)
                {
                    return false;
                }

                var total = 0;
                for (var length = 1; length <= 16; length++)
                {
                    counts[length] = body[at++];
                    total += counts[length];
                }

                if (total > 256 || at + total > body.Length)
                {
                    return false;
                }

                var table = new HuffmanTable();
                for (var i = 0; i < total; i++)
                {
                    table.Values[i] = body[at++];
                }

                var code = 0;
                var position = 0;
                for (var length = 1; length <= 16; length++)
                {
                    if (counts[length] > 0)
                    {
                        table.ValuePointer[length] = position;
                        table.MinCode[length] = code;
                        position += counts[length];
                        code += counts[length];
                        table.MaxCode[length] = code - 1;
                    }
                    else
                    {
                        table.MaxCode[length] = -1;
                    }

                    if (code >= 1 << length)
                    {
                        return false;
                    }

                    code <<= 1;
                }

                table.MaxCode[17] = 0xFFFFF;

                if (alternating)
                {
                    _acTables[index] = table;
                }
                else
                {
                    _dcTables[index] = table;
                }
            }

            return true;
        }

        private bool TryReadRestartInterval()
        {
            if (!TrySegment(out var body) || body.Length != 2)
            {
                return false;
            }

            _restartInterval = (body[0] << 8) | body[1];
            return true;
        }

        private bool TryReadFrame()
        {
            if (!TrySegment(out var body) || body.Length < 6)
            {
                return false;
            }

            if (body[0] != 8)
            {
                return false;
            }

            Height = (body[1] << 8) | body[2];
            Width = (body[3] << 8) | body[4];
            int count = body[5];

            if (Width <= 0 || Height <= 0 || Width > MaxDimension || Height > MaxDimension)
            {
                return false;
            }

            if ((_expectedWidth > 0 && Width != _expectedWidth)
                || (_expectedHeight > 0 && Height != _expectedHeight))
            {
                return false;
            }

            if (count is not (1 or 3) || body.Length != 6 + (count * 3))
            {
                return false;
            }

            _components = new Component[count];
            _maxHorizontal = 1;
            _maxVertical = 1;

            for (var i = 0; i < count; i++)
            {
                var at = 6 + (i * 3);
                var sampling = body[at + 1];
                var component = new Component
                {
                    Id = body[at],
                    Horizontal = sampling >> 4,
                    Vertical = sampling & 0x0F,
                    QuantizerIndex = body[at + 2],
                };

                if (component.Horizontal is < 1 or > 4 || component.Vertical is < 1 or > 4)
                {
                    return false;
                }

                if (component.QuantizerIndex > 3)
                {
                    return false;
                }

                _components[i] = component;
                _maxHorizontal = Math.Max(_maxHorizontal, component.Horizontal);
                _maxVertical = Math.Max(_maxVertical, component.Vertical);
            }

            _scanComponents = new int[4];
            _frameSeen = true;
            return TrySizeComponents();
        }

        private bool TrySizeComponents()
        {
            var mcusAcross = CeilDiv(Width, _maxHorizontal * BlockSide);
            var mcuRows = CeilDiv(Height, _maxVertical * BlockSide);

            foreach (var component in _components)
            {
                component.DownsampledWidth = CeilDiv(Width * component.Horizontal, _maxHorizontal);
                component.DownsampledHeight = CeilDiv(Height * component.Vertical, _maxVertical);
                component.WidthInBlocks = CeilDiv(component.DownsampledWidth, BlockSide);
                component.HeightInBlocks = CeilDiv(component.DownsampledHeight, BlockSide);

                if (!TryChooseUpsampler(component))
                {
                    return false;
                }

                var stride = Math.Max(component.WidthInBlocks, mcusAcross * component.Horizontal) * BlockSide;
                var rows = Math.Max(component.HeightInBlocks, mcuRows * component.Vertical) * BlockSide;

                var cells = (long)stride * rows;
                if (cells > int.MaxValue)
                {
                    return false;
                }

                component.Stride = stride;
                component.Plane = new byte[(int)cells];
            }

            return true;
        }

        private bool TryChooseUpsampler(Component component)
        {
            if (component.Horizontal == _maxHorizontal && component.Vertical == _maxVertical)
            {
                component.Upsampler = Upsampler.Fullsize;
                return true;
            }

            var fancy = component.DownsampledWidth > 2;

            if (component.Horizontal * 2 == _maxHorizontal && component.Vertical == _maxVertical)
            {
                component.Upsampler = fancy ? Upsampler.WideFancy : Upsampler.WideBox;
                return true;
            }

            if (component.Horizontal * 2 == _maxHorizontal && component.Vertical * 2 == _maxVertical)
            {
                component.Upsampler = fancy ? Upsampler.QuarterFancy : Upsampler.QuarterBox;
                return true;
            }

            return false;
        }

        private bool TryReadScanHeader()
        {
            if (!TrySegment(out var body) || !_frameSeen || body.Length < 1)
            {
                return false;
            }

            int count = body[0];
            if (count is < 1 or > 4 || body.Length != (count * 2) + 4)
            {
                return false;
            }

            for (var i = 0; i < count; i++)
            {
                var id = body[1 + (i * 2)];
                var tables = body[2 + (i * 2)];

                var found = -1;
                for (var c = 0; c < _components.Length; c++)
                {
                    if (_components[c].Id == id)
                    {
                        found = c;
                        break;
                    }
                }

                if (found < 0)
                {
                    return false;
                }

                var component = _components[found];
                component.DcTableIndex = tables >> 4;
                component.AcTableIndex = tables & 0x0F;

                if (component.DcTableIndex > 3 || component.AcTableIndex > 3)
                {
                    return false;
                }

                if (_dcTables[component.DcTableIndex] is null || _acTables[component.AcTableIndex] is null)
                {
                    return false;
                }

                if (!_quantizerPresent[component.QuantizerIndex])
                {
                    return false;
                }

                _scanComponents[i] = found;
            }

            if (body[(count * 2) + 1] != 0 || body[(count * 2) + 2] != 63 || body[(count * 2) + 3] != 0)
            {
                return false;
            }

            _scanCount = count;
            _scanAt = _at;
            _bits = 0;
            _bitCount = 0;
            _markerHit = false;
            _markerAt = _file.Length;
            return true;
        }

        private bool TryDecodeScan()
        {
            int mcusAcross;
            int mcuRows;

            if (_scanCount == 1)
            {
                var only = _components[_scanComponents[0]];
                mcusAcross = only.WidthInBlocks;
                mcuRows = only.HeightInBlocks;
            }
            else
            {
                mcusAcross = CeilDiv(Width, _maxHorizontal * BlockSide);
                mcuRows = CeilDiv(Height, _maxVertical * BlockSide);
            }

            for (var i = 0; i < _scanCount; i++)
            {
                _components[_scanComponents[i]].Predictor = 0;
            }

            var restartsToGo = _restartInterval;

            for (var mcuRow = 0; mcuRow < mcuRows; mcuRow++)
            {
                for (var mcuColumn = 0; mcuColumn < mcusAcross; mcuColumn++)
                {
                    if (_restartInterval != 0 && restartsToGo == 0)
                    {
                        ProcessRestart();
                        restartsToGo = _restartInterval;
                    }

                    if (_scanCount == 1)
                    {
                        var only = _components[_scanComponents[0]];
                        if (!TryDecodeBlock(only, mcuRow, mcuColumn))
                        {
                            return false;
                        }
                    }
                    else
                    {
                        for (var i = 0; i < _scanCount; i++)
                        {
                            var component = _components[_scanComponents[i]];
                            for (var v = 0; v < component.Vertical; v++)
                            {
                                for (var h = 0; h < component.Horizontal; h++)
                                {
                                    var row = (mcuRow * component.Vertical) + v;
                                    var column = (mcuColumn * component.Horizontal) + h;
                                    if (!TryDecodeBlock(component, row, column))
                                    {
                                        return false;
                                    }
                                }
                            }
                        }
                    }

                    restartsToGo--;
                }
            }

            _scanSeen = true;

            if (!_markerHit)
            {
                SkipToMarker();
            }

            _at = _markerAt;
            return true;
        }

        private bool TryDecodeBlock(Component component, int blockRow, int blockColumn)
        {
            var block = _coefficients;
            Array.Clear(block);

            var dcTable = _dcTables[component.DcTableIndex];
            var acTable = _acTables[component.AcTableIndex];
            if (dcTable is null || acTable is null)
            {
                return false;
            }

            var size = DecodeSymbol(dcTable);
            if (size > 15)
            {
                return false;
            }

            var difference = 0;
            if (size != 0)
            {
                difference = Extend(GetBits(size), size);
            }

            component.Predictor += difference;

            block[0] = (short)component.Predictor;

            for (var k = 1; k < BlockCells;)
            {
                var symbol = DecodeSymbol(acTable);
                var run = symbol >> 4;
                size = symbol & 0x0F;

                if (size != 0)
                {
                    k += run;
                    if (k >= BlockCells)
                    {
                        return false;
                    }

                    block[NaturalOrder[k]] = Extend(GetBits(size), size);
                    k++;
                }
                else
                {
                    if (run != 15)
                    {
                        break;
                    }

                    k += 16;
                }
            }

            InverseTransform(component, blockRow, blockColumn);
            return true;
        }

        private void InverseTransform(Component component, int blockRow, int blockColumn)
        {
            var block = _coefficients;
            var quantizer = component.QuantizerIndex * BlockCells;
            var workspace = _workspace;

            for (var column = 0; column < BlockSide; column++)
            {
                if (block[column + 8] == 0 && block[column + 16] == 0 && block[column + 24] == 0
                    && block[column + 32] == 0 && block[column + 40] == 0 && block[column + 48] == 0
                    && block[column + 56] == 0)
                {
                    var flat = (block[column] * _quantizers[quantizer + column]) << PassOneBits;
                    for (var row = 0; row < BlockSide; row++)
                    {
                        workspace[column + (row * 8)] = flat;
                    }

                    continue;
                }

                var z2 = block[column + 16] * _quantizers[quantizer + column + 16];
                var z3 = block[column + 48] * _quantizers[quantizer + column + 48];

                var z1 = (z2 + z3) * Fix0_541196100;
                var tmp2 = z1 + (z3 * -Fix1_847759065);
                var tmp3 = z1 + (z2 * Fix0_765366865);

                z2 = block[column] * _quantizers[quantizer + column];
                z3 = block[column + 32] * _quantizers[quantizer + column + 32];

                var tmp0 = (z2 + z3) << ConstBits;
                var tmp1 = (z2 - z3) << ConstBits;

                var tmp10 = tmp0 + tmp3;
                var tmp13 = tmp0 - tmp3;
                var tmp11 = tmp1 + tmp2;
                var tmp12 = tmp1 - tmp2;

                tmp0 = block[column + 56] * _quantizers[quantizer + column + 56];
                tmp1 = block[column + 40] * _quantizers[quantizer + column + 40];
                tmp2 = block[column + 24] * _quantizers[quantizer + column + 24];
                tmp3 = block[column + 8] * _quantizers[quantizer + column + 8];

                z1 = tmp0 + tmp3;
                z2 = tmp1 + tmp2;
                z3 = tmp0 + tmp2;
                var z4 = tmp1 + tmp3;
                var z5 = (z3 + z4) * Fix1_175875602;

                tmp0 *= Fix0_298631336;
                tmp1 *= Fix2_053119869;
                tmp2 *= Fix3_072711026;
                tmp3 *= Fix1_501321110;
                z1 *= -Fix0_899976223;
                z2 *= -Fix2_562915447;
                z3 *= -Fix1_961570560;
                z4 *= -Fix0_390180644;

                z3 += z5;
                z4 += z5;

                tmp0 += z1 + z3;
                tmp1 += z2 + z4;
                tmp2 += z2 + z3;
                tmp3 += z1 + z4;

                workspace[column] = Descale(tmp10 + tmp3, ConstBits - PassOneBits);
                workspace[column + 56] = Descale(tmp10 - tmp3, ConstBits - PassOneBits);
                workspace[column + 8] = Descale(tmp11 + tmp2, ConstBits - PassOneBits);
                workspace[column + 48] = Descale(tmp11 - tmp2, ConstBits - PassOneBits);
                workspace[column + 16] = Descale(tmp12 + tmp1, ConstBits - PassOneBits);
                workspace[column + 40] = Descale(tmp12 - tmp1, ConstBits - PassOneBits);
                workspace[column + 24] = Descale(tmp13 + tmp0, ConstBits - PassOneBits);
                workspace[column + 32] = Descale(tmp13 - tmp0, ConstBits - PassOneBits);
            }

            var plane = component.Plane;
            var stride = component.Stride;

            for (var row = 0; row < BlockSide; row++)
            {
                var p = row * 8;
                var target = (((blockRow * BlockSide) + row) * stride) + (blockColumn * BlockSide);

                if (workspace[p + 1] == 0 && workspace[p + 2] == 0 && workspace[p + 3] == 0
                    && workspace[p + 4] == 0 && workspace[p + 5] == 0 && workspace[p + 6] == 0
                    && workspace[p + 7] == 0)
                {
                    var flat = RangeLimitTable[IdctLimit + (Descale(workspace[p], PassOneBits + 3) & RangeMask)];
                    for (var column = 0; column < BlockSide; column++)
                    {
                        plane[target + column] = flat;
                    }

                    continue;
                }

                var z2 = workspace[p + 2];
                var z3 = workspace[p + 6];

                var z1 = (z2 + z3) * Fix0_541196100;
                var tmp2 = z1 + (z3 * -Fix1_847759065);
                var tmp3 = z1 + (z2 * Fix0_765366865);

                var tmp0 = (workspace[p] + workspace[p + 4]) << ConstBits;
                var tmp1 = (workspace[p] - workspace[p + 4]) << ConstBits;

                var tmp10 = tmp0 + tmp3;
                var tmp13 = tmp0 - tmp3;
                var tmp11 = tmp1 + tmp2;
                var tmp12 = tmp1 - tmp2;

                tmp0 = workspace[p + 7];
                tmp1 = workspace[p + 5];
                tmp2 = workspace[p + 3];
                tmp3 = workspace[p + 1];

                z1 = tmp0 + tmp3;
                z2 = tmp1 + tmp2;
                z3 = tmp0 + tmp2;
                var z4 = tmp1 + tmp3;
                var z5 = (z3 + z4) * Fix1_175875602;

                tmp0 *= Fix0_298631336;
                tmp1 *= Fix2_053119869;
                tmp2 *= Fix3_072711026;
                tmp3 *= Fix1_501321110;
                z1 *= -Fix0_899976223;
                z2 *= -Fix2_562915447;
                z3 *= -Fix1_961570560;
                z4 *= -Fix0_390180644;

                z3 += z5;
                z4 += z5;

                tmp0 += z1 + z3;
                tmp1 += z2 + z4;
                tmp2 += z2 + z3;
                tmp3 += z1 + z4;

                plane[target] = Clamped(tmp10 + tmp3);
                plane[target + 7] = Clamped(tmp10 - tmp3);
                plane[target + 1] = Clamped(tmp11 + tmp2);
                plane[target + 6] = Clamped(tmp11 - tmp2);
                plane[target + 2] = Clamped(tmp12 + tmp1);
                plane[target + 5] = Clamped(tmp12 - tmp1);
                plane[target + 3] = Clamped(tmp13 + tmp0);
                plane[target + 4] = Clamped(tmp13 - tmp0);
            }
        }

        private static byte Clamped(int value) =>
            RangeLimitTable[IdctLimit + (Descale(value, ConstBits + PassOneBits + 3) & RangeMask)];

        private static int Extend(int value, int size) =>
            value < 1 << (size - 1) ? value + (-1 << size) + 1 : value;

        private int DecodeSymbol(HuffmanTable table)
        {
            var code = GetBits(1);
            var length = 1;

            while (code > table.MaxCode[length])
            {
                code = (code << 1) | GetBits(1);
                length++;
            }

            return length > 16 ? 0 : table.Values[table.ValuePointer[length] + code - table.MinCode[length]];
        }

        private int GetBits(int count)
        {
            while (_bitCount < count)
            {
                _bits = (_bits << 8) | (uint)NextScanByte();
                _bitCount += 8;
            }

            _bitCount -= count;
            var value = (int)((_bits >> _bitCount) & ((1L << count) - 1));
            _bits &= (1L << _bitCount) - 1;
            return value;
        }

        private int NextScanByte()
        {
            if (_markerHit)
            {
                return 0;
            }

            if (_scanAt >= _file.Length)
            {
                _markerHit = true;
                _markerAt = _file.Length;
                return 0;
            }

            var value = _file[_scanAt++];
            if (value != 0xFF)
            {
                return value;
            }

            while (_scanAt < _file.Length && _file[_scanAt] == 0xFF)
            {
                _scanAt++;
            }

            if (_scanAt >= _file.Length)
            {
                _markerHit = true;
                _markerAt = _file.Length;
                return 0;
            }

            if (_file[_scanAt] == 0)
            {
                _scanAt++;
                return 0xFF;
            }

            _markerHit = true;
            _markerAt = _scanAt - 1;
            return 0;
        }

        private void ProcessRestart()
        {
            _bits = 0;
            _bitCount = 0;

            if (!_markerHit)
            {
                SkipToMarker();
            }

            if (_markerAt + 1 < _file.Length && _file[_markerAt + 1] is >= 0xD0 and <= 0xD7)
            {
                _scanAt = _markerAt + 2;
                _markerHit = false;
                _markerAt = _file.Length;
            }

            for (var i = 0; i < _scanCount; i++)
            {
                _components[_scanComponents[i]].Predictor = 0;
            }
        }

        private void SkipToMarker()
        {
            while (_scanAt < _file.Length)
            {
                if (_file[_scanAt] != 0xFF)
                {
                    _scanAt++;
                    continue;
                }

                var at = _scanAt;
                while (at < _file.Length && _file[at] == 0xFF)
                {
                    at++;
                }

                if (at >= _file.Length)
                {
                    break;
                }

                if (_file[at] == 0)
                {
                    _scanAt = at + 1;
                    continue;
                }

                _markerHit = true;
                _markerAt = at - 1;
                return;
            }

            _markerHit = true;
            _markerAt = _file.Length;
        }
    }
}
