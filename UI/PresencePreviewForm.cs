/**
 * geetRPCS - Presence Preview UI
 * UI form for previewing Discord Rich Presence
 */
/*
 * Copyright (c) 2026 makcrtve
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
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using DiscordRPC;
using geetRPCS.Models;
using geetRPCS.Services;

namespace geetRPCS.UI
{
    public class PresencePreviewForm : Form
    {
        #region ----- Fields -----
        private RichPresence _currentPresence;
        private System.Windows.Forms.Timer _elapsedTimer;
        private DateTime? _startTime;
        private Image _largeImage, _smallImage;
        private string _applicationId;
        private readonly object _imageLock = new object();
        private Panel mainPanel, largeImagePanel, smallImagePanel, button1Panel, button2Panel;
        private Label lblAppName, lblDetails, lblState, lblElapsed, lblButton1, lblButton2;
        private Label lblStatus, lblLargeImageText, lblSmallImageText, lblPlaceholder, lblLoading, lblCdnStatus;
        private readonly Color DiscordBackground = Color.FromArgb(47, 49, 54);
        private readonly Color DiscordCardBg = Color.FromArgb(32, 34, 37);
        private readonly Color DiscordText = Color.FromArgb(255, 255, 255);
        private readonly Color DiscordTextMuted = Color.FromArgb(185, 187, 190);
        private readonly Color DiscordTextDark = Color.FromArgb(142, 146, 151);
        private readonly Color DiscordGreen = Color.FromArgb(87, 242, 135);
        private readonly Color DiscordYellow = Color.FromArgb(250, 168, 26);
        private readonly Color DiscordButtonBg = Color.FromArgb(79, 84, 92);
        private readonly Color DiscordButtonHover = Color.FromArgb(104, 109, 118);
        private readonly Color DiscordImageBg = Color.FromArgb(64, 68, 75);
        private readonly Color DiscordBlurple = Color.FromArgb(88, 101, 242);
        private readonly HttpClient _httpClient = new HttpClient();
        private readonly string CacheFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ImageCache");
        private readonly Dictionary<string, Image> _imageCache = new Dictionary<string, Image>();
        private readonly Dictionary<string, MemoryStream> _streamCache = new Dictionary<string, MemoryStream>();
        private Dictionary<string, string> _assetIdCache = new Dictionary<string, string>();
        private bool _assetsLoaded = false;
        private bool _isFetchingAssets = false;
        private ToolTip _toolTip;
        #endregion
        #region ----- Constructor -----
        public PresencePreviewForm(string applicationId = null)
        {
            _applicationId = applicationId;
            InitializeComponent();
            StartTimer();
            EnsureCacheFolder();
            _httpClient.Timeout = TimeSpan.FromSeconds(15);
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "geetRPCS/1.0");
            if (!string.IsNullOrEmpty(_applicationId)) _ = LoadAssetsMappingAsync();
        }
        public void SetApplicationId(string appId)
        {
            _applicationId = appId;
            if (!string.IsNullOrEmpty(_applicationId) && !_assetsLoaded) _ = LoadAssetsMappingAsync();
        }
        private void EnsureCacheFolder()
        {
            try { if (!Directory.Exists(CacheFolder)) Directory.CreateDirectory(CacheFolder); }
            catch { }
        }
        private bool IsImageValid(Image image)
        {
            if (image == null) return false;
            try { var width = image.Width; var height = image.Height; return width > 0 && height > 0; }
            catch (ArgumentException) { return false; }
            catch (ObjectDisposedException) { return false; }
            catch (Exception) { return false; }
        }
        #endregion
        #region ----- UI Setup -----
        private void InitializeComponent()
        {
            this.Text = LanguageManager.Current.WindowPreviewTitle ?? "Discord Presence Preview";
            this.Size = new Size(320, 440);
            this.MinimumSize = this.MaximumSize = new Size(320, 440);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.Manual;
            this.BackColor = DiscordBackground;
            this.ShowInTaskbar = false;
            this.TopMost = true;
            var workingArea = Screen.PrimaryScreen.WorkingArea;
            this.Location = new Point(workingArea.Right - this.Width - 20, workingArea.Bottom - this.Height - 20);
            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "rpicon.ico");
                if (File.Exists(iconPath)) this.Icon = new Icon(iconPath);
            }
            catch { }
            _toolTip = new ToolTip();
            CreateHeader();
            CreateMainPanel();
            CreateImagePanels();
            CreateLabels();
            CreateButtons();
            CreateInfoPanel();
            CreateFooter();
            this.FormClosing += OnFormClosing;
            this.DoubleClick += (s, e) => this.Hide();
            mainPanel.DoubleClick += (s, e) => this.Hide();
        }
        private void CreateHeader()
        {
            var headerLabel = new Label
            {
                Text = LanguageManager.Current.PreviewPlayingGame ?? "PLAYING A GAME",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = DiscordTextMuted,
                Location = new Point(15, 12),
                AutoSize = true
            };
            this.Controls.Add(headerLabel);
            lblStatus = new Label
            {
                Text = LanguageManager.Current.PreviewLive ?? "● Live",
                Font = new Font("Segoe UI", 9),
                ForeColor = DiscordGreen,
                Location = new Point(230, 12),
                AutoSize = true
            };
            this.Controls.Add(lblStatus);
        }
        private void CreateMainPanel()
        {
            mainPanel = new Panel
            {
                Location = new Point(15, 40),
                Size = new Size(275, 350),
                BackColor = DiscordCardBg
            };
            this.Controls.Add(mainPanel);
        }
        private void CreateImagePanels()
        {
            largeImagePanel = new Panel
            {
                Size = new Size(60, 60),
                Location = new Point(12, 12),
                BackColor = Color.Transparent
            };
            largeImagePanel.Paint += LargeImagePanel_Paint;
            mainPanel.Controls.Add(largeImagePanel);
            lblPlaceholder = new Label
            {
                Text = "🎵",
                Font = new Font("Segoe UI Emoji", 22),
                ForeColor = DiscordTextMuted,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent
            };
            largeImagePanel.Controls.Add(lblPlaceholder);
            lblLoading = new Label
            {
                Text = "⏳",
                Font = new Font("Segoe UI Emoji", 18),
                ForeColor = DiscordTextMuted,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Visible = false
            };
            largeImagePanel.Controls.Add(lblLoading);
            smallImagePanel = new Panel
            {
                Size = new Size(24, 24),
                Location = new Point(52, 52),
                BackColor = Color.Transparent
            };
            smallImagePanel.Paint += SmallImagePanel_Paint;
            mainPanel.Controls.Add(smallImagePanel);
            smallImagePanel.BringToFront();
        }
        private void CreateLabels()
        {
            lblAppName = new Label
            {
                Text = "geetRPCS",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = DiscordText,
                Location = new Point(80, 12),
                Size = new Size(185, 20),
                AutoEllipsis = true
            };
            mainPanel.Controls.Add(lblAppName);
            lblDetails = new Label
            {
                Text = "Idling...",
                Font = new Font("Segoe UI", 9),
                ForeColor = DiscordTextMuted,
                Location = new Point(80, 34),
                Size = new Size(185, 18),
                AutoEllipsis = true
            };
            mainPanel.Controls.Add(lblDetails);
            lblState = new Label
            {
                Text = "Ready to work",
                Font = new Font("Segoe UI", 9),
                ForeColor = DiscordTextMuted,
                Location = new Point(80, 52),
                Size = new Size(185, 18),
                AutoEllipsis = true
            };
            mainPanel.Controls.Add(lblState);
            lblElapsed = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 8),
                ForeColor = DiscordTextDark,
                Location = new Point(80, 72),
                Size = new Size(185, 16),
                AutoEllipsis = true
            };
            mainPanel.Controls.Add(lblElapsed);
        }
        private void CreateButtons()
        {
            button1Panel = CreateButton(12, 100, "Button 1");
            lblButton1 = (Label)button1Panel.Controls[0];
            button1Panel.Visible = false;
            mainPanel.Controls.Add(button1Panel);
            button2Panel = CreateButton(12, 138, "Button 2");
            lblButton2 = (Label)button2Panel.Controls[0];
            button2Panel.Visible = false;
            mainPanel.Controls.Add(button2Panel);
        }
        private void CreateInfoPanel()
        {
            var infoPanel = new Panel
            {
                Location = new Point(12, 185),
                Size = new Size(251, 105),
                BackColor = Color.FromArgb(40, 43, 48)
            };
            mainPanel.Controls.Add(infoPanel);
            infoPanel.Controls.Add(new Label
            {
                Text = "ℹ️",
                Font = new Font("Segoe UI Emoji", 9),
                ForeColor = DiscordText,
                Location = new Point(8, 10),
                AutoSize = true
            });
            infoPanel.Controls.Add(new Label
            {
                Text = LanguageManager.Current.PreviewHeaderAssetInfo ?? "Asset Info",
                Font = new Font("Segoe UI", 8, FontStyle.Bold),
                ForeColor = DiscordText,
                Location = new Point(34, 11),
                AutoSize = true
            });
            lblLargeImageText = new Label
            {
                Text = "Large: -",
                Font = new Font("Segoe UI", 8),
                ForeColor = DiscordTextMuted,
                Location = new Point(12, 30),
                Size = new Size(230, 16),
                AutoEllipsis = true
            };
            infoPanel.Controls.Add(lblLargeImageText);
            lblSmallImageText = new Label
            {
                Text = "Small: -",
                Font = new Font("Segoe UI", 8),
                ForeColor = DiscordTextMuted,
                Location = new Point(12, 48),
                Size = new Size(230, 16),
                AutoEllipsis = true
            };
            infoPanel.Controls.Add(lblSmallImageText);
            lblCdnStatus = new Label
            {
                Text = "📡 Initializing...",
                Font = new Font("Segoe UI", 7),
                ForeColor = DiscordTextDark,
                Location = new Point(12, 70),
                Size = new Size(230, 28),
                AutoEllipsis = true
            };
            infoPanel.Controls.Add(lblCdnStatus);
        }
        private void CreateFooter()
        {
            mainPanel.Controls.Add(new Label
            {
                Text = LanguageManager.Current.PreviewDoubleClickHide ?? "💡 Double-click to hide",
                Font = new Font("Segoe UI", 8),
                ForeColor = DiscordTextDark,
                Location = new Point(12, 305),
                AutoSize = true
            });
            var refreshButton = new Label
            {
                Text = "🔄",
                Font = new Font("Segoe UI Emoji", 11),
                ForeColor = DiscordText,
                Location = new Point(185, 303),
                AutoSize = true,
                Cursor = Cursors.Hand
            };
            _toolTip.SetToolTip(refreshButton, LanguageManager.Current.PreviewRefreshAssets ?? "Refresh Assets");
            refreshButton.Click += async (s, e) =>
            {
                UpdateCdnStatus(LanguageManager.Current.PreviewStatusRefreshing ?? "🔄 Refreshing...", DiscordYellow);
                _assetsLoaded = false;
                await LoadAssetsMappingAsync();
                if (_currentPresence != null)
                {
                    ClearImageMemoryCache();
                    await LoadImagesAsync(_currentPresence.Assets);
                }
            };
            mainPanel.Controls.Add(refreshButton);
            var clearCacheButton = new Label
            {
                Text = "🗑️",
                Font = new Font("Segoe UI Emoji", 11),
                ForeColor = DiscordText,
                Location = new Point(210, 303),
                AutoSize = true,
                Cursor = Cursors.Hand
            };
            _toolTip.SetToolTip(clearCacheButton, LanguageManager.Current.PreviewClearCache ?? "Clear Cache");
            clearCacheButton.Click += (s, e) =>
            {
                ClearAllCache();
                UpdateCdnStatus(LanguageManager.Current.PreviewStatusCacheCleared ?? "🗑️ Cache cleared!", DiscordGreen);
            };
            mainPanel.Controls.Add(clearCacheButton);
            var pinButton = new Label
            {
                Text = "📌",
                Font = new Font("Segoe UI Emoji", 11),
                ForeColor = DiscordText,
                Location = new Point(235, 303),
                AutoSize = true,
                Cursor = Cursors.Hand
            };
            _toolTip.SetToolTip(pinButton, LanguageManager.Current.PreviewAlwaysOnTop ?? "Always on Top");
            pinButton.Click += (s, e) =>
            {
                this.TopMost = !this.TopMost;
                pinButton.Text = this.TopMost ? "📌" : "📍";
            };
            mainPanel.Controls.Add(pinButton);
        }
        private Panel CreateButton(int x, int y, string text)
        {
            var panel = new Panel
            {
                Location = new Point(x, y),
                Size = new Size(251, 30),
                BackColor = DiscordButtonBg,
                Cursor = Cursors.Hand
            };
            var label = new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = DiscordText,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill,
                Cursor = Cursors.Hand
            };
            panel.MouseEnter += (s, e) => panel.BackColor = DiscordButtonHover;
            panel.MouseLeave += (s, e) => panel.BackColor = DiscordButtonBg;
            label.MouseEnter += (s, e) => panel.BackColor = DiscordButtonHover;
            label.MouseLeave += (s, e) => panel.BackColor = DiscordButtonBg;
            label.Click += (s, e) =>
            {
                if (panel.Tag is string url && !string.IsNullOrEmpty(url))
                {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        { FileName = url, UseShellExecute = true });
                    }
                    catch { }
                }
            };
            panel.Controls.Add(label);
            return panel;
        }
        #endregion
        #region ----- Discord API -----
        private async Task LoadAssetsMappingAsync()
        {
            if (string.IsNullOrEmpty(_applicationId))
            {
                UpdateCdnStatus(LanguageManager.Current.PreviewStatusNoAppId ?? "❌ No Application ID", DiscordYellow);
                return;
            }
            if (_isFetchingAssets) return;
            _isFetchingAssets = true;
            string cacheFile = Path.Combine(CacheFolder, $"assets_{_applicationId}.json");
            if (File.Exists(cacheFile))
            {
                try
                {
                    var cacheAge = DateTime.Now - File.GetLastWriteTime(cacheFile);
                    if (cacheAge.TotalHours < 24)
                    {
                        string cachedJson = await File.ReadAllTextAsync(cacheFile);
                        var cachedData = JsonSerializer.Deserialize(cachedJson, Utils.JsonContext.Default.DictionaryStringString);
                        if (cachedData != null && cachedData.Count > 0)
                        {
                            _assetIdCache = cachedData;
                            _assetsLoaded = true;
                            UpdateCdnStatus(string.Format(LanguageManager.Current.PreviewStatusAssetsCached ?? "✅ {0} assets (cached)", _assetIdCache.Count), DiscordGreen);
                            _isFetchingAssets = false;
                            if (_currentPresence != null) await LoadImagesAsync(_currentPresence.Assets);
                            return;
                        }
                    }
                }
                catch { }
            }
            UpdateCdnStatus(LanguageManager.Current.PreviewStatusFetching ?? "📡 Fetching from Discord...", DiscordYellow);
            try
            {
                string apiUrl = $"https://discord.com/api/v10/oauth2/applications/{_applicationId}/assets";
                using var response = await _httpClient.GetAsync(apiUrl);
                string json = await response.Content.ReadAsStringAsync();
                if (response.IsSuccessStatusCode)
                {
                    var assets = JsonSerializer.Deserialize(json, Utils.JsonContext.Default.ListDiscordAsset);
                    if (assets == null || assets.Count == 0)
                    {
                        UpdateCdnStatus(LanguageManager.Current.PreviewStatusNoAssets ?? "⚠️ No assets found", DiscordYellow);
                        _assetsLoaded = true;
                    }
                    else
                    {
                        _assetIdCache.Clear();
                        int validCount = 0;
                        foreach (var asset in assets)
                        {
                            if (!string.IsNullOrEmpty(asset.Name) && !string.IsNullOrEmpty(asset.Id))
                            {
                                _assetIdCache[asset.Name.ToLower()] = asset.Id;
                                validCount++;
                            }
                        }
                        _assetsLoaded = true;
                        try
                        {
                            string cacheJson = JsonSerializer.Serialize(_assetIdCache, Utils.JsonContext.Default.DictionaryStringString);
                            await File.WriteAllTextAsync(cacheFile, cacheJson);
                        }
                        catch { }
                        UpdateCdnStatus(string.Format(LanguageManager.Current.PreviewStatusAssetsLoaded ?? "✅ {0} assets loaded", validCount), DiscordGreen);
                    }
                    if (_currentPresence != null) await LoadImagesAsync(_currentPresence.Assets);
                }
                else UpdateCdnStatus(string.Format(LanguageManager.Current.PreviewStatusApiError ?? "⚠️ API Error: {0}", response.StatusCode), DiscordYellow);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error mapping assets: {ex}");
                UpdateCdnStatus(LanguageManager.Current.PreviewStatusError ?? "❌ Error", DiscordYellow);
            }
            finally
            {
                _isFetchingAssets = false;
            }
        }
        private void UpdateCdnStatus(string text, Color color)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => UpdateCdnStatus(text, color)));
                return;
            }
            if (lblCdnStatus != null && !lblCdnStatus.IsDisposed)
            {
                lblCdnStatus.Text = text;
                lblCdnStatus.ForeColor = color;
            }
        }
        #endregion
        #region ----- Image Painting -----
        private void LargeImagePanel_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            var rect = new Rectangle(0, 0, largeImagePanel.Width - 1, largeImagePanel.Height - 1);
            using (var path = GetRoundedRect(rect, 8))
            {
                using (var brush = new SolidBrush(DiscordImageBg)) { g.FillPath(brush, path); }
                Image imageToDraw = null;
                lock (_imageLock) { if (IsImageValid(_largeImage)) imageToDraw = _largeImage; }
                if (imageToDraw != null)
                {
                    try
                    {
                        g.SetClip(path);
                        g.DrawImage(imageToDraw, rect);
                        g.ResetClip();
                        if (lblPlaceholder != null && !lblPlaceholder.IsDisposed) lblPlaceholder.Visible = false;
                        if (lblLoading != null && !lblLoading.IsDisposed) lblLoading.Visible = false;
                    }
                    catch (ArgumentException ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error drawing large image: {ex.Message}");
                        lock (_imageLock) { _largeImage = null; }
                        if (lblPlaceholder != null && !lblPlaceholder.IsDisposed)
                            lblPlaceholder.Visible = !(lblLoading?.Visible ?? false);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Unexpected error drawing large image: {ex.Message}");
                    }
                }
                else
                {
                    if (lblPlaceholder != null && !lblPlaceholder.IsDisposed)
                        lblPlaceholder.Visible = !(lblLoading?.Visible ?? false);
                }
            }
        }
        private void SmallImagePanel_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            var rect = new Rectangle(0, 0, smallImagePanel.Width - 1, smallImagePanel.Height - 1);
            using (var brush = new SolidBrush(DiscordCardBg))
            {
                g.FillEllipse(brush, -3, -3, smallImagePanel.Width + 6, smallImagePanel.Height + 6);
            }
            Image imageToDraw = null;
            lock (_imageLock) { if (IsImageValid(_smallImage)) imageToDraw = _smallImage; }
            if (imageToDraw != null)
            {
                try
                {
                    using (var path = new GraphicsPath())
                    {
                        path.AddEllipse(rect);
                        g.SetClip(path);
                        g.DrawImage(imageToDraw, rect);
                        g.ResetClip();
                    }
                }
                catch (ArgumentException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error drawing small image: {ex.Message}");
                    lock (_imageLock) { _smallImage = null; }
                    DrawDefaultSmallImage(g, rect);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Unexpected error drawing small image: {ex.Message}");
                    DrawDefaultSmallImage(g, rect);
                }
            }
            else DrawDefaultSmallImage(g, rect);
        }
        private void DrawDefaultSmallImage(Graphics g, Rectangle rect)
        {
            using (var brush = new SolidBrush(DiscordBlurple))
            {
                g.FillEllipse(brush, 2, 2, smallImagePanel.Width - 4, smallImagePanel.Height - 4);
            }
            using (var font = new Font("Segoe UI", 9, FontStyle.Bold))
            using (var brush = new SolidBrush(Color.White))
            {
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString("✓", font, brush,
                    new RectangleF(0, 0, smallImagePanel.Width, smallImagePanel.Height), sf);
            }
        }
        private GraphicsPath GetRoundedRect(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();
            var diameter = radius * 2;
            path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
        #endregion
        #region ----- Image Loading -----
        private async Task LoadImagesAsync(Assets assets)
        {
            if (assets == null) { ClearImages(); return; }
            ShowLoading(true);
            try
            {
                if (!string.IsNullOrEmpty(assets.LargeImageKey))
                {
                    var newLargeImage = await GetImageAsync(assets.LargeImageKey);
                    lock (_imageLock) { _largeImage = newLargeImage; }
                    UpdatePlaceholderEmoji(assets.LargeImageKey);
                }
                else lock (_imageLock) { _largeImage = null; }
                if (!string.IsNullOrEmpty(assets.SmallImageKey))
                {
                    var newSmallImage = await GetImageAsync(assets.SmallImageKey);
                    lock (_imageLock) { _smallImage = newSmallImage; }
                }
                else lock (_imageLock) { _smallImage = null; }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Error loading images: {ex.Message}"); }
            finally { ShowLoading(false); RefreshImagePanels(); }
        }
        private async Task<Image> GetImageAsync(string key)
        {
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(_applicationId)) return null;
            string keyLower = key.ToLower();
            string cacheKey = $"{_applicationId}_{keyLower}";
            lock (_imageLock)
            {
                if (_imageCache.TryGetValue(cacheKey, out var cachedImage))
                {
                    if (IsImageValid(cachedImage)) return cachedImage;
                    else _imageCache.Remove(cacheKey);
                }
            }
            string diskCachePath = Path.Combine(CacheFolder, $"{cacheKey}.png");
            if (File.Exists(diskCachePath))
            {
                try
                {
                    byte[] fileBytes = await File.ReadAllBytesAsync(diskCachePath);
                    var ms = new MemoryStream(fileBytes) { Position = 0 };
                    var diskImage = Image.FromStream(ms);
                    lock (_imageLock)
                    {
                        _imageCache[cacheKey] = diskImage;
                        _streamCache[cacheKey] = ms;
                    }
                    return diskImage;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading disk cache: {ex.Message}");
                    try { File.Delete(diskCachePath); } catch { }
                }
            }
            string assetId = null;
            if (_assetIdCache.TryGetValue(keyLower, out var id)) assetId = id;
            if (string.IsNullOrEmpty(assetId))
            {
                if (long.TryParse(key, out _)) assetId = key;
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Asset ID not found for: {key}");
                    return null;
                }
            }
            try
            {
                string url = $"https://cdn.discordapp.com/app-assets/{_applicationId}/{assetId}.png";
                System.Diagnostics.Debug.WriteLine($"Downloading: {url}");
                using var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    byte[] imageBytes = await response.Content.ReadAsByteArrayAsync();
                    var memoryStream = new MemoryStream(imageBytes) { Position = 0 };
                    var image = Image.FromStream(memoryStream);
                    lock (_imageLock)
                    {
                        _imageCache[cacheKey] = image;
                        _streamCache[cacheKey] = memoryStream;
                    }
                    try { await File.WriteAllBytesAsync(diskCachePath, imageBytes); } catch { }
                    return image;
                }
                else System.Diagnostics.Debug.WriteLine($"Failed to download {key}: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error downloading {key}: {ex.Message}");
            }
            return null;
        }
        private void ShowLoading(bool show)
        {
            if (this.InvokeRequired) { this.BeginInvoke(new Action(() => ShowLoading(show))); return; }
            if (lblLoading != null && !lblLoading.IsDisposed) lblLoading.Visible = show;
            if (lblPlaceholder != null && !lblPlaceholder.IsDisposed)
            {
                bool hasLargeImage;
                lock (_imageLock) { hasLargeImage = IsImageValid(_largeImage); }
                lblPlaceholder.Visible = !show && !hasLargeImage;
            }
        }
        private void ClearImages()
        {
            lock (_imageLock) { _largeImage = null; _smallImage = null; }
            if (lblPlaceholder != null && !lblPlaceholder.IsDisposed)
            {
                lblPlaceholder.Text = "🎵";
                lblPlaceholder.Visible = true;
            }
            RefreshImagePanels();
        }
        private void ClearImageMemoryCache()
        {
            lock (_imageLock)
            {
                foreach (var img in _imageCache.Values) { try { img?.Dispose(); } catch { } }
                _imageCache.Clear();
                foreach (var stream in _streamCache.Values) { try { stream?.Dispose(); } catch { } }
                _streamCache.Clear();
                _largeImage = null;
                _smallImage = null;
            }
        }
        private void ClearAllCache()
        {
            ClearImageMemoryCache();
            _assetIdCache.Clear();
            _assetsLoaded = false;
            try
            {
                if (Directory.Exists(CacheFolder))
                    foreach (var file in Directory.GetFiles(CacheFolder))
                    { try { File.Delete(file); } catch { } }
            }
            catch { }
        }
        private void UpdatePlaceholderEmoji(string key)
        {
            if (lblPlaceholder == null || lblPlaceholder.IsDisposed) return;
            string emoji = "🎵";
            string keyLower = key?.ToLower() ?? "";
            if (keyLower.Contains("fl") || keyLower.Contains("ableton") || keyLower.Contains("cubase") ||
                keyLower.Contains("reaper") || keyLower.Contains("protools") || keyLower.Contains("studio") ||
                keyLower.Contains("audition")) emoji = "🎵";
            else if (keyLower.Contains("photoshop") || keyLower.Contains("illustrator") ||
                     keyLower.Contains("figma") || keyLower.Contains("canva") || keyLower.Contains("gimp") ||
                     keyLower.Contains("affinity") || keyLower.Contains("coreldraw") ||
                     keyLower.Contains("inkscape")) emoji = "🎨";
            else if (keyLower.Contains("premiere") || keyLower.Contains("resolve") || keyLower.Contains("vegas") ||
                     keyLower.Contains("capcut") || keyLower.Contains("filmora") ||
                     keyLower.Contains("aftereffects")) emoji = "🎬";
            else if (keyLower.Contains("blender") || keyLower.Contains("maya") || keyLower.Contains("sketchup") ||
                     keyLower.Contains("autocad")) emoji = "🏗️";
            else if (keyLower.Contains("chrome") || keyLower.Contains("firefox") || keyLower.Contains("edge") ||
                     keyLower.Contains("brave") || keyLower.Contains("zen")) emoji = "🌐";
            else if (keyLower.Contains("obs") || keyLower.Contains("streamlabs")) emoji = "📺";
            else if (keyLower.Contains("word") || keyLower.Contains("excel") ||
                     keyLower.Contains("powerpoint")) emoji = "📄";
            else if (keyLower.Contains("telegram") || keyLower.Contains("discord")) emoji = "💬";
            else if (keyLower.Contains("vlc") || keyLower.Contains("media")) emoji = "▶️";
            lblPlaceholder.Text = emoji;
        }
        private void RefreshImagePanels()
        {
            if (this.InvokeRequired) { this.BeginInvoke(new Action(RefreshImagePanels)); return; }
            if (largeImagePanel != null && !largeImagePanel.IsDisposed) largeImagePanel.Invalidate();
            if (smallImagePanel != null && !smallImagePanel.IsDisposed) smallImagePanel.Invalidate();
        }
        #endregion
        #region ----- Timer -----
        private void StartTimer()
        {
            _elapsedTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            _elapsedTimer.Tick += (s, e) => UpdateElapsedTime();
            _elapsedTimer.Start();
        }
        private void UpdateElapsedTime()
        {
            if (lblElapsed == null || lblElapsed.IsDisposed) return;
            if (_startTime == null) { lblElapsed.Text = ""; return; }
            var elapsed = DateTime.Now - _startTime.Value;
            if (elapsed.TotalHours >= 1)
                lblElapsed.Text = $"⏱️ {(int)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00} elapsed";
            else
                lblElapsed.Text = $"⏱️ {elapsed.Minutes:00}:{elapsed.Seconds:00} elapsed";
        }
        #endregion
        #region ----- Update Presence -----
        public async void UpdatePresence(RichPresence presence)
        {
            if (this.InvokeRequired) { this.BeginInvoke(new Action(() => UpdatePresence(presence))); return; }
            if (!this.Visible) return;
            _currentPresence = presence;
            if (presence == null) { SetIdleState(); return; }
            if (lblAppName != null && !lblAppName.IsDisposed)
                lblAppName.Text = presence.Assets?.LargeImageText ?? "geetRPCS";
            if (lblDetails != null && !lblDetails.IsDisposed)
                lblDetails.Text = presence.Details ?? "Idling...";
            if (lblState != null && !lblState.IsDisposed)
            {
                lblState.Text = presence.State ?? "";
                lblState.Visible = !string.IsNullOrEmpty(presence.State);
            }
            if (presence.Timestamps?.Start != null)
            {
                _startTime = presence.Timestamps.Start;
                UpdateElapsedTime();
            }
            else
            {
                _startTime = null;
                if (lblElapsed != null && !lblElapsed.IsDisposed) lblElapsed.Text = "";
            }
            if (presence.Buttons != null && presence.Buttons.Length > 0)
            {
                if (button1Panel != null && !button1Panel.IsDisposed)
                {
                    button1Panel.Visible = true;
                    if (lblButton1 != null && !lblButton1.IsDisposed)
                        lblButton1.Text = presence.Buttons[0].Label;
                    button1Panel.Tag = presence.Buttons[0].Url;
                }
                if (presence.Buttons.Length > 1)
                {
                    if (button2Panel != null && !button2Panel.IsDisposed)
                    {
                        button2Panel.Visible = true;
                        if (lblButton2 != null && !lblButton2.IsDisposed)
                            lblButton2.Text = presence.Buttons[1].Label;
                        button2Panel.Tag = presence.Buttons[1].Url;
                    }
                }
                else { if (button2Panel != null && !button2Panel.IsDisposed) button2Panel.Visible = false; }
            }
            else
            {
                if (button1Panel != null && !button1Panel.IsDisposed) button1Panel.Visible = false;
                if (button2Panel != null && !button2Panel.IsDisposed) button2Panel.Visible = false;
            }
            string largeKey = presence.Assets?.LargeImageKey ?? "-";
            string smallKey = presence.Assets?.SmallImageKey ?? "-";
            string largeText = presence.Assets?.LargeImageText ?? "-";
            string smallText = presence.Assets?.SmallImageText ?? "-";
            if (lblLargeImageText != null && !lblLargeImageText.IsDisposed)
                lblLargeImageText.Text = $"Large: {largeText} ({largeKey})";
            if (lblSmallImageText != null && !lblSmallImageText.IsDisposed)
                lblSmallImageText.Text = $"Small: {smallText} ({smallKey})";
            await LoadImagesAsync(presence.Assets);
            SetLiveStatus();
        }
        #endregion
        #region ----- States -----
        public void SetIdleState()
        {
            if (this.InvokeRequired) { this.BeginInvoke(new Action(SetIdleState)); return; }
            if (lblAppName != null && !lblAppName.IsDisposed) lblAppName.Text = "geetRPCS";
            if (lblDetails != null && !lblDetails.IsDisposed) lblDetails.Text = "Idling...";
            if (lblState != null && !lblState.IsDisposed)
            {
                lblState.Text = "Ready to work";
                lblState.Visible = true;
            }
            if (lblElapsed != null && !lblElapsed.IsDisposed) lblElapsed.Text = "";
            if (button1Panel != null && !button1Panel.IsDisposed) button1Panel.Visible = false;
            if (button2Panel != null && !button2Panel.IsDisposed) button2Panel.Visible = false;
            if (lblLargeImageText != null && !lblLargeImageText.IsDisposed) lblLargeImageText.Text = "Large: -";
            if (lblSmallImageText != null && !lblSmallImageText.IsDisposed) lblSmallImageText.Text = "Small: -";
            _startTime = null;
            ClearImages();
            SetLiveStatus();
        }
        public void SetPausedState()
        {
            if (this.InvokeRequired) { this.BeginInvoke(new Action(SetPausedState)); return; }
            if (lblStatus != null && !lblStatus.IsDisposed)
            {
                lblStatus.Text = "● Paused";
                lblStatus.ForeColor = DiscordYellow;
            }
            if (lblDetails != null && !lblDetails.IsDisposed) lblDetails.Text = "Presence paused";
            if (lblState != null && !lblState.IsDisposed)
            {
                lblState.Text = "Not showing on Discord";
                lblState.Visible = true;
            }
            if (lblElapsed != null && !lblElapsed.IsDisposed) lblElapsed.Text = "";
            _startTime = null;
        }
        private void SetLiveStatus()
        {
            if (lblStatus != null && !lblStatus.IsDisposed)
            {
                lblStatus.Text = "● Live";
                lblStatus.ForeColor = DiscordGreen;
            }
        }
        #endregion
        #region ----- Visibility -----
        public void ToggleVisibility()
        {
            if (this.Visible) 
            {
                this.Hide();
                ClearImageMemoryCache();
            }
            else { this.Show(); this.BringToFront(); }
        }
        #endregion
        #region ----- Form Closing -----
        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            _elapsedTimer?.Stop();
            _elapsedTimer?.Dispose();
            ClearImageMemoryCache();
            _httpClient?.Dispose();
        }
        #endregion

    }
}
