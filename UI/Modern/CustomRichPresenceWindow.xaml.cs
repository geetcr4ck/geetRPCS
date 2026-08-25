/**
 * geetRPCS - Custom Rich Presence editor (ModernWpf / Fluent)
 * The one-stop GUI for building your own Rich Presence without opening an
 * editor: idle/active texts, elapsed-time toggle, buttons and (advanced,
 * collapsed by default) your own Discord Application ID with the tutorial
 * and asset-pack links — the fields config.json holds, so users never need
 * to edit JSON or open a text editor. Only the edited fields are mutated;
 * Assets pass through untouched.
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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using geetRPCS.Models;
using geetRPCS.Services;

namespace geetRPCS.UI.Modern
{
    public partial class CustomRichPresenceWindow : Window
    {
        private const string AssetsUrl = "https://github.com/geetcr4ck/geetRPCS/raw/main/AssetPack.zip";

        private readonly Config _current;

        /// <summary>The updated config to persist when Save is clicked (null when canceled).</summary>
        internal Config Result { get; private set; }

        // Test accessors (InternalsVisibleTo: Tests)
        internal string IdleDetails { get => IdleDetailsBox.Text; set => IdleDetailsBox.Text = value; }
        internal string IdleState { get => IdleStateBox.Text; set => IdleStateBox.Text = value; }
        internal string ActiveDetails { get => ActiveDetailsBox.Text; set => ActiveDetailsBox.Text = value; }
        internal string ActiveState { get => ActiveStateBox.Text; set => ActiveStateBox.Text = value; }
        internal string AppIdText { get => AppIdBox.Text; set => AppIdBox.Text = value; }
        internal bool IsSaveEnabled => SaveButton.IsEnabled;
        internal bool IsInvalidButtonsVisible => InvalidButtonsText.Visibility == Visibility.Visible;
        internal bool IsAppIdErrorVisible => AppIdErrorText.Visibility == Visibility.Visible;
        internal bool IsAppIdWarningVisible => AppIdWarningPanel.Visibility == Visibility.Visible;
        internal void SetButton1(string label, string url) { Button1LabelBox.Text = label; Button1UrlBox.Text = url; }
        internal void SetButton2(string label, string url) { Button2LabelBox.Text = label; Button2UrlBox.Text = url; }
        internal void SetTimestamps(bool? value) => TimestampsCheck.IsChecked = value;

        // Placeholder chips insert into the template box the user touched last
        // (ActiveDetails by default), so the click is never a no-op.
        private TextBox _lastFocusedTemplate;

        public CustomRichPresenceWindow(Config current)
        {
            InitializeComponent();

            _current = current ?? AppCoordinator.GetDefaultConfig();
            _lastFocusedTemplate = ActiveDetailsBox;

            Title = LanguageManager.Current.WindowPresenceTitle ?? "Custom Rich Presence";
            IdleSectionText.Text = LanguageManager.Current.PresenceIdleSection ?? "Idle (no app active)";
            ActiveSectionText.Text = LanguageManager.Current.PresenceActiveSection ?? "Active (app detected)";
            IdleDetailsLabel.Text = ActiveDetailsLabel.Text = LanguageManager.Current.LabelDetails ?? "Details";
            IdleStateLabel.Text = ActiveStateLabel.Text = LanguageManager.Current.LabelState ?? "State";
            PlaceholdersHint.Text = LanguageManager.Current.PresencePlaceholdersHint ?? "Placeholders — click to insert:";
            TimestampsCheck.Content = LanguageManager.Current.PresenceShowTimestamps ?? "Show elapsed time";
            ButtonsSectionText.Text = LanguageManager.Current.PresenceButtonsSection ?? "Buttons (max 2)";
            ButtonLabelHeader.Text = LanguageManager.Current.PresenceButtonLabel ?? "Label";
            ButtonUrlHeader.Text = LanguageManager.Current.PresenceButtonUrl ?? "URL";
            InvalidButtonsText.Text = LanguageManager.Current.PresenceInvalidButtons
                ?? "Each filled button needs a label (1-32 chars) and an http(s) URL.";
            AppIdSectionText.Text = LanguageManager.Current.PresenceAppidSection ?? "Discord Application ID (advanced)";
            AppIdErrorText.Text = LanguageManager.Current.ErrorInvalidAppId
                ?? "Application ID must be 17-20 digits (numbers only).";
            TutorialRun.Text = LanguageManager.Current.LinkTutorial ?? "Read Tutorial";
            AssetsRun.Text = LanguageManager.Current.LinkDownloadAssets ?? "Download Asset Pack (Required)";
            CancelButton.Content = LanguageManager.Current.BtnCancel ?? "Cancel";
            ResetButton.Content = LanguageManager.Current.BtnResetDefault ?? "Reset Default";
            SaveButton.Content = LanguageManager.Current.BtnSave ?? "Save";

            // The localized change-app-id message is "instruction\n\nWARNING: ..."
            // (same key the old Change App ID dialog used): split it so the
            // warning renders as its own callout instead of a wall of text.
            string msg = LanguageManager.Current.DialogChangeAppIdMessage ?? "";
            string description = msg, warning = null;
            int split = msg.IndexOf("\n\n", StringComparison.Ordinal);
            if (split >= 0)
            {
                description = msg.Substring(0, split).Trim();
                warning = msg.Substring(split + 2).Trim();
            }
            AppIdDescriptionText.Text = description;
            if (!string.IsNullOrEmpty(warning))
            {
                AppIdWarningPanel.Visibility = Visibility.Visible;
                AppIdWarningText.Text = warning;
            }

            LoadFromConfig(_current);

            try
            {
                string iconPath = Utils.AppPaths.IconPath;
                if (File.Exists(iconPath)) Icon = LoadIcon(iconPath);
            }
            catch { }

            // Same deferred-activation pattern as the other tray dialogs: opened
            // from the tray while the menu is still closing, so focus lands once
            // the pump settles and the user can type immediately.
            Loaded += (s, e) =>
            {
                var focusTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
                focusTimer.Tick += (s2, e2) =>
                {
                    focusTimer.Stop();
                    WindowActivation.ForceForeground(this);
                    IdleDetailsBox.Focus();
                    Keyboard.Focus(IdleDetailsBox);
                };
                focusTimer.Start();
            };

            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape) { Close(); e.Handled = true; }
            };
        }

        private void LoadFromConfig(Config cfg)
        {
            var d = cfg.Discord;
            IdleDetailsBox.Text = d?.Details ?? "";
            IdleStateBox.Text = d?.State ?? "";
            ActiveDetailsBox.Text = d?.ActiveDetails ?? "";
            ActiveStateBox.Text = d?.ActiveState ?? "";
            AppIdBox.Text = d?.ApplicationId ?? "";
            TimestampsCheck.IsChecked = d?.ShowTimestamps ?? true;
            var buttons = d?.Buttons;
            Button1LabelBox.Text = buttons != null && buttons.Length > 0 ? buttons[0].Label ?? "" : "";
            Button1UrlBox.Text = buttons != null && buttons.Length > 0 ? buttons[0].Url ?? "" : "";
            Button2LabelBox.Text = buttons != null && buttons.Length > 1 ? buttons[1].Label ?? "" : "";
            Button2UrlBox.Text = buttons != null && buttons.Length > 1 ? buttons[1].Url ?? "" : "";
            Validate();
        }

        /// <summary>A button row is "filled" when either field has text; filled
        /// rows need both a 1-32 label and a valid http(s) URL (PresenceBuilder
        /// would silently drop anything else — surface it here instead).</summary>
        private (bool Valid, ButtonConfig[] Buttons) BuildButtons()
        {
            bool filled1 = Button1LabelBox.Text.Trim().Length > 0 || Button1UrlBox.Text.Trim().Length > 0;
            bool filled2 = Button2LabelBox.Text.Trim().Length > 0 || Button2UrlBox.Text.Trim().Length > 0;
            bool valid = true;
            var list = new System.Collections.Generic.List<ButtonConfig>();
            if (filled1)
            {
                string label = Button1LabelBox.Text.Trim(), url = Button1UrlBox.Text.Trim();
                if (label.Length == 0 || label.Length > 32 || !PresenceBuilder.IsValidUrl(url)) valid = false;
                else list.Add(new ButtonConfig { Label = label, Url = url });
            }
            if (filled2)
            {
                string label = Button2LabelBox.Text.Trim(), url = Button2UrlBox.Text.Trim();
                if (label.Length == 0 || label.Length > 32 || !PresenceBuilder.IsValidUrl(url)) valid = false;
                else list.Add(new ButtonConfig { Label = label, Url = url });
            }
            return (valid, list.Count > 0 ? list.ToArray() : null);
        }

        private bool ValidateAppId()
        {
            // Required: config.json always carries a valid Application ID (the
            // field is pre-filled, so an invalid state is always user-made).
            bool valid = AppCoordinator.IsValidApplicationId(AppIdBox.Text);
            AppIdErrorText.Visibility = valid ? Visibility.Collapsed : Visibility.Visible;
            return valid;
        }

        private void Validate()
        {
            var (validButtons, _) = BuildButtons();
            InvalidButtonsText.Visibility = validButtons ? Visibility.Collapsed : Visibility.Visible;
            SaveButton.IsEnabled = validButtons && ValidateAppId();
        }

        private void OnFieldChanged(object sender, RoutedEventArgs e) => Validate();

        private void OnTemplateFocus(object sender, KeyboardFocusChangedEventArgs e)
            => _lastFocusedTemplate = sender as TextBox ?? ActiveDetailsBox;

        private void OnPlaceholderClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Content is string ph)
            {
                var box = _lastFocusedTemplate ?? ActiveDetailsBox;
                int caret = box.CaretIndex;
                box.Text = box.Text.Insert(caret, ph);
                box.CaretIndex = caret + ph.Length;
                box.Focus();
                Keyboard.Focus(box);
            }
        }

        private void OnResetClick(object sender, RoutedEventArgs e)
            => LoadFromConfig(AppCoordinator.GetDefaultConfig());

        private void OnTutorialLinkClick(object sender, RoutedEventArgs e)
            => OpenUrl(LanguageManager.Current.UrlTutorial);

        private void OnAssetsLinkClick(object sender, RoutedEventArgs e)
            => OpenUrl(AssetsUrl);

        private static void OpenUrl(string url)
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
                MessageDialog.ShowError(LanguageManager.Current.ErrorOpenLink + " " + ex.Message, "Error");
            }
        }

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            if (!SaveButton.IsEnabled) return;
            Result = BuildResult();
            if (Result == null) return;
            DialogResult = true;
        }

        /// <summary>Builds the config to persist (clone of the current one with
        /// only the edited fields mutated). Separate from OnSaveClick so tests
        /// can exercise the mapping without a dialog result.</summary>
        internal Config BuildResult()
        {
            var (validButtons, buttons) = BuildButtons();
            if (!validButtons || !ValidateAppId()) return null;

            var d = _current.Discord ?? AppCoordinator.GetDefaultConfig().Discord;
            return new Config
            {
                Discord = new DiscordConfig
                {
                    ApplicationId = AppIdBox.Text.Trim(),
                    Details = NullIfEmpty(IdleDetailsBox.Text),
                    State = NullIfEmpty(IdleStateBox.Text),
                    ActiveDetails = NullIfEmpty(ActiveDetailsBox.Text),
                    ActiveState = NullIfEmpty(ActiveStateBox.Text),
                    Assets = d.Assets,
                    Buttons = buttons,
                    ShowTimestamps = TimestampsCheck.IsChecked == null ? (bool?)null : TimestampsCheck.IsChecked.Value
                }
            };
        }

        private static string NullIfEmpty(string s)
            => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        private static ImageSource LoadIcon(string path)
        {
            using var icon = new System.Drawing.Icon(path);
            return Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        }
    }
}
