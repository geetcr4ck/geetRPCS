/**
 * geetRPCS - App Version
 * Single source of truth for the running application version. The version number
 * itself is defined once in the project file (geetRPCS.csproj -> <Version>) and
 * flows into the generated assembly metadata, which is read here.
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

namespace geetRPCS.Utils
{
    internal static class AppVersion
    {
        // Short form, e.g. "1.4.0"
        public static string VersionText
        {
            get
            {
                var version = typeof(AppVersion).Assembly.GetName().Version;
                return version != null ? version.ToString(3) : "0.0.0";
            }
        }

        // Full display string, e.g. "geetRPCS v1.4.0"
        public static string DisplayName => $"geetRPCS v{VersionText}";
    }
}