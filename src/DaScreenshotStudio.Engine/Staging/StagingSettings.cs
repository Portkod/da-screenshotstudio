using System.Text.Json;
using System.Text.Json.Serialization;
using DaScreenshotStudio.Engine.App;

namespace DaScreenshotStudio.Engine.Staging;

public sealed class StagingSettings
{
    public const string FileName = "dass.json";

    public const string ClientMapsFolder = "maps";

    public string ClientPath { get; set; } = "";

    public string MapsPath { get; set; } = "";

    public string MetafilePath { get; set; } = "";

    public string MapCatalogPath { get; set; } = "maps.json";

    public string ItemsPath { get; set; } = "Items.json";

    public string LibraryPath { get; set; } = "characters.json";

    [JsonIgnore]
    public string BaseDirectory { get; set; } = Directory.GetCurrentDirectory();

    [JsonIgnore]
    public string MapsDirectory =>
        MapsPath.Length > 0 ? Resolve(MapsPath) : new ClientDirectory(Resolve(ClientPath)).PathOf(ClientMapsFolder);

    [JsonIgnore]
    public string? MetafileDirectory => MetafilePath.Length > 0 ? Resolve(MetafilePath) : null;

    public SceneHost OpenHost() => new(Resolve(ClientPath), MapsDirectory, MetafileDirectory);

    public string Resolve(string path) => Path.GetFullPath(Path.Combine(BaseDirectory, path));

    public static StagingSettings Locate(string directory)
    {
        for (var current = new DirectoryInfo(directory); current is not null; current = current.Parent)
        {
            var path = Path.Combine(current.FullName, FileName);
            if (File.Exists(path))
            {
                return Load(path);
            }
        }

        return new StagingSettings { BaseDirectory = directory };
    }

    public static StagingSettings Load(string path)
    {
        var settings = JsonSerializer.Deserialize(File.ReadAllText(path), StagingSettingsJson.Default.StagingSettings)
            ?? new StagingSettings();
        settings.BaseDirectory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        return settings;
    }

    public void Save() =>
        File.WriteAllText(
            Path.Combine(BaseDirectory, FileName),
            JsonSerializer.Serialize(this, StagingSettingsJson.Default.StagingSettings));
}

[JsonSourceGenerationOptions(WriteIndented = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true)]
[JsonSerializable(typeof(StagingSettings))]
internal sealed partial class StagingSettingsJson : JsonSerializerContext;
