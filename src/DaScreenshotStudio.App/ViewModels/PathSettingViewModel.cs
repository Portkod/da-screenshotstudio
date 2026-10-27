using CommunityToolkit.Mvvm.ComponentModel;
using DaScreenshotStudio.Engine.Staging;

namespace DaScreenshotStudio.App.ViewModels;

public enum PathKind
{
    /// <summary>A folder, picked with the folder picker.</summary>
    Folder,

    /// <summary>A file that has to exist to be useful, picked with the open picker.</summary>
    File,

    /// <summary>A file the studio writes, picked with the save picker so a new name can be given.</summary>
    WrittenFile,
}

public enum PathState
{
    Info,
    Good,
    Warning,
    Error,
}

/// <summary>What a path check found, and what to tell the user about it.</summary>
public readonly record struct PathCheck(PathState State, string Message)
{
    public static PathCheck Good(string message) => new(PathState.Good, message);

    public static PathCheck Info(string message) => new(PathState.Info, message);

    public static PathCheck Warning(string message) => new(PathState.Warning, message);

    public static PathCheck Error(string message) => new(PathState.Error, message);
}

/// <summary>
/// One path on the Settings tab. It is checked as it is typed or picked, so a wrong folder shows before it is
/// applied, and picked paths under the folder holding dass.json are kept relative so the settings can move with it.
/// </summary>
public sealed partial class PathSettingViewModel : ObservableObject
{
    private readonly StagingSettings _settings;
    private readonly Func<string, PathCheck> _check;

    public PathSettingViewModel(
        StagingSettings settings, string label, string description, PathKind kind, Func<string, PathCheck> check)
    {
        _settings = settings;
        _check = check;
        Label = label;
        Description = description;
        Kind = kind;
        Recheck();
    }

    public string Label { get; }

    public string Description { get; }

    public PathKind Kind { get; }

    public bool IsFolder => Kind == PathKind.Folder;

    [ObservableProperty]
    public partial string Value { get; set; } = "";

    [ObservableProperty]
    public partial PathState State { get; private set; }

    [ObservableProperty]
    public partial string Feedback { get; private set; } = "";

    /// <summary>The value resolved against the folder holding dass.json, or null when blank or malformed.</summary>
    public string? FullPath
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Value))
            {
                return null;
            }

            try
            {
                return _settings.Resolve(Value.Trim());
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return null;
            }
        }
    }

    public bool IsGood => State == PathState.Good;

    public bool IsWarning => State == PathState.Warning;

    public bool IsError => State == PathState.Error;

    public string Glyph => State switch
    {
        PathState.Good => "✓",
        PathState.Warning => "!",
        PathState.Error => "✕",
        _ => "i",
    };

    /// <summary>Where a picker should open: the path itself, or the nearest folder above it that exists.</summary>
    public string? StartFolder
    {
        get
        {
            var path = FullPath ?? _settings.BaseDirectory;
            if (Kind != PathKind.Folder || !Directory.Exists(path))
            {
                path = Path.GetDirectoryName(path);
            }

            while (path is not null && !Directory.Exists(path))
            {
                path = Path.GetDirectoryName(path);
            }

            return path;
        }
    }

    /// <summary>Takes a path from a picker, made relative when it sits under the folder holding dass.json.</summary>
    public void Pick(string path)
    {
        var relative = Path.GetRelativePath(_settings.BaseDirectory, path);
        var outside = relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
        Value = outside || Path.IsPathRooted(relative) ? path : relative;
    }

    public void Recheck()
    {
        var check = FullPath is { } path ? _check(path) : _check("");
        State = check.State;
        Feedback = check.Message;
    }

    partial void OnValueChanged(string value)
    {
        Recheck();
        OnPropertyChanged(nameof(FullPath));
    }

    partial void OnStateChanged(PathState value)
    {
        OnPropertyChanged(nameof(IsGood));
        OnPropertyChanged(nameof(IsWarning));
        OnPropertyChanged(nameof(IsError));
        OnPropertyChanged(nameof(Glyph));
    }
}
