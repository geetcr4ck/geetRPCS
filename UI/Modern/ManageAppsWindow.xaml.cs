/**
 * geetRPCS - Manage Applications window (ModernWpf / Fluent)
 * WPF replacement for the WinForms ManageAppsForm. Shows the app database
 * with enable/disable toggles, the FULL per-app presence override editor
 * (Details/State, large image, Application ID, elapsed time, buttons) and
 * the Add Custom App flow. Overrides write through to settings.json; custom
 * apps are stored there too and merged over apps.json at load.
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
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using geetRPCS.Models;
using geetRPCS.Services;

namespace geetRPCS.UI.Modern
{
    public partial class ManageAppsWindow : Window
    {
        private readonly List<AppItemViewModel> _allItems = new List<AppItemViewModel>();
        private readonly ListCollectionView _view;
        private readonly DispatcherTimer _searchTimer;
        private readonly Action<string, bool> _onAppToggled;
        private readonly Action<string, AppOverrideConfig> _onOverrideChanged;
        private readonly Action<AppConfig> _onAddCustomApp;
        private readonly Action<string> _onRemoveCustomApp;
        private HashSet<string> _customProcesses;
        private string _lastAppliedFilter; // null until the first ApplyFilter
        private readonly System.Diagnostics.Stopwatch _openSw = new System.Diagnostics.Stopwatch();
        private static ImageSource _windowIcon;

        /// <summary>True while the off-screen startup pre-show is active. Set by
        /// the host (Program.PreCreateManageAppsWindow), cleared by
        /// PrepareForShow. Replaces the old Left/Top &lt; 0 heuristic, which
        /// misfired on monitors whose work area starts at negative coordinates
        /// (the fade was skipped and an invisible Opacity-0 modal remained).</summary>
        internal bool IsPreShow { get; set; }

        /// <summary>All items (unfiltered). Exposed for tests.</summary>
        internal IReadOnlyList<AppItemViewModel> Items => _allItems;

        /// <summary>Raised after a custom app was added/removed so the host can
        /// push fresh data back into RefreshData (the window never reads the
        /// managers itself, keeping it testable).</summary>
        internal event EventHandler DataReloadRequested;

        /// <summary>Compatibility signature (details/state-only override callback,
        /// no custom-app actions) kept for existing tests/self-tests.</summary>
        public ManageAppsWindow(
            IReadOnlyList<AppConfig> apps,
            HashSet<string> disabledApps,
            Dictionary<string, AppOverrideConfig> overrides,
            Action<string, bool> onAppToggled,
            Action<string, string, string> onOverrideChanged)
            : this(apps, disabledApps, overrides, null, onAppToggled,
                (proc, ov) => onOverrideChanged?.Invoke(proc, ov?.Details ?? "", ov?.State ?? ""),
                null, null)
        {
        }

        public ManageAppsWindow(
            IReadOnlyList<AppConfig> apps,
            HashSet<string> disabledApps,
            Dictionary<string, AppOverrideConfig> overrides,
            HashSet<string> customProcesses,
            Action<string, bool> onAppToggled,
            Action<string, AppOverrideConfig> onOverrideChanged,
            Action<AppConfig> onAddCustomApp,
            Action<string> onRemoveCustomApp)
        {
            InitializeComponent();

            Title = LanguageManager.Current.WindowManageAppsTitle ?? "Manage Applications";
            TitleText.Text = LanguageManager.Current.ManageAppsTitle ?? "MANAGE APPS";
            SearchPlaceholder.Text = LanguageManager.Current.ManageAppsSearch ?? "Search apps...";
            AddAppButton.Content = FluentGlyphs.StripLeadingEmoji(LanguageManager.Current.ManageAddApp ?? "➕ Add Custom App");
            _onAppToggled = onAppToggled;
            _onOverrideChanged = onOverrideChanged;
            _onAddCustomApp = onAddCustomApp;
            _onRemoveCustomApp = onRemoveCustomApp;
            // Without the callback (compat ctor / tests) the button would be a no-op.
            if (onAddCustomApp == null) AddAppButton.Visibility = Visibility.Collapsed;

            // Single stable view bound ONCE. Filtering mutates the view's Filter
            // predicate instead of replacing AppsList.ItemsSource with a new List
            // on every keystroke (the old approach reset the item generator on each
            // keystroke, re-realizing ~30 visible rows of Expander+TextBoxes and
            // hitching the UI for a few ms).
            _view = new ListCollectionView(_allItems);
            RefreshData(apps, disabledApps, overrides, customProcesses);

            // Search is debounced (~120ms): re-filtering only after the user pauses
            // typing, so per-keystroke cost is just the TextBox itself. Clearing via
            // the TextBox's built-in X button hits the same path and is instant too.
            // The re-filter itself runs at Background priority: the user has paused
            // typing, so it lands in an idle dispatcher slot (after rendering)
            // instead of blocking input processing.
            _searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
            _searchTimer.Tick += (s, e) =>
            {
                _searchTimer.Stop();
                Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(ApplyFilter));
            };

            // Populate the list AFTER the window is visible at Background priority:
            // the first layout pass stays cheap (no rows), so the window paints
            // instantly and the rows materialize a frame later — the reported
            // few-ms freeze on open disappears.
            Loaded += (s, e) =>
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                {
                    if (!IsLoaded) return;
                    AppsList.ItemsSource = _view;
                    ApplyFilter();
                }));
            };

            // The window is opened from the tray menu while the ContextMenuStrip is
            // still closing; WinForms can restore focus to the previously active
            // window after that, stealing keyboard input. Retry a few times using
            // the full Win32 "force foreground" sequence (SetWindowPos TOPMOST
            // flip + BringWindowToTop + SetForegroundWindow) so the search box is
            // immediately typeable even under the OS foreground lock.
            Loaded += (s, e) =>
            {
                // The startup pre-show (off-screen, Opacity=0, then hidden) must
                // NOT run the focus machinery below: ForceSearchBoxFocus calls
                // WindowActivation.ForceForeground (ShowWindow SW_RESTORE + …),
                // which re-shows the hidden off-screen window — leaving it
                // visible at (-32000,-32000) so ToggleManageAppsVisibility takes
                // the wrong branch (Activate instead of reuse+ShowDialog). The
                // real show re-focuses via the Activated handler below.
                if (IsPreShow) return;
                // Modal ShowDialog makes this the active window immediately, so
                // focus the search box right away — the first keystroke lands
                // without waiting for the retry timer's 150ms tick. The retry
                // loop below stays as a safety net if focus is stolen later.
                SearchBox.Focus();
                Keyboard.Focus(SearchBox);
                int attempt = 0;
                var focusTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
                focusTimer.Tick += (s2, e2) =>
                {
                    attempt++;
                    // If the user already has focus in the search box (they started
                    // typing), stop retrying — forcing the Win32 foreground mid-typing
                    // steals keyboard state and feels like a freeze.
                    if (SearchBox.IsKeyboardFocused && IsActive)
                    {
                        focusTimer.Stop();
                        return;
                    }
                    if (ForceSearchBoxFocus())
                    {
                        focusTimer.Stop();
                        LogService.Log($"Search box focused (attempt {attempt})", "INFO", "ManageApps");
                    }
                    else if (attempt >= 5)
                    {
                        // One concise WARN with diagnostics instead of logging every attempt.
                        focusTimer.Stop();
                        IntPtr hWnd = new WindowInteropHelper(this).Handle;
                        IntPtr fg = Utils.PInvoke.User32.GetForegroundWindow();
                        LogService.Log(
                            $"Search box focus retries exhausted: active={IsActive} fgMatch={fg == hWnd} " +
                            $"kbdFocused={SearchBox.IsKeyboardFocused} " +
                            $"fgClass='{Utils.PInvoke.User32.GetForegroundWindowClass()}'",
                            "WARN", "ManageApps");
                    }
                };
                focusTimer.Start();
            };

            // Safety net: if the user re-activates the window later (alt-tab back),
            // put focus back in the search box so typing works right away.
            Activated += (s, e) =>
            {
                ReleaseTopmostAfterActivation();
                if (IsVisible && !SearchBox.IsKeyboardFocused)
                {
                    SearchBox.Focus();
                    Keyboard.Focus(SearchBox);
                }
            };

            // Esc closes the window (modal-dialog convention). TextBox does not
            // consume Escape, so the tunneling PreviewKeyDown on the window sees it.
            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    Close();
                    e.Handled = true;
                }
            };

            // Lifecycle: a FRESH window per open (same as the other tray dialogs).
            // That is what gets the native Win10/11 DWM open animation on every
            // open; a hidden-then-reshown HWND does not reliably re-play it.
            // The no-white-flash guarantees are independent of the lifecycle:
            // WM_ERASEBKGND suppression + the startup warm-up below.

            try { Icon = GetWindowIcon(); } catch { }

            // Kill the last remaining open artifact: the OS paints a window's
            // class background (white) on show before WPF presents its first
            // frame. Measured as one full-white frame (~16ms) at open even with
            // fully warm content. WPF paints the entire surface itself, so the
            // erase is pure waste: suppress it and DWM re-composites the last
            // presented (dark) frame until the new one arrives.
            SourceInitialized += (s, e) =>
            {
                if (PresentationSource.FromVisual(this) is HwndSource src)
                    src.AddHook(SuppressEraseBkgnd);
            };

            // Measure the real open cost in the real app. RefreshData restarts the
            // stopwatch before each real show. Loaded fires for fresh windows
            // (first ever Show); IsVisibleChanged fires on every show including
            // reuses of the pre-created window (whose Loaded does not re-fire).
            Loaded += (s, e) =>
                LogService.Log($"ManageAppsWindow loaded in {_openSw.ElapsedMilliseconds}ms", "INFO", "ManageApps");
            IsVisibleChanged += (s, e) =>
            {
                if (IsVisible)
                {
                    LogService.Log($"ManageAppsWindow visible in {_openSw.ElapsedMilliseconds}ms", "INFO", "ManageApps");
                }
                else
                {
                    // Drop topmost while the window is not shown so it never
                    // floats above other apps' fullscreen sessions
                    // (PrepareForShow re-arms it for the next open).
                    Topmost = false;
                }
            };
        }

        /// <summary>WndProc hook: swallows WM_ERASEBKGND so the OS never paints the
        /// white class-background fill on show (see SourceInitialized hook).</summary>
        private static IntPtr SuppressEraseBkgnd(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_ERASEBKGND = 0x0014;
            if (msg == WM_ERASEBKGND) handled = true;
            return IntPtr.Zero;
        }

        /// <summary>Arms the window for a real (user-visible) show: Topmost for
        /// the shown session, because ShowDialog's own activation loses the
        /// foreground fight against a fullscreen/always-on-top player (measured:
        /// the dialog stayed at z-order 32 behind a fullscreen mpv). The window
        /// appears INSTANTLY at full opacity — no fade: the native DWM open
        /// animation (played because every open is a fresh window, same
        /// lifecycle as the other tray dialogs) provides the motion instead.
        /// The pin is TEMPORARY: ReleaseTopmostAfterActivation drops it as soon
        /// as the window is activated, so it never floats above other apps (or
        /// the tray menu) for the rest of the session.</summary>
        internal void PrepareForShow()
        {
            IsPreShow = false;
            Topmost = true;
        }

        /// <summary>Once the modal open has actually ACTIVATED, the z-order fight
        /// is over — un-pin Topmost. Keeping it on for the whole shown session
        /// made the window float above every other application (and above the
        /// tray context menu) while the user worked elsewhere. Deferred one
        /// dispatcher turn so the activation fully settles first.</summary>
        private void ReleaseTopmostAfterActivation()
        {
            if (!Topmost) return;
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                if (IsVisible) Topmost = false;
            }));
        }

        /// <summary>Compatibility 3-arg RefreshData (no custom-app set).</summary>
        internal void RefreshData(
            IReadOnlyList<AppConfig> apps,
            HashSet<string> disabledApps,
            Dictionary<string, AppOverrideConfig> overrides)
            => RefreshData(apps, disabledApps, overrides, _customProcesses);

        /// <summary>Rebuilds the app list from the current config. Called by the ctor
        /// and again right before each real show (the parked hidden window must
        /// reflect config changes). When the app list is UNCHANGED (the common
        /// open), the rebuild and _view.Refresh() are skipped entirely: Refresh()
        /// resets the item generator, so every realized row container would be
        /// re-created at the next layout pass. On machines where that pass takes
        /// ~250-350ms the window is shown with the render still pending, and the
        /// OS fills the not-yet-painted HWND white for that whole period (the
        /// measured white flash; Window.Opacity cannot mask it because it happens
        /// before WPF renders anything). Keeping the warm rows makes the re-show
        /// present cached visuals instead.</summary>
        internal void RefreshData(
            IReadOnlyList<AppConfig> apps,
            HashSet<string> disabledApps,
            Dictionary<string, AppOverrideConfig> overrides,
            HashSet<string> customProcesses)
        {
            // Per-open cost of rebuilding the rows, logged at INFO so a still-
            // failing report can pinpoint whether RefreshData itself is the hitch.
            var sw = System.Diagnostics.Stopwatch.StartNew();
            _customProcesses = customProcesses != null
                ? new HashSet<string>(customProcesses, StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var sorted = (apps ?? Array.Empty<AppConfig>())
                .OrderBy(a => a.AppName, StringComparer.OrdinalIgnoreCase).ToArray();
            bool unchanged = _allItems.Count == sorted.Length;
            if (unchanged)
            {
                for (int i = 0; i < _allItems.Count; i++)
                {
                    if (!StringComparer.OrdinalIgnoreCase.Equals(_allItems[i].App.Process, sorted[i].Process))
                    { unchanged = false; break; }
                }
            }
            if (unchanged)
            {
                // Same apps: sync enable/override state in place. Setting the VM
                // properties does NOT fire the write-through callbacks (this is a
                // state sync from the coordinator, not a user edit).
                var disabled = new HashSet<string>(
                    disabledApps ?? new HashSet<string>(), StringComparer.OrdinalIgnoreCase);
                foreach (var vm in _allItems) vm.RefreshState(disabled, overrides, _customProcesses);
                if (AppsList.ItemsSource != null) ApplyFilter();
                sw.Stop();
                _openSw.Restart();
                LogService.Log(
                    $"ManageAppsWindow RefreshData skipped rebuild ({_allItems.Count} items unchanged) in {sw.ElapsedMilliseconds}ms",
                    "DEBUG", "ManageApps");
                return;
            }
            var disabledSet = new HashSet<string>(
                disabledApps ?? new HashSet<string>(), StringComparer.OrdinalIgnoreCase);
            _allItems.Clear();
            foreach (var a in sorted)
            {
                _allItems.Add(new AppItemViewModel(a, !disabledSet.Contains(a.Process), overrides,
                    _customProcesses.Contains(a.Process), _onAppToggled, _onOverrideChanged));
            }
            _view.Refresh();
            if (AppsList.ItemsSource != null) ApplyFilter();
            sw.Stop();
            _openSw.Restart();
            if (_allItems.Count > 0)
                LogService.Log(
                    $"ManageAppsWindow rebuilt {_allItems.Count} items in {sw.ElapsedMilliseconds}ms",
                    "INFO", "ManageApps");
            // Data changed: force the (expensive) row realization NOW, while the
            // window is still hidden, so the real show starts from an already
            // laid-out tree instead of a pending white pre-paint fill.
            if (!IsVisible) UpdateLayout();
        }

        /// <summary>Window icon, decoded once per process (the 226KB .ico decode +
        /// HICON conversion takes ~30-50ms — paying it on every open made the ctor
        /// the biggest chunk of the reported open freeze).</summary>
        private static ImageSource GetWindowIcon()
        {
            if (_windowIcon != null) return _windowIcon;
            string iconPath = Utils.AppPaths.IconPath;
            if (File.Exists(iconPath))
            {
                using var icon = new System.Drawing.Icon(iconPath);
                _windowIcon = Imaging.CreateBitmapSourceFromHIcon(
                    icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                if (_windowIcon.CanFreeze) _windowIcon.Freeze();
            }
            return _windowIcon;
        }

        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            // Placeholder toggles immediately (cheap); the list re-filter is debounced.
            // Deliberately NO logging here: with logLevel DEBUG a per-keystroke log
            // line used to write to disk synchronously on every key — a self-inflicted
            // hitch while typing and on the clear-X click. (The keystroke cost was
            // already measured: ~0.1ms/key, input stack is not the bottleneck.)
            SearchPlaceholder.Visibility = string.IsNullOrEmpty(SearchBox.Text)
                ? Visibility.Visible : Visibility.Collapsed;
            _searchTimer.Stop();
            _searchTimer.Start();
        }

        /// <summary>Applies the current search text to the stable filtered view.
        /// Called from the debounce timer (typing/clearing) and once at load.
        /// Skips when the text has not changed since the last application (the
        /// debounce timer can restart with identical text, e.g. after
        /// RefreshData), saving a full view refresh + row re-realization.</summary>
        private void ApplyFilter()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            string filter = SearchBox.Text?.Trim() ?? "";
            if (filter == _lastAppliedFilter) return;
            _lastAppliedFilter = filter;
            if (filter.Length == 0)
            {
                _view.Filter = null;
            }
            else
            {
                // Ordinal Contains over the precomputed lowercase SearchKey:
                // same matches as the old per-item OrdinalIgnoreCase Contains on
                // Name + Process, without re-case-converting both strings per
                // item per keystroke-pause.
                string key = filter.ToLowerInvariant();
                _view.Filter = obj => obj is AppItemViewModel v &&
                    v.SearchKey.Contains(key, StringComparison.Ordinal);
            }
            CountText.Text = string.Format(
                LanguageManager.Current.ManageAppsFound ?? "{0} apps found", _view.Count);
            sw.Stop();
            // DEBUG-level (visible when settings.json logLevel is DEBUG): lets a
            // still-failing report be pinpointed from geetRPCS.log — if the filter
            // itself is the hitch this line shows it.
            LogService.Log(
                $"Search filter applied ({_view.Count} of {_allItems.Count} shown) in {sw.ElapsedMilliseconds}ms",
                "DEBUG", "ManageApps");
        }

        /// <summary>
        /// Forces the window to the Win32 foreground (see WindowActivation) and
        /// focuses the search box. Returns true once the box really has focus
        /// and the window is active.
        /// </summary>
        private bool ForceSearchBoxFocus()
        {
            WindowActivation.ForceForeground(this);
            SearchBox.Focus();
            Keyboard.Focus(SearchBox);
            return IsActive && SearchBox.IsKeyboardFocused;
        }

        // ----- Custom app actions -----
        private void OnAddAppClick(object sender, RoutedEventArgs e)
        {
            if (_onAddCustomApp == null) return;
            var dlg = new AddCustomAppWindow(_allItems.Select(i => i.App.Process).ToList());
            if (dlg.ShowDialog() == true && dlg.Result != null)
            {
                _onAddCustomApp(dlg.Result);
                DataReloadRequested?.Invoke(this, EventArgs.Empty);
                MessageDialog.ShowInfo(
                    string.Format(LanguageManager.Current.MsgAppAdded ?? "Custom app '{0}' added.",
                        dlg.Result.AppName ?? dlg.Result.Process),
                    LanguageManager.Current.AppName);
            }
        }

        private void OnDeleteCustomAppClick(object sender, RoutedEventArgs e)
        {
            if (!(sender is FrameworkElement fe) || !(fe.DataContext is AppItemViewModel vm)) return;
            if (_onRemoveCustomApp == null || !vm.IsCustomApp) return;
            if (MessageDialog.Confirm(
                string.Format(LanguageManager.Current.DialogDeleteAppMessage ?? "Delete custom app '{0}'?", vm.Name),
                LanguageManager.Current.DialogDeleteAppTitle ?? "Delete custom app"))
            {
                _onRemoveCustomApp(vm.App.Process);
                DataReloadRequested?.Invoke(this, EventArgs.Empty);
                MessageDialog.ShowInfo(
                    string.Format(LanguageManager.Current.MsgAppRemoved ?? "Custom app '{0}' removed.", vm.Name),
                    LanguageManager.Current.AppName);
            }
        }

        private void OnResetOverrideClick(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is AppItemViewModel vm)
                vm.ResetOverride();
        }

    }

    /// <summary>One row in the app list. Writes through to the app coordinator on every change.</summary>
    internal sealed class AppItemViewModel : INotifyPropertyChanged
    {
        private readonly Action<string, bool> _onToggled;
        private readonly Action<string, AppOverrideConfig> _onOverrideChanged;
        private bool _isEnabled;
        private string _details, _state, _largeKey, _largeText, _clientId;
        private string _btn1Label, _btn1Url, _btn2Label, _btn2Url;
        private bool? _showTimestamps;
        private bool _isCustomApp;
        private bool _isExpanded;
        private readonly string _searchKey;

        public AppItemViewModel(
            AppConfig app,
            bool isEnabled,
            Dictionary<string, AppOverrideConfig> overrides,
            bool isCustomApp,
            Action<string, bool> onToggled,
            Action<string, AppOverrideConfig> onOverrideChanged)
        {
            App = app;
            _isEnabled = isEnabled;
            _isCustomApp = isCustomApp;
            _onToggled = onToggled;
            _onOverrideChanged = onOverrideChanged;
            _searchKey = (app.AppName + " " + app.Process).ToLowerInvariant();

            DetailsLabel = LanguageManager.Current.LabelDetails ?? "Details";
            StateLabel = LanguageManager.Current.LabelState ?? "State";
            LargeKeyLabel = LanguageManager.Current.LabelLargeKey ?? "Large Image Key";
            LargeTextLabel = LanguageManager.Current.LabelLargeText ?? "Large Image Text";
            ClientIdLabel = LanguageManager.Current.LabelClientId ?? "Application ID (optional)";
            TimestampsLabel = LanguageManager.Current.PresenceShowTimestamps ?? "Show elapsed time";
            ButtonsLabel = LanguageManager.Current.PresenceButtonsSection ?? "Buttons (max 2)";
            HintLabel = LanguageManager.Current.LabelOverrideHint ?? "Empty fields inherit the app database defaults.";
            ResetLabel = LanguageManager.Current.ManageResetOverride ?? "Reset to default";
            DeleteLabel = LanguageManager.Current.ManageDeleteApp ?? "Delete";
            CustomBadge = LanguageManager.Current.ManageCustomBadge ?? "CUSTOM";
            CustomizeHeader = LanguageManager.Current.ManageCustomize ?? "Customize presence";
            ClientIdError = LanguageManager.Current.ErrorInvalidAppId
                ?? "Application ID must be 17-20 digits (numbers only).";

            if (overrides != null && overrides.TryGetValue(app.Process, out var ov))
                ApplyOverride(ov);
        }

        private void ApplyOverride(AppOverrideConfig ov)
        {
            _details = ov?.Details ?? "";
            _state = ov?.State ?? "";
            _largeKey = ov?.LargeKey ?? "";
            _largeText = ov?.LargeText ?? "";
            _clientId = ov?.ClientId ?? "";
            _showTimestamps = ov?.ShowTimestamps;
            _btn1Label = _btn1Url = _btn2Label = _btn2Url = "";
            if (ov?.Buttons != null)
            {
                if (ov.Buttons.Count > 0)
                {
                    _btn1Label = ov.Buttons[0]?.Label ?? "";
                    _btn1Url = ov.Buttons[0]?.Url ?? "";
                }
                if (ov.Buttons.Count > 1)
                {
                    _btn2Label = ov.Buttons[1]?.Label ?? "";
                    _btn2Url = ov.Buttons[1]?.Url ?? "";
                }
            }
        }

        public AppConfig App { get; }
        public string Name => App.AppName;
        public string ProcessText => App.Process + ".exe";
        internal string SearchKey => _searchKey;
        public string DetailsLabel { get; }
        public string StateLabel { get; }
        public string LargeKeyLabel { get; }
        public string LargeTextLabel { get; }
        public string ClientIdLabel { get; }
        public string TimestampsLabel { get; }
        public string ButtonsLabel { get; }
        public string HintLabel { get; }
        public string ResetLabel { get; }
        public string DeleteLabel { get; }
        public string CustomBadge { get; }
        public string CustomizeHeader { get; }
        public string ClientIdError { get; }

        public bool IsCustomApp
        {
            get => _isCustomApp;
            private set { if (_isCustomApp != value) { _isCustomApp = value; OnPropertyChanged(nameof(IsCustomApp)); } }
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    OnPropertyChanged(nameof(IsEnabled));
                    _onToggled?.Invoke(App.Process, value);
                }
            }
        }

        public string Details
        {
            get => _details;
            set { if (_details != value) { _details = value; OnPropertyChanged(nameof(Details)); RaiseOverrideChanged(); } }
        }

        public string State
        {
            get => _state;
            set { if (_state != value) { _state = value; OnPropertyChanged(nameof(State)); RaiseOverrideChanged(); } }
        }

        public string LargeKey
        {
            get => _largeKey;
            set { if (_largeKey != value) { _largeKey = value; OnPropertyChanged(nameof(LargeKey)); RaiseOverrideChanged(); } }
        }

        public string LargeText
        {
            get => _largeText;
            set { if (_largeText != value) { _largeText = value; OnPropertyChanged(nameof(LargeText)); RaiseOverrideChanged(); } }
        }

        public string ClientId
        {
            get => _clientId;
            set
            {
                if (_clientId != value)
                {
                    _clientId = value;
                    OnPropertyChanged(nameof(ClientId));
                    OnPropertyChanged(nameof(HasClientIdError));
                    RaiseOverrideChanged();
                }
            }
        }

        /// <summary>Three-state: null (indeterminate) = inherit the app/config default.</summary>
        public bool? ShowTimestamps
        {
            get => _showTimestamps;
            set { if (_showTimestamps != value) { _showTimestamps = value; OnPropertyChanged(nameof(ShowTimestamps)); RaiseOverrideChanged(); } }
        }

        public string Button1Label
        {
            get => _btn1Label;
            set { if (_btn1Label != value) { _btn1Label = value; OnPropertyChanged(nameof(Button1Label)); RaiseOverrideChanged(); } }
        }

        public string Button1Url
        {
            get => _btn1Url;
            set { if (_btn1Url != value) { _btn1Url = value; OnPropertyChanged(nameof(Button1Url)); RaiseOverrideChanged(); } }
        }

        public string Button2Label
        {
            get => _btn2Label;
            set { if (_btn2Label != value) { _btn2Label = value; OnPropertyChanged(nameof(Button2Label)); RaiseOverrideChanged(); } }
        }

        public string Button2Url
        {
            get => _btn2Url;
            set { if (_btn2Url != value) { _btn2Url = value; OnPropertyChanged(nameof(Button2Url)); RaiseOverrideChanged(); } }
        }

        /// <summary>Live validation hint: a non-empty but malformed Application ID
        /// is NOT propagated to the override (an invalid client id would break the
        /// per-app Discord client switch at detection time).</summary>
        public bool HasClientIdError
            => !string.IsNullOrWhiteSpace(_clientId) && !AppCoordinator.IsValidApplicationId(_clientId.Trim());

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged(nameof(IsExpanded));
                    OnPropertyChanged(nameof(Editor)); // realize/tear down the editor
                }
            }
        }

        /// <summary>Content for the lazy override editor: the row VM while
        /// expanded (so the DataTemplate binds to it), null while collapsed —
        /// collapsed rows never realize the editor fields, keeping re-filters cheap.</summary>
        public object Editor => IsExpanded ? this : null;

        /// <summary>Clears every override field and writes the (now empty)
        /// override through — the coordinator removes the entry, restoring the
        /// app database defaults.</summary>
        internal void ResetOverride()
        {
            _details = _state = _largeKey = _largeText = _clientId = "";
            _btn1Label = _btn1Url = _btn2Label = _btn2Url = "";
            _showTimestamps = null;
            OnPropertyChanged(nameof(Details));
            OnPropertyChanged(nameof(State));
            OnPropertyChanged(nameof(LargeKey));
            OnPropertyChanged(nameof(LargeText));
            OnPropertyChanged(nameof(ClientId));
            OnPropertyChanged(nameof(HasClientIdError));
            OnPropertyChanged(nameof(ShowTimestamps));
            OnPropertyChanged(nameof(Button1Label));
            OnPropertyChanged(nameof(Button1Url));
            OnPropertyChanged(nameof(Button2Label));
            OnPropertyChanged(nameof(Button2Url));
            RaiseOverrideChanged();
        }

        /// <summary>Builds the override payload from the current field values
        /// (null when everything is empty = entry removed). An invalid clientId
        /// is omitted so it can never reach the RPC switch.</summary>
        private AppOverrideConfig BuildOverride()
        {
            string clientId = null;
            if (!string.IsNullOrWhiteSpace(_clientId) && AppCoordinator.IsValidApplicationId(_clientId.Trim()))
                clientId = _clientId.Trim();

            List<AppButtonConfig> buttons = null;
            if (!string.IsNullOrWhiteSpace(_btn1Label) || !string.IsNullOrWhiteSpace(_btn1Url)
                || !string.IsNullOrWhiteSpace(_btn2Label) || !string.IsNullOrWhiteSpace(_btn2Url))
            {
                buttons = new List<AppButtonConfig>(2);
                if (!string.IsNullOrWhiteSpace(_btn1Label) && !string.IsNullOrWhiteSpace(_btn1Url))
                    buttons.Add(new AppButtonConfig { Label = _btn1Label.Trim(), Url = _btn1Url.Trim() });
                if (!string.IsNullOrWhiteSpace(_btn2Label) && !string.IsNullOrWhiteSpace(_btn2Url))
                    buttons.Add(new AppButtonConfig { Label = _btn2Label.Trim(), Url = _btn2Url.Trim() });
                if (buttons.Count == 0) buttons = null;
            }

            bool any = !string.IsNullOrWhiteSpace(_details)
                || !string.IsNullOrWhiteSpace(_state)
                || !string.IsNullOrWhiteSpace(_largeKey)
                || !string.IsNullOrWhiteSpace(_largeText)
                || clientId != null
                || _showTimestamps != null
                || buttons != null;
            if (!any) return null;

            return new AppOverrideConfig
            {
                Details = NullIfEmpty(_details),
                State = NullIfEmpty(_state),
                LargeKey = NullIfEmpty(_largeKey),
                LargeText = NullIfEmpty(_largeText),
                ClientId = clientId,
                ShowTimestamps = _showTimestamps,
                Buttons = buttons
            };
        }

        private static string NullIfEmpty(string s)
            => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        private void RaiseOverrideChanged()
            => _onOverrideChanged?.Invoke(App.Process, BuildOverride());

        /// <summary>Syncs enable/override/custom state from the coordinator in place
        /// (same app list). Raises change notifications but does NOT fire the
        /// write-through callbacks: the coordinator already holds this state.</summary>
        internal void RefreshState(HashSet<string> disabledApps, Dictionary<string, AppOverrideConfig> overrides,
            HashSet<string> customProcesses)
        {
            bool enabled = disabledApps == null || !disabledApps.Contains(App.Process);
            if (_isEnabled != enabled) { _isEnabled = enabled; OnPropertyChanged(nameof(IsEnabled)); }
            bool custom = customProcesses != null && customProcesses.Contains(App.Process);
            if (_isCustomApp != custom) { _isCustomApp = custom; OnPropertyChanged(nameof(IsCustomApp)); }

            AppOverrideConfig ov = null;
            overrides?.TryGetValue(App.Process, out ov);
            string details = ov?.Details ?? "", state = ov?.State ?? "",
                   largeKey = ov?.LargeKey ?? "", largeText = ov?.LargeText ?? "", clientId = ov?.ClientId ?? "";
            bool? timestamps = ov?.ShowTimestamps;
            string b1l = "", b1u = "", b2l = "", b2u = "";
            if (ov?.Buttons != null)
            {
                if (ov.Buttons.Count > 0) { b1l = ov.Buttons[0]?.Label ?? ""; b1u = ov.Buttons[0]?.Url ?? ""; }
                if (ov.Buttons.Count > 1) { b2l = ov.Buttons[1]?.Label ?? ""; b2u = ov.Buttons[1]?.Url ?? ""; }
            }
            if (_details != details) { _details = details; OnPropertyChanged(nameof(Details)); }
            if (_state != state) { _state = state; OnPropertyChanged(nameof(State)); }
            if (_largeKey != largeKey) { _largeKey = largeKey; OnPropertyChanged(nameof(LargeKey)); }
            if (_largeText != largeText) { _largeText = largeText; OnPropertyChanged(nameof(LargeText)); }
            if (_clientId != clientId) { _clientId = clientId; OnPropertyChanged(nameof(ClientId)); OnPropertyChanged(nameof(HasClientIdError)); }
            if (_showTimestamps != timestamps) { _showTimestamps = timestamps; OnPropertyChanged(nameof(ShowTimestamps)); }
            if (_btn1Label != b1l) { _btn1Label = b1l; OnPropertyChanged(nameof(Button1Label)); }
            if (_btn1Url != b1u) { _btn1Url = b1u; OnPropertyChanged(nameof(Button1Url)); }
            if (_btn2Label != b2l) { _btn2Label = b2l; OnPropertyChanged(nameof(Button2Label)); }
            if (_btn2Url != b2u) { _btn2Url = b2u; OnPropertyChanged(nameof(Button2Url)); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string name)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
