/**
 * geetRPCS - Manage Apps UI
 * UI form for managing and overriding app configurations
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
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using geetRPCS.Models;
using geetRPCS.Services;

namespace geetRPCS.UI
{
    public class ManageAppsForm : Form
    {
        #region ----- Fields -----
        private readonly List<AppConfig> _allApps;
        private HashSet<string> _disabledApps;
        private Dictionary<string, AppOverrideConfig> _overrides;
        private readonly Action<string, bool> _onAppToggled;
        private readonly Action<string, string, string> _onOverrideChanged;
        private Panel searchPanel, scrollArea;
        private FlowLayoutPanel listPanel;
        private TextBox txtSearch;
        private Label lblCount;
        private readonly Color DiscordBackground = Color.FromArgb(47, 49, 54);
        private readonly Color DiscordCardBg = Color.FromArgb(32, 34, 37);
        private readonly Color DiscordHeader = Color.FromArgb(32, 34, 37);
        private readonly Color DiscordText = Color.FromArgb(255, 255, 255);
        private readonly Color DiscordTextMuted = Color.FromArgb(185, 187, 190);
        private readonly Color DiscordTextDark = Color.FromArgb(142, 146, 151);
        private readonly Color DiscordItemHover = Color.FromArgb(53, 55, 60);
        private readonly Color DiscordSearchBg = Color.FromArgb(30, 31, 34);
        private readonly Color DiscordBlurple = Color.FromArgb(88, 101, 242);
        #endregion
        public ManageAppsForm(
            IReadOnlyList<AppConfig> apps,
            HashSet<string> disabledApps,
            Dictionary<string, AppOverrideConfig> overrides,
            Action<string, bool> onAppToggled,
            Action<string, string, string> onOverrideChanged)
        {
            _allApps = apps.OrderBy(a => a.AppName).ToList();
            _disabledApps = new HashSet<string>(disabledApps, StringComparer.OrdinalIgnoreCase);
            _overrides = overrides;
            _onAppToggled = onAppToggled;
            _onOverrideChanged = onOverrideChanged;
            InitializeComponent();
            PopulateList();
        }
        private void InitializeComponent()
        {
            this.Text = LanguageManager.Current.WindowManageAppsTitle ?? "Manage Applications";
            this.Size = new Size(450, 700);
            this.MinimumSize = new Size(400, 500);
            this.BackColor = DiscordBackground;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.DoubleBuffered = true;
            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "rpicon.ico");
                if (File.Exists(iconPath)) this.Icon = new Icon(iconPath);
            }
            catch { }
            searchPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 110,
                BackColor = DiscordHeader,
                Padding = new Padding(20, 15, 20, 15)
            };
            this.Controls.Add(searchPanel);
            var lblTitle = new Label
            {
                Text = LanguageManager.Current.ManageAppsTitle ?? "MANAGE APPS",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = DiscordTextMuted,
                Location = new Point(20, 15),
                AutoSize = true
            };
            searchPanel.Controls.Add(lblTitle);
            txtSearch = new TextBox
            {
                Location = new Point(20, 45),
                Width = searchPanel.Width - 40,
                Height = 30,
                BackColor = DiscordSearchBg,
                ForeColor = DiscordText,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 11),
                PlaceholderText = LanguageManager.Current.ManageAppsSearch ?? "Search apps...",
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            txtSearch.TextChanged += (s, e) => PopulateList(txtSearch.Text);
            searchPanel.Controls.Add(txtSearch);
            lblCount = new Label
            {
                Text = string.Format(LanguageManager.Current.ManageAppsFound ?? "{0} apps found", _allApps.Count),
                Font = new Font("Segoe UI", 8),
                ForeColor = DiscordTextDark,
                Location = new Point(20, 85),
                AutoSize = true
            };
            searchPanel.Controls.Add(lblCount);
            var sep = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = Color.FromArgb(43, 45, 49) };
            this.Controls.Add(sep);
            sep.BringToFront();
            scrollArea = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = DiscordBackground
            };
            this.Controls.Add(scrollArea);
            scrollArea.BringToFront();
            listPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(0),
                Margin = new Padding(0),
                Width = scrollArea.Width,
                BackColor = Color.Transparent
            };
            scrollArea.Controls.Add(listPanel);
            this.Layout += (s, e) => SyncListWidth();
            scrollArea.SizeChanged += (s, e) => SyncListWidth();
        }
        private void SyncListWidth()
        {
            if (listPanel == null || scrollArea == null) return;
            listPanel.SuspendLayout();
            int targetWidth = scrollArea.ClientSize.Width;
            listPanel.Width = targetWidth;
            foreach (Control c in listPanel.Controls) c.Width = targetWidth;
            listPanel.ResumeLayout();
        }
        private void PopulateList(string filter = "")
        {
            listPanel.SuspendLayout();
            listPanel.Controls.Clear();
            var filtered = string.IsNullOrWhiteSpace(filter)
                ? _allApps
                : _allApps.Where(a => a.AppName.Contains(filter, StringComparison.OrdinalIgnoreCase) || 
                                      a.Process.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var app in filtered)
            {
                listPanel.Controls.Add(CreateAppItem(app));
            }
            lblCount.Text = string.Format(LanguageManager.Current.ManageAppsFound ?? "{0} apps found", filtered.Count);
            listPanel.ResumeLayout();
            SyncListWidth();
        }
        private Control CreateAppItem(AppConfig app)
        {
            bool isEnabled = !_disabledApps.Contains(app.Process);
            bool hasOverride = _overrides.ContainsKey(app.Process);
            var overrideConfig = hasOverride ? _overrides[app.Process] : null;
            var container = new Panel
            {
                Height = 60,
                Margin = new Padding(0, 0, 0, 1),
                BackColor = DiscordBackground,
                Cursor = Cursors.Hand,
                Padding = new Padding(20, 0, 20, 0),
                Tag = "collapsed"
            };
            var lblName = new Label
            {
                Text = app.AppName,
                Font = new Font("Segoe UI", 10, isEnabled ? FontStyle.Bold : FontStyle.Regular),
                ForeColor = isEnabled ? DiscordText : DiscordTextDark,
                Location = new Point(20, 12),
                AutoSize = true,
                BackColor = Color.Transparent
            };
            var lblProcess = new Label
            {
                Text = app.Process + ".exe",
                Font = new Font("Segoe UI", 8),
                ForeColor = Color.FromArgb(120, 124, 133),
                Location = new Point(20, 32),
                AutoSize = true,
                BackColor = Color.Transparent
            };
            var toggle = new Label
            {
                Text = isEnabled ? "✅" : "❌",
                Font = new Font("Segoe UI Emoji", 14),
                ForeColor = isEnabled ? DiscordBlurple : DiscordTextDark,
                Width = 40,
                Height = 40,
                Location = new Point(container.Width - 60, 10),
                Anchor = AnchorStyles.Right,
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand,
                BackColor = Color.Transparent
            };
            var btnEdit = new Label
            {
                Text = "⚙️",
                Font = new Font("Segoe UI Emoji", 14),
                ForeColor = DiscordTextDark,
                Width = 40,
                Height = 40,
                Location = new Point(container.Width - 110, 10),
                Anchor = AnchorStyles.Right,
                TextAlign = ContentAlignment.MiddleCenter,
                AutoSize = false,
                Cursor = Cursors.Hand,
                BackColor = Color.Transparent
            };
            var overridePanel = new Panel
            {
                Location = new Point(0, 60),
                Width = container.Width,
                Height = 100,
                BackColor = Color.FromArgb(43, 45, 49),
                Visible = false,
                Padding = new Padding(20, 10, 20, 10)
            };
            var txtDetails = new TextBox
            {
                Location = new Point(20, 25),
                Width = 360,
                BackColor = DiscordSearchBg,
                ForeColor = DiscordText,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9),
                PlaceholderText = LanguageManager.Current.LabelDetails,
                Text = overrideConfig?.Details ?? "",
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            var lblDet = new Label { Text = LanguageManager.Current.LabelDetails, Font = new Font("Segoe UI", 7, FontStyle.Bold), ForeColor = DiscordTextDark, Location = new Point(20, 8), AutoSize = true };
            var txtState = new TextBox
            {
                Location = new Point(20, 65),
                Width = 360,
                BackColor = DiscordSearchBg,
                ForeColor = DiscordText,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9),
                PlaceholderText = LanguageManager.Current.LabelState,
                Text = overrideConfig?.State ?? "",
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            var lblSta = new Label { Text = LanguageManager.Current.LabelState, Font = new Font("Segoe UI", 7, FontStyle.Bold), ForeColor = DiscordTextDark, Location = new Point(20, 48), AutoSize = true };
            overridePanel.Controls.AddRange(new Control[] { lblDet, txtDetails, lblSta, txtState });
            Action saveOverride = () => {
                _onOverrideChanged?.Invoke(app.Process, txtDetails.Text, txtState.Text);
            };
            txtDetails.TextChanged += (s, e) => saveOverride();
            txtState.TextChanged += (s, e) => saveOverride();
            Action toggleExpand = () => {
                bool isCollapsed = container.Tag.ToString() == "collapsed";
                if (isCollapsed) {
                    container.Height = 160;
                    container.Tag = "expanded";
                    overridePanel.Visible = true;
                    btnEdit.ForeColor = DiscordBlurple;
                } else {
                    container.Height = 60;
                    container.Tag = "collapsed";
                    overridePanel.Visible = false;
                    btnEdit.ForeColor = DiscordTextDark;
                }
            };
            btnEdit.Click += (s, e) => toggleExpand();
            Action performToggle = () =>
            {
                bool currentState = _disabledApps.Contains(app.Process);
                bool newState = currentState;
                if (currentState) {
                    _disabledApps.Remove(app.Process);
                    newState = true;
                } else {
                    _disabledApps.Add(app.Process);
                    newState = false;
                }
                toggle.Text = newState ? "✅" : "❌";
                lblName.ForeColor = newState ? DiscordText : DiscordTextDark;
                lblName.Font = new Font("Segoe UI", 10, newState ? FontStyle.Bold : FontStyle.Regular);
                _onAppToggled?.Invoke(app.Process, newState);
            };
            container.Click += (s, e) => performToggle();
            lblName.Click += (s, e) => performToggle();
            lblProcess.Click += (s, e) => performToggle();
            toggle.Click += (s, e) => performToggle();
            container.MouseEnter += (s, e) => { if (container.Tag.ToString() == "collapsed") container.BackColor = DiscordCardBg; };
            container.MouseLeave += (s, e) => { if (container.Tag.ToString() == "collapsed") container.BackColor = DiscordBackground; };
            container.Controls.Add(btnEdit);
            container.Controls.Add(toggle);
            container.Controls.Add(lblName);
            container.Controls.Add(lblProcess);
            container.Controls.Add(overridePanel);
            container.SizeChanged += (s, e) => {
                toggle.Left = container.Width - 60;
                btnEdit.Left = container.Width - 110;
                overridePanel.Width = container.Width;
                txtDetails.Width = overridePanel.Width - 40;
                txtState.Width = overridePanel.Width - 40;
            };
            return container;
        }
    }
}
