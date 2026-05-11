/**
 * geetRPCS - App Configuration
 * App configuration models for geetRPCS
 */
/*
 * Copyright (c) 2026 makcrtve
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace geetRPCS.Models
{
    public class AppConfig
    {
        [JsonPropertyName("process")]
        public string Process { get; set; }
        [JsonPropertyName("appName")]
        public string AppName { get; set; }
        [JsonPropertyName("windowTitle")]
        public string WindowTitle { get; set; }
        [JsonPropertyName("largeKey")]
        public string LargeKey { get; set; }
        [JsonPropertyName("largeText")]
        public string LargeText { get; set; }
        [JsonPropertyName("smallKey")]
        public string SmallKey { get; set; }
        // NOTE: smallText is intentionally removed - hardcoded as "geetRPCS" in PresenceAssets.cs
        // This ensures consistent branding that cannot be modified via apps.json
        [JsonPropertyName("clientId")]
        public string ClientId { get; set; }
        [JsonPropertyName("customDetails")]
        public string CustomDetails { get; set; }
        [JsonPropertyName("buttons")]
        public List<AppButtonConfig> Buttons { get; set; }
        [JsonPropertyName("wittyTexts")]
        public List<string> WittyTexts { get; set; }

        [JsonPropertyName("processMatchMode")]
        public string ProcessMatchMode { get; set; }

        [JsonPropertyName("titleMatchMode")]
        public string TitleMatchMode { get; set; }

        [JsonIgnore]
        public System.Text.RegularExpressions.Regex ProcessRegex { get; set; }

        [JsonIgnore]
        public System.Text.RegularExpressions.Regex TitleRegex { get; set; }
        public static List<AppConfig> Load(string path)
        {
            try
            {
                string json = File.ReadAllText(path);
                return JsonSerializer.Deserialize(json, Utils.JsonContext.Default.ListAppConfig);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AppConfig] Error loading {path}: {ex.Message}");
                return null;
            }
        }
    }
    public class AppButtonConfig
    {
        [JsonPropertyName("label")]
        public string Label { get; set; }
        [JsonPropertyName("url")]
        public string Url { get; set; }
    }
}
