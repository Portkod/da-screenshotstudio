using System.Numerics;
using System.Runtime.CompilerServices;

namespace DaScreenshotStudio.Engine.Rendering;

public sealed class PixelBlender
{
    public const int MaxAlphaWeight = 32;

    public ushort ShadeKey { get; set; } = PalettePacker.BumpedBlack;

    public ushort OpaqueKey { get; }

    private readonly PixelPacking _packing;

    private readonly byte[] _screenFive;
    private readonly byte[] _screenGreen;
    private readonly int _fiveStride;
    private readonly int _greenStride;

    private readonly byte[] _rampFive;
    private readonly byte[] _rampGreen;

    public PixelBlender(in PixelPacking packing)
    {
        _packing = packing;
        _screenFive = BuildScreenTable(packing.RedMax);
        _screenGreen = packing.GreenMax == packing.RedMax ? _screenFive : BuildScreenTable(packing.GreenMax);
        _fiveStride = packing.RedMax + 1;
        _greenStride = packing.GreenMax + 1;
        _rampFive = BuildRampTable(packing.RedMax);
        _rampGreen = packing.GreenMax == packing.RedMax ? _rampFive : BuildRampTable(packing.GreenMax);
        OpaqueKey = packing.Pack(byte.MaxValue, byte.MaxValue, byte.MaxValue);
    }

