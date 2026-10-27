using DaScreenshotStudio.Assets.Archives;

namespace DaScreenshotStudio.Assets.Characters;

public readonly struct HumanSkillMotion
{
    internal HumanSkillMotion(int startFrame, int frames, char variant)
    {
        StartFrame = startFrame;
        Frames = frames;
        Variant = variant;
    }

    public int StartFrame { get; }

    public int Frames { get; }

    public char Variant { get; }
}

public sealed class HumanSkillMotions
{
    public const int RecordCount = 0x80;

    public const int GarmentLimit = 0x800;

    public const byte FirstAnimation = 0x80;

    public const int ExtendedGarmentBase = 1000;

    public const ushort RefusingWeaponSprite = 0x82;

    public const string OrdinaryEntry = "Skill_e.tbl";

    public const string ExtendedEntry = "Skill_i.tbl";

    public static readonly HumanSkillMotions Empty = new();

    private readonly sbyte[] _fileNumber = new sbyte[RecordCount];
    private readonly short[] _startFrame = new short[RecordCount];
    private readonly short[] _frames = new short[RecordCount];
    private readonly bool[] _permitted = new bool[RecordCount * GarmentLimit];

    public HumanSkillMotions() => Array.Fill(_fileNumber, (sbyte)-1);

    public int LoadedRecords { get; private set; }

    public static HumanSkillMotions Load(IAssetArchive archive)
    {
        ArgumentNullException.ThrowIfNull(archive);

        var table = new HumanSkillMotions();

        if (archive.TryRead(OrdinaryEntry, out var ordinary))
        {
            table.Parse(ordinary, 0);
        }

        if (archive.TryRead(ExtendedEntry, out var extended))
        {
            table.Parse(extended, ExtendedGarmentBase);
        }

        return table;
    }

    public void Parse(ReadOnlySpan<byte> text, int garmentBase)
    {
        var line = 0;

        while (line < text.Length)
        {
            var end = line;
            while (end < text.Length && text[end] is not ((byte)'\n' or (byte)'\r'))
            {
                end++;
            }

            ParseRow(text[line..end], garmentBase);
            line = end + 1;
        }
    }

    public bool TryGetMotion(
        byte animation,
        ushort weaponSprite,
        ushort armorSprite,
        ushort overcoatSprite,
        out HumanSkillMotion motion)
    {
        motion = default;

        if (animation < FirstAnimation || weaponSprite == RefusingWeaponSprite)
        {
            return false;
        }

        var record = animation - FirstAnimation;
        var garment = overcoatSprite != 0 ? overcoatSprite : armorSprite;

        if (garment >= GarmentLimit || !_permitted[(record * GarmentLimit) + garment])
        {
            return false;
        }

        return TryGetRecord(animation, out motion);
    }

    public bool TryGetRecord(byte animation, out HumanSkillMotion motion)
    {
        motion = default;

        if (animation < FirstAnimation)
        {
            return false;
        }

        var record = animation - FirstAnimation;
        var file = _fileNumber[record];
        if (file < 0 || _frames[record] <= 0)
        {
            return false;
        }

        motion = new HumanSkillMotion(_startFrame[record], _frames[record], (char)('a' + file));
        return true;
    }

    private void ParseRow(ReadOnlySpan<byte> row, int garmentBase)
    {
        if (row.Length == 0 || row[0] == (byte)';')
        {
            return;
        }

        Span<int> head = stackalloc int[4];
        var taken = 0;
        var at = 0;

        while (taken < head.Length && TryReadNumber(row, ref at, out var value))
        {
            head[taken++] = value;
        }

        if (taken < head.Length)
        {
            return;
        }

        var record = head[0];
        var file = head[1];
        var startFrame = head[2];
        var frames = head[3];

        if ((uint)record >= RecordCount || (uint)file > 'z' - 'a' || (uint)startFrame > short.MaxValue
            || (uint)frames > short.MaxValue)
        {
            return;
        }

        if (_fileNumber[record] < 0)
        {
            LoadedRecords++;
        }

        _fileNumber[record] = (sbyte)file;
        _startFrame[record] = (short)startFrame;
        _frames[record] = (short)frames;

        var permitted = _permitted.AsSpan(record * GarmentLimit, GarmentLimit);
        while (TryReadNumber(row, ref at, out var garment))
        {
            var index = garment + garmentBase;
            if ((uint)index < GarmentLimit)
            {
                permitted[index] = true;
            }
        }
    }

    private static bool TryReadNumber(ReadOnlySpan<byte> row, ref int at, out int value)
    {
        value = 0;

        while (at < row.Length && row[at] is < (byte)'0' or > (byte)'9')
        {
            at++;
        }

        if (at >= row.Length)
        {
            return false;
        }

        while (at < row.Length && row[at] is >= (byte)'0' and <= (byte)'9')
        {
            value = value > (int.MaxValue / 10) ? int.MaxValue : (value * 10) + (row[at] - (byte)'0');
            at++;
        }

        return true;
    }
}
