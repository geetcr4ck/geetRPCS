/**
 * geetRPCS - Taskbar Watcher
 * Monitors active windows and taskbar changes
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
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using geetRPCS.Models;

namespace geetRPCS.Services
{
    internal static class TaskbarWatcher
    {
        public delegate void AppChanged(string processName, string details, string state, IntPtr hWnd);
        private static AppChanged _callback;
        private static string _lastFound, _lastTitle;
        private static readonly object _lock = new object();
        private static IntPtr _hookHandle;
        private static WinEventDelegate _eventDelegate;
        private static System.Threading.Timer _debounceTimer;
        private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
        private const uint WINEVENT_OUTOFCONTEXT = 0;
        private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        public static void Reload()
        {
            AppConfigManager.Reload();
            lock (_lock)
            {
                _lastFound = null;
                _lastTitle = null;
            }
            CheckCurrentApp();
        }

        private static System.Threading.Timer _livenessTimer;

        public static void Start(AppChanged callback)
        {
            _callback = callback;
            _eventDelegate = new WinEventDelegate(WinEventProc);

            _debounceTimer = new System.Threading.Timer(_ => CheckCurrentApp(), null, Timeout.Infinite, Timeout.Infinite);
            _livenessTimer = new System.Threading.Timer(_ => CheckLiveness(), null, 3000, 3000);

            _hookHandle = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, _eventDelegate, 0, 0, WINEVENT_OUTOFCONTEXT);

            CheckCurrentApp();
        }

        private static void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if (eventType == EVENT_SYSTEM_FOREGROUND)
            {
                _debounceTimer.Change(250, Timeout.Infinite);
            }
        }

        private static void CheckCurrentApp()
        {
            var (proc, hwnd, title) = GetCurrentApp();
            lock (_lock)
            {
                if (proc != null)
                {
                    if (proc == _lastFound && title == _lastTitle)
                        return;
                    _lastFound = proc;
                    _lastTitle = title;
                    _callback?.Invoke(proc, null, title, hwnd);
                }
                else
                {
                    if (_lastFound != null)
                    {
                        if (IsProcessRunning(_lastFound))
                        {
                            return;
                        }
                        else
                        {
                            _lastFound = null;
                            _lastTitle = null;
                            _callback?.Invoke("config", null, null, IntPtr.Zero);
                        }
                    }
                }
            }
        }

        private static void CheckLiveness()
        {
            lock (_lock)
            {
                if (_lastFound != null)
                {
                    if (!IsProcessRunning(_lastFound))
                    {
                        _lastFound = null;
                        _lastTitle = null;
                        _callback?.Invoke("config", null, null, IntPtr.Zero);
                    }
                }
            }
        }

        private static bool IsProcessRunning(string processName)
        {
            try
            {
                var processes = Process.GetProcessesByName(processName);
                bool running = processes.Length > 0;
                foreach (var p in processes) p.Dispose();
                return running;
            }
            catch { return false; }
        }

        private static (string processName, IntPtr hWnd, string title) GetCurrentApp()
        {
            IntPtr foregroundHwnd = GetForegroundWindow();
            if (foregroundHwnd != IntPtr.Zero)
            {
                GetWindowThreadProcessId(foregroundHwnd, out uint pid);
                if (pid != 0)
                {
                    try
                    {
                        using var p = Process.GetProcessById((int)pid);
                        string procName = p.ProcessName;
                        string title = GetWindowTitle(foregroundHwnd);

                        // 1. Check Exact Matches
                        var exactMatches = AppConfigManager.Apps.Where(a => 
                            AppConfigManager.ExactProcessNames.Contains(procName) &&
                            a.Process != null && 
                            a.Process.Equals(procName, StringComparison.OrdinalIgnoreCase)).ToList();

                        // 2. Check Advanced Matches
                        var advancedMatches = AppConfigManager.AdvancedProcessApps.Where(a => 
                        {
                            if (string.IsNullOrEmpty(a.ProcessMatchMode) || string.IsNullOrEmpty(a.Process)) return false;
                            
                            string mode = a.ProcessMatchMode.ToLower();
                            if (mode == "regex" && a.ProcessRegex != null)
                                return a.ProcessRegex.IsMatch(procName);
                            if (mode == "contains")
                                return procName.IndexOf(a.Process, StringComparison.OrdinalIgnoreCase) >= 0;
                            if (mode == "startswith")
                                return procName.StartsWith(a.Process, StringComparison.OrdinalIgnoreCase);
                            if (mode == "endswith")
                                return procName.EndsWith(a.Process, StringComparison.OrdinalIgnoreCase);
                            
                            return false;
                        }).ToList();

                        var allMatches = exactMatches.Concat(advancedMatches).ToList();

                        if (allMatches.Count > 0)
                        {
                            // Try to find a match based on Window Title rules
                            var titleMatch = allMatches.FirstOrDefault(a => 
                            {
                                if (string.IsNullOrEmpty(a.WindowTitle)) return false;

                                string mode = a.TitleMatchMode?.ToLower() ?? "contains"; // Default is contains
                                
                                if (mode == "exact")
                                    return title.Equals(a.WindowTitle, StringComparison.OrdinalIgnoreCase);
                                if (mode == "regex" && a.TitleRegex != null)
                                    return a.TitleRegex.IsMatch(title);
                                if (mode == "startswith")
                                    return title.StartsWith(a.WindowTitle, StringComparison.OrdinalIgnoreCase);
                                if (mode == "endswith")
                                    return title.EndsWith(a.WindowTitle, StringComparison.OrdinalIgnoreCase);
                                
                                // default: contains
                                return title.IndexOf(a.WindowTitle, StringComparison.OrdinalIgnoreCase) >= 0;
                            });

                            if (titleMatch != null) return (procName, foregroundHwnd, title);

                            var defaultMatch = allMatches.FirstOrDefault(a => string.IsNullOrEmpty(a.WindowTitle));
                            if (defaultMatch != null) return (procName, foregroundHwnd, title);
                        }
                    }
                    catch
                    {
                    }
                }
            }
            return (null, IntPtr.Zero, null);
        }
        private static string GetWindowTitle(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return "";
            int len = GetWindowTextLengthW(hWnd);
            if (len <= 0) return "";
            var sb = new StringBuilder(len + 1);
            GetWindowTextW(hWnd, sb, sb.Capacity);
            return sb.ToString();
        }
        #region ----- Win32 -----
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowTextLengthW(IntPtr hWnd);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowTextW(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);
        [DllImport("user32.dll")]
        private static extern bool UnhookWinEvent(IntPtr hWinEventHook);
        #endregion
    }
}
