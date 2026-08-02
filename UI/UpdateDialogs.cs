/**
 * geetRPCS - Update Dialogs
 * UI dialogs for application update notifications
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
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using static geetRPCS.Services.UpdateChecker;

#nullable enable

namespace geetRPCS.UI
{
    internal static class UpdateDialogs
    {
        private static string CURRENT_VERSION => Utils.AppVersion.VersionText;

        public static void ShowEnhancedUpdateDialog(GitHubRelease release)
        {
            string latestVersion = release.TagName?.TrimStart('v') ?? "Unknown";
            string releaseNotes = release.Body ?? "No release notes available.";
            string downloadUrl = release.HtmlUrl ?? "https://github.com/geetcr4ck/geetRPCS/releases";
            DateTime publishedDate = release.PublishedAt;

            using var dialog = CreateBaseDialog(Services.LanguageManager.Current.UpdateAvailableTitle, new Size(550, 750));
            dialog.MaximumSize = new Size(700, 900);
            AddHeaderPanel(dialog, "🎊", Services.LanguageManager.Current.UpdateAvailableMessage, Services.LanguageManager.Current.UpdateSubtitle,
                Color.FromArgb(88, 101, 242), Color.FromArgb(88, 101, 242), Color.FromArgb(115, 125, 255));

            var contentPanel = CreateContentPanel(dialog);
            contentPanel.AutoScroll = true;
            int yPos = 10;

            // Version box
            var versionBox = new Panel
            {
                Location = new Point(20, yPos),
                Size = new Size(contentPanel.Width - 60, 75),
                BackColor = Color.FromArgb(32, 34, 37),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            AddLabel(versionBox, Services.LanguageManager.Current.UpdateCurrentVersion, new Point(15, 12), new Font("Segoe UI", 9, FontStyle.Bold), Color.FromArgb(185, 187, 190));
            AddLabel(versionBox, $"v{CURRENT_VERSION}", new Point(15, 35), new Font("Segoe UI", 11, FontStyle.Bold), Color.FromArgb(250, 168, 26));
            AddLabel(versionBox, Services.LanguageManager.Current.UpdateLatestVersion, new Point(250, 12), new Font("Segoe UI", 9, FontStyle.Bold), Color.FromArgb(185, 187, 190));
            AddLabel(versionBox, $"v{latestVersion}", new Point(250, 35), new Font("Segoe UI", 11, FontStyle.Bold), Color.FromArgb(87, 242, 135));
            contentPanel.Controls.Add(versionBox);
            yPos += 85;

            AddLabel(contentPanel, $"📅 {Services.LanguageManager.Current.UpdateReleased} {publishedDate:MMMM dd, yyyy 'at' HH:mm} UTC", new Point(20, yPos), new Font("Segoe UI", 8), Color.FromArgb(142, 146, 151));
            yPos += 25;

            // Changelog
            AddLabel(contentPanel, Services.LanguageManager.Current.UpdateChangelog, new Point(20, yPos), new Font("Segoe UI", 10, FontStyle.Bold), Color.White);
            yPos += 25;
            var changelogBox = new RichTextBox
            {
                Location = new Point(20, yPos),
                Size = new Size(contentPanel.Width - 60, 80),
                BackColor = Color.FromArgb(32, 34, 37),
                ForeColor = Color.FromArgb(220, 221, 222),
                Font = new Font("Segoe UI", 9),
                BorderStyle = BorderStyle.None,
                ReadOnly = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                Text = FormatReleaseNotes(releaseNotes)
            };
            contentPanel.Controls.Add(changelogBox);
            yPos += 90;

            // How to update
            AddLabel(contentPanel, Services.LanguageManager.Current.UpdateHowTo, new Point(20, yPos), new Font("Segoe UI", 10, FontStyle.Bold), Color.White);
            yPos += 25;

            // === Method 0: In-App Update ===
            var inAppBox = new Panel
            {
                Location = new Point(20, yPos),
                Size = new Size(contentPanel.Width - 60, 90),
                BackColor = Color.FromArgb(32, 34, 37),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            inAppBox.Paint += (s, e) => {
                using var pen = new Pen(Color.FromArgb(88, 101, 242), 2);
                e.Graphics.DrawRectangle(pen, 1, 1, inAppBox.Width - 3, inAppBox.Height - 3);
            };

            AddLabel(inAppBox, Services.LanguageManager.Current.UpdateMethodInApp ?? "★ In-App Update (Recommended)", new Point(10, 8), new Font("Segoe UI", 9, FontStyle.Bold), Color.FromArgb(88, 101, 242));

            var updateNowBtn = CreateButton(Services.LanguageManager.Current.BtnUpdateNow, Color.FromArgb(88, 101, 242), new Size(150, 32));
            updateNowBtn.Location = new Point(10, 35);
            inAppBox.Controls.Add(updateNowBtn);

            var progressBar = new ProgressBar
            {
                Location = new Point(10, 35),
                Size = new Size(inAppBox.Width - 180, 25),
                Style = ProgressBarStyle.Continuous,
                Visible = false
            };
            inAppBox.Controls.Add(progressBar);

            var statusLabel = new Label
            {
                Location = new Point(10, 65),
                Size = new Size(inAppBox.Width - 100, 20),
                ForeColor = Color.FromArgb(185, 187, 190),
                Font = new Font("Segoe UI", 8),
                Text = "",
                Visible = false
            };
            inAppBox.Controls.Add(statusLabel);

            var cancelBtn = CreateButton(Services.LanguageManager.Current.BtnCancel ?? "Cancel", Color.FromArgb(237, 66, 69), new Size(80, 25));
            cancelBtn.Font = new Font("Segoe UI", 8, FontStyle.Bold);
            cancelBtn.Location = new Point(inAppBox.Width - 90, 35);
            cancelBtn.Visible = false;
            inAppBox.Controls.Add(cancelBtn);

            CancellationTokenSource? cts = null;

            updateNowBtn.Click += async (s, e) =>
            {
                try
                {
                    updateNowBtn.Visible = false;
                    progressBar.Visible = true;
                    statusLabel.Visible = true;
                    cancelBtn.Visible = true;
                    progressBar.Value = 0;
                    statusLabel.Text = Services.LanguageManager.Current.UpdatePreparing ?? "Preparing update...";

                    cts = new CancellationTokenSource();
                    var downloader = new Services.UpdateDownloader();

                    downloader.OnProgressChanged += (percent, current, total, speed) =>
                    {
                        try
                        {
                            if (dialog.IsDisposed) return;
                            Action updateUI = () =>
                            {
                                if (progressBar.IsDisposed || statusLabel.IsDisposed) return;
                                progressBar.Value = Math.Min(Math.Max(percent, 0), 100);
                                double currentMB = current / 1024.0 / 1024.0;
                                double totalMB = total / 1024.0 / 1024.0;
                                double speedMBps = speed / 1024.0 / 1024.0;
                                string etaStr = "";
                                if (speed > 0 && total > current)
                                {
                                    double remainingBytes = total - current;
                                    double etaSeconds = remainingBytes / speed;
                                    if (etaSeconds < 60)
                                        etaStr = $" | ETA: {etaSeconds:F0}s";
                                    else
                                        etaStr = $" | ETA: {etaSeconds / 60:F0}m {etaSeconds % 60:F0}s";
                                }
                                statusLabel.Text = $"{currentMB:F1} / {totalMB:F1} MB @ {speedMBps:F2} MB/s{etaStr}";
                            };
                            if (dialog.InvokeRequired) dialog.BeginInvoke(updateUI);
                            else updateUI();
                        }
                        catch { }
                    };

                    downloader.OnStatusChanged += (status) =>
                    {
                        try
                        {
                            if (dialog.IsDisposed) return;
                            Action updateUI = () => { if (!statusLabel.IsDisposed) statusLabel.Text = status; };
                            if (dialog.InvokeRequired) dialog.BeginInvoke(updateUI);
                            else updateUI();
                        }
                        catch { }
                    };

                    downloader.OnError += (error) =>
                    {
                        try
                        {
                            if (dialog.IsDisposed) return;
                            Action updateUI = () =>
                            {
                                MessageBox.Show(error, "Update Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                updateNowBtn.Visible = true;
                                progressBar.Visible = false;
                                statusLabel.Visible = false;
                                cancelBtn.Visible = false;
                            };
                            if (dialog.InvokeRequired) dialog.BeginInvoke(updateUI);
                            else updateUI();
                        }
                        catch { }
                    };

                    cancelBtn.Click += (s2, e2) =>
                    {
                        cts?.Cancel();
                        updateNowBtn.Visible = true;
                        progressBar.Visible = false;
                        statusLabel.Visible = false;
                        cancelBtn.Visible = false;
                        statusLabel.Text = "";
                    };

                    string? extractedPath = await downloader.PrepareUpdateAsync(release, cts.Token);

                    if (!string.IsNullOrEmpty(extractedPath) && !cts.Token.IsCancellationRequested)
                    {
                        if (downloader.LaunchUpdater(extractedPath))
                        {
                            Services.LogService.Log("Updater launched, closing application for update", "INFO", "UpdateDialogs");
                            dialog.DialogResult = DialogResult.OK;
                            Application.Exit();
                        }
                        else
                        {
                            updateNowBtn.Visible = true;
                            progressBar.Visible = false;
                            cancelBtn.Visible = false;
                            statusLabel.Text = Services.LanguageManager.Current.UpdateDownloadFailed ?? "Update failed. Try another method.";
                        }
                    }
                    else if (!cts.Token.IsCancellationRequested)
                    {
                        updateNowBtn.Visible = true;
                        progressBar.Visible = false;
                        cancelBtn.Visible = false;
                        statusLabel.Visible = true;
                        statusLabel.Text = Services.LanguageManager.Current.UpdateDownloadFailed ?? "Download failed. Try another method.";
                    }
                }
                catch (Exception ex)
                {
                    Services.LogService.Log($"In-app update error: {ex.Message}", "ERROR", "UpdateDialogs");
                    updateNowBtn.Visible = true;
                    progressBar.Visible = false;
                    cancelBtn.Visible = false;
                    statusLabel.Visible = true;
                    statusLabel.Text = "Error: " + ex.Message;
                }
            };

            contentPanel.Controls.Add(inAppBox);
            yPos += 100;

            // === Method 1: PowerShell ===
            var method1Box = new Panel
            {
                Location = new Point(20, yPos),
                Size = new Size(contentPanel.Width - 60, 70),
                BackColor = Color.FromArgb(32, 34, 37),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            AddLabel(method1Box, Services.LanguageManager.Current.UpdateMethodPs, new Point(10, 8), new Font("Segoe UI", 9, FontStyle.Bold), Color.FromArgb(87, 242, 135));
            var cmdText = "irm https://bit.ly/geetrpcs | iex";
            var cmdBox = new TextBox
            {
                Text = cmdText,
                Location = new Point(10, 32),
                Size = new Size(method1Box.Width - 100, 25),
                BackColor = Color.FromArgb(47, 49, 54),
                ForeColor = Color.FromArgb(220, 221, 222),
                BorderStyle = BorderStyle.FixedSingle,
                ReadOnly = true,
                Font = new Font("Consolas", 9)
            };
            method1Box.Controls.Add(cmdBox);
            var copyBtn = CreateButton(Services.LanguageManager.Current.BtnCopy, Color.FromArgb(79, 84, 92), new Size(70, 24));
            copyBtn.Font = new Font("Segoe UI", 8, FontStyle.Bold);
            copyBtn.Location = new Point(method1Box.Width - 80, 31);
            copyBtn.Click += (s, e) => {
                try
                {
                    var thread = new Thread(() => {
                        try { Clipboard.SetText(cmdText); } catch { }
                    });
                    thread.SetApartmentState(ApartmentState.STA);
                    thread.Start();
                    thread.Join();
                    copyBtn.Text = Services.LanguageManager.Current.BtnCopied;
                    Task.Delay(2000).ContinueWith(_ => copyBtn.Invoke((Action)(() => copyBtn.Text = Services.LanguageManager.Current.BtnCopy)));
                }
                catch (Exception ex)
                {
                    Services.LogService.Log($"Failed to copy to clipboard: {ex.Message}", "ERROR", "UpdateDialogs");
                }
            };
            method1Box.Controls.Add(copyBtn);
            contentPanel.Controls.Add(method1Box);
            yPos += 80;

            // === Method 2: GitHub Releases ===
            var method2Box = new Panel
            {
                Location = new Point(20, yPos),
                Size = new Size(contentPanel.Width - 60, 50),
                BackColor = Color.FromArgb(32, 34, 37),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            AddLabel(method2Box, Services.LanguageManager.Current.UpdateMethodGithub, new Point(10, 15), new Font("Segoe UI", 9, FontStyle.Bold), Color.FromArgb(185, 187, 190));
            var githubLinkBtn = CreateButton(Services.LanguageManager.Current.BtnOpenLink, Color.FromArgb(79, 84, 92), new Size(110, 24));
            githubLinkBtn.Font = new Font("Segoe UI", 8, FontStyle.Bold);
            githubLinkBtn.Location = new Point(method2Box.Width - 120, 13);
            githubLinkBtn.Click += (s, e) => {
                try { Process.Start(new ProcessStartInfo { FileName = downloadUrl, UseShellExecute = true }); } catch { }
            };
            method2Box.Controls.Add(githubLinkBtn);
            contentPanel.Controls.Add(method2Box);

            dialog.Controls.Add(contentPanel);
            var closeBtn = CreateButton(Services.LanguageManager.Current.BtnClose, Color.FromArgb(79, 84, 92), new Size(130, 38));
            closeBtn.Click += (s, e) => dialog.DialogResult = DialogResult.Cancel;
            AddButtonPanel(dialog, closeBtn);
            dialog.ShowDialog();
        }

        internal static bool ShowAppsUpdateDialog(string remoteVersion)
        {
            using var dialog = CreateBaseDialog(Services.LanguageManager.Current.UpdateAppsAvailableTitle, new Size(450, 350));
            AddHeaderPanel(dialog, "📦", Services.LanguageManager.Current.UpdateAppsAvailableMessage, null!,
                Color.FromArgb(250, 168, 26), Color.FromArgb(250, 168, 26), Color.FromArgb(255, 188, 66));
            var contentPanel = CreateContentPanel(dialog);
            var versionBox = new Panel
            {
                Location = new Point(20, 20),
                Size = new Size(contentPanel.Width - 40, 70),
                BackColor = Color.FromArgb(32, 34, 37),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            AddLabel(versionBox, Services.LanguageManager.Current.UpdateAppsLatestVersion, new Point(15, 15), new Font("Segoe UI", 9, FontStyle.Bold), Color.FromArgb(185, 187, 190));
            AddLabel(versionBox, $"v{remoteVersion}", new Point(15, 38), new Font("Segoe UI", 12, FontStyle.Bold), Color.FromArgb(250, 168, 26));
            contentPanel.Controls.Add(versionBox);
            var infoLabel = new Label
            {
                Text = "A new update for supported applications is available!\nThis update doesn't require restarting geetRPCS.",
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(185, 187, 190),
                Location = new Point(20, 110),
                Size = new Size(contentPanel.Width - 40, 50),
                TextAlign = ContentAlignment.TopCenter
            };
            contentPanel.Controls.Add(infoLabel);
            dialog.Controls.Add(contentPanel);
            var updateBtn = CreateButton(Services.LanguageManager.Current.BtnUpdateNow, Color.FromArgb(87, 242, 135), new Size(160, 38));
            var closeBtn = CreateButton(Services.LanguageManager.Current.BtnClose, Color.FromArgb(79, 84, 92), new Size(100, 38));
            bool result = false;
            updateBtn.Click += (s, e) => { result = true; dialog.DialogResult = DialogResult.OK; };
            closeBtn.Click += (s, e) => dialog.DialogResult = DialogResult.Cancel;
            AddButtonPanel(dialog, closeBtn, updateBtn);
            dialog.ShowDialog();
            return result;
        }

        internal static bool ShowWittyUpdateDialog(string remoteVersion)
        {
            using var dialog = CreateBaseDialog(Services.LanguageManager.Current.UpdateWittyAvailableTitle ?? "Witty Texts Update", new Size(450, 350));
            AddHeaderPanel(dialog, "💬", Services.LanguageManager.Current.UpdateWittyAvailableMessage ?? "🎉 New Witty Texts Available!", null!,
                Color.FromArgb(114, 137, 218), Color.FromArgb(114, 137, 218), Color.FromArgb(144, 167, 248));
            var contentPanel = CreateContentPanel(dialog);
            var versionBox = new Panel
            {
                Location = new Point(20, 20),
                Size = new Size(contentPanel.Width - 40, 70),
                BackColor = Color.FromArgb(32, 34, 37),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            AddLabel(versionBox, Services.LanguageManager.Current.UpdateWittyLatestVersion ?? "Latest Version:", new Point(15, 15), new Font("Segoe UI", 9, FontStyle.Bold), Color.FromArgb(185, 187, 190));
            AddLabel(versionBox, $"v{remoteVersion}", new Point(15, 38), new Font("Segoe UI", 12, FontStyle.Bold), Color.FromArgb(114, 137, 218));
            contentPanel.Controls.Add(versionBox);
            var infoLabel = new Label
            {
                Text = "New witty texts are available for your Discord presence!\nThis update doesn't require restarting geetRPCS.",
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(185, 187, 190),
                Location = new Point(20, 110),
                Size = new Size(contentPanel.Width - 40, 50),
                TextAlign = ContentAlignment.TopCenter
            };
            contentPanel.Controls.Add(infoLabel);
            dialog.Controls.Add(contentPanel);
            var updateBtn = CreateButton(Services.LanguageManager.Current.BtnUpdateNow, Color.FromArgb(87, 242, 135), new Size(160, 38));
            var closeBtn = CreateButton(Services.LanguageManager.Current.BtnClose, Color.FromArgb(79, 84, 92), new Size(100, 38));
            bool result = false;
            updateBtn.Click += (s, e) => { result = true; dialog.DialogResult = DialogResult.OK; };
            closeBtn.Click += (s, e) => dialog.DialogResult = DialogResult.Cancel;
            AddButtonPanel(dialog, closeBtn, updateBtn);
            dialog.ShowDialog();
            return result;
        }

        internal static void ShowUpToDateDialog()
        {
            using var dialog = CreateBaseDialog(Services.LanguageManager.Current.DialogUpToDateTitle ?? "✅ You're Up to Date!", new Size(450, 280));
            AddHeaderPanel(dialog, "✅", Services.LanguageManager.Current.DialogUpToDateTitle ?? "You're Up to Date!", null!,
                Color.FromArgb(87, 242, 135), Color.FromArgb(87, 242, 135), Color.FromArgb(67, 181, 129));
            var contentPanel = CreateContentPanel(dialog);
            var versionBox = new Panel
            {
                Location = new Point(20, 15),
                Size = new Size(contentPanel.Width - 40, 60),
                BackColor = Color.FromArgb(32, 34, 37),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            AddLabel(versionBox, Services.LanguageManager.Current.UpdateDialogCurrentVersion ?? "📦 Current Version:", new Point(15, 12), new Font("Segoe UI", 9, FontStyle.Bold), Color.FromArgb(185, 187, 190));
            AddLabel(versionBox, $"v{CURRENT_VERSION}", new Point(15, 32), new Font("Segoe UI", 13, FontStyle.Bold), Color.FromArgb(87, 242, 135));
            contentPanel.Controls.Add(versionBox);
            var infoLabel = new Label
            {
                Text = Services.LanguageManager.Current.UpdateDialogUpToDateMessage ?? "You have the latest version of geetRPCS installed.\nEnjoy your productivity! 🚀",
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(185, 187, 190),
                Location = new Point(20, 90),
                Size = new Size(contentPanel.Width - 40, 40),
                TextAlign = ContentAlignment.TopCenter
            };
            contentPanel.Controls.Add(infoLabel);
            dialog.Controls.Add(contentPanel);
            var okBtn = CreateButton(Services.LanguageManager.Current.UpdateBtnAwesome ?? "👍 Awesome!", Color.FromArgb(87, 242, 135), new Size(140, 38));
            okBtn.Click += (s, e) => dialog.DialogResult = DialogResult.OK;
            AddButtonPanel(dialog, okBtn);
            dialog.ShowDialog();
        }

        #region ----- UI Helpers -----
        internal static string FormatReleaseNotes(string notes)
        {
            if (string.IsNullOrEmpty(notes)) return "No release notes available.";
            if (notes.Length > 800)
            {
                notes = notes.Substring(0, 800) + "...\n\n[View full changelog on GitHub]";
            }
            return notes;
        }
        private static Form CreateBaseDialog(string title, Size size)
        {
            var dialog = new Form
            {
                Text = title,
                Size = size,
                MinimumSize = new Size(size.Width - 50, size.Height - 70),
                StartPosition = FormStartPosition.CenterScreen,
                BackColor = Color.FromArgb(47, 49, 54),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                ShowInTaskbar = false,
                Font = new Font("Segoe UI", 9)
            };
            try
            {
                string iconPath = Utils.AppPaths.IconPath;
                if (File.Exists(iconPath)) dialog.Icon = new Icon(iconPath);
            }
            catch (Exception ex) { Services.LogService.Log($"Failed to load dialog icon: {ex.Message}", "WARNING", "UpdateDialogs"); }
            return dialog;
        }
        private static void AddHeaderPanel(Form dialog, string icon, string title, string subtitle, Color bg, Color gradStart, Color gradEnd)
        {
            var header = new Panel { Dock = DockStyle.Top, Height = 100, BackColor = bg };
            header.Paint += (s, e) =>
            {
                using var brush = new LinearGradientBrush(header.ClientRectangle, gradStart, gradEnd, 45f);
                e.Graphics.FillRectangle(brush, header.ClientRectangle);
            };
            AddLabel(header, icon, new Point(20, 30), new Font("Segoe UI Emoji", 28), Color.White, new Size(50, 50));
            AddLabel(header, title, new Point(80, 25), new Font("Segoe UI", 16, FontStyle.Bold), Color.White, new Size(450, 30));
            if (!string.IsNullOrEmpty(subtitle))
                AddLabel(header, subtitle, new Point(80, 55), new Font("Segoe UI", 9), Color.FromArgb(220, 221, 222), new Size(450, 20));
            dialog.Controls.Add(header);
        }
        private static Panel CreateContentPanel(Form dialog)
        {
            return new Panel
            {
                Location = new Point(0, 100),
                Size = new Size(dialog.ClientSize.Width, dialog.ClientSize.Height - 160),
                AutoScroll = false,
                BackColor = Color.FromArgb(47, 49, 54),
                Padding = new Padding(20),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
        }
        private static void AddLabel(Control parent, string text, Point loc, Font font, Color color, Size? size = null)
        {
            var lbl = new Label
            {
                Text = text,
                Location = loc,
                Font = font,
                ForeColor = color,
                AutoSize = size == null,
                BackColor = Color.Transparent
            };
            if (size != null)
            {
                lbl.Size = size.Value;
                lbl.TextAlign = ContentAlignment.MiddleLeft;
                if (size.Value.Width == 50) lbl.TextAlign = ContentAlignment.MiddleCenter; // Hack for icon
            }
            parent.Controls.Add(lbl);
        }
        private static Button CreateButton(string text, Color color, Size size)
        {
            var btn = new Button
            {
                Text = text,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Size = size,
                BackColor = color,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Right
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.MouseEnter += (s, e) => btn.BackColor = ControlPaint.Light(color);
            btn.MouseLeave += (s, e) => btn.BackColor = color;
            return btn;
        }
        private static void AddButtonPanel(Form dialog, params Button[] buttons)
        {
            var panel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 60,
                BackColor = Color.FromArgb(32, 34, 37),
                Padding = new Padding(20, 10, 20, 10)
            };
            int x = panel.Width - 20;
            foreach (var btn in buttons.Reverse())
            {
                if (btn.Text == "⏰ Remind Me Later")
                {
                    btn.Location = new Point(20, 11);
                    btn.Anchor = AnchorStyles.Left;
                }
                else
                {
                    x -= btn.Width + 10;
                    btn.Location = new Point(x + 10, 11);
                }
                panel.Controls.Add(btn);
            }
            dialog.Controls.Add(panel);
        }
        #endregion
    }
}
