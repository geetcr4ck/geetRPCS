/**
 * geetRPCS - Native Utility
 * Native Windows API definitions and imports
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

namespace geetRPCS.Utils
{
    internal static partial class PInvoke
    {
        internal static class User32
        {
            public const int SW_HIDE = 0;
            public const int SW_RESTORE = 9;
            public const int HWND_TOPMOST = -1;
            public const int HWND_NOTOPMOST = -2;
            public const uint SWP_NOSIZE = 0x0001;
            public const uint SWP_NOMOVE = 0x0002;
            public const uint SWP_SHOWWINDOW = 0x0040;
            public const int WM_CHAR = 0x0102;

            [System.Runtime.InteropServices.DllImport("user32.dll")]
            internal static extern bool ShowWindow(System.IntPtr hWnd, int nCmdShow);
            [System.Runtime.InteropServices.DllImport("user32.dll")]
            internal static extern bool SetForegroundWindow(System.IntPtr hWnd);
            [System.Runtime.InteropServices.DllImport("user32.dll")]
            internal static extern bool BringWindowToTop(System.IntPtr hWnd);
            [System.Runtime.InteropServices.DllImport("user32.dll")]
            internal static extern System.IntPtr GetForegroundWindow();
            [System.Runtime.InteropServices.DllImport("user32.dll")]
            internal static extern bool SetWindowPos(System.IntPtr hWnd, System.IntPtr hWndInsertAfter,
                int x, int y, int cx, int cy, uint uFlags);
            [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
            internal static extern int GetClassName(System.IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);
            [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
            internal static extern System.IntPtr SendMessage(System.IntPtr hWnd, int msg, System.IntPtr wParam, System.IntPtr lParam);
            [System.Runtime.InteropServices.DllImport("kernel32.dll")]
            internal static extern System.IntPtr GetConsoleWindow();

            /// <summary>Class name of the current foreground window (for diagnostics).</summary>
            internal static string GetForegroundWindowClass()
            {
                try
                {
                    var sb = new System.Text.StringBuilder(256);
                    GetClassName(GetForegroundWindow(), sb, sb.Capacity);
                    return sb.ToString();
                }
                catch { return ""; }
            }
        }
    }
}
