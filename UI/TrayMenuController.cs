/**
 * geetRPCS - Tray Menu Controller
 * Builds and refreshes the system-tray context menu. UI-only logic that used to
 * live inside Program.cs; commands are forwarded to the AppCoordinator and shell.
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
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Encodings.Web;
using System.Windows.Forms;
using geetRPCS.Models;
using geetRPCS.Services;
using geetRPCS.UI.Modern;
using geetRPCS.Utils;

namespace geetRPCS.UI
{
    internal sealed class TrayMenuController
    {
        private readonly NotifyIcon _trayIcon;
        private readonly ITrayCoordinator _coordinator;
        private readonly ITrayShell _shell;
        private const int BALLOON_TIMEOUT_MS = 2000;

        // Menu item references updated in place (instead of full rebuilds).
        public ToolStripMenuItem PauseItem { get; private set; }
        public ToolStripMenuItem PrivateModeItem { get; private set; }
        public ToolStripMenuItem PreviewMenuItem { get; private set; }
        public ToolStripMenuItem MouseEnergyItem { get; private set; }
        public ToolStripMenuItem TrayAnimationItem { get; private set; }
        public ToolStripMenuItem ManageAppsMenuItem { get; private set; }
        public ToolStripMenuItem StatisticsMenuItem { get; private set; }
        public ToolStripMenuItem ThemeMenuItem { get; private set; }

        public TrayMenuController(NotifyIcon trayIcon, ITrayCoordinator coordinator, ITrayShell shell)
        {
            _trayIcon = trayIcon ?? throw new ArgumentNullException(nameof(trayIcon));
            _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            _shell = shell ?? throw new ArgumentNullException(nameof(shell));
        }

        /// <summary>Fully rebuilds the context menu (language change, reload, ...).</summary>
        public void Rebuild()
        {
            try
            {
                if (_trayIcon.ContextMenuStrip != null)
                {
                    DisposeMenuImages(_trayIcon.ContextMenuStrip);
                    _trayIcon.ContextMenuStrip.Dispose();
                }
                var menu = new ContextMenuStrip { Renderer = new FluentMenuRenderer() };

                PauseItem = CreateMenuItem(_coordinator.IsPaused ? LanguageManager.Current.MenuResume : LanguageManager.Current.MenuPause,
                    _coordinator.IsPaused ? FluentGlyphs.Play : FluentGlyphs.Pause, isChecked: _coordinator.IsPaused);
                PauseItem.Click += (_, __) => _coordinator.TogglePause();
                menu.Items.Add(PauseItem);

                PrivateModeItem = CreateMenuItem(LanguageManager.Current.MenuPrivateMode, FluentGlyphs.Lock, isChecked: _coordinator.PrivateMode);
                PrivateModeItem.Click += (_, __) => _coordinator.TogglePrivateMode();
                menu.Items.Add(PrivateModeItem);

                MouseEnergyItem = CreateMenuItem(LanguageManager.Current.MenuMouseEnergy, FluentGlyphs.Mouse,
                    isChecked: SettingsService.Instance.MouseEnergyEnabled);
                MouseEnergyItem.Click += async (_, __) =>
                {
                    bool newState = !SettingsService.Instance.MouseEnergyEnabled;
                    SetToggleState(MouseEnergyItem, newState);
                    await _coordinator.SetMouseEnergyAsync(newState);
                };
                menu.Items.Add(MouseEnergyItem);

                TrayAnimationItem = CreateMenuItem(LanguageManager.Current.MenuTrayAnimation, FluentGlyphs.Palette,
                    isChecked: SettingsService.Instance.TrayAnimationEnabled);
                TrayAnimationItem.Click += async (_, __) =>
                {
                    bool newState = !SettingsService.Instance.TrayAnimationEnabled;
                    SetToggleState(TrayAnimationItem, newState);
                    await _coordinator.SetTrayAnimationAsync(newState);
                };
                menu.Items.Add(TrayAnimationItem);

                // Theme (Dark / Light / System) - applies live to the ModernWpf windows.
                // The item text shows the active mode as a suffix (e.g. "🌗 Theme: Dark")
                // so the current theme is visible without opening the submenu.
                string themeMode = SettingsService.Instance.ThemeMode;
                ThemeMenuItem = CreateMenuItem(GetThemeMenuText(themeMode), FluentGlyphs.Moon);
                var themeMenu = ThemeMenuItem;
            var themeSystemItem = new ToolStripMenuItem(EscapeMnemonics(LanguageManager.Current.MenuThemeSystem ?? "System"))
            { Padding = MenuItemPadding };
            var themeDarkItem = new ToolStripMenuItem(EscapeMnemonics(LanguageManager.Current.MenuThemeDark ?? "Dark"))
            { Padding = MenuItemPadding };
            var themeLightItem = new ToolStripMenuItem(EscapeMnemonics(LanguageManager.Current.MenuThemeLight ?? "Light"))
            { Padding = MenuItemPadding };
                SetSubmenuSelection(themeSystemItem, themeMode != "Dark" && themeMode != "Light");
                SetSubmenuSelection(themeDarkItem, themeMode == "Dark");
                SetSubmenuSelection(themeLightItem, themeMode == "Light");
                themeSystemItem.Click += (_, __) => SetThemeMode("System", themeSystemItem, themeDarkItem, themeLightItem);
                themeDarkItem.Click += (_, __) => SetThemeMode("Dark", themeSystemItem, themeDarkItem, themeLightItem);
                themeLightItem.Click += (_, __) => SetThemeMode("Light", themeSystemItem, themeDarkItem, themeLightItem);
                themeMenu.DropDownItems.Add(themeSystemItem);
                themeMenu.DropDownItems.Add(themeDarkItem);
                themeMenu.DropDownItems.Add(themeLightItem);
                menu.Items.Add(themeMenu);

                var telemetryItem = CreateMenuItem(LanguageManager.Current.MenuTelemetry, FluentGlyphs.Send,
                    isChecked: TelemetryService.IsEnabled());
                telemetryItem.Click += async (s, args) =>
                {
                    bool newState = !TelemetryService.IsEnabled();
                    await _coordinator.ToggleTelemetryAsync(newState);
                    SetToggleState((ToolStripMenuItem)s!, newState);
                };
                menu.Items.Add(telemetryItem);

                // Auto-Update toggle
                var autoUpdateItem = CreateMenuItem(LanguageManager.Current.MenuAutoUpdate ?? "🔄 Auto-Update", FluentGlyphs.UpdateRestore,
                    isChecked: SettingsService.Instance.AutoUpdateEnabled);
                autoUpdateItem.Click += async (s, args) =>
                {
                    bool newState = !SettingsService.Instance.AutoUpdateEnabled;
                    SettingsService.Instance.AutoUpdateEnabled = newState;
                    await SettingsService.SaveAsync();
                    SetToggleState((ToolStripMenuItem)s!, newState);
                    _shell.ShowBalloonTip(LanguageManager.Current.AppName,
                        newState ? (LanguageManager.Current.MsgAutoUpdateEnabled ?? "Auto-update enabled. App will update automatically.")
                                 : (LanguageManager.Current.MsgAutoUpdateDisabled ?? "Auto-update disabled. You'll be notified about updates."),
                        ToolTipIcon.Info);
                    LogService.Log($"Auto-update {(newState ? "enabled" : "disabled")}", "INFO", "TrayMenu");
                };
                menu.Items.Add(autoUpdateItem);
                menu.Items.Add(new ToolStripSeparator());

                // Checked shows the Manage Apps window is open (it is modal now,
                // so clicking the item again just activates the open window).
                ManageAppsMenuItem = CreateMenuItem(LanguageManager.Current.MenuManageApps, FluentGlyphs.Settings,
                    isChecked: _shell.IsManageAppsOpen);
                var manageAppsItem = ManageAppsMenuItem;
                manageAppsItem.Click += (_, __) =>
                {
                    // Open the window IMMEDIATELY, before the tray menu finishes
                    // closing (same as the other modal tray dialogs): the modal
                    // ShowDialog forces activation, so the menu's close no longer
                    // steals focus. The old menu.BeginInvoke deferral waited for
                    // the menu to fully close (~150-250ms), making the click feel
                    // laggy.
                    _shell.ToggleManageAppsVisibility();
                };
                menu.Items.Add(manageAppsItem);

                // Custom Rich Presence: the one-stop GUI for building your own
                // presence — idle/active texts, timestamps, buttons and (advanced)
                // your own Discord Application ID — so users never need to
                // hand-edit JSON or open a text editor. Absorbs the old Change
                // Application ID dialog. Same immediate-open pattern: the modal
                // ShowDialog forces activation, so the menu's close no longer
                // steals focus.
                var customPresenceItem = CreateMenuItem(LanguageManager.Current.MenuDefaultPresence, FluentGlyphs.Chat,
                    (_, __) =>
                    {
                        var dlg = new CustomRichPresenceWindow(_coordinator.Config);
                        if (dlg.ShowDialog() == true && dlg.Result != null)
                        {
                            if (_coordinator.SaveConfig(dlg.Result))
                                MessageDialog.ShowInfo(LanguageManager.Current.MsgPresenceSaved ?? "Custom Rich Presence saved.",
                                    LanguageManager.Current.AppName);
                            else
                                MessageDialog.ShowError(LanguageManager.Current.ErrorSaveConfig ?? "Failed to save config.",
                                    LanguageManager.Current.AppName);
                        }
                        System.Threading.Tasks.Task.Run(async () =>
                        { await System.Threading.Tasks.Task.Delay(500); MemoryHelper.TrimMemory(); });
                    });
                menu.Items.Add(customPresenceItem);
                menu.Items.Add(new ToolStripSeparator());

                AddStatisticsMenu(menu);

                PreviewMenuItem = CreateMenuItem(LanguageManager.Current.MenuPreviewWindow, FluentGlyphs.View,
                    isChecked: _shell.IsPreviewVisible);
                PreviewMenuItem.Click += (_, __) =>
                {
                    // Defer until the tray menu has fully closed (same pattern as
                    // Manage Apps) so the window can take foreground cleanly. A
                    // theme switch / OS theme flip can Rebuild() (and dispose this
                    // menu) before the queued delegate runs; nothing below may
                    // touch the disposed menu.
                    menu.BeginInvoke(new Action(() =>
                    {
                        if (menu.IsDisposed) return;
                        _shell.TogglePreviewVisibility();
                    }));
                };
                menu.Items.Add(PreviewMenuItem);
                menu.Items.Add(new ToolStripSeparator());

                var startupItem = CreateMenuItem(LanguageManager.Current.MenuStartup, FluentGlyphs.Flag,
                    isChecked: StartupTask.IsEnabled());
                startupItem.Click += (_, __) =>
                {
                    try
                    {
                        bool wasOn = StartupTask.IsEnabled();
                        StartupTask.Enable(!wasOn);
                        SetToggleState(startupItem, !wasOn);
                    }
                    catch (Exception ex)
                    {
                        LogService.Log($"Startup toggle error: {ex.Message}", "ERROR", "TrayMenu");
                        MessageDialog.ShowError(LanguageManager.Current.ErrorStartupToggle + ex.Message,
                            LanguageManager.Current.AppName);
                    }
                };
                menu.Items.Add(startupItem);
                AddQuickActionsMenu(menu);
                menu.Items.Add(new ToolStripSeparator());
                AddLanguageMenu(menu);
                // Help & Guide: built-in readme (how the app works, customization,
                // troubleshooting) so users never need to open GitHub or the
                // install folder for the basics.
                menu.Items.Add(CreateMenuItem(LanguageManager.Current.MenuHelp, FluentGlyphs.Help,
                    (_, __) =>
                    {
                        var guide = new GuideWindow();
                        guide.ShowDialog();
                        System.Threading.Tasks.Task.Run(async () =>
                        { await System.Threading.Tasks.Task.Delay(500); MemoryHelper.TrimMemory(); });
                    }));
                menu.Items.Add(CreateMenuItem(LanguageManager.Current.MenuCheckUpdates, FluentGlyphs.Refresh, (_, __) => _shell.CheckForUpdatesFromMenu()));
                menu.Items.Add(CreateMenuItem(LanguageManager.Current.MenuOpenLog, FluentGlyphs.Document, (_, __) => _shell.OpenLog()));
                menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add(CreateMenuItem(LanguageManager.Current.MenuExit, FluentGlyphs.Power, (_, __) => _shell.ExitApp()));

                _trayIcon.ContextMenuStrip = menu;
                PinImageMargins(menu);
                UpdateTrayText();
                LogService.Log("Tray menu updated", "INFO", "TrayMenu");
            }
            catch (Exception ex) { LogService.Log($"Failed to update tray menu: {ex}", "ERROR", "TrayMenu"); }
        }

        /// <summary>Applies a theme mode (Dark/Light/System) live, persists it and re-checks the menu items.</summary>
        private async void SetThemeMode(string mode, ToolStripMenuItem systemItem, ToolStripMenuItem darkItem, ToolStripMenuItem lightItem)
        {
            SettingsService.Instance.ThemeMode = mode;
            WpfHost.ApplyThemeMode(mode);
            SetSubmenuSelection(systemItem, mode == "System");
            SetSubmenuSelection(darkItem, mode == "Dark");
            SetSubmenuSelection(lightItem, mode == "Light");
            if (ThemeMenuItem != null) ThemeMenuItem.Text = GetThemeMenuText(mode);
            await SettingsService.SaveAsync();
            // Rebuild AFTER the theme click unwinds: the menu-item glyph images are
            // baked bitmaps carrying the previous theme's color, and disposing the
            // ContextMenuStrip from inside its own click handler would crash. The
            // deferred rebuild re-renders every icon with the new theme's color.
            _shell.RebuildTrayMenuDeferred();
            LogService.Log($"Theme mode set to {mode}", "INFO", "TrayMenu");
        }

        /// <summary>Menu text for the Theme item including the active mode, e.g. "🌗 Theme: Dark".</summary>
        internal static string GetThemeMenuText(string mode)
        {
            string label = mode == "Dark" ? (LanguageManager.Current.MenuThemeDark ?? "Dark")
                         : mode == "Light" ? (LanguageManager.Current.MenuThemeLight ?? "Light")
                         : (LanguageManager.Current.MenuThemeSystem ?? "System");
            return EscapeMnemonics($"{FluentGlyphs.StripLeadingEmoji(LanguageManager.Current.MenuTheme ?? "🌗 Theme")}: {label}");
        }

        /// <summary>Compact Fluent menu item padding: 8px vertical total (≈24px tall
        /// items) and 8px horizontal — keeps the tray menu tight so it doesn't
        /// stretch too tall.</summary>
        internal static readonly Padding MenuItemPadding = new Padding(8, 4, 8, 4);

        /// <summary>WinForms menu text treats "&" as the mnemonic prefix — the
        /// next character becomes an (invisible) access key and the ampersand
        /// itself is swallowed, which rendered "Help & Guide" as "Help  Guide".
        /// Doubling the ampersand is the WinForms idiom for a literal "&".</summary>
        internal static string EscapeMnemonics(string text)
            => string.IsNullOrEmpty(text) ? text : text.Replace("&", "&&");

        /// <summary>Builds a menu item with the emoji prefix stripped from the
        /// localized text and replaced by a monochrome Segoe Fluent glyph image.
        /// WinForms menu items can't mix fonts in one string, and PUA glyphs get
        /// no GDI font-fallback — so the glyph is drawn into the item's Image.
        /// Toggle items (isChecked) render the glyph in the theme ACCENT color as
        /// their ON indicator INSTEAD of the Checked property: .NET 8 WinForms
        /// renders Checked+Image as a hardcoded OS-accent blue square scaled over
        /// the icon, which no renderer override can stop (the OnRenderItemCheck
        /// path isn't even called) and which clashes with the Fluent theme.
        /// Items WITHOUT an image keep Checked (clean accent checkmark).</summary>
        private static ToolStripMenuItem CreateMenuItem(string localizedText, string glyph, EventHandler onClick = null, bool isChecked = false)
        {
            var item = new ToolStripMenuItem(EscapeMnemonics(FluentGlyphs.StripLeadingEmoji(localizedText)))
            {
                // Glyph color follows the active theme (resolved at render time),
                // not a fixed gray — matches the menu text/background contrast. ON
                // state uses AccentGlyph (accent contrast-adjusted for the theme bg).
                Image = FluentGlyphs.CreateMenuGlyph(glyph, isChecked ? ThemePalette.AccentGlyph : ThemePalette.TextSecondary),
                Padding = MenuItemPadding,
                Tag = glyph
            };
            if (onClick != null) item.Click += onClick;
            return item;
        }

        /// <summary>Refreshes an image-based toggle item's ON state: the glyph is
        /// re-rendered in the contrast-safe accent color when on, secondary gray
        /// when off. Replaces setting Checked on items that carry an image (see
        /// CreateMenuItem for why Checked+Image is avoided). The glyph codepoint is
        /// read from item.Tag (set by CreateMenuItem).</summary>
        public static void SetToggleState(ToolStripMenuItem item, bool on)
        {
            if (item == null) return;
            string glyph = item.Tag as string;
            if (string.IsNullOrEmpty(glyph)) return;
            var old = item.Image;
            item.Image = FluentGlyphs.CreateMenuGlyph(glyph, on ? ThemePalette.AccentGlyph : ThemePalette.TextSecondary);
            old?.Dispose();
        }

        /// <summary>Selection indicator for radio-style submenu items (theme mode,
        /// language, shortcuts): an accent check glyph when selected, a transparent
        /// 16px placeholder otherwise. Same 16px accent rendering as the top-level
        /// toggle glyphs, so the whole menu uses one visual language instead of
        /// WinForms' internal checkmark (which also renders inconsistently under
        /// .NET 8). Unselected rows must still carry an Image: WinForms collapses
        /// the dropdown's image margin to zero width when no visible item has one,
        /// which shifts the submenu's text column off the main menu's alignment.</summary>
        internal static void SetSubmenuSelection(ToolStripMenuItem item, bool selected)
        {
            if (item == null) return;
            var old = item.Image;
            item.Image = selected ? FluentGlyphs.CreateMenuGlyph(FluentGlyphs.CheckMark, ThemePalette.AccentGlyph)
                                  : new System.Drawing.Bitmap(16, 16);
            old?.Dispose();
        }

        /// <summary>WinForms only allocates a dropdown's image margin when at least
        /// one visible item has an Image (see SetSubmenuSelection's transparent
        /// placeholder). Pinning ShowImageMargin here additionally keeps the gutter
        /// alive for submenus whose items never carry images, so every column
        /// stays flush with the main menu.</summary>
        private static void PinImageMargins(ToolStrip strip)
        {
            foreach (ToolStripItem item in strip.Items)
            {
                if (item is ToolStripMenuItem mi && mi.HasDropDownItems)
                {
                    if (mi.DropDown is ToolStripDropDownMenu dd) dd.ShowImageMargin = true;
                    PinImageMargins(mi.DropDown);
                }
            }
        }

        /// <summary>Menu glyphs are rendered bitmaps; the strip's Dispose does not
        /// release them, so they are disposed explicitly before the strip goes.</summary>
        private static void DisposeMenuImages(ContextMenuStrip menu)
        {
            foreach (ToolStripItem item in menu.Items)
            {
                if (item is ToolStripMenuItem mi && mi.Image != null)
                {
                    mi.Image.Dispose();
                    mi.Image = null;
                }
            }
        }

        /// <summary>Refreshes pause/private check state and the tray tooltip text (no full rebuild).</summary>
        public void UpdatePresentation()
        {
            try
            {
                if (PauseItem != null)
                {
                    // The pause item swaps its glyph (Play when paused so you can
                    // resume, Pause otherwise) AND its ON color (accent = paused).
                    string pauseGlyph = _coordinator.IsPaused ? FluentGlyphs.Play : FluentGlyphs.Pause;
                    PauseItem.Text = EscapeMnemonics(FluentGlyphs.StripLeadingEmoji(
                        _coordinator.IsPaused ? LanguageManager.Current.MenuResume : LanguageManager.Current.MenuPause));
                    PauseItem.Tag = pauseGlyph;
                    SetToggleState(PauseItem, _coordinator.IsPaused);
                }
                if (PrivateModeItem != null) SetToggleState(PrivateModeItem, _coordinator.PrivateMode);
                if (MouseEnergyItem != null) SetToggleState(MouseEnergyItem, SettingsService.Instance.MouseEnergyEnabled);
                if (TrayAnimationItem != null) SetToggleState(TrayAnimationItem, SettingsService.Instance.TrayAnimationEnabled);
                UpdateTrayText();
            }
            catch (Exception ex) { LogService.Log($"UpdateTrayPresentation error: {ex.Message}", "ERROR", "TrayMenu"); }
        }

        private void UpdateTrayText()
        {
            string status = LanguageManager.Current.AppName;
            if (_coordinator.IsPaused) status += LanguageManager.Current.TrayPaused;
            else if (_coordinator.PrivateMode) status += LanguageManager.Current.TrayPrivate;
            _trayIcon.Text = status;
        }

        #region ----- Sub menus -----
        private void AddStatisticsMenu(ContextMenuStrip menu)
        {
            // Checked while the shared statistics window is open (live-updated via
            // Program's StatisticsWindow.IsOpenChanged subscription).
            var statsMenu = CreateMenuItem(LanguageManager.Current.MenuStatistics, FluentGlyphs.Chart,
                isChecked: _shell.IsStatsOpen);
            StatisticsMenuItem = statsMenu;
            statsMenu.DropDownItems.Add(CreateMenuItem(LanguageManager.Current.MenuToday, FluentGlyphs.Calendar, (_, __) => _coordinator.Stats.ShowToday()));
            statsMenu.DropDownItems.Add(CreateMenuItem(LanguageManager.Current.MenuThisWeek, FluentGlyphs.CalendarWeek, (_, __) => _coordinator.Stats.ShowWeek()));
            statsMenu.DropDownItems.Add(CreateMenuItem(LanguageManager.Current.MenuThisMonth, FluentGlyphs.Chart, (_, __) => _coordinator.Stats.ShowMonth()));
            statsMenu.DropDownItems.Add(CreateMenuItem(LanguageManager.Current.MenuAllTime, FluentGlyphs.Stopwatch, (_, __) => _coordinator.Stats.ShowAllTime()));
            statsMenu.DropDownItems.Add(new ToolStripSeparator());
            statsMenu.DropDownItems.Add(CreateMenuItem(LanguageManager.Current.MenuExportCSV, FluentGlyphs.Save, (_, __) => _coordinator.Stats.ExportAsync("csv")));
            statsMenu.DropDownItems.Add(CreateMenuItem(LanguageManager.Current.MenuExportJSON, FluentGlyphs.Document, (_, __) => _coordinator.Stats.ExportAsync("json")));
            statsMenu.DropDownItems.Add(new ToolStripSeparator());
            statsMenu.DropDownItems.Add(CreateMenuItem(LanguageManager.Current.MenuResetStats, FluentGlyphs.Delete, async (_, __) =>
            {
                if (MessageDialog.Confirm(LanguageManager.Current.DialogResetStatsMessage, LanguageManager.Current.DialogResetStatsTitle))
                {
                    await _coordinator.Stats.ResetAsync();
                    _shell.ShowBalloonTip(LanguageManager.Current.AppName, LanguageManager.Current.MsgStatsReset, ToolTipIcon.Info);
                }
            }));
            menu.Items.Add(statsMenu);
        }

        private void AddQuickActionsMenu(ContextMenuStrip menu)
        {
            var quickActionsMenu = CreateMenuItem(LanguageManager.Current.MenuQuickActions, FluentGlyphs.Bolt);
            quickActionsMenu.DropDownItems.Add(CreateMenuItem(LanguageManager.Current.MenuOpenFolder, FluentGlyphs.FolderOpen,
                (_, __) => { try { System.Diagnostics.Process.Start("explorer.exe", AppPaths.InstallDir); } catch (Exception ex) { LogService.Log($"Failed to open folder: {ex.Message}", "ERROR", "TrayMenu"); } }));
            quickActionsMenu.DropDownItems.Add(CreateMenuItem(LanguageManager.Current.MenuEditConfig, FluentGlyphs.Settings,
                (_, __) => OpenOrCreateConfig()));
            quickActionsMenu.DropDownItems.Add(CreateMenuItem(LanguageManager.Current.MenuEditApps, FluentGlyphs.Edit,
                (_, __) => OpenFileWithEditor(AppPaths.AppsPath, "apps.json")));
            quickActionsMenu.DropDownItems.Add(new ToolStripSeparator());
            quickActionsMenu.DropDownItems.Add(CreateMenuItem(LanguageManager.Current.MenuReloadAll, FluentGlyphs.Refresh, (_, __) =>
            {
                if (MessageDialog.Confirm(LanguageManager.Current.DialogReloadMessage, LanguageManager.Current.DialogReloadTitle))
                    _coordinator.ReloadConfig();
            }));

            quickActionsMenu.DropDownItems.Add(new ToolStripSeparator());
            var shortcutMenu = CreateMenuItem(LanguageManager.Current.MenuManageShortcuts ?? "➕ Manage Shortcuts", FluentGlyphs.Add);

            var desktopShortcutItem = new ToolStripMenuItem(EscapeMnemonics(LanguageManager.Current.MenuShortcutDesktop ?? "Desktop Shortcut"))
            { Padding = MenuItemPadding };
            SetSubmenuSelection(desktopShortcutItem, ShortcutManager.IsDesktopShortcutExists());
            desktopShortcutItem.Click += async (_, __) =>
            {
                try
                {
                    if (ShortcutManager.IsDesktopShortcutExists())
                    {
                        if (MessageDialog.Confirm(LanguageManager.Current.DialogRemoveDesktopShortcut ?? "Remove desktop shortcut?", LanguageManager.Current.AppName))
                        {
                            ShortcutManager.RemoveDesktopShortcut();
                            _shell.ShowBalloonTip(LanguageManager.Current.AppName, LanguageManager.Current.MsgShortcutDesktopRemoved ?? "Desktop shortcut removed", ToolTipIcon.Info);
                            SettingsService.Instance.ShortcutPreferences.DesktopShortcut = false;
                            await SettingsService.SaveAsync();
                        }
                    }
                    else
                    {
                        ShortcutManager.CreateDesktopShortcut();
                        ShortcutManager.RefreshIconCache();
                        _shell.ShowBalloonTip(LanguageManager.Current.AppName, LanguageManager.Current.MsgShortcutDesktopCreated ?? "Desktop shortcut created", ToolTipIcon.Info);
                        SettingsService.Instance.ShortcutPreferences.DesktopShortcut = true;
                        SettingsService.Instance.ShortcutPreferences.PreferenceSaved = true;
                        await SettingsService.SaveAsync();
                    }
                    Rebuild();
                }
                catch (Exception ex)
                {
                    LogService.Log($"Desktop shortcut error: {ex.Message}", "ERROR", "TrayMenu");
                    MessageDialog.ShowError(LanguageManager.Current.ErrorManageDesktopShortcut + ex.Message,
                        LanguageManager.Current.AppName);
                }
            };
            shortcutMenu.DropDownItems.Add(desktopShortcutItem);

            var startMenuShortcutItem = new ToolStripMenuItem(EscapeMnemonics(LanguageManager.Current.MenuShortcutStartMenu ?? "Start Menu Shortcut"))
            { Padding = MenuItemPadding };
            SetSubmenuSelection(startMenuShortcutItem, ShortcutManager.IsStartMenuShortcutExists());
            startMenuShortcutItem.Click += async (_, __) =>
            {
                try
                {
                    if (ShortcutManager.IsStartMenuShortcutExists())
                    {
                        if (MessageDialog.Confirm(LanguageManager.Current.DialogRemoveStartMenuShortcut ?? "Remove Start Menu shortcut?", LanguageManager.Current.AppName))
                        {
                            ShortcutManager.RemoveStartMenuShortcut();
                            _shell.ShowBalloonTip(LanguageManager.Current.AppName, LanguageManager.Current.MsgShortcutStartMenuRemoved ?? "Start Menu shortcut removed", ToolTipIcon.Info);
                            SettingsService.Instance.ShortcutPreferences.StartMenuShortcut = false;
                            await SettingsService.SaveAsync();
                        }
                    }
                    else
                    {
                        ShortcutManager.CreateStartMenuShortcut();
                        ShortcutManager.RefreshIconCache();
                        _shell.ShowBalloonTip(LanguageManager.Current.AppName, LanguageManager.Current.MsgShortcutStartMenuCreated ?? "Start Menu shortcut created", ToolTipIcon.Info);
                        SettingsService.Instance.ShortcutPreferences.StartMenuShortcut = true;
                        SettingsService.Instance.ShortcutPreferences.PreferenceSaved = true;
                        await SettingsService.SaveAsync();
                    }
                    Rebuild();
                }
                catch (Exception ex)
                {
                    LogService.Log($"Start Menu shortcut error: {ex.Message}", "ERROR", "TrayMenu");
                    MessageDialog.ShowError(LanguageManager.Current.ErrorManageStartMenuShortcut + ex.Message,
                        LanguageManager.Current.AppName);
                }
            };
            shortcutMenu.DropDownItems.Add(startMenuShortcutItem);

            quickActionsMenu.DropDownItems.Add(shortcutMenu);
            menu.Items.Add(quickActionsMenu);
        }

        private void AddLanguageMenu(ContextMenuStrip menu)
        {
            var languageMenu = CreateMenuItem(LanguageManager.Current.MenuLanguage, FluentGlyphs.Globe);
            var availableLanguages = LanguageManager.GetAvailableLanguages();
            string currentLang = LanguageManager.GetCurrentLanguageCode();
            foreach (var lang in availableLanguages)
            {
                var langItem = new ToolStripMenuItem(EscapeMnemonics(lang.Name)) { Padding = MenuItemPadding };
                SetSubmenuSelection(langItem, lang.Code == currentLang);
                langItem.Click += async (_, __) =>
                {
                    await LanguageManager.SetLanguageAsync(lang.Code);
                    _shell.ShowBalloonTip(LanguageManager.Current.AppName, LanguageManager.Current.MsgLanguageChanged, ToolTipIcon.Info);
                    Rebuild();
                };
                languageMenu.DropDownItems.Add(langItem);
            }
            menu.Items.Add(languageMenu);
        }
        #endregion

        #region Config helpers (formerly in Program.cs) -----
        private void OpenOrCreateConfig()
        {
            try
            {
                if (!File.Exists(AppPaths.ConfigPath))
                {
                    if (MessageDialog.Confirm(LanguageManager.Current.DialogConfigNotFound, LanguageManager.Current.AppName))
                        CreateDefaultConfigFile();
                    else return;
                }
                OpenFileWithEditor(AppPaths.ConfigPath, "config.json");
            }
            catch (Exception ex)
            {
                LogService.Log($"Error opening config: {ex.Message}", "ERROR", "TrayMenu");
                MessageDialog.ShowError($"{LanguageManager.Current.ErrorPrefix}{ex.Message}", LanguageManager.Current.AppName);
            }
        }

        private void CreateDefaultConfigFile()
        {
            try
            {
                var defaultConfig = AppCoordinator.GetDefaultConfig();
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                File.WriteAllText(AppPaths.ConfigPath,
                    JsonSerializer.Serialize(defaultConfig, typeof(Config), new JsonContext(options)));
                LogService.Log("Created default config.json", "INFO", "TrayMenu");
                _shell.ShowBalloonTip(LanguageManager.Current.AppName, LanguageManager.Current.MsgConfigCreated, ToolTipIcon.Info);
            }
            catch (Exception ex)
            {
                LogService.Log($"Failed to create config.json: {ex.Message}", "ERROR", "TrayMenu");
                MessageDialog.ShowError($"{LanguageManager.Current.ErrorCreateConfig}\n{ex.Message}",
                    LanguageManager.Current.AppName);
            }
        }

        private void OpenFileWithEditor(string filePath, string fileName)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    MessageDialog.ShowError(LanguageManager.Current.DialogFileNotFound, LanguageManager.Current.AppName);
                    return;
                }
                var psi = new System.Diagnostics.ProcessStartInfo { FileName = filePath, UseShellExecute = true };
                System.Diagnostics.Process.Start(psi);
                LogService.Log($"Opened {fileName} with default editor", "INFO", "TrayMenu");
                _shell.ShowBalloonTip(LanguageManager.Current.AppName, LanguageManager.Current.MsgReloadTip, ToolTipIcon.Info);
            }
            catch (Exception ex)
            {
                LogService.Log($"Failed to open {fileName}: {ex.Message}", "ERROR", "TrayMenu");
                if (MessageDialog.Confirm(LanguageManager.Current.DialogOpenWithNotepad, LanguageManager.Current.AppName))
                    System.Diagnostics.Process.Start("notepad.exe", filePath);
            }
        }

        #endregion
    }
}