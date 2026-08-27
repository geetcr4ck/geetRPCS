/**
 * geetRPCS - Statistics window (ModernWpf / Fluent)
 * WPF replacement for the plain-text InfoDialog dumps produced by
 * StatsCoordinator. One shared modeless instance is reused by the four
 * tray-menu views (today / week / month / all-time); each Show() call
 * just swaps the content and re-activates the window.
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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using geetRPCS.Services;

namespace geetRPCS.UI.Modern
{
    public partial class StatisticsWindow : Window
    {
        private static StatisticsWindow _instance;

        /// <summary>The live shared instance (null when closed). Exposed for tests.</summary>
        internal static StatisticsWindow Instance => _instance;

        /// <summary>
        /// Fires when the shared window is shown (true) or fully closed (false).
        /// The tray menu uses this to keep its checkmark in sync.
        /// </summary>
        internal static event Action<bool> IsOpenChanged;

        public StatisticsWindow()
        {
            InitializeComponent();
            Title = LanguageManager.Current.MenuStatistics ?? "Statistics";
            CloseButton.Content = LanguageManager.Current.BtnClose ?? "Close";
            try
            {
                string iconPath = Utils.AppPaths.IconPath;
                if (File.Exists(iconPath)) Icon = LoadIcon(iconPath);
            }
            catch { }
        }

        /// <summary>Shows the shared window (creating it on first use) with the given view.</summary>
        internal static void Show(StatisticsViewModel view)
        {
            if (_instance == null)
            {
                _instance = new StatisticsWindow();
                _instance.Closed += (s, e) =>
                {
                    _instance = null;
                    IsOpenChanged?.Invoke(false);
                };
                _instance.Show();
                IsOpenChanged?.Invoke(true);
            }
            _instance.LoadView(view);
            _instance.Activate();
        }

        private void LoadView(StatisticsViewModel view)
        {
            TitleText.Text = view.Title ?? "";
            SubtitleText.Text = view.Subtitle ?? "";
            SubtitleText.Visibility = string.IsNullOrEmpty(view.Subtitle)
                ? Visibility.Collapsed : Visibility.Visible;

            bool empty = view.Rows == null || view.Rows.Count == 0;
            EmptyText.Text = view.EmptyMessage ?? "";
            EmptyText.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
            RowsList.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
            RowsList.ItemsSource = view.Rows;
            TotalsList.ItemsSource = view.Totals;
        }

        private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

        // Test accessors (InternalsVisibleTo: Tests)
        internal string WindowTitleText => TitleText.Text;
        internal int RowCount => RowsList.Items.Count;
        internal bool IsEmptyVisible => EmptyText.Visibility == Visibility.Visible;
        internal int TotalsCount => TotalsList.Items.Count;

        private static ImageSource LoadIcon(string path)
        {
            using var icon = new System.Drawing.Icon(path);
            return Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        }
    }

    /// <summary>One ranked row in the statistics list.</summary>
    internal sealed class StatsRow
    {
        public int Rank { get; set; }
        public string AppName { get; set; }
        public string TimeText { get; set; }
    }

    /// <summary>Immutable snapshot of one statistics view (today / week / month / all-time).</summary>
    internal sealed class StatisticsViewModel
    {
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public string EmptyMessage { get; set; }
        public List<StatsRow> Rows { get; set; } = new List<StatsRow>();
        public List<string> Totals { get; set; } = new List<string>();
    }
}
