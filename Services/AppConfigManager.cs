/**
 * geetRPCS - Config Manager
 * Manages loading and saving of app configurations
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using geetRPCS.Models;
using geetRPCS.Utils;

namespace geetRPCS.Services
{
    internal static class AppConfigManager
    {
        private static List<AppConfig> _apps;
        private static HashSet<string> _processNames;
        private static List<AppConfig> _exactProcessApps;
        private static List<AppConfig> _advancedProcessApps;
        private static Dictionary<string, AppConfig> _exactLookup;
        private static readonly object _lock = new object();
        private static readonly string AppsPath = AppPaths.AppsPath;

        public static IReadOnlyList<AppConfig> Apps
        {
            get { lock (_lock) { if (_apps == null) Reload(); return _apps; } }
        }

        public static HashSet<string> ExactProcessNames
        {
            get { lock (_lock) { if (_processNames == null) Reload(); return _processNames; } }
        }

        public static IReadOnlyList<AppConfig> AdvancedProcessApps
        {
            get { lock (_lock) { if (_advancedProcessApps == null) Reload(); return _advancedProcessApps; } }
        }

        public static void Reload()
            => Reload(AppConfig.Load(AppsPath), SettingsService.Instance.CustomApps);

        /// <summary>Testable core: classifies the built-in apps merged with the
        /// user's custom apps (see MergeCustomApps).</summary>
        internal static void Reload(List<AppConfig> loadedApps, List<AppConfig> customApps)
        {
            lock (_lock)
            {
                try
                {
                    var allApps = loadedApps ?? new List<AppConfig>();
                    _apps = allApps.Where(a => !string.IsNullOrEmpty(a.Process)).ToList();
                    MergeCustomApps(_apps, customApps);
                    
                    _exactProcessApps = new List<AppConfig>();
                    _advancedProcessApps = new List<AppConfig>();
                    _processNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    foreach (var app in _apps)
                    {
                        // Default to Exact for Process if not specified or unrecognized
                        bool isAdvancedProcess = !string.IsNullOrEmpty(app.ProcessMatchMode) && 
                            !app.ProcessMatchMode.Equals("Exact", StringComparison.OrdinalIgnoreCase);

                        if (isAdvancedProcess)
                        {
                            _advancedProcessApps.Add(app);
                        }
                        else
                        {
                            _exactProcessApps.Add(app);
                            _processNames.Add(app.Process);
                        }

                        // Precompile Process Regex if needed
                        if (app.ProcessMatchMode != null && app.ProcessMatchMode.Equals("Regex", StringComparison.OrdinalIgnoreCase))
                        {
                            try { app.ProcessRegex = new Regex(app.Process, RegexOptions.IgnoreCase | RegexOptions.Compiled); }
                            catch (Exception ex) { Debug.WriteLine($"[AppConfigManager] Invalid Process Regex '{app.Process}': {ex.Message}"); }
                        }

                        // Precompile Title Regex if needed
                        if (app.TitleMatchMode != null && app.TitleMatchMode.Equals("Regex", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!string.IsNullOrEmpty(app.WindowTitle))
                            {
                                try { app.TitleRegex = new Regex(app.WindowTitle, RegexOptions.IgnoreCase | RegexOptions.Compiled); }
                                catch (Exception ex) { Debug.WriteLine($"[AppConfigManager] Invalid Title Regex '{app.WindowTitle}': {ex.Message}"); }
                            }
                        }
                    }

                    // Exact-name lookup for the presence-build path: a linear
                    // FirstOrDefault over _apps used to run up to 4x per build.
                    // First-match-wins keeps FirstOrDefault semantics for
                    // duplicate process names.
                    _exactLookup = new Dictionary<string, AppConfig>(StringComparer.OrdinalIgnoreCase);
                    foreach (var app in _apps)
                        if (!_exactLookup.ContainsKey(app.Process))
                            _exactLookup[app.Process] = app;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AppConfigManager] Failed to load apps.json: {ex.Message}");
                    _apps = new List<AppConfig>();
                    _exactProcessApps = new List<AppConfig>();
                    _advancedProcessApps = new List<AppConfig>();
                    _processNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    _exactLookup = new Dictionary<string, AppConfig>(StringComparer.OrdinalIgnoreCase);
                }
            }
        }

        /// <summary>Merges the user's custom apps (settings.json) into the loaded
        /// list IN PLACE. A custom entry whose process matches a built-in entry
        /// REPLACES it (the user wins — a way to fix/tune built-ins); new
        /// processes are appended. apps.json itself stays read-only so the
        /// apps-DB updater can overwrite it without losing user customizations.</summary>
        private static void MergeCustomApps(List<AppConfig> apps, List<AppConfig> customApps)
        {
            try
            {
                var custom = customApps?.Where(c => !string.IsNullOrEmpty(c.Process)).ToList();
                if (custom == null || custom.Count == 0) return;
                var customByProcess = new Dictionary<string, AppConfig>(StringComparer.OrdinalIgnoreCase);
                foreach (var c in custom) customByProcess[c.Process] = c; // later duplicates win
                for (int i = apps.Count - 1; i >= 0; i--)
                    if (customByProcess.ContainsKey(apps[i].Process)) apps.RemoveAt(i);
                apps.AddRange(custom);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AppConfigManager] Failed to merge custom apps: {ex.Message}");
            }
        }

        /// <summary>First apps entry whose Process equals processName (ordinal
        /// ignore case), via the lookup dictionary built in Reload.</summary>
        public static AppConfig FindExact(string processName)
        {
            if (string.IsNullOrEmpty(processName)) return null;
            lock (_lock)
            {
                if (_apps == null) Reload();
                return _exactLookup.TryGetValue(processName, out var app) ? app : null;
            }
        }

        /// <summary>Returns the apps.json entry for a process with the user's
        /// settings override applied (override &gt; database for largeKey/largeText/
        /// showTimestamps/buttons/clientId). Details/State overrides are resolved
        /// separately in PresenceBuilder, which has its own fallback chain.</summary>
        public static AppConfig GetEffectiveApp(string processName)
        {
            var app = FindExact(processName);
            if (app == null) return null;
            return SettingsService.Instance.AppOverrides.TryGetValue(app.Process, out var ov)
                ? GetEffectiveApp(app, ov)
                : app;
        }

        /// <summary>Pure merge of one entry + one override (clone; null override
        /// fields inherit). Exposed separately so precedence is unit-testable
        /// without touching the real settings store.</summary>
        internal static AppConfig GetEffectiveApp(AppConfig app, AppOverrideConfig overrideConfig)
        {
            if (app == null || overrideConfig == null) return app;
            return new AppConfig
            {
                Process = app.Process,
                AppName = app.AppName,
                WindowTitle = app.WindowTitle,
                LargeKey = overrideConfig.LargeKey ?? app.LargeKey,
                LargeText = overrideConfig.LargeText ?? app.LargeText,
                SmallKey = app.SmallKey,
                ClientId = overrideConfig.ClientId ?? app.ClientId,
                CustomDetails = app.CustomDetails,
                ShowTimestamps = overrideConfig.ShowTimestamps ?? app.ShowTimestamps,
                Buttons = overrideConfig.Buttons ?? app.Buttons,
                WittyTexts = app.WittyTexts,
                ProcessMatchMode = app.ProcessMatchMode,
                TitleMatchMode = app.TitleMatchMode
            };
        }
    }
}
