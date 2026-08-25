/**
 * geetRPCS - App Settings
 * Application settings models for geetRPCS
 */
/*
 * Copyright (c) 2026 geetcr4ck
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 */

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace geetRPCS.Models
{
    public class AppSettings
    {
        [JsonPropertyName("language")]
        public string Language { get; set; } = "en";
        [JsonPropertyName("disabledApps")]
        public List<string> DisabledApps { get; set; } = new List<string>();
        [JsonPropertyName("mouseEnergyEnabled")]
        public bool MouseEnergyEnabled { get; set; } = true;
        [JsonPropertyName("trayAnimationEnabled")]
        public bool TrayAnimationEnabled { get; set; } = true;
        [JsonPropertyName("themeMode")]
        public string ThemeMode { get; set; } = "System"; // System, Dark, Light
        [JsonPropertyName("telemetryEnabled")]
        public bool TelemetryEnabled { get; set; } = true;
        [JsonPropertyName("updateNotificationMode")]
        public string UpdateNotificationMode { get; set; } = "Notification"; // Notification, Dialog, Silent
        [JsonPropertyName("appOverrides")]
        public Dictionary<string, AppOverrideConfig> AppOverrides { get; set; } = new Dictionary<string, AppOverrideConfig>(StringComparer.OrdinalIgnoreCase);
        [JsonPropertyName("customApps")]
        public List<AppConfig> CustomApps { get; set; } = new List<AppConfig>();
        [JsonPropertyName("logLevel")]
        public string LogLevel { get; set; } = "INFO"; // DEBUG, INFO, WARN, ERROR
        [JsonPropertyName("autoUpdateEnabled")]
        public bool AutoUpdateEnabled { get; set; } = false;
        [JsonPropertyName("shortcutPreferences")]
        public ShortcutPreferences ShortcutPreferences { get; set; } = new ShortcutPreferences();
    }
    public class ShortcutPreferences
    {
        [JsonPropertyName("desktopShortcut")]
        public bool DesktopShortcut { get; set; } = true;
        [JsonPropertyName("startMenuShortcut")]
        public bool StartMenuShortcut { get; set; } = true;
        [JsonPropertyName("preferenceSaved")]
        public bool PreferenceSaved { get; set; } = false;
    }
    /// <summary>Per-app user customization layered over the apps.json entry.
    /// Every field is optional: null means "inherit the database default".
    /// Persisted in settings.json (never in apps.json, which the apps-DB
    /// updater overwrites wholesale).</summary>
    public class AppOverrideConfig
    {
        [JsonPropertyName("details")]
        public string Details { get; set; }
        [JsonPropertyName("state")]
        public string State { get; set; }
        [JsonPropertyName("largeKey")]
        public string LargeKey { get; set; }
        [JsonPropertyName("largeText")]
        public string LargeText { get; set; }
        [JsonPropertyName("showTimestamps")]
        public bool? ShowTimestamps { get; set; }
        [JsonPropertyName("buttons")]
        public List<AppButtonConfig> Buttons { get; set; }
        [JsonPropertyName("clientId")]
        public string ClientId { get; set; }
    }
}
