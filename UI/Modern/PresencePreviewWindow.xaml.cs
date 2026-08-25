/**
 * geetRPCS - Discord Presence Preview window (ModernWpf / Fluent)
 * WPF replacement for the WinForms PresencePreviewForm. Same features:
 * Discord asset mapping + CDN image loading with disk/memory cache,
 * placeholder emoji by asset key, 1s elapsed timer, up to two action
 * buttons, asset info + CDN status, refresh / clear-cache / always-on-top
 * footer buttons, double-click to hide, bottom-right placement.
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
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DiscordRPC;
using geetRPCS.Services;

namespace geetRPCS.UI.Modern
{
    public partial class PresencePreviewWindow : Window
    {
        #region ----- Fields -----
        private RichPresence _currentPresence;
        private DispatcherTimer _elapsedTimer;
        private DateTime? _startTime;
        private string _applicationId;
        private readonly object _imageLock = new object();
        // Shared for the whole process: one HttpClient per preview open kept
        // re-allocating the SocketsHttpHandler + connection pool and
        // re-establishing connections. Never disposed (safe for a tray app).
        private static readonly HttpClient _httpClient = CreateHttpClient();
        private readonly string CacheFolder = Utils.AppPaths.ImageCacheDir;
        // Decoded bitmaps are full ARGB surfaces; the FIFO order bounds the
        // worst case during long sessions with many asset switches (the cache
        // is also cleared on hide/close).
        private const int MAX_CACHED_IMAGES = 16;
        private readonly Dictionary<string, BitmapImage> _imageCache = new Dictionary<string, BitmapImage>();
        private readonly Queue<string> _imageCacheOrder = new Queue<string>();
        private Dictionary<string, string> _assetIdCache = new Dictionary<string, string>();
        private bool _assetsLoaded = false;
        private bool _isFetchingAssets = false;

        #endregion

        #region ----- Constructor -----
        public PresencePreviewWindow(string applicationId = null)
        {
            InitializeComponent();

            Title = LanguageManager.Current.WindowPreviewTitle ?? "Discord Presence Preview";
            HeaderLabel.Text = LanguageManager.Current.PreviewPlayingGame ?? "PLAYING A GAME";
            StatusText.Text = LanguageManager.Current.PreviewLive ?? "● Live";
            StatusText.SetResourceReference(TextBlock.ForegroundProperty, "SystemFillColorSuccessBrush");
            DetailsText.Text = LanguageManager.Current.PreviewIdling;
            StateText.Text = LanguageManager.Current.PreviewReadyToWork;
            LargeImageInfo.Text = LanguageManager.Current.PreviewLargeEmpty;
            SmallImageInfo.Text = LanguageManager.Current.PreviewSmallEmpty;
            CdnStatus.Text = LanguageManager.Current.PreviewInitializing;
            AssetInfoHeader.Text = LanguageManager.Current.PreviewHeaderAssetInfo ?? "Asset Info";
            RefreshButton.Content = FluentGlyphs.Refresh;
            ClearCacheButton.Content = FluentGlyphs.Delete;
            PinButton.Content = FluentGlyphs.Pin; // Topmost starts OFF — pin only on user request
            // (a default-on Topmost made the preview float above every app and
            // above the tray context menu until the user noticed the pin button)
            RefreshButton.ToolTip = LanguageManager.Current.PreviewRefreshAssets ?? "Refresh Assets";
            ClearCacheButton.ToolTip = LanguageManager.Current.PreviewClearCache ?? "Clear Cache";
            PinButton.ToolTip = LanguageManager.Current.PreviewAlwaysOnTop ?? "Always on Top";
            ToolTip = LanguageManager.Current.PreviewDoubleClickHide ?? "💡 Double-click to hide";
            Button1.Content = string.Format(LanguageManager.Current.PreviewButtonPlaceholder ?? "{0}", 1);
            Button2.Content = string.Format(LanguageManager.Current.PreviewButtonPlaceholder ?? "{0}", 2);

            try
            {
                string iconPath = Utils.AppPaths.IconPath;
                if (File.Exists(iconPath)) Icon = LoadIcon(iconPath);
            }
            catch { }

            _applicationId = applicationId;
            EnsureCacheFolder();

            StartTimer();
            // Don't tick the 1s elapsed timer while hidden: the window is commonly
            // hidden by the double-click or parked behind the modal Manage Apps
            // window, and each tick re-runs layout on ElapsedText for nothing.
            IsVisibleChanged += (s, e) =>
            {
                if (_elapsedTimer != null) _elapsedTimer.IsEnabled = IsVisible;
            };
            // Same deferred force-foreground pattern as ManageAppsWindow: when opened
            // from the tray menu, retry the Win32 foreground sequence until the
            // window is actually active (keeps it interactive immediately). The
            // TOPMOST flip is skipped because this window is always-on-top by design.
            Loaded += (s, e) =>
            {
                PositionBottomRight();
                int attempt = 0;
                var activateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
                activateTimer.Tick += (s2, e2) =>
                {
                    attempt++;
                    WindowActivation.ForceForeground(this);
                    if (IsActive || attempt >= 4) activateTimer.Stop();
                };
                activateTimer.Start();
            };
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

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "geetRPCS/1.0");
            return client;
        }

        /// <summary>Inserts into the memory image cache with a FIFO bound.</summary>
        internal void CacheImage(string cacheKey, BitmapImage image)
        {
            lock (_imageLock)
            {
                if (!_imageCache.ContainsKey(cacheKey)) _imageCacheOrder.Enqueue(cacheKey);
                _imageCache[cacheKey] = image;
                while (_imageCache.Count > MAX_CACHED_IMAGES && _imageCacheOrder.Count > 0)
                {
                    _imageCache.Remove(_imageCacheOrder.Dequeue());
                }
            }
        }

        private void PositionBottomRight()
        {
            Left = SystemParameters.WorkArea.Right - ActualWidth - 20;
            Top = SystemParameters.WorkArea.Bottom - ActualHeight - 20;
        }

        private static ImageSource LoadIcon(string path)
        {
            using var icon = new System.Drawing.Icon(path);
            return Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        }
        #endregion

        #region ----- Discord API -----
        private async Task LoadAssetsMappingAsync()
        {
            if (string.IsNullOrEmpty(_applicationId))
            {
                UpdateCdnStatus(LanguageManager.Current.PreviewStatusNoAppId ?? "❌ No Application ID", "SystemFillColorCautionBrush");
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
                            UpdateCdnStatus(string.Format(LanguageManager.Current.PreviewStatusAssetsCached ?? "✅ {0} assets (cached)", _assetIdCache.Count), "SystemFillColorSuccessBrush");
                            _isFetchingAssets = false;
                            if (_currentPresence != null) await LoadImagesAsync(_currentPresence.Assets);
                            return;
                        }
                    }
                }
                catch { }
            }
            UpdateCdnStatus(LanguageManager.Current.PreviewStatusFetching ?? "📡 Fetching from Discord...", "SystemFillColorCautionBrush");
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
                        UpdateCdnStatus(LanguageManager.Current.PreviewStatusNoAssets ?? "⚠️ No assets found", "SystemFillColorCautionBrush");
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
                        UpdateCdnStatus(string.Format(LanguageManager.Current.PreviewStatusAssetsLoaded ?? "✅ {0} assets loaded", validCount), "SystemFillColorSuccessBrush");
                    }
                    if (_currentPresence != null) await LoadImagesAsync(_currentPresence.Assets);
                }
                else UpdateCdnStatus(string.Format(LanguageManager.Current.PreviewStatusApiError ?? "⚠️ API Error: {0}", response.StatusCode), "SystemFillColorCautionBrush");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error mapping assets: {ex}");
                UpdateCdnStatus(LanguageManager.Current.PreviewStatusError ?? "❌ Error", "SystemFillColorCautionBrush");
            }
            finally
            {
                _isFetchingAssets = false;
            }
        }

        /// <summary>Sets the CDN status line using a theme resource key (DynamicResource, theme-aware).</summary>
        private void UpdateCdnStatus(string text, string colorKey)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(() => UpdateCdnStatus(text, colorKey)));
                return;
            }
            CdnStatus.Text = text;
            CdnStatus.SetResourceReference(TextBlock.ForegroundProperty, colorKey);
        }
        #endregion

        #region ----- Image Loading -----
        private async Task LoadImagesAsync(Assets assets)
        {
            if (assets == null) { ClearImages(); return; }
            ShowLoading(true);
            try
            {
                BitmapImage large = null, small = null;
                if (!string.IsNullOrEmpty(assets.LargeImageKey))
                {
                    large = await GetImageAsync(assets.LargeImageKey);
                    UpdatePlaceholderEmoji(assets.LargeImageKey);
                }
                if (!string.IsNullOrEmpty(assets.SmallImageKey))
                    small = await GetImageAsync(assets.SmallImageKey);
                ApplyImages(large, small);
            }
            catch (Exception ex) { Debug.WriteLine($"Error loading images: {ex.Message}"); }
            finally { ShowLoading(false); }
        }

        private void ApplyImages(BitmapImage large, BitmapImage small)
        {
            if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(new Action(() => ApplyImages(large, small))); return; }
            LargeImageBrush.ImageSource = large;
            SmallImageBrush.ImageSource = small;
            LargePlaceholder.Visibility = large == null ? Visibility.Visible : Visibility.Collapsed;
            SmallCheck.Visibility = small == null ? Visibility.Visible : Visibility.Collapsed;
        }

        private async Task<BitmapImage> GetImageAsync(string key)
        {
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(_applicationId)) return null;
            string keyLower = key.ToLower();
            string cacheKey = $"{_applicationId}_{keyLower}";
            lock (_imageLock)
            {
                if (_imageCache.TryGetValue(cacheKey, out var cached)) return cached;
            }
            string diskCachePath = Path.Combine(CacheFolder, $"{cacheKey}.png");
            if (File.Exists(diskCachePath))
            {
                try
                {
                    byte[] fileBytes = await File.ReadAllBytesAsync(diskCachePath);
                    var diskImage = LoadBitmap(fileBytes);
                    CacheImage(cacheKey, diskImage);
                    return diskImage;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error loading disk cache: {ex.Message}");
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
                    Debug.WriteLine($"Asset ID not found for: {key}");
                    return null;
                }
            }
            try
            {
                string url = $"https://cdn.discordapp.com/app-assets/{_applicationId}/{assetId}.png";
                Debug.WriteLine($"Downloading: {url}");
                using var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    byte[] imageBytes = await response.Content.ReadAsByteArrayAsync();
                    var image = LoadBitmap(imageBytes);
                    CacheImage(cacheKey, image);
                    try { await File.WriteAllBytesAsync(diskCachePath, imageBytes); } catch { }
                    return image;
                }
                else Debug.WriteLine($"Failed to download {key}: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error downloading {key}: {ex.Message}");
            }
            return null;
        }

        private static BitmapImage LoadBitmap(byte[] bytes)
        {
            using var ms = new MemoryStream(bytes);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = ms;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }

        private void ShowLoading(bool show)
        {
            if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(new Action(() => ShowLoading(show))); return; }
            LargeLoading.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            if (!show && LargeImageBrush.ImageSource == null)
                LargePlaceholder.Visibility = Visibility.Visible;
        }

        private void ClearImages()
        {
            ApplyImages(null, null);
            LargePlaceholder.Text = "🎵";
        }

        private void ClearImageMemoryCache()
        {
            lock (_imageLock)
            {
                _imageCache.Clear();
                _imageCacheOrder.Clear();
            }
            ApplyImages(null, null);
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
            LargePlaceholder.Text = emoji;
        }
        #endregion

        #region ----- Timer -----
        private void StartTimer()
        {
            _elapsedTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _elapsedTimer.Tick += (s, e) => UpdateElapsedTime();
            _elapsedTimer.Start();
        }

        private void UpdateElapsedTime()
        {
            if (_startTime == null) { ElapsedText.Text = ""; return; }
            var elapsed = DateTime.Now - _startTime.Value;
            string elapsedText = elapsed.TotalHours >= 1
                ? $"{(int)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}"
                : $"{elapsed.Minutes:00}:{elapsed.Seconds:00}";
            ElapsedText.Text = string.Format(LanguageManager.Current.PreviewElapsed, elapsedText);
        }
        #endregion

        #region ----- Update Presence -----
        public async void UpdatePresence(RichPresence presence)
        {
            if (!Dispatcher.CheckAccess()) { _ = Dispatcher.BeginInvoke(new Action(() => UpdatePresence(presence))); return; }
            if (!IsVisible) return;
            _currentPresence = presence;
            if (presence == null) { SetIdleState(); return; }

            AppNameText.Text = presence.Assets?.LargeImageText ?? "geetRPCS";
            DetailsText.Text = presence.Details ?? LanguageManager.Current.PreviewIdling;
            StateText.Text = presence.State ?? "";
            StateText.Visibility = string.IsNullOrEmpty(presence.State)
                ? Visibility.Collapsed : Visibility.Visible;

            if (presence.Timestamps?.Start != null)
            {
                _startTime = presence.Timestamps.Start;
                UpdateElapsedTime();
            }
            else
            {
                _startTime = null;
                ElapsedText.Text = "";
            }

            if (presence.Buttons != null && presence.Buttons.Length > 0)
            {
                Button1.Visibility = Visibility.Visible;
                Button1.Content = presence.Buttons[0].Label;
                Button1.Tag = presence.Buttons[0].Url;
                if (presence.Buttons.Length > 1)
                {
                    Button2.Visibility = Visibility.Visible;
                    Button2.Content = presence.Buttons[1].Label;
                    Button2.Tag = presence.Buttons[1].Url;
                }
                else Button2.Visibility = Visibility.Collapsed;
            }
            else
            {
                Button1.Visibility = Visibility.Collapsed;
                Button2.Visibility = Visibility.Collapsed;
            }

            string largeKey = presence.Assets?.LargeImageKey ?? "-";
            string smallKey = presence.Assets?.SmallImageKey ?? "-";
            string largeText = presence.Assets?.LargeImageText ?? "-";
            string smallText = presence.Assets?.SmallImageText ?? "-";
            LargeImageInfo.Text = string.Format(LanguageManager.Current.PreviewLargeImage, largeText, largeKey);
            SmallImageInfo.Text = string.Format(LanguageManager.Current.PreviewSmallImage, smallText, smallKey);

            await LoadImagesAsync(presence.Assets);
            SetLiveStatus();
        }
        #endregion

        #region ----- States -----
        public void SetIdleState()
        {
            if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(new Action(SetIdleState)); return; }
            AppNameText.Text = "geetRPCS";
            DetailsText.Text = LanguageManager.Current.PreviewIdling;
            StateText.Text = LanguageManager.Current.PreviewReadyToWork;
            StateText.Visibility = Visibility.Visible;
            ElapsedText.Text = "";
            Button1.Visibility = Visibility.Collapsed;
            Button2.Visibility = Visibility.Collapsed;
            LargeImageInfo.Text = LanguageManager.Current.PreviewLargeEmpty;
            SmallImageInfo.Text = LanguageManager.Current.PreviewSmallEmpty;
            _startTime = null;
            ClearImages();
            SetLiveStatus();
        }

        public void SetPausedState()
        {
            if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(new Action(SetPausedState)); return; }
            StatusText.Text = LanguageManager.Current.PreviewPaused;
            StatusText.SetResourceReference(TextBlock.ForegroundProperty, "SystemFillColorCautionBrush");
            DetailsText.Text = LanguageManager.Current.PreviewPresencePaused;
            StateText.Text = LanguageManager.Current.PreviewNotShowing;
            StateText.Visibility = Visibility.Visible;
            ElapsedText.Text = "";
            _startTime = null;
        }

        private void SetLiveStatus()
        {
            if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(new Action(SetLiveStatus)); return; }
            StatusText.Text = LanguageManager.Current.PreviewLive;
            StatusText.SetResourceReference(TextBlock.ForegroundProperty, "SystemFillColorSuccessBrush");
        }
        #endregion

        #region ----- Visibility / actions -----
        public void ToggleVisibility()
        {
            if (IsVisible)
            {
                Hide();
                ClearImageMemoryCache();
            }
            else { Show(); Activate(); }
        }

        private void OnWindowDoubleClick(object sender, MouseButtonEventArgs e) => Hide();

        private void OnButtonClick(object sender, RoutedEventArgs e)
        {
            if (((System.Windows.Controls.Button)sender).Tag is string url && !string.IsNullOrEmpty(url))
            {
                try
                {
                    Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
                }
                catch { }
            }
        }

        private async void OnRefreshClick(object sender, RoutedEventArgs e)
        {
            UpdateCdnStatus(LanguageManager.Current.PreviewStatusRefreshing ?? "🔄 Refreshing...", "SystemFillColorCautionBrush");
            _assetsLoaded = false;
            await LoadAssetsMappingAsync();
            if (_currentPresence != null)
            {
                ClearImageMemoryCache();
                await LoadImagesAsync(_currentPresence.Assets);
            }
        }

        private void OnClearCacheClick(object sender, RoutedEventArgs e)
        {
            ClearAllCache();
            UpdateCdnStatus(LanguageManager.Current.PreviewStatusCacheCleared ?? "🗑️ Cache cleared!", "SystemFillColorSuccessBrush");
        }

        private void OnPinClick(object sender, RoutedEventArgs e)
        {
            Topmost = !Topmost;
            PinButton.Content = Topmost ? FluentGlyphs.Pinned : FluentGlyphs.Pin;
        }
        #endregion

        #region ----- Closing -----
        protected override void OnClosed(EventArgs e)
        {
            _elapsedTimer?.Stop();
            ClearImageMemoryCache();
            // _httpClient is process-shared and intentionally not disposed.
            base.OnClosed(e);
        }
        #endregion

        // Test accessors (InternalsVisibleTo: Tests)
        internal int CachedImageCount { get { lock (_imageLock) return _imageCache.Count; } }
        internal bool HasCachedImage(string key) { lock (_imageLock) return _imageCache.ContainsKey(key); }
        internal string AppNameValue => AppNameText.Text;
        internal string DetailsValue => DetailsText.Text;
        internal string StateValue => StateText.Text;
        internal bool IsButton1Visible => Button1.Visibility == Visibility.Visible;
        internal bool IsButton2Visible => Button2.Visibility == Visibility.Visible;
        internal string StatusValue => StatusText.Text;
        internal string RefreshButtonGlyph => RefreshButton.Content as string;
        internal string ClearCacheButtonGlyph => ClearCacheButton.Content as string;
        internal string PinButtonGlyph => PinButton.Content as string;
        internal bool IsElapsedTimerRunning => _elapsedTimer != null && _elapsedTimer.IsEnabled;
    }
}