    public PixelPacking Packing => _packing;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ushort Screen(ushort source, ushort destination) => _packing.Compose(
        _screenFive[(_packing.Red(source) * _fiveStride) + _packing.Red(destination)],
        _screenGreen[(_packing.Green(source) * _greenStride) + _packing.Green(destination)],
        _screenFive[(_packing.Blue(source) * _fiveStride) + _packing.Blue(destination)]);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ushort Average(ushort a, ushort b) => _packing.Compose(
        (_packing.Red(a) + _packing.Red(b)) >> 1,
        (_packing.Green(a) + _packing.Green(b)) >> 1,
        (_packing.Blue(a) + _packing.Blue(b)) >> 1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ushort Alpha(ushort source, ushort destination, int weight)
    {
        var inverse = MaxAlphaWeight - weight;
        return _packing.Compose(
            ((_packing.Red(source) * weight) + (_packing.Red(destination) * inverse)) >> 5,
            ((_packing.Green(source) * weight) + (_packing.Green(destination) * inverse)) >> 5,
            ((_packing.Blue(source) * weight) + (_packing.Blue(destination) * inverse)) >> 5);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ushort Light(ushort ambient, ushort destination, int ambientWeight)
    {
        var keep = MaxAlphaWeight - ambientWeight;
        return _packing.Compose(
            ((_packing.Red(destination) * keep) >> 5) + ((_packing.Red(ambient) * ambientWeight) >> 5),
            ((_packing.Green(destination) * keep) >> 5) + ((_packing.Green(ambient) * ambientWeight) >> 5),
            ((_packing.Blue(destination) * keep) >> 5) + ((_packing.Blue(ambient) * ambientWeight) >> 5));
    }

    public void LightSpan(Span<ushort> destination, ushort ambient, int weight)
    {
        weight = Math.Clamp(weight, 0, MaxAlphaWeight);
        var i = 0;

        if (Vector.IsHardwareAccelerated && destination.Length >= Vector<ushort>.Count)
        {
            var lanes = new LightLanes(in _packing, ambient);
            var weights = new Vector<ushort>((ushort)weight);

            for (; i <= destination.Length - Vector<ushort>.Count; i += Vector<ushort>.Count)
            {
                lanes.Blend(destination[i..], weights);
            }
        }

        for (; i < destination.Length; i++)
        {
            destination[i] = Light(ambient, destination[i], weight);
        }
    }

    public int LightSpanWithMask(Span<ushort> destination, ushort ambient, ReadOnlySpan<byte> weights)
    {
        var length = Math.Min(destination.Length, weights.Length);
        var lit = 0;
        var i = 0;

        if (Vector.IsHardwareAccelerated && length >= Vector<byte>.Count)
        {
            var lanes = new LightLanes(in _packing, ambient);
            var full = new Vector<ushort>(MaxAlphaWeight);
            var half = Vector<ushort>.Count;

            var counts = Vector<ushort>.Zero;
            var blocks = 0;

            for (; i <= length - Vector<byte>.Count; i += Vector<byte>.Count)
            {
                Vector.Widen(new Vector<byte>(weights[i..]), out var low, out var high);
                low = Vector.Min(low, full);
                high = Vector.Min(high, full);

                counts += Vector.GreaterThan(low, Vector<ushort>.Zero) & Vector<ushort>.One;
                counts += Vector.GreaterThan(high, Vector<ushort>.Zero) & Vector<ushort>.One;

                lanes.Blend(destination[i..], low);
                lanes.Blend(destination[(i + half)..], high);

                if (++blocks == 0x7F)
                {
                    lit += Vector.Sum(counts);
                    counts = Vector<ushort>.Zero;
                    blocks = 0;
                }
            }

            lit += Vector.Sum(counts);
        }

        for (; i < length; i++)
        {
            int weight = weights[i];

            if (weight > 0)
            {
                destination[i] = Light(ambient, destination[i], Math.Min(weight, MaxAlphaWeight));
                lit++;
            }
        }

        return lit;
    }

    private readonly struct LightLanes
    {
        private readonly Vector<ushort> _redMax;
        private readonly Vector<ushort> _greenMax;
        private readonly Vector<ushort> _blueMax;
        private readonly Vector<ushort> _red;
        private readonly Vector<ushort> _green;
        private readonly Vector<ushort> _blue;
        private readonly int _redShift;
        private readonly int _greenShift;

        public LightLanes(in PixelPacking packing, ushort ambient)
        {
            _redMax = new Vector<ushort>((ushort)packing.RedMax);
            _greenMax = new Vector<ushort>((ushort)packing.GreenMax);
            _blueMax = new Vector<ushort>((ushort)packing.BlueMax);
            _red = new Vector<ushort>((ushort)packing.Red(ambient));
            _green = new Vector<ushort>((ushort)packing.Green(ambient));
            _blue = new Vector<ushort>((ushort)packing.Blue(ambient));
            _redShift = packing.RedShift;
            _greenShift = packing.GreenShift;
        }

        public void Blend(Span<ushort> target, Vector<ushort> weight)
        {
            var pixels = new Vector<ushort>(target);
            var keep = new Vector<ushort>(MaxAlphaWeight) - weight;

            var red = Vector.ShiftRightLogical(
                    (Vector.ShiftRightLogical(pixels, _redShift) & _redMax) * keep, 5)
                + Vector.ShiftRightLogical(_red * weight, 5);
            var green = Vector.ShiftRightLogical(
                    (Vector.ShiftRightLogical(pixels, _greenShift) & _greenMax) * keep, 5)
                + Vector.ShiftRightLogical(_green * weight, 5);
            var blue = Vector.ShiftRightLogical((pixels & _blueMax) * keep, 5)
                + Vector.ShiftRightLogical(_blue * weight, 5);

            (Vector.ShiftLeft(red, _redShift) | Vector.ShiftLeft(green, _greenShift) | blue).CopyTo(target);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ushort Additive(ushort source, ushort destination) => _packing.Compose(
        Math.Min(_packing.RedMax, _packing.Red(source) + _packing.Red(destination)),
        Math.Min(_packing.GreenMax, _packing.Green(source) + _packing.Green(destination)),
        Math.Min(_packing.BlueMax, _packing.Blue(source) + _packing.Blue(destination)));

    public const int SourceWeightSteps = 10;

    private static ReadOnlySpan<byte> SourceWeights => [0, 0, 0, 1, 2, 3, 5, 6, 7, 9, 10];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ushort SourceWeighted(ushort source, ushort destination)
    {
        var brightest = Math.Max(
            Math.Max(_packing.ExpandRed(source), _packing.ExpandGreen(source)),
            _packing.ExpandBlue(source));

        int weight = SourceWeights[brightest / 25];
        var keep = SourceWeightSteps - weight;
        var fiveStride = SourceWeightSteps + 1;

        return _packing.Compose(
            Math.Min(
                _packing.RedMax,
                _rampFive[(_packing.Red(source) * fiveStride) + weight]
                    + _rampFive[(_packing.Red(destination) * fiveStride) + keep]),
            Math.Min(
                _packing.GreenMax,
                _rampGreen[(_packing.Green(source) * fiveStride) + weight]
                    + _rampGreen[(_packing.Green(destination) * fiveStride) + keep]),
            Math.Min(
                _packing.BlueMax,
                _rampFive[(_packing.Blue(source) * fiveStride) + weight]
                    + _rampFive[(_packing.Blue(destination) * fiveStride) + keep]));
    }

    public interface IPixelKernel
    {
        ushort Apply(ushort source, ushort destination);
    }

    public readonly struct AverageKernel(PixelBlender blender) : IPixelKernel
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ushort Apply(ushort source, ushort destination) => blender.Average(source, destination);
    }

    public readonly struct ScreenKernel(PixelBlender blender) : IPixelKernel
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ushort Apply(ushort source, ushort destination) => blender.Screen(source, destination);
    }

    public readonly struct AlphaKernel(PixelBlender blender, int weight) : IPixelKernel
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ushort Apply(ushort source, ushort destination) => blender.Alpha(source, destination, weight);
    }

    public readonly struct AdditiveKernel(PixelBlender blender) : IPixelKernel
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ushort Apply(ushort source, ushort destination) => blender.Additive(source, destination);
    }

