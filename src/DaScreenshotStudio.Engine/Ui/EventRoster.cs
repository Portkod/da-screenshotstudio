using DaScreenshotStudio.Assets.Metadata;
using DaScreenshotStudio.Assets.Tables;
using DaScreenshotStudio.Engine.Core;

namespace DaScreenshotStudio.Engine.Ui;

public enum EventRowState
{
    Complete = 0,
    Available = 1,
    Locked = 2,
}

public static class EventRoster
{
    public const int ProgressionLineLength = 32;

    public const int AbilityBucket = 6;

    public const int MasterBucket = 7;

    public const int BucketCount = 7;

    public static ReadOnlySpan<int> LevelBands => [11, 41, 71, 99];

    public static int BucketFor(int level, bool showAbilityMetadata, bool showMasterMetadata)
    {
        if (showMasterMetadata)
        {
            return MasterBucket;
        }

        if (showAbilityMetadata)
        {
            return AbilityBucket;
        }

        var bucket = 1;
        foreach (var band in LevelBands)
        {
            if (level >= band)
            {
                bucket++;
            }
        }

        return bucket;
    }

    public static int LowestBucket(int levelMask)
    {
        var bucket = 1;
        for (var mask = 1u; mask != 0; mask <<= 1, bucket++)
        {
            if ((levelMask & mask) != 0)
            {
                return bucket >= 1 && bucket <= BucketCount ? bucket : 1;
            }
        }

        return 1;
    }

    public static int FormatProgressionLine(EventRecord record, MessageTable messages, Span<byte> destination)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(messages);

        var bucket = LowestBucket(record.LevelMask);

        if (bucket == AbilityBucket)
        {
            return AbilityRoster.Format(messages.Get(MessageTable.MasterRequirementSlot), 0, destination);
        }

        if (bucket == MasterBucket)
        {
            return AbilityRoster.Format(messages.Get(MessageTable.MedenianSlot), 0, destination);
        }

        return AbilityRoster.Format(messages.Get(MessageTable.EventProgressionSlot), bucket, destination);
    }

    public static EventRowState StateOf(
        EventRecord record,
        LegendListPane? legend,
        int bucket,
        CharacterClass characterClass)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (legend is not null && legend.ContainsKey(record.Id))
        {
            return EventRowState.Complete;
        }

        return RequirementsMet(record, legend, bucket, characterClass)
            ? EventRowState.Available
            : EventRowState.Locked;
    }

    public static bool RequirementsMet(
        EventRecord record,
        LegendListPane? legend,
        int bucket,
        CharacterClass characterClass)
    {
        ArgumentNullException.ThrowIfNull(record);

        if ((record.LevelMask & BitFor(bucket)) == 0)
        {
            return false;
        }

        if ((record.ClassMask & BitFor((int)characterClass)) == 0)
        {
            return false;
        }

        foreach (var prerequisite in record.Prerequisites)
        {
            if (prerequisite.Length != 0 && (legend is null || !legend.ContainsKey(prerequisite)))
            {
                return false;
            }
        }

        return true;
    }

    private static int BitFor(int value) => value > 0 && value <= 31 ? 1 << (value - 1) : 0;
}
