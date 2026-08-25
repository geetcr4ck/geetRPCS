/**
 * geetRPCS - Tray shell contract
 * Narrow interface over Program so TrayMenuController can be exercised
 * end-to-end in tests with a lightweight fake shell (no ApplicationContext,
 * tray icon, hotkeys or update orchestrator needed).
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

using System.Windows.Forms;

namespace geetRPCS.UI
{
    /// <summary>Shell surface used by TrayMenuController (subset of Program).</summary>
    internal interface ITrayShell
    {
        bool IsManageAppsOpen { get; }
        bool IsPreviewVisible { get; }
        bool IsStatsOpen { get; }

        void ToggleManageAppsVisibility();
        void TogglePreviewVisibility();
        void RebuildTrayMenuDeferred();
        void CheckForUpdatesFromMenu();
        void OpenLog();
        void ExitApp();
        void ShowBalloonTip(string title, string text, ToolTipIcon icon);
    }
}
