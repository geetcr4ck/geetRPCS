/**
 * geetRPCS - Window Activation helper
 * Win32 foreground-forcing shared by the ModernWpf windows opened from the
 * tray menu. The OS foreground lock can keep a modeless WPF window from
 * receiving real keyboard input even though logical focus looks correct
 * (IsKeyboardFocused), so these windows force the Win32 foreground.
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
using System.Windows;
using System.Windows.Interop;

namespace geetRPCS.UI.Modern
{
    internal static class WindowActivation
    {
        /// <summary>
        /// Forces a window to the Win32 foreground: ShowWindow(SW_RESTORE), then
        /// (for non-topmost windows) the classic SetWindowPos(TOPMOST ->
        /// NOTOPMOST) flip to bypass the OS foreground lock, then
        /// BringWindowToTop + SetForegroundWindow + WPF Activate. Topmost
        /// windows skip the flip - it would clear the always-on-top style.
        /// </summary>
        public static void ForceForeground(Window window)
        {
            IntPtr hWnd = new WindowInteropHelper(window).Handle;
            try
            {
                Utils.PInvoke.User32.ShowWindow(hWnd, Utils.PInvoke.User32.SW_RESTORE);
                if (!window.Topmost)
                {
                    Utils.PInvoke.User32.SetWindowPos(hWnd, (IntPtr)Utils.PInvoke.User32.HWND_TOPMOST,
                        0, 0, 0, 0,
                        Utils.PInvoke.User32.SWP_NOMOVE | Utils.PInvoke.User32.SWP_NOSIZE | Utils.PInvoke.User32.SWP_SHOWWINDOW);
                    Utils.PInvoke.User32.SetWindowPos(hWnd, (IntPtr)Utils.PInvoke.User32.HWND_NOTOPMOST,
                        0, 0, 0, 0,
                        Utils.PInvoke.User32.SWP_NOMOVE | Utils.PInvoke.User32.SWP_NOSIZE);
                }
                Utils.PInvoke.User32.BringWindowToTop(hWnd);
                Utils.PInvoke.User32.SetForegroundWindow(hWnd);
            }
            catch { }
            window.Activate();
        }
    }
}
