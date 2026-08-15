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
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Encodings.Web;
using System.Windows.Forms;
using geetRPCS.Models;
using geetRPCS.Services;
using geetRPCS.Utils;

namespace geetRPCS.UI
{
    internal sealed class TrayMenuController
    {
        private readonly NotifyIcon _trayIcon;
        private readonly AppCoordinator _coordinator;
        private readonly Program _shell;
        private const int BALLOON_TIMEOUT_MS = 2000;

        // Menu item references updated in place (instead of full rebuilds).
        public ToolStripMenuItem PauseItem { get; private set; }
        public ToolStripMenuItem PrivateModeItem { get; private set; }
        public ToolStripMenuItem PreviewMenuItem { get; private set; }
        public ToolStripMenuItem MouseEnergyItem { get; private set; }
        public ToolStripMenuItem TrayAnimationItem { get; private set; }

        public TrayMenuController(NotifyIcon trayIcon, AppCoordinator coordinator, Program shell)
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
                _trayIcon.ContextMenuStrip?.Dispose();
                var menu = new ContextMenuStrip();

                PauseItem = new ToolStripMenuItem(_coordinator.IsPaused ? LanguageManager.Current.MenuResume : LanguageManager.Current.MenuPause)
                { Checked = _coordinator.IsPaused };
                PauseItem.Click += (_, __) => _coordinator.TogglePause();
                menu.Items.Add(PauseItem);

                PrivateModeItem = new ToolStripMenuItem(LanguageManager.Current.MenuPrivateMode) { Checked = _coordinator.PrivateMode };
                PrivateModeItem.Click += (_, __) => _coordinator.TogglePrivateMode();
                menu.Items.Add(PrivateModeItem);

                MouseEnergyItem = new ToolStripMenuItem(LanguageManager.Current.MenuMouseEnergy) { Checked = SettingsService.Instance.MouseEnergyEnabled };
                MouseEnergyItem.Click += async (_, __) => await _coordinator.SetMouseEnergyAsync(!SettingsService.Instance.MouseEnergyEnabled);
                menu.Items.Add(MouseEnergyItem);

                TrayAnimationItem = new ToolStripMenuItem(LanguageManager.Current.MenuTrayAnimation) { Checked = SettingsService.Instance.TrayAnimationEnabled };
                TrayAnimationItem.Click += async (_, __) => await _coordinator.SetTrayAnimationAsync(!SettingsService.Instance.TrayAnimationEnabled);
                menu.Items.Add(TrayAnimationItem);

                var telemetryItem = new ToolStripMenuItem(LanguageManager.Current.MenuTelemetry)
                { Checked = TelemetryService.IsEnabled() };
                telemetryItem.Click += async (s, args) =>
                {
                    bool newState = !TelemetryService.IsEnabled();
                    await _coordinator.ToggleTelemetryAsync(newState);
                    ((ToolStripMenuItem)s!).Checked = newState;
                };
                menu.Items.Add(telemetryItem);

                // Auto-Update toggle
                var autoUpdateItem = new ToolStripMenuItem(LanguageManager.Current.MenuAutoUpdate ?? "🔄 Auto-Update") { Checked = SettingsService.Instance.AutoUpdateEnabled };
                autoUpdateItem.Click += async (s, args) =>
                {
                    bool newState = !SettingsService.Instance.AutoUpdateEnabled;
                    SettingsService.Instance.AutoUpdateEnabled = newState;
                    await SettingsService.SaveAsync();
                    ((ToolStripMenuItem)s!).Checked = newState;
                    _shell.ShowBalloonTip(LanguageManager.Current.AppName,
                        newState ? (LanguageManager.Current.MsgAutoUpdateEnabled ?? "Auto-update enabled. App will update automatically.")
                                 : (LanguageManager.Current.MsgAutoUpdateDisabled ?? "Auto-update disabled. You'll be notified about updates."),
                        ToolTipIcon.Info);
                    LogService.Log($"Auto-update {(newState ? "enabled" : "disabled")}", "INFO", "TrayMenu");
                };
                menu.Items.Add(autoUpdateItem);
                menu.Items.Add(new ToolStripSeparator());

                var manageAppsItem = new ToolStripMenuItem(LanguageManager.Current.MenuManageApps);
                manageAppsItem.Click += (_, __) => _shell.ToggleManageAppsVisibility();
                menu.Items.Add(manageAppsItem);

