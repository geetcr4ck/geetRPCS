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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using geetRPCS.UI;
using geetRPCS.UI.Modern;

namespace geetRPCS.Services
{
    internal sealed class StatsCoordinator : IStatsCoordinator
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

        public async Task ResetAsync()
        {
            string json;
            lock (_lock)
            {
                _statistics.AppUsage.Clear();
                _statistics.TotalTrackedTime = TimeSpan.Zero;
                json = _statistics.PrepareJson();
            }
            await AppStatistics.WriteJsonAsync(json).ConfigureAwait(false);
        }

        #region ----- Views -----
        public void ShowToday()
        {
            List<(string appName, TimeSpan time)> topApps;
            lock (_lock) topApps = _statistics.GetTopAppsToday(10);
            var vm = new StatisticsViewModel
            {
                Title = LanguageManager.Current.StatsTodayTitle,
                EmptyMessage = LanguageManager.Current.StatsNoDataToday
            };
            AddRows(vm, topApps);
            if (topApps.Count > 0)
                vm.Totals.Add($"{LanguageManager.Current.StatsTotal} " +
                    FormatTimeSpan(TimeSpan.FromSeconds(topApps.Sum(x => x.time.TotalSeconds))));
            StatisticsWindow.Show(vm);
        }

        public void ShowWeek()
        {
            var weekStart = DateTime.Now.Date.AddDays(-(int)DateTime.Now.DayOfWeek);
            List<(string AppName, TimeSpan Time)> appsThisWeek;
            lock (_lock)
            {
                appsThisWeek = _statistics.AppUsage.Values
                    .Where(a => a.WeeklyUsage.ContainsKey(weekStart))
                    .Select(a => (a.AppName, a.WeeklyUsage[weekStart]))
                    .OrderByDescending(x => x.Item2).Take(10).ToList();
            }
            var vm = new StatisticsViewModel
            {
                Title = LanguageManager.Current.StatsWeekTitle,
                Subtitle = $"{LanguageManager.Current.StatsWeekOf} {weekStart:MMM dd, yyyy}",
                EmptyMessage = LanguageManager.Current.StatsNoDataWeek
            };
            AddRows(vm, appsThisWeek);
            if (appsThisWeek.Count > 0)
                vm.Totals.Add($"{LanguageManager.Current.StatsTotal} " +
                    FormatTimeSpan(TimeSpan.FromSeconds(appsThisWeek.Sum(x => x.Item2.TotalSeconds))));
            StatisticsWindow.Show(vm);
        }

        public void ShowMonth()
        {
            var monthStart = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            List<(string AppName, TimeSpan Time)> appsThisMonth;
            lock (_lock)
            {
                appsThisMonth = _statistics.AppUsage.Values
                    .Where(a => a.MonthlyUsage.ContainsKey(monthStart))
                    .Select(a => (a.AppName, a.MonthlyUsage[monthStart]))
                    .OrderByDescending(x => x.Item2).Take(10).ToList();
            }
            var vm = new StatisticsViewModel
            {
                Title = LanguageManager.Current.StatsMonthTitle,
                Subtitle = $"{monthStart:MMMM yyyy}",
                EmptyMessage = LanguageManager.Current.StatsNoDataMonth
            };
            AddRows(vm, appsThisMonth);
            if (appsThisMonth.Count > 0)
                vm.Totals.Add($"{LanguageManager.Current.StatsTotal} " +
                    FormatTimeSpan(TimeSpan.FromSeconds(appsThisMonth.Sum(x => x.Item2.TotalSeconds))));
            StatisticsWindow.Show(vm);
        }

        public void ShowAllTime()
        {
            List<(string appName, TimeSpan time)> allTimeTop;
            DateTime firstUsed = default;
            TimeSpan totalTrackedTime;
            int totalApps;
            lock (_lock)
            {
                allTimeTop = _statistics.GetTopAppsAllTime(10);
                totalApps = _statistics.AppUsage.Count;
                totalTrackedTime = _statistics.TotalTrackedTime;
                if (totalApps > 0) firstUsed = _statistics.AppUsage.Values.Min(a => a.FirstUsed);
            }
            var vm = new StatisticsViewModel
            {
                Title = LanguageManager.Current.StatsAllTimeTitle,
                EmptyMessage = LanguageManager.Current.StatsNoData
            };
            AddRows(vm, allTimeTop);
            if (allTimeTop.Count > 0)
            {
                vm.Subtitle = $"{LanguageManager.Current.StatsTrackingSince} " +
                    $"{firstUsed:MMM dd, yyyy}";
                vm.Totals.Add($"{LanguageManager.Current.StatsTotalTracked} {FormatTimeSpan(totalTrackedTime)}");
                vm.Totals.Add($"{LanguageManager.Current.StatsTotalApps} {totalApps}");
            }
            StatisticsWindow.Show(vm);
        }

        private static void AddRows(StatisticsViewModel vm, IEnumerable<(string AppName, TimeSpan Time)> apps)
        {
            int rank = 1;
            foreach (var (appName, time) in apps)
                vm.Rows.Add(new StatsRow { Rank = rank++, AppName = appName, TimeText = FormatTimeSpan(time) });
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
                    if (MessageDialog.Confirm(
                        $"{LanguageManager.Current.StatsExportSuccess}\n\n{Path.GetFileName(filePath)}\n\n{LanguageManager.Current.StatsOpenFolder}",
                        LanguageManager.Current.AppName))
                        System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{filePath}\"");
                }
                else MessageDialog.ShowInfo(LanguageManager.Current.StatsExportFailed, LanguageManager.Current.AppName);
            }
            catch (Exception ex)
            {
                LogService.Log($"Export error: {ex.Message}", "ERROR", "Stats");
                MessageDialog.ShowInfo(string.Format(LanguageManager.Current.ErrorExport, ex.Message),
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