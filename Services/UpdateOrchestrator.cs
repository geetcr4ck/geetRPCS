/**
 * geetRPCS - Update Orchestrator
 * Runs the background update checks (application/data) and maintenance loops that
 * used to live inside Program.cs. All loops are cooperative-cancellable.
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
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace geetRPCS.Services
{
    /// <summary>
    /// Centralizes the delayed startup update check and the periodic
    /// apps/witty database refresh.
    /// </summary>
    internal sealed class UpdateOrchestrator : IDisposable
    {
        private const int APPS_UPDATE_CHECK_INTERVAL_MS = 30 * 60 * 1000; // 30 minutes
        private const int STARTUP_DELAY_MS = 3000;                        // 3 seconds

        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly Action<string, string, ToolTipIcon> _notify;
        private readonly Action<UpdateChecker.GitHubRelease> _releaseDiscovered;

        public UpdateOrchestrator(Action<string, string, ToolTipIcon> notify,
                                   Action<UpdateChecker.GitHubRelease> releaseDiscovered)
        {
            _notify = notify ?? throw new ArgumentNullException(nameof(notify));
            _releaseDiscovered = releaseDiscovered ?? throw new ArgumentNullException(nameof(releaseDiscovered));
        }

        public void Start()
        {
            _ = StartupCheckAsync(_cts.Token);
            _ = PeriodicUpdateLoopAsync(_cts.Token);
        }

        private async Task StartupCheckAsync(CancellationToken ct)
        {
            try
            {
                try { await Task.Delay(STARTUP_DELAY_MS, ct); }
                catch (OperationCanceledException) { return; }

                var release = await UpdateChecker.CheckForUpdates(showUpToDateMessage: false);
                if (release != null && !ct.IsCancellationRequested)
                    _releaseDiscovered(release);

                if (await UpdateChecker.CheckForAppsUpdate(silent: true))
                {
                    AppConfigManager.Reload();
                    _notify(LanguageManager.Current.AppName, LanguageManager.Current.MsgAppsUpdated, ToolTipIcon.Info);
                }

                if (await UpdateChecker.CheckForWittyUpdate(silent: true))
                {
                    NarrativeService.Reload();
                    _notify(LanguageManager.Current.AppName, LanguageManager.Current.MsgWittyUpdated, ToolTipIcon.Info);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                LogService.Log($"Startup update check failed: {ex.Message}", "ERROR", "UpdateOrchestrator");
            }
        }

        private async Task PeriodicUpdateLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try { await Task.Delay(APPS_UPDATE_CHECK_INTERVAL_MS, ct); }
                catch (OperationCanceledException) { break; }
                try
                {
                    if (await UpdateChecker.CheckForAppsUpdate(silent: true))
                    {
                        AppConfigManager.Reload();
                        _notify(LanguageManager.Current.AppName, LanguageManager.Current.MsgAppsUpdated, ToolTipIcon.Info);
                        LogService.Log("Periodic apps.json update applied successfully", "INFO", "UpdateOrchestrator");
                    }

                    if (await UpdateChecker.CheckForWittyUpdate(silent: true))
                    {
                        NarrativeService.Reload();
                        _notify(LanguageManager.Current.AppName, LanguageManager.Current.MsgWittyUpdated, ToolTipIcon.Info);
                        LogService.Log("Periodic witty.json update applied successfully", "INFO", "UpdateOrchestrator");
                    }
                }
                catch (Exception ex)
                {
                    LogService.Log($"Periodic update check failed: {ex.Message}", "ERROR", "UpdateOrchestrator");
                }
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
        }
    }
}