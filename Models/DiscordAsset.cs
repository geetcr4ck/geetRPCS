using System;
using System.Text.Json.Serialization;

namespace geetRPCS.Models
{
    public class DiscordAsset
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }
        [JsonPropertyName("name")]
        public string Name { get; set; }
        [JsonPropertyName("type")]
        public int Type { get; set; }
    }
}
