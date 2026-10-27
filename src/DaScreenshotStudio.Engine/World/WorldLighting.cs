using System.Numerics;
using DaScreenshotStudio.Assets.Maps;
using DaScreenshotStudio.Engine.Rendering;

namespace DaScreenshotStudio.Engine.World;

public sealed class WorldLighting
{
    public const int MaskIntensityLimit = 0x20;

    public const int MaxObjectLights = WorldObjectTable.Capacity;

    private readonly ObjectLight[] _objectLights = new ObjectLight[MaxObjectLights];

    private byte[] _row = [];
    private byte[] _weights = [];
    private int _objectLightCount;

    public HeaLightMask? Mask { get; set; }

    public ushort AmbientColor { get; set; }

    public int AmbientIntensity { get; set; } = MaskIntensityLimit;

    public bool UsesMask => Mask is not null && AmbientIntensity < MaskIntensityLimit;

    public int PixelsLit { get; private set; }

    public void ClearObjectLights() => _objectLightCount = 0;

    public bool AddObjectLight(int centerX, int centerY, ObjectLightMask mask)
    {
        ArgumentNullException.ThrowIfNull(mask);

        if (_objectLightCount >= MaxObjectLights)
        {
            return false;
        }

        _objectLights[_objectLightCount] = new ObjectLight(centerX, centerY, mask);
        _objectLightCount++;
        return true;
    }

    public void Apply(Canvas canvas, int viewOffsetX, int viewOffsetY)
    {
        ArgumentNullException.ThrowIfNull(canvas);

        PixelsLit = 0;

        if (AmbientIntensity >= MaskIntensityLimit)
        {
            return;
        }

        var weight = PixelBlender.MaxAlphaWeight - (AmbientIntensity * PixelBlender.MaxAlphaWeight / MaskIntensityLimit);
        var usesMask = UsesMask;

        if (!usesMask && _objectLightCount == 0)
        {
            canvas.LightRect(canvas.Bounds, AmbientColor, weight);
            PixelsLit = canvas.Width * canvas.Height;
            return;
        }

        if (_row.Length < canvas.Width)
        {
            _row = new byte[canvas.Width];
            _weights = new byte[canvas.Width];
        }

        var maskLeft = 0;
        var maskTop = 0;

        if (usesMask)
        {
            Mask!.Project(0, 0, out var maskOriginX, out var maskOriginY);
            maskLeft = maskOriginX - viewOffsetX - IsometricProjection.HalfTileWidth;
            maskTop = maskOriginY - viewOffsetY;
        }

        var row = _row.AsSpan(0, canvas.Width);
        var weights = _weights.AsSpan(0, canvas.Width);

        var startX = Math.Max(0, maskLeft);
        var skipped = maskLeft < 0 ? Math.Min(canvas.Width, -maskLeft) : 0;

        var band = usesMask ? Mask!.FindBand(startX) : 0;

        for (var y = 0; y < canvas.Height; y++)
        {
            weights.Fill((byte)weight);

            if (usesMask && (uint)(maskTop + y) < (uint)Mask!.CanvasHeight)
            {
                var written = Mask.DecodeRow(band, maskTop + y, startX, row[..(canvas.Width - skipped)]);
                Merge(weights.Slice(skipped, written), row[..written]);
            }

            MergeObjectLights(weights, y);

            PixelsLit += canvas.LightRowWithMask(0, y, AmbientColor, weights);
        }
    }

    private void MergeObjectLights(Span<byte> weights, int y)
    {
        for (var i = 0; i < _objectLightCount; i++)
        {
            ref readonly var light = ref _objectLights[i];

            if (y < light.Top || y >= light.Bottom)
            {
                continue;
            }

            var left = light.Left;
            var mask = light.Mask;
            var from = Math.Max(0, left);
            var to = Math.Min(weights.Length, left + mask.Width);
            if (from >= to)
            {
                continue;
            }

            Merge(weights[from..to], mask.Row(y - light.Top)[(from - left)..(to - left)]);
        }
    }

    private static void Merge(Span<byte> weights, ReadOnlySpan<byte> pixels)
    {
        var i = 0;

        if (Vector.IsHardwareAccelerated && weights.Length >= Vector<byte>.Count)
        {
            var limit = new Vector<byte>(MaskIntensityLimit);

            for (; i <= weights.Length - Vector<byte>.Count; i += Vector<byte>.Count)
            {
                var value = new Vector<byte>(pixels[i..]);
                var lit = limit - Vector.Min(value, limit);
                Vector.Min(new Vector<byte>(weights[i..]), lit).CopyTo(weights[i..]);
            }
        }

        for (; i < weights.Length; i++)
        {
            var lit = MaskIntensityLimit - pixels[i];

            if (lit < weights[i])
            {
                weights[i] = (byte)Math.Max(0, lit);
            }
        }
    }

    private readonly struct ObjectLight(int centerX, int centerY, ObjectLightMask mask)
    {
        public readonly int Top = centerY + mask.OffsetY;

        public readonly int Bottom = centerY + mask.OffsetY + mask.Height;

        public readonly int Left = centerX + mask.OffsetX;

        public readonly ObjectLightMask Mask = mask;
    }
}