    public readonly struct SourceWeightedKernel(PixelBlender blender) : IPixelKernel
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ushort Apply(ushort source, ushort destination) => blender.SourceWeighted(source, destination);
    }

    public readonly struct ShadeKernel(PixelBlender blender) : IPixelKernel
    {
        private readonly ushort _key = blender.ShadeKey;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ushort Apply(ushort source, ushort destination) =>
            source == _key ? blender.Average(source, destination) : source;
    }

    public readonly struct DarkenKernel(PixelBlender blender) : IPixelKernel
    {
        private readonly ushort _key = blender.OpaqueKey;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ushort Apply(ushort source, ushort destination) =>
            source == _key ? blender.Average(destination, PalettePacker.TransparentKey) : source;
    }

    public readonly struct VeilKernel(PixelBlender blender) : IPixelKernel
    {
        private readonly ushort _key = blender.OpaqueKey;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ushort Apply(ushort source, ushort destination) =>
            source == _key ? source : blender.Average(source, destination);
    }

    public readonly struct CopyKernel : IPixelKernel
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ushort Apply(ushort source, ushort destination) => source;
    }

    public readonly struct TintedKernel<TInner>(PixelBlender blender, ushort color, TInner inner) : IPixelKernel
        where TInner : struct, IPixelKernel
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ushort Apply(ushort source, ushort destination) =>
            inner.Apply(blender.Average(source, color), destination);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ushort Combine(BlendMode mode, ushort source, ushort destination, int weight) => mode switch
    {
        BlendMode.Average => Average(source, destination),
        BlendMode.Screen => Screen(source, destination),
        BlendMode.Alpha or BlendMode.AlphaPlane => Alpha(source, destination, weight),
        BlendMode.Additive => Additive(source, destination),
        BlendMode.SourceWeighted => SourceWeighted(source, destination),
        BlendMode.Shade => source == ShadeKey ? Average(source, destination) : source,
        BlendMode.Darken => source == OpaqueKey ? Average(destination, PalettePacker.TransparentKey) : source,
        BlendMode.Veil => source == OpaqueKey ? source : Average(source, destination),
        _ => source,
    };

    private static byte[] BuildScreenTable(int max)
    {
        var span = max + 1;
        var table = new byte[span * span];

        for (var source = 0; source <= max; source++)
        {
            for (var destination = 0; destination <= max; destination++)
            {
                table[(source * span) + destination] =
                    (byte)Math.Min(max, ((source * span) + (destination * (span - source))) / span);
            }
        }

        return table;
    }

    private static byte[] BuildRampTable(int max)
    {
        var stride = SourceWeightSteps + 1;
        var table = new byte[(max + 1) * stride];

        for (var component = 0; component <= max; component++)
        {
            for (var weight = 0; weight <= SourceWeightSteps; weight++)
            {
                table[(component * stride) + weight] =
                    (byte)(((component * weight) + (SourceWeightSteps / 2)) / SourceWeightSteps);
            }
        }

        return table;
    }
}
