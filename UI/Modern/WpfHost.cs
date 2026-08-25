/**
 * geetRPCS - WPF Host bootstrap
 * Bootstraps the WPF Application + ModernWpf (Fluent) theme so WPF windows
 * (UI/Modern/*.xaml) can be shown from the WinForms host.
 *
 * Integration notes (validated against ModernWpfUI 1.0.0-preview.7):
 *  - A WPF Application must exist before any WPF window is created, otherwise
 *    pack://application URIs and StaticResource lookups fail.
 *  - ThemeResources implements ISupportInitialize; in XAML the parser calls
 *    BeginInit/EndInit, but in code it must be done manually. EndInit also
 *    requires Application.Current to already exist, or the theme is never
 *    applied (controls then fail with "'DependencyProperty.UnsetValue' is not
 *    a valid value for property 'BorderThickness'").
 *  - Set the application theme AFTER EndInit so ThemeResources swaps in the
 *    Dark/Light dictionary.
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
using ModernWpf;
using ModernWpf.Controls;
using geetRPCS.Services;

namespace geetRPCS.UI.Modern
{
    internal static class WpfHost
    {
        private static Application _application;

        /// <summary>Creates the WPF Application + ModernWpf theme exactly once. Call before showing any WPF window.</summary>
        public static void EnsureInitialized()
        {
            if (_application != null) return;

            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            // Set the field before anything else: ApplyThemeMode re-enters
            // EnsureInitialized, which must become a no-op at that point.
            _application = app;

            var resources = new ResourceDictionary();
            var theme = new ThemeResources();
            resources.MergedDictionaries.Add(theme);
            app.Resources = resources;

            theme.BeginInit();
            theme.EndInit(); // Requires Application.Current (set above) or the theme never applies.

            // Apply the persisted theme mode (System/Dark/Light); all windows use
            // DynamicResource theme brushes so both themes render correctly.
            ApplyThemeMode(SettingsService.Instance.ThemeMode);

            resources.MergedDictionaries.Add(new FluentControlsResources { UseCompactResources = false });
        }

        /// <summary>
        /// Warms up the WPF stack at startup so the FIRST real window opens instantly
        /// instead of paying the one-time cost (control-template/BAML loading, first
        /// layout, font cache and render/composition init) when the user clicks its
        /// tray item — the reported few-ms freeze on first Manage Apps open. Shows a
        /// tiny invisible off-screen window containing the control types the app's
        /// windows use (TextBox / CheckBox / Expander / Button — the ManageApps row
        /// template and dialog buttons), forces layout, pumps once, then closes it.
        /// Must run on the UI thread after EnsureInitialized. Never breaks startup.
        /// </summary>
        public static void PreWarm()
        {
            if (_application == null) return;
            try
            {
                var panel = new System.Windows.Controls.StackPanel();
                panel.Children.Add(new System.Windows.Controls.TextBox { Text = "warmup" });
                panel.Children.Add(new System.Windows.Controls.CheckBox { IsChecked = true });
                panel.Children.Add(new System.Windows.Controls.Expander
                {
                    IsExpanded = true,
                    Content = new System.Windows.Controls.TextBox { Text = "warmup" }
                });
                // The lazy Details/State editor row wraps its content in a
                // ContentControl whose DataTemplate materializes on expand — that
                // template type must be warm too or the first expand pays it.
                panel.Children.Add(new System.Windows.Controls.ContentControl
                {
                    Content = new System.Windows.Controls.TextBox { Text = "warmup" }
                });
                panel.Children.Add(new System.Windows.Controls.Button { Content = "warmup" });

                // The ManageApps list chrome (ScrollViewer + its ScrollBar) is NOT
                // covered by the controls above — ModernWpf's ScrollBar template is
                // heavy on first load. Height=1 forces the vertical ScrollBar to
                // realize (overflow) so its template is warm too.
                var scroll = new System.Windows.Controls.ScrollViewer
                {
                    Height = 1,
                    VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
                    Content = panel
                };

                var win = new Window
                {
                    Width = 1, Height = 1,
                    Left = -32000, Top = -32000, // off-screen
                    WindowStyle = WindowStyle.None,
                    ShowInTaskbar = false,
                    ShowActivated = false, // never steal focus
                    Opacity = 0,            // never flash
                    Background = System.Windows.Media.Brushes.Transparent,
                    Content = scroll
                };
                win.Show();
                win.UpdateLayout(); // force template application + measure/arrange
                // Let the message loop process the present so the render/composition
                // pipeline initializes too (font/D3D init happens on first present).
                for (int i = 0; i < 3; i++) System.Windows.Forms.Application.DoEvents();
                win.Close();
                LogService.Log("WPF pre-warm complete", "DEBUG", "WpfHost");
            }
            catch (Exception ex)
            {
                LogService.Log($"WPF pre-warm failed: {ex.Message}", "WARN", "WpfHost");
            }
        }

        /// <summary>
        /// Applies the theme mode live: "Dark" / "Light" force the ModernWpf theme,
        /// anything else ("System") follows the Windows light/dark preference.
        /// </summary>
        public static void ApplyThemeMode(string mode)
        {
            EnsureInitialized();
            ThemeManager.Current.ApplicationTheme = mode switch
            {
                "Dark" => ApplicationTheme.Dark,
                "Light" => ApplicationTheme.Light,
                _ => null // System
            };
        }
    }
}
