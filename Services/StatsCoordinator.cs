/**
 * geetRPCS - Statistics Coordinator
 * Centralizes usage-statistics views and exports (today / week / month / all-time)
 * on top of the AppStatistics store.
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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using geetRPCS.UI;

namespace geetRPCS.Services
{
    internal sealed class StatsCoordinator
    {
        private readonly AppStatistics _statistics;
        private readonly object _lock;

        public StatsCoordinator(AppStatistics statistics, object lockObject)
        {
            _statistics = statistics;
            _lock = lockObject;
        }

        public AppStatistics Statistics => _statistics;

        /// <summary>Attributée the last idle gap to the newly foreground app.</summary>
        public void TrackUsage(string processName, string appName, TimeSpan duration)
        {
            if (string.IsNullOrEmpty(processName) || duration <= TimeSpan.Zero) return;
            lock (_lock) { _statistics.TrackApp(processName, appName, duration); }
        }

        public string PrepareJson()
        {
            lock (_lock) { return _statistics.PrepareJson(); }
        }

        public Task ResetAsync() => _statistics.ResetAsync();

        #region ----- Views -----
        public void ShowToday()
        {
            var topApps = _statistics.GetTopAppsToday(10);
            if (topApps.Count == 0)
            {
                InfoDialog.Show(LanguageManager.Current.StatsNoDataToday, LanguageManager.Current.MenuToday);
                return;
            }
            var sb = new StringBuilder();
            sb.AppendLine(LanguageManager.Current.StatsTodayTitle);
            sb.AppendLine("=============\n");
            int rank = 1;
            foreach (var (appName, time) in topApps)
            {
                sb.AppendLine($"{rank}. {appName}");
                sb.AppendLine($"   {FormatTimeSpan(time)}\n");
                rank++;
            }
            var totalToday = topApps.Sum(x => x.time.TotalSeconds);
            sb.AppendLine($"{LanguageManager.Current.StatsTotal} {FormatTimeSpan(TimeSpan.FromSeconds(totalToday))}");
            InfoDialog.Show(sb.ToString(), LanguageManager.Current.MenuToday);
        }

        public void ShowWeek()
        {
            var weekStart = DateTime.Now.Date.AddDays(-(int)DateTime.Now.DayOfWeek);
            var sb = new StringBuilder();
            sb.AppendLine(LanguageManager.Current.StatsWeekTitle);
            sb.AppendLine($"{LanguageManager.Current.StatsWeekOf} {weekStart:MMM dd, yyyy}");
            sb.AppendLine("=================\n");
            var appsThisWeek = _statistics.AppUsage.Values
                .Where(a => a.WeeklyUsage.ContainsKey(weekStart))
                .Select(a => (a.AppName, a.WeeklyUsage[weekStart]))
                .OrderByDescending(x => x.Item2).Take(10).ToList();
            if (appsThisWeek.Count == 0)
            {
                InfoDialog.Show(LanguageManager.Current.StatsNoDataWeek, LanguageManager.Current.MenuThisWeek);
                return;
            }
            int rank = 1;
            foreach (var (appName, time) in appsThisWeek)
            {
                sb.AppendLine($"{rank}. {appName}");
                sb.AppendLine($"   {FormatTimeSpan(time)}\n");
                rank++;
            }
            var totalWeek = appsThisWeek.Sum(x => x.Item2.TotalSeconds);
            sb.AppendLine($"{LanguageManager.Current.StatsTotal} {FormatTimeSpan(TimeSpan.FromSeconds(totalWeek))}");
            InfoDialog.Show(sb.ToString(), LanguageManager.Current.MenuThisWeek);
        }

        public void ShowMonth()
        {
            var monthStart = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var sb = new StringBuilder();
            sb.AppendLine(LanguageManager.Current.StatsMonthTitle);
            sb.AppendLine($"{monthStart:MMMM yyyy}");
            sb.AppendLine("==================\n");
            var appsThisMonth = _statistics.AppUsage.Values
                .Where(a => a.MonthlyUsage.ContainsKey(monthStart))
                .Select(a => (a.AppName, a.MonthlyUsage[monthStart]))
                .OrderByDescending(x => x.Item2).Take(10).ToList();
            if (appsThisMonth.Count == 0)
            {
                InfoDialog.Show(LanguageManager.Current.StatsNoDataMonth, LanguageManager.Current.MenuThisMonth);
                return;
            }
            int rank = 1;
            foreach (var (appName, time) in appsThisMonth)
            {
                sb.AppendLine($"{rank}. {appName}");
                sb.AppendLine($"   {FormatTimeSpan(time)}\n");
                rank++;
            }
            var totalMonth = appsThisMonth.Sum(x => x.Item2.TotalSeconds);
            sb.AppendLine($"{LanguageManager.Current.StatsTotal} {FormatTimeSpan(TimeSpan.FromSeconds(totalMonth))}");
            InfoDialog.Show(sb.ToString(), LanguageManager.Current.MenuThisMonth);
        }

        public void ShowAllTime()
        {
            var allTimeTop = _statistics.GetTopAppsAllTime(10);
            if (allTimeTop.Count == 0)
            {
                InfoDialog.Show(LanguageManager.Current.StatsNoData, LanguageManager.Current.MenuAllTime);
                return;
            }
            var sb = new StringBuilder();
            sb.AppendLine(LanguageManager.Current.StatsAllTimeTitle);
            sb.AppendLine($"{LanguageManager.Current.StatsTrackingSince} {_statistics.AppUsage.Values.Min(a => a.FirstUsed):MMM dd, yyyy}");
            sb.AppendLine("===================\n");
            int rank = 1;
            foreach (var (appName, time) in allTimeTop)
            {
                sb.AppendLine($"{rank}. {appName}");
                sb.AppendLine($"   {FormatTimeSpan(time)}\n");
                rank++;
            }
            sb.AppendLine($"{LanguageManager.Current.StatsTotalTracked} {FormatTimeSpan(_statistics.TotalTrackedTime)}");
            sb.AppendLine($"{LanguageManager.Current.StatsTotalApps} {_statistics.AppUsage.Count}");
            InfoDialog.Show(sb.ToString(), LanguageManager.Current.MenuAllTime);
        }

        public async void ExportAsync(string format)
        {
            try
            {
                string content;
                lock (_lock)
                {
                    content = format == "csv" ? _statistics.PrepareCSV() : _statistics.PrepareExportJSON();
                }
                string filePath = await _statistics.WriteExportAsync(content, format);
                if (filePath != null && File.Exists(filePath))
                {
                    if (ConfirmDialog.Show(
                        $"{LanguageManager.Current.StatsExportSuccess}\n\n{Path.GetFileName(filePath)}\n\n{LanguageManager.Current.StatsOpenFolder}",
                        LanguageManager.Current.AppName))
                        System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{filePath}\"");
                }
                else InfoDialog.Show(LanguageManager.Current.StatsExportFailed, LanguageManager.Current.AppName);
            }
            catch (Exception ex)
            {
                LogService.Log($"Export error: {ex.Message}", "ERROR", "Stats");
                InfoDialog.Show(string.Format(LanguageManager.Current.ErrorExport, ex.Message),
                    LanguageManager.Current.AppName);
            }
        }
        #endregion

        public static string FormatTimeSpan(TimeSpan time)
        {
            if (time.TotalHours >= 1) return $"{(int)time.TotalHours}h {time.Minutes}m";
            else if (time.TotalMinutes >= 1) return $"{(int)time.TotalMinutes}m {time.Seconds}s";
            else return $"{(int)time.TotalSeconds}s";
        }
    }
}