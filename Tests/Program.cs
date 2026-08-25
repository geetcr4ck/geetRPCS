/**
 * geetRPCS - Tests
 * Minimal dependency-free test runner (no test framework needed).
 * Run with: dotnet run --project Tests
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
using System.Text.Json;
using System.Threading;
using DiscordRPC;
using geetRPCS.Models;
using geetRPCS.Services;
using geetRPCS.UI;
using geetRPCS.UI.Modern;
using geetRPCS.Utils;

namespace Tests
{
    internal static class Program
    {
        private static int _failures;

        private static void Check(string name, bool condition)
        {
            Console.WriteLine((condition ? "PASS  " : "FAIL  ") + name);
            if (!condition) _failures++;
        }

        /// <summary>Most saturated blue pixel within the image-margin column
        /// (x &lt; 34) of a rendered menu — the core of an accent-colored glyph. The
        /// menu border is neutral gray (b-r == 0) so it is never selected.</summary>
        private static System.Drawing.Color MostBluePixelInColumn(System.Drawing.Bitmap bmp)
        {
            var best = System.Drawing.Color.Transparent;
            int bestBr = -1;
            for (int y = 0; y < bmp.Height; y++)
                for (int x = 0; x < 34; x++)
                {
                    var p = bmp.GetPixel(x, y);
                    int br = p.B - p.R;
                    if (p.A > 25 && br > bestBr) { best = p; bestBr = br; }
                }
            return best;
        }

        /// <summary>Returns the most opaque pixel of a rendered glyph bitmap — the
        /// center of a stroke, where the brush color is at (near) full coverage.</summary>
        private static System.Drawing.Color MaxAlphaPixel(System.Drawing.Bitmap bmp)
        {
            var best = System.Drawing.Color.Transparent;
            for (int x = 0; x < bmp.Width; x++)
                for (int y = 0; y < bmp.Height; y++)
                {
                    var p = bmp.GetPixel(x, y);
                    if (p.A > best.A) best = p;
                }
            return best;
        }

        /// <summary>Text shown by a GuideWindow nav row (the item template's
        /// first TextBlock), or null when the container is not realized.</summary>
        private static string GuideNavItemText(GuideWindow win, int index)
        {
            if (win.NavList.ItemContainerGenerator.Status !=
                System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
                return null;
            if (!(win.NavList.ItemContainerGenerator.ContainerFromIndex(index)
                    is System.Windows.Controls.ListBoxItem item))
                return null;
            return FindDescendantText(item)?.Text;
        }

        private static System.Windows.Controls.TextBlock FindDescendantText(System.Windows.DependencyObject root)
        {
            int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
                if (child is System.Windows.Controls.TextBlock tb) return tb;
                var deeper = FindDescendantText(child);
                if (deeper != null) return deeper;
            }
            return null;
        }

        [STAThread]
        private static int Main()
        {
            return Run();
        }

        private static int Run()
        {
            Console.WriteLine("IsValidApplicationId tests:");
            Console.WriteLine("-- valid: 17-20 digits --");
            Check("17 digits accepted", AppCoordinator.IsValidApplicationId("12345678901234567"));
            Check("18 digits accepted", AppCoordinator.IsValidApplicationId("123456789012345678"));
            Check("19 digits accepted (default app id)", AppCoordinator.IsValidApplicationId("1433700335863726183"));
            Check("20 digits accepted", AppCoordinator.IsValidApplicationId("12345678901234567890"));
            Console.WriteLine("-- boundaries: 16 and 21 digits rejected --");
            Check("16 digits rejected", !AppCoordinator.IsValidApplicationId("1234567890123456"));
            Check("21 digits rejected", !AppCoordinator.IsValidApplicationId("123456789012345678901"));
            Console.WriteLine("-- non-digit characters rejected --");
            Check("trailing letter rejected", !AppCoordinator.IsValidApplicationId("12345678901234567a"));
            Check("embedded letter rejected", !AppCoordinator.IsValidApplicationId("1234567890123456a1"));
            Check("hyphen rejected", !AppCoordinator.IsValidApplicationId("12345678-901234567"));
            Check("decimal point rejected", !AppCoordinator.IsValidApplicationId("1234567890123456.7"));
            Console.WriteLine("-- empty / whitespace / null rejected --");
            Check("empty rejected", !AppCoordinator.IsValidApplicationId(""));
            Check("whitespace rejected", !AppCoordinator.IsValidApplicationId("     "));
            Check("null rejected", !AppCoordinator.IsValidApplicationId(null));
            Console.WriteLine("-- trimming --");
            Check("whitespace-padded valid id accepted (trimmed)", AppCoordinator.IsValidApplicationId("  12345678901234567  "));
            Check("whitespace-padded short id still rejected", !AppCoordinator.IsValidApplicationId(" 1234567890123456 "));

            Console.WriteLine("Telemetry default:");
            // First run creates new AppSettings() with no saved file; the property
            // initializer must keep telemetry ON by default (PRIVACY.md: "default: On").
            Check("telemetry ON by default (new AppSettings)", new AppSettings().TelemetryEnabled);
            Check("theme mode defaults to System (new AppSettings)", new AppSettings().ThemeMode == "System");

            Console.WriteLine("Private browsing detection:");
            Check("Chrome Incognito detected", PrivateBrowsingDetector.IsPrivateWindow("chrome", "Secret tab - Incognito - Google Chrome"));
            Check("Chrome accessible-only Incognito detected", PrivateBrowsingDetector.IsPrivateWindow("chrome", "Bank - Google Chrome", "Bank - Google Chrome (Incognito)"));
            Check("Chrome Indonesian private tab detected", PrivateBrowsingDetector.IsPrivateWindow("chrome", "Tab Samaran Baru - Google Chrome"));
            Check("Edge InPrivate detected", PrivateBrowsingDetector.IsPrivateWindow("msedge", "Secret tab - InPrivate - Microsoft Edge"));
            Check("Firefox Private Browsing detected", PrivateBrowsingDetector.IsPrivateWindow("firefox", "Private Browsing — Mozilla Firefox"));
            Check("Firefox Indonesian private browsing detected", PrivateBrowsingDetector.IsPrivateWindow("firefox", "Penjelajahan Pribadi — Mozilla Firefox"));
            Check("Brave private window detected", PrivateBrowsingDetector.IsPrivateWindow("brave", "New Private Window - Brave"));
            Check("Brave accessible Private suffix detected", PrivateBrowsingDetector.IsPrivateWindow("brave", "Bank - Brave", "Bank - Brave (Private)"));
            Check("Brave Indonesian accessible Pribadi suffix detected", PrivateBrowsingDetector.IsPrivateWindow("brave", "Bank - Brave", "Bank - Brave (Pribadi)"));
            Check("Brave Tor window detected", PrivateBrowsingDetector.IsPrivateWindow("brave", "Private with Tor - Brave"));
            Check("Zen private window detected", PrivateBrowsingDetector.IsPrivateWindow("zen", "Private Browsing — Zen Browser"));
            Check("detection is case-insensitive", PrivateBrowsingDetector.IsPrivateWindow("CHROME", "new INCOGNITO tab - google chrome"));
            Check("normal browser title is not private", !PrivateBrowsingDetector.IsPrivateWindow("chrome", "OpenAI - Google Chrome"));
            Check("normal accessible browser name is not private", !PrivateBrowsingDetector.IsPrivateWindow("chrome", "OpenAI - Google Chrome", "OpenAI - Google Chrome"));
            Check("generic private word is not enough", !PrivateBrowsingDetector.IsPrivateWindow("brave", "Private notes - Brave"));
            Check("Brave normal page containing Private is not hidden", !PrivateBrowsingDetector.IsPrivateWindow("brave", "My Private notes - Brave", "My Private notes - Brave"));
            Check("Brave page-title suffix is not mistaken for annotation", !PrivateBrowsingDetector.IsPrivateWindow("brave", "Article (Private) - Brave", "Article (Private) - Brave"));
            Check("bare Brave Private annotation is rejected", !PrivateBrowsingDetector.IsPrivateWindow("brave", "Private", "Private (Private)"));
            Check("Brave Guest window is not hidden", !PrivateBrowsingDetector.IsPrivateWindow("brave", "New Tab - Brave", "New Tab - Brave (Guest)"));
            Check("indicator in a non-browser app is ignored", !PrivateBrowsingDetector.IsPrivateWindow("Code", "Incognito implementation - Visual Studio Code"));
            Check("missing process is not private", !PrivateBrowsingDetector.IsPrivateWindow(null, "Incognito"));
            Check("missing title is not private", !PrivateBrowsingDetector.IsPrivateWindow("chrome", null));
            Check("German Chrome Inkognito annotation detected", PrivateBrowsingDetector.IsPrivateWindow("chrome", "Bank - Google Chrome", "Bank - Google Chrome (Inkognito)"));
            Check("French Chrome Navigation privée detected", PrivateBrowsingDetector.IsPrivateWindow("chrome", "Banque - Navigation privée - Google Chrome"));
            Check("Japanese Chrome secret annotation detected", PrivateBrowsingDetector.IsPrivateWindow("chrome", "銀行 - Google Chrome", "銀行 - Google Chrome (シークレット)"));
            Check("Korean Chrome secret annotation detected", PrivateBrowsingDetector.IsPrivateWindow("chrome", "은행 - Google Chrome", "은행 - Google Chrome (시크릿)"));
            Check("Simplified Chinese Chrome annotation detected", PrivateBrowsingDetector.IsPrivateWindow("chrome", "银行 - Google Chrome", "银行 - Google Chrome (隐身)"));
            Check("Traditional Chinese Chrome annotation detected", PrivateBrowsingDetector.IsPrivateWindow("chrome", "銀行 - Google Chrome", "銀行 - Google Chrome (無痕)"));
            Check("Vietnamese Chrome an danh detected", PrivateBrowsingDetector.IsPrivateWindow("chrome", "Ngân hàng - ẩn danh - Google Chrome"));
            Check("Russian Firefox private mode detected", PrivateBrowsingDetector.IsPrivateWindow("firefox", "Банк — Приватный режим — Mozilla Firefox"));
            Check("Spanish Firefox navegación privada detected", PrivateBrowsingDetector.IsPrivateWindow("firefox", "Banco - Navegación privada"));
            Check("German Firefox privates Fenster detected", PrivateBrowsingDetector.IsPrivateWindow("firefox", "Bank - Privates Fenster - Mozilla Firefox"));
            Check("Brave German Privat annotation detected", PrivateBrowsingDetector.IsPrivateWindow("brave", "Bank - Brave", "Bank - Brave (Privat)"));
            Check("lone French word privé is not enough", !PrivateBrowsingDetector.IsPrivateWindow("chrome", "Guide des hôtels privés - Google Chrome"));
            Check("generic anonymous word is not enough", !PrivateBrowsingDetector.IsPrivateWindow("chrome", "Anonymous browsing tips - Google Chrome"));
            Check("Firefox ignores the Incognito family", !PrivateBrowsingDetector.IsPrivateWindow("firefox", "Incognito article - Mozilla Firefox"));
            Check("unknown-language Thai Edge annotation detected structurally", PrivateBrowsingDetector.IsPrivateWindow("msedge", "ธนาคาร - Microsoft Edge", "ธนาคาร - Microsoft Edge (ส่วนตัว)"));
            Check("unknown-language Turkish Chrome annotation detected structurally", PrivateBrowsingDetector.IsPrivateWindow("chrome", "Bank - Google Chrome", "Bank - Google Chrome (Gizli pencere)"));
            Check("structural rule needs a genuine browser title", !PrivateBrowsingDetector.IsPrivateWindow("chrome", "Some page (Mystery)", "Some page (Mystery) (Xyz)"));
            Check("Edge Russian Guest annotation is not hidden", !PrivateBrowsingDetector.IsPrivateWindow("msedge", "New Tab - Microsoft Edge", "New Tab - Microsoft Edge (Гость)"));
            Check("Chrome Japanese Guest annotation is not hidden", !PrivateBrowsingDetector.IsPrivateWindow("chrome", "New Tab - Google Chrome", "New Tab - Google Chrome (ゲスト)"));
            Check("Turkish Firefox Gizli Gezinti detected", PrivateBrowsingDetector.IsPrivateWindow("firefox", "Banka - Gizli Gezinti"));
            Check("Greek Firefox private browsing detected", PrivateBrowsingDetector.IsPrivateWindow("firefox", "Τράπεζα - Ιδιωτική περιήγηση"));
            Check("Arabic Firefox private browsing detected", PrivateBrowsingDetector.IsPrivateWindow("firefox", "التصفح الخاص"));
            Check("Vietnamese Firefox private browsing detected", PrivateBrowsingDetector.IsPrivateWindow("firefox", "Ngân hàng - Duyệt web riêng tư"));
            Check("Czech Firefox private browsing detected", PrivateBrowsingDetector.IsPrivateWindow("firefox", "Banka - Soukromé prohlížení"));
            var maskBuilder = new PresenceBuilder(new Config()) { PrivateMode = true };
            Check("hidden window title is redacted with asterisks", maskBuilder.ReplacePlaceholders("{window_title}", "chrome", IntPtr.Zero) == "**********");

            Console.WriteLine("Effective app overrides (AppConfigManager.GetEffectiveApp):");
            var baseApp = new AppConfig
            {
                Process = "notepad", AppName = "Notepad",
                LargeKey = "orig-key", LargeText = "Original",
                ClientId = "111111111111111111",
                ShowTimestamps = false,
                Buttons = new List<AppButtonConfig> { new AppButtonConfig { Label = "A", Url = "https://a.example/" } }
            };
            var effFull = AppConfigManager.GetEffectiveApp(baseApp, new AppOverrideConfig
            {
                LargeKey = "new-key", LargeText = "New",
                ClientId = "222222222222222222",
                ShowTimestamps = true,
                Buttons = new List<AppButtonConfig> { new AppButtonConfig { Label = "B", Url = "https://b.example/" } }
            });
            Check("override largeKey wins", effFull.LargeKey == "new-key");
            Check("override largeText wins", effFull.LargeText == "New");
            Check("override clientId wins", effFull.ClientId == "222222222222222222");
            Check("override showTimestamps wins", effFull.ShowTimestamps == true);
            Check("override buttons win", effFull.Buttons != null && effFull.Buttons.Count == 1 && effFull.Buttons[0].Label == "B");
            Check("untouched base fields preserved", effFull.AppName == "Notepad" && effFull.Process == "notepad");
            var effEmpty = AppConfigManager.GetEffectiveApp(baseApp, new AppOverrideConfig());
            Check("empty override inherits everything", effEmpty.LargeKey == "orig-key" && effEmpty.ClientId == "111111111111111111"
                && effEmpty.ShowTimestamps == false && effEmpty.Buttons[0].Label == "A");
            Check("null override returns the same instance", AppConfigManager.GetEffectiveApp(baseApp, null) == baseApp);
            Check("merge result is a clone (base untouched)", effFull != baseApp && baseApp.LargeKey == "orig-key");

            Console.WriteLine("Custom apps merge (AppConfigManager.Reload core):");
            var builtIns = new List<AppConfig>
            {
                new AppConfig { Process = "fl64", AppName = "FL Studio" },
                new AppConfig { Process = "code", AppName = "VS Code", ProcessMatchMode = "Contains" }
            };
            var customApps = new List<AppConfig>
            {
                new AppConfig { Process = "myapp", AppName = "My App" },
                new AppConfig { Process = "FL64", AppName = "My FL Studio" } // same process, different case
            };
            AppConfigManager.Reload(builtIns, customApps);
            Check("custom app appended", AppConfigManager.Apps.Any(a => a.Process == "myapp"));
            var flEntries = AppConfigManager.Apps.Where(a => a.Process.Equals("FL64", StringComparison.OrdinalIgnoreCase)).ToList();
            Check("custom entry replaces built-in (case-insensitive)", flEntries.Count == 1 && flEntries[0].AppName == "My FL Studio");
            Check("custom app in exact match set", AppConfigManager.ExactProcessNames.Contains("myapp"));
            Check("advanced process match preserved", AppConfigManager.AdvancedProcessApps.Any(a => a.Process == "code"));
            AppConfigManager.Reload(builtIns, null);
            Check("no custom apps leaves built-ins untouched",
                AppConfigManager.Apps.All(a => !a.Process.Equals("myapp", StringComparison.OrdinalIgnoreCase)));
            AppConfigManager.Reload(); // restore real state for anything later

            Console.WriteLine("Config JSON round-trip (AppCoordinator.SerializeConfig):");
            var roundCfg = AppCoordinator.GetDefaultConfig();
            roundCfg.Discord.Details = "Idle text";
            roundCfg.Discord.ActiveDetails = "Editing {app_name}";
            roundCfg.Discord.ShowTimestamps = false;
            roundCfg.Discord.Buttons = new[]
            {
                new ButtonConfig { Label = "Site", Url = "https://example.com/" },
                new ButtonConfig { Label = "Docs", Url = "https://docs.example.com/" }
            };
            var roundParsed = System.Text.Json.JsonSerializer.Deserialize(
                AppCoordinator.SerializeConfig(roundCfg), JsonContext.Default.Config);
            Check("round-trip keeps applicationId", roundParsed.Discord.ApplicationId == roundCfg.Discord.ApplicationId);
            Check("round-trip keeps idle texts", roundParsed.Discord.Details == "Idle text" && roundParsed.Discord.State == roundCfg.Discord.State);
            Check("round-trip keeps active templates", roundParsed.Discord.ActiveDetails == "Editing {app_name}");
            Check("round-trip keeps timestamps flag", roundParsed.Discord.ShowTimestamps == false);
            Check("round-trip keeps both buttons", roundParsed.Discord.Buttons != null && roundParsed.Discord.Buttons.Length == 2
                && roundParsed.Discord.Buttons[1].Url == "https://docs.example.com/");

            Console.WriteLine("Directory checksum (UpdaterHelper --checksum):");
            // The combined directory hash must be deterministic (same content => same
            // hash, independent of timestamps) and sensitive to content/name changes.
            string tmpDir = Path.Combine(Path.GetTempPath(), "geet_checksum_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tmpDir);
            try
            {
                string sub = Path.Combine(tmpDir, "sub");
                Directory.CreateDirectory(sub);
                File.WriteAllText(Path.Combine(sub, "a.txt"), "hello");
                File.WriteAllText(Path.Combine(tmpDir, "b.txt"), "world");

                string h1 = geetRPCS.Utils.DirectoryChecksum.Compute(tmpDir);
                string h2 = geetRPCS.Utils.DirectoryChecksum.Compute(tmpDir);
                Check("hash is deterministic (same dir computed twice)", h1 == h2);
                Check("hash is 64 uppercase hex chars", h1.Length == 64 && h1 == h1.ToUpperInvariant());

                File.WriteAllText(Path.Combine(sub, "a.txt"), "hello world");
                string h3 = geetRPCS.Utils.DirectoryChecksum.Compute(tmpDir);
                Check("hash changes when content changes", h3 != h1);

                File.WriteAllText(Path.Combine(sub, "a.txt"), "hello");
                string h4 = geetRPCS.Utils.DirectoryChecksum.Compute(tmpDir);
                Check("hash is independent of file timestamps", h4 == h1);

                File.Move(Path.Combine(sub, "a.txt"), Path.Combine(tmpDir, "a-moved.txt"));
                string h5 = geetRPCS.Utils.DirectoryChecksum.Compute(tmpDir);
                Check("hash changes when a file is renamed/moved", h5 != h1);
            }
            finally
            {
                Directory.Delete(tmpDir, true);
            }

            Console.WriteLine("apps.json validation:");
            // The repo's app database must load and every real app entry (one with a
            // process name; comment/db_version headers are skipped) must carry a valid
            // Discord Application ID.
            string appsPath = FindAppsJson();
            Check($"apps.json found ({appsPath})", appsPath != null);
            if (appsPath != null)
            {
                var apps = AppConfig.Load(appsPath);
                var realApps = apps?.Where(a => !string.IsNullOrEmpty(a.Process)).ToList() ?? new System.Collections.Generic.List<AppConfig>();
                Check($"apps.json loads ({realApps.Count} app entries)", realApps.Count > 0);

                int invalid = 0;
                foreach (var app in realApps)
                {
                    if (!AppCoordinator.IsValidApplicationId(app.ClientId))
                    {
                        invalid++;
                        Console.WriteLine($"      invalid clientId '{app.ClientId}' for '{app.AppName}' (process '{app.Process}')");
                    }
                }
                Check("all clientIds are valid (17-20 digits)", invalid == 0);

                var dupes = realApps.GroupBy(a => a.Process, StringComparer.OrdinalIgnoreCase)
                                    .Where(g => g.Count() > 1)
                                    .Select(g => g.Key)
                                    .ToList();
                foreach (var d in dupes)
                    Console.WriteLine($"      duplicate process '{d}'");
                Check("process names are unique", dupes.Count == 0);

                var noKey = realApps.Where(a => string.IsNullOrEmpty(a.LargeKey)).Select(a => a.Process).ToList();
                foreach (var p in noKey)
                    Console.WriteLine($"      missing largeKey for '{p}'");
                Check("all entries have a non-empty largeKey", noKey.Count == 0);

                int badUrls = 0;
                foreach (var app in realApps)
                {
                    if (app.Buttons == null) continue;
                    foreach (var b in app.Buttons)
                    {
                        if (b == null || string.IsNullOrWhiteSpace(b.Url) ||
                            !Uri.TryCreate(b.Url, UriKind.Absolute, out var uri) ||
                            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                        {
                            badUrls++;
                            Console.WriteLine($"      invalid button URL '{b?.Url}' for '{app.AppName}' (process '{app.Process}')");
                        }
                    }
                }
                Check("all button URLs are non-empty http/https", badUrls == 0);

                int badLabels = 0;
                foreach (var app in realApps)
                {
                    if (app.Buttons == null) continue;
                    foreach (var b in app.Buttons)
                    {
                        if (b == null || string.IsNullOrWhiteSpace(b.Label) || b.Label.Length > 32)
                        {
                            badLabels++;
                            Console.WriteLine($"      invalid button label '{b?.Label}' ({b?.Label?.Length} chars) for '{app.AppName}' (process '{app.Process}')");
                        }
                    }
                }
                Check("all button labels are non-empty and <= 32 chars", badLabels == 0);

                int tooManyButtons = realApps.Count(a => a.Buttons != null && a.Buttons.Count > 2);
                foreach (var app in realApps.Where(a => a.Buttons != null && a.Buttons.Count > 2))
                    Console.WriteLine($"      {app.Buttons.Count} buttons for '{app.AppName}' (process '{app.Process}')");
                Check("no entry has more than 2 buttons", tooManyButtons == 0);

                var noSmall = realApps.Where(a => string.IsNullOrEmpty(a.SmallKey)).Select(a => a.Process).ToList();
                foreach (var p in noSmall)
                    Console.WriteLine($"      missing smallKey for '{p}'");
                Check("all entries have a non-empty smallKey", noSmall.Count == 0);
            }

            Console.WriteLine("Language file parity:");
            // Every key defined in en.json must exist in every other language file
            // AND in template.json, so untranslated keys surface here instead of
            // silently falling back to English at runtime.
            string langsDir = FindLanguagesDir();
            Check($"Languages folder found ({langsDir})", langsDir != null);
            if (langsDir != null)
            {
                string enPath = Path.Combine(langsDir, "en.json");
                Check("en.json exists", File.Exists(enPath));
                if (File.Exists(enPath))
                {
                    var enKeys = JsonDocument.Parse(File.ReadAllText(enPath))
                                             .RootElement.EnumerateObject()
                                             .Select(p => p.Name)
                                             .ToHashSet();
                    int filesWithGaps = 0;
                    foreach (var file in Directory.EnumerateFiles(langsDir, "*.json")
                                                  .OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                    {
                        string code = Path.GetFileNameWithoutExtension(file);
                        if (code.Equals("en", StringComparison.OrdinalIgnoreCase)) continue;
                        var keys = JsonDocument.Parse(File.ReadAllText(file))
                                               .RootElement.EnumerateObject()
                                               .Select(p => p.Name)
                                               .ToHashSet();
                        var missing = enKeys.Where(k => !keys.Contains(k))
                                            .OrderBy(k => k, StringComparer.Ordinal)
                                            .ToList();
                        if (missing.Count > 0)
                        {
                            filesWithGaps++;
                            Console.WriteLine($"      {code}.json: {missing.Count} missing key(s): {string.Join(", ", missing)}");
                        }
                    }
                    Check("every key in en.json exists in every language file and template.json", filesWithGaps == 0);
                }
            }

            Console.WriteLine("WPF pre-warm smoke test:");
            // PreWarm() moves the first-window one-time cost (templates, layout,
            // font/composition init) from the first tray-menu open to startup. It
            // must run without exception and leave no window behind.
            try
            {
                WpfHost.EnsureInitialized();
                WpfHost.PreWarm();
                for (int i = 0; i < 5; i++)
                {
                    System.Windows.Forms.Application.DoEvents();
                    Thread.Sleep(10);
                }
                Check("pre-warm runs without exception", true);
                Check("pre-warm leaves no window loaded",
                    System.Windows.Application.Current.Windows.Count == 0);
                // Warmed windows must still open normally afterwards.
                var warmWin = new ManageAppsWindow(
                    new List<AppConfig>(), new HashSet<string>(),
                    new Dictionary<string, AppOverrideConfig>(),
                    (p, e) => { }, (p, d, s) => { });
                warmWin.Show();
                for (int i = 0; i < 20; i++)
                {
                    System.Windows.Forms.Application.DoEvents();
                    Thread.Sleep(10);
                }
                Check("window opens normally after pre-warm", warmWin.IsLoaded);
                warmWin.Close();
                for (int i = 0; i < 5; i++)
                {
                    System.Windows.Forms.Application.DoEvents();
                    Thread.Sleep(10);
                }
                Check("warmed window closes cleanly", !warmWin.IsLoaded);
            }
            catch (Exception ex)
            {
                Check("pre-warm smoke test ran without exception", false);
                Console.WriteLine("      " + ex);
            }

            Console.WriteLine("Memory trim after heavy window close:");
            // Program.cs trims the working set (GC + EmptyWorkingSet) after heavy
            // windows close. Verify the process working set actually drops when a
            // 100-app ManageAppsWindow is opened, closed and trimmed.
            try
            {
                WpfHost.EnsureInitialized();
                long WorkingSet() => System.Diagnostics.Process.GetCurrentProcess().WorkingSet64;
                var heavyApps = new List<AppConfig>();
                for (int i = 0; i < 100; i++)
                    heavyApps.Add(new AppConfig { Process = "p" + i, AppName = "App " + i, ClientId = "12345678901234567" });

                long before = WorkingSet();
                var memWin = new ManageAppsWindow(heavyApps, new HashSet<string>(),
                    new Dictionary<string, AppOverrideConfig>(), (p, e) => { }, (p, d, s) => { });
                memWin.Show();
                for (int i = 0; i < 30; i++) { System.Windows.Forms.Application.DoEvents(); Thread.Sleep(10); }
                long afterOpen = WorkingSet();
                // Pre-create idle state: Program.cs drops the rows (empty refresh)
                // on the hidden window so the idle pre-created window holds little.
                // Working set can't show the release (only EmptyWorkingSet pages it
                // out) — assert on the GC heap, which actually shrinks when the row
                // view models become garbage.
                long heapOpen = GC.GetTotalMemory(true);
                memWin.RefreshData(new List<AppConfig>(), new HashSet<string>(),
                    new Dictionary<string, AppOverrideConfig>());
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                long heapEmpty = GC.GetTotalMemory(true);
                memWin.Close();
                for (int i = 0; i < 30; i++) { System.Windows.Forms.Application.DoEvents(); Thread.Sleep(10); }
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                geetRPCS.Utils.MemoryHelper.TrimMemory();
                long afterTrim = WorkingSet();
                Console.WriteLine("      [working set] before={0}KB open={1}KB afterTrim={2}KB",
                    before / 1024, afterOpen / 1024, afterTrim / 1024);
                Console.WriteLine("      [heap] open={0}KB afterEmptyRefresh={1}KB",
                    heapOpen / 1024, heapEmpty / 1024);
                Check("heavy window grows the working set", afterOpen > before + 256 * 1024);
                Check("dropping rows releases the window heap", heapEmpty < heapOpen);
                Check("trim releases the window memory", afterTrim < afterOpen);
            }
            catch (Exception ex)
            {
                Check("memory trim test ran without exception", false);
                Console.WriteLine("      " + ex);
            }

            Console.WriteLine("WPF Modern UI (ModernWpfUI 1.0.0-preview.7) interop:");
            // Prove the WPF ManageAppsWindow works when the thread is pumped by the
            // WinForms message loop (which is how the tray app actually runs).
            try
            {
                var toggles = new List<(string Proc, bool Enabled)>();
                var overrides = new List<(string Proc, string Details, string State)>();

                var wpfApps = new List<AppConfig>
                {
                    new AppConfig { Process = "notepad", AppName = "Notepad", ClientId = "1433700335863726183" },
                    new AppConfig { Process = "code", AppName = "Visual Studio Code", ClientId = "1433700335863726183" },
                };
                var wpfDisabled = new HashSet<string> { "code" };
                var wpfOverrides = new Dictionary<string, AppOverrideConfig>
                {
                    ["code"] = new AppOverrideConfig { Details = "override-details", State = "override-state" }
                };

                WpfHost.EnsureInitialized();
                var win = new ManageAppsWindow(wpfApps, wpfDisabled, wpfOverrides,
                    (proc, enabled) => toggles.Add((proc, enabled)),
                    (proc, d, s) => overrides.Add((proc, d, s)));

                Check("window created with 2 items", win.Items.Count == 2);
                var codeVm = win.Items.First(i => i.App.Process == "code");
                Check("disabled state loaded from settings", codeVm.IsEnabled == false);
                Check("override details loaded", codeVm.Details == "override-details");
                Check("override state loaded", codeVm.State == "override-state");

                win.Show();
                for (int i = 0; i < 50; i++)
                {
                    System.Windows.Forms.Application.DoEvents();
                    Thread.Sleep(10);
                }

                Check("window IsLoaded under WinForms pump", win.IsLoaded);
                Check("window laid out (ActualWidth > 0)", win.ActualWidth > 0);
                // No fade by design (removed after the white flash was fixed by
                // other means): the window must be fully opaque from the start.
                Check("window fully opaque immediately (no fade)", win.Opacity == 1.0);

                win.Items.First(i => i.App.Process == "notepad").IsEnabled = false;
                Check("toggle callback fired with (proc, false)",
                    toggles.Count == 1 && toggles[0] == ("notepad", false));

                codeVm.Details = "working on X";
                Check("override callback fired with new details",
                    overrides.Count == 1 && overrides[0].Proc == "code" && overrides[0].Details == "working on X");

                win.Close();
            }
            catch (Exception ex)
            {
                Check("WPF smoke test ran without exception", false);
                Console.WriteLine("      " + ex);
            }

            Console.WriteLine("ManageAppsWindow search box diagnostics:");
            // The search TextBox must accept keyboard focus and hit-testing when
            // shown under the WinForms pump (user report: could not type).
            try
            {
                WpfHost.EnsureInitialized();
                var searchWin = new ManageAppsWindow(
                    new List<AppConfig> { new AppConfig { Process = "notepad", AppName = "Notepad", ClientId = "12345678901234567" } },
                    new HashSet<string>(),
                    new Dictionary<string, AppOverrideConfig>(),
                    (proc, enabled) => { },
                    (proc, d, s) => { });
                searchWin.Show();
                for (int i = 0; i < 50; i++)
                {
                    System.Windows.Forms.Application.DoEvents();
                    Thread.Sleep(10);
                }
                Check("search box enabled", searchWin.SearchBox.IsEnabled);
                Check("search box not readonly", !searchWin.SearchBox.IsReadOnly);
                Check("search box focusable", searchWin.SearchBox.Focusable);
                // The deferred-activation retry must focus the search box by itself
                // (no manual Focus() call) — the real-app tray menu steals focus
                // otherwise and the box cannot be typed into.
                Check("search box auto-focused after show", searchWin.SearchBox.IsKeyboardFocused);
                bool focusOk = searchWin.SearchBox.Focus();
                for (int i = 0; i < 20; i++)
                {
                    System.Windows.Forms.Application.DoEvents();
                    Thread.Sleep(10);
                }
                Check("search box accepts keyboard focus", focusOk && searchWin.SearchBox.IsKeyboardFocused);
                searchWin.SearchBox.Text = "not";
                for (int i = 0; i < 20; i++)
                {
                    System.Windows.Forms.Application.DoEvents();
                    Thread.Sleep(10);
                }
                Check("typing text into search box sticks", searchWin.SearchBox.Text == "not");
                Check("placeholder hidden while typing", searchWin.SearchPlaceholder.Visibility == System.Windows.Visibility.Collapsed);
                searchWin.Close();
            }
            catch (Exception ex)
            {
                Check("search box diagnostic ran without exception", false);
                Console.WriteLine("      " + ex);
            }

            Console.WriteLine("ManageAppsWindow tray-menu simulation (real ContextMenuStrip path):");
            // Reproduce how the app actually opens the window: a tray
            // ContextMenuStrip item whose click opens the modal window IMMEDIATELY
            // (no BeginInvoke deferral — same as the other tray dialogs). An auto-close
            // DispatcherTimer (ticking inside the modal loop) captures the state
            // before closing the dialog.
            try
            {
                WpfHost.EnsureInitialized();
                var trayMenu = new System.Windows.Forms.ContextMenuStrip();
                ManageAppsWindow trayWin = null;
                bool trayLoaded = false, trayFocused = false;
                var trayItem = new System.Windows.Forms.ToolStripMenuItem("Manage Apps");
                trayItem.Click += (_, __) =>
                {
                    trayWin = new ManageAppsWindow(
                        new System.Collections.Generic.List<AppConfig>
                        {
                            new AppConfig { Process = "notepad", AppName = "Notepad", ClientId = "12345678901234567" }
                        },
                        new System.Collections.Generic.HashSet<string>(),
                        new System.Collections.Generic.Dictionary<string, AppOverrideConfig>(),
                        (proc, enabled) => { },
                        (proc, d, s) => { });
                    var closeTimer = new System.Windows.Threading.DispatcherTimer
                    { Interval = TimeSpan.FromMilliseconds(600) };
                    closeTimer.Tick += (s2, e2) =>
                    {
                        closeTimer.Stop();
                        trayLoaded = trayWin.IsLoaded;
                        trayFocused = trayWin.SearchBox.IsKeyboardFocused;
                        trayWin.Close();
                    };
                    closeTimer.Start();
                    trayWin.ShowDialog(); // blocks here; the timer ticks inside the modal loop
                };
                trayMenu.Items.Add(trayItem);
                trayMenu.Show(System.Windows.Forms.Cursor.Position);
                for (int i = 0; i < 30; i++)
                {
                    System.Windows.Forms.Application.DoEvents();
                    Thread.Sleep(10);
                }
                trayItem.PerformClick();
                trayMenu.Close(); // real flow: the menu closes right after the item click
                for (int i = 0; i < 100; i++)
                {
                    System.Windows.Forms.Application.DoEvents();
                    Thread.Sleep(10);
                }
                Check("tray-sim: window opened and loaded via modal ShowDialog", trayLoaded);
                Check("tray-sim: search box keyboard-focused inside modal", trayFocused);
                trayMenu.Dispose();
            }
            catch (Exception ex)
            {
                Check("tray-sim ran without exception", false);
                Console.WriteLine("      " + ex);
            }

            Console.WriteLine("CustomRichPresenceWindow (WPF) interop:");
            // The one-stop GUI replacement for hand-editing config.json (and the
            // old Change App ID dialog): pre-fill from the current config, live
            // button + Application ID validation, and a save payload that only
            // mutates the edited fields.
            try
            {
                WpfHost.EnsureInitialized();
                var curCfg = AppCoordinator.GetDefaultConfig();
                var pWin = new CustomRichPresenceWindow(curCfg);

                Check("presence editor pre-fills idle details", pWin.IdleDetails == curCfg.Discord.Details);
                Check("presence editor pre-fills active template", pWin.ActiveDetails == curCfg.Discord.ActiveDetails);
                Check("presence editor pre-fills the application id", pWin.AppIdText == curCfg.Discord.ApplicationId);
                Check("app-id warning callout visible (message has WARNING part)", pWin.IsAppIdWarningVisible);
                Check("save enabled with valid defaults", pWin.IsSaveEnabled);

                pWin.SetButton2("Broken", "notaurl");
                Check("save disabled for invalid button URL", !pWin.IsSaveEnabled);
                Check("button validation hint visible", pWin.IsInvalidButtonsVisible);

                pWin.SetButton2("", "");
                Check("save re-enabled after clearing the bad button", pWin.IsSaveEnabled);

                pWin.AppIdText = "12345";
                Check("save disabled for invalid application id", !pWin.IsSaveEnabled);
                Check("app-id error visible for invalid id", pWin.IsAppIdErrorVisible);

                pWin.AppIdText = "123456789012345678";
                pWin.SetButton1("My Site", "https://example.com/");
                pWin.SetTimestamps(false);
                var built = pWin.BuildResult();
                Check("built result carries the edited application id",
                    built != null && built.Discord.ApplicationId == "123456789012345678");
                Check("built result keeps assets untouched",
                    built.Discord.Assets == curCfg.Discord.Assets);
                Check("built result carries the edited button",
                    built.Discord.Buttons != null && built.Discord.Buttons.Length == 1
                    && built.Discord.Buttons[0].Label == "My Site");
                Check("built result carries the timestamps toggle", built.Discord.ShowTimestamps == false);

                pWin.Show();
                for (int i = 0; i < 50; i++)
                {
                    System.Windows.Forms.Application.DoEvents();
                    Thread.Sleep(10);
                }
                Check("presence editor IsLoaded under WinForms pump", pWin.IsLoaded);
                pWin.Close();
            }
            catch (Exception ex)
            {
                Check("CustomRichPresenceWindow smoke test ran without exception", false);
                Console.WriteLine("      " + ex);
            }

            Console.WriteLine("GuideWindow (WPF) interop:");
            // The built-in Help & Guide: six localized topics rendered from the
            // guide_* keys, with link buttons on the About topic.
            try
            {
                WpfHost.EnsureInitialized();
                var gWin = new GuideWindow();

                Check("guide has six topics", gWin.TopicCount == 6);
                Check("first topic selected and rendered",
                    gWin.SelectedTopicIndex == 0 && gWin.RenderedParagraphCount > 0);

                gWin.SelectedTopicIndex = 5; // About
                Check("about topic renders", gWin.RenderedParagraphCount > 0);

                gWin.Show();
                for (int i = 0; i < 50; i++)
                {
                    System.Windows.Forms.Application.DoEvents();
                    Thread.Sleep(10);
                }
                Check("guide window IsLoaded under WinForms pump", gWin.IsLoaded);
                // Regression: the nav titles must actually render. DisplayMemberPath
                // resolves through WPF binding, which only sees PROPERTIES — the
                // original GuideTopic used public fields and every nav item was
                // silently empty.
                string navFirst = GuideNavItemText(gWin, 0);
                string navLast = GuideNavItemText(gWin, 5);
                Check("guide nav item 0 renders its title text", !string.IsNullOrEmpty(navFirst));
                Check("guide nav item 5 renders its title text", !string.IsNullOrEmpty(navLast));
                gWin.Close();
            }
            catch (Exception ex)
            {
                Check("GuideWindow smoke test ran without exception", false);
                Console.WriteLine("      " + ex);
            }

            Console.WriteLine("StatisticsWindow (WPF) interop:");
            // Prove the WPF statistics window renders rank rows, totals and the
            // empty state, and that the shared instance is reused then cleaned up.
            try
            {
                WpfHost.EnsureInitialized();

                // The tray menu checkmark is driven by this event: it must fire
                // true when the shared window opens and false when it closes.
                bool statsOpenChanged = false;
                StatisticsWindow.IsOpenChanged += isOpen => statsOpenChanged = isOpen;

                var statsVm = new StatisticsViewModel
                {
                    Title = "TODAY'S USAGE",
                    Subtitle = "Week of Jul 10, 2026",
                    EmptyMessage = "No data for today."
                };
                statsVm.Rows.Add(new StatsRow { Rank = 1, AppName = "Visual Studio Code", TimeText = "3h 12m" });
                statsVm.Rows.Add(new StatsRow { Rank = 2, AppName = "Notepad", TimeText = "42m 10s" });
                statsVm.Totals.Add("Total: 3h 54m");

                StatisticsWindow.Show(statsVm);
                for (int i = 0; i < 50; i++)
                {
                    System.Windows.Forms.Application.DoEvents();
                    Thread.Sleep(10);
                }

                var statsWin = StatisticsWindow.Instance;
                Check("stats open event fired on show", statsOpenChanged);
                Check("stats window IsLoaded under WinForms pump", statsWin != null && statsWin.IsLoaded);
                Check("stats title rendered", statsWin != null && statsWin.WindowTitleText == "TODAY'S USAGE");
                Check("stats rows rendered", statsWin != null && statsWin.RowCount == 2);
                Check("stats empty state hidden with data", statsWin != null && !statsWin.IsEmptyVisible);
                Check("stats totals rendered", statsWin != null && statsWin.TotalsCount == 1);

                StatisticsWindow.Show(new StatisticsViewModel
                {
                    Title = "ALL TIME STATISTICS",
                    EmptyMessage = "No statistics data available."
                });
                for (int i = 0; i < 50; i++)
                {
                    System.Windows.Forms.Application.DoEvents();
                    Thread.Sleep(10);
                }
                Check("stats instance reused (same window object)", statsWin != null && ReferenceEquals(statsWin, StatisticsWindow.Instance));
                Check("stats empty state visible without data", statsWin != null && statsWin.IsEmptyVisible);
                Check("stats rows cleared for empty view", statsWin != null && statsWin.RowCount == 0);
                Check("stats title swapped per view", statsWin != null && statsWin.WindowTitleText == "ALL TIME STATISTICS");

                statsWin?.Close();
                for (int i = 0; i < 20; i++)
                {
                    System.Windows.Forms.Application.DoEvents();
                    Thread.Sleep(10);
                }
                Check("stats instance cleared on close", StatisticsWindow.Instance == null);
                Check("stats open event fired false on close", !statsOpenChanged);
            }
            catch (Exception ex)
            {
                Check("StatisticsWindow smoke test ran without exception", false);
                Console.WriteLine("      " + ex);
            }

            Console.WriteLine("PresencePreviewWindow (WPF) interop:");
            // Prove the WPF presence preview renders presence updates, buttons,
            // paused/idle states and hide/show toggle (no Application ID => no
            // network calls, images stay on their placeholders).
            try
            {
                WpfHost.EnsureInitialized();
                var preview = new PresencePreviewWindow(null);
                preview.Show();
                for (int i = 0; i < 50; i++)
                {
                    System.Windows.Forms.Application.DoEvents();
                    Thread.Sleep(10);
                }
                Check("preview window IsLoaded under WinForms pump", preview.IsLoaded);
                // The deferred force-foreground retry must make the window the
                // active one (regression for the tray-menu activation pattern).
                Check("preview window active after show", preview.IsActive);
                // Footer buttons must use Segoe Fluent glyphs (not emoji) and the
                // pin button starts UNPINNED (Topmost defaults OFF — a default-on
                // pin floated the preview above other apps and the tray menu).
                Check("refresh button uses Segoe Fluent refresh glyph", preview.RefreshButtonGlyph == "\uE72C");
                Check("clear-cache button uses Segoe Fluent delete glyph", preview.ClearCacheButtonGlyph == "\uE74D");
                Check("pin button starts unpinned (not topmost by default)", preview.PinButtonGlyph == FluentGlyphs.Pin);

                preview.UpdatePresence(new RichPresence
                {
                    Details = "Working on something",
                    State = "Coding",
                    Assets = new Assets
                    {
                        LargeImageText = "geetRPCS",
                        LargeImageKey = "geetrpcs-logo",
                        SmallImageKey = "geetrpcs-small"
                    },
                    Buttons = new[] { new DiscordRPC.Button { Label = "Open Link", Url = "https://example.com" } }
                });
                for (int i = 0; i < 50; i++)
                {
                    System.Windows.Forms.Application.DoEvents();
                    Thread.Sleep(10);
                }
                Check("app name updated from presence", preview.AppNameValue == "geetRPCS");
                Check("details updated from presence", preview.DetailsValue == "Working on something");
                Check("state updated from presence", preview.StateValue == "Coding");
                Check("presence button 1 visible", preview.IsButton1Visible);
                Check("presence button 2 hidden", !preview.IsButton2Visible);
                Check("live status after presence", preview.StatusValue == LanguageManager.Current.PreviewLive);

                preview.SetPausedState();
                Check("paused status shown", preview.StatusValue == LanguageManager.Current.PreviewPaused);

                preview.SetIdleState();
                Check("idle state resets buttons", !preview.IsButton1Visible);
                Check("idle state restores app name", preview.AppNameValue == "geetRPCS");
                Check("idle state restores live status", preview.StatusValue == LanguageManager.Current.PreviewLive);

                preview.ToggleVisibility();
                Check("toggle hides preview", !preview.IsVisible);
                // The 1s elapsed timer must not tick while hidden (each tick was
                // re-running layout on the elapsed label for no visible effect).
                Check("elapsed timer stops while hidden", !preview.IsElapsedTimerRunning);
                preview.ToggleVisibility();
                Check("toggle shows preview again", preview.IsVisible);
                Check("elapsed timer resumes when shown", preview.IsElapsedTimerRunning);

                preview.Close();
                Check("elapsed timer stopped after close", !preview.IsElapsedTimerRunning);
            }
            catch (Exception ex)
            {
                Check("PresencePreviewWindow smoke test ran without exception", false);
                Console.WriteLine("      " + ex);
            }

            Console.WriteLine("Presence preview create/close heap growth (RAM-leak regression):");
            // A leaked per-window resource (timer, HttpClient, image cache) would
            // show up as retained heap growth across show/close cycles. Generous
            // 4MB bound: WPF caches templates/theme resources process-wide on
            // first use, so a few hundred KB of legit one-time caches are fine.
            try
            {
                WpfHost.EnsureInitialized();
                GC.Collect();
                GC.WaitForPendingFinalizers();
                long before = GC.GetTotalMemory(true);
                for (int i = 0; i < 10; i++)
                {
                    var w = new PresencePreviewWindow(null);
                    w.Show();
                    for (int p = 0; p < 10; p++) { System.Windows.Forms.Application.DoEvents(); Thread.Sleep(5); }
                    w.Close();
                    for (int p = 0; p < 10; p++) { System.Windows.Forms.Application.DoEvents(); Thread.Sleep(5); }
                }
                GC.Collect();
                GC.WaitForPendingFinalizers();
                long growth = GC.GetTotalMemory(true) - before;
                Console.WriteLine($"      [heap growth over 10 create/close cycles] {growth / 1024} KB");
                Check("preview create/close cycles do not leak the heap", growth < 4 * 1024 * 1024);
            }
            catch (Exception ex)
            {
                Check("preview heap-growth test ran without exception", false);
                Console.WriteLine("      " + ex);
            }

            Console.WriteLine("UpdateDialog (WPF) interop:");
            // Prove the WPF update dialogs configure their sections per mode
            // (enhanced: version row + changelog + in-app box; simple: body
            // text + hidden update sections) and render under the pump.
            try
            {
                WpfHost.EnsureInitialized();

                var appsWin = UpdateDialog.CreateApps("9.9.9");
                Check("apps dialog shows remote version", appsWin.VersionLeftValueText == "v9.9.9");
                Check("apps dialog hides update sections", !appsWin.IsInAppBoxVisible);
                Check("apps dialog shows body text", appsWin.IsInfoTextVisible);
                appsWin.Show();
                for (int i = 0; i < 50; i++)
                {
                    System.Windows.Forms.Application.DoEvents();
                    Thread.Sleep(10);
                }
                Check("apps dialog IsLoaded under WinForms pump", appsWin.IsLoaded);
                appsWin.Close();

                var wittyWin = UpdateDialog.CreateWitty("2.0.0");
                Check("witty dialog shows remote version", wittyWin.VersionLeftValueText == "v2.0.0");
                wittyWin.Close();

                var upToDateWin = UpdateDialog.CreateUpToDate();
                Check("up-to-date dialog shows current version",
                    upToDateWin.VersionLeftValueText == "v" + geetRPCS.Utils.AppVersion.VersionText);
                // Language-agnostic: the header must equal the localized title
                // (emoji stripped) whatever UI language the machine uses.
                Check("up-to-date header title has no duplicate emoji",
                    upToDateWin.HeaderTitleText == FluentGlyphs.StripLeadingEmoji(
                        LanguageManager.Current.DialogUpToDateTitle ?? "You're Up to Date!"));
                upToDateWin.Close();

                var enhancedWin = UpdateDialog.CreateEnhanced(new UpdateChecker.GitHubRelease
                {
                    TagName = "v9.9.9",
                    Body = "- fix a bug",
                    HtmlUrl = "https://github.com/geetcr4ck/geetRPCS/releases",
                    PublishedAt = new DateTime(2026, 8, 10, 12, 0, 0)
                });
                Check("enhanced dialog shows latest version", enhancedWin.VersionRightValueText == "v9.9.9");
                Check("enhanced dialog changelog rendered", enhancedWin.ChangelogText.Contains("fix a bug"));
                Check("enhanced dialog shows in-app update box", enhancedWin.IsInAppBoxVisible);
                enhancedWin.Close();

                string longNotes = new string('x', 900);
                string truncated = UpdateDialog.FormatReleaseNotes(longNotes);
                Check("release notes truncated to 800 chars", truncated.Length > 800 && truncated.EndsWith("GitHub]"));
                Check("empty release notes fall back to no-notes text",
                    UpdateDialog.FormatReleaseNotes(null) == LanguageManager.Current.UpdateNoReleaseNotes);
            }
            catch (Exception ex)
            {
                Check("UpdateDialog smoke test ran without exception", false);
                Console.WriteLine("      " + ex);
            }

            Console.WriteLine("Theme mode apply (Dark/Light/System live switch):");
            // WpfHost.ApplyThemeMode must switch the ModernWpf actual theme immediately,
            // and System must restore the follow-the-OS behavior (null = system).
            try
            {
                WpfHost.EnsureInitialized();
                WpfHost.ApplyThemeMode("Dark");
                Check("theme mode 'Dark' applies live",
                    ModernWpf.ThemeManager.Current.ActualApplicationTheme == ModernWpf.ApplicationTheme.Dark);
                WpfHost.ApplyThemeMode("Light");
                Check("theme mode 'Light' applies live",
                    ModernWpf.ThemeManager.Current.ActualApplicationTheme == ModernWpf.ApplicationTheme.Light);
                WpfHost.ApplyThemeMode("System");
                Check("theme mode 'System' restores follow-OS",
                    ModernWpf.ThemeManager.Current.ApplicationTheme == null);
                // The tray menu Theme item shows the active mode as a text suffix
                // (e.g. "🌗 Theme: Dark") so no submenu visit is needed. Compared
                // against the localized label — the machine's settings.json may
                // hold any UI language.
                Check("theme menu text shows System mode",
                    TrayMenuController.GetThemeMenuText("System").EndsWith(LanguageManager.Current.MenuThemeSystem ?? "System"));
                Check("theme menu text shows Dark mode",
                    TrayMenuController.GetThemeMenuText("Dark").EndsWith(LanguageManager.Current.MenuThemeDark ?? "Dark"));
                Check("theme menu text shows Light mode",
                    TrayMenuController.GetThemeMenuText("Light").EndsWith(LanguageManager.Current.MenuThemeLight ?? "Light"));
                // The emoji prefix (🌗) is replaced by a Fluent glyph image, so the text is clean.
                Check("theme menu text has no emoji prefix", !TrayMenuController.GetThemeMenuText("Dark").Contains("🌗"));
            }
            catch (Exception ex)
            {
                Check("theme mode apply ran without exception", false);
                Console.WriteLine("      " + ex);
            }

            Console.WriteLine("Fluent theme resource keys (must resolve under WpfHost):");
            // Every DynamicResource key the windows rely on must exist in the loaded
            // ModernWpf theme dictionaries (light and dark), otherwise the value
            // silently stays unset.
            try
            {
                WpfHost.EnsureInitialized();
                string[] themeKeys =
                {
                    "SystemFillColorSuccessBrush",
                    "SystemFillColorCautionBrush",
                    "SystemFillColorCriticalBrush",
                    "SystemFillColorCautionBackgroundBrush",
                    "AccentFillColorDefaultBrush",
                    "AccentTextFillColorPrimaryBrush",
                    "TextOnAccentFillColorPrimaryBrush",
                    "DividerStrokeColorDefaultBrush"
                };
                foreach (var key in themeKeys)
                {
                    Check($"theme resource '{key}' resolves",
                        System.Windows.Application.Current?.TryFindResource(key) != null);
                }
            }
            catch (Exception ex)
            {
                Check("theme resource key check ran without exception", false);
                Console.WriteLine("      " + ex);
            }

            Console.WriteLine("FluentGlyphs tray-menu helpers (emoji strip + glyph images):");
            // Tray menu items render Segoe Fluent glyphs as item images; the emoji
            // prefix is stripped from the localized text at display time.
            try
            {
                Check("strip surrogate-pair emoji", FluentGlyphs.StripLeadingEmoji("🔄 Reload Config") == "Reload Config");
                Check("strip BMP emoji", FluentGlyphs.StripLeadingEmoji("✅ You're Up to Date!") == "You're Up to Date!");
                Check("strip emoji + variation selector", FluentGlyphs.StripLeadingEmoji("👁️ Preview Window") == "Preview Window");
                Check("strip gear emoji + variation selector", FluentGlyphs.StripLeadingEmoji("⚙️ Manage Apps") == "Manage Apps");
                Check("strip pause emoji + variation selector", FluentGlyphs.StripLeadingEmoji("⏸️ Pause") == "Pause");
                Check("strip play triangle + variation selector", FluentGlyphs.StripLeadingEmoji("▶️ Resume") == "Resume");
                Check("text without emoji unchanged", FluentGlyphs.StripLeadingEmoji("Pause") == "Pause");
                Check("null text becomes empty", FluentGlyphs.StripLeadingEmoji(null) == "");

                using (var img = FluentGlyphs.CreateMenuGlyph(FluentGlyphs.Settings))
                {
                    Check("menu glyph bitmap is 16x16", img != null && img.Width == 16 && img.Height == 16);
                    bool hasInk = false;
                    for (int x = 0; x < img.Width && !hasInk; x++)
                        for (int y = 0; y < img.Height && !hasInk; y++)
                            if (img.GetPixel(x, y).A > 0) hasInk = true;
                    Check("menu glyph actually rendered pixels", hasInk);
                }
                Check("tray glyph constants are non-empty",
                    FluentGlyphs.Settings.Length > 0 && FluentGlyphs.View.Length > 0 && FluentGlyphs.Moon.Length > 0);

                // Glyph color follows the ACTIVE theme (ThemePalette.TextSecondary),
                // not a fixed neutral gray — and switches when the theme mode changes.
                WpfHost.ApplyThemeMode("Dark");
                var darkBrush = ThemePalette.TextSecondary;
                using (var darkImg = FluentGlyphs.CreateMenuGlyph(FluentGlyphs.Settings))
                {
                    var px = MaxAlphaPixel(darkImg);
                    Check("dark-theme glyph renders light pixels", px.A > 0 && px.R > 128 && px.G > 128 && px.B > 128);
                    Check("dark-theme glyph matches ThemePalette.TextSecondary",
                        Math.Abs(px.R - darkBrush.R) <= 10 && Math.Abs(px.G - darkBrush.G) <= 10 && Math.Abs(px.B - darkBrush.B) <= 10);
                    Check("dark-theme glyph is not the old fixed gray", px.R != 117 || px.G != 117 || px.B != 117);
                }
                WpfHost.ApplyThemeMode("Light");
                var lightBrush = ThemePalette.TextSecondary;
                using (var lightImg = FluentGlyphs.CreateMenuGlyph(FluentGlyphs.Settings))
                {
                    var px = MaxAlphaPixel(lightImg);
                    Check("light-theme glyph renders dark pixels", px.A > 0 && px.R < 100 && px.G < 100 && px.B < 100);
                    Check("glyph color changes when the theme mode changes", darkBrush.ToArgb() != lightBrush.ToArgb());
                }
                // Explicit color overload is honored (used for theme-aware rendering).
                using (var redImg = FluentGlyphs.CreateMenuGlyph(FluentGlyphs.Settings, System.Drawing.Color.FromArgb(255, 220, 30, 30)))
                {
                    var px = MaxAlphaPixel(redImg);
                    Check("explicit glyph color overload honored", px.A > 0 && px.R > 150 && px.G < 100 && px.B < 100);
                }
                WpfHost.ApplyThemeMode("System");
            }
            catch (Exception ex)
            {
                Check("FluentGlyphs helpers ran without exception", false);
                Console.WriteLine("      " + ex);
            }

            Console.WriteLine("FluentMenuRenderer (tray menu styling):");
            // The custom renderer must construct, resolve theme colors, and paint
            // the menu background with the current theme's color (light/dark).
            try
            {
                var renderer = new FluentMenuRenderer();
                Check("FluentMenuRenderer constructs", renderer != null);
                // ModernWpf subtle-fill/divider brushes are translucent in the dark
                // theme (e.g. DividerStrokeColorDefaultBrush = #26FFFFFF), so only
                // assert they resolve to a visible (non-fully-transparent) color.
                Check("menu hover fill resolves visible", ThemePalette.HoverFill.A > 0);
                Check("menu divider resolves visible", ThemePalette.Divider.A > 0);
                Check("menu secondary text resolves visible", ThemePalette.TextSecondary.A > 0);

                // Compact menu sizing: reduced vertical padding keeps the tray menu
                // from stretching too tall (~24px items instead of ~30px).
                Check("compact menu item padding is (8,4,8,4)",
                    TrayMenuController.MenuItemPadding == new System.Windows.Forms.Padding(8, 4, 8, 4));
                using (var oldItem = new System.Windows.Forms.ToolStripMenuItem("Compact")
                { Padding = new System.Windows.Forms.Padding(10, 6, 10, 6) })   // previous padding
                using (var compactItem = new System.Windows.Forms.ToolStripMenuItem("Compact")
                { Padding = TrayMenuController.MenuItemPadding })
                {
                    var oldPref = oldItem.GetPreferredSize(System.Drawing.Size.Empty);
                    var newPref = compactItem.GetPreferredSize(System.Drawing.Size.Empty);
                    Check("compact item is shorter than the old padding", newPref.Height < oldPref.Height);
                    Check("compact item saves the 4px vertical padding", oldPref.Height - newPref.Height == 4);
                    Check("compact item height stays readable", newPref.Height >= 20 && newPref.Height <= 30);
                }

                using (var menu = new System.Windows.Forms.ContextMenuStrip())
                {
                    menu.Renderer = renderer;
                    menu.Items.Add(new System.Windows.Forms.ToolStripMenuItem("Test Item")
                    { Padding = new System.Windows.Forms.Padding(10, 6, 10, 6) });
                    menu.Items.Add(new System.Windows.Forms.ToolStripMenuItem("Second"));
                    menu.Size = new System.Drawing.Size(220, 64);
                    menu.PerformLayout();
                    using (var bmp = new System.Drawing.Bitmap(menu.Width, menu.Height))
                    {
                        menu.DrawToBitmap(bmp, new System.Drawing.Rectangle(System.Drawing.Point.Empty, menu.Size));
                        var px = bmp.GetPixel(2, 2);
                        Check("menu background painted from theme", px.ToArgb() == ThemePalette.Background.ToArgb());
                    }
                }
            }
            catch (Exception ex)
            {
                Check("FluentMenuRenderer tests ran without exception", false);
                Console.WriteLine("      " + ex);
            }

            Console.WriteLine("Tray menu visual snapshot (dark vs light) + toggle-state icons:");
            // Renders the tray menu (top-level + stats submenu) in dark and light to
            // PNGs under .visual/ (viewable in the preview gallery) and verifies the
            // toggle-item ON indicator: .NET 8 renders Checked+Image as a hardcoded
            // OS-blue square that no renderer override can stop, so image items show
            // their ON state as an ACCENT-colored 16px glyph (SetToggleState) instead
            // of the Checked property.
            try
            {
                string outDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".visual");
                Directory.CreateDirectory(outDir);

                System.Drawing.Bitmap BuildMenu(bool dark, bool statsOnly)
                {
                    WpfHost.ApplyThemeMode(dark ? "Dark" : "Light");
                    var m = new System.Windows.Forms.ContextMenuStrip { Renderer = new FluentMenuRenderer() };
                    System.Windows.Forms.ToolStripMenuItem Item(string text, string glyph, bool on = false)
                    {
                        var i = new System.Windows.Forms.ToolStripMenuItem(text)
                        {
                            Image = FluentGlyphs.CreateMenuGlyph(glyph, on ? ThemePalette.AccentGlyph : ThemePalette.TextSecondary),
                            Padding = TrayMenuController.MenuItemPadding,
                            Tag = glyph
                        };
                        return i;
                    }
                    System.Windows.Forms.ToolStripSeparator Sep() => new System.Windows.Forms.ToolStripSeparator();
                    if (statsOnly)
                    {
                        m.Items.Add(Item("Today", FluentGlyphs.Calendar));
                        m.Items.Add(Item("This Week", FluentGlyphs.CalendarWeek));
                        m.Items.Add(Item("This Month", FluentGlyphs.Chart));
                        m.Items.Add(Item("All Time", FluentGlyphs.Stopwatch));
                        m.Items.Add(Sep());
                        m.Items.Add(Item("Export CSV", FluentGlyphs.Save));
                        m.Items.Add(Item("Export JSON", FluentGlyphs.Document));
                        m.Items.Add(Sep());
                        m.Items.Add(Item("Reset Stats", FluentGlyphs.Delete));
                    }
                    else
                    {
                        m.Items.Add(Item("Pause", FluentGlyphs.Play, on: true));
                        m.Items.Add(Item("Private Mode", FluentGlyphs.Lock));
                        m.Items.Add(Item("Mouse Energy", FluentGlyphs.Mouse, on: true));
                        m.Items.Add(Item("Tray Animation", FluentGlyphs.Palette));
                        m.Items.Add(Item("Theme: Dark", FluentGlyphs.Moon));
                        m.Items.Add(Item("Telemetry", FluentGlyphs.Send));
                        m.Items.Add(Item("Auto-Update", FluentGlyphs.UpdateRestore));
                        m.Items.Add(Sep());
                        m.Items.Add(Item("Manage Apps", FluentGlyphs.Settings, on: true));
                        m.Items.Add(Item("Change Application ID", FluentGlyphs.Edit));
                        m.Items.Add(Sep());
                        m.Items.Add(Item("Statistics", FluentGlyphs.Chart));
                        m.Items.Add(Item("Preview Window", FluentGlyphs.View));
                        m.Items.Add(Sep());
                        m.Items.Add(Item("Run on Startup", FluentGlyphs.Flag, on: true));
                        m.Items.Add(Item("Quick Actions", FluentGlyphs.Bolt));
                        m.Items.Add(Sep());
                        m.Items.Add(Item("Language", FluentGlyphs.Globe));
                        m.Items.Add(Item("Check for Updates", FluentGlyphs.Refresh));
                        m.Items.Add(Item("Open Log", FluentGlyphs.Document));
                        m.Items.Add(Sep());
                        m.Items.Add(Item("Exit", FluentGlyphs.Power));
                    }
                    var pref = m.GetPreferredSize(System.Drawing.Size.Empty);
                    m.Size = new System.Drawing.Size(pref.Width, pref.Height);
                    m.PerformLayout();
                    var bmp = new System.Drawing.Bitmap(m.Width, m.Height);
                    m.DrawToBitmap(bmp, new System.Drawing.Rectangle(System.Drawing.Point.Empty, m.Size));
                    return bmp;
                }

                using (var b = BuildMenu(true, false)) b.Save(Path.Combine(outDir, "menu_dark.png"));
                using (var b = BuildMenu(false, false)) b.Save(Path.Combine(outDir, "menu_light.png"));
                using (var b = BuildMenu(true, true)) b.Save(Path.Combine(outDir, "stats_dark.png"));
                using (var b = BuildMenu(false, true)) b.Save(Path.Combine(outDir, "stats_light.png"));

                // Toggle-state icon checks (dark theme):
                WpfHost.ApplyThemeMode("Dark");
                System.Drawing.Bitmap ToggleMenu(bool on)
                {
                    var m = new System.Windows.Forms.ContextMenuStrip { Renderer = new FluentMenuRenderer() };
                    m.Items.Add(new System.Windows.Forms.ToolStripMenuItem("Toggle")
                    {
                        Padding = TrayMenuController.MenuItemPadding,
                        Tag = FluentGlyphs.Settings,
                        Image = FluentGlyphs.CreateMenuGlyph(FluentGlyphs.Settings,
                            on ? ThemePalette.AccentGlyph : ThemePalette.TextSecondary)
                    });
                    var pref = m.GetPreferredSize(System.Drawing.Size.Empty);
                    m.Size = new System.Drawing.Size(pref.Width, pref.Height);
                    m.PerformLayout();
                    var bmp = new System.Drawing.Bitmap(m.Width, m.Height);
                    m.DrawToBitmap(bmp, new System.Drawing.Rectangle(System.Drawing.Point.Empty, m.Size));
                    return bmp;
                }
                int IconColumnBlue(System.Drawing.Bitmap bmp)
                {
                    int blues = 0;
                    for (int y = 0; y < bmp.Height; y++)
                        for (int x = 0; x < 34; x++)
                        {
                            var p = bmp.GetPixel(x, y);
                            if (p.A > 25 && p.B > 120 && p.B - p.R > 40) blues++;
                        }
                    return blues;
                }
                using (var onBmp = ToggleMenu(true))
                {
                    // The old .NET 8 checked+image rendering painted ~460 blue pixels
                    // (a scaled OS-blue square); the 16px accent glyph is far smaller.
                    Check("toggle ON icon has no blue-square rendering", IconColumnBlue(onBmp) < 200);
                    // Most saturated blue pixel within the icon column (x<34) — the
                    // menu border is gray (b-r == 0) and the text is outside the column.
                    var px = MostBluePixelInColumn(onBmp);
                    var acc = ThemePalette.AccentGlyph;
                    Check("toggle ON icon is accent-colored",
                        Math.Abs(px.R - acc.R) <= 12 && Math.Abs(px.G - acc.G) <= 12 && Math.Abs(px.B - acc.B) <= 12);
                }
                using (var offBmp = ToggleMenu(false))
                    Check("toggle OFF icon is not accent-colored", IconColumnBlue(offBmp) < 40);

                // SetToggleState swaps the baked glyph color between accent and gray.
                using (var toggleItem = new System.Windows.Forms.ToolStripMenuItem("T")
                {
                    Tag = FluentGlyphs.Settings,
                    Image = FluentGlyphs.CreateMenuGlyph(FluentGlyphs.Settings, ThemePalette.TextSecondary)
                })
                {
                    TrayMenuController.SetToggleState(toggleItem, true);
                    using (var onImg = (System.Drawing.Bitmap)toggleItem.Image)
                    {
                        var px = MaxAlphaPixel(onImg);
                        var acc = ThemePalette.AccentGlyph;
                        Check("SetToggleState(true) renders accent icon",
                            Math.Abs(px.R - acc.R) <= 12 && Math.Abs(px.G - acc.G) <= 12 && Math.Abs(px.B - acc.B) <= 12);
                    }
                    TrayMenuController.SetToggleState(toggleItem, false);
                    using (var offImg = (System.Drawing.Bitmap)toggleItem.Image)
                    {
                        var px = MaxAlphaPixel(offImg);
                        var sec = ThemePalette.TextSecondary;
                        Check("SetToggleState(false) renders secondary-gray icon",
                            Math.Abs(px.R - sec.R) <= 12 && Math.Abs(px.G - sec.G) <= 12 && Math.Abs(px.B - sec.B) <= 12);
                    }
                }

                // Contrast-safe accent: a bright Windows accent (e.g. cyan) must not
                // make ON icons invisible on the light menu — AccentGlyph guarantees
                // >=3:1 against the current theme background in both themes.
                WpfHost.ApplyThemeMode("Light");
                Check("light-theme icon accent has >=3:1 contrast",
                    ThemePalette.ContrastRatio(ThemePalette.AccentGlyph, ThemePalette.Background) >= 3.0);
                WpfHost.ApplyThemeMode("Dark");
                Check("dark-theme icon accent has >=3:1 contrast",
                    ThemePalette.ContrastRatio(ThemePalette.AccentGlyph, ThemePalette.Background) >= 3.0);

                // Submenu selection indicator: accent check glyph when selected,
                // transparent placeholder otherwise (a non-null image keeps the
                // dropdown's image gutter from collapsing, so all columns stay
                // aligned with the main menu).
                using (var sel = new System.Windows.Forms.ToolStripMenuItem("Sel"))
                using (var unsel = new System.Windows.Forms.ToolStripMenuItem("Unsel"))
                {
                    TrayMenuController.SetSubmenuSelection(sel, true);
                    TrayMenuController.SetSubmenuSelection(unsel, false);
                    Check("submenu selected item shows check glyph", sel.Image != null);
                    Check("submenu unselected item keeps transparent 16px placeholder",
                        unsel.Image is System.Drawing.Bitmap ph && ph.Width == 16 && ph.Height == 16 && MaxAlphaPixel(ph).A == 0);
                    if (sel.Image != null)
                    {
                        using (var img = (System.Drawing.Bitmap)sel.Image)
                        {
                            var px = MaxAlphaPixel(img);
                            var acc = ThemePalette.AccentGlyph;
                            Check("submenu selection glyph is accent-colored",
                                Math.Abs(px.R - acc.R) <= 12 && Math.Abs(px.G - acc.G) <= 12 && Math.Abs(px.B - acc.B) <= 12);
                        }
                    }
                }
                WpfHost.ApplyThemeMode("System");
            }
            catch (Exception ex)
            {
                Check("tray menu visual snapshot ran without exception", false);
                Console.WriteLine("      " + ex);
            }

            Console.WriteLine("TrayMenuController end-to-end (interface fakes, real Rebuild()):");
            // Rebuild() normally needs a full Program + AppCoordinator; the
            // ITrayCoordinator/ITrayShell contracts let the REAL menu be built and
            // clicked in a test with lightweight fakes.
            try
            {
                var coord = new FakeCoordinator();
                var shell = new FakeShell();
                using (var icon = new System.Windows.Forms.NotifyIcon())
                {
                    var controller = new TrayMenuController(icon, coord, shell);
                    controller.Rebuild();
                    var menu = icon.ContextMenuStrip;
                    Check("Rebuild built the real tray menu", menu != null && menu.Items.Count >= 20);
                    Check("pause item text has no emoji leak", controller.PauseItem != null
                        && controller.PauseItem.Text == FluentGlyphs.StripLeadingEmoji(LanguageManager.Current.MenuPause));
                    Check("all toggle items created", controller.PrivateModeItem != null
                        && controller.MouseEnergyItem != null && controller.TrayAnimationItem != null
                        && controller.ThemeMenuItem != null && controller.ManageAppsMenuItem != null
                        && controller.StatisticsMenuItem != null && controller.PreviewMenuItem != null);

                    // Clicking Pause forwards to the coordinator.
                    controller.PauseItem.PerformClick();
                    Check("pause click forwards to coordinator", coord.Called.Contains("TogglePause"));

                    // Paused state swaps text+glyph via UpdatePresentation (no rebuild).
                    coord.IsPaused = true;
                    controller.UpdatePresentation();
                    Check("paused item shows resume text (no emoji)",
                        controller.PauseItem.Text == FluentGlyphs.StripLeadingEmoji(LanguageManager.Current.MenuResume));
                    Check("paused item swaps to play glyph", (string)controller.PauseItem.Tag == FluentGlyphs.Play);

                    // Mouse Energy toggle: starts off, click flips state + coordinator.
                    bool prevMouseEnergy = SettingsService.Instance.MouseEnergyEnabled;
                    try
                    {
                        SettingsService.Instance.MouseEnergyEnabled = false;
                        controller.Rebuild();
                        menu = icon.ContextMenuStrip;
                        var mouseItem = controller.MouseEnergyItem;
                        mouseItem.PerformClick();
                        Check("mouse-energy click forwards new state", coord.Called.Contains("SetMouseEnergy:True"));
                        using (var img = (System.Drawing.Bitmap)mouseItem.Image)
                        {
                            var px = MaxAlphaPixel(img);
                            var acc = ThemePalette.AccentGlyph;
                            Check("mouse-energy ON icon is accent after click",
                                Math.Abs(px.R - acc.R) <= 12 && Math.Abs(px.G - acc.G) <= 12 && Math.Abs(px.B - acc.B) <= 12);
                        }
                    }
                    finally { SettingsService.Instance.MouseEnergyEnabled = prevMouseEnergy; }

                    // Statistics submenu clicks reach the stats coordinator.
                    var stats = (FakeStats)coord.Stats;
                    controller.StatisticsMenuItem.DropDownItems[0].PerformClick(); // Today
                    controller.StatisticsMenuItem.DropDownItems[1].PerformClick(); // This Week
                    controller.StatisticsMenuItem.DropDownItems[2].PerformClick(); // This Month
                    controller.StatisticsMenuItem.DropDownItems[3].PerformClick(); // All Time
                    Check("statistics submenu routes to stats coordinator",
                        stats.Called.Contains("today") && stats.Called.Contains("week")
                        && stats.Called.Contains("month") && stats.Called.Contains("alltime"));

                    // Private Mode: click forwards + ON state swaps to accent.
                    controller.PrivateModeItem.PerformClick();
                    Check("private-mode click forwards to coordinator", coord.Called.Contains("TogglePrivateMode"));
                    coord.PrivateMode = true;
                    controller.UpdatePresentation();
                    using (var privImg = (System.Drawing.Bitmap)controller.PrivateModeItem.Image)
                    {
                        var px = MaxAlphaPixel(privImg);
                        var acc = ThemePalette.AccentGlyph;
                        Check("private-mode ON icon is accent after click",
                            Math.Abs(px.R - acc.R) <= 12 && Math.Abs(px.G - acc.G) <= 12 && Math.Abs(px.B - acc.B) <= 12);
                    }

                    // Tray Animation toggle forwards the new state.
                    controller.TrayAnimationItem.PerformClick();
                    Check("tray-animation click forwards new state",
                        coord.Called.Any(c => c.StartsWith("SetTrayAnimation:")));

                    // Telemetry forwards the inverted current state (record happens
                    // synchronously before the await completes).
                    var telemetryItem = menu.Items.OfType<System.Windows.Forms.ToolStripMenuItem>()
                        .First(i => (i.Tag as string) == FluentGlyphs.Send);
                    telemetryItem.PerformClick();
                    Check("telemetry click forwards new state",
                        coord.Called.Any(c => c.StartsWith("ToggleTelemetry:")));

                    // "Help & Guide" must survive WinForms mnemonic processing: the
                    // raw "&" was swallowed as an access-key prefix (invisible
                    // underline on the following space), rendering "Help  Guide".
                    Check("mnemonic escape doubles the ampersand",
                        TrayMenuController.EscapeMnemonics("Help & Guide") == "Help && Guide"
                        && TrayMenuController.EscapeMnemonics("Pause") == "Pause");
                    var helpItem = menu.Items.OfType<System.Windows.Forms.ToolStripMenuItem>()
                        .First(i => (i.Tag as string) == FluentGlyphs.Help);
                    Check("help item text carries a literal ampersand (&&)",
                        helpItem.Text.Contains("&&"));

                    // Auto-Update flips the persisted setting (restored afterwards so
                    // the dev's settings.json is untouched).
                    bool wasAutoUpdate = SettingsService.Instance.AutoUpdateEnabled;
                    try
                    {
                        var autoItem = menu.Items.OfType<System.Windows.Forms.ToolStripMenuItem>()
                            .First(i => (i.Tag as string) == FluentGlyphs.UpdateRestore);
                        autoItem.PerformClick();
                        Check("auto-update click flips the setting",
                            SettingsService.Instance.AutoUpdateEnabled != wasAutoUpdate);
                    }
                    finally
                    {
                        SettingsService.Instance.AutoUpdateEnabled = wasAutoUpdate;
                        SettingsService.SaveAsync().GetAwaiter().GetResult();
                    }

                    // Manage Apps / Preview clicks call the shell directly (the menu
                    // handle is forced so the ToolStrip is fully created first).
                    var _ = menu.Handle;
                    controller.ManageAppsMenuItem.PerformClick();
                    controller.PreviewMenuItem.PerformClick();
                    for (int i = 0; i < 30 &&
                        !(shell.Called.Contains("ToggleManageApps") && shell.Called.Contains("TogglePreview")); i++)
                    {
                        System.Windows.Forms.Application.DoEvents();
                        Thread.Sleep(10);
                    }
                    Check("manage-apps click defers to shell", shell.Called.Contains("ToggleManageApps"));
                    Check("preview click defers to shell", shell.Called.Contains("TogglePreview"));

                    // Statistics exports route to the stats coordinator.
                    controller.StatisticsMenuItem.DropDownItems[5].PerformClick(); // Export CSV
                    controller.StatisticsMenuItem.DropDownItems[6].PerformClick(); // Export JSON
                    Check("stats exports route to stats coordinator",
                        stats.Called.Contains("export:csv") && stats.Called.Contains("export:json"));

                    // Shell actions: Check for Updates (the only top-level Refresh item),
                    // Open Log and Exit forward to the shell.
                    var checkItem = menu.Items.OfType<System.Windows.Forms.ToolStripMenuItem>()
                        .FirstOrDefault(i => (i.Tag as string) == FluentGlyphs.Refresh);
                    checkItem?.PerformClick();
                    Check("check-for-updates forwards to shell", shell.Called.Contains("CheckUpdates"));
                    menu.Items.OfType<System.Windows.Forms.ToolStripMenuItem>()
                        .First(i => (i.Tag as string) == FluentGlyphs.Document).PerformClick();
                    Check("open-log forwards to shell", shell.Called.Contains("OpenLog"));
                    menu.Items.OfType<System.Windows.Forms.ToolStripMenuItem>()
                        .First(i => (i.Tag as string) == FluentGlyphs.Power).PerformClick();
                    Check("exit forwards to shell", shell.Called.Contains("Exit"));

                    // Items we do NOT click (registry writes, editor launches, dialogs,
                    // persistence): assert structure instead.
                    Check("startup item present (not clicked: registry side effect)",
                        menu.Items.OfType<System.Windows.Forms.ToolStripMenuItem>()
                            .Any(i => (i.Tag as string) == FluentGlyphs.Flag));
                    Check("theme submenu has the 3 mode items",
                        controller.ThemeMenuItem.DropDownItems.Count == 3);
                    Check("quick actions submenu present (not clicked: dialogs/editors)",
                        menu.Items.OfType<System.Windows.Forms.ToolStripMenuItem>()
                            .Any(i => (i.Tag as string) == FluentGlyphs.Bolt));
                    Check("language submenu lists the languages",
                        menu.Items.OfType<System.Windows.Forms.ToolStripMenuItem>()
                            .Any(i => (i.Tag as string) == FluentGlyphs.Globe && i.DropDownItems.Count > 0));
                    Check("stats submenu has all 9 entries", controller.StatisticsMenuItem.DropDownItems.Count == 9);
                }
            }
            catch (Exception ex)
            {
                Check("TrayMenuController end-to-end ran without exception", false);
                Console.WriteLine("      " + ex);
            }

            Console.WriteLine("ManageAppsWindow Esc close + virtualization:");
            try
            {
                WpfHost.EnsureInitialized();
                var escWin = new ManageAppsWindow(
                    new List<AppConfig>(), new HashSet<string>(),
                    new Dictionary<string, AppOverrideConfig>(),
                    (proc, enabled) => { }, (proc, d, s) => { });
                escWin.Show();
                for (int i = 0; i < 30; i++) { System.Windows.Forms.Application.DoEvents(); Thread.Sleep(10); }
                Check("manage apps window loads", escWin.IsLoaded);
                Check("apps list is virtualized",
                    System.Windows.Controls.VirtualizingStackPanel.GetIsVirtualizing(escWin.AppsList));
                escWin.Close();

                // Always-on-top regression: PrepareForShow pins Topmost so the
                // modal open wins the foreground fight, but the pin must be
                // RELEASED once the window activates — not stay on for the whole
                // session floating above other apps and the tray menu.
                var topWin = new ManageAppsWindow(
                    new List<AppConfig>(), new HashSet<string>(),
                    new Dictionary<string, AppOverrideConfig>(),
                    (proc, enabled) => { }, (proc, d, s) => { });
                topWin.PrepareForShow();
                Check("prepare-for-show arms the temporary topmost pin", topWin.Topmost);
                topWin.Show();
                for (int i = 0; i < 50; i++) { System.Windows.Forms.Application.DoEvents(); Thread.Sleep(10); }
                Check("manage apps window releases Topmost after activation", !topWin.Topmost);
                topWin.Close();

                // Esc must CLOSE the window for real: every open is a fresh
                // window so each open gets the native Win10/11 DWM open animation.
                var escWin2 = new ManageAppsWindow(
                    new List<AppConfig>(), new HashSet<string>(),
                    new Dictionary<string, AppOverrideConfig>(),
                    (proc, enabled) => { }, (proc, d, s) => { });
                escWin2.Show();
                for (int i = 0; i < 30; i++) { System.Windows.Forms.Application.DoEvents(); Thread.Sleep(10); }
                var escSource = System.Windows.PresentationSource.FromVisual(escWin2);
                var escArgs = new System.Windows.Input.KeyEventArgs(
                    System.Windows.Input.Keyboard.PrimaryDevice, escSource, 0, System.Windows.Input.Key.Escape);
                escArgs.RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent;
                escWin2.RaiseEvent(escArgs);
                for (int i = 0; i < 30; i++) { System.Windows.Forms.Application.DoEvents(); Thread.Sleep(10); }
                Check("Esc closes the manage apps window (fresh window per open)",
                    !escWin2.IsVisible && !escWin2.IsLoaded);
            }
            catch (Exception ex)
            {
                Check("ManageAppsWindow Esc/virtualization test ran without exception", false);
                Console.WriteLine("      " + ex);
            }

            Console.WriteLine("ManageAppsWindow open/typing/clear hitch checks (debounced filter):");
            // User report: opening the window, typing in the search box and clicking
            // the clear-X each froze the PC for a few ms. The fix: the list is bound
            // once to a stable filtered view (no ItemsSource replacement per
            // keystroke), filtering is debounced, and the initial population is
            // deferred off the first layout pass.
            try
            {
                WpfHost.EnsureInitialized();
                var perfApps = new List<AppConfig>();
                for (int i = 0; i < 100; i++)
                    perfApps.Add(new AppConfig { Process = "proc" + i, AppName = "App " + i, ClientId = "12345678901234567" });
                var perfWin = new ManageAppsWindow(perfApps, new HashSet<string>(),
                    new Dictionary<string, AppOverrideConfig>(), (p, e) => { }, (p, d, s) => { });
                var sw = System.Diagnostics.Stopwatch.StartNew();
                perfWin.Show();
                int openPumps = 0;
                while (!perfWin.IsLoaded && openPumps < 200)
                {
                    System.Windows.Forms.Application.DoEvents();
                    Thread.Sleep(2);
                    openPumps++;
                }
                sw.Stop();
                Console.WriteLine("      [open until IsLoaded] {0} ms ({1} pumps)", sw.ElapsedMilliseconds, openPumps);
                Check("perf: window loads with 100 apps", perfWin.IsLoaded);
                // Show() loads the window synchronously (0 pumps), but the list
                // population is deferred to a Background-priority op. Manual
                // DoEvents pumping starves Background ops, so wait on WALL CLOCK
                // (same as the settle loops below) instead of a fixed pump count.
                var swPop = System.Diagnostics.Stopwatch.StartNew();
                while (perfWin.AppsList.Items.Count != 100 && swPop.ElapsedMilliseconds < 3000)
                {
                    System.Windows.Forms.Application.DoEvents();
                    Thread.Sleep(5);
                }
                Check("perf: list populated after deferred load", perfWin.AppsList.Items.Count == 100);
                var sourceView = perfWin.AppsList.ItemsSource;
                string count100 = string.Format(
                    LanguageManager.Current.ManageAppsFound ?? "{0} apps found", 100);
                string count11 = string.Format(
                    LanguageManager.Current.ManageAppsFound ?? "{0} apps found", 11);
                Check("perf: initial count shown", perfWin.CountText.Text == count100);

                // Typing "App 9" matches App 9 + App 90-99 = 11 items. The count must
                // NOT change synchronously (debounce) and must settle after the timer.
                var swType = System.Diagnostics.Stopwatch.StartNew();
                perfWin.SearchBox.Text = "App 9";
                swType.Stop();
                Console.WriteLine("      [set-text sync] {0} ms", swType.ElapsedMilliseconds);
                Check("typing does not re-filter synchronously", perfWin.CountText.Text == count100);
                Check("set-text itself is cheap (<50ms)", swType.ElapsedMilliseconds < 50);
                // Tight pump bounded by WALL CLOCK (the debounce timer needs 120ms
                // of real time, a pump-count guard would exit early) so the settle
                // time is a real measurement of debounce + filter + row re-realization.
                var swSettle = System.Diagnostics.Stopwatch.StartNew();
                while (perfWin.CountText.Text != count11 && swSettle.ElapsedMilliseconds < 3000)
                {
                    System.Windows.Forms.Application.DoEvents();
                }
                swSettle.Stop();
                Console.WriteLine("      [debounce+filter settle] {0} ms (incl. 120ms debounce)", swSettle.ElapsedMilliseconds);
                Check("filter settles after debounce", perfWin.CountText.Text == count11);
                Check("typing keeps the stable view (no ItemsSource rebuild)",
                    ReferenceEquals(perfWin.AppsList.ItemsSource, sourceView));

                // Clearing (same code path as the built-in X button).
                var swClear = System.Diagnostics.Stopwatch.StartNew();
                perfWin.SearchBox.Text = "";
                swClear.Stop();
                Console.WriteLine("      [clear sync] {0} ms", swClear.ElapsedMilliseconds);
                Check("clear keeps the old count until debounce", perfWin.CountText.Text == count11);
                Check("clear itself is cheap (<50ms)", swClear.ElapsedMilliseconds < 50);
                var swClearSettle = System.Diagnostics.Stopwatch.StartNew();
                while (perfWin.CountText.Text != count100 && swClearSettle.ElapsedMilliseconds < 3000)
                {
                    System.Windows.Forms.Application.DoEvents();
                }
                swClearSettle.Stop();
                Console.WriteLine("      [clear+filter settle] {0} ms (incl. 120ms debounce)", swClearSettle.ElapsedMilliseconds);
                Check("clear resets the list after debounce", perfWin.CountText.Text == count100);
                Check("clear keeps the stable view (no ItemsSource rebuild)",
                    ReferenceEquals(perfWin.AppsList.ItemsSource, sourceView));
                // Lazy editor: collapsed rows must NOT realize the Details/State
                // TextBoxes (the heavy part of the row template).
                Check("collapsed row has no editor realized", perfWin.Items[0].Editor == null);
                perfWin.Items[0].IsExpanded = true;
                Check("expanded row exposes the editor", perfWin.Items[0].Editor != null);
                perfWin.Items[0].IsExpanded = false;
                Check("collapsing tears the editor down", perfWin.Items[0].Editor == null);
                perfWin.Close();
            }
            catch (Exception ex)
            {
                Check("ManageAppsWindow hitch checks ran without exception", false);
                Console.WriteLine("      " + ex);
            }

            Console.WriteLine("LogService write cost (synchronous per-call flush regression):");
            // LogService used AutoFlush=true: every Log() call flushed to disk under a
            // global lock. With logLevel DEBUG (the dev machine's settings.json) that
            // put a synchronous disk write on EVERY keystroke (the per-keystroke
            // diagnostic) and let UI threads block behind background loggers' writes.
            // Writes are now buffered and flushed by a 1s timer / every N writes.
            try
            {
                LogService.Initialize();
                string prevLevel = SettingsService.Instance?.LogLevel ?? "INFO";
                LogService.SetMinLevel(LogLevel.DEBUG);
                var logSw = System.Diagnostics.Stopwatch.StartNew();
                for (int i = 0; i < 200; i++)
                    LogService.Log("perf: buffered write " + i, "DEBUG", "PerfTest");
                logSw.Stop();
                Console.WriteLine("      [200 DEBUG log writes] {0} ms", logSw.ElapsedMilliseconds);
                Check("LogService buffers writes (200 DEBUG lines < 100ms sync)",
                    logSw.ElapsedMilliseconds < 100);
                LogService.SetMinLevel(prevLevel);
            }
            catch (Exception ex)
            {
                Check("LogService buffered-write check ran without exception", false);
                Console.WriteLine("      " + ex);
            }

            Console.WriteLine("Real keyboard input cost (WPF input stack baseline):");
            // Programmatic .Text= measures 0ms, but the user's "freezing while
            // typing" is REAL keystrokes through the WPF input stack + pump. Send
            // real keys and measure — if this is ~1-3ms/key it is the framework
            // input baseline (every WPF TextBox), not our code.
            try
            {
                WpfHost.EnsureInitialized();
                var kWin = new ManageAppsWindow(new List<AppConfig>(), new HashSet<string>(),
                    new Dictionary<string, AppOverrideConfig>(), (p, e) => { }, (p, d, s) => { });
                kWin.Show();
                for (int i = 0; i < 30; i++) { System.Windows.Forms.Application.DoEvents(); Thread.Sleep(10); }
                kWin.Activate();
                kWin.SearchBox.Focus();
                System.Windows.Input.Keyboard.Focus(kWin.SearchBox);
                for (int i = 0; i < 20; i++) { System.Windows.Forms.Application.DoEvents(); Thread.Sleep(10); }
                // Real WM_CHAR messages straight to the window (no OS foreground
                // needed) exercise the WPF input stack per keystroke.
                IntPtr hWnd = new System.Windows.Interop.WindowInteropHelper(kWin).Handle;
                var swKeys = System.Diagnostics.Stopwatch.StartNew();
                foreach (char c in "abcdefgh")
                    geetRPCS.Utils.PInvoke.User32.SendMessage(hWnd, geetRPCS.Utils.PInvoke.User32.WM_CHAR, (IntPtr)c, IntPtr.Zero);
                swKeys.Stop();
                for (int i = 0; i < 5; i++) { System.Windows.Forms.Application.DoEvents(); Thread.Sleep(10); }
                Console.WriteLine("      [8 real WM_CHAR keystrokes] {0} ms", swKeys.ElapsedMilliseconds);
                Check("keystrokes reached the search box", kWin.SearchBox.Text == "abcdefgh");
                Check("8 real keystrokes processed quickly (<150ms)", swKeys.ElapsedMilliseconds < 150);
                // The per-keystroke handler must stay disk-free: with DEBUG logging
                // active, 30 text changes complete in a few ms. (The old per-keystroke
                // DEBUG log line did a synchronous disk flush on every key.)
                string prevLevel2 = SettingsService.Instance?.LogLevel ?? "INFO";
                LogService.SetMinLevel(LogLevel.DEBUG);
                var typeSw = System.Diagnostics.Stopwatch.StartNew();
                for (int i = 0; i < 30; i++) kWin.SearchBox.Text = "x" + i;
                typeSw.Stop();
                LogService.SetMinLevel(prevLevel2);
                Console.WriteLine("      [30 text-change handler runs @DEBUG] {0} ms", typeSw.ElapsedMilliseconds);
                Check("text-change handler is disk-free (30 changes < 20ms)",
                    typeSw.ElapsedMilliseconds < 20);
                kWin.Close();
                for (int i = 0; i < 10; i++) { System.Windows.Forms.Application.DoEvents(); Thread.Sleep(10); }
            }
            catch (Exception ex)
            {
                Check("real keyboard input test ran without exception", false);
                Console.WriteLine("      " + ex);
            }

            Console.WriteLine("Presence refresh runs off the UI thread (process enumeration):");
            // Root cause of the persistent "freeze while typing / on clear-X / on open":
            // the 5s witty timer called RefreshCurrentPresence() on the UI thread, which
            // runs Process.GetProcessesByName + MainWindowHandle — tens of ms of Win32
            // enumeration — while the modal ManageApps window was open (the modal frame
            // still pumps WM_TIMER, so the timer ticked mid-typing). The enumeration
            // now runs on a background thread with an in-flight guard.
            try
            {
                var enumSw = System.Diagnostics.Stopwatch.StartNew();
                using (var p = System.Diagnostics.Process.GetProcessesByName("explorer").FirstOrDefault()) { }
                enumSw.Stop();
                Console.WriteLine("      [Process.GetProcessesByName(explorer)] {0} ms", enumSw.ElapsedMilliseconds);

                var coord = new AppCoordinator(new FakeAppHost());
                typeof(AppCoordinator).GetField("_currentApp",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .SetValue(coord, "explorer");
                var callSw = System.Diagnostics.Stopwatch.StartNew();
                coord.RefreshCurrentPresence();
                callSw.Stop();
                int inFlight = (int)typeof(AppCoordinator).GetField("_refreshInFlight",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .GetValue(coord);
                Check("RefreshCurrentPresence returns without UI-thread enumeration (<20ms)",
                    callSw.ElapsedMilliseconds < 20);
                Check("refresh work runs on a background thread (in-flight right after call)",
                    inFlight == 1);
                var waitSw = System.Diagnostics.Stopwatch.StartNew();
                while (inFlight == 1 && waitSw.ElapsedMilliseconds < 5000)
                {
                    Thread.Sleep(10);
                    inFlight = (int)typeof(AppCoordinator).GetField("_refreshInFlight",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                        .GetValue(coord);
                }
                Check("background refresh completes", inFlight == 0);
            }
            catch (Exception ex)
            {
                Check("presence refresh off-UI-thread check ran without exception", false);
                Console.WriteLine("      " + ex);
            }

            Console.WriteLine("PresencePreviewWindow image cache FIFO bound:");
            // Long sessions with many asset switches used to grow the decoded
            // bitmap cache without bound; it now keeps the newest 16 entries.
            try
            {
                WpfHost.EnsureInitialized();
                var cacheWin = new PresencePreviewWindow(null);
                for (int i = 0; i < 24; i++)
                    cacheWin.CacheImage($"app_k{i}", new System.Windows.Media.Imaging.BitmapImage());
                Check("image cache bounded at 16 entries", cacheWin.CachedImageCount == 16);
                Check("oldest entries evicted", !cacheWin.HasCachedImage("app_k0") && !cacheWin.HasCachedImage("app_k7"));
                Check("newest entries kept", cacheWin.HasCachedImage("app_k23") && cacheWin.HasCachedImage("app_k8"));
                cacheWin.Close();
            }
            catch (Exception ex)
            {
                Check("image cache bound test ran without exception", false);
                Console.WriteLine("      " + ex);
            }

            Console.WriteLine("Combined WPF windows (all five open in one pump session):");
            // Open every converted WPF window at once and pump the WinForms loop,
            // then close them all: catches resource conflicts between windows.
            try
            {
                WpfHost.EnsureInitialized();

                var manageWin = new ManageAppsWindow(
                    new List<AppConfig>(),
                    new HashSet<string>(),
                    new Dictionary<string, AppOverrideConfig>(),
                    (proc, enabled) => { },
                    (proc, d, s) => { });
                manageWin.Show();

                var customWin = new CustomRichPresenceWindow(AppCoordinator.GetDefaultConfig());
                customWin.Show();

                var statsVm = new StatisticsViewModel
                {
                    Title = "TODAY'S USAGE",
                    EmptyMessage = "No data."
                };
                statsVm.Rows.Add(new StatsRow { Rank = 1, AppName = "Notepad", TimeText = "1h" });
                StatisticsWindow.Show(statsVm);

                var previewWin = new PresencePreviewWindow(null);
                previewWin.Show();

                var updateWin = UpdateDialog.CreateApps("9.9.9");
                updateWin.Show();

                // Always-on-top regressions: no window opened from the tray may
                // pin itself above everything by default (they used to cover
                // other apps and even the tray context menu).
                Check("preview window is NOT topmost by default", !previewWin.Topmost);
                Check("custom presence window is NOT topmost", !customWin.Topmost);

                for (int i = 0; i < 100; i++)
                {
                    System.Windows.Forms.Application.DoEvents();
                    Thread.Sleep(10);
                }

                Check("all five windows loaded simultaneously",
                    manageWin.IsLoaded && customWin.IsLoaded &&
                    StatisticsWindow.Instance?.IsLoaded == true &&
                    previewWin.IsLoaded && updateWin.IsLoaded);
                Check("statistics rows list is virtualized",
                    StatisticsWindow.Instance != null &&
                    System.Windows.Controls.VirtualizingStackPanel.GetIsVirtualizing(StatisticsWindow.Instance.RowsList));

                // Close all (reverse order) and pump again
                updateWin.Close();
                previewWin.Close();
                StatisticsWindow.Instance?.Close();
                customWin.Close();
                manageWin.Close();
                for (int i = 0; i < 50; i++)
                {
                    System.Windows.Forms.Application.DoEvents();
                    Thread.Sleep(10);
                }
                Check("statistics singleton cleaned up after close", StatisticsWindow.Instance == null);
                Check("no window remains loaded after closing all",
                    !manageWin.IsLoaded && !customWin.IsLoaded &&
                    !previewWin.IsLoaded && !updateWin.IsLoaded);
            }
            catch (Exception ex)
            {
                Check("combined WPF smoke test ran without exception", false);
                Console.WriteLine("      " + ex);
            }

            Console.WriteLine();
            if (_failures == 0)
            {
                Console.WriteLine("ALL TESTS PASSED");
                return 0;
            }
            Console.WriteLine($"{_failures} TEST(S) FAILED");
            return 1;
        }

        // --- Fakes for the TrayMenuController end-to-end test ---
        private sealed class FakeAppHost : IAppHost
        {
            public readonly List<string> Called = new List<string>();
            public void ShowBalloon(string title, string message, System.Windows.Forms.ToolTipIcon icon) => Called.Add("balloon");
            public void PublishPresence(RichPresence presence) => Called.Add("publish");
            public void PreviewPausedState() => Called.Add("paused");
            public void PreviewIdleState() => Called.Add("idle");
            public void RefreshTrayPresentation() => Called.Add("tray");
            public void RebuildTrayMenu() => Called.Add("rebuild");
            public void AnimateOnSwitch() => Called.Add("animate");
        }

        private sealed class FakeStats : IStatsCoordinator
        {
            public readonly List<string> Called = new List<string>();
            public void ShowToday() => Called.Add("today");
            public void ShowWeek() => Called.Add("week");
            public void ShowMonth() => Called.Add("month");
            public void ShowAllTime() => Called.Add("alltime");
            public void ExportAsync(string format) => Called.Add("export:" + format);
            public System.Threading.Tasks.Task ResetAsync()
            { Called.Add("reset"); return System.Threading.Tasks.Task.CompletedTask; }
        }

        private sealed class FakeCoordinator : ITrayCoordinator
        {
            public bool IsPaused { get; set; }
            public bool PrivateMode { get; set; }
            public Config Config { get; set; } = new Config();
            public IStatsCoordinator Stats { get; set; } = new FakeStats();
            public readonly List<string> Called = new List<string>();
            public void TogglePause() => Called.Add("TogglePause");
            public void TogglePrivateMode() => Called.Add("TogglePrivateMode");
            public System.Threading.Tasks.Task SetMouseEnergyAsync(bool enabled)
            { Called.Add("SetMouseEnergy:" + enabled); return System.Threading.Tasks.Task.CompletedTask; }
            public System.Threading.Tasks.Task SetTrayAnimationAsync(bool enabled)
            { Called.Add("SetTrayAnimation:" + enabled); return System.Threading.Tasks.Task.CompletedTask; }
            public System.Threading.Tasks.Task ToggleTelemetryAsync(bool enabled)
            { Called.Add("ToggleTelemetry:" + enabled); return System.Threading.Tasks.Task.CompletedTask; }
            public bool SaveConfig(Config cfg)
            { Called.Add("SaveConfig"); return true; }
            public void ReloadConfig() => Called.Add("ReloadConfig");
        }

        private sealed class FakeShell : ITrayShell
        {
            public bool IsManageAppsOpen { get; set; }
            public bool IsPreviewVisible { get; set; }
            public bool IsStatsOpen { get; set; }
            public readonly List<string> Called = new List<string>();
            public void ToggleManageAppsVisibility() => Called.Add("ToggleManageApps");
            public void TogglePreviewVisibility() => Called.Add("TogglePreview");
            public void RebuildTrayMenuDeferred() => Called.Add("RebuildDeferred");
            public void CheckForUpdatesFromMenu() => Called.Add("CheckUpdates");
            public void OpenLog() => Called.Add("OpenLog");
            public void ExitApp() => Called.Add("Exit");
            public void ShowBalloonTip(string title, string text, System.Windows.Forms.ToolTipIcon icon)
                => Called.Add("Balloon");
        }

        // Walk up from the current directory to locate the repo's apps.json, so the
        // check works whether tests run from the repo root or from the bin folder.
        private static string FindAppsJson()
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            for (int i = 0; i < 6 && dir != null; i++)
            {
                string candidate = Path.Combine(dir.FullName, "apps.json");
                if (File.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }
            return null;
        }

        // Walk up from the current directory to locate the repo's Languages folder.
        private static string FindLanguagesDir()
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            for (int i = 0; i < 6 && dir != null; i++)
            {
                string candidate = Path.Combine(dir.FullName, "Languages");
                if (Directory.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }
            return null;
        }
    }
}
