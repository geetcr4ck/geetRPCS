/**
 * geetRPCS - Update Downloader
 * Downloads and prepares application updates
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

#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using static geetRPCS.Services.UpdateChecker;

namespace geetRPCS.Services
{
    internal class UpdateDownloader
    {
        // --- Events ---
        public event Action<int, long, long, double>? OnProgressChanged;  // percent, current, total, speed (bytes/s)
        public event Action<string>? OnStatusChanged;
        public event Action<string>? OnError;

        // --- Constants ---
        private static readonly string AppFolder = AppDomain.CurrentDomain.BaseDirectory;

        private static readonly string TempUpdateFolder = Path.Combine(Path.GetTempPath(), "geetRPCS_update");
        private const int BUFFER_SIZE = 81920; // 80 KB buffer for faster downloads

        // Detects the installed version type based on file structure.
        // Portable version is larger and self-contained (>30MB), minimal is small (<10MB).
        public string DetectInstalledVersionType()
        {
            try
            {
                string exePath = Path.Combine(AppFolder, "geetRPCS.exe");
                if (File.Exists(exePath))
                {
                    var fileInfo = new FileInfo(exePath);
                    long sizeInMB = fileInfo.Length / 1024 / 1024;

                    Log($"geetRPCS.exe size: {fileInfo.Length} bytes ({sizeInMB} MB)", "DEBUG");

                    // Portable (self-contained) is typically > 30MB
                    if (fileInfo.Length > 30 * 1024 * 1024)
                    {
                        Log("Detected version type: portable (large executable > 30MB)", "INFO");
                        return "portable";
                    }

                    // Minimal (framework-dependent) is typically < 10MB
                    if (fileInfo.Length < 10 * 1024 * 1024)
                    {
                        Log("Detected version type: minimal (small executable < 10MB)", "INFO");
                        return "minimal";
                    }
                }

                // Fallback: check for multiple DLLs (minimal has many runtime DLLs)
                int dllCount = Directory.GetFiles(AppFolder, "*.dll", SearchOption.TopDirectoryOnly).Length;
                Log($"DLL count in app folder: {dllCount}", "DEBUG");

                if (dllCount > 10)
                {
                    Log("Detected version type: minimal (many DLLs present)", "INFO");
                    return "minimal";
                }

                // Default to portable for safety
                Log("Version type detection inconclusive, defaulting to portable", "INFO");
                return "portable";
            }
            catch (Exception ex)
            {
                Log($"Version type detection failed: {ex.Message}", "ERROR");
                return "portable";
            }
        }

        // Downloads and extracts the update, returning the path to extracted files.
        public async Task<string?> PrepareUpdateAsync(GitHubRelease release, CancellationToken ct = default)
        {
            try
            {
                OnStatusChanged?.Invoke(LanguageManager.Current.UpdatePreparing ?? "Preparing update...");

                // Cleanup any previous update attempt
                CleanupTempFolder();

                // Detect version type
                string versionType = DetectInstalledVersionType();

                // Find matching asset
                var asset = FindMatchingAsset(release, versionType);
                if (asset == null)
                {
                    string error = $"Could not find {versionType} version in release assets";
                    Log(error, "ERROR");
                    OnError?.Invoke(error);
                    return null;
                }

                Log($"Found asset: {asset.Name} ({asset.Size / 1024 / 1024:F1} MB)", "INFO");

                // Create temp folder
                Directory.CreateDirectory(TempUpdateFolder);
                string zipPath = Path.Combine(TempUpdateFolder, asset.Name ?? "update.zip");

                // Download
                OnStatusChanged?.Invoke(LanguageManager.Current.UpdateDownloading ?? "Downloading update...");
                bool downloadSuccess = await DownloadFileAsync(asset.BrowserDownloadUrl!, zipPath, asset.Size, ct);
                
                if (!downloadSuccess || ct.IsCancellationRequested)
                {
                    CleanupTempFolder();
                    return null;
                }

                // Verify download
                if (!File.Exists(zipPath))
                {
                    OnError?.Invoke("Download completed but file not found");
                    return null;
                }

                var downloadedFile = new FileInfo(zipPath);
                if (downloadedFile.Length < asset.Size * 0.95) // Allow 5% tolerance
                {
                    OnError?.Invoke($"Downloaded file is incomplete ({downloadedFile.Length} vs {asset.Size} bytes)");
                    CleanupTempFolder();
                    return null;
                }

                // Extract
                OnStatusChanged?.Invoke(LanguageManager.Current.UpdateExtracting ?? "Extracting files...");
                string extractPath = Path.Combine(TempUpdateFolder, "extracted");
                bool extractSuccess = await ExtractZipAsync(zipPath, extractPath, ct);
                
                if (!extractSuccess || ct.IsCancellationRequested)
                {
                    CleanupTempFolder();
                    return null;
                }

                // Find the actual content folder (might be nested)
                string contentPath = FindContentFolder(extractPath);
                
                Log($"Update prepared successfully at: {contentPath}", "INFO");
                OnStatusChanged?.Invoke(LanguageManager.Current.UpdateReadyRestart ?? "Update ready! geetRPCS will restart.");
                
                return contentPath;
            }
            catch (OperationCanceledException)
            {
                Log("Update preparation cancelled by user", "INFO");
                CleanupTempFolder();
                return null;
            }
            catch (Exception ex)
            {
                Log($"Update preparation failed: {ex.Message}", "ERROR");
                OnError?.Invoke(ex.Message);
                CleanupTempFolder();
                return null;
            }
        }

        // Finds the matching asset for the given version type.
        private GitHubAsset? FindMatchingAsset(GitHubRelease release, string versionType)
        {
            if (release.Assets == null || release.Assets.Count == 0)
            {
                Log("No assets found in release", "ERROR");
                return null;
            }

            foreach (var asset in release.Assets)
            {
                if (asset.Name != null && asset.Name.Contains(versionType, StringComparison.OrdinalIgnoreCase)
                    && asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    return asset;
                }
            }

            // Fallback: try to find any zip file
            foreach (var asset in release.Assets)
            {
                if (asset.Name != null && asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    Log($"Using fallback asset: {asset.Name}", "WARNING");
                    return asset;
                }
            }

            return null;
        }

        // Downloads a file with progress reporting.
        // Uses rolling average for speed calculation for smoother updates.
        private async Task<bool> DownloadFileAsync(string url, string destinationPath, long expectedSize, CancellationToken ct)
        {
            try
            {
                var client = UpdateChecker.SharedHttpClient;

                using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
                response.EnsureSuccessStatusCode();

                long totalBytes = response.Content.Headers.ContentLength ?? expectedSize;
                long downloadedBytes = 0;
                
                // Timing for accurate speed calculation
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                DateTime lastProgressUpdate = DateTime.Now;
                
                // Rolling average for smoother speed display (last 5 samples)
                const int SPEED_SAMPLE_COUNT = 5;
                var speedSamples = new Queue<double>();
                long lastDownloadedBytes = 0;
                DateTime lastSpeedSampleTime = DateTime.Now;

                using var contentStream = await response.Content.ReadAsStreamAsync(ct);
                using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, BUFFER_SIZE, true);
                
                var buffer = new byte[BUFFER_SIZE];
                int bytesRead;

                // Initial progress
                OnProgressChanged?.Invoke(0, 0, totalBytes, 0);

                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead, ct);
                    downloadedBytes += bytesRead;

                    // Update progress every 150ms for smooth UI updates
                    var now = DateTime.Now;
                    if ((now - lastProgressUpdate).TotalMilliseconds > 150)
                    {
                        // Calculate instantaneous speed for this interval
                        double intervalSeconds = (now - lastSpeedSampleTime).TotalSeconds;
                        if (intervalSeconds > 0)
                        {
                            long bytesThisInterval = downloadedBytes - lastDownloadedBytes;
                            double instantSpeed = bytesThisInterval / intervalSeconds;
                            
                            // Add to rolling average
                            speedSamples.Enqueue(instantSpeed);
                            if (speedSamples.Count > SPEED_SAMPLE_COUNT)
                                speedSamples.Dequeue();
                            
                            lastDownloadedBytes = downloadedBytes;
                            lastSpeedSampleTime = now;
                        }

                        // Calculate average speed from samples
                        double averageSpeed = speedSamples.Count > 0 
                            ? speedSamples.Average() 
                            : (stopwatch.Elapsed.TotalSeconds > 0 ? downloadedBytes / stopwatch.Elapsed.TotalSeconds : 0);

                        // Calculate accurate percentage
                        int percent = totalBytes > 0 
                            ? (int)Math.Min(99, (downloadedBytes * 100) / totalBytes)  // Cap at 99% until truly done
                            : 0;
                        
                        OnProgressChanged?.Invoke(percent, downloadedBytes, totalBytes, averageSpeed);
                        lastProgressUpdate = now;
                    }
                }

                // Ensure file is flushed
                await fileStream.FlushAsync(ct);
                
                // Final progress update - now we're truly at 100%
                double finalSpeed = stopwatch.Elapsed.TotalSeconds > 0 
                    ? downloadedBytes / stopwatch.Elapsed.TotalSeconds 
                    : 0;
                OnProgressChanged?.Invoke(100, downloadedBytes, totalBytes, finalSpeed);
                
                stopwatch.Stop();
                Log($"Download complete: {downloadedBytes / 1024.0 / 1024.0:F2} MB in {stopwatch.Elapsed.TotalSeconds:F1}s ({finalSpeed / 1024.0 / 1024.0:F2} MB/s avg)", "INFO");
                return true;
            }
            catch (OperationCanceledException)
            {
                Log("Download cancelled", "INFO");
                return false;
            }
            catch (Exception ex)
            {
                Log($"Download failed: {ex.Message}", "ERROR");
                OnError?.Invoke(LanguageManager.Current.UpdateDownloadFailed ?? "Download failed. Please try another method.");
                return false;
            }
        }

        // Extracts a ZIP file to the specified path.
        private async Task<bool> ExtractZipAsync(string zipPath, string extractPath, CancellationToken ct)
        {
            try
            {
                await Task.Run(() =>
                {
                    if (Directory.Exists(extractPath))
                    {
                        Directory.Delete(extractPath, true);
                    }

                    ZipFile.ExtractToDirectory(zipPath, extractPath);
                }, ct);

                Log($"Extraction complete: {extractPath}", "INFO");
                return true;
            }
            catch (OperationCanceledException)
            {
                Log("Extraction cancelled", "INFO");
                return false;
            }
            catch (Exception ex)
            {
                Log($"Extraction failed: {ex.Message}", "ERROR");
                OnError?.Invoke($"Extraction failed: {ex.Message}");
                return false;
            }
        }

        // Finds the actual content folder (handles nested folder in ZIP).
        private string FindContentFolder(string extractPath)
        {
            var dirs = Directory.GetDirectories(extractPath);
            var files = Directory.GetFiles(extractPath);

            // If there's only one folder and no files, the content is nested
            if (dirs.Length == 1 && files.Length == 0)
            {
                string nestedFolder = dirs[0];
                // Verify it contains the expected content
                if (File.Exists(Path.Combine(nestedFolder, "geetRPCS.exe")))
                {
                    Log($"Found nested content folder: {nestedFolder}", "INFO");
                    return nestedFolder;
                }
            }

            // Check if current folder is the content folder
            if (File.Exists(Path.Combine(extractPath, "geetRPCS.exe")))
            {
                return extractPath;
            }

            // Search deeper
            foreach (var dir in dirs)
            {
                if (File.Exists(Path.Combine(dir, "geetRPCS.exe")))
                {
                    return dir;
                }
            }

            // Fallback
            return extractPath;
        }

        // Cleans up the temporary update folder.
        public void CleanupTempFolder()
        {
            try
            {
                if (Directory.Exists(TempUpdateFolder))
                {
                    Directory.Delete(TempUpdateFolder, true);
                    Log("Cleaned up temp update folder", "INFO");
                }
            }
            catch (Exception ex)
            {
                Log($"Failed to cleanup temp folder: {ex.Message}", "WARNING");
            }
        }

        // Launches the updater helper to perform the actual update.
        public bool LaunchUpdater(string sourcePath)
        {
            try
            {
                string updaterPath = Path.Combine(AppFolder, "Updater.exe");
                
                if (!File.Exists(updaterPath))
                {
                    Log($"Updater.exe not found at: {updaterPath}", "ERROR");
                    OnError?.Invoke("Updater.exe not found. Please use PowerShell or GitHub method.");
                    return false;
                }

                string targetPath = AppFolder.TrimEnd('\\', '/');
                string exeName = "geetRPCS.exe";
                
                var startInfo = new ProcessStartInfo
                {
                    FileName = updaterPath,
                    Arguments = $"--source \"{sourcePath}\" --target \"{targetPath}\" --exe \"{exeName}\"",
                    UseShellExecute = true,
                    CreateNoWindow = false
                };

                Log($"Launching updater: {startInfo.FileName} {startInfo.Arguments}", "INFO");
                Process.Start(startInfo);
                return true;
            }
            catch (Exception ex)
            {
                Log($"Failed to launch updater: {ex.Message}", "ERROR");
                OnError?.Invoke($"Failed to launch updater: {ex.Message}");
                return false;
            }
        }

        // Gets the path to the temp update folder.
        public static string GetTempUpdateFolder() => TempUpdateFolder;

        private static void Log(string message, string level = "INFO")
        {
            // Delegate to centralized LogService
            LogService.Log(message, level, "UpdateDownloader");
        }
    }
}
