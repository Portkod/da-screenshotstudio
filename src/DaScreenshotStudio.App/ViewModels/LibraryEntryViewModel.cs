using Avalonia.Media.Imaging;
using DaScreenshotStudio.Engine.Catalog;
using DaScreenshotStudio.Engine.Staging;

namespace DaScreenshotStudio.App.ViewModels;

/// <summary>A saved character in the library list.</summary>
public sealed class LibraryEntryViewModel(SavedCharacter model, Bitmap? preview)
{
    public SavedCharacter Model { get; } = model;

    public Bitmap? Preview { get; } = preview;

    public string Name => Model.Name.Length > 0 ? Model.Name : "(unnamed)";
}

/// <summary>An equipment database entry in the items list.</summary>
public sealed class ItemEntryViewModel(CatalogItem model)
{
    public CatalogItem Model { get; } = model;

    public string Name => Model.Name;

    public string Detail
    {
        get
        {
            var gender = Model.Gender == ItemGender.Any ? "" : $" {Model.Gender}";
            var dye = Model.TakesRequestedDye ? " dyeable" : Model.Color > 0 ? $" color {Model.Color}" : "";
            return $"{Model.Category} #{Model.FileId}{gender}{dye}";
        }
    }
}

/// <summary>A map in the catalog list, or a find-map candidate.</summary>
public sealed class MapEntryViewModel(
    int id, string name, int width, int height, double? score = null, int slideX = 0, int slideY = 0)
{
    /// <summary>For a search result, the camera slide the reference was taken at.</summary>
    public int SlideX { get; } = slideX;

    public int SlideY { get; } = slideY;

    public int Id { get; } = id;

    public string Name { get; } = name;

    public int Width { get; } = width;

    public int Height { get; } = height;

    public double? Score { get; } = score;

    public string FileName => $"lod{Id}.map";

    /// <summary>The map's name, or its file when it has none yet.</summary>
    public string DisplayName => Name.Length > 0 ? Name : FileName;

    /// <summary>The file and size under the name, and for a search result how well it matched.</summary>
    public string Details
    {
        get
        {
            var details = $"{FileName}  ·  {Width} × {Height}";
            if (Score is { } score)
            {
                details += $"  ·  {score:P1} match";
                if (SlideX != 0 || SlideY != 0)
                {
                    details += $"  ·  slide {SlideX}, {SlideY}";
                }
            }

            return details;
        }
    }
}
