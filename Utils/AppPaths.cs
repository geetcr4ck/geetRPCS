/**
 * geetRPCS - App Paths
 * Central path provider. Program files (shipped resources) stay next to the
 * executable, while user data (settings, statistics, logs, telemetry, caches)
 * lives in %LOCALAPPDATA%\geetRPCS - matching the documented install layout in
 * install.ps1 / PRIVACY.md regardless of where the portable exe is placed.
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
using System.IO;

namespace geetRPCS.Utils
{
    internal static class AppPaths
    {
        // Directory that contains geetRPCS.exe and the shipped resource files.
        public static string InstallDir { get; } = AppDomain.CurrentDomain.BaseDirectory;

        // Directory for user-generated data (same as InstallDir when the app is
        // installed to %LOCALAPPDATA%\geetRPCS, otherwise a central data folder).
        public static string UserDataDir { get; } = InitializeUserDataDir();

        // --- Application resources (next to the executable) ---
        public static string AppsPath => Path.Combine(InstallDir, "apps.json");
        public static string WittyPath => Path.Combine(InstallDir, "witty.json");
        public static string IconPath => Path.Combine(InstallDir, "rpicon.ico");
        public static string LanguagesDir => Path.Combine(InstallDir, "Languages");

        // --- User data (centralized under %LOCALAPPDATA%\geetRPCS) ---
        public static string ConfigPath => Path.Combine(UserDataDir, "config.json");
        public static string SettingsPath => Path.Combine(UserDataDir, "settings.json");
        public static string StatisticsPath => Path.Combine(UserDataDir, "statistics.json");
        public static string LogPath => Path.Combine(UserDataDir, "geetRPCS.log");
        public static string TelemetryPath => Path.Combine(UserDataDir, ".telemetry");
        public static string TelemetryLogPath => Path.Combine(UserDataDir, "telemetry.log");
        public static string ImageCacheDir => Path.Combine(UserDataDir, "ImageCache");

        private static string InitializeUserDataDir()
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "geetRPCS");
            try { Directory.CreateDirectory(dir); }
            catch { /* Non-fatal: fall back gracefully at write time */ }
            return dir;
        }
    }
}