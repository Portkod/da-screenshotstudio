using System.Text.Json;
using System.Text.Json.Serialization;

namespace DaScreenshotStudio.Engine.Staging;

public sealed class SavedCharacter
{
    public string Name { get; set; } = "";

    public AislingLook Look { get; set; } = new();
}

public sealed class CharacterLibrary
{
    public List<SavedCharacter> Characters { get; set; } = [];

    public static CharacterLibrary Load(string path)
    {
        if (!File.Exists(path))
        {
            return new CharacterLibrary();
        }

        return JsonSerializer.Deserialize(File.ReadAllText(path), CharacterLibraryJson.Default.CharacterLibrary)
            ?? new CharacterLibrary();
    }

    public void Save(string path) =>
        File.WriteAllText(path, JsonSerializer.Serialize(this, CharacterLibraryJson.Default.CharacterLibrary));
}

[JsonSourceGenerationOptions(WriteIndented = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true)]
[JsonSerializable(typeof(CharacterLibrary))]
internal sealed partial class CharacterLibraryJson : JsonSerializerContext;
