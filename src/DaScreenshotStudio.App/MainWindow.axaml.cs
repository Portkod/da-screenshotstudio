using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using DaScreenshotStudio.App.Controls;
using DaScreenshotStudio.App.ViewModels;

namespace DaScreenshotStudio.App;

public sealed partial class MainWindow : Window
{
    private static readonly FilePickerFileType SceneFiles = new("DA Screenshot Studio scene") { Patterns = ["*.scene.json", "*.json"] };
    private static readonly FilePickerFileType BitmapFiles = new("Bitmap") { Patterns = ["*.bmp"] };
    private static readonly FilePickerFileType JsonFiles = new("JSON") { Patterns = ["*.json"] };

    public MainWindow()
    {
        InitializeComponent();
        var editor = new MainViewModel();
        DataContext = editor;
        if (!editor.IsClientReady)
        {
            // Nothing can be drawn without a client, so start where it is set.
            ToolTabs.SelectedItem = SettingsTab;
        }

        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, OnWindowDragOver);
        AddHandler(DragDrop.DropEvent, OnWindowDrop);
        Closed += (_, _) => editor.Dispose();
        editor.Sprites.RevealRequested += row =>
        {
            ToolTabs.SelectedItem = SpritesTab;
            Avalonia.Threading.Dispatcher.UIThread.Post(() => SpriteRows.ScrollIntoView(row));
        };
        editor.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.Selected) && editor.Selected is not null)
            {
                InspectorTabs.SelectedIndex = 0;
            }
        };
    }

    private MainViewModel Editor => (MainViewModel)DataContext!;

    /// <summary>Opens a scene file, as passing one on the command line does.</summary>
    public void Open(string path) => Editor.OpenScene(path);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
        {
            return;
        }

        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        switch (e.Key)
        {
            case Key.S when ctrl:
                OnSave(this, e);
                break;
            case Key.O when ctrl:
                OnOpen(this, e);
                break;
            case Key.E when ctrl:
                OnExport(this, e);
                break;
            case Key.F1:
                Editor.Overlay = OverlayMode.Scene;
                break;
            case Key.F2:
                Editor.Overlay = OverlayMode.Reference;
                break;
            case Key.F3:
                Editor.Overlay = OverlayMode.Difference;
                break;
            case Key.F4:
                Editor.Overlay = OverlayMode.Blend;
                break;
            case Key.PageUp:
                Editor.PreviousSpriteCommand.Execute(null);
                break;
            case Key.PageDown:
                Editor.NextSpriteCommand.Execute(null);
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    private async void OnOpen(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open scene",
            FileTypeFilter = [SceneFiles],
            AllowMultiple = false,
        });

        if (files.Count > 0 && files[0].TryGetLocalPath() is { } path)
        {
            Editor.OpenScene(path);
        }
    }

    private async void OnSave(object? sender, RoutedEventArgs e)
    {
        if (Editor.ScenePath is { } path)
        {
            Editor.SaveScene(path);
            return;
        }

        await SaveAs();
    }

    private async void OnSaveAs(object? sender, RoutedEventArgs e) => await SaveAs();

    private async Task SaveAs()
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save scene",
            FileTypeChoices = [SceneFiles],
            DefaultExtension = ".scene.json",
            SuggestedFileName = "scene.scene.json",
        });

        if (file?.TryGetLocalPath() is { } path)
        {
            Editor.SaveScene(path);
        }
    }

    private async void OnExport(object? sender, RoutedEventArgs e)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export screenshot",
            FileTypeChoices = [BitmapFiles],
            DefaultExtension = ".bmp",
            SuggestedFileName = "lod001.bmp",
        });

        if (file?.TryGetLocalPath() is { } path)
        {
            Editor.Export(path);
        }
    }

    private async void OnLoadReference(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Reference screenshot",
            FileTypeFilter = [BitmapFiles],
            AllowMultiple = false,
        });

        if (files.Count > 0 && files[0].TryGetLocalPath() is { } path)
        {
            Editor.LoadReference(path);
        }
    }

    private async void OnBrowsePath(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: PathSettingViewModel setting })
        {
            return;
        }

        var start = setting.StartFolder is { } folder ? await StorageProvider.TryGetFolderFromPathAsync(folder) : null;
        string? picked;
        switch (setting.Kind)
        {
            case PathKind.Folder:
                var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = setting.Label,
                    SuggestedStartLocation = start,
                    AllowMultiple = false,
                });
                picked = folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
                break;
            case PathKind.File:
                var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = setting.Label,
                    SuggestedStartLocation = start,
                    FileTypeFilter = [JsonFiles],
                    AllowMultiple = false,
                });
                picked = files.Count > 0 ? files[0].TryGetLocalPath() : null;
                break;
            default:
                var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = setting.Label,
                    SuggestedStartLocation = start,
                    SuggestedFileName = Path.GetFileName(setting.FullPath),
                    FileTypeChoices = [JsonFiles],
                    DefaultExtension = ".json",
                    ShowOverwritePrompt = false,
                });
                picked = file?.TryGetLocalPath();
                break;
        }

        if (picked is not null)
        {
            setting.Pick(picked);
        }
    }

    private void OnWindowDragOver(object? sender, DragEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }

        e.DragEffects = e.DataTransfer.Contains(DataFormat.File) ? DragDropEffects.Copy : DragDropEffects.None;
    }

    /// <summary>A dropped bitmap becomes the reference, and a dropped scene file is opened.</summary>
    private void OnWindowDrop(object? sender, DragEventArgs e)
    {
        if (e.Handled || e.DataTransfer.TryGetFile() is not { } file || file.TryGetLocalPath() is not { } path)
        {
            return;
        }

        if (path.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase))
        {
            Editor.LoadReference(path);
        }
        else if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            Editor.OpenScene(path);
        }

        e.Handled = true;
    }

    private async void OnLibraryPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control { DataContext: LibraryEntryViewModel entry }
            && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            var data = new DataTransfer();
            var item = new DataTransferItem();
            item.Set(DragFormats.Character, entry.Model);
            data.Add(item);
            await DragDrop.DoDragDropAsync(e, data, DragDropEffects.Copy);
        }
    }

    private void OnLibraryDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: LibraryEntryViewModel entry })
        {
            Editor.PlaceFromLibraryCommand.Execute(entry);
        }
    }

    private async void OnItemPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control { DataContext: ItemEntryViewModel entry }
            && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            var data = new DataTransfer();
            var item = new DataTransferItem();
            item.Set(DragFormats.Item, entry.Model);
            data.Add(item);
            await DragDrop.DoDragDropAsync(e, data, DragDropEffects.Copy);
        }
    }

    private async void OnSpritePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control { DataContext: SpriteTileViewModel tile }
            && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            Editor.Sprites.Pick(tile);
            var data = new DataTransfer();
            var item = new DataTransferItem();
            item.Set(DragFormats.Sprite, Editor.Sprites.PickOf(tile));
            data.Add(item);
            await DragDrop.DoDragDropAsync(e, data, DragDropEffects.Copy);
        }
    }

    private void OnSpriteDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: SpriteTileViewModel tile })
        {
            Editor.UseSpriteCommand.Execute(tile);
        }
    }

    private void OnItemDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: ItemEntryViewModel entry })
        {
            Editor.WearItemCommand.Execute(entry);
        }
    }
}
