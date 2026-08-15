/**
 * geetRPCS - Main Application
 * Discord Rich Presence Custom Switcher main logic.
 *
 * This file is deliberately slim: it acts as the application host (entry point,
 * tray icon, hotkeys, preview form) and wires the feature components together:
 *   - AppCoordinator    : central state & presence/RPC orchestration
 *   - PresenceBuilder   : RPC payload assembly
 *   - StatsCoordinator  : usage statistics views/exports
 *   - UpdateOrchestrator: background update & maintenance loops
 *   - TrayMenuController: tray context menu UI
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
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using DiscordRPC;
using geetRPCS.Services;
using geetRPCS.UI;
using geetRPCS.Utils;

class Program : ApplicationContext, IAppHost
{
    // --- UI host state ---
    private NotifyIcon trayIcon = null!;
    private readonly Control _threadMarshaller = new Control();
    private PresencePreviewForm? _previewForm;
    private ManageAppsForm? _manageAppsForm;
    private TrayMenuController? _trayMenu;
    private AppCoordinator? _coordinator;
    private UpdateOrchestrator? _updater;
    private TrayIconAnimator? _trayAnimator;
    private GlobalHotkey? _hkPause, _hkPreview, _hkReload, _hkPrivate, _hkStats;
    private UpdateChecker.GitHubRelease? _pendingUpdate;

    private static readonly string IconPath = AppPaths.IconPath;

    // --- Main Entry ---
    #region Main
    [STAThread]
    static void Main()
    {
        using (Mutex mutex = new Mutex(true, "geetRPCS-v1-SingleInstance", out bool createdNew))
        {
            if (!createdNew)
            {
                MessageBox.Show(LanguageManager.Current.ErrorAlreadyRunning, LanguageManager.Current.AppName,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            LogService.Initialize();
            try
            {
                Log($"Application started at {DateTime.Now}", "INFO", "Startup");
                Log($"App folder: {AppPaths.InstallDir}", "INFO", "Startup");
                PInvoke.User32.ShowWindow(PInvoke.User32.GetConsoleWindow(), PInvoke.User32.SW_HIDE);
                Application.Run(new Program());
            }
            catch (Exception ex)
            {
                Log($"Fatal error: {ex.Message}", "ERROR", "Fatal");
                MessageBox.Show(string.Format(LanguageManager.Current.ErrorStartupFatal, ex.Message),
                    LanguageManager.Current.DialogFatalTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
    public Program()
    {
        try
        {
            _threadMarshaller.CreateControl();
            if (!ValidateRequiredFiles()) { Application.Exit(); return; }

            _coordinator = new AppCoordinator(this);
            if (!_coordinator.Prepare())
            {
                MessageBox.Show(LanguageManager.Current.ErrorUnableLoadConfig, LanguageManager.Current.DialogErrorTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                Application.Exit();
                return;
            }
            if (!InitializeDiscordRPC() || !SetupTrayIcon()) { Application.Exit(); return; }

            _coordinator.PublishIdlePresence();
            _coordinator.StartWatcher();
            _coordinator.StartTimers();
            _coordinator.InitMouseTracker();
            RegisterHotkeys();

            _updater = new UpdateOrchestrator(ShowBalloonTip, OnReleaseFound);
            _updater.Start();
            _coordinator.StartAutoUpdateCheck();

            Log("geetRPCS initialized successfully!");
            MemoryHelper.TrimMemory();
        }
        catch (Exception ex)
        {
            Log($"INIT ERROR: {ex}");
            MessageBox.Show(string.Format(LanguageManager.Current.ErrorStartupFatal, ex.Message),
                LanguageManager.Current.DialogErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            Application.Exit();
        }
    }
    #endregion

    // ----------------------------------------------------------------
    // IAppHost implementation (feedback from the coordinator)
    // ----------------------------------------------------------------
    public void ShowBalloon(string title, string message, ToolTipIcon icon) => ShowBalloonTip(title, message, icon);
    public void PublishPresence(RichPresence presence)
    {
        if (_previewForm != null && _previewForm.Visible) _previewForm.UpdatePresence(presence);
    }
    public void PreviewPausedState()
    {
        if (_previewForm != null && _previewForm.Visible) _previewForm.SetPausedState();
    }
    public void PreviewIdleState()
    {
        if (_previewForm != null && _previewForm.Visible) _previewForm.SetIdleState();
    }
    public void RefreshTrayPresentation() => _trayMenu?.UpdatePresentation();
    public void RebuildTrayMenu()
    {
        if (_threadMarshaller.InvokeRequired) { _threadMarshaller.BeginInvoke(new Action(RebuildTrayMenu)); return; }
        _trayMenu?.Rebuild();
    }
    public void AnimateOnSwitch() => _trayAnimator?.AnimateOnSwitch();

    // ----------------------------------------------------------------
    // Shell actions consumed by TrayMenuController
    // ----------------------------------------------------------------
    public bool IsPreviewVisible => _previewForm != null && _previewForm.Visible;
    public void TogglePreviewVisibility()
    {
        if (_previewForm == null || _previewForm.IsDisposed)
        {
            Log("Creating PresencePreviewForm...", "INFO", "Preview");
            InitPreviewForm();
            _previewForm!.Show();
            if (_coordinator != null)
            {
                if (_coordinator.CurrentApp == null)
                    _coordinator.PublishIdlePresence();
                else
                    _coordinator.RefreshCurrentPresence();
            }
        }
        else
        {
            Log("Destroying PresencePreviewForm to save RAM...", "INFO", "Preview");
            _previewForm.Close();
            _previewForm = null;
            MemoryHelper.TrimMemory();
        }
    }

    public void ToggleManageAppsVisibility()
    {
        if (_coordinator == null) return;
        if (_manageAppsForm == null || _manageAppsForm.IsDisposed)
        {
            Log("Opening ManageAppsForm...", "INFO", "ManageApps");
            _manageAppsForm = new ManageAppsForm(
                AppConfigManager.Apps,
                new HashSet<string>(_coordinator.DisabledApps, StringComparer.OrdinalIgnoreCase),
                _coordinator.Overrides,
                async (proc, enabled) =>
                {
                    _coordinator.SetAppDisabled(proc, enabled);
                    await _coordinator.SaveSettingsAsync();
                },
                async (proc, details, state) =>
                {
                    _coordinator.SetAppOverride(proc, details, state);
                    await _coordinator.SaveSettingsAsync();
                });
            _manageAppsForm.Show();
        }
        else
        {
            _manageAppsForm.BringToFront();
        }
    }

    public void CheckForUpdatesFromMenu()
    {
        _threadMarshaller.Invoke(new Action(async () =>
        {
            var release = await UpdateChecker.CheckForUpdates(showUpToDateMessage: true);
            if (release != null)
            {
                UpdateDialogs.ShowEnhancedUpdateDialog(release);
            }
        }));
    }

    public void OpenLog()
    {
        try
        {
            string logPath = AppPaths.LogPath;
            if (File.Exists(logPath)) System.Diagnostics.Process.Start("notepad.exe", logPath);
            else MessageBox.Show(LanguageManager.Current.DialogLogNotCreated, LanguageManager.Current.AppName,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { Log($"Failed to open log file: {ex.Message}"); }
    }

    public void ExitApp() => OnExit(null, EventArgs.Empty);

    // ----------------------------------------------------------------
    // Initialization helpers
    // ----------------------------------------------------------------
    private bool InitializeDiscordRPC()
    {
        if (_coordinator == null) return false;
        if (!_coordinator.InitializeRpc())
        {
            return false;
        }
        return true;
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
            trayIcon.DoubleClick += (s, e) => _threadMarshaller.Invoke(new Action(() => _coordinator!.TogglePause()));
            trayIcon.BalloonTipClicked += (s, e) =>
            {
                if (_pendingUpdate != null)
                {
                    _threadMarshaller.Invoke(new Action(() =>
                    {
                        UpdateDialogs.ShowEnhancedUpdateDialog(_pendingUpdate);
                        _pendingUpdate = null;
                    }));
                }
            };
            _trayMenu = new TrayMenuController(trayIcon, _coordinator!, this);
            _trayMenu.Rebuild();
            _trayAnimator = new TrayIconAnimator(trayIcon, IconPath, _threadMarshaller, (msg) => Log(msg, "DEBUG", "TrayIconAnimator"));
            _coordinator!.AttachTrayAnimator(_trayAnimator);
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

    private void InitPreviewForm()
    {
        string appId = _coordinator!.Config.Discord?.ApplicationId ?? "";
        _previewForm = new PresencePreviewForm(appId);
        _previewForm.FormClosing += (sender, e) =>
        {
            if (_trayMenu?.PreviewMenuItem != null) _trayMenu.PreviewMenuItem.Checked = false;
            Task.Run(async () => { await Task.Delay(500); MemoryHelper.TrimMemory(); });
        };
        _previewForm.VisibleChanged += (sender, e) =>
        {
            if (_trayMenu?.PreviewMenuItem != null) _trayMenu.PreviewMenuItem.Checked = _previewForm.Visible;
            if (_previewForm != null && !_previewForm.Visible) MemoryHelper.TrimMemory();
        };
    }

    private void RegisterHotkeys()
    {
        try
        {
            _hkPause = CreateHotkey(Keys.Control | Keys.Alt, Keys.P, () => _coordinator!.TogglePause(), "Pause");
            _hkPreview = CreateHotkey(Keys.Control | Keys.Alt, Keys.V, TogglePreviewVisibility, "Preview");
            _hkReload = CreateHotkey(Keys.Control | Keys.Alt, Keys.R, () => _coordinator!.ReloadConfig(), "Reload");
            _hkPrivate = CreateHotkey(Keys.Control | Keys.Alt, Keys.H, () => _coordinator!.TogglePrivateMode(), "Private Mode");
            _hkStats = CreateHotkey(Keys.Control | Keys.Alt, Keys.S, () => _coordinator!.Stats.ShowToday(), "Stats Today");
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

    // ----------------------------------------------------------------
    // Update discovery
    // ----------------------------------------------------------------
    private void OnReleaseFound(UpdateChecker.GitHubRelease release)
    {
        _pendingUpdate = release;
        string mode = SettingsService.Instance.UpdateNotificationMode;
        Log($"Update available. Mode: {mode}");
        _threadMarshaller.Invoke(new Action(() =>
        {
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
        }));
    }

    // ----------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------
    private bool ValidateRequiredFiles()
    {
        var missingFiles = new List<string>();
        if (!File.Exists(AppPaths.AppsPath)) missingFiles.Add("apps.json");
        if (!File.Exists(AppPaths.IconPath)) missingFiles.Add("rpicon.ico");
        if (missingFiles.Count > 0)
        {
            MessageBox.Show(LanguageManager.Current.ErrorMissingFiles +
                string.Join("\n", missingFiles.Select(f => $"• {f}")) +
                LanguageManager.Current.ErrorFilesLocation + AppPaths.InstallDir,
                LanguageManager.Current.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
        return true;
    }

    public void ShowBalloonTip(string title, string text, ToolTipIcon icon)
    {
        try
        {
            void show()
            {
                trayIcon.BalloonTipTitle = title;
                trayIcon.BalloonTipText = text;
                trayIcon.BalloonTipIcon = icon;
                trayIcon.ShowBalloonTip(2000);
            }
            if (_threadMarshaller.InvokeRequired) _threadMarshaller.BeginInvoke(new Action(show));
            else show();
        }
        catch (Exception ex) { Log($"ShowBalloonTip error: {ex.Message}"); }
    }

    private static void Log(string message, string level = "INFO", string module = "geetRPCS")
    {
        // Delegate to centralized LogService (kept for backward compatibility).
        LogService.Log(message, level, module);
    }

    // ----------------------------------------------------------------
    // Exit
    // ----------------------------------------------------------------
    private void OnExit(object? sender, EventArgs e)
    {
        try
        {
            Log("geetRPCS shutting down...");
            try
            {
                if (_coordinator != null)
                    TelemetryService.ReportShutdownAsync(_coordinator.SessionDuration, _coordinator.AppsUsedCount).Wait(3000);
            }
            catch (Exception ex) { Log($"Shutdown telemetry error: {ex.Message}"); }

            _hkPause?.Dispose();
            _hkPreview?.Dispose();
            _hkReload?.Dispose();
            _hkPrivate?.Dispose();
            _hkStats?.Dispose();

            _updater?.Dispose();
            _trayAnimator?.Stop();
            _trayAnimator?.Dispose();
            _previewForm?.Close();
            _previewForm?.Dispose();

            _coordinator?.SaveStats();
            _coordinator?.Dispose();

            trayIcon?.ContextMenuStrip?.Dispose();
            if (trayIcon != null) trayIcon.Visible = false;
            trayIcon?.Dispose();
            LogService.Shutdown();
            _threadMarshaller?.Dispose();
        }
        catch { }
        finally { Application.Exit(); }
    }
}