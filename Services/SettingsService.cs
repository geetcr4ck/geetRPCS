/**
 * geetRPCS - Settings Service
 * Manages persistent application settings
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
using System.Threading.Tasks;
using geetRPCS.Models;

namespace geetRPCS.Services
{
    internal class SettingsService
    {
        private static readonly string SettingsPath = Utils.AppPaths.SettingsPath;
        private static readonly object _lock = new object();
        private static SettingsService _instance;
        private static AppSettings _settings;
        public static SettingsService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new SettingsService();
                    }
                }
                return _instance;
            }
        }
        private SettingsService()
        {
            Load();
        }
        // --- Property Accessors ---
        public string Language
        {
            get { lock (_lock) { return _settings.Language; } }
            set { lock (_lock) { _settings.Language = value; } }
        }
        public List<string> DisabledApps
        {
            get { lock (_lock) { return _settings.DisabledApps; } }
            set { lock (_lock) { _settings.DisabledApps = value; } }
        }
        public bool MouseEnergyEnabled
        {
            get { lock (_lock) { return _settings.MouseEnergyEnabled; } }
            set { lock (_lock) { _settings.MouseEnergyEnabled = value; } }
        }
        public bool TrayAnimationEnabled
        {
            get { lock (_lock) { return _settings.TrayAnimationEnabled; } }
            set { lock (_lock) { _settings.TrayAnimationEnabled = value; } }
        }
        public bool TelemetryEnabled
        {
            get { lock (_lock) { return _settings.TelemetryEnabled; } }
            set { lock (_lock) { _settings.TelemetryEnabled = value; } }
        }
        public string UpdateNotificationMode
        {
            get { lock (_lock) { return _settings.UpdateNotificationMode; } }
            set { lock (_lock) { _settings.UpdateNotificationMode = value; } }
        }
        public Dictionary<string, AppOverrideConfig> AppOverrides
        {
            get { lock (_lock) { return _settings.AppOverrides; } }
            set { lock (_lock) { _settings.AppOverrides = value; } }
        }
        public string LogLevel
        {
            get { lock (_lock) { return _settings.LogLevel; } }
            set { lock (_lock) { _settings.LogLevel = value; } }
        }
        public bool AutoUpdateEnabled
        {
            get { lock (_lock) { return _settings.AutoUpdateEnabled; } }
            set { lock (_lock) { _settings.AutoUpdateEnabled = value; } }
        }
        public ShortcutPreferences ShortcutPreferences
        {
            get { lock (_lock) { return _settings.ShortcutPreferences; } }
            set { lock (_lock) { _settings.ShortcutPreferences = value; } }
        }
        private static void Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    string json = File.ReadAllText(SettingsPath);
                    _settings = JsonSerializer.Deserialize(json, Utils.JsonContext.Default.AppSettings) ?? new AppSettings();
                }
                else
                {
                    _settings = new AppSettings();
                }
            }
            catch
            {
                _settings = new AppSettings();
            }
        }
        private static readonly System.Threading.SemaphoreSlim _fileLock = new System.Threading.SemaphoreSlim(1, 1);
        public static async Task SaveAsync()
        {
            try
            {
                string json;
                lock (_lock)
                {
                    json = JsonSerializer.Serialize(_settings, Utils.JsonContext.Default.AppSettings);
                }
                await _fileLock.WaitAsync().ConfigureAwait(false);
                try
                {
                    await File.WriteAllTextAsync(SettingsPath, json).ConfigureAwait(false);
                }
                finally
                {
                    _fileLock.Release();
                }
            }
            catch (Exception ex)
            {
                try
                {
                    string logPath = Utils.AppPaths.LogPath;
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    File.AppendAllText(logPath, $"[{timestamp}] [SettingsService] [ERROR] Failed to save settings: {ex.Message}\r\n");
                }
                catch { /* If logging fails, we can't do much */ }
            }
        }
        public static void Reload()
        {
            lock (_lock)
            {
                Load();
            }
        }
    }
}
