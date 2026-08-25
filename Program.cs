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
using geetRPCS.UI.Modern;
using geetRPCS.Utils;

class Program : ApplicationContext, IAppHost, ITrayShell
{
    // --- UI host state ---
    private NotifyIcon trayIcon = null!;
    private readonly Control _threadMarshaller = new Control();
    private PresencePreviewWindow? _previewForm;
    private ManageAppsWindow? _manageAppsWindow;
    private TrayMenuController? _trayMenu;
    private AppCoordinator? _coordinator;
    private UpdateOrchestrator? _updater;
    private TrayIconAnimator? _trayAnimator;
    private GlobalHotkey? _hkPause, _hkPreview, _hkReload, _hkPrivate, _hkStats;
    private UpdateChecker.GitHubRelease? _pendingUpdate;
    // Per-session Manage Apps open stats (logged as a summary at exit) so a
    // still-failing freeze report can be mapped from geetRPCS.log without
    // asking the user to instrument anything.
    private long _openCount, _openTotalMs, _openMaxMs;

    /// <summary>Diagnostic flag (--selftest-manageapps): auto-open/close the
    /// Manage Apps window 3 times through the real open path, then exit
    /// cleanly. Used with an external screen watcher to capture the reported
    /// open-time white flash frame-by-frame.</summary>
    private static bool SelfTestManageApps;

    /// <summary>Diagnostic flag (--selftest-idle): run normally for ~65s (no
    /// windows opened), then exit cleanly. Used with Tests/measure.ps1 to
    /// sample idle RAM/CPU before vs after an optimization pass.</summary>
    private static bool SelfTestIdle;

    private static readonly string IconPath = AppPaths.IconPath;

