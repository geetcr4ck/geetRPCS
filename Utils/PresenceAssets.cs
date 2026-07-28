/**
 * geetRPCS - Presence Assets
 * Manages image assets for Rich Presence
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
using System.Linq;
using DiscordRPC;
using geetRPCS.Services;

namespace geetRPCS.Utils
{
    internal static class PresenceAssets
    {
        public static void Reload() => AppConfigManager.Reload();
        public static Assets ForApp(string processName, Assets fallback)
        {
            var app = AppConfigManager.Apps.FirstOrDefault(a => a.Process.Equals(processName, StringComparison.OrdinalIgnoreCase));
            var assets = new Assets
            {
                LargeImageKey = app?.LargeKey ?? fallback.LargeImageKey,
                LargeImageText = app?.LargeText ?? fallback.LargeImageText,
                SmallImageKey = "geetrpcs-small", // "Patented" key
                SmallImageText = "geetRPCS"       // "Patented" text
            };
            return assets;
        }
    }
}
