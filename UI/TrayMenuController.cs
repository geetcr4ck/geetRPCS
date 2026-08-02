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
                var autoUpdateItem = new ToolStripMenuItem("🔄 Auto-Update") { Checked = SettingsService.Instance.AutoUpdateEnabled };
                autoUpdateItem.Click += async (s, args) =>
                {
                    bool newState = !SettingsService.Instance.AutoUpdateEnabled;
                    SettingsService.Instance.AutoUpdateEnabled = newState;
                    await SettingsService.SaveAsync();
                    ((ToolStripMenuItem)s!).Checked = newState;
                    _shell.ShowBalloonTip(LanguageManager.Current.AppName,
                        newState ? "Auto-update enabled. App will update automatically."
                                 : "Auto-update disabled. You'll be notified about updates.",
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
                if (MessageBox.Show(LanguageManager.Current.DialogResetStatsMessage, LanguageManager.Current.DialogResetStatsTitle,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
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
                if (MessageBox.Show(LanguageManager.Current.DialogReloadMessage, LanguageManager.Current.DialogReloadTitle,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes) _coordinator.ReloadConfig();
            });

            quickActionsMenu.DropDownItems.Add(new ToolStripSeparator());
            var shortcutMenu = new ToolStripMenuItem("➕ Manage Shortcuts");

            var desktopShortcutItem = new ToolStripMenuItem("Desktop Shortcut")
            { Checked = ShortcutManager.IsDesktopShortcutExists() };
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
                            _shell.ShowBalloonTip(LanguageManager.Current.AppName, "Desktop shortcut removed", ToolTipIcon.Info);
                            SettingsService.Instance.ShortcutPreferences.DesktopShortcut = false;
                            await SettingsService.SaveAsync();
                        }
                    }
                    else
                    {
                        ShortcutManager.CreateDesktopShortcut();
                        ShortcutManager.RefreshIconCache();
                        _shell.ShowBalloonTip(LanguageManager.Current.AppName, "Desktop shortcut created", ToolTipIcon.Info);
                        SettingsService.Instance.ShortcutPreferences.DesktopShortcut = true;
                        SettingsService.Instance.ShortcutPreferences.PreferenceSaved = true;
                        await SettingsService.SaveAsync();
                    }
                    Rebuild();
                }
                catch (Exception ex)
                {
                    LogService.Log($"Desktop shortcut error: {ex.Message}", "ERROR", "TrayMenu");
                    MessageBox.Show($"Failed to manage desktop shortcut: {ex.Message}",
                        LanguageManager.Current.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            shortcutMenu.DropDownItems.Add(desktopShortcutItem);

            var startMenuShortcutItem = new ToolStripMenuItem("Start Menu Shortcut")
            { Checked = ShortcutManager.IsStartMenuShortcutExists() };
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
                            _shell.ShowBalloonTip(LanguageManager.Current.AppName, "Start Menu shortcut removed", ToolTipIcon.Info);
                            SettingsService.Instance.ShortcutPreferences.StartMenuShortcut = false;
                            await SettingsService.SaveAsync();
                        }
                    }
                    else
                    {
                        ShortcutManager.CreateStartMenuShortcut();
                        ShortcutManager.RefreshIconCache();
                        _shell.ShowBalloonTip(LanguageManager.Current.AppName, "Start Menu shortcut created", ToolTipIcon.Info);
                        SettingsService.Instance.ShortcutPreferences.StartMenuShortcut = true;
                        SettingsService.Instance.ShortcutPreferences.PreferenceSaved = true;
                        await SettingsService.SaveAsync();
                    }
                    Rebuild();
                }
                catch (Exception ex)
                {
                    LogService.Log($"Start Menu shortcut error: {ex.Message}", "ERROR", "TrayMenu");
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
                    var result = MessageBox.Show(LanguageManager.Current.DialogConfigNotFound,
                        LanguageManager.Current.AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (result == DialogResult.Yes) CreateDefaultConfigFile();
                    else return;
                }
                OpenFileWithEditor(AppPaths.ConfigPath, "config.json");
            }
            catch (Exception ex)
            {
                LogService.Log($"Error opening config: {ex.Message}", "ERROR", "TrayMenu");
                MessageBox.Show($"Error: {ex.Message}", LanguageManager.Current.AppName,
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
                var result = MessageBox.Show(LanguageManager.Current.DialogOpenWithNotepad, LanguageManager.Current.AppName,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result == DialogResult.Yes) System.Diagnostics.Process.Start("notepad.exe", filePath);
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
            var btnReset = new System.Windows.Forms.Button()
            {
                Text = "Reset Default",
                Left = 210,
                Width = 120,
                Top = 180,
                BackColor = Color.FromArgb(79, 84, 92),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            btnReset.FlatAppearance.BorderSize = 0;
            btnReset.Click += (s, e) => { textBox.Text = defaultAppId; };
            var confirmation = new System.Windows.Forms.Button()
            {
                Text = LanguageManager.Current.BtnSave ?? "Save",
                Left = 340,
                Width = 120,
                Top = 180,
                DialogResult = DialogResult.OK,
                BackColor = Color.FromArgb(88, 101, 242),
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
            prompt.Controls.Add(btnReset);
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
    }
}