                var changeIdItem = new ToolStripMenuItem(LanguageManager.Current.MenuChangeAppId);
                changeIdItem.Click += (_, __) =>
                {
                    string currentId = _coordinator.Config.Discord?.ApplicationId ?? "";
                    string newId = ShowInputDialog(
                        LanguageManager.Current.DialogChangeAppIdMessage,
                        LanguageManager.Current.DialogChangeAppIdTitle,
                        currentId);
                    if (!string.IsNullOrWhiteSpace(newId) && newId.Trim() != currentId)
                    {
                        if (_coordinator.ChangeApplicationId(newId))
                        {
                            MessageBox.Show(LanguageManager.Current.MsgAppIdChanged,
                                LanguageManager.Current.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        else
                        {
                            MessageBox.Show($"{LanguageManager.Current.ErrorSaveConfig}: invalid ID",
                                LanguageManager.Current.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                };
                menu.Items.Add(changeIdItem);
                menu.Items.Add(new ToolStripSeparator());

                AddStatisticsMenu(menu);

                PreviewMenuItem = new ToolStripMenuItem(LanguageManager.Current.MenuPreviewWindow)
                { Checked = _shell.IsPreviewVisible };
                PreviewMenuItem.Click += (_, __) => _shell.TogglePreviewVisibility();
                menu.Items.Add(PreviewMenuItem);
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
                        LogService.Log($"Startup toggle error: {ex.Message}", "ERROR", "TrayMenu");
                        MessageBox.Show(LanguageManager.Current.ErrorStartupToggle + ex.Message,
                            LanguageManager.Current.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                };
                menu.Items.Add(startupItem);
                AddQuickActionsMenu(menu);
                menu.Items.Add(new ToolStripSeparator());
                AddLanguageMenu(menu);
                menu.Items.Add(LanguageManager.Current.MenuCheckUpdates, null, (_, __) => _shell.CheckForUpdatesFromMenu());
                menu.Items.Add(LanguageManager.Current.MenuOpenLog, null, (_, __) => _shell.OpenLog());
                menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add(LanguageManager.Current.MenuExit, null, (_, __) => _shell.ExitApp());

                _trayIcon.ContextMenuStrip = menu;
                UpdateTrayText();
                LogService.Log("Tray menu updated", "INFO", "TrayMenu");
            }
            catch (Exception ex) { LogService.Log($"Failed to update tray menu: {ex}", "ERROR", "TrayMenu"); }
        }

        /// <summary>Refreshes pause/private check state and the tray tooltip text (no full rebuild).</summary>
        public void UpdatePresentation()
        {
            try
            {
                if (PauseItem != null)
                {
                    PauseItem.Checked = _coordinator.IsPaused;
                    PauseItem.Text = _coordinator.IsPaused ? LanguageManager.Current.MenuResume : LanguageManager.Current.MenuPause;
                }
                if (PrivateModeItem != null) PrivateModeItem.Checked = _coordinator.PrivateMode;
                if (MouseEnergyItem != null) MouseEnergyItem.Checked = SettingsService.Instance.MouseEnergyEnabled;
                if (TrayAnimationItem != null) TrayAnimationItem.Checked = SettingsService.Instance.TrayAnimationEnabled;
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
            var statsMenu = new ToolStripMenuItem(LanguageManager.Current.MenuStatistics);
            statsMenu.DropDownItems.Add(LanguageManager.Current.MenuToday, null, (_, __) => _coordinator.Stats.ShowToday());
            statsMenu.DropDownItems.Add(LanguageManager.Current.MenuThisWeek, null, (_, __) => _coordinator.Stats.ShowWeek());
            statsMenu.DropDownItems.Add(LanguageManager.Current.MenuThisMonth, null, (_, __) => _coordinator.Stats.ShowMonth());
            statsMenu.DropDownItems.Add(LanguageManager.Current.MenuAllTime, null, (_, __) => _coordinator.Stats.ShowAllTime());
            statsMenu.DropDownItems.Add(new ToolStripSeparator());
            statsMenu.DropDownItems.Add(LanguageManager.Current.MenuExportCSV, null, (_, __) => _coordinator.Stats.ExportAsync("csv"));
            statsMenu.DropDownItems.Add(LanguageManager.Current.MenuExportJSON, null, (_, __) => _coordinator.Stats.ExportAsync("json"));
            statsMenu.DropDownItems.Add(new ToolStripSeparator());
            statsMenu.DropDownItems.Add(LanguageManager.Current.MenuResetStats, null, async (_, __) =>
            {
                if (ConfirmDialog.Show(LanguageManager.Current.DialogResetStatsMessage, LanguageManager.Current.DialogResetStatsTitle))
                {
                    await _coordinator.Stats.ResetAsync();
                    _shell.ShowBalloonTip(LanguageManager.Current.AppName, LanguageManager.Current.MsgStatsReset, ToolTipIcon.Info);
                }
            });
            menu.Items.Add(statsMenu);
        }

        private void AddQuickActionsMenu(ContextMenuStrip menu)
        {
            var quickActionsMenu = new ToolStripMenuItem(LanguageManager.Current.MenuQuickActions);
            quickActionsMenu.DropDownItems.Add(LanguageManager.Current.MenuOpenFolder, null,
                (_, __) => { try { System.Diagnostics.Process.Start("explorer.exe", AppPaths.InstallDir); } catch (Exception ex) { LogService.Log($"Failed to open folder: {ex.Message}", "ERROR", "TrayMenu"); } });
            quickActionsMenu.DropDownItems.Add(LanguageManager.Current.MenuEditConfig, null,
                (_, __) => OpenOrCreateConfig());
            quickActionsMenu.DropDownItems.Add(LanguageManager.Current.MenuEditApps, null,
                (_, __) => OpenFileWithEditor(AppPaths.AppsPath, "apps.json"));
            quickActionsMenu.DropDownItems.Add(new ToolStripSeparator());
            quickActionsMenu.DropDownItems.Add(LanguageManager.Current.MenuReloadAll, null, (_, __) =>
            {
                if (ConfirmDialog.Show(LanguageManager.Current.DialogReloadMessage, LanguageManager.Current.DialogReloadTitle))
                    _coordinator.ReloadConfig();
            });

            quickActionsMenu.DropDownItems.Add(new ToolStripSeparator());
            var shortcutMenu = new ToolStripMenuItem(LanguageManager.Current.MenuManageShortcuts ?? "➕ Manage Shortcuts");

            var desktopShortcutItem = new ToolStripMenuItem(LanguageManager.Current.MenuShortcutDesktop ?? "Desktop Shortcut")
            { Checked = ShortcutManager.IsDesktopShortcutExists() };
            desktopShortcutItem.Click += async (_, __) =>
            {
                try
                {
                    if (ShortcutManager.IsDesktopShortcutExists())
                    {
                        if (ConfirmDialog.Show(LanguageManager.Current.DialogRemoveDesktopShortcut ?? "Remove desktop shortcut?", LanguageManager.Current.AppName))
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
                    MessageBox.Show(LanguageManager.Current.ErrorManageDesktopShortcut + ex.Message,
                        LanguageManager.Current.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            shortcutMenu.DropDownItems.Add(desktopShortcutItem);

            var startMenuShortcutItem = new ToolStripMenuItem(LanguageManager.Current.MenuShortcutStartMenu ?? "Start Menu Shortcut")
            { Checked = ShortcutManager.IsStartMenuShortcutExists() };
            startMenuShortcutItem.Click += async (_, __) =>
            {
                try
                {
                    if (ShortcutManager.IsStartMenuShortcutExists())
                    {
                        if (ConfirmDialog.Show(LanguageManager.Current.DialogRemoveStartMenuShortcut ?? "Remove Start Menu shortcut?", LanguageManager.Current.AppName))
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
                    MessageBox.Show(LanguageManager.Current.ErrorManageStartMenuShortcut + ex.Message,
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
                    if (ConfirmDialog.Show(LanguageManager.Current.DialogConfigNotFound, LanguageManager.Current.AppName))
                        CreateDefaultConfigFile();
                    else return;
                }
                OpenFileWithEditor(AppPaths.ConfigPath, "config.json");
            }
            catch (Exception ex)
            {
                LogService.Log($"Error opening config: {ex.Message}", "ERROR", "TrayMenu");
                MessageBox.Show($"{LanguageManager.Current.ErrorPrefix}{ex.Message}", LanguageManager.Current.AppName,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                MessageBox.Show($"{LanguageManager.Current.ErrorCreateConfig}\n{ex.Message}",
                    LanguageManager.Current.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OpenFileWithEditor(string filePath, string fileName)
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
                LogService.Log($"Opened {fileName} with default editor", "INFO", "TrayMenu");
                _shell.ShowBalloonTip(LanguageManager.Current.AppName, LanguageManager.Current.MsgReloadTip, ToolTipIcon.Info);
            }
            catch (Exception ex)
            {
                LogService.Log($"Failed to open {fileName}: {ex.Message}", "ERROR", "TrayMenu");
                if (ConfirmDialog.Show(LanguageManager.Current.DialogOpenWithNotepad, LanguageManager.Current.AppName))
                    System.Diagnostics.Process.Start("notepad.exe", filePath);
            }
        }

        private string ShowInputDialog(string text, string caption, string defaultValue = "")
        {
            string tutorialUrl = LanguageManager.Current.UrlTutorial;
            string assetsUrl = "https://github.com/geetcr4ck/geetRPCS/raw/main/AssetPack.zip";
            string defaultAppId = "1433700335863726183";

            // Discord-style dark palette (matches ManageAppsForm).
            Color bg = Color.FromArgb(47, 49, 54);
            Color inputBg = Color.FromArgb(30, 31, 34);
            Color textColor = Color.FromArgb(255, 255, 255);
            Color blurple = Color.FromArgb(88, 101, 242);
            Color blurpleHover = Color.FromArgb(71, 82, 196);
            Color blurpleDown = Color.FromArgb(60, 69, 165);
            Color btnBg = Color.FromArgb(78, 80, 88);
            Color btnHover = Color.FromArgb(109, 111, 120);
            Color btnDown = Color.FromArgb(92, 94, 102);
            Color warnBg = Color.FromArgb(44, 41, 33);
            Color warnText = Color.FromArgb(240, 178, 50);
            Color warnAccent = Color.FromArgb(250, 166, 26);
            Color errorText = Color.FromArgb(240, 71, 71); // Discord red #F04747

            // The localized message is "instruction\n\nWARNING: ...". Split it so the
            // warning renders as its own callout instead of a wall of text.
            string description = text, warning = null;
            int split = text.IndexOf("\n\n", StringComparison.Ordinal);
            if (split >= 0)
            {
                description = text.Substring(0, split).Trim();
                warning = text.Substring(split + 2).Trim();
            }

            const int PAD = 24;
            const int CLIENT_W = 480;
            Font font = new Font("Segoe UI", 9);
            Font inputFont = new Font("Segoe UI", 10);

            using Form prompt = new Form()
            {
                FormBorderStyle = FormBorderStyle.FixedDialog,
                Text = caption,
                StartPosition = FormStartPosition.CenterScreen,
                MaximizeBox = false,
                MinimizeBox = false,
                ShowInTaskbar = false,
                BackColor = bg,
                ForeColor = textColor
            };
            try
            {
                string iconPath = Utils.AppPaths.IconPath;
                if (File.Exists(iconPath)) prompt.Icon = new Icon(iconPath);
            }
            catch { }

            int contentW = CLIENT_W - 2 * PAD;
            int y = PAD;

            // 1. Description
            var textLabel = new Label()
            {
                Left = PAD,
                Top = y,
                Width = contentW,
                Text = description,
                AutoSize = false,
                Font = font,
                ForeColor = textColor
            };
            textLabel.Height = TextRenderer.MeasureText(description, font, new Size(contentW, 0), TextFormatFlags.WordBreak).Height + 2;
            prompt.Controls.Add(textLabel);
            y += textLabel.Height + 14;

            // 2. Warning callout (amber accent bar + tinted panel)
            if (!string.IsNullOrEmpty(warning))
            {
                int warnTextW = contentW - 4 - 24; // minus accent bar and label padding
                int warnH = TextRenderer.MeasureText(warning, font, new Size(warnTextW, 0), TextFormatFlags.WordBreak).Height + 18;
                var warnPanel = new Panel
                {
                    Left = PAD,
                    Top = y,
                    Width = contentW,
                    Height = warnH,
                    BackColor = warnBg
                };
                var warnAccentBar = new Panel { Dock = DockStyle.Left, Width = 4, BackColor = warnAccent };
                var warnLabel = new Label
                {
                    Dock = DockStyle.Fill,
                    Text = warning,
                    Font = font,
                    ForeColor = warnText,
                    AutoSize = false,
                    Padding = new Padding(12, 9, 12, 9)
                };
                warnPanel.Controls.Add(warnAccentBar);
                warnPanel.Controls.Add(warnLabel);
                prompt.Controls.Add(warnPanel);
                y += warnH + 14;
            }

            // 3. Input (pre-filled with the current ID)
            var textBox = new TextBox()
            {
                Left = PAD,
                Top = y,
                Width = contentW,
                Text = defaultValue,
                Font = inputFont,
                BackColor = inputBg,
                ForeColor = textColor,
                BorderStyle = BorderStyle.FixedSingle,
                TabIndex = 0
            };
            prompt.Controls.Add(textBox);
            y += textBox.Height + 4;

            // Inline validation error (shown only while the input is invalid)
            var lblError = new Label()
            {
                Left = PAD,
                Top = y,
                Width = contentW,
                Height = 15,
                Text = LanguageManager.Current.ErrorInvalidAppId ?? "Application ID must be 17-20 digits (numbers only).",
                Font = new Font("Segoe UI", 8),
                ForeColor = errorText,
                Visible = false
            };
            prompt.Controls.Add(lblError);
            y += 15 + 5;

            // 4. Helper links (tutorial + asset pack)
            var links = new FlowLayoutPanel
            {
                Left = PAD,
                Top = y,
                Width = contentW,
                Height = 22,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent
            };
            var lnkTut = new LinkLabel()
            {
                Text = LanguageManager.Current.LinkTutorial,
                AutoSize = true,
                LinkColor = blurple,
                ActiveLinkColor = Color.FromArgb(115, 125, 255),
                Font = font
            };
            lnkTut.LinkClicked += (s, e) => OpenUrl(tutorialUrl);
            var lnkAssets = new LinkLabel()
            {
                Text = LanguageManager.Current.LinkDownloadAssets,
                AutoSize = true,
                LinkColor = blurple,
                ActiveLinkColor = Color.FromArgb(115, 125, 255),
                Font = font,
                Margin = new Padding(16, 0, 0, 0)
            };
            lnkAssets.LinkClicked += (s, e) => OpenUrl(assetsUrl);
            links.Controls.Add(lnkTut);
            links.Controls.Add(lnkAssets);
            prompt.Controls.Add(links);
            y += links.Height + 18;

            // 5. Action row (right-aligned): Cancel | Reset Default | Save
            int btnW = 104, btnH = 32, gap = 8;
            var btnCancel = MakeDialogButton(LanguageManager.Current.BtnCancel ?? "Cancel", btnBg, btnHover, btnDown, font);
            btnCancel.Bounds = new Rectangle(CLIENT_W - PAD - 3 * btnW - 2 * gap, y, btnW, btnH);
            btnCancel.DialogResult = DialogResult.Cancel;
            btnCancel.TabIndex = 1;
            var btnReset = MakeDialogButton(LanguageManager.Current.BtnResetDefault ?? "Reset Default", btnBg, btnHover, btnDown, font);
            btnReset.Bounds = new Rectangle(CLIENT_W - PAD - 2 * btnW - gap, y, btnW, btnH);
            btnReset.TabIndex = 2;
            btnReset.Click += (s, e) =>
            {
                textBox.Text = defaultAppId;
                textBox.Focus();
                textBox.SelectAll();
            };
            var btnSave = MakeDialogButton(LanguageManager.Current.BtnSave ?? "Save", blurple, blurpleHover, blurpleDown, font);
            btnSave.Bounds = new Rectangle(CLIENT_W - PAD - btnW, y, btnW, btnH);
            btnSave.DialogResult = DialogResult.OK;
            btnSave.TabIndex = 3;
            prompt.Controls.Add(btnCancel);
            prompt.Controls.Add(btnReset);
            prompt.Controls.Add(btnSave);

            // Save is enabled only for a valid, changed ID; otherwise show an inline error.
            Action refreshSave = () =>
            {
                string val = textBox.Text?.Trim() ?? "";
                bool valid = AppCoordinator.IsValidApplicationId(val);
                bool changed = val.Length > 0 && val != (defaultValue ?? "").Trim();
                btnSave.Enabled = valid && changed;
                lblError.Visible = val.Length > 0 && !valid;
            };
            textBox.TextChanged += (s, e) => refreshSave();
            refreshSave();

            prompt.AcceptButton = btnSave;
            prompt.CancelButton = btnCancel;
            prompt.Shown += (s, e) =>
            {
                textBox.Focus();
                textBox.SelectAll();
            };

            prompt.ClientSize = new Size(CLIENT_W, y + btnH + PAD);
            return prompt.ShowDialog() == DialogResult.OK ? textBox.Text : "";
        }

        /// <summary>Flat dark-theme button with hover/pressed states (Discord style).</summary>
        private static Button MakeDialogButton(string text, Color bg, Color hover, Color down, Font font)
        {
            return new Button
            {
                Text = text,
                Font = font,
                ForeColor = Color.White,
                BackColor = bg,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0, MouseOverBackColor = hover, MouseDownBackColor = down },
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
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
    }
}