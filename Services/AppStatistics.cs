/**
 * geetRPCS - Statistics Service
 * Tracks and manages application usage statistics
 */
/*
 * Copyright (c) 2026 makcrtve
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
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
namespace geetRPCS.Services
{
    internal class AppStatistics
    {
        private static readonly string AppFolder = AppDomain.CurrentDomain.BaseDirectory;
        private static readonly string StatsPath = Path.Combine(AppFolder, "statistics.json");

        private static readonly JsonSerializerOptions _readOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new TimeSpanConverter() }
        };

        private static readonly JsonSerializerOptions _writeOptions = new()
        {
            WriteIndented = true,
            Converters = { new TimeSpanConverter() }
        };

        [JsonPropertyName("appUsage")]
        public Dictionary<string, AppUsageData> AppUsage { get; set; } = new();
        [JsonPropertyName("lastUpdated")]
        public DateTime LastUpdated { get; set; } = DateTime.Now;
        [JsonPropertyName("totalTrackedTime")]
        public TimeSpan TotalTrackedTime { get; set; } = TimeSpan.Zero;
        #region ----- Load & Save -----
        public static AppStatistics Load()
        {
            try
            {
                if (!File.Exists(StatsPath))
                {
                    Log("Statistics file not found - creating new", "INFO");
                    return new AppStatistics();
                }
                string json = File.ReadAllText(StatsPath);
                var stats = JsonSerializer.Deserialize(json, Utils.JsonContext.Default.AppStatistics);
                Log($"Loaded {stats.AppUsage.Count} tracked apps", "INFO");
                return stats ?? new AppStatistics();
            }
            catch (Exception ex)
            {
                Log($"Failed to load statistics: {ex.Message}", "ERROR");
                return new AppStatistics();
            }
        }
        private static readonly System.Threading.SemaphoreSlim _fileLock = new System.Threading.SemaphoreSlim(1, 1);
        public string PrepareJson()
        {
            LastUpdated = DateTime.Now;
            return JsonSerializer.Serialize(this, Utils.JsonContext.Default.AppStatistics);
        }
        public static async Task WriteJsonAsync(string json)
        {
            try
            {
                await _fileLock.WaitAsync().ConfigureAwait(false);
                try
                {
                    await File.WriteAllTextAsync(StatsPath, json).ConfigureAwait(false);
                    Log($"Saved statistics (Async)", "INFO");
                }
                finally
                {
                    _fileLock.Release();
                }
            }
            catch (Exception ex) { Log($"Failed to save statistics async: {ex.Message}", "ERROR"); }
        }
        #endregion
        #region ----- Tracking -----
        public void TrackApp(string processName, string appName, TimeSpan duration)
        {
            if (string.IsNullOrEmpty(processName)) return;
            if (!AppUsage.ContainsKey(processName))
                AppUsage[processName] = new AppUsageData
                {
                    ProcessName = processName,
                    AppName = appName,
                    FirstUsed = DateTime.Now
                };
            var data = AppUsage[processName];
            data.AppName = appName;
            data.TotalTime += duration;
            data.LastUsed = DateTime.Now;
            data.SessionCount++;
            var now = DateTime.Now;
            var today = now.Date;
            var weekStart = today.AddDays(-(int)today.DayOfWeek);
            var monthStart = new DateTime(now.Year, now.Month, 1);
            if (!data.DailyUsage.ContainsKey(today)) data.DailyUsage[today] = TimeSpan.Zero;
            data.DailyUsage[today] += duration;
            if (!data.WeeklyUsage.ContainsKey(weekStart)) data.WeeklyUsage[weekStart] = TimeSpan.Zero;
            data.WeeklyUsage[weekStart] += duration;
            if (!data.MonthlyUsage.ContainsKey(monthStart)) data.MonthlyUsage[monthStart] = TimeSpan.Zero;
            data.MonthlyUsage[monthStart] += duration;
            TotalTrackedTime += duration;
        }
        #endregion
        #region ----- Queries -----
        public TimeSpan GetTodayUsage(string processName)
        {
            if (!AppUsage.ContainsKey(processName)) return TimeSpan.Zero;
            var today = DateTime.Now.Date;
            return AppUsage[processName].DailyUsage.ContainsKey(today)
                ? AppUsage[processName].DailyUsage[today] : TimeSpan.Zero;
        }
        public TimeSpan GetThisWeekUsage(string processName)
        {
            if (!AppUsage.ContainsKey(processName)) return TimeSpan.Zero;
            var weekStart = DateTime.Now.Date.AddDays(-(int)DateTime.Now.DayOfWeek);
            return AppUsage[processName].WeeklyUsage.ContainsKey(weekStart)
                ? AppUsage[processName].WeeklyUsage[weekStart] : TimeSpan.Zero;
        }
        public TimeSpan GetThisMonthUsage(string processName)
        {
            if (!AppUsage.ContainsKey(processName)) return TimeSpan.Zero;
            var monthStart = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            return AppUsage[processName].MonthlyUsage.ContainsKey(monthStart)
                ? AppUsage[processName].MonthlyUsage[monthStart] : TimeSpan.Zero;
        }
        public List<(string appName, TimeSpan time)> GetTopAppsToday(int count = 5)
        {
            var today = DateTime.Now.Date;
            return AppUsage.Values.Where(a => a.DailyUsage.ContainsKey(today))
                .Select(a => (a.AppName, a.DailyUsage[today]))
                .OrderByDescending(x => x.Item2).Take(count).ToList();
        }
        public List<(string appName, TimeSpan time)> GetTopAppsAllTime(int count = 5)
        {
            return AppUsage.Values.Select(a => (a.AppName, a.TotalTime))
                .OrderByDescending(x => x.Item2).Take(count).ToList();
        }
        #endregion
        #region ----- Export -----
        public string PrepareCSV()
        {
            var sb = new StringBuilder();
            sb.AppendLine("App Name,Process Name,Total Time (Hours),Sessions,First Used,Last Used");
            foreach (var app in AppUsage.Values.OrderByDescending(a => a.TotalTime))
                sb.AppendLine($"\"{app.AppName}\",\"{app.ProcessName}\"," +
                              $"{app.TotalTime.TotalHours:F2},{app.SessionCount}," +
                              $"{app.FirstUsed:yyyy-MM-dd HH:mm},{app.LastUsed:yyyy-MM-dd HH:mm}");
            return sb.ToString();
        }
        public string PrepareExportJSON()
        {
            return JsonSerializer.Serialize(this, Utils.JsonContext.Default.AppStatistics);
        }
        public async Task<string> WriteExportAsync(string content, string extension)
        {
            try
            {
                string fileName = $"geetRPCS_Statistics_{DateTime.Now:yyyyMMdd_HHmmss}.{extension}";
                string filePath = Path.Combine(AppFolder, fileName);
                await _fileLock.WaitAsync().ConfigureAwait(false);
                try
                {
                    await File.WriteAllTextAsync(filePath, content, Encoding.UTF8).ConfigureAwait(false);
                    Log($"Exported to {extension.ToUpper()}: {fileName}", "INFO");
                    return filePath;
                }
                finally { _fileLock.Release(); }
            }
            catch (Exception ex)
            {
                Log($"Export failed: {ex.Message}", "ERROR");
                return null;
            }
        }
        #endregion
        #region ----- Cleanup -----
        public void CleanupOldData(int daysToKeep = 90)
        {
            var cutoffDate = DateTime.Now.AddDays(-daysToKeep);
            foreach (var app in AppUsage.Values)
            {
                foreach (var key in app.DailyUsage.Keys.Where(d => d < cutoffDate).ToList())
                    app.DailyUsage.Remove(key);
                foreach (var key in app.WeeklyUsage.Keys.Where(d => d < cutoffDate).ToList())
                    app.WeeklyUsage.Remove(key);
                foreach (var key in app.MonthlyUsage.Keys.Where(d => d < cutoffDate).ToList())
                    app.MonthlyUsage.Remove(key);
            }
            Log($"Cleaned data older than {daysToKeep} days", "INFO");
        }
        public async Task ResetAsync()
        {
            AppUsage.Clear();
            TotalTrackedTime = TimeSpan.Zero;
            LastUpdated = DateTime.Now;
            string json = PrepareJson();
            await WriteJsonAsync(json).ConfigureAwait(false);
            Log("Statistics reset to default", "INFO");
        }
        #endregion
        #region ----- Helpers -----
        private static void Log(string message, string level = "INFO")
        {
            // Delegate to centralized LogService
            LogService.Log(message, level, "Statistics");
        }
        #endregion
    }
    #region ----- Data Models -----
    public class AppUsageData
    {
        [JsonPropertyName("processName")] public string ProcessName { get; set; }
        [JsonPropertyName("appName")] public string AppName { get; set; }
        [JsonPropertyName("totalTime")] public TimeSpan TotalTime { get; set; } = TimeSpan.Zero;
        [JsonPropertyName("sessionCount")] public int SessionCount { get; set; } = 0;
        [JsonPropertyName("firstUsed")] public DateTime FirstUsed { get; set; }
        [JsonPropertyName("lastUsed")] public DateTime LastUsed { get; set; }
        [JsonPropertyName("dailyUsage")] public Dictionary<DateTime, TimeSpan> DailyUsage { get; set; } = new();
        [JsonPropertyName("weeklyUsage")] public Dictionary<DateTime, TimeSpan> WeeklyUsage { get; set; } = new();
        [JsonPropertyName("monthlyUsage")] public Dictionary<DateTime, TimeSpan> MonthlyUsage { get; set; } = new();
    }
    public class TimeSpanConverter : JsonConverter<TimeSpan>
    {
        public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String) return TimeSpan.Parse(reader.GetString());
            return TimeSpan.Zero;
        }
        public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }
    #endregion
}
