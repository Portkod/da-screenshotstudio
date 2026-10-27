namespace DaScreenshotStudio.Engine.Ui;

public static class GuiPaletteNames
{
    public const int Default = 0;

    public const int NoPalette = -1;

    private static readonly Arm[] Arms =
    [
        new("album.epf", false, 17),
        new("album_b.epf", false, 17),

        new("bw_flag.epf", false, 16),
        new("bw_check.epf", false, NoPalette),
        new("bw_num.epf", false, NoPalette),
        new("bw_", true, 13),

        new("dlgcre01.epf", false, 8),
        new("dlgcre", true, 4),

        new("gbicon01.epf", false, 2),
        new("gbicon03.epf", false, 2),
        new("gbicon12.epf", false, 1),

        new("key", true, 14),
        new("kdesc", true, 14),
        new("khotkey.epf", false, 14),

        new("legends.epf", false, 3),
        new("loading", true, 2),
        new("lod0", true, 4),
        new("lodbk", true, 7),
        new("lback", true, 4),
        new("leicon", true, 10),
        new("lsback.", true, 10),
        new("lsback2", true, 10),
        new("lsshelpe", true, 10),
        new("lsshelp", true, 6),
        new("lsspsk2", true, 10),
        new("lss", true, 10),
        new("lsu", true, Default),
        new("lpopup", true, Default),
        new("ldi", true, 11),
        new("lwmap.epf", false, 12),
        new("lg_", true, 15),

        new("mouse.epf", false, 3),
        new("mail.epf", false, 3),
        new("manufac.epf", false, Default),
        new("manuprev.epf", false, NoPalette),
        new("manunext.epf", false, NoPalette),

        new("nation.epf", false, 5),

        new("orb001.epf", false, 1),
        new("orb002.epf", false, 1),

        new("statcon.epf", false, 2),
        new("setup0", true, 4),
        new("spell0", true, 6),
        new("skill0", true, 6),
        new("staff.epf", false, 9),

        new("tmapv", true, 12),
    ];

    public static int Count => Arms.Length;

    public static int NumberFor(scoped ReadOnlySpan<char> entryName)
    {
        for (var i = 0; i < Arms.Length; i++)
        {
            var arm = Arms[i];
            var matched = arm.Prefix
                ? entryName.StartsWith(arm.Text, StringComparison.OrdinalIgnoreCase)
                : entryName.Equals(arm.Text, StringComparison.OrdinalIgnoreCase);

            if (matched)
            {
                return arm.Number;
            }
        }

        return Default;
    }

    public readonly record struct Arm(string Text, bool Prefix, int Number);

    public static ReadOnlySpan<Arm> Table => Arms;
}
