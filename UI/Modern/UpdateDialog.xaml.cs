/**
 * geetRPCS - Update dialog (ModernWpf / Fluent)
 * WPF replacement for the four WinForms dialogs that used to live in
 * UI/UpdateDialogs.cs. UI/UpdateDialogs keeps its public surface as a thin
 * shim so Program.cs and UpdateChecker.cs stay unchanged. The enhanced
 * dialog drives the same UpdateDownloader flow: progress bar + ETA, cancel,
 * and launching the updater before exiting the app.
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
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using geetRPCS.Services;

#nullable enable

namespace geetRPCS.UI.Modern
{
    public partial class UpdateDialog : Window
    {
        private const string PowerShellCommand = "irm https://bit.ly/geetrpcs | iex";

        private UpdateChecker.GitHubRelease? _release;
        private string _downloadUrl = "";
        private CancellationTokenSource? _cts;

        public UpdateDialog()
        {
            InitializeComponent();
            CloseFooterBtn.Content = LanguageManager.Current.BtnClose ?? "Close";
            try
            {
                string iconPath = Utils.AppPaths.IconPath;
                if (File.Exists(iconPath)) Icon = LoadIcon(iconPath);
            }
            catch { }
        }

        // ----------------------------------------------------------------
        // Static factories (called by the UI.UpdateDialogs shim)
        // ----------------------------------------------------------------
        internal static void ShowEnhanced(UpdateChecker.GitHubRelease release)
            => CreateEnhanced(release).ShowDialog();

        internal static bool ShowApps(string remoteVersion)
            => CreateApps(remoteVersion).ShowDialog() == true;

        internal static bool ShowWitty(string remoteVersion)
            => CreateWitty(remoteVersion).ShowDialog() == true;

        internal static void ShowUpToDate()
            => CreateUpToDate().ShowDialog();

        internal static UpdateDialog CreateEnhanced(UpdateChecker.GitHubRelease release)
        {
            var win = new UpdateDialog();
            win.ConfigureEnhanced(release);
            return win;
        }

        internal static UpdateDialog CreateApps(string remoteVersion)
        {
            var win = new UpdateDialog();
            win.ConfigureSimple("📦",
                LanguageManager.Current.UpdateAppsAvailableTitle,
                LanguageManager.Current.UpdateAppsAvailableMessage,
                LanguageManager.Current.UpdateAppsLatestVersion,
                $"v{remoteVersion}",
                LanguageManager.Current.UpdateAppsAvailableBody,
                "SystemFillColorCautionBrush",
                LanguageManager.Current.BtnUpdateNow);
            return win;
        }

        internal static UpdateDialog CreateWitty(string remoteVersion)
        {
            var win = new UpdateDialog();
            win.ConfigureSimple("💬",
                LanguageManager.Current.UpdateWittyAvailableTitle ?? "Witty Texts Update",
                LanguageManager.Current.UpdateWittyAvailableMessage ?? "🎉 New Witty Texts Available!",
                LanguageManager.Current.UpdateWittyLatestVersion ?? "Latest Version:",
                $"v{remoteVersion}",
                LanguageManager.Current.UpdateWittyAvailableBody,
                "AccentFillColorDefaultBrush",
                LanguageManager.Current.BtnUpdateNow);
            return win;
        }

        internal static UpdateDialog CreateUpToDate()
        {
            var win = new UpdateDialog();
            win.ConfigureSimple("✅",
                LanguageManager.Current.DialogUpToDateTitle ?? "You're Up to Date!",
                LanguageManager.Current.DialogUpToDateTitle ?? "You're Up to Date!",
                LanguageManager.Current.UpdateDialogCurrentVersion ?? "📦 Current Version:",
                $"v{Utils.AppVersion.VersionText}",
                LanguageManager.Current.UpdateDialogUpToDateMessage ?? "You have the latest version of geetRPCS installed.\nEnjoy your productivity! 🚀",
                "SystemFillColorSuccessBrush",
                LanguageManager.Current.UpdateBtnAwesome ?? "👍 Awesome!",
                showClose: false);
            return win;
        }

        internal static string FormatReleaseNotes(string? notes)
        {
            if (string.IsNullOrEmpty(notes)) return LanguageManager.Current.UpdateNoReleaseNotes;
            if (notes.Length > 800)
            {
                notes = notes.Substring(0, 800) + "...\n\n[View full changelog on GitHub]";
            }
            return notes;
        }

        // ----------------------------------------------------------------
        // Configuration
        // ----------------------------------------------------------------
        private void ConfigureHeader(string icon, string title, string? subtitle)
        {
            HeaderIcon.Text = icon;
            HeaderTitle.Text = FluentGlyphs.StripLeadingEmoji(title ?? "");
            HeaderSubtitle.Text = subtitle ?? "";
            HeaderSubtitle.Visibility = string.IsNullOrEmpty(subtitle)
                ? Visibility.Collapsed : Visibility.Visible;
        }

        private void ConfigureEnhanced(UpdateChecker.GitHubRelease release)
        {
            _release = release;
            _downloadUrl = release.HtmlUrl ?? "https://github.com/geetcr4ck/geetRPCS/releases";
            string latestVersion = release.TagName?.TrimStart('v')
                ?? LanguageManager.Current.UpdateVersionUnknown;

            Title = LanguageManager.Current.UpdateAvailableTitle;
            ConfigureHeader("🎊",
                LanguageManager.Current.UpdateAvailableMessage,
                LanguageManager.Current.UpdateSubtitle);

            VersionLeftLabel.Text = LanguageManager.Current.UpdateCurrentVersion;
            VersionLeftValue.Text = $"v{Utils.AppVersion.VersionText}";
            VersionLeftValue.SetResourceReference(TextBlock.ForegroundProperty, "SystemFillColorCautionBrush");
            VersionRightLabel.Text = LanguageManager.Current.UpdateLatestVersion;
            VersionRightValue.Text = $"v{latestVersion}";
            VersionRightValue.SetResourceReference(TextBlock.ForegroundProperty, "SystemFillColorSuccessBrush");

            ReleaseDateText.Text = $"📅 {LanguageManager.Current.UpdateReleased} " +
                $"{release.PublishedAt:MMMM dd, yyyy 'at' HH:mm} UTC";
            ChangelogHeader.Text = LanguageManager.Current.UpdateChangelog;
            ChangelogBox.Text = FormatReleaseNotes(release.Body);
            HowToHeader.Text = LanguageManager.Current.UpdateHowTo;

            InAppTitle.Text = LanguageManager.Current.UpdateMethodInApp ?? "★ In-App Update (Recommended)";
            UpdateNowBtn.Content = LanguageManager.Current.BtnUpdateNow;
            CancelBtn.Content = LanguageManager.Current.BtnCancel ?? "Cancel";
            PsTitle.Text = LanguageManager.Current.UpdateMethodPs;
            CmdBox.Text = PowerShellCommand;
            CopyBtn.Content = LanguageManager.Current.BtnCopy;
            GithubTitle.Text = LanguageManager.Current.UpdateMethodGithub;
            OpenLinkBtn.Content = LanguageManager.Current.BtnOpenLink;

            PrimaryFooterBtn.Visibility = Visibility.Collapsed;
        }

        private void ConfigureSimple(string icon, string title, string headerTitle, string versionLabel,
            string versionValue, string body, string valueBrushKey,
            string primaryText, bool showClose = true)
        {
            Width = 520;
            Title = FluentGlyphs.StripLeadingEmoji(title);
            ConfigureHeader(icon, headerTitle, null);

            RightVersionGrid.Visibility = Visibility.Collapsed;
            VersionLeftLabel.Text = versionLabel;
            VersionLeftValue.Text = versionValue;
            VersionLeftValue.SetResourceReference(TextBlock.ForegroundProperty, valueBrushKey);
            InfoText.Text = body;
            InfoText.Visibility = Visibility.Visible;

            ReleaseDateText.Visibility = Visibility.Collapsed;
            ChangelogHeader.Visibility = Visibility.Collapsed;
            ChangelogBox.Visibility = Visibility.Collapsed;
            HowToHeader.Visibility = Visibility.Collapsed;
            InAppBox.Visibility = Visibility.Collapsed;
            PsBox.Visibility = Visibility.Collapsed;
            GithubBox.Visibility = Visibility.Collapsed;

            PrimaryFooterBtn.Content = primaryText;
            PrimaryFooterBtn.Visibility = Visibility.Visible;
            CloseFooterBtn.Visibility = showClose ? Visibility.Visible : Visibility.Collapsed;
        }

        // ----------------------------------------------------------------
        // Actions
        // ----------------------------------------------------------------
        private void OnPrimaryClick(object sender, RoutedEventArgs e) => DialogResult = true;

        private void OnCloseClick(object sender, RoutedEventArgs e) => DialogResult = false;

        private void OnOpenLinkClick(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = _downloadUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex) { LogService.Log($"Failed to open release link: {ex.Message}", "ERROR", "UpdateDialog"); }
        }

        private async void OnCopyClick(object sender, RoutedEventArgs e)
        {
            try { System.Windows.Clipboard.SetText(CmdBox.Text); }
            catch (Exception ex) { LogService.Log($"Failed to copy to clipboard: {ex.Message}", "ERROR", "UpdateDialog"); }
            CopyBtn.Content = LanguageManager.Current.BtnCopied ?? "✅ Copied";
            await Task.Delay(2000);
            CopyBtn.Content = LanguageManager.Current.BtnCopy ?? "📋 Copy";
        }

        private void OnCancelDownloadClick(object sender, RoutedEventArgs e)
        {
            _cts?.Cancel();
            ResetDownloadUi();
        }

        private async void OnUpdateNowClick(object sender, RoutedEventArgs e)
        {
            try
            {
                UpdateNowBtn.Visibility = Visibility.Collapsed;
                ProgressBar.Visibility = Visibility.Visible;
                CancelBtn.Visibility = Visibility.Visible;
                StatusText.Visibility = Visibility.Visible;
                ProgressBar.Value = 0;
                StatusText.Text = LanguageManager.Current.UpdatePreparing ?? "Preparing update...";

                _cts?.Dispose();
                _cts = new CancellationTokenSource();
                var downloader = new UpdateDownloader();

                downloader.OnProgressChanged += (percent, current, total, speed) =>
                {
                    try { Dispatcher.BeginInvoke(new Action(() => UpdateProgress(percent, current, total, speed))); }
                    catch { }
                };
                downloader.OnStatusChanged += (status) =>
                {
                    try { Dispatcher.BeginInvoke(new Action(() => { if (IsLoaded) StatusText.Text = status; })); }
                    catch { }
                };
                downloader.OnError += (error) =>
                {
                    try
                    {
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            MessageDialog.ShowError(error, LanguageManager.Current.UpdateErrorTitle);
                            ResetDownloadUi();
                        }));
                    }
                    catch { }
                };

                string? extractedPath = await downloader.PrepareUpdateAsync(_release!, _cts.Token);

                if (!string.IsNullOrEmpty(extractedPath) && !_cts.IsCancellationRequested)
                {
                    if (downloader.LaunchUpdater(extractedPath))
                    {
                        LogService.Log("Updater launched, closing application for update", "INFO", "UpdateDialog");
                        DialogResult = true;
                        // Defer the app exit until the modal frame has unwound.
                        _ = Dispatcher.BeginInvoke(new Action(() => System.Windows.Forms.Application.Exit()));
                    }
                    else
                    {
                        ResetDownloadUi();
                        StatusText.Text = LanguageManager.Current.UpdateDownloadFailed ?? "Update failed. Try another method.";
                    }
                }
                else if (!_cts.IsCancellationRequested)
                {
                    ResetDownloadUi();
                    StatusText.Text = LanguageManager.Current.UpdateDownloadFailed ?? "Download failed. Try another method.";
                }
            }
            catch (Exception ex)
            {
                LogService.Log($"In-app update error: {ex.Message}", "ERROR", "UpdateDialog");
                ResetDownloadUi();
                StatusText.Text = LanguageManager.Current.ErrorPrefix + ex.Message;
            }
        }

        private void UpdateProgress(int percent, long current, long total, double speed)
        {
            if (!IsLoaded) return;
            ProgressBar.Value = Math.Min(Math.Max(percent, 0), 100);
            double currentMB = current / 1024.0 / 1024.0;
            double totalMB = total / 1024.0 / 1024.0;
            double speedMBps = speed / 1024.0 / 1024.0;
            string etaStr = "";
            if (speed > 0 && total > current)
            {
                double remainingBytes = total - current;
                double etaSeconds = remainingBytes / speed;
                etaStr = etaSeconds < 60
                    ? $" | ETA: {etaSeconds:F0}s"
                    : $" | ETA: {etaSeconds / 60:F0}m {etaSeconds % 60:F0}s";
            }
            StatusText.Text = $"{currentMB:F1} / {totalMB:F1} MB @ {speedMBps:F2} MB/s{etaStr}";
        }

        private void ResetDownloadUi()
        {
            UpdateNowBtn.Visibility = Visibility.Visible;
            ProgressBar.Visibility = Visibility.Collapsed;
            CancelBtn.Visibility = Visibility.Collapsed;
            StatusText.Text = "";
        }

        protected override void OnClosed(EventArgs e)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            base.OnClosed(e);
        }

        private static ImageSource LoadIcon(string path)
        {
            using var icon = new System.Drawing.Icon(path);
            return Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        }

        // Test accessors (InternalsVisibleTo: Tests)
        internal string HeaderTitleText => HeaderTitle.Text;
        internal string VersionLeftValueText => VersionLeftValue.Text;
        internal string VersionRightValueText => VersionRightValue.Text;
        internal string ChangelogText => ChangelogBox.Text;
        internal bool IsInAppBoxVisible => InAppBox.Visibility == Visibility.Visible;
        internal bool IsInfoTextVisible => InfoText.Visibility == Visibility.Visible;
    }
}
