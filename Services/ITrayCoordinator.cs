/**
 * geetRPCS - Tray coordinator contracts
 * Narrow interfaces over AppCoordinator / StatsCoordinator so the tray menu can
 * be built and exercised end-to-end (Rebuild + clicks) in tests with lightweight
 * fakes, without spinning up the full application (Program + AppCoordinator +
 * Discord RPC + taskbar watchers).
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

using System.Threading.Tasks;
using geetRPCS.Models;

namespace geetRPCS.Services
{
    /// <summary>Statistics views/exports used by the tray menu (subset of StatsCoordinator).</summary>
    internal interface IStatsCoordinator
    {
        void ShowToday();
        void ShowWeek();
        void ShowMonth();
        void ShowAllTime();
        void ExportAsync(string format);
        Task ResetAsync();
    }

    /// <summary>Coordinator surface used by TrayMenuController (subset of AppCoordinator).</summary>
    internal interface ITrayCoordinator
    {
        bool IsPaused { get; }
        bool PrivateMode { get; }
        Config Config { get; }
        IStatsCoordinator Stats { get; }

        void TogglePause();
        void TogglePrivateMode();
        Task SetMouseEnergyAsync(bool enabled);
        Task SetTrayAnimationAsync(bool enabled);
        Task ToggleTelemetryAsync(bool enabled);
        bool SaveConfig(Config cfg);
        void ReloadConfig();
    }
}
