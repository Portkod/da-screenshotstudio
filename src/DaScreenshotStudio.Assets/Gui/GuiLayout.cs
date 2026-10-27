using System.Text;
using DaScreenshotStudio.Assets.Archives;

namespace DaScreenshotStudio.Assets.Gui;

public readonly struct GuiLayoutImage : IEquatable<GuiLayoutImage>
{
    internal GuiLayoutImage(string entryName, int frame)
    {
        EntryName = entryName;
        Frame = frame;
    }

    public string EntryName { get; }

    public int Frame { get; }

    public bool Equals(GuiLayoutImage other) =>
        Frame == other.Frame && string.Equals(EntryName, other.EntryName, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is GuiLayoutImage other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(EntryName, Frame);

    public override string ToString() => $"{EntryName} {Frame}";

    public static bool operator ==(GuiLayoutImage left, GuiLayoutImage right) => left.Equals(right);

    public static bool operator !=(GuiLayoutImage left, GuiLayoutImage right) => !left.Equals(right);
}

public sealed class GuiLayoutControl
{
    public const string DefaultName = "CONTROL";

    public const int ImageButtonStateCount = 3;

    internal GuiLayoutControl(string name, int type, int left, int top, int right, int bottom, GuiLayoutImage[] images, int[] values, int[] colors)
    {
        Name = name;
        Type = type;
        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
        Images = images;
        Values = values;
        Colors = colors;
    }

    public string Name { get; }

    public int Type { get; }

    public int Left { get; }

    public int Top { get; }

    public int Right { get; }

    public int Bottom { get; }

    public IReadOnlyList<GuiLayoutImage> Images { get; }

    public IReadOnlyList<int> Values { get; }

    public IReadOnlyList<int> Colors { get; }

    public override string ToString() => Name;
}

public sealed class GuiLayout
{
    private readonly GuiLayoutControl[] _controls;
    private readonly Dictionary<string, int> _byName;
    private readonly Dictionary<string, int>.AlternateLookup<ReadOnlySpan<char>> _lookup;

    private GuiLayout(string name, GuiLayoutControl[] controls)
    {
        Name = name;
        _controls = controls;

        _byName = new Dictionary<string, int>(controls.Length, StringComparer.Ordinal);
        for (var i = 0; i < controls.Length; i++)
        {
            _byName.TryAdd(controls[i].Name, i);
        }

        _lookup = _byName.GetAlternateLookup<ReadOnlySpan<char>>();
    }

    public string Name { get; }

    public int ControlCount => _controls.Length;

    public GuiLayoutControl this[int index] => _controls[index];

    public static GuiLayout Parse(string name, ReadOnlySpan<byte> data)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        var controls = new List<GuiLayoutControl>();
        var reader = new GuiLayoutTokenizer(data);

        while (reader.TryReadToken(out var token))
        {
            if (token.Kind != GuiLayoutTokenKind.Tag || !token.Text.SequenceEqual("CONTROL"u8))
            {
                continue;
            }

            controls.Add(ReadControl(ref reader));
        }

        return new GuiLayout(name, [.. controls]);
    }

    public static GuiLayout? Load(IAssetArchive archive, scoped ReadOnlySpan<char> name)
    {
        ArgumentNullException.ThrowIfNull(archive);
        return archive.TryRead(name, out var data) ? Parse(new string(name), data) : null;
    }

    public bool TryFind(scoped ReadOnlySpan<char> controlName, out GuiLayoutControl control)
    {
        if (_lookup.TryGetValue(controlName, out var index))
        {
            control = _controls[index];
            return true;
        }

        control = null!;
        return false;
    }

    private static GuiLayoutControl ReadControl(ref GuiLayoutTokenizer reader)
    {
        var name = GuiLayoutControl.DefaultName;
        var type = 0;
        int left = 0, top = 0, right = 0, bottom = 0;
        List<GuiLayoutImage>? images = null;
        List<int>? values = null;
        List<int>? colors = null;

        var field = Field.None;

        while (reader.TryReadToken(out var token))
        {
            if (token.Kind == GuiLayoutTokenKind.Tag)
            {
                if (token.Text.SequenceEqual("ENDCONTROL"u8))
                {
                    break;
                }

                field =
                    token.Text.SequenceEqual("NAME"u8) ? Field.Name
                    : token.Text.SequenceEqual("TYPE"u8) ? Field.Type
                    : token.Text.SequenceEqual("RECT"u8) ? Field.Rect
                    : token.Text.SequenceEqual("IMAGE"u8) ? Field.Image
                    : token.Text.SequenceEqual("VALUE"u8) ? Field.Value
                    : token.Text.SequenceEqual("COLOR"u8) ? Field.Color
                    : Field.None;

                if (field == Field.Rect)
                {
                    left = ReadNumber(ref reader);
                    top = ReadNumber(ref reader);
                    right = ReadNumber(ref reader);
                    bottom = ReadNumber(ref reader);
                    field = Field.None;
                }

                continue;
            }

            switch (field)
            {
                case Field.Name:
                    name = Encoding.Latin1.GetString(token.Text);
                    field = Field.None;
                    break;

                case Field.Type:
                    type = token.Number;
                    field = Field.None;
                    break;

                case Field.Image:
                    if (token.Kind == GuiLayoutTokenKind.Text)
                    {
                        images ??= [];
                        images.Add(new GuiLayoutImage(Encoding.Latin1.GetString(token.Text), ReadNumber(ref reader)));
                    }

                    break;

                case Field.Value:
                    if (token.Kind == GuiLayoutTokenKind.Number)
                    {
                        values ??= [];
                        values.Add(token.Number);
                    }

                    break;

                case Field.Color:
                    if (token.Kind == GuiLayoutTokenKind.Number)
                    {
                        colors ??= [];
                        colors.Add(token.Number);
                    }

                    break;

                default:
                    break;
            }
        }

        return new GuiLayoutControl(
            name,
            type,
            left,
            top,
            right,
            bottom,
            images is null ? [] : [.. images],
            values is null ? [] : [.. values],
            colors is null ? [] : [.. colors]);
    }

    private static int ReadNumber(ref GuiLayoutTokenizer reader) =>
        reader.TryReadToken(out var token) && token.Kind == GuiLayoutTokenKind.Number ? token.Number : 0;

    private enum Field
    {
        None,
        Name,
        Type,
        Rect,
        Image,
        Value,
        Color,
    }
}
