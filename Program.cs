/**
 * geetRPCS - Main Application
 * Discord Rich Presence Custom Switcher main logic
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

#nullable enable
using DiscordRPC;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using geetRPCS.Models;
using geetRPCS.Services;
using geetRPCS.Utils;
using geetRPCS.UI;

class Program : ApplicationContext
{

    // --- Fields & Configuration ---
    #region Configuration
    private NotifyIcon trayIcon = null!;
    private DiscordRpcClient? rpc;
    private string? _currentRpcClientId;
    private Config config = null!;
    private Dictionary<string, DateTime> appTimers = null!;
    private string? currentApp;
    private bool privateMode, isPaused;
    private ToolStripMenuItem? privateModeItem, pauseItem, previewMenuItem, _mouseEnergyMenuItem, _trayAnimationMenuItem;
    private AppStatistics statistics = null!;
    private PresencePreviewForm? previewForm;
    private ManageAppsForm? _manageAppsForm;
    private DateTime lastStatsUpdate, _sessionStartTime;
    private System.Windows.Forms.Timer? statsTimer;
    private readonly object _lockState = new object();
    private HashSet<string> _disabledApps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> _appsUsedThisSession = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private GlobalHotkey? _hkPause, _hkPreview, _hkReload, _hkPrivate, _hkStats;
    private MouseActivityTracker? _mouseTracker;
    private TrayIconAnimator? _trayAnimator;

    private UpdateChecker.GitHubRelease? _pendingUpdate; // Store pending update
    private readonly Control _threadMarshaller = new Control();
    private static readonly string AppFolder = AppDomain.CurrentDomain.BaseDirectory;
    private static readonly string ConfigPath = Path.Combine(AppFolder, "config.json");
    private static readonly string AppsPath = Path.Combine(AppFolder, "apps.json");
    private static readonly string IconPath = Path.Combine(AppFolder, "rpicon.ico");

    private const int STATS_SAVE_INTERVAL_MS = 5 * 60 * 1000; // 5 minutes
    private const int WITTY_ROTATION_INTERVAL_MS = 5000;      // 5 seconds
    private const int BALLOON_TIP_TIMEOUT_MS = 2000;
    private const int APPS_UPDATE_CHECK_INTERVAL_MS = 30 * 60 * 1000; // 30 minutes
    private const int MIN_ENERGY_RPC_INTERVAL_SECONDS = 5;  // Rate limit for energy-based RPC updates
    private DateTime _lastEnergyRpcUpdate = DateTime.MinValue;
    #endregion

    // --- Main Entry ---
    #region Main
    [STAThread]
    static void Main()
    {
        using (Mutex mutex = new Mutex(true, "geetRPCS-v1-SingleInstance", out bool createdNew))
        {
            if (!createdNew)
            {
                MessageBox.Show(LanguageManager.Current.ErrorAlreadyRunning, "geetRPCS",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            LogService.Initialize();
            try
            {
                Log($"Application started at {DateTime.Now}", "INFO", "Startup");
                Log($"App folder: {AppFolder}", "INFO", "Startup");
                PInvoke.User32.ShowWindow(PInvoke.User32.GetConsoleWindow(), PInvoke.User32.SW_HIDE);
                Application.Run(new Program());
            }
            catch (Exception ex)
            {
                Log($"Fatal error: {ex.Message}", "ERROR", "Fatal");
                MessageBox.Show($"Application failed to start:\n\n{ex.Message}\n\nCheck geetRPCS.log for details.",
                    "geetRPCS - Fatal Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
    public Program()
    {
        try
        {
            _threadMarshaller.CreateControl();
        if (!ValidateRequiredFiles()) { Application.Exit(); return; }
        LoadSettings();
            Task.Run(async () =>
            {
                await Task.Delay(3000);
                _threadMarshaller.Invoke(new Action(async () =>
                {
                    var release = await UpdateChecker.CheckForUpdates(showUpToDateMessage: false);
                    if (release != null)
                    {
                        _pendingUpdate = release;
                        string mode = SettingsService.Instance.UpdateNotificationMode; // "Notification", "Dialog", "Silent"
                        Log($"Update available. Mode: {mode}");

                        if (mode == "Dialog")
                        {
                             UpdateDialogs.ShowEnhancedUpdateDialog(release);
                        }
                        else if (mode == "Notification")
                        {
                             ShowBalloonTip(LanguageManager.Current.UpdateAvailableTitle,
                                 $"{LanguageManager.Current.UpdateAvailableMessage}\n\nv{release.TagName?.TrimStart('v')}", 
                                 ToolTipIcon.Info);
                        }
                        // Silent mode does nothing
                    }

                    if (await UpdateChecker.CheckForAppsUpdate(silent: true))
                    {
                        AppConfigManager.Reload();
                        ShowBalloonTip(LanguageManager.Current.AppName, LanguageManager.Current.MsgAppsUpdated, ToolTipIcon.Info);
                    }

                    if (await UpdateChecker.CheckForWittyUpdate(silent: true))
                    {
                        NarrativeService.Reload();
                        ShowBalloonTip(LanguageManager.Current.AppName, LanguageManager.Current.MsgWittyUpdated ?? "Witty texts database updated!", ToolTipIcon.Info);
                    }
                }));
            });
            config = LoadConfig();
            if (config == null) { Application.Exit(); return; }
            AppConfigManager.Reload();
            appTimers = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
            statistics = AppStatistics.Load();
            statistics.CleanupOldData(60); // Prune data older than 60 days to save RAM
            lastStatsUpdate = DateTime.Now;
            Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromMinutes(30));
                        MemoryHelper.TrimMemory();
                    }
                    catch (Exception ex)
                    {
                        Log($"Memory trim error: {ex.Message}", "ERROR", "MemoryHelper");
                    }
                }
            });
            // Periodic apps.json and witty.json update checker
            Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(APPS_UPDATE_CHECK_INTERVAL_MS);
                    try
                    {
                        if (await UpdateChecker.CheckForAppsUpdate(silent: true))
                        {
                            AppConfigManager.Reload();
                            _threadMarshaller.Invoke(new Action(() =>
                            {
                                ShowBalloonTip(LanguageManager.Current.AppName, LanguageManager.Current.MsgAppsUpdated, ToolTipIcon.Info);
                            }));
                            Log("Periodic apps.json update applied successfully", "INFO", "UpdateChecker");
                        }

                        if (await UpdateChecker.CheckForWittyUpdate(silent: true))
                        {
                            NarrativeService.Reload();
                            _threadMarshaller.Invoke(new Action(() =>
                            {
                                ShowBalloonTip(LanguageManager.Current.AppName, LanguageManager.Current.MsgWittyUpdated ?? "Witty texts database updated!", ToolTipIcon.Info);
                            }));
                            Log("Periodic witty.json update applied successfully", "INFO", "UpdateChecker");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"Periodic update check failed: {ex.Message}", "ERROR", "UpdateChecker");
                    }
                }
            });
            InitStatsTimer();
            if (!InitializeDiscordRPC() || !SetupTrayIcon()) { Application.Exit(); return; }
            UpdatePresenceFromConfig();
            TaskbarWatcher.Start(OnAppDetected);
            _sessionStartTime = DateTime.Now;
            Log("geetRPCS initialized successfully!");
            RegisterHotkeys();
            InitMouseTracker();
            MemoryHelper.TrimMemory();
            
            // Start auto-update background checker if enabled
            if (SettingsService.Instance.AutoUpdateEnabled)
            {
                UpdateChecker.StartAutoUpdateChecker();
                Log("Auto-update background checker started");
            }
        }
        catch (Exception ex)
        {
            Log($"INIT ERROR: {ex}");
            MessageBox.Show($"Failed to start geetRPCS:\n\n{ex.Message}\n\nCheck geetRPCS.log for details.",
                "geetRPCS - Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Application.Exit();
        }
    }
    private void ToggleManageAppsVisibility()
    {
        if (_manageAppsForm == null || _manageAppsForm.IsDisposed)
        {
            Log("Opening ManageAppsForm...");
            _manageAppsForm = new ManageAppsForm(
                AppConfigManager.Apps,
                _disabledApps,
                SettingsService.Instance.AppOverrides,
                async (proc, enabled) =>
                {
                    lock (_lockState)
                    {
                        if (enabled) _disabledApps.Remove(proc);
                        else
                        {
                            _disabledApps.Add(proc);
                            if (currentApp == proc) { currentApp = null; UpdatePresenceFromConfig(); }
                        }
                    }
                    await SaveSettingsAsync();
                },
                async (proc, details, state) =>
                {
                    lock (_lockState)
                    {
                        if (string.IsNullOrWhiteSpace(details) && string.IsNullOrWhiteSpace(state))
                        {
                            SettingsService.Instance.AppOverrides.Remove(proc);
                        }
                        else
                        {
                            SettingsService.Instance.AppOverrides[proc] = new AppOverrideConfig { Details = details, State = state };
                        }
                    }
                    await SaveSettingsAsync();
                });
            _manageAppsForm.Show();
        }
        else
        {
            _manageAppsForm.BringToFront();
        }
    }
    #endregion
    #region ----- Initialization Helpers -----
    private void InitStatsTimer()
    {
        statsTimer = new System.Windows.Forms.Timer { Interval = STATS_SAVE_INTERVAL_MS };
        statsTimer.Tick += async (_, __) =>
        {
            string json;
            lock (_lockState)
            {
                json = statistics.PrepareJson();
            }
            await AppStatistics.WriteJsonAsync(json);
            Log("Statistics auto-saved");
        };
        statsTimer.Start();
        var wittyTimer = new System.Windows.Forms.Timer { Interval = WITTY_ROTATION_INTERVAL_MS };
        wittyTimer.Tick += (_, __) =>
        {
            if (!isPaused && currentApp != null && currentApp != "config")
            {
                if (NarrativeService.ShouldRotate(currentApp))
                {
                    RefreshCurrentPresence();
                }
            }
        };
        wittyTimer.Start();
    }
    private void TogglePreviewVisibility()
    {
        if (previewForm == null || previewForm.IsDisposed)
        {
            Log("Creating PresencePreviewForm...");
            InitPreviewForm();
            previewForm!.Show();
            RefreshCurrentPresence();
            if (currentApp == null || currentApp == "config") UpdatePresenceFromConfig();
        }
        else
        {
            Log("Destroying PresencePreviewForm to save RAM...");
            previewForm.Close();
            previewForm = null;
            MemoryHelper.TrimMemory();
        }
    }
    private void InitPreviewForm()
    {
        previewForm = new PresencePreviewForm(config.Discord!.ApplicationId);
        previewForm.FormClosing += (sender, e) =>
        {
            if (previewMenuItem != null)
            {
                if (previewForm.InvokeRequired) previewForm.BeginInvoke(new Action(() => previewMenuItem.Checked = false));
                else previewMenuItem.Checked = false;
            }
            Task.Run(async () => { await Task.Delay(500); MemoryHelper.TrimMemory(); });
        };
        previewForm.VisibleChanged += (sender, e) =>
        {
            if (previewMenuItem != null)
            {
                if (previewForm.InvokeRequired) previewForm.BeginInvoke(new Action(() => previewMenuItem.Checked = previewForm.Visible));
                else previewMenuItem.Checked = previewForm.Visible;
            }
            if (previewForm != null && !previewForm.Visible) MemoryHelper.TrimMemory();
        };
    }
    private void InitMouseTracker()
    {
        _mouseTracker = new MouseActivityTracker();
        _mouseTracker.SetEnabled(SettingsService.Instance.MouseEnergyEnabled);
        _mouseTracker.OnEnergyChanged += OnMouseEnergyChanged;
        _mouseTracker.Start();
        Log("Mouse tracker initialized and started");
    }
    private void RegisterHotkeys()
    {
        try
        {
            _hkPause = CreateHotkey(Keys.Control | Keys.Alt, Keys.P, () => OnTogglePause(null, EventArgs.Empty), "Pause");
            _hkPreview = CreateHotkey(Keys.Control | Keys.Alt, Keys.V, () => TogglePreviewVisibility(), "Preview");
            _hkReload = CreateHotkey(Keys.Control | Keys.Alt, Keys.R, () => OnReload(null, EventArgs.Empty), "Reload");
            _hkPrivate = CreateHotkey(Keys.Control | Keys.Alt, Keys.H, () => OnTogglePrivateMode(null, EventArgs.Empty), "Private Mode");
            _hkStats = CreateHotkey(Keys.Control | Keys.Alt, Keys.S, () => ShowTodayStatistics(), "Stats Today");
        }
        catch (Exception ex) { Log($"Failed to register hotkey: {ex.Message}"); }
    }
    private GlobalHotkey CreateHotkey(Keys modifiers, Keys key, Action action, string name)
    {
        var hk = new GlobalHotkey(modifiers, key);
        hk.HotkeyPressed += () =>
        {
            System.Media.SystemSounds.Beep.Play();
            _threadMarshaller.Invoke(action);
        };
        Log($"Hotkey registered: {name}");
        return hk;
    }
    #endregion
    #region ----- Config Management -----
    private Config LoadConfig()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                string json = File.ReadAllText(ConfigPath);
                var cfg = JsonSerializer.Deserialize(json, JsonContext.Default.Config);
                if (cfg?.Discord != null && !string.IsNullOrEmpty(cfg.Discord.ApplicationId))
                {
                    Log("Config loaded from config.json");
                    return cfg;
                }
            }
            Log("Using default config (config.json not found or invalid)");
            return GetDefaultConfig();
        }
        catch (JsonException ex)
        {
            Log($"JSON Parse Error in config.json: {ex.Message} - Using default config");
            return GetDefaultConfig();
        }
        catch (Exception ex)
        {
            Log($"Failed to load config: {ex.Message} - Using default config");
            return GetDefaultConfig();
        }
    }
    private Config GetDefaultConfig() => new Config
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
                LargeImageText = $"geetRPCS v{System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)}",
                SmallImageKey = "geetrpcs-small",
                SmallImageText = "Powered by geetRPCS"
            },
            Buttons = new[]
            {
                new ButtonConfig { Label = "Try this app!", Url = "https://geetrpcs.vercel.app/" }
            }
        }
    };
    #endregion
    #region ----- Settings Management -----
    private void LoadSettings()
    {
        try
        {
            var settings = SettingsService.Instance;
            lock (_lockState)
            {
                _disabledApps.Clear();
                foreach (var app in settings.DisabledApps)
                    if (!string.IsNullOrEmpty(app)) _disabledApps.Add(app);
            }
            Log($"Settings loaded - Disabled apps: {_disabledApps.Count}", "INFO", "Settings");
        }
        catch (Exception ex) { Log($"Failed to load settings: {ex.Message}", "ERROR", "Settings"); }
    }
    private async Task SaveSettingsAsync()
    {
        try
        {
            lock (_lockState)
            {
                SettingsService.Instance.DisabledApps = _disabledApps.ToList();
            }
            await SettingsService.SaveAsync();
            Log("Settings saved successfully", "INFO", "Settings");
        }
        catch (Exception ex) { Log($"Error saving settings: {ex.Message}"); }
    }
    #endregion
    #region ----- Validation & Initialization -----
    private bool ValidateRequiredFiles()
    {
        var missingFiles = new List<string>();
        if (!File.Exists(AppsPath)) missingFiles.Add("apps.json");
        if (!File.Exists(IconPath)) missingFiles.Add("rpicon.ico");
        if (missingFiles.Count > 0)
        {
            MessageBox.Show(LanguageManager.Current.ErrorMissingFiles +
                string.Join("\n", missingFiles.Select(f => $"• {f}")) +
                LanguageManager.Current.ErrorFilesLocation + AppFolder,
                LanguageManager.Current.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
        return true;
    }
    private bool InitializeDiscordRPC(string? clientId = null)
    {
        try
        {
            string idToUse = clientId ?? config.Discord!.ApplicationId;
            if (string.IsNullOrEmpty(idToUse)) return false;
            if (rpc != null)
            {
                if (_currentRpcClientId == idToUse) return true;
                Log($"Switching Discord Client ID: {_currentRpcClientId ?? "none"} -> {idToUse}");
                rpc.Dispose();
            }
            _currentRpcClientId = idToUse;
            rpc = new DiscordRpcClient(idToUse);
            rpc.OnReady += async (sender, e) =>
            {
                Log($"Discord RPC Ready! (ID: {idToUse}) User: {e.User.Username} (ID: {e.User.ID})");
                await Task.Run(async () =>
                {
                    try
                    {
                        string username = e.User.Username ?? "Unknown";
                        string displayName = e.User.DisplayName ?? username;
                        ulong userId = e.User.ID;
                        Log($"Telemetry: Preparing to send - User: {displayName}, ID: {userId}");
                        await TelemetryService.ReportStartupAsync(displayName, userId);
                    }
                    catch (Exception ex) { Log($"Telemetry error in OnReady: {ex.Message}"); }
                });
            };
            rpc.OnError += (sender, e) => Log($"Discord RPC Error: {e.Message}");
            rpc.OnConnectionFailed += (sender, e) => Log($"Discord RPC Connection Failed: {e.FailedPipe}");
            rpc.Initialize();
            Log($"Discord RPC initialized successfully with ID: {idToUse}", "INFO", "DiscordRPC");
            return true;
        }
        catch (Exception ex)
        {
            Log($"Failed to initialize Discord RPC: {ex.Message}", "ERROR", "DiscordRPC");
            MessageBox.Show(string.Format(LanguageManager.Current.ErrorDiscordConnection, ex.Message),
                LanguageManager.Current.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
    }
    private bool SetupTrayIcon()
    {
        try
        {
            trayIcon = new NotifyIcon
            {
                Icon = new Icon(IconPath),
                Text = LanguageManager.Current.AppName,
                Visible = true
            };
            trayIcon.DoubleClick += (s, e) => _threadMarshaller.Invoke(new Action(() => OnTogglePause(null, EventArgs.Empty)));
            trayIcon.BalloonTipClicked += (s, e) =>
            {
               if (_pendingUpdate != null)
               {
                   _threadMarshaller.Invoke(new Action(() => {
                        UpdateDialogs.ShowEnhancedUpdateDialog(_pendingUpdate);
                        _pendingUpdate = null; // Clear after showing
                   }));
               }
            };
            UpdateTrayMenu();
            _trayAnimator = new TrayIconAnimator(trayIcon, IconPath, _threadMarshaller, (msg) => Log(msg));
            Log("Tray icon setup completed", "INFO", "TrayIcon");
            return true;
        }
        catch (Exception ex)
        {
            Log($"Failed to setup tray icon: {ex.Message}", "ERROR", "TrayIcon");
            MessageBox.Show(LanguageManager.Current.ErrorOpenFile + ex.Message,
                LanguageManager.Current.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }
    #endregion
    #region ----- Control Actions -----
    private void OnTogglePause(object? sender, EventArgs e)
    {
        isPaused = !isPaused;
        pauseItem!.Checked = isPaused;
        pauseItem.Text = isPaused ? LanguageManager.Current.MenuResume : LanguageManager.Current.MenuPause;
        UpdateTrayText();
        if (isPaused)
        {
            rpc?.ClearPresence();
            if (previewForm != null && previewForm.Visible) previewForm.SetPausedState();
            Log("Presence paused");
            ShowBalloonTip(LanguageManager.Current.AppName, LanguageManager.Current.MsgPresencePaused, ToolTipIcon.Info);
        }
        else
        {
            Log("Presence resumed");
            ShowBalloonTip(LanguageManager.Current.AppName, LanguageManager.Current.MsgPresenceResumed, ToolTipIcon.Info);
            if (previewForm != null && previewForm.Visible) previewForm.SetIdleState();
            if (currentApp != null && currentApp != "config") RefreshCurrentPresence();
            else UpdatePresenceFromConfig();
        }
        MemoryHelper.TrimMemory();
    }
    private void OnMouseEnergyChanged(MouseActivityTracker.EnergyLevel energy, double velocity, int cpm)
    {
        if (!isPaused && currentApp != null && currentApp != "config" && SettingsService.Instance.MouseEnergyEnabled)
        {
            var now = DateTime.UtcNow;
            if ((now - _lastEnergyRpcUpdate).TotalSeconds >= MIN_ENERGY_RPC_INTERVAL_SECONDS)
            {
                _lastEnergyRpcUpdate = now;
                RefreshCurrentPresence();
                Log($"Energy RPC updated: {energy}", "DEBUG", "MouseEnergy");
            }
        }
    }
    private async void OnToggleMouseEnergy(object? sender, EventArgs e)
    {
        bool newState = !SettingsService.Instance.MouseEnergyEnabled;
        SettingsService.Instance.MouseEnergyEnabled = newState;
        await SettingsService.SaveAsync();
        _mouseEnergyMenuItem!.Checked = newState;
        _mouseTracker?.SetEnabled(newState);
        ShowBalloonTip(LanguageManager.Current.AppName,
            newState ? LanguageManager.Current.MsgMouseEnergyOn : LanguageManager.Current.MsgMouseEnergyOff,
            ToolTipIcon.Info);
        if (!isPaused && currentApp != null) RefreshCurrentPresence();
    }
    private async void OnToggleTrayAnimation(object? sender, EventArgs e)
    {
        bool newState = !SettingsService.Instance.TrayAnimationEnabled;
        SettingsService.Instance.TrayAnimationEnabled = newState;
        await SettingsService.SaveAsync();
        _trayAnimationMenuItem!.Checked = newState;
        if (!newState)
            _trayAnimator?.Stop();
        ShowBalloonTip(LanguageManager.Current.AppName,
            newState ? (LanguageManager.Current.MsgTrayAnimationOn ?? "Tray animation enabled")
                    : (LanguageManager.Current.MsgTrayAnimationOff ?? "Tray animation disabled"),
            ToolTipIcon.Info);
    }
    private void UpdateTrayText()
    {
        string status = LanguageManager.Current.AppName;
        if (isPaused) status += LanguageManager.Current.TrayPaused;
        else if (privateMode) status += LanguageManager.Current.TrayPrivate;
        trayIcon.Text = status;
    }
    private void OnTogglePrivateMode(object? sender, EventArgs e)
    {
        privateMode = !privateMode;
        privateModeItem!.Checked = privateMode;
        UpdateTrayText();
        ShowBalloonTip(LanguageManager.Current.AppName,
            privateMode ? LanguageManager.Current.MsgPrivateModeOn : LanguageManager.Current.MsgPrivateModeOff,
            ToolTipIcon.Info);
        if (!isPaused && currentApp != null) RefreshCurrentPresence();
    }
    private void RefreshCurrentPresence()
    {
        if (currentApp == null || currentApp == "config") return;
        try
        {
            var processes = System.Diagnostics.Process.GetProcessesByName(currentApp);
            try
            {
                foreach (var process in processes)
                {
                    try
                    {
                        IntPtr hwnd = process.MainWindowHandle;
                        if (hwnd != IntPtr.Zero)
                        {
                            OnAppDetected(currentApp, "", "", hwnd);
                            break;
                        }
                    }
                    catch { }
                }
            }
            finally
            {
                foreach (var p in processes) p.Dispose();
            }
        }
        catch (Exception ex) { Log($"RefreshCurrentPresence error: {ex.Message}"); }
    }
    private void OnReload(object? sender, EventArgs e)
    {
        try
        {
            Log("Reloading configuration...");
            rpc?.Dispose();
            var newConfig = LoadConfig();
            if (newConfig == null)
            {
                ShowBalloonTip(LanguageManager.Current.AppName, LanguageManager.Current.ErrorReloadFailed, ToolTipIcon.Error);
                InitializeDiscordRPC();
                return;
            }
            config = newConfig;
            AppConfigManager.Reload();
            TaskbarWatcher.Reload();
            Placeholders.Reload();
            PresenceAssets.Reload();
            NarrativeService.Reload();
            Log("Static caches reloaded (TaskbarWatcher, Placeholders, PresenceAssets, NarrativeService)");
            if (!InitializeDiscordRPC())
            {
                ShowBalloonTip(LanguageManager.Current.AppName,
                    string.Format(LanguageManager.Current.ErrorDiscordConnection, "Connection failed"), ToolTipIcon.Error);
                return;
            }
            lock (_lockState)
            {
                currentApp = null;
                appTimers.Clear();
            }
            if (!isPaused) UpdatePresenceFromConfig();
            ShowBalloonTip(LanguageManager.Current.AppName, LanguageManager.Current.MsgConfigReloaded, ToolTipIcon.Info);
            Log("Configuration reloaded successfully");
            UpdateTrayMenu();
        }
        catch (Exception ex)
        {
            Log($"Reload error: {ex}");
            ShowBalloonTip(LanguageManager.Current.AppName, LanguageManager.Current.ErrorReloadFailed + ": " + ex.Message, ToolTipIcon.Error);
        }
    }
    private void OnResetTimers(object? sender, EventArgs e)
    {
        lock (_lockState) { appTimers.Clear(); currentApp = null; }
        NarrativeService.ResetAll();
        Log("All timers reset");
        MessageBox.Show(LanguageManager.Current.MsgTimersReset, LanguageManager.Current.AppName,
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
    #endregion
    #region ----- Presence Management -----
    private void UpdatePresenceFromConfig()
    {
        if (isPaused) { Log("Skipping presence update - paused"); return; }
        try
        {
            if (_currentRpcClientId != config.Discord!.ApplicationId)
            {
                InitializeDiscordRPC(null); // Revert to default
            }
            currentApp = null;
            var pr = new RichPresence
            {
                Details = config.Discord!.Details ?? LanguageManager.Current.Idling,
                State = config.Discord.State ?? LanguageManager.Current.Ready,
                Assets = GetDefaultAssets()
            };
            if (config.Discord.Buttons?.Length > 0)
            {
                var validButtons = config.Discord.Buttons
                    .Where(b => !string.IsNullOrEmpty(b.Label)
                                && !string.IsNullOrEmpty(b.Url)
                                && IsValidUrl(b.Url)
                                && b.Label.Length <= 32)
                    .Take(2)
                    .Select(b => new DiscordRPC.Button { Label = b.Label, Url = b.Url })
                    .ToArray();
                if (validButtons.Length > 0)
                    pr.Buttons = validButtons;
            }
            rpc?.SetPresence(pr);
            if (previewForm != null && previewForm.Visible) previewForm.UpdatePresence(pr);
            Log("Updated presence to idle state");
        }
        catch (Exception ex) { Log($"UpdatePresenceFromConfig error: {ex.Message}"); }
    }
    private void OnAppDetected(string proc, string _, string __, IntPtr hWnd)
    {
        if (isPaused) return;
        bool isDisabled;
        lock (_lockState) { isDisabled = _disabledApps.Contains(proc); }
        if (isDisabled)
        {
            if (currentApp == proc)
            {
                Log($"App '{proc}' is disabled. Clearing presence.");
                UpdatePresenceFromConfig();
            }
            return;
        }
        try
        {
            if (proc == "config") { UpdatePresenceFromConfig(); return; }
            if (!appTimers.ContainsKey(proc))
            {
                appTimers[proc] = DateTime.UtcNow;
                Log($"New app timer started: {proc}");
            }
            string? prevApp = currentApp;
            if (SettingsService.Instance.TrayAnimationEnabled && currentApp != proc)
            {
                Log($"App switch detected: '{prevApp ?? "null"}' ? '{proc}' - Triggering animation");
                _trayAnimator?.AnimateOnSwitch();
            }
            else
            {
                LogService.Debug($"Animation NOT triggered - enabled: {SettingsService.Instance.TrayAnimationEnabled}, prevApp: '{prevApp}', newApp: '{proc}'", "Program");
            }
            currentApp = proc;
            _appsUsedThisSession.Add(proc);
            var appConfig = AppConfigManager.Apps.FirstOrDefault(a => a.Process?.Equals(proc, StringComparison.OrdinalIgnoreCase) == true);
            string? targetClientId = !string.IsNullOrEmpty(appConfig?.ClientId) ? appConfig.ClientId : config.Discord!.ApplicationId;
            if (_currentRpcClientId != targetClientId)
            {
                Log($"App '{proc}' requires Client ID switch: {_currentRpcClientId ?? "default"} -> {targetClientId}");
                InitializeDiscordRPC(targetClientId);
            }
            try
            {
                TimeSpan sessionTime = DateTime.Now - lastStatsUpdate;
                if (sessionTime.TotalSeconds > 0 && sessionTime.TotalMinutes < 10)
                {
                    string appName = Placeholders.GetAppName(proc);
                    lock (_lockState) { statistics.TrackApp(proc, appName, sessionTime); }
                }
                lastStatsUpdate = DateTime.Now;
            }
            catch (Exception ex) { Log($"Statistics tracking error: {ex.Message}"); }
            string detailsTemplate = GetCustomDetailsForApp(proc, appConfig);
            string stateTemplate = GetCustomStateForApp(proc);
            string details = ReplacePlaceholders(detailsTemplate, proc, hWnd);
            string state = ReplacePlaceholders(stateTemplate, proc, hWnd);
            if (SettingsService.Instance.MouseEnergyEnabled && _mouseTracker != null)
            {
                string energyState = _mouseTracker.GetEnergyStateText();
                if (!string.IsNullOrEmpty(energyState)) state = $"{state} | {energyState}";
            }
            Assets finalAsset = PresenceAssets.ForApp(proc, GetDefaultAssets());
            var presence = new RichPresence
            {
                Details = details,
                State = state,
                Assets = finalAsset,
                Timestamps = new Timestamps { Start = appTimers[proc] }
            };
            var appButtons = GetButtonsForApp(appConfig);
            if (appButtons != null && appButtons.Length > 0) presence.Buttons = appButtons;
            rpc?.SetPresence(presence);
            if (previewForm != null && previewForm.Visible) previewForm.UpdatePresence(presence);
        }
        catch (Exception ex) { Log($"OnAppDetected error: {ex.Message}"); }
    }
    private string GetCustomDetailsForApp(string processName, AppConfig? appConfig = null)
    {
        if (SettingsService.Instance.AppOverrides.TryGetValue(processName, out var ov) && !string.IsNullOrWhiteSpace(ov.Details))
            return ov.Details;
        var app = appConfig ?? AppConfigManager.Apps.FirstOrDefault(a => a.Process?.Equals(processName, StringComparison.OrdinalIgnoreCase) == true);
        if (!string.IsNullOrWhiteSpace(app?.CustomDetails)) return app.CustomDetails;
        return config.Discord!.ActiveDetails ?? "";
    }
    private string GetCustomStateForApp(string processName)
    {
        if (SettingsService.Instance.AppOverrides.TryGetValue(processName, out var ov) && !string.IsNullOrWhiteSpace(ov.State))
            return ov.State;
        return config.Discord!.ActiveState ?? "";
    }
    private DiscordRPC.Button[]? GetButtonsForApp(AppConfig? appConfig)
    {
        if (appConfig?.Buttons == null || appConfig.Buttons.Count == 0) return null;
        var validButtons = appConfig.Buttons
            .Where(b => !string.IsNullOrEmpty(b.Label)
                        && !string.IsNullOrEmpty(b.Url)
                        && IsValidUrl(b.Url)  // NEW: Validate URL
                        && b.Label.Length <= 32)  // NEW: Discord limit
            .Take(2)
            .Select(b => new DiscordRPC.Button { Label = b.Label, Url = b.Url })
            .ToArray();
        return validButtons.Length > 0 ? validButtons : null;
    }
    private Assets GetDefaultAssets() => new Assets
    {
        LargeImageKey = config.Discord!.Assets?.LargeImageKey ?? "",
        LargeImageText = config.Discord.Assets?.LargeImageText ?? "",
        SmallImageKey = config.Discord.Assets?.SmallImageKey ?? "",
        SmallImageText = config.Discord.Assets?.SmallImageText ?? ""
    };
    private bool IsValidUrl(string url)
    {
        if (string.IsNullOrEmpty(url)) return false;
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return false;
        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
    private string ReplacePlaceholders(string format, string processName, IntPtr hWnd)
    {
        if (string.IsNullOrEmpty(format)) return format ?? "";
        try
        {
            string appName = Placeholders.GetAppName(processName);
            string title = Placeholders.GetWindowTitle(hWnd);
            if (privateMode && !string.IsNullOrEmpty(title)) title = "********";
            if (string.IsNullOrEmpty(title) || title.Length <= 3) title = privateMode ? "********" : LanguageManager.Current.Working;
            string wittyText = NarrativeService.GetForApp(processName);
            return format.Replace("{process_name}", processName ?? "")
                .Replace("{app_name}", appName ?? processName ?? "")
                .Replace("{window_title}", title)
                .Replace("{witty_text}", wittyText);
        }
        catch (Exception ex) { Log($"ReplacePlaceholders error: {ex.Message}"); return format; }
    }
    #endregion
    #region ----- Helpers & UI -----
    private void ShowBalloonTip(string title, string text, ToolTipIcon icon)
    {
        try
        {
            if (_threadMarshaller.InvokeRequired)
                _threadMarshaller.BeginInvoke(new Action(() =>
                {
                    trayIcon.BalloonTipTitle = title;
                    trayIcon.BalloonTipText = text;
                    trayIcon.BalloonTipIcon = icon;
                    trayIcon.ShowBalloonTip(BALLOON_TIP_TIMEOUT_MS);
                }));
            else
            {
                trayIcon.BalloonTipTitle = title;
                trayIcon.BalloonTipText = text;
                trayIcon.BalloonTipIcon = icon;
                trayIcon.ShowBalloonTip(BALLOON_TIP_TIMEOUT_MS);
            }
        }
        catch (Exception ex) { Log($"ShowBalloonTip error: {ex.Message}"); }
    }
    private static void Log(string message, string level = "INFO", string module = "geetRPCS")
    {
        // Delegate to centralized LogService (backward compatibility wrapper)
        LogService.Log(message, level, module);
    }
    private void OpenFileWithDefaultEditor(string filePath, string fileName)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                MessageBox.Show(LanguageManager.Current.DialogFileNotFound, LanguageManager.Current.AppName,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var psi = new System.Diagnostics.ProcessStartInfo { FileName = filePath, UseShellExecute = true };
            System.Diagnostics.Process.Start(psi);
            Log($"Opened {fileName} with default editor");
            ShowBalloonTip(LanguageManager.Current.AppName, LanguageManager.Current.MsgReloadTip, ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            Log($"Failed to open {fileName}: {ex.Message}");
            var result = MessageBox.Show(LanguageManager.Current.DialogOpenWithNotepad, LanguageManager.Current.AppName,
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result == DialogResult.Yes) System.Diagnostics.Process.Start("notepad.exe", filePath);
        }
    }
    private void UpdateTrayMenu()
    {
        if (_threadMarshaller.InvokeRequired) { _threadMarshaller.BeginInvoke(new Action(UpdateTrayMenu)); return; }
        try
        {
            trayIcon.ContextMenuStrip?.Dispose();
            var menu = new ContextMenuStrip();
            pauseItem = new ToolStripMenuItem(isPaused ? LanguageManager.Current.MenuResume : LanguageManager.Current.MenuPause)
            { Checked = isPaused };
            pauseItem.Click += OnTogglePause;
            menu.Items.Add(pauseItem);
            privateModeItem = new ToolStripMenuItem(LanguageManager.Current.MenuPrivateMode) { Checked = privateMode };
            privateModeItem.Click += OnTogglePrivateMode;
            menu.Items.Add(privateModeItem);
            _mouseEnergyMenuItem = new ToolStripMenuItem(LanguageManager.Current.MenuMouseEnergy) { Checked = SettingsService.Instance.MouseEnergyEnabled };
            _mouseEnergyMenuItem.Click += OnToggleMouseEnergy;
            menu.Items.Add(_mouseEnergyMenuItem);
            _trayAnimationMenuItem = new ToolStripMenuItem(LanguageManager.Current.MenuTrayAnimation ?? "?? Tray Icon Animation") { Checked = SettingsService.Instance.TrayAnimationEnabled };
            _trayAnimationMenuItem.Click += OnToggleTrayAnimation;
            menu.Items.Add(_trayAnimationMenuItem);
            var telemetryItem = new ToolStripMenuItem(LanguageManager.Current.MenuTelemetry ?? "?? Send Usage Data")
            { Checked = TelemetryService.IsEnabled() };
            telemetryItem.Click += async (s, args) =>
            {
                bool newState = !TelemetryService.IsEnabled();
                await TelemetryService.SetEnabledAsync(newState);
                ((ToolStripMenuItem)s!).Checked = newState;
                ShowBalloonTip(LanguageManager.Current.AppName,
                    newState ? (LanguageManager.Current.MsgTelemetryOn ?? "Usage data enabled")
                    : (LanguageManager.Current.MsgTelemetryOff ?? "Usage data disabled"), ToolTipIcon.Info);
            };
            menu.Items.Add(telemetryItem);
            
            // Auto-Update toggle
            var autoUpdateItem = new ToolStripMenuItem("🔄 Auto-Update") { Checked = SettingsService.Instance.AutoUpdateEnabled };
            autoUpdateItem.Click += async (s, args) =>
            {
                bool newState = !SettingsService.Instance.AutoUpdateEnabled;
                SettingsService.Instance.AutoUpdateEnabled = newState;
                await SettingsService.SaveAsync();
                ((ToolStripMenuItem)s!).Checked = newState;
                ShowBalloonTip(LanguageManager.Current.AppName,
                    newState ? "Auto-update enabled. App will update automatically." 
                    : "Auto-update disabled. You'll be notified about updates.", ToolTipIcon.Info);
                Log($"Auto-update {(newState ? "enabled" : "disabled")}");
            };
            menu.Items.Add(autoUpdateItem);
            menu.Items.Add(new ToolStripSeparator());
            var manageAppsItem = new ToolStripMenuItem(LanguageManager.Current.MenuManageApps);
            manageAppsItem.Click += (s, e) => ToggleManageAppsVisibility();
            menu.Items.Add(manageAppsItem);
            var changeIdItem = new ToolStripMenuItem(LanguageManager.Current.MenuChangeAppId); // [Updated]
            changeIdItem.Click += (s, e) =>
            {
                string currentId = config.Discord!.ApplicationId;
                string newId = ShowInputDialog(
                    LanguageManager.Current.DialogChangeAppIdMessage,
                    LanguageManager.Current.DialogChangeAppIdTitle,
                    currentId);
                if (!string.IsNullOrWhiteSpace(newId) && newId != currentId)
                {
                    try
                    {
                        config.Discord.ApplicationId = newId.Trim();
                        var options = new JsonSerializerOptions { WriteIndented = true };
                        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(config, typeof(Config), new JsonContext(options)));
                        try
                        {
                            OnReload(null, EventArgs.Empty);
                        }
                        catch { }
                        MessageBox.Show(
                            LanguageManager.Current.MsgAppIdChanged,
                            LanguageManager.Current.AppName, // Atau "Success"
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        Log($"Failed to save ID: {ex.Message}");
                        MessageBox.Show($"{LanguageManager.Current.ErrorSaveConfig}: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            };
            menu.Items.Add(changeIdItem);
            menu.Items.Add(new ToolStripSeparator());
            AddStatisticsMenu(menu);
            previewMenuItem = new ToolStripMenuItem(LanguageManager.Current.MenuPreviewWindow)
            { Checked = previewForm?.Visible ?? false };
            previewMenuItem.Click += (_, __) => TogglePreviewVisibility();
            menu.Items.Add(previewMenuItem);
            menu.Items.Add(new ToolStripSeparator());
            var startupItem = new ToolStripMenuItem(LanguageManager.Current.MenuStartup);
            try { startupItem.Checked = StartupTask.IsEnabled(); } catch { startupItem.Checked = false; }
            startupItem.Click += (_, __) =>
            {
                try
                {
                    StartupTask.Enable(!startupItem.Checked);
                    startupItem.Checked = !startupItem.Checked;
                }
                catch (Exception ex)
                {
                    Log($"Startup toggle error: {ex.Message}");
                    MessageBox.Show(LanguageManager.Current.ErrorStartupToggle + ex.Message,
                        LanguageManager.Current.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };
            menu.Items.Add(startupItem);
            AddQuickActionsMenu(menu);
            menu.Items.Add(new ToolStripSeparator());
            AddLanguageMenu(menu);
            menu.Items.Add(LanguageManager.Current.MenuCheckUpdates, null,
                async (_, __) => 
                {
                    var release = await UpdateChecker.CheckForUpdates(showUpToDateMessage: true);
                    if (release != null)
                    {
                        _threadMarshaller.Invoke(new Action(() => UpdateDialogs.ShowEnhancedUpdateDialog(release)));
                    }
                });
            menu.Items.Add(LanguageManager.Current.MenuOpenLog, null, (_, __) =>
            {
                try
                {
                    string logPath = Path.Combine(AppFolder, "geetRPCS.log");
                    if (File.Exists(logPath)) System.Diagnostics.Process.Start("notepad.exe", logPath);
                    else MessageBox.Show(LanguageManager.Current.DialogLogNotCreated, LanguageManager.Current.AppName,
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex) { Log($"Failed to open log file: {ex.Message}"); }
            });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(LanguageManager.Current.MenuExit, null, OnExit);
            trayIcon.ContextMenuStrip = menu;
            Log("Tray menu updated");
        }
        catch (Exception ex) { Log($"Failed to update tray menu: {ex}"); }
    }
    private void AddStatisticsMenu(ContextMenuStrip menu)
    {
        var statsMenu = new ToolStripMenuItem(LanguageManager.Current.MenuStatistics);
        statsMenu.DropDownItems.Add(LanguageManager.Current.MenuToday, null, (_, __) => ShowTodayStatistics());
        statsMenu.DropDownItems.Add(LanguageManager.Current.MenuThisWeek, null, (_, __) => ShowWeekStatistics());
        statsMenu.DropDownItems.Add(LanguageManager.Current.MenuThisMonth, null, (_, __) => ShowMonthStatistics());
        statsMenu.DropDownItems.Add(LanguageManager.Current.MenuAllTime, null, (_, __) => ShowAllTimeStatistics());
        statsMenu.DropDownItems.Add(new ToolStripSeparator());
        statsMenu.DropDownItems.Add(LanguageManager.Current.MenuExportCSV, null, (_, __) => ExportStatistics("csv"));
        statsMenu.DropDownItems.Add(LanguageManager.Current.MenuExportJSON, null, (_, __) => ExportStatistics("json"));
        statsMenu.DropDownItems.Add(new ToolStripSeparator());
        statsMenu.DropDownItems.Add(LanguageManager.Current.MenuResetStats, null, async (_, __) =>
        {
            if (MessageBox.Show(LanguageManager.Current.DialogResetStatsMessage, LanguageManager.Current.DialogResetStatsTitle,
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                await statistics.ResetAsync();
                ShowBalloonTip(LanguageManager.Current.AppName, LanguageManager.Current.MsgStatsReset, ToolTipIcon.Info);
            }
        });
        menu.Items.Add(statsMenu);
    }
    private void AddQuickActionsMenu(ContextMenuStrip menu)
    {
        var quickActionsMenu = new ToolStripMenuItem(LanguageManager.Current.MenuQuickActions);
        quickActionsMenu.DropDownItems.Add(LanguageManager.Current.MenuOpenFolder, null,
            (_, __) => { try { System.Diagnostics.Process.Start("explorer.exe", AppFolder); } catch (Exception ex) { Log($"Failed to open folder: {ex.Message}"); } });
        quickActionsMenu.DropDownItems.Add(LanguageManager.Current.MenuEditConfig, null,
            (_, __) => OpenOrCreateConfig());
        quickActionsMenu.DropDownItems.Add(LanguageManager.Current.MenuEditApps, null,
            (_, __) => OpenFileWithDefaultEditor(AppsPath, "apps.json"));
        quickActionsMenu.DropDownItems.Add(new ToolStripSeparator());
        quickActionsMenu.DropDownItems.Add(LanguageManager.Current.MenuReloadAll, null, (_, __) =>
        {
            if (MessageBox.Show(LanguageManager.Current.DialogReloadMessage, LanguageManager.Current.DialogReloadTitle,
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes) OnReload(null, EventArgs.Empty);
        });
        
        // Add Shortcut Management submenu
        quickActionsMenu.DropDownItems.Add(new ToolStripSeparator());
        var shortcutMenu = new ToolStripMenuItem("➕ Manage Shortcuts");
        
        // Desktop Shortcut
        var desktopShortcutItem = new ToolStripMenuItem("Desktop Shortcut") 
        { 
            Checked = ShortcutManager.IsDesktopShortcutExists() 
        };
        desktopShortcutItem.Click += async (_, __) =>
        {
            try
            {
                if (ShortcutManager.IsDesktopShortcutExists())
                {
                    if (MessageBox.Show("Remove desktop shortcut?", LanguageManager.Current.AppName,
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        ShortcutManager.RemoveDesktopShortcut();
                        ShowBalloonTip(LanguageManager.Current.AppName, "Desktop shortcut removed", ToolTipIcon.Info);
                        
                        // Update preference
                        SettingsService.Instance.ShortcutPreferences.DesktopShortcut = false;
                        await SettingsService.SaveAsync();
                    }
                }
                else
                {
                    ShortcutManager.CreateDesktopShortcut();
                    ShortcutManager.RefreshIconCache();
                    ShowBalloonTip(LanguageManager.Current.AppName, "Desktop shortcut created", ToolTipIcon.Info);
                    
                    // Update preference
                    SettingsService.Instance.ShortcutPreferences.DesktopShortcut = true;
                    SettingsService.Instance.ShortcutPreferences.PreferenceSaved = true;
                    await SettingsService.SaveAsync();
                }
                UpdateTrayMenu(); // Refresh to update checkmark
            }
            catch (Exception ex)
            {
                Log($"Desktop shortcut error: {ex.Message}", "ERROR");
                MessageBox.Show($"Failed to manage desktop shortcut: {ex.Message}", 
                    LanguageManager.Current.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };
        shortcutMenu.DropDownItems.Add(desktopShortcutItem);
        
        // Start Menu Shortcut
        var startMenuShortcutItem = new ToolStripMenuItem("Start Menu Shortcut") 
        { 
            Checked = ShortcutManager.IsStartMenuShortcutExists() 
        };
        startMenuShortcutItem.Click += async (_, __) =>
        {
            try
            {
                if (ShortcutManager.IsStartMenuShortcutExists())
                {
                    if (MessageBox.Show("Remove Start Menu shortcut?", LanguageManager.Current.AppName,
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        ShortcutManager.RemoveStartMenuShortcut();
                        ShowBalloonTip(LanguageManager.Current.AppName, "Start Menu shortcut removed", ToolTipIcon.Info);
                        
                        // Update preference
                        SettingsService.Instance.ShortcutPreferences.StartMenuShortcut = false;
                        await SettingsService.SaveAsync();
                    }
                }
                else
                {
                    ShortcutManager.CreateStartMenuShortcut();
                    ShortcutManager.RefreshIconCache();
                    ShowBalloonTip(LanguageManager.Current.AppName, "Start Menu shortcut created", ToolTipIcon.Info);
                    
                    // Update preference
                    SettingsService.Instance.ShortcutPreferences.StartMenuShortcut = true;
                    SettingsService.Instance.ShortcutPreferences.PreferenceSaved = true;
                    await SettingsService.SaveAsync();
                }
                UpdateTrayMenu(); // Refresh to update checkmark
            }
            catch (Exception ex)
            {
                Log($"Start Menu shortcut error: {ex.Message}", "ERROR");
                MessageBox.Show($"Failed to manage Start Menu shortcut: {ex.Message}", 
                    LanguageManager.Current.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };
        shortcutMenu.DropDownItems.Add(startMenuShortcutItem);
        
        quickActionsMenu.DropDownItems.Add(shortcutMenu);
        
        menu.Items.Add(quickActionsMenu);
    }
    private void AddLanguageMenu(ContextMenuStrip menu)
    {
        var languageMenu = new ToolStripMenuItem(LanguageManager.Current.MenuLanguage);
        var availableLanguages = LanguageManager.GetAvailableLanguages();
        string currentLang = LanguageManager.GetCurrentLanguageCode();
        foreach (var lang in availableLanguages)
        {
            var langItem = new ToolStripMenuItem(lang.Name) { Checked = (lang.Code == currentLang) };
            langItem.Click += async (_, __) =>
            {
                await LanguageManager.SetLanguageAsync(lang.Code);
                ShowBalloonTip(LanguageManager.Current.AppName, LanguageManager.Current.MsgLanguageChanged, ToolTipIcon.Info);
                UpdateTrayMenu();
                UpdateTrayText();
            };
            languageMenu.DropDownItems.Add(langItem);
        }
        menu.Items.Add(languageMenu);
    }
    private void OpenOrCreateConfig()
    {
        try
        {
            if (!File.Exists(ConfigPath))
            {
                var result = MessageBox.Show(
                    LanguageManager.Current.DialogConfigNotFound,
                    LanguageManager.Current.AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result == DialogResult.Yes) CreateDefaultConfigFile();
                else return;
            }
            OpenFileWithDefaultEditor(ConfigPath, "config.json");
        }
        catch (Exception ex)
        {
            Log($"Error opening config: {ex.Message}");
            MessageBox.Show($"Error: {ex.Message}", LanguageManager.Current.AppName,
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
    private void CreateDefaultConfigFile()
    {
        try
        {
            var defaultConfig = GetDefaultConfig();
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(defaultConfig, typeof(Config), new JsonContext(options)));
            Log("Created default config.json");
            ShowBalloonTip(LanguageManager.Current.AppName, LanguageManager.Current.MsgConfigCreated, ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            Log($"Failed to create config.json: {ex.Message}");
            MessageBox.Show($"{LanguageManager.Current.ErrorCreateConfig}\n{ex.Message}",
                LanguageManager.Current.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
    private string ShowInputDialog(string text, string caption, string defaultValue = "")
    {
        string tutorialUrl = LanguageManager.Current.UrlTutorial;
        string assetsUrl = "https://github.com/geetcr4ck/geetRPCS/raw/main/AssetPack.zip";
        string defaultAppId = "1433700335863726183";
        using Form prompt = new Form()
        {
            Width = 500,
            Height = 280,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            Text = caption,
            StartPosition = FormStartPosition.CenterScreen,
            MaximizeBox = false,
            MinimizeBox = false,
            BackColor = Color.FromArgb(47, 49, 54),
            ForeColor = Color.White
        };
        Label textLabel = new Label()
        {
            Left = 20,
            Top = 20,
            Width = 440,
            Text = text,
            AutoSize = false,
            Height = 60,
            Font = new Font("Segoe UI", 9)
        };
        TextBox textBox = new TextBox()
        {
            Left = 20,
            Top = 80,
            Width = 440,
            Text = defaultValue,
            Font = new Font("Segoe UI", 10)
        };
        LinkLabel lnkTut = new LinkLabel()
        {
            Text = LanguageManager.Current.LinkTutorial,
            Left = 20,
            Top = 120,
            AutoSize = true,
            LinkColor = Color.FromArgb(88, 101, 242),
            ActiveLinkColor = Color.FromArgb(115, 125, 255),
            Font = new Font("Segoe UI", 9)
        };
        lnkTut.LinkClicked += (s, e) => OpenUrl(tutorialUrl);
        LinkLabel lnkAssets = new LinkLabel()
        {
            Text = LanguageManager.Current.LinkDownloadAssets,
            Left = 20,
            Top = 145,
            AutoSize = true,
            LinkColor = Color.FromArgb(88, 101, 242),
            ActiveLinkColor = Color.FromArgb(115, 125, 255),
            Font = new Font("Segoe UI", 9)
        };
        lnkAssets.LinkClicked += (s, e) => OpenUrl(assetsUrl);
        System.Windows.Forms.Button btnReset = new System.Windows.Forms.Button()
        {
            Text = "Reset Default", // Can be replaced with just “Reset”
            Left = 210, // To the left of the Save button
            Width = 120,
            Top = 180,
            BackColor = Color.FromArgb(79, 84, 92), // Gray (Secondary)
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.White,
            Cursor = Cursors.Hand
        };
        btnReset.FlatAppearance.BorderSize = 0;
        btnReset.Click += (s, e) => { textBox.Text = defaultAppId; };
        System.Windows.Forms.Button confirmation = new System.Windows.Forms.Button()
        {
            Text = LanguageManager.Current.BtnSave ?? "Save",
            Left = 340,
            Width = 120,
            Top = 180,
            DialogResult = DialogResult.OK,
            BackColor = Color.FromArgb(88, 101, 242), // Blurple (Primary)
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.White,
            Cursor = Cursors.Hand
        };
        confirmation.FlatAppearance.BorderSize = 0;
        confirmation.Click += (sender, e) => { prompt.Close(); };
        prompt.Controls.Add(textLabel);
        prompt.Controls.Add(textBox);
        prompt.Controls.Add(lnkTut);
        prompt.Controls.Add(lnkAssets);
        prompt.Controls.Add(btnReset);      // <--- Add Reset Button
        prompt.Controls.Add(confirmation);
        prompt.AcceptButton = confirmation;
        return prompt.ShowDialog() == DialogResult.OK ? textBox.Text : "";
    }
    private void OpenUrl(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(LanguageManager.Current.ErrorOpenLink + " " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
    #endregion
    #region ----- Statistics -----
    private void ShowTodayStatistics()
    {
        var topApps = statistics.GetTopAppsToday(10);
        if (topApps.Count == 0)
        {
            MessageBox.Show(LanguageManager.Current.StatsNoDataToday, LanguageManager.Current.MenuToday,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var sb = new StringBuilder();
        sb.AppendLine(LanguageManager.Current.StatsTodayTitle);
        sb.AppendLine("=============\n");
        int rank = 1;
        foreach (var (appName, time) in topApps)
        {
            sb.AppendLine($"{rank}. {appName}");
            sb.AppendLine($"   {FormatTimeSpan(time)}\n");
            rank++;
        }
        var totalToday = topApps.Sum(x => x.time.TotalSeconds);
        sb.AppendLine($"{LanguageManager.Current.StatsTotal} {FormatTimeSpan(TimeSpan.FromSeconds(totalToday))}");
        MessageBox.Show(sb.ToString(), LanguageManager.Current.MenuToday, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
    private void ShowWeekStatistics()
    {
        var weekStart = DateTime.Now.Date.AddDays(-(int)DateTime.Now.DayOfWeek);
        var sb = new StringBuilder();
        sb.AppendLine(LanguageManager.Current.StatsWeekTitle);
        sb.AppendLine($"{LanguageManager.Current.StatsWeekOf} {weekStart:MMM dd, yyyy}");
        sb.AppendLine("=================\n");
        var appsThisWeek = statistics.AppUsage.Values
            .Where(a => a.WeeklyUsage.ContainsKey(weekStart))
            .Select(a => (a.AppName, a.WeeklyUsage[weekStart]))
            .OrderByDescending(x => x.Item2).Take(10).ToList();
        if (appsThisWeek.Count == 0)
        {
            MessageBox.Show(LanguageManager.Current.StatsNoDataWeek, LanguageManager.Current.MenuThisWeek,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        int rank = 1;
        foreach (var (appName, time) in appsThisWeek)
        {
            sb.AppendLine($"{rank}. {appName}");
            sb.AppendLine($"   {FormatTimeSpan(time)}\n");
            rank++;
        }
        var totalWeek = appsThisWeek.Sum(x => x.Item2.TotalSeconds);
        sb.AppendLine($"{LanguageManager.Current.StatsTotal} {FormatTimeSpan(TimeSpan.FromSeconds(totalWeek))}");
        MessageBox.Show(sb.ToString(), LanguageManager.Current.MenuThisWeek, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
    private void ShowMonthStatistics()
    {
        var monthStart = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        var sb = new StringBuilder();
        sb.AppendLine(LanguageManager.Current.StatsMonthTitle);
        sb.AppendLine($"{monthStart:MMMM yyyy}");
        sb.AppendLine("==================\n");
        var appsThisMonth = statistics.AppUsage.Values
            .Where(a => a.MonthlyUsage.ContainsKey(monthStart))
            .Select(a => (a.AppName, a.MonthlyUsage[monthStart]))
            .OrderByDescending(x => x.Item2).Take(10).ToList();
        if (appsThisMonth.Count == 0)
        {
            MessageBox.Show(LanguageManager.Current.StatsNoDataMonth, LanguageManager.Current.MenuThisMonth,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        int rank = 1;
        foreach (var (appName, time) in appsThisMonth)
        {
            sb.AppendLine($"{rank}. {appName}");
            sb.AppendLine($"   {FormatTimeSpan(time)}\n");
            rank++;
        }
        var totalMonth = appsThisMonth.Sum(x => x.Item2.TotalSeconds);
        sb.AppendLine($"{LanguageManager.Current.StatsTotal} {FormatTimeSpan(TimeSpan.FromSeconds(totalMonth))}");
        MessageBox.Show(sb.ToString(), LanguageManager.Current.MenuThisMonth, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
    private void ShowAllTimeStatistics()
    {
        var topApps = statistics.GetTopAppsAllTime(10);
        if (topApps.Count == 0)
        {
            MessageBox.Show(LanguageManager.Current.StatsNoData, LanguageManager.Current.MenuAllTime,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var sb = new StringBuilder();
        sb.AppendLine(LanguageManager.Current.StatsAllTimeTitle);
        sb.AppendLine($"{LanguageManager.Current.StatsTrackingSince} {statistics.AppUsage.Values.Min(a => a.FirstUsed):MMM dd, yyyy}");
        sb.AppendLine("===================\n");
        int rank = 1;
        foreach (var (appName, time) in topApps)
        {
            sb.AppendLine($"{rank}. {appName}");
            sb.AppendLine($"   {FormatTimeSpan(time)}\n");
            rank++;
        }
        sb.AppendLine($"{LanguageManager.Current.StatsTotalTracked} {FormatTimeSpan(statistics.TotalTrackedTime)}");
        sb.AppendLine($"{LanguageManager.Current.StatsTotalApps} {statistics.AppUsage.Count}");
        MessageBox.Show(sb.ToString(), LanguageManager.Current.MenuAllTime, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
    private async void ExportStatistics(string format)
    {
        try
        {
            string content;
            lock (_lockState)
            {
                content = format == "csv" ? statistics.PrepareCSV() : statistics.PrepareExportJSON();
            }
            string filePath = await statistics.WriteExportAsync(content, format);
            if (filePath != null && File.Exists(filePath))
            {
                var result = MessageBox.Show(
                    $"{LanguageManager.Current.StatsExportSuccess}\n\n{Path.GetFileName(filePath)}\n\n{LanguageManager.Current.StatsOpenFolder}",
                    LanguageManager.Current.AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                if (result == DialogResult.Yes) System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{filePath}\"");
            }
            else MessageBox.Show(LanguageManager.Current.StatsExportFailed, LanguageManager.Current.AppName,
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch (Exception ex)
        {
            Log($"Export error: {ex.Message}");
            MessageBox.Show(string.Format(LanguageManager.Current.ErrorExport, ex.Message),
                LanguageManager.Current.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
    private string FormatTimeSpan(TimeSpan time)
    {
        if (time.TotalHours >= 1) return $"{(int)time.TotalHours}h {time.Minutes}m";
        else if (time.TotalMinutes >= 1) return $"{(int)time.TotalMinutes}m {time.Seconds}s";
        else return $"{(int)time.TotalSeconds}s";
    }
    #endregion
    #region ----- Exit -----
    private void OnExit(object? sender, EventArgs e)
    {
        try
        {
            Log("geetRPCS shutting down...");
            try
            {
                var sessionDuration = DateTime.Now - _sessionStartTime;
                TelemetryService.ReportShutdownAsync(sessionDuration, _appsUsedThisSession.Count).Wait(3000);
            }
            catch (Exception ex) { Log($"Shutdown telemetry error: {ex.Message}"); }
            _hkPause?.Dispose();
            _hkPreview?.Dispose();
            _hkReload?.Dispose();
            _hkPrivate?.Dispose();
            _hkStats?.Dispose();
            _mouseTracker?.Stop();
            _mouseTracker?.Dispose();
            _trayAnimator?.Stop();
            _trayAnimator?.Dispose();
            previewForm?.Close();
            previewForm?.Dispose();
            if (statistics != null)
            {
                string json;
                lock (_lockState) { json = statistics.PrepareJson(); }
                AppStatistics.WriteJsonAsync(json).Wait(3000);
            }
            Log("Statistics saved on exit");
            statsTimer?.Stop();
            statsTimer?.Dispose();
            trayIcon?.ContextMenuStrip?.Dispose();
            if (trayIcon != null) trayIcon.Visible = false;
            trayIcon?.Dispose();
            rpc?.ClearPresence();
            rpc?.Dispose();
            LogService.Shutdown();
            _threadMarshaller?.Dispose();
        }
        catch { }
        finally { Application.Exit(); }
    }
    #endregion
}
