using DaScreenshotStudio.Assets.Metadata;
using DaScreenshotStudio.Assets.Tables;
using DaScreenshotStudio.Engine.Rendering;

namespace DaScreenshotStudio.Engine.Ui;

public sealed class AbilityGridPane : CellRollPane
{
    public const int IconBankCount = 3;

    private static readonly string[] SkillBanks = ["skill001.epf", "skill002.epf", "skill003.epf"];
    private static readonly string[] SpellBanks = ["spell001.epf", "spell002.epf", "spell003.epf"];

    private readonly UiAssets _assets;
    private readonly bool _spells;

    private int[] _icon = [];
    private byte[] _bank = [];

    public AbilityGridPane(in PixelRect bounds, PixelBlender blender, UiAssets assets, bool spells)
        : base(in bounds, blender, assets)
    {
        _assets = assets;
        _spells = spells;
    }

    public string BankFor(AbilityRowState state) => BankName(_spells, state);

    public static string BankName(bool spells, AbilityRowState state) =>
        (spells ? SpellBanks : SkillBanks)[Math.Clamp((int)state, 1, IconBankCount) - 1];

    public int IconAt(int index) => _icon[CheckIndex(index)];

    public AbilityRowState StateAt(int index) => (AbilityRowState)_bank[CheckIndex(index)];

    public void SetAbilities(
        ClassAbilityTable table,
        PlayerModel player,
        MessageTable messages,
        bool showAbilityMetadata,
        bool showMasterMetadata)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(player);

        var kind = _spells ? ClassAbilityKind.Spell : ClassAbilityKind.Skill;
        var own = _spells ? player.Spells : player.Skills;

        var wanted = 0;
        foreach (var ability in table.Records)
        {
            if (ability.Kind == kind)
            {
                wanted++;
            }
        }

        Grow(wanted);
        Count = 0;

        foreach (var ability in table.Records)
        {
            if (ability.Kind != kind)
            {
                continue;
            }

            var slot = RowText(Count);
            var name = slot[..RecordTextLength];
            var line = slot[RecordTextLength..];

            var written = 0;
            foreach (var character in ability.Name)
            {
                if (written == RecordTextLength)
                {
                    break;
                }

                name[written++] = (byte)character;
            }

            SetRowLengths(
                Count,
                written,
                AbilityRoster.FormatLevelLine(ability, messages, line[..AbilityRoster.LevelLineLength]));

            _icon[Count] = ability.Icon;
            _bank[Count] = (byte)AbilityRoster.StateOf(
                ability,
                player,
                own,
                showAbilityMetadata,
                showMasterMetadata);
            Count++;
        }

        Position = Math.Min(Position, Maximum);
        Invalidate();
    }

    protected override IconFrame IconFor(int index) =>
        _assets.PlacedPaletteFrame(BankFor((AbilityRowState)_bank[index]), _icon[index]);

    private void Grow(int rows)
    {
        GrowRows(rows);

        if (_icon.Length >= rows)
        {
            return;
        }

        _icon = new int[rows];
        _bank = new byte[rows];
    }
}
