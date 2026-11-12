using System.Text.Json.Serialization;

namespace Mark.TaskbarFolders.Models;

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(WindowPlacement))]
internal sealed partial class WindowPlacementJsonContext : JsonSerializerContext
{
}
