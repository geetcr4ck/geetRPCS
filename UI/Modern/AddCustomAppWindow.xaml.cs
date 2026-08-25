/**
 * geetRPCS - Add Custom App dialog (ModernWpf / Fluent)
 * GUI replacement for hand-editing apps.json: builds a full AppConfig entry
 * (process + match modes, optional window-title match, details template,
 * large image, timestamps, buttons, optional per-app Application ID) and
 * returns it for storage in settings.json customApps.
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
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
    public partial class AddCustomAppWindow : Window
    {
        private static readonly string[] MatchModes = { "Exact", "Contains", "StartsWith", "EndsWith", "Regex" };
        // Same shape TaskbarWatcher matches on: no .exe, no spaces, no path.
        private static readonly Regex ProcessNameRegex = new Regex("^[A-Za-z0-9._-]+$", RegexOptions.Compiled);

        private readonly List<string> _existingProcesses;

        /// <summary>The entry to persist when Add is clicked (null when canceled).</summary>
        internal AppConfig Result { get; private set; }

        // Test accessors (InternalsVisibleTo: Tests)
        internal string ProcessText { get => ProcessBox.Text; set => ProcessBox.Text = value; }
        internal bool IsAddEnabled => AddButton.IsEnabled;
        internal bool IsProcessErrorVisible => ProcessErrorText.Visibility == Visibility.Visible;

        public AddCustomAppWindow(List<string> existingProcesses)
        {
            InitializeComponent();

            _existingProcesses = existingProcesses ?? new List<string>();

            Title = LanguageManager.Current.WindowAddAppTitle ?? "Add Custom App";
            ProcessLabel.Text = LanguageManager.Current.AddAppProcess ?? "Process name (without .exe)";
            ProcessHint.Text = LanguageManager.Current.AddAppProcessHint ?? "Find it in Task Manager → Details (e.g. notepad).";
            NameLabel.Text = LanguageManager.Current.AddAppName ?? "Display name";
            MatchModeLabel.Text = LanguageManager.Current.AddAppMatchMode ?? "Process match mode";
            ClientIdLabel.Text = LanguageManager.Current.LabelClientId ?? "Application ID (optional)";
            ProcessErrorText.Text = LanguageManager.Current.AddAppInvalidProcess
                ?? "Enter a process name (letters, digits, dot, dash, underscore).";
            WindowTitleSection.Text = LanguageManager.Current.AddAppWindowTitle ?? "Window title (optional)";
            WindowTitleLabel.Text = LanguageManager.Current.AddAppWindowTitle ?? "Window title (optional)";
            TitleMatchLabel.Text = LanguageManager.Current.AddAppTitleMatchMode ?? "Title match mode";
            DetailsLabel.Text = LanguageManager.Current.AddAppDetails ?? "Details template (optional)";
            PlaceholdersHint.Text = LanguageManager.Current.PresencePlaceholdersHint ?? "Placeholders — click to insert:";
            LargeKeyLabel.Text = LanguageManager.Current.LabelLargeKey ?? "Large Image Key";
            LargeTextLabel.Text = LanguageManager.Current.LabelLargeText ?? "Large Image Text";
            TimestampsCheck.Content = LanguageManager.Current.PresenceShowTimestamps ?? "Show elapsed time";
            ButtonsSectionText.Text = LanguageManager.Current.PresenceButtonsSection ?? "Buttons (max 2)";
            InvalidButtonsText.Text = LanguageManager.Current.PresenceInvalidButtons
                ?? "Each filled button needs a label (1-32 chars) and an http(s) URL.";
            ClientIdErrorText.Text = LanguageManager.Current.ErrorInvalidAppId
                ?? "Application ID must be 17-20 digits (numbers only).";
            CancelButton.Content = LanguageManager.Current.BtnCancel ?? "Cancel";
            AddButton.Content = LanguageManager.Current.AddAppBtnAdd ?? "Add App";

            foreach (var m in MatchModes)
            {
                ProcessMatchBox.Items.Add(m);
                TitleMatchBox.Items.Add(m);
            }
            ProcessMatchBox.SelectedIndex = 0; // Exact
            TitleMatchBox.SelectedIndex = 1;   // Contains (the watcher default)
            TimestampsCheck.IsChecked = null;  // inherit
            Validate(); // empty process => Add stays disabled until valid

            try
            {
                string iconPath = Utils.AppPaths.IconPath;
                if (File.Exists(iconPath)) Icon = LoadIcon(iconPath);
            }
            catch { }

            Loaded += (s, e) =>
            {
                var focusTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
                focusTimer.Tick += (s2, e2) =>
                {
                    focusTimer.Stop();
                    WindowActivation.ForceForeground(this);
                    ProcessBox.Focus();
                    Keyboard.Focus(ProcessBox);
                };
                focusTimer.Start();
            };

            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape) { Close(); e.Handled = true; }
            };
        }

        private (bool ValidProcess, bool ValidButtons, bool ValidClientId) Validate()
        {
            string proc = NormalizeProcess(ProcessBox.Text);

            bool duplicate = _existingProcesses.Any(p =>
                string.Equals(p?.Trim(), proc, StringComparison.OrdinalIgnoreCase));
            bool validProcess = ProcessNameRegex.IsMatch(proc) && !duplicate;
            if (!ProcessNameRegex.IsMatch(proc))
                ProcessErrorText.Text = LanguageManager.Current.AddAppInvalidProcess
                    ?? "Enter a process name (letters, digits, dot, dash, underscore).";
            else if (duplicate)
                ProcessErrorText.Text = string.Format(
                    LanguageManager.Current.ErrorDuplicateProcess ?? "An app with process name '{0}' already exists.", proc);
            ProcessErrorText.Visibility = validProcess ? Visibility.Collapsed : Visibility.Visible;

            bool validButtons = true;
            foreach (var (label, url) in new[]
            {
                (Button1LabelBox.Text, Button1UrlBox.Text),
                (Button2LabelBox.Text, Button2UrlBox.Text)
            })
            {
                bool filled = !string.IsNullOrWhiteSpace(label) || !string.IsNullOrWhiteSpace(url);
                if (filled && (string.IsNullOrWhiteSpace(label) || label.Trim().Length > 32
                    || !PresenceBuilder.IsValidUrl(url)))
                { validButtons = false; break; }
            }
            InvalidButtonsText.Visibility = validButtons ? Visibility.Collapsed : Visibility.Visible;

            string clientId = ClientIdBox.Text?.Trim() ?? "";
            bool validClientId = clientId.Length == 0 || AppCoordinator.IsValidApplicationId(clientId);
            ClientIdErrorText.Visibility = validClientId ? Visibility.Collapsed : Visibility.Visible;

            AddButton.IsEnabled = validProcess && validButtons && validClientId;
            return (validProcess, validButtons, validClientId);
        }

        /// <summary>User input may include ".exe" or padding — normalize to the
        /// bare process name the watcher matches against.</summary>
        private static string NormalizeProcess(string raw)
        {
            string p = (raw ?? "").Trim();
            if (p.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) p = p[..^4];
            return p.Trim();
        }

        private void OnFieldChanged(object sender, RoutedEventArgs e) => Validate();

        private void OnPlaceholderClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Content is string ph)
            {
                int caret = DetailsBox.CaretIndex;
                DetailsBox.Text = DetailsBox.Text.Insert(caret, ph);
                DetailsBox.CaretIndex = caret + ph.Length;
                DetailsBox.Focus();
                Keyboard.Focus(DetailsBox);
            }
        }

        private void OnAddClick(object sender, RoutedEventArgs e)
        {
            var (validProcess, validButtons, validClientId) = Validate();
            if (!validProcess || !validButtons || !validClientId) return;

            string proc = NormalizeProcess(ProcessBox.Text);
            string name = string.IsNullOrWhiteSpace(NameBox.Text) ? proc : NameBox.Text.Trim();

            var buttons = new List<AppButtonConfig>(2);
            if (!string.IsNullOrWhiteSpace(Button1LabelBox.Text) && !string.IsNullOrWhiteSpace(Button1UrlBox.Text))
                buttons.Add(new AppButtonConfig { Label = Button1LabelBox.Text.Trim(), Url = Button1UrlBox.Text.Trim() });
            if (!string.IsNullOrWhiteSpace(Button2LabelBox.Text) && !string.IsNullOrWhiteSpace(Button2UrlBox.Text))
                buttons.Add(new AppButtonConfig { Label = Button2LabelBox.Text.Trim(), Url = Button2UrlBox.Text.Trim() });

            string clientId = ClientIdBox.Text?.Trim() ?? "";
            string windowTitle = WindowTitleBox.Text?.Trim() ?? "";

            Result = new AppConfig
            {
                Process = proc,
                AppName = name,
                WindowTitle = windowTitle.Length > 0 ? windowTitle : null,
                // Only persist non-default match modes, keeping settings.json lean.
                ProcessMatchMode = ProcessMatchBox.SelectedItem as string == "Exact" ? null : ProcessMatchBox.SelectedItem as string,
                TitleMatchMode = windowTitle.Length == 0 || TitleMatchBox.SelectedItem as string == "Contains"
                    ? null : TitleMatchBox.SelectedItem as string,
                CustomDetails = string.IsNullOrWhiteSpace(DetailsBox.Text) ? null : DetailsBox.Text.Trim(),
                LargeKey = string.IsNullOrWhiteSpace(LargeKeyBox.Text) ? null : LargeKeyBox.Text.Trim(),
                LargeText = string.IsNullOrWhiteSpace(LargeTextBox.Text) ? null : LargeTextBox.Text.Trim(),
                ShowTimestamps = TimestampsCheck.IsChecked,
                Buttons = buttons.Count > 0 ? buttons : null,
                ClientId = clientId.Length > 0 ? clientId : null
            };
            DialogResult = true;
        }

        private static ImageSource LoadIcon(string path)
        {
            using var icon = new System.Drawing.Icon(path);
            return Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        }
    }
}
