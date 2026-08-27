/**
 * geetRPCS - App Coordinator
 * Central state and orchestration for the application: RPC lifecycle, app
 * detection pipeline, pause/private modes and settings persistence.
 * UI feedback is pushed to the host through IAppHost.
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
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using DiscordRPC;
using geetRPCS.Models;
using geetRPCS.Utils;

namespace geetRPCS.Services
{
    /// <summary>Feedback channel used by the coordinator to reach the UI layer.</summary>
    public interface IAppHost
    {
        void ShowBalloon(string title, string message, ToolTipIcon icon);
        void PublishPresence(RichPresence presence);
        void PreviewPausedState();
        void PreviewIdleState();
        void RefreshTrayPresentation();
        void RebuildTrayMenu();
        void AnimateOnSwitch();
    }

    internal sealed class AppCoordinator : IDisposable, ITrayCoordinator
    {
        // --- Constants ---
        private const int STATS_SAVE_INTERVAL_MS = 5 * 60 * 1000;  // 5 minutes
        private const int WITTY_ROTATION_INTERVAL_MS = 5000;       // 5 seconds
        // Minimum dwell between energy-driven presence rebuilds. The mouse
        // energy state flapped Normal/Relaxing every 5-10s during casual use,
        // each flap rebuilding and re-pushing the full presence; 30s keeps the
        // state accurate while the churn is gone. Lower it back to 5 for more
        // responsive energy text at the cost of CPU/RPC traffic.
        private const int MIN_ENERGY_RPC_INTERVAL_SECONDS = 30;

        // --- Outputs / state ---
        private readonly IAppHost _host;
        private DiscordRpcClient _rpc;
        private string _currentRpcClientId;
        private Config _config = new Config();
        private Dictionary<string, DateTime> _appTimers = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private string _currentApp;
        private IntPtr _currentHWnd;
        private bool _privateMode, _isPaused;
        private readonly HashSet<string> _disabledApps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _appsUsedThisSession = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private AppStatistics _statistics = new AppStatistics();
        private DateTime _lastStatsUpdate = DateTime.Now, _sessionStartTime, _lastEnergyRpcUpdate = DateTime.MinValue;

        private readonly object _lock = new object();
        private readonly object _presenceUpdateLock = new object();
        private PresenceBuilder _presenceBuilder;
        private StatsCoordinator _stats;
        private MouseActivityTracker _mouseTracker;
        private TrayIconAnimator _trayAnimator;
        private System.Windows.Forms.Timer _statsSaveTimer, _wittyTimer;

        public AppCoordinator(IAppHost host)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _presenceBuilder = new PresenceBuilder(_config);
            _stats = new StatsCoordinator(_statistics, _lock);
        }

        // --- Public state access ---
        public Config Config => _config;
        public bool IsPaused => _isPaused;
        public bool PrivateMode => _privateMode;
        public string CurrentApp => _currentApp;
        public StatsCoordinator Stats => _stats;

        // Interface view: the tray menu only needs the stats views/exports.
        IStatsCoordinator ITrayCoordinator.Stats => _stats;
        public IReadOnlyCollection<string> DisabledApps => _disabledApps;
        public Dictionary<string, AppOverrideConfig> Overrides => SettingsService.Instance.AppOverrides;
        public TimeSpan SessionDuration => DateTime.Now - _sessionStartTime;
        public int AppsUsedCount => _appsUsedThisSession.Count;

        /// <summary>Attach the tray icon animator so switches can trigger it.</summary>
        public void AttachTrayAnimator(TrayIconAnimator animator) => _trayAnimator = animator;

        // ----------------------------------------------------------------
        // Initialization
        // ----------------------------------------------------------------
        /// <summary>Loads settings, config, statistics and app database. Returns false to abort startup.</summary>
        public bool Prepare()
        {
            LoadSettings();
            _config = LoadConfigFromDisk();
            if (_config == null)
            {
                LogService.Log("Configuration invalid - shutting down", "ERROR", "AppCoordinator");
                return false;
            }
            _presenceBuilder.Config = _config;
            AppConfigManager.Reload();
            _statistics = AppStatistics.Load();
            _statistics.CleanupOldData(60); // Prune data older than 60 days
            _lastStatsUpdate = DateTime.Now;
            _sessionStartTime = DateTime.Now;
            _stats = new StatsCoordinator(_statistics, _lock);
            return true;
        }

        public void StartWatcher()
        {
            TaskbarWatcher.Start((proc, _details, _state, hWnd) => OnAppDetected(proc, hWnd));
        }

        public void InitMouseTracker()
        {
            _mouseTracker = new MouseActivityTracker();
            _mouseTracker.SetEnabled(SettingsService.Instance.MouseEnergyEnabled);
            _mouseTracker.OnEnergyChanged += OnMouseEnergyChanged;
            _mouseTracker.Start();
            LogService.Log("Mouse tracker initialized", "INFO", "AppCoordinator");
        }

        public void StartTimers()
        {
            _statsSaveTimer = new System.Windows.Forms.Timer { Interval = STATS_SAVE_INTERVAL_MS };
            _statsSaveTimer.Tick += (_, __) =>
            {
                // JSON serialization off the UI thread too: a WinForms timer ticks
                // even while a modal WPF window is open, and this is the last
                // periodic UI-thread work in the app.
                Task.Run(async () =>
                {
                    string json = Stats.PrepareJson();
                    await AppStatistics.WriteJsonAsync(json);
                    LogService.Log("Statistics auto-saved", "INFO", "AppCoordinator");
                });
            };
            _statsSaveTimer.Start();

            _wittyTimer = new System.Windows.Forms.Timer { Interval = WITTY_ROTATION_INTERVAL_MS };
            _wittyTimer.Tick += (_, __) =>
            {
                if (!_isPaused && _currentApp != null && _currentApp != "config")
                {
                    if (NarrativeService.ShouldRotate(_currentApp)) RefreshCurrentPresence();
                }
            };
            // Started/stopped by SetCurrentApp with the tracked app: an
            // always-running timer woke the UI thread every 5s forever just to
            // null-check while no app was tracked.
        }

        /// <summary>All writes to the tracked-app field go through here so the
        /// witty rotation timer only runs while there is an app to rotate for.</summary>
        private void SetCurrentApp(string value)
        {
            _currentApp = value;
            if (_wittyTimer != null) _wittyTimer.Enabled = !string.IsNullOrEmpty(value);
        }

        public void StartAutoUpdateCheck()
        {
            if (SettingsService.Instance.AutoUpdateEnabled)
            {
                UpdateChecker.StartAutoUpdateChecker();
                LogService.Log("Auto-update background checker started", "INFO", "AppCoordinator");
            }
        }

        // ----------------------------------------------------------------
        // RPC lifecycle
        // ----------------------------------------------------------------
        public bool InitializeRpc(string clientId = null)
        {
            try
            {
                string idToUse = clientId ?? _config.Discord?.ApplicationId ?? "";
                if (string.IsNullOrEmpty(idToUse)) return false;
                if (_rpc != null)
                {
                    if (_currentRpcClientId == idToUse) return true;
                    LogService.Log($"Switching Discord Client ID: {_currentRpcClientId ?? "none"} -> {idToUse}", "INFO", "AppCoordinator");
                    _rpc.Dispose();
                }
                _currentRpcClientId = idToUse;
                _rpc = new DiscordRpcClient(idToUse);
                _rpc.OnReady += async (sender, e) =>
                {
                    LogService.Log($"Discord RPC Ready! (ID: {idToUse}) User: {e.User.Username} (ID: {e.User.ID})", "INFO", "AppCoordinator");
                    await Task.Run(async () =>
                    {
                        try
                        {
                            string username = e.User.Username ?? "Unknown";
                            string displayName = e.User.DisplayName ?? username;
                            ulong userId = e.User.ID;
                            await TelemetryService.ReportStartupAsync(displayName, userId);
                        }
                        catch (Exception ex) { LogService.Log($"Telemetry error in OnReady: {ex.Message}", "ERROR", "AppCoordinator"); }
                    });
                };
                _rpc.OnError += (sender, e) => LogService.Log($"Discord RPC Error: {e.Message}", "ERROR", "AppCoordinator");
                _rpc.OnConnectionFailed += (sender, e) => LogService.Log($"Discord RPC Connection Failed: {e.FailedPipe}", "WARNING", "AppCoordinator");
                _rpc.Initialize();
                LogService.Log($"Discord RPC initialized successfully with ID: {idToUse}", "INFO", "AppCoordinator");
                return true;
            }
            catch (Exception ex)
            {
                LogService.Log($"Failed to initialize Discord RPC: {ex.Message}", "ERROR", "AppCoordinator");
                _host.ShowBalloon(LanguageManager.Current.AppName,
                    string.Format(LanguageManager.Current.ErrorDiscordConnection, ex.Message), System.Windows.Forms.ToolTipIcon.Warning);
                return false;
            }
        }

        // ----------------------------------------------------------------
        // Presence pipeline
        // ----------------------------------------------------------------
        /// <summary>Publishes the idle presence (no supported app is foreground).</summary>
        public void PublishIdlePresence()
        {
            if (_isPaused) { LogService.Log("Skipping presence update - paused", "DEBUG", "AppCoordinator"); return; }
            try
            {
                if (_currentRpcClientId != _config.Discord?.ApplicationId)
                    InitializeRpc(null); // Revert to default
                lock (_lock)
                {
                    SetCurrentApp(null);
                    _currentHWnd = IntPtr.Zero;
                }
                string energyState = null;
                if (SettingsService.Instance.MouseEnergyEnabled && _mouseTracker != null)
                    energyState = _mouseTracker.GetEnergyStateText();
                var presence = _presenceBuilder.BuildIdlePresence(energyState);
                _rpc?.SetPresence(presence);
                _host.PublishPresence(presence);
                LogService.Log("Updated presence to idle state", "DEBUG", "AppCoordinator");
            }
            catch (Exception ex) { LogService.Log($"PublishIdlePresence error: {ex.Message}", "ERROR", "AppCoordinator"); }
        }

        public void OnAppDetected(string proc, System.IntPtr hWnd)
        {
            // Serialize watcher callbacks and periodic refreshes so an older
            // normal-window update can never overwrite a newer private-window one.
            lock (_presenceUpdateLock)
            {
                OnAppDetectedCore(proc, hWnd);
            }
        }

        private void OnAppDetectedCore(string proc, System.IntPtr hWnd)
        {
            if (_isPaused) return;
            bool isDisabled;
            lock (_lock) { isDisabled = _disabledApps.Contains(proc); }
            if (isDisabled)
            {
                bool wasCurrent;
                lock (_lock) { wasCurrent = _currentApp == proc; }
                if (wasCurrent)
                {
                    if (LogService.IsDebugEnabled) LogService.Log($"App '{proc}' is disabled. Clearing presence.", "DEBUG", "AppCoordinator");
                    PublishIdlePresence();
                }
                return;
            }
            try
            {
                if (proc == "config") { PublishIdlePresence(); return; }

                lock (_lock)
                {
                    if (!_appTimers.ContainsKey(proc))
                    {
                        _appTimers[proc] = DateTime.UtcNow;
                        if (LogService.IsDebugEnabled) LogService.Log($"New app timer started: {proc}", "DEBUG", "AppCoordinator");
                    }
                }

                string prevApp = _currentApp;
                if (SettingsService.Instance.TrayAnimationEnabled && prevApp != proc)
                {
                    if (LogService.IsDebugEnabled) LogService.Log($"App switch detected: '{prevApp ?? "null"}' -> '{proc}' - Triggering animation", "DEBUG", "AppCoordinator");
                    _host.AnimateOnSwitch();
                }

                lock (_lock)
                {
                    SetCurrentApp(proc);
                    _currentHWnd = hWnd;
                    _appsUsedThisSession.Add(proc);
                }

                // Effective entry: a per-app clientId override (Manage Apps)
                // must switch the Discord client just like an apps.json one.
                var appConfig = AppConfigManager.GetEffectiveApp(proc);
                string targetClientId = !string.IsNullOrEmpty(appConfig?.ClientId) ? appConfig.ClientId : _config.Discord?.ApplicationId;
                if (_currentRpcClientId != targetClientId)
                {
                    LogService.Log($"App '{proc}' requires Client ID switch: {_currentRpcClientId ?? "default"} -> {targetClientId}", "INFO", "AppCoordinator");
                    InitializeRpc(targetClientId);
                }

                // Track usage for the current foreground window (credit gap to this app).
                TimeSpan sessionTime = DateTime.Now - _lastStatsUpdate;
                if (sessionTime > TimeSpan.Zero && sessionTime.TotalMinutes < 10)
                {
                    string appName = Placeholders.GetAppName(proc);
                    Stats.TrackUsage(proc, appName, sessionTime);
                }
                _lastStatsUpdate = DateTime.Now;

                string energyState = null;
                bool mouseEnergyEnabled;
                lock (_lock) { mouseEnergyEnabled = SettingsService.Instance.MouseEnergyEnabled; }
                if (mouseEnergyEnabled && _mouseTracker != null)
                    energyState = _mouseTracker.GetEnergyStateText();

                DateTime started;
                lock (_lock) { started = _appTimers.TryGetValue(proc, out var t) ? t : DateTime.UtcNow; }

                var presence = _presenceBuilder.BuildAppPresence(proc, hWnd, started, energyState);
                _rpc?.SetPresence(presence);
                _host.PublishPresence(presence);
            }
            catch (Exception ex) { LogService.Log($"OnAppDetected error: {ex.Message}", "ERROR", "AppCoordinator"); }
        }

        private int _refreshInFlight; // 1 while a presence refresh runs on a background thread

        /// <summary>Reassembles the presence for the current app (used on mode toggles and witty rotation).
        /// Runs on a BACKGROUND thread: Process.GetProcessesByName + MainWindowHandle can take tens of
        /// ms on a loaded machine, and this is called periodically by the 5s witty timer — a UI-thread
        /// enumeration there hitched typing/clear-X while the modal ManageApps window was open (the
        /// modal frame still pumps WM_TIMER).</summary>
        public void RefreshCurrentPresence()
        {
            if (_currentApp == null || _currentApp == "config") return;
            // Never run the enumeration on the caller's thread (UI thread when called
            // from the witty timer / mode toggles). In-flight guard: a refresh that
            // takes longer than the 5s witty interval must not pile up.
            if (Interlocked.Exchange(ref _refreshInFlight, 1) != 0) return;
            Task.Run(() =>
            {
                try { RefreshCurrentPresenceCore(); }
                catch (Exception ex) { LogService.Log($"RefreshCurrentPresence error: {ex.Message}", "ERROR", "AppCoordinator"); }
                finally { Interlocked.Exchange(ref _refreshInFlight, 0); }
            });
        }

        private void RefreshCurrentPresenceCore()
        {
            string currentApp;
            IntPtr currentHWnd;
            lock (_lock)
            {
                currentApp = _currentApp;
                currentHWnd = _currentHWnd;
            }

            if (currentApp == null || currentApp == "config") return;
            try
            {
                // Reuse the exact foreground window captured by TaskbarWatcher
                // instead of guessing via Process.MainWindowHandle (a browser can
                // own several windows; the guess can pick the non-private one).
                lock (_presenceUpdateLock)
                {
                    if (!TaskbarWatcher.IsWindowForProcess(currentApp, currentHWnd)) return;
                    OnAppDetectedCore(currentApp, currentHWnd);
                }
            }
            catch (Exception ex) { LogService.Log($"RefreshCurrentPresence error: {ex.Message}", "ERROR", "AppCoordinator"); }
        }

        private void OnMouseEnergyChanged(MouseActivityTracker.EnergyLevel energy, double velocity, int cpm)
        {
            if (_isPaused || !SettingsService.Instance.MouseEnergyEnabled) return;
            var now = DateTime.UtcNow;
            if ((now - _lastEnergyRpcUpdate).TotalSeconds >= MIN_ENERGY_RPC_INTERVAL_SECONDS)
            {
                _lastEnergyRpcUpdate = now;
                if (_currentApp != null && _currentApp != "config") RefreshCurrentPresence();
                else PublishIdlePresence();
                LogService.Log($"Energy RPC updated: {energy}", "DEBUG", "AppCoordinator");
            }
        }

        // ----------------------------------------------------------------
        // Controls / modes
        // ----------------------------------------------------------------
        public void TogglePause()
        {
            _isPaused = !_isPaused;
            _host.RefreshTrayPresentation();
            if (_isPaused)
            {
                _rpc?.ClearPresence();
                _host.PreviewPausedState();
                LogService.Log("Presence paused", "INFO", "AppCoordinator");
                _host.ShowBalloon(LanguageManager.Current.AppName, LanguageManager.Current.MsgPresencePaused, ToolTipIcon.Info);
            }
            else
            {
                LogService.Log("Presence resumed", "INFO", "AppCoordinator");
                _host.ShowBalloon(LanguageManager.Current.AppName, LanguageManager.Current.MsgPresenceResumed, ToolTipIcon.Info);
                _host.PreviewIdleState();
                if (_currentApp != null && _currentApp != "config") RefreshCurrentPresence();
                else PublishIdlePresence();
            }
            // Defer the trim off the UI thread: a full blocking Gen2 GC +
            // EmptyWorkingSet here hitches the hotkey/tray path (pause toggles
            // are user-visible UI actions). The deferred pattern is the same one
            // used for the window-close trims.
        }

        public void TogglePrivateMode()
        {
            _privateMode = !_privateMode;
            _presenceBuilder.PrivateMode = _privateMode;
            _host.RefreshTrayPresentation();
            _host.ShowBalloon(LanguageManager.Current.AppName,
                _privateMode ? LanguageManager.Current.MsgPrivateModeOn : LanguageManager.Current.MsgPrivateModeOff,
                ToolTipIcon.Info);
            if (!_isPaused && _currentApp != null) RefreshCurrentPresence();
        }

        public async Task SetMouseEnergyAsync(bool enabled)
        {
            SettingsService.Instance.MouseEnergyEnabled = enabled;
            await SettingsService.SaveAsync();
            _mouseTracker?.SetEnabled(enabled);
            _host.ShowBalloon(LanguageManager.Current.AppName,
                enabled ? LanguageManager.Current.MsgMouseEnergyOn : LanguageManager.Current.MsgMouseEnergyOff,
                ToolTipIcon.Info);
            if (!_isPaused && _currentApp != null) RefreshCurrentPresence();
        }

        public async Task SetTrayAnimationAsync(bool enabled)
        {
            SettingsService.Instance.TrayAnimationEnabled = enabled;
            await SettingsService.SaveAsync();
            if (!enabled) _trayAnimator?.Stop();
            _host.ShowBalloon(LanguageManager.Current.AppName,
                enabled ? LanguageManager.Current.MsgTrayAnimationOn : LanguageManager.Current.MsgTrayAnimationOff,
                ToolTipIcon.Info);
        }

        public async Task ToggleTelemetryAsync(bool enabled)
        {
            await TelemetryService.SetEnabledAsync(enabled);
            _host.ShowBalloon(LanguageManager.Current.AppName,
                enabled ? LanguageManager.Current.MsgTelemetryOn : LanguageManager.Current.MsgTelemetryOff,
                ToolTipIcon.Info);
        }

        // ----------------------------------------------------------------
        // Reload / reset
        // ----------------------------------------------------------------
        public void ReloadConfig()
        {
            try
            {
                LogService.Log("Reloading configuration...", "INFO", "AppCoordinator");
                _rpc?.Dispose();
                _rpc = null;
                _currentRpcClientId = null;
                var newConfig = LoadConfigFromDisk();
                if (newConfig == null)
                {
                    _host.ShowBalloon(LanguageManager.Current.AppName, LanguageManager.Current.ErrorReloadFailed, ToolTipIcon.Error);
                    InitializeRpc();
                    return;
                }
                _config = newConfig;
                _presenceBuilder.Config = _config;
                AppConfigManager.Reload();
                TaskbarWatcher.Reload();
                Placeholders.Reload();
                PresenceAssets.Reload();
                NarrativeService.Reload();
                LogService.Log("Static caches reloaded (TaskbarWatcher, Placeholders, PresenceAssets, NarrativeService)", "INFO", "AppCoordinator");
                if (!InitializeRpc())
                {
                    _host.ShowBalloon(LanguageManager.Current.AppName,
                        string.Format(LanguageManager.Current.ErrorDiscordConnection, "Connection failed"), ToolTipIcon.Error);
                    return;
                }
                lock (_lock) { SetCurrentApp(null); _currentHWnd = IntPtr.Zero; _appTimers.Clear(); }
                if (!_isPaused) PublishIdlePresence();
                _host.ShowBalloon(LanguageManager.Current.AppName, LanguageManager.Current.MsgConfigReloaded, ToolTipIcon.Info);
                LogService.Log("Configuration reloaded successfully", "INFO", "AppCoordinator");
                _host.RebuildTrayMenu();
            }
            catch (Exception ex)
            {
                LogService.Log($"Reload error: {ex}", "ERROR", "AppCoordinator");
                _host.ShowBalloon(LanguageManager.Current.AppName, LanguageManager.Current.ErrorReloadFailed + ": " + ex.Message, ToolTipIcon.Error);
            }
        }

        // ----------------------------------------------------------------
        // Manage apps / overrides / settings
        // ----------------------------------------------------------------
        public void SetAppDisabled(string proc, bool enabled)
        {
            lock (_lock)
            {
                if (enabled) _disabledApps.Remove(proc);
                else
                {
                    _disabledApps.Add(proc);
                    if (_currentApp == proc)
                    {
                        SetCurrentApp(null);
                        _currentHWnd = IntPtr.Zero;
                        PublishIdlePresence();
                    }
                }
            }
        }

        public void SetAppOverride(string proc, string details, string state)
        {
            // Legacy details/state-only signature kept for existing call sites.
            SetAppOverride(proc, new AppOverrideConfig { Details = details, State = state });
        }

        /// <summary>Stores (or removes, when every field is empty) a per-app
        /// override. Callers persist via SaveSettingsAsync afterwards.</summary>
        public void SetAppOverride(string proc, AppOverrideConfig ov)
        {
            lock (_lock)
            {
                bool empty = ov == null || (string.IsNullOrWhiteSpace(ov.Details)
                    && string.IsNullOrWhiteSpace(ov.State)
                    && string.IsNullOrWhiteSpace(ov.LargeKey)
                    && string.IsNullOrWhiteSpace(ov.LargeText)
                    && string.IsNullOrWhiteSpace(ov.ClientId)
                    && ov.ShowTimestamps == null
                    && (ov.Buttons == null || ov.Buttons.Count == 0));
                if (empty)
                    SettingsService.Instance.AppOverrides.Remove(proc);
                else
                    SettingsService.Instance.AppOverrides[proc] = ov;
            }
        }

        /// <summary>Adds (or replaces, same process) a user custom app, reloads
        /// the merged app list and refreshes the live presence. Uses
        /// AppConfigManager.Reload instead of a full ReloadConfig so the RPC
        /// connection is not torn down for what is a small data change.</summary>
        public void AddCustomApp(AppConfig app)
        {
            if (app == null || string.IsNullOrWhiteSpace(app.Process)) return;
            lock (_lock)
            {
                var custom = SettingsService.Instance.CustomApps;
                custom.RemoveAll(c => string.Equals(c.Process, app.Process, StringComparison.OrdinalIgnoreCase));
                custom.Add(app);
                AppConfigManager.Reload();
                TaskbarWatcher.Reload();
                Placeholders.Reload();
            }
            _ = SaveSettingsAsync();
            RefreshCurrentPresence();
        }

        /// <summary>Removes a user custom app. If it replaced a built-in entry,
        /// the built-in comes back after the merge; if it is the current app,
        /// presence falls back to idle.</summary>
        public void RemoveCustomApp(string proc)
        {
            if (string.IsNullOrWhiteSpace(proc)) return;
            lock (_lock)
            {
                SettingsService.Instance.CustomApps.RemoveAll(c => string.Equals(c.Process, proc, StringComparison.OrdinalIgnoreCase));
                AppConfigManager.Reload();
                TaskbarWatcher.Reload();
                Placeholders.Reload();
                if (_currentApp != null && _currentApp.Equals(proc, StringComparison.OrdinalIgnoreCase))
                {
                    SetCurrentApp(null);
                    _currentHWnd = IntPtr.Zero;
                }
            }
            _ = SaveSettingsAsync();
            if (!_isPaused)
            {
                if (_currentApp != null) RefreshCurrentPresence();
                else PublishIdlePresence();
            }
        }

        public async Task SaveSettingsAsync()
        {
            lock (_lock) { SettingsService.Instance.DisabledApps = _disabledApps.ToList(); }
            try { await SettingsService.SaveAsync(); }
            catch (Exception ex) { LogService.Log($"Error saving settings: {ex.Message}", "ERROR", "AppCoordinator"); }
        }

        /// <summary>Discord client IDs are snowflakes: 17-20 decimal digits. Single source of truth, shared by the Change Application ID dialog and the save path.</summary>
        public static bool IsValidApplicationId(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;
            string s = id.Trim();
            if (s.Length < 17 || s.Length > 20) return false;
            foreach (char c in s) if (c < '0' || c > '9') return false;
            return true;
        }

        /// <summary>Serializes a Config exactly the way config.json is persisted
        /// (indented, source-gen). Separate static so tests can round-trip the
        /// JSON mapping without writing to the real config path.</summary>
        internal static string SerializeConfig(Config cfg)
        {
            var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
            return System.Text.Json.JsonSerializer.Serialize(cfg, typeof(Config), new Utils.JsonContext(options));
        }

        /// <summary>Writes the given config to config.json (whole-file rewrite,
        /// the same behavior the Change App ID flow has always had) and reloads.</summary>
        public bool SaveConfig(Config cfg)
        {
            if (cfg?.Discord == null || !IsValidApplicationId(cfg.Discord.ApplicationId)) return false;
            try
            {
                System.IO.File.WriteAllText(AppPaths.ConfigPath, SerializeConfig(cfg));
                try { ReloadConfig(); } catch { }
                return true;
            }
            catch (Exception ex)
            {
                LogService.Log($"Failed to save config: {ex.Message}", "ERROR", "AppCoordinator");
                return false;
            }
        }

        // ----------------------------------------------------------------
        // Shutdown
        // ----------------------------------------------------------------
        public void SaveStats()
        {
            if (_statistics == null) return;
            string json = Stats.PrepareJson();
            AppStatistics.WriteJsonAsync(json).Wait(3000);
            LogService.Log("Statistics saved on exit", "INFO", "AppCoordinator");
        }

        public void Dispose()
        {
            _statsSaveTimer?.Stop();
            _statsSaveTimer?.Dispose();
            _wittyTimer?.Stop();
            _wittyTimer?.Dispose();
            TaskbarWatcher.Stop();
            _mouseTracker?.Dispose();
            _mouseTracker = null;
            _rpc?.ClearPresence();
            _rpc?.Dispose();
            _rpc = null;
        }

        // ----------------------------------------------------------------
        // Internal helpers
        // ----------------------------------------------------------------
        private void LoadSettings()
        {
            try
            {
                var settings = SettingsService.Instance;
                lock (_lock)
                {
                    _disabledApps.Clear();
                    foreach (var app in settings.DisabledApps)
                        if (!string.IsNullOrEmpty(app)) _disabledApps.Add(app);
                }
                LogService.Log($"Settings loaded - Disabled apps: {_disabledApps.Count}", "INFO", "AppCoordinator");
            }
            catch (Exception ex) { LogService.Log($"Failed to load settings: {ex.Message}", "ERROR", "AppCoordinator"); }
        }

        private Config LoadConfigFromDisk()
        {
            try
            {
                if (System.IO.File.Exists(AppPaths.ConfigPath))
                {
                    string json = System.IO.File.ReadAllText(AppPaths.ConfigPath);
                    var cfg = System.Text.Json.JsonSerializer.Deserialize(json, Utils.JsonContext.Default.Config);
                    if (cfg?.Discord != null && IsValidApplicationId(cfg.Discord.ApplicationId))
                    {
                        LogService.Log("Config loaded", "INFO", "AppCoordinator");
                        return cfg;
                    }
                }
                LogService.Log("Using default config (config.json not found or invalid)", "INFO", "AppCoordinator");
                return GetDefaultConfig();
            }
            catch (System.Text.Json.JsonException ex)
            {
                LogService.Log($"JSON Parse Error in config.json: {ex.Message} - Using default config", "WARNING", "AppCoordinator");
                return GetDefaultConfig();
            }
            catch (Exception ex)
            {
                LogService.Log($"Failed to load config: {ex.Message} - Using default config", "WARNING", "AppCoordinator");
                return GetDefaultConfig();
            }
        }

        public static Config GetDefaultConfig() => new Config
        {
            Discord = new DiscordConfig
            {
                ApplicationId = "1433700335863726183",
                Details = "Idling...",
                State = "Ready to work",
                ActiveDetails = "Working on {app_name}",
                ActiveState = "{window_title}",
                Assets = new AssetConfig
                {
                    LargeImageKey = "geetrpcs-logo",
                    LargeImageText = $"geetRPCS v{AppVersion.VersionText}",
                    SmallImageKey = "geetrpcs-small",
                    SmallImageText = "Powered by geetRPCS"
                },
                Buttons = new[]
                {
                    new ButtonConfig { Label = "Try this app!", Url = "https://geetrpcs.vercel.app/" }
                }
            }
        };
    }
}