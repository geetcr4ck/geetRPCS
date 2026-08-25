/**
 * geetRPCS - Update Checker
 * Checks for application and apps.json updates
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
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

#nullable enable

namespace geetRPCS.Services
{
    internal static class UpdateChecker
    {
        // --- Configuration ---
        private const string GITHUB_API_URL = "https://api.github.com/repos/geetcr4ck/geetRPCS/releases/latest";
        private const string APPS_RAW_URL = "https://raw.githubusercontent.com/geetcr4ck/geetRPCS/main/apps.json";
        private const string WITTY_RAW_URL = "https://raw.githubusercontent.com/geetcr4ck/geetRPCS/main/witty.json";
        private static string CURRENT_VERSION => Utils.AppVersion.VersionText;
        private static readonly string AppFolder = Utils.AppPaths.InstallDir;
        private static readonly string AppsPath = Utils.AppPaths.AppsPath;
        private static readonly string WittyPath = Utils.AppPaths.WittyPath;
        private static System.Threading.Timer? _autoUpdateTimer;
        private static bool _isAutoUpdateInProgress = false;
        private const int MAX_RETRIES = 3;
        private const int RETRY_DELAY_MS = 2000;

        // --- Shared HttpClient (avoids socket exhaustion) ---
        internal static readonly HttpClient SharedHttpClient = CreateSharedHttpClient();
        private static HttpClient CreateSharedHttpClient()
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "geetRPCS-UpdateChecker");
            client.Timeout = TimeSpan.FromSeconds(15);
            return client;
        }

        // --- Retry Helper ---
        private static async Task<T?> RetryAsync<T>(Func<Task<T?>> action, string operationName) where T : class
        {
            for (int attempt = 1; attempt <= MAX_RETRIES; attempt++)
            {
                try
                {
                    return await action();
                }
                catch (Exception ex)
                {
                    Log($"{operationName} attempt {attempt}/{MAX_RETRIES} failed: {ex.Message}", attempt < MAX_RETRIES ? "WARNING" : "ERROR");
                    if (attempt < MAX_RETRIES)
                        await Task.Delay(RETRY_DELAY_MS * attempt);
                }
            }
            return null;
        }

        private static async Task<string?> RetryStringAsync(Func<Task<string>> action, string operationName)
        {
            for (int attempt = 1; attempt <= MAX_RETRIES; attempt++)
            {
                try
                {
                    return await action();
                }
                catch (Exception ex)
                {
                    Log($"{operationName} attempt {attempt}/{MAX_RETRIES} failed: {ex.Message}", attempt < MAX_RETRIES ? "WARNING" : "ERROR");
                    if (attempt < MAX_RETRIES)
                        await Task.Delay(RETRY_DELAY_MS * attempt);
                }
            }
            return null;
        }

        #region ----- Auto Update -----
        // Start background auto-update checker if enabled in settings
        public static void StartAutoUpdateChecker(int intervalHours = 6)
        {
            if (_autoUpdateTimer != null) return; // Already running
            
            try
            {
                // Check immediately on startup (after 30 seconds delay)
                var initialDelay = TimeSpan.FromSeconds(30);
                var interval = TimeSpan.FromHours(intervalHours);
                
                _autoUpdateTimer = new System.Threading.Timer(async _ => await AutoUpdateCheck(), null, initialDelay, interval);
                Log($"Auto-update checker started with {intervalHours}h interval", "INFO");
            }
            catch (Exception ex)
            {
                Log($"Failed to start auto-update checker: {ex.Message}", "ERROR");
            }
        }

        // Stop background auto-update checker
        public static void StopAutoUpdateChecker()
        {
            try
            {
                _autoUpdateTimer?.Dispose();
                _autoUpdateTimer = null;
                Log("Auto-update checker stopped", "INFO");
            }
            catch (Exception ex)
            {
                Log($"Error stopping auto-update checker: {ex.Message}", "ERROR");
            }
        }

        // Background auto-update check - silently downloads and installs updates
        private static async Task AutoUpdateCheck()
        {
            // Skip if auto-update is disabled or already in progress
            if (!SettingsService.Instance.AutoUpdateEnabled || _isAutoUpdateInProgress)
                return;

            try
            {
                _isAutoUpdateInProgress = true;
                Log("Running background auto-update check", "INFO");

                var release = await CheckForUpdates(showUpToDateMessage: false);
                if (release != null)
                {
                    Log($"Auto-update: New version {release.TagName} available - starting silent download", "INFO");

                    // Silent download and install
                    await Task.Run(async () =>
                    {
                        try
                        {
                            var downloader = new UpdateDownloader();

                            // Download without UI
                            Log("Auto-update: Downloading update in background...", "INFO");
                            string? extractedPath = await downloader.PrepareUpdateAsync(release, CancellationToken.None);

                            if (!string.IsNullOrEmpty(extractedPath))
                            {
                                Log($"Auto-update: Download complete, launching updater from {extractedPath}", "INFO");

                                // Launch updater silently
                                if (downloader.LaunchUpdater(extractedPath))
                                {
                                    Log("Auto-update: Updater launched successfully, application will restart", "INFO");

                                    // Give updater time to start
                                    await Task.Delay(1000);

                                    // Exit application to allow update
                                    Application.Exit();
                                }
                                else
                                {
                                    Log("Auto-update: Failed to launch updater", "ERROR");
                                }
                            }
                            else
                            {
                                Log("Auto-update: Download/extraction failed", "ERROR");
                            }
                        }
                        catch (Exception ex)
                        {
                            Log($"Auto-update: Silent update failed - {ex.Message}", "ERROR");
                        }
                    });
                }
                else
                {
                    Log("Auto-update check: Application is up to date", "DEBUG");
                }
            }
            catch (Exception ex)
            {
                Log($"Auto-update check failed: {ex.Message}", "ERROR");
            }
            finally
            {
                _isAutoUpdateInProgress = false;
            }
        }
        #endregion

        #region ----- Update Checks -----
        public static async Task<bool> CheckForAppsUpdate(bool silent = true)
        {
            try
            {
                Log("Checking for apps.json updates", "INFO");
                if (!File.Exists(AppsPath)) return false;
                string localJson = File.ReadAllText(AppsPath);
                string localVersion = "0.0.0.0";
                using (JsonDocument doc = JsonDocument.Parse(localJson))
                {
                    if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
                    {
                        var firstObj = doc.RootElement[0];
                        if (firstObj.TryGetProperty("db_version", out var verProp))
                        {
                            localVersion = verProp.GetString() ?? "0.0.0.0";
                        }
                    }
                }
                string? remoteJson = await RetryStringAsync(
                    () => SharedHttpClient.GetStringAsync(APPS_RAW_URL), "Fetch apps.json");
                if (remoteJson == null) return false;

                string remoteVersion = "0.0.0.0";
                using (JsonDocument doc = JsonDocument.Parse(remoteJson))
                {
                    if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
                    {
                        var firstObj = doc.RootElement[0];
                        if (firstObj.TryGetProperty("db_version", out var verProp))
                        {
                            remoteVersion = verProp.GetString() ?? "0.0.0.0";
                        }
                    }
                }
                Log($"Local Apps Version: {localVersion}, Remote Apps Version: {remoteVersion}", "DEBUG");
                if (IsNewerVersion(remoteVersion, localVersion))
                {
                    Log($"New apps.json version available: {remoteVersion}", "INFO");
                    // silent (the startup/periodic background checks in
                    // UpdateOrchestrator) applies the update without prompting:
                    // those run on threadpool threads, and popping a modal WPF
                    // dialog from a background thread was both unrequested and
                    // blocked the check loop until it was dismissed. Only
                    // interactive callers (silent=false) show the dialog.
                    if (silent || UI.UpdateDialogs.ShowAppsUpdateDialog(remoteVersion))
                    {
                        File.WriteAllText(AppsPath, remoteJson);
                        Log("apps.json updated successfully", "INFO");
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Apps update check failed: {ex.Message}", "ERROR");
            }
            return false;
        }

        public static async Task<bool> CheckForWittyUpdate(bool silent = true)
        {
            try
            {
                Log("Checking for witty.json updates", "INFO");
                if (!File.Exists(WittyPath)) return false;
                string localJson = File.ReadAllText(WittyPath);
                string localVersion = "0.0.0";
                using (JsonDocument doc = JsonDocument.Parse(localJson))
                {
                    if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        if (doc.RootElement.TryGetProperty("_version", out var verProp))
                        {
                            localVersion = verProp.GetString() ?? "0.0.0";
                        }
                    }
                }
                string? remoteJson = await RetryStringAsync(
                    () => SharedHttpClient.GetStringAsync(WITTY_RAW_URL), "Fetch witty.json");
                if (remoteJson == null) return false;

                string remoteVersion = "0.0.0";
                using (JsonDocument doc = JsonDocument.Parse(remoteJson))
                {
                    if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        if (doc.RootElement.TryGetProperty("_version", out var verProp))
                        {
                            remoteVersion = verProp.GetString() ?? "0.0.0";
                        }
                    }
                }
                Log($"Local Witty Version: {localVersion}, Remote Witty Version: {remoteVersion}", "DEBUG");
                if (IsNewerVersion(remoteVersion, localVersion))
                {
                    Log($"New witty.json version available: {remoteVersion}", "INFO");
                    // Same silent semantics as CheckForAppsUpdate: background
                    // checks apply the update without a dialog.
                    if (silent || UI.UpdateDialogs.ShowWittyUpdateDialog(remoteVersion))
                    {
                        File.WriteAllText(WittyPath, remoteJson);
                        Log("witty.json updated successfully", "INFO");
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Witty update check failed: {ex.Message}", "ERROR");
            }
            return false;
        }

        public static async Task<GitHubRelease?> CheckForUpdates(bool showUpToDateMessage = false)
        {
            try
            {
                Log("Checking for updates", "INFO");
                var latestRelease = await FetchLatestRelease();
                if (latestRelease == null)
                {
                    Log("Failed to fetch latest release", "ERROR");
                    if (showUpToDateMessage)
                        UI.Modern.MessageDialog.ShowError(LanguageManager.Current.UpdateCheckFailed,
                            LanguageManager.Current.UpdateAvailableTitle);
                    return null;
                }
                string latestVersion = latestRelease.TagName?.TrimStart('v') ?? "0.0.0";
                Log($"Current version: {CURRENT_VERSION}", "DEBUG");
                Log($"Latest version: {latestVersion}", "DEBUG");
                if (IsNewerVersion(latestVersion, CURRENT_VERSION))
                {
                    Log($"New version available: {latestVersion}", "INFO");
                    return latestRelease;
                }
                else
                {
                    Log("Application is up to date", "INFO");
                    if (showUpToDateMessage)
                        UI.UpdateDialogs.ShowUpToDateDialog();
                    return null;
                }
            }
            catch (Exception ex)
            {
                Log($"Update check failed: {ex.Message}", "ERROR");
                if (showUpToDateMessage)
                    UI.Modern.MessageDialog.ShowError($"{LanguageManager.Current.UpdateCheckFailed}\n\n{ex.Message}",
                        LanguageManager.Current.UpdateAvailableTitle);
                return null;
            }
        }

        private static async Task<GitHubRelease?> FetchLatestRelease()
        {
            return await RetryAsync(async () =>
            {
                string json = await SharedHttpClient.GetStringAsync(GITHUB_API_URL);
                return JsonSerializer.Deserialize(json, Utils.JsonContext.Default.GitHubRelease);
            }, "Fetch GitHub release");
        }
        #endregion

        #region ----- Helpers -----
        internal static bool IsNewerVersion(string latestVersion, string currentVersion)
        {
            try
            {
                var latest = new Version(latestVersion);
                var current = new Version(currentVersion);
                return latest > current;
            }
            catch { return false; }
        }
        private static void Log(string message, string level = "INFO")
        {
            // Delegate to centralized LogService
            LogService.Log(message, level, "UpdateChecker");
        }
        #endregion

        #region ----- GitHub API Model -----
        public class GitHubRelease
        {
            [JsonPropertyName("tag_name")] public string? TagName { get; set; }
            [JsonPropertyName("name")] public string? Name { get; set; }
            [JsonPropertyName("body")] public string? Body { get; set; }
            [JsonPropertyName("html_url")] public string? HtmlUrl { get; set; }
            [JsonPropertyName("published_at")] public DateTime PublishedAt { get; set; }
            [JsonPropertyName("prerelease")] public bool Prerelease { get; set; }
            [JsonPropertyName("assets")] public List<GitHubAsset>? Assets { get; set; }
        }
        
        public class GitHubAsset
        {
            [JsonPropertyName("name")] public string? Name { get; set; }
            [JsonPropertyName("browser_download_url")] public string? BrowserDownloadUrl { get; set; }
            [JsonPropertyName("size")] public long Size { get; set; }
            [JsonPropertyName("download_count")] public int DownloadCount { get; set; }
        }
        #endregion
    }
}
