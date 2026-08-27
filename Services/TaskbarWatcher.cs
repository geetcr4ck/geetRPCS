/**
 * geetRPCS - Taskbar Watcher
 * Monitors active windows and taskbar changes
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
        private static IntPtr _lastHWnd;
        private static uint _lastPid;
        // Foreground HWND tracked from EVENT_SYSTEM_FOREGROUND so the hot
        // NAMECHANGE filter does not need a GetForegroundWindow() P/Invoke for
        // every title change in the system (hundreds/s on a busy desktop).
        private static IntPtr _cachedForegroundHwnd;
        // process name -> config matches (exact + advanced), TTL-bounded so a
        // remotely updated apps.json is picked up without an explicit Reload.
        private static readonly Dictionary<string, List<AppConfig>> _matchCache =
            new Dictionary<string, List<AppConfig>>(StringComparer.OrdinalIgnoreCase);
        private static DateTime _matchCacheStamp = DateTime.UtcNow;
        private static readonly TimeSpan MatchCacheTtl = TimeSpan.FromMinutes(5);
        private static readonly object _lock = new object();
        private static readonly object _cacheLock = new object();
        private static bool _started;
        private static IntPtr _hookHandle;
        private static IntPtr _nameChangeHookHandle;
        private static WinEventDelegate _eventDelegate;
        private static System.Threading.Timer _debounceTimer;
        private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
        private const uint EVENT_OBJECT_NAMECHANGE = 0x800C;
        private const uint WINEVENT_OUTOFCONTEXT = 0;
        private const int OBJID_WINDOW = 0;
        private const int CHILDID_SELF = 0;
        private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        public static void Reload()
        {
            AppConfigManager.Reload();
            lock (_lock)
            {
                _lastFound = null;
                _lastTitle = null;
                _lastHWnd = IntPtr.Zero;
                _lastPid = 0;
            }
            lock (_cacheLock)
            {
                _matchCache.Clear();
                _matchCacheStamp = DateTime.UtcNow;
            }
            CheckCurrentApp();
        }

        private static System.Threading.Timer _livenessTimer;

        public static void Start(AppChanged callback)
        {
            lock (_lock)
            {
                if (_started) return;
                _started = true;
                _callback = callback;
                _eventDelegate = new WinEventDelegate(WinEventProc);

                _debounceTimer = new System.Threading.Timer(_ => CheckCurrentApp(), null, Timeout.Infinite, Timeout.Infinite);
                _livenessTimer = new System.Threading.Timer(_ => CheckLiveness(), null, 3000, 3000);

                _hookHandle = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, _eventDelegate, 0, 0, WINEVENT_OUTOFCONTEXT);
                _nameChangeHookHandle = SetWinEventHook(EVENT_OBJECT_NAMECHANGE, EVENT_OBJECT_NAMECHANGE, IntPtr.Zero, _eventDelegate, 0, 0, WINEVENT_OUTOFCONTEXT);
                _cachedForegroundHwnd = GetForegroundWindow();
            }
            CheckCurrentApp();
        }

        public static void Stop()
        {
            System.Threading.Timer debounceTimer;
            System.Threading.Timer livenessTimer;
            IntPtr hookHandle;
            IntPtr nameChangeHookHandle;
            lock (_lock)
            {
                if (!_started) return;
                _started = false;
                debounceTimer = _debounceTimer;
                livenessTimer = _livenessTimer;
                hookHandle = _hookHandle;
                nameChangeHookHandle = _nameChangeHookHandle;
                _debounceTimer = null;
                _livenessTimer = null;
                _hookHandle = IntPtr.Zero;
                _nameChangeHookHandle = IntPtr.Zero;
                _callback = null;
                _eventDelegate = null;
                _cachedForegroundHwnd = IntPtr.Zero;
            }
            debounceTimer?.Dispose();
            livenessTimer?.Dispose();
            if (hookHandle != IntPtr.Zero) UnhookWinEvent(hookHandle);
            if (nameChangeHookHandle != IntPtr.Zero) UnhookWinEvent(nameChangeHookHandle);
        }

        private static void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if (!_started) return;
            if (eventType == EVENT_SYSTEM_FOREGROUND)
            {
                _cachedForegroundHwnd = hwnd;
                _debounceTimer?.Change(250, Timeout.Infinite);
            }
            // NAMECHANGE fires for EVERY title/name change system-wide; compare
            // against the cached foreground HWND (plain field compare) instead
            // of a GetForegroundWindow() P/Invoke per event. If the cache is
            // ever stale, the 3s liveness poll still re-reads the title.
            else if (eventType == EVENT_OBJECT_NAMECHANGE
                && hwnd != IntPtr.Zero
                && idObject == OBJID_WINDOW
                && idChild == CHILDID_SELF
                && hwnd == _cachedForegroundHwnd)
            {
                _debounceTimer?.Change(250, Timeout.Infinite);
            }
        }

        private static void CheckCurrentApp()
        {
            if (!_started) return;
            var (proc, pid, hwnd, title) = GetCurrentApp();
            AppChanged callback = null;
            string callbackProc = null, callbackDetails = null, callbackState = null;
            IntPtr callbackHwnd = IntPtr.Zero;
            lock (_lock)
            {
                if (!_started) return;
                if (proc != null)
                {
                    if (proc == _lastFound && hwnd == _lastHWnd && title == _lastTitle)
                        return;
                    _lastFound = proc;
                    _lastTitle = title;
                    _lastHWnd = hwnd;
                    _lastPid = pid;
                    callback = _callback;
                    callbackProc = proc;
                    callbackState = title;
                    callbackHwnd = hwnd;
                }
                else if (_lastFound != null && !IsProcessAlive(_lastPid))
                {
                    _lastFound = null;
                    _lastTitle = null;
                    _lastHWnd = IntPtr.Zero;
                    _lastPid = 0;
                    callback = _callback;
                    callbackProc = "config";
                }
            }
            callback?.Invoke(callbackProc, callbackDetails, callbackState, callbackHwnd);
        }

        private static void CheckLiveness()
        {
            if (!_started) return;
            bool checkForTitleChange = false;
            AppChanged callback = null;
            lock (_lock)
            {
                if (!_started) return;
                if (_lastFound != null)
                {
                    if (!IsProcessAlive(_lastPid))
                    {
                        _lastFound = null;
                        _lastTitle = null;
                        _lastHWnd = IntPtr.Zero;
                        _lastPid = 0;
                        callback = _callback;
                    }
                    else
                    {
                        checkForTitleChange = true;
                    }
                }
            }
            callback?.Invoke("config", null, null, IntPtr.Zero);
            if (checkForTitleChange) CheckCurrentApp();
        }

        /// <summary>Liveness by PID: one GetProcessById lookup instead of the
        /// full system snapshot Process.GetProcessesByName used to allocate
        /// every 3s while a tracked app was in the foreground.</summary>
        private static bool IsProcessAlive(uint pid)
        {
            if (pid == 0) return false;
            try
            {
                using var p = Process.GetProcessById((int)pid);
                return true;
            }
            catch (ArgumentException) { return false; } // PID no longer exists
            catch { return false; }
        }

        internal static bool IsWindowForProcess(string processName, IntPtr hWnd)
        {
            if (string.IsNullOrEmpty(processName) || hWnd == IntPtr.Zero || !IsWindow(hWnd))
                return false;

            GetWindowThreadProcessId(hWnd, out uint pid);
            if (pid == 0) return false;

            try
            {
                using var process = Process.GetProcessById((int)pid);
                return process.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static (string processName, uint pid, IntPtr hWnd, string title) GetCurrentApp()
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

                        var allMatches = GetMatchesFor(procName);

                        if (allMatches.Count > 0)
                        {
                            // Try to find a match based on Window Title rules
                            var titleMatch = allMatches.FirstOrDefault(a =>
                            {
                                if (string.IsNullOrEmpty(a.WindowTitle)) return false;

                                string mode = a.TitleMatchMode ?? "contains";

                                if (mode.Equals("exact", StringComparison.OrdinalIgnoreCase))
                                    return title.Equals(a.WindowTitle, StringComparison.OrdinalIgnoreCase);
                                if (mode.Equals("regex", StringComparison.OrdinalIgnoreCase) && a.TitleRegex != null)
                                    return a.TitleRegex.IsMatch(title);
                                if (mode.Equals("startswith", StringComparison.OrdinalIgnoreCase))
                                    return title.StartsWith(a.WindowTitle, StringComparison.OrdinalIgnoreCase);
                                if (mode.Equals("endswith", StringComparison.OrdinalIgnoreCase))
                                    return title.EndsWith(a.WindowTitle, StringComparison.OrdinalIgnoreCase);

                                // default: contains
                                return title.IndexOf(a.WindowTitle, StringComparison.OrdinalIgnoreCase) >= 0;
                            });

                            if (titleMatch != null) return (procName, pid, foregroundHwnd, title);

                            var defaultMatch = allMatches.FirstOrDefault(a => string.IsNullOrEmpty(a.WindowTitle));
                            if (defaultMatch != null) return (procName, pid, foregroundHwnd, title);
                        }
                    }
                    catch
                    {
                    }
                }
            }
            return (null, 0, IntPtr.Zero, null);
        }

        /// <summary>Exact + advanced config matches for a process name, cached
        /// with a TTL: the 3s liveness poll re-runs this for the tracked
        /// foreground app, and the 4 scans over the app database are pure waste
        /// when neither the config nor the process changed.</summary>
        private static List<AppConfig> GetMatchesFor(string procName)
        {
            lock (_cacheLock)
            {
                var now = DateTime.UtcNow;
                if (now - _matchCacheStamp > MatchCacheTtl)
                {
                    _matchCache.Clear();
                    _matchCacheStamp = now;
                }
                if (_matchCache.TryGetValue(procName, out var cached)) return cached;

                var exactMatches = AppConfigManager.Apps.Where(a =>
                    AppConfigManager.ExactProcessNames.Contains(procName) &&
                    a.Process != null &&
                    a.Process.Equals(procName, StringComparison.OrdinalIgnoreCase)).ToList();

                var advancedMatches = AppConfigManager.AdvancedProcessApps.Where(a =>
                {
                    if (string.IsNullOrEmpty(a.ProcessMatchMode) || string.IsNullOrEmpty(a.Process)) return false;

                    string mode = a.ProcessMatchMode;
                    if (mode.Equals("regex", StringComparison.OrdinalIgnoreCase) && a.ProcessRegex != null)
                        return a.ProcessRegex.IsMatch(procName);
                    if (mode.Equals("contains", StringComparison.OrdinalIgnoreCase))
                        return procName.IndexOf(a.Process, StringComparison.OrdinalIgnoreCase) >= 0;
                    if (mode.Equals("startswith", StringComparison.OrdinalIgnoreCase))
                        return procName.StartsWith(a.Process, StringComparison.OrdinalIgnoreCase);
                    if (mode.Equals("endswith", StringComparison.OrdinalIgnoreCase))
                        return procName.EndsWith(a.Process, StringComparison.OrdinalIgnoreCase);

                    return false;
                }).ToList();

                var all = exactMatches.Concat(advancedMatches).ToList();
                _matchCache[procName] = all;
                return all;
            }
        }
        private static string GetWindowTitle(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return "";
            int len = GetWindowTextLengthW(hWnd);
            if (len <= 0) return "";
            var sb = new StringBuilder(len + 1);
            return GetWindowTextW(hWnd, sb, sb.Capacity) > 0 ? sb.ToString() : "";
        }
        #region ----- Win32 -----
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindow(IntPtr hWnd);
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
