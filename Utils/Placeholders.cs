/**
 * geetRPCS - Text Placeholder
 * Handles string placeholder expansion for RPC text
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
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using geetRPCS.Services;

namespace geetRPCS.Utils
{
    internal static class Placeholders
    {
        public static void Reload() => AppConfigManager.Reload();
        public static string GetAppName(string processName)
        {
            var app = AppConfigManager.Apps.FirstOrDefault(a => a.Process.Equals(processName, StringComparison.OrdinalIgnoreCase));
            return app?.AppName ?? processName;
        }
        public static string Replace(string format, string processName, IntPtr hWnd)
        {
            if (string.IsNullOrEmpty(format)) return format;
            string appName = GetAppName(processName);
            string title = GetWindowTitle(hWnd);
            if (string.IsNullOrEmpty(title) || title.Length <= 3) title = "Working...";
            return format.Replace("{process_name}", processName)
                .Replace("{app_name}", appName)
                .Replace("{window_title}", title);
        }
        public static string GetWindowTitle(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return "";
            int len = GetWindowTextLengthW(hWnd);
            if (len <= 0) return "";
            var sb = new StringBuilder(len + 1);
            GetWindowTextW(hWnd, sb, sb.Capacity);
            string title = sb.ToString();
            if (string.IsNullOrWhiteSpace(title) || title.Length <= 3) return "";
            return title;
        }
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowTextLengthW(IntPtr hWnd);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowTextW(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    }
}
