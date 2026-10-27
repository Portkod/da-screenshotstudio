using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DaScreenshotStudio.Assets.Metadata;
using DaScreenshotStudio.Engine.App;
using DaScreenshotStudio.Engine.Catalog;
using DaScreenshotStudio.Engine.Core;
using DaScreenshotStudio.Engine.Staging;

namespace DaScreenshotStudio.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject, IEditorHost, IDisposable
{
    private const int UndoDepth = 200;
    private static readonly TimeSpan CoalesceWindow = TimeSpan.FromMilliseconds(800);

    private readonly List<string> _undo = [];
    private readonly List<string> _redo = [];
    private string? _lastEditKey;
    private DateTime _lastEditAt;
    private bool _renderQueued;
    private SceneHost? _host;
    private Screenshot? _frame;
    private CancellationTokenSource? _search;
    private string? _followedClient;

    public MainViewModel()
    {
        Settings = StagingSettings.Locate(Directory.GetCurrentDirectory());
        ClientFolder = new PathSettingViewModel(
            Settings, "Client folder", "The Dark Ages 7.41 install folder.",
            PathKind.Folder, SettingsChecks.Client);
        MapsFolder = new PathSettingViewModel(
            Settings, "Maps folder", "Where the lod*.map files are.",
            PathKind.Folder, SettingsChecks.Maps);
        MetafileFolder = new PathSettingViewModel(
            Settings, "Metafile folder", "Light and the other metafile tables.",
            PathKind.Folder, path => SettingsChecks.Metafiles(path, ClientFolder.FullPath));
        MapCatalogFile = new PathSettingViewModel(
            Settings, "Map catalog file", "The names and sizes of the maps you have saved.",
            PathKind.WrittenFile, SettingsChecks.MapCatalog);
        ItemsFile = new PathSettingViewModel(
            Settings, "Items database", "The equipment list the Items tab and look matching use.",
            PathKind.File, SettingsChecks.Items);
        LibraryFile = new PathSettingViewModel(
            Settings, "Character library file", "The characters saved on the Library tab.",
            PathKind.WrittenFile, SettingsChecks.Library);
        ClientFolder.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PathSettingViewModel.Value))
            {
                FollowClientFolder();
                MetafileFolder.Recheck();
            }
        };

        Document = NewDocument();
        Hud = new HudViewModel(this, Document, Swatch, SpellIcon, SkillIcon);
        Sprites = new SpriteBrowserViewModel(() => _host);
        Frame = FrameBitmaps.CreateScreen();
        ReloadData();
        OpenHost();
        RebuildObjects();
        Render();
    }

    public StagingSettings Settings { get; }

    /// <summary>The creature and ground item sprite browser.</summary>
    public SpriteBrowserViewModel Sprites { get; }

    public SceneDocument Document { get; private set; }

    [ObservableProperty]
    public partial string? ScenePath { get; private set; }

    [ObservableProperty]
    public partial bool IsDirty { get; private set; }

    [ObservableProperty]
    public partial HudViewModel Hud { get; private set; }

    public ObservableCollection<SceneObjectViewModel> Objects { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection), nameof(SelectedIsAisling))]
    public partial SceneObjectViewModel? Selected { get; set; }

    public bool HasSelection => Selected is not null;

    public bool SelectedIsAisling => Selected?.IsAisling == true;

    /// <summary>The rendered screen with the overlay applied, redrawn in place.</summary>
    public WriteableBitmap Frame { get; }

    /// <summary>Raised after <see cref="Frame"/> has been redrawn, since the bitmap object stays the same.</summary>
    public event EventHandler? FrameUpdated;

    [ObservableProperty]
    public partial Screenshot? Reference { get; private set; }

    [ObservableProperty]
    public partial string ReferenceName { get; private set; } = "No reference";

    [ObservableProperty]
    public partial OverlayMode Overlay { get; set; } = OverlayMode.Scene;

    [ObservableProperty]
    public partial double BlendAmount { get; set; } = 0.5;

    [ObservableProperty]
    public partial int Zoom { get; set; } = 1;

    [ObservableProperty]
    public partial string DiffSummary { get; private set; } = "";

    [ObservableProperty]
    public partial string Status { get; private set; } = "";

    [ObservableProperty]
    public partial string HoverText { get; set; } = "";

    /// <summary>The text palette's colors that markup can name, plus plain white.</summary>
    public ObservableCollection<TextColorSwatch> TextColors { get; } = [];

    private readonly Dictionary<int, IBrush> _swatches = [];

    private readonly Dictionary<int, Bitmap?> _spellIcons = [];
    private readonly Dictionary<int, Bitmap?> _skillIcons = [];

    /// <summary>A skill icon for the skill book list.</summary>
    private Bitmap? SkillIcon(int icon)
    {
        if (_skillIcons.TryGetValue(icon, out var bitmap))
        {
            return bitmap;
        }

        bitmap = _host?.Ui.SkillIcons is { } bank && bank.Frame(icon).Image is { IsEmpty: false } art
            ? FrameBitmaps.FromPixmap(art)
            : null;
        _skillIcons[icon] = bitmap;
        return bitmap;
    }

    /// <summary>A spell icon for the effect list, at its full size.</summary>
    private Bitmap? SpellIcon(int icon)
    {
        if (_spellIcons.TryGetValue(icon, out var bitmap))
        {
            return bitmap;
        }

        bitmap = _host is not null && _host.TryGetSpellIcon(icon, out var art) ? FrameBitmaps.FromPixmap(art) : null;
        _spellIcons[icon] = bitmap;
        return bitmap;
    }

    private IBrush Swatch(int index)
    {
        if (_swatches.TryGetValue(index, out var brush))
        {
            return brush;
        }

        if (_host is null)
        {
            return Brushes.Transparent;
        }

        var (r, g, b) = Screenshot.Expand(Engine.Present.BmpImage.ToRgb555(_host.Ui.Palette[index]));
        brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        _swatches[index] = brush;
        return brush;
    }

    private void RefreshTextColors()
    {
        _swatches.Clear();
        TextColors.Clear();
        if (_host is null)
        {
            return;
        }

        var codes = new SortedDictionary<int, string>();
        for (var letter = (byte)'a'; letter <= 'x'; letter++)
        {
            if (TextMarkup.TryGetPaletteIndex(letter, out var index))
            {
                codes[index] = codes.TryGetValue(index, out var known) ? $"{known} {{={(char)letter}" : $"{{={(char)letter}";
            }
        }

        codes.TryAdd(0x10, "");
        foreach (var (index, code) in codes)
        {
            TextColors.Add(new TextColorSwatch(index, code, Swatch(index)));
        }
    }

    public static IReadOnlyList<OverlayMode> OverlayModes { get; } = Enum.GetValues<OverlayMode>();

    public static IReadOnlyList<int> ZoomLevels { get; } = [1, 2, 3, 4];

    // Catalogs.

    public MapCatalog Maps { get; private set; } = new();

    public ObservableCollection<MapEntryViewModel> MapEntries { get; } = [];

    public ObservableCollection<MapEntryViewModel> MapCandidatesFound { get; } = [];

    [ObservableProperty]
    public partial bool IsSearchingMaps { get; private set; }

    [ObservableProperty]
    public partial string MapEntryName { get; set; } = "";

    public ItemCatalog Items { get; private set; } = ItemCatalog.Empty;

    public ObservableCollection<ItemEntryViewModel> FilteredItems { get; } = [];

    public static IReadOnlyList<string> ItemCategories { get; } =
        ["All", .. Enum.GetNames<ItemCategory>()];

    [ObservableProperty]
    public partial string ItemFilter { get; set; } = "";

    [ObservableProperty]
    public partial string ItemCategoryFilter { get; set; } = "All";

    [ObservableProperty]
    public partial int Dye { get; set; }

    [ObservableProperty]
    public partial bool FitGenderOnly { get; set; } = true;

    public CharacterLibrary Library { get; private set; } = new();

    public ObservableCollection<LibraryEntryViewModel> LibraryEntries { get; } = [];

    // Settings fields, edited on their own tab and applied together.

    public PathSettingViewModel ClientFolder { get; }

    public PathSettingViewModel MapsFolder { get; }

    public PathSettingViewModel MetafileFolder { get; }

    public PathSettingViewModel MapCatalogFile { get; }

    public PathSettingViewModel ItemsFile { get; }

    public PathSettingViewModel LibraryFile { get; }

    public IReadOnlyList<PathSettingViewModel> ClientSettings => [ClientFolder, MapsFolder, MetafileFolder];

    public IReadOnlyList<PathSettingViewModel> DataSettings => [MapCatalogFile, ItemsFile, LibraryFile];

    /// <summary>Where Apply and save writes the settings.</summary>
    public string SettingsFile => Path.Combine(Settings.BaseDirectory, StagingSettings.FileName);

    /// <summary>Whether the client opened, so scenes can be drawn.</summary>
    [ObservableProperty]
    public partial bool IsClientReady { get; private set; }

    public string WindowTitle => $"Screenshot Studio - {(ScenePath is null ? "untitled" : Path.GetFileName(ScenePath))}{(IsDirty ? " *" : "")}";

    partial void OnScenePathChanged(string? value) => OnPropertyChanged(nameof(WindowTitle));

    partial void OnIsDirtyChanged(bool value) => OnPropertyChanged(nameof(WindowTitle));

    partial void OnOverlayChanged(OverlayMode value) => Compose();

    partial void OnBlendAmountChanged(double value) => Compose();

    partial void OnItemFilterChanged(string value) => FilterItems();

    partial void OnItemCategoryFilterChanged(string value) => FilterItems();

    partial void OnFitGenderOnlyChanged(bool value) => FilterItems();

    partial void OnSelectedChanged(SceneObjectViewModel? value)
    {
        RefreshPreview(value);
        FilterItems();
        WearItemCommand.NotifyCanExecuteChanged();
    }

    private static SceneDocument NewDocument() => new()
    {
        Hud = new SceneHud
        {
            Health = 100,
            MaxHealth = 100,
            Mana = 100,
            MaxMana = 100,
        },
    };

    // Startup and settings.

    private void ReloadData()
    {
        MapsFolder.Value = Settings.MapsPath;
        MetafileFolder.Value = Settings.MetafilePath;
        ClientFolder.Value = Settings.ClientPath;
        FollowClientFolder();
        MapCatalogFile.Value = Settings.MapCatalogPath;
        ItemsFile.Value = Settings.ItemsPath;
        LibraryFile.Value = Settings.LibraryPath;

        Maps = TryLoad(() => MapCatalog.Load(Settings.Resolve(Settings.MapCatalogPath)), new MapCatalog(), "map catalog");
        Items = File.Exists(Settings.Resolve(Settings.ItemsPath))
            ? TryLoad(() => ItemCatalog.Load(Settings.Resolve(Settings.ItemsPath)), ItemCatalog.Empty, "items")
            : ItemCatalog.Empty;
        Library = TryLoad(() => CharacterLibrary.Load(Settings.Resolve(Settings.LibraryPath)), new CharacterLibrary(), "library");

        RefreshMapEntries();
        FilterItems();
    }

    private T TryLoad<T>(Func<T> load, T fallback, string what)
    {
        try
        {
            return load();
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or System.Text.Json.JsonException or UnauthorizedAccessException)
        {
            Status = $"Could not read the {what}: {exception.Message}";
            return fallback;
        }
    }

    private void OpenHost()
    {
        _host?.Dispose();
        _host = null;
        IsClientReady = false;

        if (Settings.ClientPath.Length == 0)
        {
            Status = "Set the client folder on the Settings tab.";
            return;
        }

        try
        {
            _host = Settings.OpenHost();
            IsClientReady = true;
            Status = "Ready.";
            RefreshTextColors();
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
        {
            Status = $"Could not open the client: {exception.Message}";
        }

        RefreshLibrary();
        Sprites.Reload();
    }

    [RelayCommand]
    private void ApplySettings()
    {
        Settings.ClientPath = ClientFolder.Value.Trim();
        Settings.MapsPath = MapsFolder.Value.Trim();
        Settings.MetafilePath = MetafileFolder.Value.Trim();
        Settings.MapCatalogPath = MapCatalogFile.Value.Trim();
        Settings.ItemsPath = ItemsFile.Value.Trim();
        Settings.LibraryPath = LibraryFile.Value.Trim();

        try
        {
            Settings.Save();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Status = $"Could not save the settings: {exception.Message}";
        }

        ReloadData();
        _matchHost?.Dispose();
        _matchHost = null;
        OpenHost();
        Render();
    }

    private void FollowClientFolder()
    {
        var previous = _followedClient;
        _followedClient = ClientFolder.FullPath;
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        foreach (var (folder, name) in new[] { (MapsFolder, StagingSettings.ClientMapsFolder), (MetafileFolder, MetafileStore.DirectoryName) })
        {
            var given = previous is null ? null : new ClientDirectory(previous).PathOf(name);
            if (folder.Value.Length > 0 && !string.Equals(folder.FullPath, given, comparison))
            {
                continue;
            }

            if (_followedClient is null)
            {
                folder.Value = "";
            }
            else
            {
                folder.Pick(new ClientDirectory(_followedClient).PathOf(name));
            }
        }
    }

    // Rendering.

    public void SceneChanged()
    {
        IsDirty = true;
        QueueRender();
    }

    private void QueueRender()
    {
        if (_renderQueued)
        {
            return;
        }

        _renderQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _renderQueued = false;
            Render();
        }, DispatcherPriority.Background);
    }

    /// <summary>How far the view is moved off the player, in screen pixels. Never part of the scene.</summary>
    public int PanX { get; private set; }

    public int PanY { get; private set; }

    public bool IsPanned => PanX != 0 || PanY != 0;

    /// <summary>Moves the view to a pan, which drawing, picking and outlines all follow.</summary>
    public void SetPan(int x, int y)
    {
        if (x == PanX && y == PanY)
        {
            return;
        }

        PanX = x;
        PanY = y;
        OnPropertyChanged(nameof(IsPanned));
        QueueRender();
    }

    [RelayCommand]
    private void ResetView() => SetPan(0, 0);

    /// <summary>Runs work against the scene drawn without the pan, then puts the panned view back.</summary>
    private T Unpanned<T>(Func<SceneHost, T> work)
    {
        var host = _host!;
        host.PanX = 0;
        host.PanY = 0;
        try
        {
            SceneApplier.Apply(host, Document, Maps);
            return work(host);
        }
        finally
        {
            host.PanX = PanX;
            host.PanY = PanY;
            if (IsPanned)
            {
                Render();
            }
        }
    }

    /// <summary>Applies the document and redraws. Exactly what export writes, before the overlay.</summary>
    public void Render()
    {
        if (_host is null)
        {
            return;
        }

        var watch = Stopwatch.StartNew();
        try
        {
            _host.PanX = PanX;
            _host.PanY = PanY;
            SceneApplier.Apply(_host, Document, Maps);
            _frame = Screenshot.Decode(_host.EncodeBmp());
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or ArgumentException)
        {
            Status = exception.Message;
            return;
        }

        Compose();
        Status = string.Create(CultureInfo.InvariantCulture, $"Rendered in {watch.ElapsedMilliseconds} ms.");
        RefreshPreview(Selected);
    }

    private void Compose()
    {
        if (_frame is null)
        {
            return;
        }

        // A panned view no longer lines up with the reference, so it shows the scene alone.
        if (IsPanned)
        {
            FrameBitmaps.Compose(Frame, _frame, null, OverlayMode.Scene, 0);
            DiffSummary = $"View panned {PanX},{PanY}. Home returns to the player.";
        }
        else
        {
            FrameBitmaps.Compose(Frame, _frame, Reference, Overlay, BlendAmount);
            UpdateDiff();
        }

        FrameUpdated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Whether the scene, drawn as export writes it, equals the reference pixel for pixel.</summary>
    private bool IsExactMatch()
    {
        if (_host is null || Reference is not { } reference)
        {
            return false;
        }

        var frame = IsPanned ? Unpanned(host => Screenshot.Decode(host.EncodeBmp())) : _frame;
        return frame is not null && reference.Width == frame.Width && reference.Pixels.AsSpan().SequenceEqual(frame.Pixels);
    }

    private void UpdateDiff()
    {
        if (_frame is null || Reference is null)
        {
            DiffSummary = "";
            return;
        }

        if (Reference.Width != _frame.Width || Reference.Height != _frame.Height)
        {
            DiffSummary = "Reference size differs";
            return;
        }

        var diff = ScreenshotDiff.Compare(Reference, _frame);
        DiffSummary = diff.IsExact
            ? "Exact match"
            : string.Create(CultureInfo.InvariantCulture, $"{diff.Count:N0} pixels differ");
    }

    // Undo.

    public void BeginEdit(string key)
    {
        var now = DateTime.UtcNow;
        if (key == _lastEditKey && now - _lastEditAt < CoalesceWindow)
        {
            _lastEditAt = now;
            return;
        }

        _lastEditKey = key;
        _lastEditAt = now;
        PushUndo();
    }

    private void PushUndo()
    {
        _undo.Add(Snapshot());
        if (_undo.Count > UndoDepth)
        {
            _undo.RemoveAt(0);
        }

        _redo.Clear();
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    private string Snapshot() => Document.ToJson();

    private bool CanUndo() => _undo.Count > 0;

    private bool CanRedo() => _redo.Count > 0;

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo()
    {
        _redo.Add(Snapshot());
        Restore(_undo[^1]);
        _undo.RemoveAt(_undo.Count - 1);
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo()
    {
        _undo.Add(Snapshot());
        Restore(_redo[^1]);
        _redo.RemoveAt(_redo.Count - 1);
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    private void Restore(string snapshot)
    {
        var selected = Selected is null ? -1 : Objects.IndexOf(Selected);
        Document = SceneDocument.FromJson(snapshot);
        _lastEditKey = null;
        Hud = new HudViewModel(this, Document, Swatch, SpellIcon, SkillIcon);
        RebuildObjects();
        RefreshDoors();
        Selected = selected >= 0 && selected < Objects.Count ? Objects[selected] : null;
        IsDirty = true;
        Render();
    }

    /// <summary>The player, which is always the first entry of <see cref="Objects"/>.</summary>
    public SceneObjectViewModel Player => Objects[0];

    private void RebuildObjects()
    {
        Objects.Clear();
        Objects.Add(new SceneObjectViewModel(this, Document.Player, isPlayer: true));
        foreach (var staged in Document.Objects)
        {
            Objects.Add(new SceneObjectViewModel(this, staged));
        }
    }

    // Files.

    [RelayCommand]
    private void New()
    {
        LoadDocument(NewDocument(), null);
        Reference = null;
        ReferenceName = "No reference";
        Compose();
    }

    public void OpenScene(string path)
    {
        try
        {
            LoadDocument(SceneDocument.Load(path), path);
        }
        catch (Exception exception) when (exception is IOException or System.Text.Json.JsonException or InvalidDataException)
        {
            Status = $"Could not open the scene: {exception.Message}";
            return;
        }

        if (Document.ReferencePathFrom(Path.GetDirectoryName(Path.GetFullPath(path))!) is { } reference)
        {
            LoadReference(reference, remember: false);
        }
    }

    private void LoadDocument(SceneDocument document, string? path)
    {
        Document = document;
        ScenePath = path;
        _undo.Clear();
        _redo.Clear();
        _lastEditKey = null;
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
        Hud = new HudViewModel(this, Document, Swatch, SpellIcon, SkillIcon);
        RebuildObjects();
        RefreshDoors();
        Selected = null;
        IsDirty = false;
        Render();
    }

    public void SaveScene(string path)
    {
        try
        {
            if (Reference is not null && ReferencePath is { } referencePath)
            {
                Document.Reference = SceneDocument.PortablePath(
                    Path.GetRelativePath(Path.GetDirectoryName(Path.GetFullPath(path))!, referencePath));
            }

            Document.Verified = IsExactMatch();
            Document.Save(path);
            ScenePath = path;
            IsDirty = false;
            Status = $"Saved {Path.GetFileName(path)}.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Status = $"Could not save: {exception.Message}";
        }
    }

    /// <summary>Writes the screenshot exactly as the client would.</summary>
    public void Export(string path)
    {
        if (_host is null)
        {
            Status = "Nothing to export without a client folder.";
            return;
        }

        try
        {
            File.WriteAllBytes(path, Unpanned(host => host.EncodeBmp()));
            Status = $"Exported {Path.GetFileName(path)}.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            Status = $"Could not export: {exception.Message}";
        }
    }

    public string? ReferencePath { get; private set; }

    public void LoadReference(string path, bool remember = true)
    {
        try
        {
            Reference = Screenshot.Read(path);
            ReferencePath = Path.GetFullPath(path);
            ReferenceName = Path.GetFileName(path);
            if (remember)
            {
                IsDirty = true;
            }

            if (Overlay == OverlayMode.Scene)
            {
                Overlay = OverlayMode.Difference;
            }
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException)
        {
            Status = $"Could not read the reference: {exception.Message}";
            return;
        }

        Compose();
    }

    [RelayCommand]
    private void ClearReference()
    {
        Reference = null;
        ReferencePath = null;
        ReferenceName = "No reference";
        Document.Reference = null;
        Compose();
    }

    // Objects.

    /// <summary>New objects stand beside the player rather than on top of it.</summary>
    private (int X, int Y) BesidePlayer => (Document.Player.X + 1, Document.Player.Y);

    [RelayCommand]
    private void AddAisling() => AddObject(new SceneObject
    {
        Kind = SceneObjectKind.Aisling,
        X = BesidePlayer.X,
        Y = BesidePlayer.Y,
        Look = new AislingLook { Head = 1, Armor = 1, Arms = 1, Boots = 1 },
    });

    [RelayCommand]
    private void AddCreature() => AddObject(new SceneObject
    {
        Kind = SceneObjectKind.Creature,
        X = BesidePlayer.X,
        Y = BesidePlayer.Y,
        Sprite = 1,
    });

    [RelayCommand]
    private void AddItem() => AddObject(new SceneObject
    {
        Kind = SceneObjectKind.Item,
        X = BesidePlayer.X,
        Y = BesidePlayer.Y,
        Sprite = 1,
    });

    public void AddObject(SceneObject staged)
    {
        PushUndo();
        _lastEditKey = null;
        Document.Objects.Add(staged);
        var view = new SceneObjectViewModel(this, staged);
        Objects.Add(view);
        Selected = view;
        SceneChanged();
    }

    [RelayCommand]
    private void DeleteSelected()
    {
        if (Selected is not { } selected)
        {
            return;
        }

        if (selected.IsPlayer)
        {
            Status = "Every scene requires a player. Restyle it instead.";
            return;
        }

        PushUndo();
        _lastEditKey = null;
        var index = Objects.IndexOf(selected);
        Document.Objects.Remove(selected.Model);
        Objects.Remove(selected);
        Selected = Objects.Count == 0 ? null : Objects[Math.Min(index, Objects.Count - 1)];
        SceneChanged();
    }

    [RelayCommand]
    private void DuplicateSelected()
    {
        if (Selected is not { } selected)
        {
            return;
        }

        var copy = new SceneDocument { Objects = [selected.Model] }.Clone().Objects[0];
        copy.X++;
        AddObject(copy);
    }

    /// <summary>Moves an object one step later in the list, which only matters for overlapping balloons.</summary>
    [RelayCommand]
    private void MoveSelectedUp() => Reorder(-1);

    [RelayCommand]
    private void MoveSelectedDown() => Reorder(1);

    private void Reorder(int delta)
    {
        if (Selected is not { IsPlayer: false } selected)
        {
            return;
        }

        // The player holds the first row, so the document's own list starts at row one.
        var index = Objects.IndexOf(selected);
        var target = index + delta;
        if (target < 1 || target >= Objects.Count)
        {
            return;
        }

        PushUndo();
        _lastEditKey = null;
        Objects.Move(index, target);
        Document.Objects.RemoveAt(index - 1);
        Document.Objects.Insert(target - 1, selected.Model);
        Selected = selected;
        SceneChanged();
    }

    /// <summary>
    /// Steps the selection by whole cells, or the player when nothing is selected, which takes the
    /// camera with it.
    /// </summary>
    public void Nudge(int dx, int dy)
    {
        var target = Selected ?? Player;
        BeginEdit($"nudge:{target.GetHashCode()}");
        target.Model.X += dx;
        target.Model.Y += dy;
        target.RefreshAll();
        SceneChanged();
    }

    public void Rotate()
    {
        if (Selected is not { } selected)
        {
            return;
        }

        selected.Direction = (Direction)(((int)selected.Direction + 1) % 4);
    }

    /// <summary>Puts an object on a cell, as a drag on the canvas does.</summary>
    public void MoveTo(SceneObjectViewModel target, int x, int y)
    {
        if (target.Model.X == x && target.Model.Y == y)
        {
            return;
        }

        BeginEdit($"drag:{target.GetHashCode()}");
        target.Model.X = x;
        target.Model.Y = y;
        target.RefreshAll();
        SceneChanged();
    }

    /// <summary>The object drawn under a screen pixel in the current frame.</summary>
    public SceneObjectViewModel? ObjectAt(int screenX, int screenY) =>
        _host is not null
        && _host.TryPickObject(screenX, screenY, out var entity)
        && SceneApplier.IndexOf(entity) is { } index
        && index + 1 < Objects.Count
            ? Objects[index + 1]
            : null;

    /// <summary>The doors the scene stages, for the list on the Scene and HUD tab.</summary>
    public ObservableCollection<string> DoorEntries { get; } = [];

    private void RefreshDoors()
    {
        DoorEntries.Clear();
        foreach (var door in Document.Map.Doors)
        {
            DoorEntries.Add($"{door.X},{door.Y} {(door.Left ? "left" : "right")} wall: {(door.Open ? "open" : "shut")}");
        }
    }

    /// <summary>
    /// Opens or shuts the door drawn under a screen pixel. A door put back the way the map file has it
    /// leaves the list.
    /// </summary>
    public void ToggleDoorAt(int screenX, int screenY)
    {
        if (_host is null || !_host.TryPickWall(screenX, screenY, out var x, out var y, out var left))
        {
            Status = "No wall under the pointer.";
            return;
        }

        if (!_host.TryGetDoor(x, y, left, out var openInFile, out var openNow))
        {
            Status = "That wall is not a door.";
            return;
        }

        PushUndo();
        _lastEditKey = null;
        var doors = Document.Map.Doors;
        doors.RemoveAll(d => d.X == x && d.Y == y && d.Left == left);
        if (!openNow != openInFile)
        {
            doors.Add(new SceneDoor { X = x, Y = y, Left = left, Open = !openNow });
        }

        RefreshDoors();
        Status = $"Door at {x},{y} is now {(openNow ? "shut" : "open")}.";
        SceneChanged();
    }

    [ObservableProperty]
    public partial bool IsMatchingPose { get; private set; }

    public static IReadOnlyList<string> LookSlots { get; } = ["Whole look", .. Enum.GetNames<LookSlot>()];

    [ObservableProperty]
    public partial string LookSlotToMatch { get; set; } = "Whole look";

    private CancellationTokenSource? _lookSearch;

    /// <summary>
    /// Searches the selected aisling's look against the reference, one slot from the equipment database
    /// or every slot in turn, and applies whatever improves the match.
    /// </summary>
    [RelayCommand]
    private async Task MatchLookAsync()
    {
        if (_lookSearch is not null)
        {
            _lookSearch.Cancel();
            return;
        }

        if (Selected is not { IsAisling: true } target)
        {
            Status = "Select an aisling to match its look.";
            return;
        }

        if (Reference is not { } reference)
        {
            Status = "Load a reference screenshot first.";
            return;
        }

        if (Items.Items.Count == 0)
        {
            Status = "The look is matched against Items.json, which is not loaded.";
            return;
        }

        var index = target.IsPlayer ? -1 : Document.Objects.IndexOf(target.Model);
        var scene = Document.Clone();
        var settings = Settings;
        var maps = Maps;
        var catalog = Items;
        var whole = !Enum.TryParse<LookSlot>(LookSlotToMatch, out var slot);
        var progress = new Progress<string>(message => Status = message);

        _lookSearch = new CancellationTokenSource();
        var token = _lookSearch.Token;
        IsMatchingPose = true;
        Status = whole ? "Matching the whole look..." : $"Matching {slot}...";

        try
        {
            var result = await Task.Run(() =>
            {
                _matchHost ??= settings.OpenHost();
                return whole
                    ? LookMatcher.MatchLook(_matchHost, scene, index, catalog, reference, maps, progress, cancellation: token)
                    : LookMatcher.MatchSlot(_matchHost, scene, index, slot, catalog, reference, maps, token);
            }, token);

            PushUndo();
            _lastEditKey = null;
            target.Model.Look = result.Look.Clone();
            target.RefreshAll();
            SceneChanged();
            Status = string.Create(
                CultureInfo.InvariantCulture,
                $"{(whole ? "Whole look" : slot.ToString())}: {result.Label}. {result.Differing:N0} pixels differ around the figure, {result.Tried} looks tried.");
        }
        catch (OperationCanceledException)
        {
            Status = "Look matching stopped.";
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or ArgumentException)
        {
            Status = $"Could not match the look: {exception.Message}";
        }
        finally
        {
            _lookSearch.Dispose();
            _lookSearch = null;
            IsMatchingPose = false;
        }
    }

    private SceneHost? _matchHost;

    /// <summary>
    /// Tries every frame the selected figure could be caught in and keeps the one that best matches the
    /// reference. The renderer it draws with is its own, so the editor stays responsive.
    /// </summary>
    [RelayCommand]
    private async Task MatchPoseAsync()
    {
        if (IsMatchingPose)
        {
            return;
        }

        if (Selected is not { IsPosable: true } target)
        {
            Status = "Select a figure to match its pose.";
            return;
        }

        if (Reference is not { } reference)
        {
            Status = "Load a reference screenshot first.";
            return;
        }

        var index = target.IsPlayer ? -1 : Document.Objects.IndexOf(target.Model);
        var scene = Document.Clone();
        var settings = Settings;
        var maps = Maps;

        IsMatchingPose = true;
        Status = "Matching pose...";

        try
        {
            var result = await Task.Run(() =>
            {
                _matchHost ??= settings.OpenHost();
                return PoseMatcher.Find(_matchHost, scene, index, reference, maps);
            });

            var model = target.Model;
            var pose = result.Pose;
            if (model.WalkFrame != pose.WalkFrame || model.IdleStep != pose.IdleStep || model.IdleSegment != pose.IdleSegment)
            {
                PushUndo();
                _lastEditKey = null;
                model.WalkFrame = pose.WalkFrame;
                model.IdleStep = pose.IdleStep;
                model.IdleSegment = pose.IdleSegment;
                target.RefreshAll();
                SceneChanged();
            }

            var segment = pose.IdleSegment is { } s ? $", idle segment {s}" : "";
            Status = string.Create(
                CultureInfo.InvariantCulture,
                $"Best of {result.Tried} poses: walk frame {pose.WalkFrame}, idle step {pose.IdleStep}{segment}, {result.Differing:N0} pixels differ.");
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or ArgumentException)
        {
            Status = $"Could not match the pose: {exception.Message}";
        }
        finally
        {
            IsMatchingPose = false;
        }
    }

    /// <summary>Finds which ground item sprite and dye the selected item shows in the reference.</summary>
    [RelayCommand]
    private async Task MatchItemAsync()
    {
        if (_lookSearch is not null)
        {
            _lookSearch.Cancel();
            return;
        }

        if (Selected is not { Kind: SceneObjectKind.Item } target)
        {
            Status = "Select a ground item to match its sprite.";
            return;
        }

        if (Reference is not { } reference)
        {
            Status = "Load a reference screenshot first.";
            return;
        }

        var index = Document.Objects.IndexOf(target.Model);
        var scene = Document.Clone();
        var settings = Settings;
        var maps = Maps;
        var dyes = ItemMatcher.Dyes(Items);
        var progress = new Progress<string>(message => Status = message);

        _lookSearch = new CancellationTokenSource();
        var token = _lookSearch.Token;
        IsMatchingPose = true;
        Status = "Matching the item...";

        try
        {
            var result = await Task.Run(() =>
            {
                _matchHost ??= settings.OpenHost();
                return ItemMatcher.Find(_matchHost, scene, index, reference, dyes, maps, progress, token);
            }, token);

            PushUndo();
            _lastEditKey = null;
            target.Model.Sprite = result.Sprite;
            target.Model.Color = result.Color;
            target.RefreshAll();
            SceneChanged();
            Status = string.Create(
                CultureInfo.InvariantCulture,
                $"Item sprite {result.Sprite}, dye {result.Color}: {result.Differing:N0} pixels differ on its tile. Sprites run to {result.Ceiling}.");
        }
        catch (OperationCanceledException)
        {
            Status = "Item matching stopped.";
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or ArgumentException)
        {
            Status = $"Could not match the item: {exception.Message}";
        }
        finally
        {
            _lookSearch.Dispose();
            _lookSearch = null;
            IsMatchingPose = false;
        }
    }

    /// <summary>Adds the ground items the reference shows on cells the scene leaves bare.</summary>
    [RelayCommand]
    private async Task FindLooseItemsAsync()
    {
        if (_lookSearch is not null)
        {
            _lookSearch.Cancel();
            return;
        }

        if (Reference is not { } reference)
        {
            Status = "Load a reference screenshot first.";
            return;
        }

        var scene = Document.Clone();
        var settings = Settings;
        var maps = Maps;
        var dyes = ItemMatcher.Dyes(Items);
        var progress = new Progress<string>(message => Status = message);

        _lookSearch = new CancellationTokenSource();
        var token = _lookSearch.Token;
        IsMatchingPose = true;
        Status = "Looking for loose items...";

        try
        {
            var found = await Task.Run(() =>
            {
                _matchHost ??= settings.OpenHost();
                return ItemMatcher.FindLoose(_matchHost, scene, reference, dyes, maps, progress, token);
            }, token);

            if (found.Count == 0)
            {
                Status = "No loose items found.";
                return;
            }

            PushUndo();
            _lastEditKey = null;
            foreach (var loose in found)
            {
                var item = new SceneObject
                {
                    Kind = SceneObjectKind.Item,
                    Name = $"Found item {loose.Sprite}",
                    X = loose.X,
                    Y = loose.Y,
                    Sprite = loose.Sprite,
                    Color = loose.Color,
                };
                Document.Objects.Add(item);
                Objects.Add(new SceneObjectViewModel(this, item));
            }

            SceneChanged();
            Status = $"Added {found.Count} items: " + string.Join(
                ", ", found.Select(f => $"{f.Sprite} at {f.X},{f.Y} ({f.Before} to {f.After} px)"));
        }
        catch (OperationCanceledException)
        {
            Status = "Item search stopped.";
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or ArgumentException)
        {
            Status = $"Could not search for items: {exception.Message}";
        }
        finally
        {
            _lookSearch.Dispose();
            _lookSearch = null;
            IsMatchingPose = false;
        }
    }

    /// <summary>
    /// Matches the effect strip's icons and the book the lower panel shows against the reference, slot
    /// by slot, and writes what it finds into the scene.
    /// </summary>
    [RelayCommand]
    private async Task MatchIconsAsync()
    {
        if (_lookSearch is not null)
        {
            _lookSearch.Cancel();
            return;
        }

        if (Reference is not { } reference)
        {
            Status = "Load a reference screenshot first.";
            return;
        }

        var scene = Document.Clone();
        var settings = Settings;
        var maps = Maps;
        var dyes = ItemMatcher.Dyes(Items);
        var progress = new Progress<string>(text => Status = text);

        _lookSearch = new CancellationTokenSource();
        var token = _lookSearch.Token;
        IsMatchingPose = true;
        Status = "Matching icons...";

        try
        {
            var report = await Task.Run(() =>
            {
                _matchHost ??= settings.OpenHost();
                return IconMatcher.MatchAll(_matchHost, scene, reference, dyes, maps, progress, token);
            }, token);

            // The matches only ever change the interface's banked parts, so those are what is taken.
            PushUndo();
            _lastEditKey = null;
            var found = report.Scene.Hud;
            Document.Hud.StatusEffects = found.StatusEffects;
            Document.Hud.Spells = found.Spells;
            Document.Hud.Skills = found.Skills;
            Document.Hud.Inventory = found.Inventory;
            Document.Hud.Equipment = found.Equipment;
            Document.Hud.Window.Profile.Legend = found.Window.Profile.Legend;

            Hud = new HudViewModel(this, Document, Swatch, SpellIcon, SkillIcon);
            SceneChanged();
            Status = $"Matched {report.Lines.Count} slots, {report.Differing} pixels left in them.";
        }
        catch (OperationCanceledException)
        {
            Status = "Icon matching stopped.";
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or ArgumentException)
        {
            Status = $"Could not match icons: {exception.Message}";
        }
        finally
        {
            _lookSearch.Dispose();
            _lookSearch = null;
            IsMatchingPose = false;
        }
    }

    [RelayCommand]
    private void ClearDoors()
    {
        if (Document.Map.Doors.Count == 0)
        {
            return;
        }

        PushUndo();
        _lastEditKey = null;
        Document.Map.Doors.Clear();
        RefreshDoors();
        SceneChanged();
    }

    public (int X, int Y)? CellAt(int screenX, int screenY)
    {
        if (_host is null)
        {
            return null;
        }

        _host.CellAt(screenX, screenY, out var x, out var y);
        return (x, y);
    }

    public (int X, int Y)? CellCenter(int cellX, int cellY) => _host?.CellCenter(cellX, cellY);

    public Engine.Rendering.PixelRect? ViewRectangle => _host?.ViewRectangle;

    private void RefreshPreview(SceneObjectViewModel? target)
    {
        if (target is null || _host is null)
        {
            return;
        }

        target.Preview = _host.TryPreview(target.Model, out var art) ? FrameBitmaps.FromPixmap(art) : null;

        if (target.IsCreature)
        {
            var segments = _host.StandingSegments(target.Model.Sprite);
            var parts = new List<string>();
            for (var i = 0; i < segments.Length; i++)
            {
                parts.Add($"segment {i}: {segments[i].Steps} steps, {segments[i].Chance}% roll");
            }

            target.IdleInfo = parts.Count == 0 ? "This sheet has no idle animation." : string.Join("\n", parts);
        }
    }

    // Items.

    private void FilterItems()
    {
        FilteredItems.Clear();
        var text = ItemFilter.Trim();
        var gender = Selected?.Model.Look is { } look ? Dressing.GenderOf(look) : (ItemGender?)null;
        var category = Enum.TryParse<ItemCategory>(ItemCategoryFilter, out var parsed) ? parsed : (ItemCategory?)null;

        foreach (var item in Items.Items)
        {
            if (category is { } wanted && item.Category != wanted)
            {
                continue;
            }

            if (FitGenderOnly && gender is { } figure && !item.Fits(figure))
            {
                continue;
            }

            if (text.Length > 0
                && !item.Name.Contains(text, StringComparison.OrdinalIgnoreCase)
                && !(item.AlternativeNames?.Any(name => name.Contains(text, StringComparison.OrdinalIgnoreCase)) ?? false)
                && item.FileId.ToString(CultureInfo.InvariantCulture) != text)
            {
                continue;
            }

            FilteredItems.Add(new ItemEntryViewModel(item));
            if (FilteredItems.Count >= 500)
            {
                break;
            }
        }
    }

    private bool CanWear(ItemEntryViewModel? item) => item is not null && Selected?.IsAisling == true;

    [RelayCommand(CanExecute = nameof(CanWear))]
    private void WearItem(ItemEntryViewModel? item)
    {
        if (item is not null && Selected is { IsAisling: true } selected)
        {
            Wear(selected, item.Model);
        }
    }

    /// <summary>Dresses an aisling in an item, as dropping the item on it does.</summary>
    public void Wear(SceneObjectViewModel target, CatalogItem item)
    {
        if (!target.IsAisling)
        {
            return;
        }

        PushUndo();
        _lastEditKey = null;
        Dressing.Wear(target.Model.Look ??= new AislingLook(), item, (byte)Math.Clamp(Dye, 0, 255));
        target.RefreshAll();
        Selected = target;
        Status = $"{target.Model.Name} now wears {item.Name}.";
        SceneChanged();
    }

    // Sprites.

    /// <summary>Gives a creature or ground item another sprite and dye, as dropping one on it does.</summary>
    public void ApplySprite(SceneObjectViewModel target, SpritePick pick)
    {
        if (target.Kind != pick.Kind)
        {
            return;
        }

        PushUndo();
        _lastEditKey = null;
        target.Model.Sprite = (ushort)pick.Sprite;
        target.Model.Color = pick.Dye;
        target.RefreshAll();
        Selected = target;
        Status = $"{target.Title} is now sprite {pick.Sprite}.";
        SceneChanged();
    }

    /// <summary>Places a new creature or ground item from the browser on a cell.</summary>
    public void PlaceSprite(SpritePick pick, int x, int y) => AddObject(new SceneObject
    {
        Kind = pick.Kind,
        X = x,
        Y = y,
        Sprite = (ushort)pick.Sprite,
        Color = pick.Dye,
        Direction = pick.Kind == SceneObjectKind.Creature ? pick.Facing : Direction.Down,
    });

    /// <summary>
    /// What double-clicking a thumbnail does: the selected figure of the same kind takes the sprite, and
    /// without one a new figure is placed beside the player.
    /// </summary>
    [RelayCommand]
    private void UseSprite(SpriteTileViewModel? tile)
    {
        if (tile is null)
        {
            return;
        }

        var pick = Sprites.PickOf(tile);
        if (Selected is { } selected && selected.Kind == pick.Kind)
        {
            ApplySprite(selected, pick);
        }
        else
        {
            PlaceSprite(pick, BesidePlayer.X, BesidePlayer.Y);
        }
    }

    /// <summary>Shows the selected figure's sprite in the browser.</summary>
    [RelayCommand]
    private void BrowseSprite()
    {
        if (Selected is { IsSprite: true } selected)
        {
            Sprites.Reveal(selected.Kind, selected.Sprite);
        }
    }

    [RelayCommand]
    private void PreviousSprite() => StepSprite(-1);

    [RelayCommand]
    private void NextSprite() => StepSprite(1);

    /// <summary>Moves the selected figure to the neighboring sprite that has art.</summary>
    private void StepSprite(int direction)
    {
        if (Selected is not { IsSprite: true } selected)
        {
            return;
        }

        if (Sprites.Step(selected.Kind, selected.Sprite, direction) is { } sprite)
        {
            selected.Sprite = sprite;
        }
        else
        {
            Status = direction > 0 ? "That is the last sprite." : "That is the first sprite.";
        }
    }

    // Library.

    private void RefreshLibrary()
    {
        LibraryEntries.Clear();
        foreach (var saved in Library.Characters)
        {
            Bitmap? preview = null;
            if (_host is not null
                && _host.TryPreview(new SceneObject { Kind = SceneObjectKind.Aisling, Look = saved.Look }, out var art))
            {
                preview = FrameBitmaps.FromPixmap(art);
            }

            LibraryEntries.Add(new LibraryEntryViewModel(saved, preview));
        }
    }

    [RelayCommand]
    private void SaveToLibrary()
    {
        if (Selected is not { IsAisling: true } selected)
        {
            Status = "Select an aisling to save it.";
            return;
        }

        var name = selected.Model.Name.Length > 0 ? selected.Model.Name : $"Character {Library.Characters.Count + 1}";
        var existing = Library.Characters.FindIndex(c => c.Name == name);
        var saved = new SavedCharacter { Name = name, Look = selected.Model.Look!.Clone() };

        if (existing >= 0)
        {
            Library.Characters[existing] = saved;
        }
        else
        {
            Library.Characters.Add(saved);
        }

        PersistLibrary();
        Status = $"Saved {name} to the library.";
    }

    [RelayCommand]
    private void RemoveFromLibrary(LibraryEntryViewModel? entry)
    {
        if (entry is null)
        {
            return;
        }

        Library.Characters.Remove(entry.Model);
        PersistLibrary();
    }

    private void PersistLibrary()
    {
        try
        {
            Library.Save(Settings.Resolve(Settings.LibraryPath));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Status = $"Could not save the library: {exception.Message}";
        }

        RefreshLibrary();
    }

    /// <summary>Places a saved character, or applies its look to an aisling dropped on.</summary>
    public void PlaceCharacter(SavedCharacter saved, int x, int y, SceneObjectViewModel? onto)
    {
        if (onto is { IsAisling: true })
        {
            PushUndo();
            _lastEditKey = null;
            onto.Model.Look = saved.Look.Clone();
            if (onto.Model.Name.Length == 0)
            {
                onto.Model.Name = saved.Name;
            }

            onto.RefreshAll();
            Selected = onto;
            SceneChanged();
            return;
        }

        AddObject(new SceneObject
        {
            Kind = SceneObjectKind.Aisling,
            Name = saved.Name,
            X = x,
            Y = y,
            Look = saved.Look.Clone(),
        });
    }

    [RelayCommand]
    private void PlaceFromLibrary(LibraryEntryViewModel? entry)
    {
        if (entry is not null)
        {
            PlaceCharacter(entry.Model, BesidePlayer.X, BesidePlayer.Y, null);
        }
    }

    // Maps.

    private void RefreshMapEntries()
    {
        MapEntries.Clear();
        foreach (var entry in Maps.Entries)
        {
            MapEntries.Add(new MapEntryViewModel(entry.Id, entry.Name, entry.Width, entry.Height));
        }
    }

    [RelayCommand]
    private void UseMap(MapEntryViewModel? entry)
    {
        if (entry is null)
        {
            return;
        }

        BeginEdit("map");
        _lastEditKey = null;
        Document.Map.Id = entry.Id;
        Document.Map.Width = entry.Width;
        Document.Map.Height = entry.Height;
        if (entry.Score is not null)
        {
            // The search found the camera mid-step, which is the player's own step.
            Document.Player.OffsetX = entry.SlideX;
            Document.Player.OffsetY = entry.SlideY;
            Player.RefreshAll();
        }
        if (entry.Name.Length > 0 && Document.Hud.Zone.Length == 0)
        {
            Document.Hud.Zone = entry.Name;
        }

        Hud.RefreshAll();
        SceneChanged();
    }

    /// <summary>Records the scene's map number and size, with a name, in the catalog.</summary>
    [RelayCommand]
    private void RememberMap()
    {
        var (width, height) = SceneApplier.MapSize(Document.Map, Maps);
        if (Document.Map.Id <= 0 || width <= 0 || height <= 0)
        {
            Status = "Set a map number, width and height first.";
            return;
        }

        var name = MapEntryName.Length > 0 ? MapEntryName : Document.Hud.Zone;
        Maps.Set(new MapEntry { Id = Document.Map.Id, Name = name, Width = width, Height = height });

        try
        {
            Maps.Save(Settings.Resolve(Settings.MapCatalogPath));
            Status = $"Remembered lod{Document.Map.Id} as {name}, {width}x{height}.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Status = $"Could not save the map catalog: {exception.Message}";
        }

        RefreshMapEntries();
    }

    [RelayCommand]
    private void ForgetMap(MapEntryViewModel? entry)
    {
        if (entry is null || !Maps.Remove(entry.Id))
        {
            return;
        }

        Maps.Save(Settings.Resolve(Settings.MapCatalogPath));
        RefreshMapEntries();
    }

    /// <summary>
    /// Ranks every map file and shape by how much of the reference's world view it reproduces with the
    /// camera on the scene's cell. Characters cover little of the view, so the right map stands out.
    /// </summary>
    [RelayCommand]
    private async Task FindMapAsync()
    {
        if (IsSearchingMaps)
        {
            _search?.Cancel();
            return;
        }

        if (Reference is not { } reference)
        {
            Status = "Load a reference screenshot first, and stand the player on the X and Y it shows.";
            return;
        }

        _search = new CancellationTokenSource();
        var token = _search.Token;
        var settings = Settings;
        var mapsPath = Settings.MapsDirectory;
        var x = Document.Player.X;
        var y = Document.Player.Y;

        IsSearchingMaps = true;
        MapCandidatesFound.Clear();
        Status = "Searching maps...";

        try
        {
            var results = await Task.Run(() => SearchMaps(settings, mapsPath, reference, x, y, token), token);
            foreach (var (map, width, height, score, slideX, slideY) in results)
            {
                MapCandidatesFound.Add(new MapEntryViewModel(map, Maps.Find(map)?.Name ?? "", width, height, score, slideX, slideY));
            }

            Status = results.Count == 0 ? "No map holds that cell." : "Pick a candidate to use it.";
        }
        catch (OperationCanceledException)
        {
            Status = "Map search stopped.";
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException)
        {
            Status = $"Map search failed: {exception.Message}";
        }
        finally
        {
            IsSearchingMaps = false;
            _search.Dispose();
            _search = null;
        }
    }

    private List<(int Map, int Width, int Height, double Score, int SlideX, int SlideY)> SearchMaps(
        StagingSettings settings, string mapsPath, Screenshot reference, int x, int y, CancellationToken token)
    {
        using var host = settings.OpenHost();
        var results = new List<(int Map, int Width, int Height, double Score, int SlideX, int SlideY)>();
        var candidates = MapCandidates.Enumerate(mapsPath, x, y).ToList();
        var done = 0;

        foreach (var candidate in candidates)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                host.LoadMap(candidate.Map, candidate.Width, candidate.Height);
                host.CenterOn(x, y);
                var frame = Screenshot.Decode(host.EncodeBmp());
                var (score, slideX, slideY) = MapCandidates.BestSlide(reference, frame, host.Hud.MapRectangle);
                results.Add((candidate.Map, candidate.Width, candidate.Height, score, slideX, slideY));
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or ArgumentException)
            {
            }

            if (++done % 25 == 0)
            {
                var progress = done;
                Dispatcher.UIThread.Post(() => Status = $"Searching maps... {progress} of {candidates.Count}");
            }
        }

        return [.. results.OrderByDescending(r => r.Score).Take(15)];
    }

    public void Dispose()
    {
        _search?.Cancel();
        _host?.Dispose();
        _matchHost?.Dispose();
    }
}
