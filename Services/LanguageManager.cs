/**
 * geetRPCS - Language Manager
 * Manages application-wide language and localization
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
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using geetRPCS.Models;

namespace geetRPCS.Services
{
    internal static class LanguageManager
    {
        private static readonly string AppFolder = AppDomain.CurrentDomain.BaseDirectory;
        private static readonly string LanguagesFolder = Path.Combine(AppFolder, "Languages");
        private static readonly string SettingsPath = Path.Combine(AppFolder, "settings.json");

        private static Language _currentLanguage;
        private static string _currentLanguageCode = "en";
        public static Language Current => _currentLanguage ?? LoadLanguage("en");
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
                    if (languageCode != "en")
                    {
                         Log("Attempting fallback to English...", "INFO");
                         return LoadLanguage("en");
                    }

                    return new Language(); // Last resort: empty language
                }

                string json = File.ReadAllText(path);
                var language = JsonSerializer.Deserialize(json, Utils.JsonContext.Default.Language);
                Log($"Loaded language: {languageCode}", "INFO");
                return language ?? new Language();
            }
            catch (Exception ex)
            {
                Log($"Failed to load language {languageCode}: {ex.Message}", "ERROR");

                // Fallback to English if failure wasn't for English
                if (languageCode != "en")
                {
                     return LoadLanguage("en");
                }

                return new Language();
            }
        }
        public static List<LanguageInfo> GetAvailableLanguages()
        {
            var languages = new List<LanguageInfo>
            {
                new LanguageInfo { Code = "en", Name = "English" },
                new LanguageInfo { Code = "id", Name = "Indonesian (Bahasa Indonesia)" },
                new LanguageInfo { Code = "ru", Name = "Russian (Русский)" },
                new LanguageInfo { Code = "es", Name = "Spanish (Español)" },
                new LanguageInfo { Code = "de", Name = "German (Deutsch)" },
                new LanguageInfo { Code = "zh-CN", Name = "Chinese (简体中文)" },
                new LanguageInfo { Code = "cs", Name = "Czech (Čeština)" },
                new LanguageInfo { Code = "nl", Name = "Dutch (Nederlands)" },
                new LanguageInfo { Code = "fr", Name = "French (Français)" },
                new LanguageInfo { Code = "hi", Name = "Hindi (हिन्दी)" },
                new LanguageInfo { Code = "it", Name = "Italian (Italiano)" },
                new LanguageInfo { Code = "ja", Name = "Japanese (日本語)" },
                new LanguageInfo { Code = "jv", Name = "Javanese (Basa Jawa)" },
                new LanguageInfo { Code = "ko", Name = "Korean (한국어)" },
                new LanguageInfo { Code = "ms", Name = "Malay (Bahasa Melayu)" },
                new LanguageInfo { Code = "fa", Name = "Persian (فارسی)" },
                new LanguageInfo { Code = "pt", Name = "Portuguese (Português)" },
                new LanguageInfo { Code = "su", Name = "Sundanese (Basa Sunda)" },
                new LanguageInfo { Code = "sv", Name = "Swedish (Svenska)" },
                new LanguageInfo { Code = "tl", Name = "Tagalog (Tagalog)" },
                new LanguageInfo { Code = "th", Name = "Thai (ไทย)" },
                new LanguageInfo { Code = "tr", Name = "Turkish (Türkçe)" },
                new LanguageInfo { Code = "vi", Name = "Vietnamese (Tiếng Việt)" }
            };
            return languages;
        }
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
