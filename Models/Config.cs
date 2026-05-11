using System;
using System.Text.Json.Serialization;

#nullable enable

namespace geetRPCS.Models
{
    public class Config
    {
        [JsonPropertyName("discord")]
        public DiscordConfig? Discord { get; set; }
    }

    public class DiscordConfig
    {
        [JsonPropertyName("applicationId")]
        public string ApplicationId { get; set; } = "";
        [JsonPropertyName("details")]
        public string? Details { get; set; }
        [JsonPropertyName("state")]
        public string? State { get; set; }
        [JsonPropertyName("activeDetails")]
        public string? ActiveDetails { get; set; }
        [JsonPropertyName("activeState")]
        public string? ActiveState { get; set; }
        [JsonPropertyName("assets")]
        public AssetConfig? Assets { get; set; }
        [JsonPropertyName("buttons")]
        public ButtonConfig[]? Buttons { get; set; }
    }

    public class AssetConfig
    {
        [JsonPropertyName("largeImageKey")]
        public string? LargeImageKey { get; set; }
        [JsonPropertyName("largeImageText")]
        public string? LargeImageText { get; set; }
        [JsonPropertyName("smallImageKey")]
        public string? SmallImageKey { get; set; }
        [JsonPropertyName("smallImageText")]
        public string? SmallImageText { get; set; }
    }

    public class ButtonConfig
    {
        [JsonPropertyName("label")]
        public string? Label { get; set; }
        [JsonPropertyName("url")]
        public string? Url { get; set; }
    }
}
