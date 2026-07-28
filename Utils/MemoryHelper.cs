/**
 * geetRPCS - Memory Utility
 * Utility for optimizing application memory usage
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
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace geetRPCS.Utils
{
    internal static class MemoryHelper
    {
        [DllImport("psapi.dll")]
        private static extern bool EmptyWorkingSet(IntPtr hProcess);
        public static void TrimMemory()
        {
            try
            {
                GC.Collect(2, GCCollectionMode.Optimized, false);
                GC.WaitForPendingFinalizers();
                using (var currentProcess = Process.GetCurrentProcess())
                {
                    EmptyWorkingSet(currentProcess.Handle);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TrimMemory error: {ex.Message}");
            }
        }
    }
}