    // --- Main Entry ---
    #region Main
    [STAThread]
    static void Main(string[] args)
    {
        using (Mutex mutex = new Mutex(true, "geetRPCS-v1-SingleInstance", out bool createdNew))
        {
            if (!createdNew)
            {
                // Pre-WPF path: the WPF host is only initialized below, so this
                // one dialog stays native (initializing the whole ModernWpf
                // stack just to say "already running" is not worth it).
                MessageBox.Show(LanguageManager.Current.ErrorAlreadyRunning, LanguageManager.Current.AppName,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            SelfTestManageApps = args.Length > 0 && args[0] == "--selftest-manageapps";
            SelfTestIdle = args.Length > 0 && args[0] == "--selftest-idle";
            // WPF host for the ModernWpf windows (UI/Modern). Must run before any WPF window opens.
            WpfHost.EnsureInitialized();
            LogService.Initialize();
            // Leave a trace for every unhandled exception. The runtime log showed
            // a process death with zero log lines (right after "Creating
            // PresencePreviewWindow..."): WPF dispatcher exceptions and WinForms
            // UI-thread exceptions never reach the catch below, so they vanished
            // silently. These handlers log before the process goes down.
            System.Windows.Application.Current.DispatcherUnhandledException += (s, e) =>
            {
                try { Log($"Unhandled WPF dispatcher exception: {e.Exception}", "ERROR", "Fatal"); } catch { }
                // Handled stays false: the app is in an unknown state, crash with
                // the evidence written rather than limp on corrupted state.
            };
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException); // route UI-thread exceptions to AppDomain
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                try { Log($"Unhandled exception (terminating={e.IsTerminating}): {e.ExceptionObject}", "ERROR", "Fatal"); } catch { }
            };
            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                // Unobserved task faults are usually benign (racing shutdown
                // paths); record them but do not kill the process.
                try { Log($"Unobserved task exception: {e.Exception}", "WARN", "Fatal"); e.SetObserved(); } catch { }
            };
            try
            {
                Log($"Application started at {DateTime.Now}", "INFO", "Startup");
                Log($"App folder: {AppPaths.InstallDir}", "INFO", "Startup");
                PInvoke.User32.ShowWindow(PInvoke.User32.GetConsoleWindow(), PInvoke.User32.SW_HIDE);
                // Warm the WPF stack (templates, layout, fonts, composition) now so the
                // first real window opens instantly instead of paying the one-time cost
                // when the user clicks its tray item (reported few-ms freeze on the
                // first Manage Apps open).
                WpfHost.PreWarm();
                Application.Run(new Program());
            }
            catch (Exception ex)
            {
                Log($"Fatal error: {ex.Message}", "ERROR", "Fatal");
                MessageDialog.ShowError(string.Format(LanguageManager.Current.ErrorStartupFatal, ex.Message),
                    LanguageManager.Current.DialogFatalTitle);
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
                MessageDialog.ShowError(LanguageManager.Current.ErrorUnableLoadConfig, LanguageManager.Current.DialogErrorTitle);
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

            // Pre-create + pre-show the ManageAppsWindow (hidden, off-screen) so the
            // first real open skips the one-time ~100-200ms cost (BAML parse, ModernWpf
            // templates, window layout + first composition) that PreWarm's small
            // control set does not cover. The window is reused for the first open,
            // then rebuilt after each close (BAML/icon caches keep later opens cheap).
            try { PreCreateManageAppsWindow(); } catch (Exception ex) { Log($"ManageAppsWindow pre-create failed: {ex.Message}", "WARN", "ManageApps"); }

            if (SelfTestManageApps) StartManageAppsSelfTest();
            if (SelfTestIdle)
            {
                var idleTimer = new System.Windows.Forms.Timer { Interval = 65000 };
                idleTimer.Tick += (s2, e2) =>
                {
                    idleTimer.Stop();
                    Log("SELFTEST idle period complete, exiting", "INFO", "SelfTest");
                    ExitApp();
                };
                idleTimer.Start();
            }

            Log("geetRPCS initialized successfully!");
            // Off the UI thread: TrimMemory now runs a blocking Gen2 collection
            // (all call sites are background), and the startup path must not
            // pay that on the UI thread.
            Task.Run(() => MemoryHelper.TrimMemory());
        }
        catch (Exception ex)
        {
            Log($"INIT ERROR: {ex}");
            MessageDialog.ShowError(string.Format(LanguageManager.Current.ErrorStartupFatal, ex.Message),
                LanguageManager.Current.DialogErrorTitle);
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
        if (_previewForm != null && _previewForm.IsVisible) _previewForm.UpdatePresence(presence);
    }
    public void PreviewPausedState()
    {
        if (_previewForm != null && _previewForm.IsVisible) _previewForm.SetPausedState();
    }
    public void PreviewIdleState()
    {
        if (_previewForm != null && _previewForm.IsVisible) _previewForm.SetIdleState();
    }
    public void RefreshTrayPresentation() => _trayMenu?.UpdatePresentation();
    public void RebuildTrayMenu()
    {
        if (_threadMarshaller.InvokeRequired) { _threadMarshaller.BeginInvoke(new Action(RebuildTrayMenu)); return; }
        _trayMenu?.Rebuild();
    }

    /// <summary>Rebuilds the tray menu on the UI thread AFTER the current menu
    /// interaction has unwound. Disposing the ContextMenuStrip from inside its
    /// own click handler would crash, so theme-mode switches and OS light/dark
    /// changes defer the rebuild until the menu is closed. The rebuild also
    /// re-renders the menu-item glyph images with the now-active theme color.</summary>
    public void RebuildTrayMenuDeferred()
    {
        _threadMarshaller.BeginInvoke(new Action(RebuildTrayMenu));
    }
    public void AnimateOnSwitch() => _trayAnimator?.AnimateOnSwitch();

    // ----------------------------------------------------------------
    // Shell actions consumed by TrayMenuController
    // ----------------------------------------------------------------
    public bool IsPreviewVisible => _previewForm != null && _previewForm.IsVisible;
    // IsVisible (not IsLoaded): the startup pre-created window is Loaded=true
    // while hidden, and the checkmark must only show while the dialog is open.
    public bool IsManageAppsOpen => _manageAppsWindow != null && _manageAppsWindow.IsVisible;
    public bool IsStatsOpen => StatisticsWindow.Instance != null && StatisticsWindow.Instance.IsVisible;
    public void TogglePreviewVisibility()
    {
        if (_previewForm == null || !_previewForm.IsLoaded)
        {
            Log("Creating PresencePreviewWindow...", "INFO", "Preview");
            InitPreviewForm();
            _previewForm!.Show();
            _previewForm.Activate();
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
            Log("Destroying PresencePreviewWindow to save RAM...", "INFO", "Preview");
            _previewForm.Close();
            _previewForm = null;
            // Defer the trim off the UI thread: a full blocking Gen2 GC here
            // hitches the tray/hotkey path that just toggled the preview.
            Task.Run(() => MemoryHelper.TrimMemory());
        }
    }

    /// <summary>Builds a ManageAppsWindow with the real coordinator callbacks and
    /// wires the hide/cleanup handler (checkmark off, data drop, deferred
    /// working-set trim). The window is REUSED for the whole session: it hides
    /// instead of closing (see its Closing handler), so every open after the
    /// first skips the ctor AND the first DWM present of a fresh surface, which
    /// was the reported few-ms system hitch + unrendered white frame at open.</summary>
    private ManageAppsWindow CreateManageAppsWindow()
    {
        // Assigned during startup before the first pre-create; the null-forgiving
        // access documents that this method only runs after initialization.
        var coordinator = _coordinator!;
        var win = new ManageAppsWindow(
            AppConfigManager.Apps,
            new HashSet<string>(coordinator.DisabledApps, StringComparer.OrdinalIgnoreCase),
            coordinator.Overrides,
            CustomProcessSet(),
            async (proc, enabled) =>
            {
                coordinator.SetAppDisabled(proc, enabled);
                await coordinator.SaveSettingsAsync();
            },
            async (proc, ov) =>
            {
                coordinator.SetAppOverride(proc, ov);
                await coordinator.SaveSettingsAsync();
            },
            app => coordinator.AddCustomApp(app),
            proc => coordinator.RemoveCustomApp(proc));
        // Add/remove of custom apps changed the merged app list: push fresh
        // data back into the still-open window.
        win.DataReloadRequested += (s, e) => ReloadManageAppsData(win);
        win.IsVisibleChanged += (s, e) =>
        {
            bool visible = win.IsVisible;
            TrayMenuController.SetToggleState(_trayMenu?.ManageAppsMenuItem, visible);
            if (visible || win.IsPreShow) return; // IsPreShow: the startup warm-up show
            // Dialog closed (Esc / title-bar X): release the row working set
            // off-thread. The next open is a fresh window (native DWM open
            // animation), so nothing needs to be kept warm here.
            Task.Run(async () => { await Task.Delay(500); MemoryHelper.TrimMemory(); });
        };
        return win;
    }

    private static HashSet<string> CustomProcessSet()
        => new HashSet<string>(
            SettingsService.Instance.CustomApps.Select(c => c.Process).Where(p => !string.IsNullOrEmpty(p)),
            StringComparer.OrdinalIgnoreCase);

    private void ReloadManageAppsData(ManageAppsWindow win)
    {
        var coordinator = _coordinator!;
        win.RefreshData(
            AppConfigManager.Apps,
            new HashSet<string>(coordinator.DisabledApps, StringComparer.OrdinalIgnoreCase),
            coordinator.Overrides,
            CustomProcessSet());
    }

    /// <summary>Startup warm-up ONLY: pays the one-time costs of the
    /// ManageAppsWindow type once (BAML parse, ModernWpf template load, icon
    /// decode, first layout incl. row realization) off-screen, then CLOSES the
    /// warm-up instance. Every real open creates a FRESH window, exactly like
    /// tray dialog: a newly shown HWND is what gets the native Win10/11
    /// DWM open animation, and with the caches warm a fresh create costs only
    /// tens of ms (measured "loaded in" ~50-90ms), hidden under the animation.
    /// Anti-flash is independent of this: the window suppresses
    /// WM_ERASEBKGND and its ctor forces layout while still hidden.</summary>
    private void PreCreateManageAppsWindow()
    {
        var win = CreateManageAppsWindow();
        win.WindowStartupLocation = System.Windows.WindowStartupLocation.Manual;
        win.Left = -32000;
        win.Top = -32000;
        win.IsPreShow = true;
        win.Show();   // real HWND + first layout + row realization, off-screen
        win.Close();  // warm-up instance is never shown to the user
    }

    /// <summary>Self-test: 3 real opens (1.5s apart, each closed after 1.2s by a
    /// closer timer ticking inside the modal loop) through the exact
    /// ToggleManageAppsVisibility path, then a clean exit. Pair with an
    /// external screen watcher to analyze the open frames. Opens go through
    /// the REAL tray context menu (shown, item perform-clicked, menu closing
    /// concurrently with the dialog) to mirror the manual user flow.</summary>
    private void StartManageAppsSelfTest()
    {
        int round = 0;
        var t = new System.Windows.Forms.Timer { Interval = 1500 };
        t.Tick += (s, e) =>
        {
            round++;
            if (round > 3)
            {
                t.Stop();
                Log($"SELFTEST complete ({round - 1} opens), exiting", "INFO", "SelfTest");
                ExitApp();
                return;
            }
            Log($"SELFTEST open round {round} via tray menu at {Cursor.Position}", "INFO", "SelfTest");
            var menu = trayIcon.ContextMenuStrip;
            var item = _trayMenu?.ManageAppsMenuItem;
            var closer = new System.Windows.Threading.DispatcherTimer
            { Interval = TimeSpan.FromMilliseconds(1200) };
            closer.Tick += (s2, e2) =>
            {
                closer.Stop();
                try { _manageAppsWindow?.Close(); } catch { }
            };
            closer.Start();
            // Close the menu right after the click starts processing, so the
            // menu teardown overlaps the window open exactly like a real click.
            _threadMarshaller.BeginInvoke(new Action(() => { try { menu?.Close(); } catch { } }));
            if (menu != null && item != null)
            {
                menu.Show(Cursor.Position);
                item.PerformClick(); // handler blocks in ShowDialog
            }
            else
            {
                ToggleManageAppsVisibility();
            }
        };
        t.Start();
    }

    public void ToggleManageAppsVisibility()
    {
        if (_coordinator == null) return;
        if (_manageAppsWindow == null || !_manageAppsWindow.IsVisible)
        {
            var openSw = System.Diagnostics.Stopwatch.StartNew();
            Log("Opening ManageAppsWindow...", "INFO", "ManageApps");
            // FRESH window every open, exactly like the other tray dialogs: a newly
            // shown HWND is what gets the native Win10/11 DWM open animation
            // (zoom + fade); a hidden-then-reshown window does not reliably
            // re-play it. The startup warm-up already paid the one-time BAML /
            // template / icon costs, so this create is tens of ms. The ctor's
            // RefreshData loads the current config and forces layout while the
            // window is still hidden (no pending render at show), and the window
            // suppresses WM_ERASEBKGND (no OS white pre-paint fill).
            var win = CreateManageAppsWindow();
            _manageAppsWindow = win;
            long phaseRefresh = openSw.ElapsedMilliseconds;
            // Center on the work area for the show and arm Topmost.
            win.PrepareForShow();
            var wa = System.Windows.SystemParameters.WorkArea;
            win.Left = wa.Left + (wa.Width - win.Width) / 2;
            win.Top = wa.Top + (wa.Height - win.Height) / 2;
            // Measure the perceived open (Opening log -> first visible) with a
            // phase breakdown, so a still-failing report pinpoints the exact cost.
            // The window's own IsVisibleChanged log gives create->visible; the
            // difference is the create/position phase above.
            System.Windows.DependencyPropertyChangedEventHandler? visibleHandler = null;
            visibleHandler = (s, e) =>
            {
                if (win.IsVisible)
                {
                    win.IsVisibleChanged -= visibleHandler;
                    long ms = openSw.ElapsedMilliseconds;
                    LogService.Log(
                        $"ManageAppsWindow open-to-visible {ms}ms (create+position {phaseRefresh}ms)",
                        "INFO", "ManageApps");
                    _openCount++; _openTotalMs += ms; if (ms > _openMaxMs) _openMaxMs = ms;
                }
            };
            win.IsVisibleChanged += visibleHandler;
            // Modal ShowDialog (same as the other tray dialogs): the modal Win32 loop
            // forces activation regardless of the OS foreground lock, so the
            // search box reliably receives keyboard input. Modeless Show() failed
            // repeatedly in the real tray-menu scenario (keystrokes went elsewhere
            // even though logical focus looked correct).
            TrayMenuController.SetToggleState(_trayMenu?.ManageAppsMenuItem, true);
            win.ShowDialog();
            // Dialog really closed (fresh-window lifecycle): release the field.
            // The checkmark-off + deferred trim run from the IsVisibleChanged
            // handler wired in CreateManageAppsWindow.
            _manageAppsWindow = null;
        }
        else
        {
            _manageAppsWindow.Activate();
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
            else MessageDialog.ShowInfo(LanguageManager.Current.DialogLogNotCreated, LanguageManager.Current.AppName);
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
            // Rebuild the tray menu when the OS light/dark preference flips while the
            // app follows the system theme: the glyph images are baked bitmaps, so
            // they must be re-rendered with the new theme color (the menu background
            // itself follows live via FluentMenuRenderer/ThemePalette at paint time).
            _lastSystemLightTheme = SystemUsesLightTheme();
            Microsoft.Win32.SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
            // Live-update the Statistics tray-item checkmark as the shared window opens/closes.
            StatisticsWindow.IsOpenChanged += isOpen => TrayMenuController.SetToggleState(_trayMenu?.StatisticsMenuItem, isOpen);
            _trayAnimator = new TrayIconAnimator(trayIcon, IconPath, _threadMarshaller, (msg) => Log(msg, "DEBUG", "TrayIconAnimator"));
            _coordinator!.AttachTrayAnimator(_trayAnimator);
            Log("Tray icon setup completed", "INFO", "TrayIcon");
            return true;
        }
        catch (Exception ex)
        {
            Log($"Failed to setup tray icon: {ex.Message}", "ERROR", "TrayIcon");
            MessageDialog.ShowError(LanguageManager.Current.ErrorOpenFile + ex.Message,
                LanguageManager.Current.AppName);
            return false;
        }
    }

    private void InitPreviewForm()
    {
        string appId = _coordinator!.Config.Discord?.ApplicationId ?? "";
        _previewForm = new PresencePreviewWindow(appId);
        _previewForm.Closed += (sender, e) =>
        {
            TrayMenuController.SetToggleState(_trayMenu?.PreviewMenuItem, false);
            _previewForm = null; // WPF windows cannot be re-shown after Close
            Task.Run(async () => { await Task.Delay(500); MemoryHelper.TrimMemory(); });
        };
        _previewForm.IsVisibleChanged += (sender, e) =>
        {
            TrayMenuController.SetToggleState(_trayMenu?.PreviewMenuItem, _previewForm?.IsVisible == true);
            // Deferred: GC.Collect(2) on the UI thread would hitch the toggle.
            if (_previewForm != null && !_previewForm.IsVisible) Task.Run(() => MemoryHelper.TrimMemory());
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
            MessageDialog.ShowError(LanguageManager.Current.ErrorMissingFiles +
                string.Join("\n", missingFiles.Select(f => $"• {f}")) +
                LanguageManager.Current.ErrorFilesLocation + AppPaths.InstallDir,
                LanguageManager.Current.AppName);
            return false;
        }
        return true;
    }

    // ----------------------------------------------------------------
    // OS theme-following (System mode): rebuild the tray menu so the baked
    // glyph images keep matching the actual Windows light/dark theme.
    // ----------------------------------------------------------------
    private bool _lastSystemLightTheme;

    private void OnUserPreferenceChanged(object sender, Microsoft.Win32.UserPreferenceChangedEventArgs e)
    {
        try
        {
            // WM_SETTINGCHANGE with Category.General covers the light/dark switch;
            // other General changes (mouse speed, accents, ...) are filtered out by
            // comparing the actual registry value.
            if (e.Category != Microsoft.Win32.UserPreferenceCategory.General) return;
            bool light = SystemUsesLightTheme();
            if (light == _lastSystemLightTheme) return;
            _lastSystemLightTheme = light;
            // Forced Dark/Light modes keep their own colors; only System follows the OS.
            if (SettingsService.Instance.ThemeMode != "System") return;
            RebuildTrayMenuDeferred();
        }
        catch (Exception ex) { Log($"UserPreferenceChanged handler error: {ex.Message}", "ERROR", "Theme"); }
    }

    private static bool SystemUsesLightTheme()
    {
        try
        {
            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                       @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
            {
                if (key?.GetValue("SystemUsesLightTheme") is int v) return v != 0;
            }
        }
        catch (Exception ex) { Log($"SystemUsesLightTheme error: {ex.Message}", "ERROR", "Theme"); }
        return false; // default: dark taskbar
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
            if (_openCount > 0)
                LogService.Log(
                    $"ManageApps open summary: {_openCount} opens, avg {_openTotalMs / _openCount}ms, max {_openMaxMs}ms",
                    "INFO", "ManageApps");
            Microsoft.Win32.SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
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
            // The ManageApps dialog may still be referenced if exit was
            // triggered while it was open; closing is real (fresh per open).
            try { _manageAppsWindow?.Close(); } catch { }

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