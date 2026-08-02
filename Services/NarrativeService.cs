/**
 * geetRPCS - Narrative Service
 * Manages dynamic text responses for the RPC
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
using System.IO;
using System.Linq;
using System.Text.Json;
using geetRPCS.Models;

namespace geetRPCS.Services
{
    public static class NarrativeService
    {
        // --- Models ---
        private class CacheEntry
        {
            public string Text { get; set; }
            public DateTime LastUpdated { get; set; }
        }
        private static readonly Dictionary<string, CacheEntry> _sessionTexts = new Dictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);
        private static Dictionary<string, List<string>> _wittyTexts;
        private static readonly Random _random = new Random();
        private static readonly object _lock = new object();
        private static readonly TimeSpan _rotationInterval = TimeSpan.FromSeconds(60);
        private static readonly string WittyPath = Utils.AppPaths.WittyPath;
        static NarrativeService()
        {
            LoadWittyTexts();
        }
        private static void LoadWittyTexts()
        {
            try
            {
                if (File.Exists(WittyPath))
                {
                    LogService.Debug($"Loading witty.json from: {WittyPath}", "NarrativeService");
                    string json = File.ReadAllText(WittyPath);
                    var loaded = JsonSerializer.Deserialize(json, Utils.JsonContext.Default.DictionaryStringJsonElement);
                    _wittyTexts = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                    if (loaded != null)
                    {
                        foreach (var kvp in loaded)
                        {
                            if (kvp.Value.ValueKind == JsonValueKind.Array)
                            {
                                try
                                {
                                    var texts = kvp.Value.Deserialize<List<string>>();
                                    if (texts != null)
                                    {
                                        _wittyTexts[kvp.Key] = texts;
                                    }
                                }
                                catch
                                {
                                    LogService.Debug($"Skipping entry '{kvp.Key}' - not a valid string array", "NarrativeService");
                                }
                            }
                            else
                            {
                                LogService.Debug($"Skipping non-array entry: '{kvp.Key}'", "NarrativeService");
                            }
                        }
                        LogService.Debug($"Loaded {_wittyTexts.Count} process entries from witty.json", "NarrativeService");
                    }
                    else
                    {
                        LogService.Debug("Failed to deserialize witty.json", "NarrativeService");
                    }
                }
                else
                {
                    LogService.Debug($"witty.json not found at: {WittyPath}", "NarrativeService");
                    _wittyTexts = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                }
            }
            catch (Exception ex)
            {
                LogService.Debug($"Error loading witty.json: {ex.Message}", "NarrativeService");
                _wittyTexts = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            }
        }
        public static void Reload()
        {
            lock (_lock)
            {
                LoadWittyTexts();
                _sessionTexts.Clear();
            }
        }
        public static string GetForApp(string processName)
        {
            if (string.IsNullOrEmpty(processName)) return "";
            lock (_lock)
            {
                LogService.Debug($"Looking for witty text for '{processName}' (dict: {_wittyTexts.Count} entries)", "NarrativeService");
                if (_sessionTexts.TryGetValue(processName, out var entry))
                {
                    if (DateTime.Now - entry.LastUpdated < _rotationInterval)
                    {
                        LogService.Debug($"Returning cached text: '{entry.Text}'", "NarrativeService");
                        return entry.Text;
                    }
                }
                string selectedText;
                if (_wittyTexts.TryGetValue(processName, out var texts) && texts != null && texts.Count > 0)
                {
                    LogService.Debug($"Found {texts.Count} witty texts for '{processName}'", "NarrativeService");
                    if (texts.Count > 1 && entry != null)
                    {
                         string newText;
                         int retry = 0;
                         do
                         {
                             newText = texts[_random.Next(texts.Count)];
                             retry++;
                         } while (newText == entry.Text && retry < 5);
                         selectedText = newText;
                    }
                    else
                    {
                        selectedText = texts[_random.Next(texts.Count)];
                    }
                    LogService.Debug($"Selected: '{selectedText}'", "NarrativeService");
                }
                else
                {
                    LogService.Debug($"No witty texts found for '{processName}', checking AppConfig...", "NarrativeService");
                    var app = AppConfigManager.Apps.FirstOrDefault(a => a.Process.Equals(processName, StringComparison.OrdinalIgnoreCase));
                    if (app != null && app.WittyTexts != null && app.WittyTexts.Count > 0)
                    {
                        LogService.Debug($"Found {app.WittyTexts.Count} texts in AppConfig", "NarrativeService");
                        if (app.WittyTexts.Count > 1 && entry != null)
                        {
                             string newText;
                             int retry = 0;
                             do
                             {
                                 newText = app.WittyTexts[_random.Next(app.WittyTexts.Count)];
                                 retry++;
                             } while (newText == entry.Text && retry < 5);
                             selectedText = newText;
                        }
                        else
                        {
                            selectedText = app.WittyTexts[_random.Next(app.WittyTexts.Count)];
                        }
                    }
                    else
                    {
                        LogService.Debug("Using fallback text", "NarrativeService");
                        selectedText = "Working hard... 🔨";
                    }
                }
                _sessionTexts[processName] = new CacheEntry { Text = selectedText, LastUpdated = DateTime.Now };
                return selectedText;
            }
        }
        public static bool ShouldRotate(string processName)
        {
             lock (_lock)
             {
                 if (_sessionTexts.TryGetValue(processName, out var entry))
                 {
                     return (DateTime.Now - entry.LastUpdated) >= _rotationInterval;
                 }
                 return true;
             }
        }
        public static void ResetForApp(string processName)
        {
            lock (_lock)
            {
                if (_sessionTexts.ContainsKey(processName))
                    _sessionTexts.Remove(processName);
            }
        }
        public static void ResetAll()
        {
            lock (_lock)
            {
                _sessionTexts.Clear();
            }
        }
    }
}
