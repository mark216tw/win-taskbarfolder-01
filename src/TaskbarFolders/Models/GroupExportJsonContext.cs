using System.Text.Json.Serialization;

namespace Mark.TaskbarFolders.Models;

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(GroupExportDocument))]
internal sealed partial class GroupExportJsonContext : JsonSerializerContext
{
}
