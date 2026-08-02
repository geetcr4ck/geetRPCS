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
        {
            lock (_lock)
            {
                try
                {
                    var allApps = AppConfig.Load(AppsPath) ?? new List<AppConfig>();
                    _apps = allApps.Where(a => !string.IsNullOrEmpty(a.Process)).ToList();
                    
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
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AppConfigManager] Failed to load apps.json: {ex.Message}");
                    _apps = new List<AppConfig>();
                    _exactProcessApps = new List<AppConfig>();
                    _advancedProcessApps = new List<AppConfig>();
                    _processNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                }
            }
        }
    }
}
