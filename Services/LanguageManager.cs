/**
 * geetRPCS - Language Manager
 * Manages application-wide language and localization.
 *
 * Localization strategy:
 *  - The list of available languages is discovered by scanning the Languages/
 *    folder instead of being hard-coded, so new .json files are picked up
 *    automatically.
 *  - Missing / null translations automatically fall back to the English key,
 *    centralizing the fallback logic here instead of sprinkling `?? "..."`
 *    defaults across the UI code.
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
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using geetRPCS.Models;

namespace geetRPCS.Services
{
    internal static class LanguageManager
    {
        private static readonly string AppFolder = Utils.AppPaths.InstallDir;
        private static readonly string LanguagesFolder = Utils.AppPaths.LanguagesDir;
        private static readonly string SettingsPath = Utils.AppPaths.SettingsPath;

        private static readonly string FallbackCode = "en";
        private static JsonObject _englishNode; // Cached English base used for the fallback merge

        private static Language _currentLanguage;
        private static string _currentLanguageCode = "en";
        public static Language Current => _currentLanguage ?? LoadLanguage(_currentLanguageCode);
        static LanguageManager()
        {
            EnsureLanguagesFolder();
            LoadSettings();
        }

        private static void EnsureLanguagesFolder()
        {
            try
            {
                if (!Directory.Exists(LanguagesFolder))
                {
                    Directory.CreateDirectory(LanguagesFolder);
                    Log($"Created folder: {LanguagesFolder}", "INFO");
                }
            }
            catch (Exception ex)
            {
                Log($"Failed to create folder: {ex.Message}", "ERROR");
            }
        }

        private static void LoadSettings()
        {
            try
            {
                _currentLanguageCode = SettingsService.Instance.Language ?? "en";
                _currentLanguage = LoadLanguage(_currentLanguageCode);
                Log($"Ready with language: {_currentLanguageCode}", "INFO");
            }
            catch (Exception ex)
            {
                Log($"Failed to load settings: {ex.Message}", "ERROR");
                _currentLanguage = LoadLanguage("en"); // Fallback to English
            }
        }

        public static async Task SetLanguageAsync(string languageCode)
        {
            try
            {
                var newLanguage = LoadLanguage(languageCode);
                if (newLanguage != null)
                {
                    _currentLanguage = newLanguage;
                    _currentLanguageCode = languageCode;
                    SettingsService.Instance.Language = languageCode;
                    await SettingsService.SaveAsync();
                    Log($"Language changed to: {languageCode}", "INFO");
                }
            }
            catch (Exception ex)
            {
                Log($"Failed to change language: {ex.Message}", "ERROR");
            }
        }

        public static string GetCurrentLanguageCode()
        {
            return _currentLanguageCode;
        }

        private static Language LoadLanguage(string languageCode)
        {
            try
            {
                string path = Path.Combine(LanguagesFolder, $"{languageCode}.json");
                if (!File.Exists(path))
                {
                    Log($"Language file not found: {Path.GetFileName(path)}", "WARNING");

                    // Fallback to English if the requested language is not English
                    if (languageCode != FallbackCode)
                    {
                         Log("Attempting fallback to English...", "INFO");
                         return LoadLanguage(FallbackCode);
                    }

                    return new Language(); // Last resort: empty language
                }

                string json = File.ReadAllText(path);
                var merged = MergeLanguage(json);
                if (merged == null)
                {
                    Log($"Failed to deserialize language: {languageCode}", "WARNING");
                    if (languageCode != FallbackCode) return LoadLanguage(FallbackCode);
                    return new Language();
                }

                var language = JsonSerializer.Deserialize(merged, Utils.JsonContext.Default.Language);
                Log($"Loaded language: {languageCode}", "INFO");
                return language ?? new Language();
            }
            catch (Exception ex)
            {
                Log($"Failed to load language {languageCode}: {ex.Message}", "ERROR");

                // Fallback to English if failure wasn't for English
                if (languageCode != FallbackCode)
                {
                     return LoadLanguage(FallbackCode);
                }

                return new Language();
            }
        }

        /// <summary>
        /// Merges the translation file on top of the English file, filling any
        /// missing or null key with the English value. This is the single source
        /// of fallback translations for the whole app.
        /// </summary>
        private static string MergeLanguage(string languageJson)
        {
            var englishNode = GetEnglishNode();
            var node = JsonNode.Parse(languageJson?.Trim() ?? "{}");
            if (node is JsonObject obj && englishNode != null)
            {
                foreach (var kvp in englishNode)
                {
                    if (obj.TryGetPropertyValue(kvp.Key, out var value) &&
                        value != null && value.GetValueKind() != JsonValueKind.Null)
                        continue;
                    obj[kvp.Key] = kvp.Value?.DeepClone();
                }
            }
            return node?.ToJsonString() ?? "{}";
        }

        private static JsonObject GetEnglishNode()
        {
            if (_englishNode != null) return _englishNode;
            try
            {
                string enPath = Path.Combine(LanguagesFolder, $"{FallbackCode}.json");
                if (File.Exists(enPath))
                    _englishNode = JsonNode.Parse(File.ReadAllText(enPath)) as JsonObject;
            }
            catch (Exception ex)
            {
                Log($"Failed to load English fallback node: {ex.Message}", "ERROR");
            }
            return _englishNode;
        }

        /// <summary>
        /// Discovers the available languages by scanning the Languages folder.
        /// The friendly (document) name is supplied by a curated table; languages
        /// not present in the table fall back to their code/name.
        /// </summary>
        public static List<LanguageInfo> GetAvailableLanguages()
        {
            var languages = new List<LanguageInfo>();
            try
            {
                if (Directory.Exists(LanguagesFolder))
                {
                    foreach (var file in Directory.EnumerateFiles(LanguagesFolder, "*.json"))
                    {
                        string code = Path.GetFileNameWithoutExtension(file);
                        if (string.IsNullOrWhiteSpace(code) || code.Equals("template", StringComparison.OrdinalIgnoreCase))
                            continue;
                        languages.Add(new LanguageInfo { Code = code, Name = DisplayNames.TryGetValue(code, out var name) ? name : code });
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Failed to scan language folder: {ex.Message}", "WARNING");
            }

            // Guarantee English is always present even if the folder is missing/empty.
            if (languages.All(l => !string.Equals(l.Code, FallbackCode, StringComparison.OrdinalIgnoreCase)))
            {
                languages.Insert(0, new LanguageInfo { Code = FallbackCode, Name = DisplayNames[FallbackCode] });
                if (!Directory.Exists(LanguagesFolder))
                    Log("Languages folder not found - defaulting to English", "WARNING");
            }

            return languages.OrderBy(l => l.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>Curated display names (shown in the tray language menu).</summary>
        private static readonly Dictionary<string, string> DisplayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = "English",
            ["id"] = "Indonesian (Bahasa Indonesia)",
            ["ru"] = "Russian (Русский)",
            ["es"] = "Spanish (Español)",
            ["de"] = "German (Deutsch)",
            ["zh-CN"] = "Chinese (简体中文)",
            ["cs"] = "Czech (Čeština)",
            ["nl"] = "Dutch (Nederlands)",
            ["fr"] = "French (Français)",
            ["hi"] = "Hindi (हिन्दी)",
            ["it"] = "Italian (Italiano)",
            ["ja"] = "Japanese (日本語)",
            ["jv"] = "Javanese (Basa Jawa)",
            ["ko"] = "Korean (한국어)",
            ["ms"] = "Malay (Bahasa Melayu)",
            ["fa"] = "Persian (فارسی)",
            ["pt"] = "Portuguese (Português)",
            ["su"] = "Sundanese (Basa Sunda)",
            ["sv"] = "Swedish (Svenska)",
            ["tl"] = "Tagalog (Tagalog)",
            ["th"] = "Thai (ไทย)",
            ["tr"] = "Turkish (Türkçe)",
            ["vi"] = "Vietnamese (Tiếng Việt)"
        };

        private static void Log(string message, string level = "INFO")
        {
            // Delegate to centralized LogService
            LogService.Log(message, level, "LanguageManager");
        }
        // --- Models ---
        public class LanguageInfo
        {
            public string Code { get; set; }
            public string Name { get; set; }
        }
    }
}