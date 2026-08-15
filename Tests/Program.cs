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
using System.IO;
using System.Linq;
using System.Text.Json;
using geetRPCS.Models;
using geetRPCS.Services;

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

        private static int Main()
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

            Console.WriteLine();
            if (_failures == 0)
            {
                Console.WriteLine("ALL TESTS PASSED");
                return 0;
            }
            Console.WriteLine($"{_failures} TEST(S) FAILED");
            return 1;
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
