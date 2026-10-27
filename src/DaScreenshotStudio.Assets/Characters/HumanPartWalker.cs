namespace DaScreenshotStudio.Assets.Characters;

public ref struct HumanPartWalker
{
    private readonly HumanAppearance _appearance;
    private readonly ReadOnlySpan<byte> _order;
    private readonly Span<char> _buffer;
    private readonly HumanIdleMotion _idle;
    private readonly int _motion;
    private readonly char _variant;
    private readonly int _idleStep;
    private int _nameLength;
    private int _position;

    public HumanPartWalker(
        scoped in HumanAppearance appearance,
        HumanDirection direction,
        int motion,
        scoped in HumanIdleMotion idle,
        int idleStep,
        Span<char> nameBuffer)
        : this(in appearance, direction, motion, '\0', in idle, idleStep, nameBuffer)
    {
    }

    public HumanPartWalker(
        scoped in HumanAppearance appearance,
        HumanDirection direction,
        int motion,
        char variant,
        scoped in HumanIdleMotion idle,
        int idleStep,
        Span<char> nameBuffer)
    {
        if (nameBuffer.Length < HumanPartName.MaxNameLength)
        {
            throw new ArgumentException(
                $"A part name needs {HumanPartName.MaxNameLength} characters.", nameof(nameBuffer));
        }

        _appearance = appearance;
        _order = HumanPartOrder.Select(direction, appearance.Body.BodySprite);
        _buffer = nameBuffer;
        _idle = idle;
        _motion = motion;
        _variant = variant;
        _idleStep = idleStep;
        _position = 0;
        Category = -1;
        Dye = -1;
        Motion = motion;
        Frame = -1;
        AbsoluteFrame = -1;
        Direction = direction;
    }

    public HumanDirection Direction { get; }

    public int Category { get; private set; }

    public int ResourceId { get; private set; }

    public int SpriteId { get; private set; }

    public char Letter { get; private set; }

    public int Motion { get; private set; }

    public int Frame { get; private set; }

    public int AbsoluteFrame { get; private set; }

    public int PaletteSelector { get; private set; }

    public int Dye { get; private set; }

    public readonly ReadOnlySpan<char> Name => _buffer[.._nameLength];

    public readonly bool IsMirrored => HumanPartOrder.IsMirrored(Direction);

    public bool MoveNext()
    {
        while (_position < _order.Length)
        {
            var category = _order[_position++];
            var spriteId = _appearance.GetPartSprite(category);

            if (!HumanPartCategory.TryResolve(category, spriteId, out _, out var resourceId))
            {
                continue;
            }

            var overlaid = _idle.TryGetFrame(category, _idleStep, out var idleFrame);
            var motion = overlaid ? HumanIdleMotion.MotionIndex : _motion;

            var bodySprite = _appearance.Body.BodySprite;
            var absoluteFrame =
                HeadOnlyBodyExceptions.ForcesWeaponFrameZero(bodySprite, category, motion) ? 0 : -1;

            if (HeadOnlyBodyExceptions.RedirectsOvercoatMotion(
                    bodySprite, category, motion, _appearance.WeaponSprite))
            {
                motion = HeadOnlyBodyExceptions.OvercoatMotion;
            }

            var variant = overlaid ? '\0' : _variant;

            if (!HumanPartName.TryFormat(in _appearance, category, motion, variant, _buffer, out _nameLength))
            {
                continue;
            }

            Category = category;
            ResourceId = resourceId;
            SpriteId = spriteId;
            Motion = motion;
            Frame = overlaid ? idleFrame : -1;
            AbsoluteFrame = absoluteFrame;

            Letter = _buffer[1];
            PaletteSelector = _appearance.GetPaletteSelector(category);
            Dye = _appearance.TryGetDye(category, out var dye) ? dye : -1;
            return true;
        }

        _nameLength = 0;
        return false;
    }
}
