/**
 * geetRPCS - Update Dialogs (facade)
 * The four update dialogs now live in UI/Modern/UpdateDialog (WPF ModernWpf).
 * This class keeps the original method signatures so existing call sites
 * (Program.cs, UpdateChecker.cs) are unchanged.
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

using static geetRPCS.Services.UpdateChecker;

namespace geetRPCS.UI
{
    internal static class UpdateDialogs
    {
        public static void ShowEnhancedUpdateDialog(GitHubRelease release)
            => Modern.UpdateDialog.ShowEnhanced(release);

        internal static bool ShowAppsUpdateDialog(string remoteVersion)
            => Modern.UpdateDialog.ShowApps(remoteVersion);

        internal static bool ShowWittyUpdateDialog(string remoteVersion)
            => Modern.UpdateDialog.ShowWitty(remoteVersion);

        internal static void ShowUpToDateDialog()
            => Modern.UpdateDialog.ShowUpToDate();
    }
}
