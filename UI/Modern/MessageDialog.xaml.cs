/**
 * geetRPCS - Message dialog (ModernWpf / Fluent)
 * The single Fluent message surface for the app: replaces the WinForms
 * InfoDialog / ConfirmDialog Forms and every remaining native MessageBox.
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
using System.Windows;
using geetRPCS.Services;

namespace geetRPCS.UI.Modern
{
    public partial class MessageDialog : Window
    {
        private bool _yesClicked;

        private MessageDialog(string message, string title, bool confirm)
        {
            InitializeComponent();

            Title = string.IsNullOrEmpty(title) ? LanguageManager.Current.AppName : title;
            MessageText.Text = message ?? "";
            if (confirm)
            {
                NoButton.Content = LanguageManager.Current.BtnNo ?? "No";
                YesButton.Content = LanguageManager.Current.BtnYes ?? "Yes";
                YesButton.Width = 96;
                ShowSeverityBar("SystemFillColorCautionBrush");
            }
            else
            {
                NoButton.Visibility = Visibility.Collapsed;
                YesButton.Content = LanguageManager.Current.BtnOk ?? "OK";
                YesButton.Width = 96;
            }
        }

        private void ShowSeverityBar(string brushKey)
        {
            SeverityBar.Visibility = Visibility.Visible;
            SeverityBar.SetResourceReference(BackgroundProperty, brushKey);
        }

        private void OnYesClick(object sender, RoutedEventArgs e)
        {
            _yesClicked = true;
            Close();
        }

        private void OnNoClick(object sender, RoutedEventArgs e)
        {
            _yesClicked = false;
            Close();
        }

        /// <summary>Runs the action on the WPF UI thread, blocking until done,
        /// so dialogs can be shown from background call sites (the old WinForms
        /// dialogs allowed the same).</summary>
        private static void OnUiThread(Action action)
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess()) action();
            else dispatcher.Invoke(action);
        }

        /// <summary>Information dialog with a single OK button.</summary>
        public static void ShowInfo(string message, string title = null)
            => OnUiThread(() => new MessageDialog(message, title, confirm: false).ShowDialog());

        /// <summary>Error dialog: single OK button plus a critical accent bar.</summary>
        public static void ShowError(string message, string title = null)
            => OnUiThread(() =>
            {
                var dlg = new MessageDialog(message, title, confirm: false);
                dlg.ShowSeverityBar("SystemFillColorCriticalBrush");
                dlg.ShowDialog();
            });

        /// <summary>Yes/No question. Returns true when Yes (or Enter) was picked;
        /// No and Esc return false.</summary>
        public static bool Confirm(string message, string title = null)
        {
            bool result = false;
            OnUiThread(() =>
            {
                var dlg = new MessageDialog(message, title, confirm: true);
                dlg.ShowDialog();
                result = dlg._yesClicked;
            });
            return result;
        }
    }
}
