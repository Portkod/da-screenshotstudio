using DaScreenshotStudio.Assets.Archives;
using DaScreenshotStudio.Assets.Fonts;

namespace DaScreenshotStudio.Engine.Text;

public enum TextLanguage
{
    Korean = 0,
    English = 1,
    Japanese = 2,
    Taiwan = 3,
}

public sealed class GameFont
{
    public const int EllipsisLength = 3;

    public const int FixedCellWidth = 6;

    public GameFont(LftFont face, LftCodePage codePage, TextLanguage language)
    {
        ArgumentNullException.ThrowIfNull(face);

        Face = face;
        CodePage = codePage;
        Language = language;
    }

    public LftFont Face { get; }

    public LftCodePage CodePage { get; }

    public TextLanguage Language { get; }

    public int LineHeight => Face.NominalHeight;

    public static string EntryNameFor(TextLanguage language) => language switch
    {
        TextLanguage.Korean => "lod.lft",
        TextLanguage.Japanese => "yami.lft",
        TextLanguage.Taiwan => "taiwan.lft",
        _ => "da.lft",
    };

    public static LftCodePage CodePageFor(TextLanguage language) => language switch
    {
        TextLanguage.Korean => LftCodePage.Korean949,
        TextLanguage.Japanese => LftCodePage.Japanese932,
        _ => LftCodePage.SingleByte,
    };

    public static LftCodePage DrawingCodePageFor(TextLanguage language) =>
        language == TextLanguage.Japanese ? LftCodePage.Japanese932 : LftCodePage.Korean949;

    public static GameFont? TryLoad(
        IAssetArchive archive,
        TextLanguage language = TextLanguage.English,
        bool singleByteText = false)
    {
        ArgumentNullException.ThrowIfNull(archive);

        if (!archive.TryReadMemory(EntryNameFor(language), out var data)
            && !archive.TryReadMemory(EntryNameFor(TextLanguage.English), out data))
        {
            return null;
        }

        var codePage = singleByteText ? CodePageFor(language) : DrawingCodePageFor(language);
        return new GameFont(LftFont.Parse(data), codePage, language);
    }

    public int MeasureWidth(scoped ReadOnlySpan<byte> text) => Face.Measure(text, CodePage).AdvanceWidth;

    public int CountFitting(scoped ReadOnlySpan<byte> text, int availableWidth)
    {
        var penX = 0;
        var consumed = 0;
        var reader = new LftKeyReader(text, CodePage);

        while (reader.TryReadKey(out var key))
        {
            var bytes = key > 0xFF ? 2 : 1;
            var advance = bytes * FixedCellWidth;
            if (penX + advance > availableWidth)
            {
                break;
            }

            penX += advance;
            consumed += bytes;
        }

        return consumed;
    }

    public int TruncateWithEllipsis(scoped ReadOnlySpan<byte> text, int cells, out bool truncated)
    {
        if (text.Length <= cells)
        {
            truncated = false;
            return text.Length;
        }

        truncated = true;
        var limit = Math.Max(0, cells - EllipsisLength);
        var kept = 0;
        var reader = new LftKeyReader(text, CodePage);

        while (reader.TryReadKey(out var key))
        {
            var width = key > 0xFF ? 2 : 1;
            if (kept + width > limit)
            {
                break;
            }

            kept += width;
        }

        return kept;
    }
}
