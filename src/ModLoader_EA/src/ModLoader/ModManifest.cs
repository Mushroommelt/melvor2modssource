using System.Text.Json;
using System.Text.Json.Serialization;

namespace MelvorModLoader;

/// <summary>
/// A mod's <c>mod.json</c>, next to its assembly in <c>mods/&lt;ModFolder&gt;/</c>:
/// <code>
/// {
///   "id": "Multitasking",
///   "name": "Multitasking",
///   "version": "1.0.0",
///   "author": "you",
///   "description": "Train all skills at once.",
///   "assembly": "MultitaskingMod.dll"
/// }
/// </code>
/// </summary>
public sealed record ModManifest
{
    public const string FileName = "mod.json";

    /// <summary>Unique id; also used as the mod's Harmony id. Mods load in id order.</summary>
    public required string Id { get; init; }

    public string? Name { get; init; }

    public string? Version { get; init; }

    public string? Author { get; init; }

    public string? Description { get; init; }

    /// <summary>The mod's main assembly, relative to the mod folder.</summary>
    public required string Assembly { get; init; }

    [JsonIgnore]
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? Id : Name;

    private static readonly JsonSerializerOptions s_options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static ModManifest Read(string path) =>
        JsonSerializer.Deserialize<ModManifest>(File.ReadAllText(path), s_options)
        ?? throw new InvalidDataException($"{path} is empty.");
}
