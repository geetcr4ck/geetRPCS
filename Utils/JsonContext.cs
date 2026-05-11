using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using geetRPCS.Models;
using geetRPCS.Services;

namespace geetRPCS.Utils
{
    [JsonSourceGenerationOptions(
        WriteIndented = true, 
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = new[] { typeof(geetRPCS.Services.TimeSpanConverter) })]
    [JsonSerializable(typeof(Config))]
    [JsonSerializable(typeof(AppConfig))]
    [JsonSerializable(typeof(List<AppConfig>))]
    [JsonSerializable(typeof(AppSettings))]
    [JsonSerializable(typeof(Language))]
    [JsonSerializable(typeof(AppStatistics))]
    [JsonSerializable(typeof(UpdateChecker.GitHubRelease))]
    [JsonSerializable(typeof(Dictionary<string, string>))]
    [JsonSerializable(typeof(Dictionary<string, JsonElement>))]
    [JsonSerializable(typeof(List<DiscordAsset>))]
    internal partial class JsonContext : JsonSerializerContext
    {
    }
}
