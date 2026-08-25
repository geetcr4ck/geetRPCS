/**
 * geetRPCS - Presence Builder
 * Builds RichPresence payloads (idle/active) from the loaded config, the app
 * database, placeholder expansion, narrative texts and mouse-energy state.
 * Kept UI-free so RPC payload assembly is testable and decoupled from the host.
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
using System.Linq;
using DiscordRPC;
using geetRPCS.Models;
using geetRPCS.Utils;

namespace geetRPCS.Services
{
    internal sealed class PresenceBuilder
    {
        // Language-neutral redaction shown instead of the window title whenever
        // private mode hides it (manual toggle or auto-detected browser window).
        private const string HiddenTitle = "**********";

        public Config Config { get; set; }
        public bool PrivateMode { get; set; }

        public PresenceBuilder(Config config)
        {
            Config = config;
        }

        /// <summary>Builds the idle (no app active) presence from config.json.</summary>
        public RichPresence BuildIdlePresence(string energyState = null)
        {
            string details = string.IsNullOrWhiteSpace(Config.Discord?.Details) ? LanguageManager.Current.Idling : Config.Discord.Details;
            string state = string.IsNullOrWhiteSpace(Config.Discord?.State) ? LanguageManager.Current.Ready : Config.Discord.State;
            if (!string.IsNullOrEmpty(energyState)) state = $"{state} | {energyState}";
            var presence = new RichPresence
            {
                Details = details,
                State = state,
                Assets = GetDefaultAssets()
            };
            var buttons = BuildButtons(Config.Discord?.Buttons?.Select(b => (b.Label, b.Url)) ?? Enumerable.Empty<(string, string)>());
            if (buttons != null && buttons.Length > 0) presence.Buttons = buttons;
            return presence;
        }

        /// <summary>Builds the active presence for a detected application.</summary>
        public RichPresence BuildAppPresence(string processName, IntPtr hWnd, DateTime started, string energyState = null)
        {
            string details = ReplacePlaceholders(GetCustomDetailsForApp(processName), processName, hWnd);
            string state = ReplacePlaceholders(GetCustomStateForApp(processName), processName, hWnd);
            if (!string.IsNullOrEmpty(energyState)) state = $"{state} | {energyState}";

            var presence = new RichPresence
            {
                Details = details,
                State = state,
                Assets = PresenceAssets.ForApp(processName, GetDefaultAssets())
            };

            // Effective entry = apps.json (or a custom app) with the user's
            // override applied: timestamps/buttons respect the override here.
            var appConfig = AppConfigManager.GetEffectiveApp(processName);
            if (appConfig?.ShowTimestamps ?? Config.Discord?.ShowTimestamps ?? true)
                presence.Timestamps = new Timestamps { Start = started };

            var appButtons = BuildButtons(appConfig?.Buttons?.Select(b => (b.Label, b.Url)) ?? Enumerable.Empty<(string, string)>());
            if (appButtons != null && appButtons.Length > 0) presence.Buttons = appButtons;
            return presence;
        }

        /// <summary>Template resolution for the detail line (override &gt; app &gt; active).</summary>
        public string GetCustomDetailsForApp(string processName)
        {
            if (SettingsService.Instance.AppOverrides.TryGetValue(processName, out var ov) && !string.IsNullOrWhiteSpace(ov.Details))
                return ov.Details;
            var app = AppConfigManager.FindExact(processName);
            if (!string.IsNullOrWhiteSpace(app?.CustomDetails)) return app.CustomDetails;
            return Config.Discord?.ActiveDetails ?? "";
        }

        /// <summary>Trampled state line (override &gt; config active state).</summary>
        public string GetCustomStateForApp(string processName)
        {
            if (SettingsService.Instance.AppOverrides.TryGetValue(processName, out var ov) && !string.IsNullOrWhiteSpace(ov.State))
                return ov.State;
            return Config.Discord?.ActiveState ?? "";
        }

        public Assets GetDefaultAssets() => new Assets
        {
            LargeImageKey = Config.Discord?.Assets?.LargeImageKey ?? "",
            LargeImageText = Config.Discord?.Assets?.LargeImageText ?? "",
            SmallImageKey = Config.Discord?.Assets?.SmallImageKey ?? "",
            SmallImageText = Config.Discord?.Assets?.SmallImageText ?? ""
        };

        /// <summary>Validates and caps buttons (Discord allows at most 2, label &lt;= 32 chars, https only).</summary>
        private DiscordRPC.Button[] BuildButtons(IEnumerable<(string Label, string Url)> source)
        {
            var valid = source
                .Where(b => !string.IsNullOrEmpty(b.Label)
                            && !string.IsNullOrEmpty(b.Url)
                            && IsValidUrl(b.Url)
                            && b.Label.Length <= 32)
                .Take(2)
                .Select(b => new DiscordRPC.Button { Label = b.Label, Url = b.Url })
                .ToArray();
            return valid.Length > 0 ? valid : null;
        }

        public static bool IsValidUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return false;
            return Uri.TryCreate(url, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        public string ReplacePlaceholders(string format, string processName, IntPtr hWnd)
        {
            if (string.IsNullOrEmpty(format)) return format ?? "";
            try
            {
                string appName = Placeholders.GetAppName(processName);
                string title = Placeholders.GetWindowTitle(hWnd);
                string accessibleWindowName = PrivateBrowsingDetector.IsSupportedBrowser(processName)
                    ? Placeholders.GetAccessibleWindowName(hWnd, title)
                    : "";
                bool shouldHideTitle = PrivateMode
                    || PrivateBrowsingDetector.IsPrivateWindow(processName, title, accessibleWindowName);
                if (shouldHideTitle)
                    title = HiddenTitle;
                else if (string.IsNullOrEmpty(title) || title.Length <= 3)
                    title = LanguageManager.Current.Working;
                string wittyText = NarrativeService.GetForApp(processName);
                return format.Replace("{process_name}", processName ?? "")
                    .Replace("{app_name}", appName ?? processName ?? "")
                    .Replace("{window_title}", title)
                    .Replace("{witty_text}", wittyText);
            }
            catch (Exception ex)
            {
                LogService.Log($"ReplacePlaceholders error: {ex.Message}", "ERROR", "PresenceBuilder");
                return format;
            }
        }
    }
}