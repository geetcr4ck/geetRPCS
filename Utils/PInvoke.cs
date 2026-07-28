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
            [System.Runtime.InteropServices.DllImport("user32.dll")]
            internal static extern bool ShowWindow(System.IntPtr hWnd, int nCmdShow);
            [System.Runtime.InteropServices.DllImport("kernel32.dll")]
            internal static extern System.IntPtr GetConsoleWindow();
        }
    }
}
