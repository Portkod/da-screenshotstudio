using Avalonia.Data.Converters;

namespace DaScreenshotStudio.App.ViewModels;

public static class Converters
{
    public static readonly IValueConverter SearchLabel =
        new FuncValueConverter<bool, string>(searching => searching ? "Stop searching" : "Find map");

    public static readonly IValueConverter PathPlaceholder =
        new FuncValueConverter<bool, string>(folder => folder ? "Type a folder path or browse" : "Type a file path or browse");
}
