using System.Globalization;
using DaScreenshotStudio.Assets.Metadata;
using DaScreenshotStudio.Engine.App;

namespace DaScreenshotStudio.App.ViewModels;

/// <summary>
/// The checks behind the Settings tab's feedback lines. Each looks for the files the studio will go on to read,
/// so the message says what is wrong with a path before the settings are applied.
/// </summary>
public static class SettingsChecks
{
    /// <summary>The archives the interface is drawn from; the client cannot be opened without them.</summary>
    private static readonly string[] InterfaceArchives = ["setoa.dat", "national.dat"];

    private static readonly EnumerationOptions AnyCase = new() { MatchCasing = MatchCasing.CaseInsensitive };

    public static PathCheck Client(string path)
    {
        if (path.Length == 0)
        {
            return PathCheck.Error("Required. Pick the folder the Dark Ages client is installed in.");
        }

        return Guard(() =>
        {
            if (!Directory.Exists(path))
            {
                return PathCheck.Error("There is no folder at this path.");
            }

            var files = new ClientDirectory(path);
            var missing = InterfaceArchives.Where(name => !File.Exists(files.PathOf(name))).ToList();
            if (missing.Count == InterfaceArchives.Length)
            {
                return PathCheck.Error("Not a client folder: no setoa.dat or national.dat. Pick the folder Darkages.exe is in.");
            }

            if (missing.Count > 0)
            {
                return PathCheck.Error($"{string.Join(" and ", missing)} is missing, so the interface cannot be drawn.");
            }

            if (!ClientAssets.MapArchiveNames.Any(name => File.Exists(files.PathOf(name))))
            {
                return PathCheck.Error($"None of {string.Join(", ", ClientAssets.MapArchiveNames)} is here, so maps cannot be drawn.");
            }

            return PathCheck.Good("Found the interface and map archives.");
        });
    }

    public static PathCheck Maps(string path)
    {
        if (path.Length == 0)
        {
            return PathCheck.Info("Blank, so the maps folder inside the client folder is read.");
        }

        return Guard(() =>
        {
            if (!Directory.Exists(path))
            {
                return PathCheck.Error("There is no folder at this path.");
            }

            var count = Directory.EnumerateFiles(path, "lod*.map", AnyCase).Count();
            return count == 0
                ? PathCheck.Warning("There are no lod*.map files in this folder.")
                : PathCheck.Good(string.Create(CultureInfo.InvariantCulture, $"{count} map files."));
        });
    }

    /// <summary>The metafile folder is optional: without one, the client's own metafile folder is read.</summary>
    public static PathCheck Metafiles(string path, string? clientPath)
    {
        return Guard(() =>
        {
            if (path.Length > 0 && Directory.Exists(path))
            {
                var count = Directory.EnumerateFiles(path).Count();
                return count == 0
                    ? PathCheck.Warning("The folder is empty.")
                    : PathCheck.Good(string.Create(CultureInfo.InvariantCulture, $"{count} metafiles."));
            }

            var fallback = clientPath is null ? null : new ClientDirectory(clientPath).PathOf(MetafileStore.DirectoryName);
            if (fallback is not null && Directory.Exists(fallback))
            {
                return PathCheck.Info("Optional. Not found, so the client's own metafile folder is read instead.");
            }

            return clientPath is null
                ? PathCheck.Info("Optional. Not found, so the client's own metafile folder will be read.")
                : PathCheck.Warning("Not found, and the client has no metafile folder either. Lighting and metafile tables are skipped.");
        });
    }

    public static PathCheck Items(string path) =>
        ExistingFile(path, "Not found, so the Items tab stays empty.");

    public static PathCheck MapCatalog(string path) =>
        WrittenFile(path, "Not created yet. It is written when you remember a map.");

    public static PathCheck Library(string path) =>
        WrittenFile(path, "Not created yet. It is written when you save a character.");

    private static PathCheck ExistingFile(string path, string missing)
    {
        if (path.Length == 0)
        {
            return PathCheck.Warning(missing);
        }

        return Guard(() =>
            File.Exists(path) ? PathCheck.Good("Found.")
            : Directory.Exists(path) ? PathCheck.Error("This is a folder. Pick a file.")
            : PathCheck.Warning(missing));
    }

    private static PathCheck WrittenFile(string path, string missing)
    {
        if (path.Length == 0)
        {
            return PathCheck.Error("Required, so there is somewhere to save it.");
        }

        return Guard(() =>
            File.Exists(path) ? PathCheck.Good("Found.")
            : Directory.Exists(path) ? PathCheck.Error("This is a folder. Give a file name.")
            : !Directory.Exists(Path.GetDirectoryName(path)) ? PathCheck.Error("The folder this file goes in does not exist.")
            : PathCheck.Info(missing));
    }

    private static PathCheck Guard(Func<PathCheck> check)
    {
        try
        {
            return check();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return PathCheck.Error(exception.Message);
        }
    }
}
