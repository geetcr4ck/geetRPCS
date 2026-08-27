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
using geetRPCS.Services;

namespace geetRPCS.Utils
{
    internal static class MemoryHelper
    {
        [DllImport("psapi.dll")]
        private static extern bool EmptyWorkingSet(IntPtr hProcess);
        private static int _trimInProgress;
        public static void TrimMemory()
        {
            if (System.Threading.Interlocked.Exchange(ref _trimInProgress, 1) != 0) return;
            try
            {
                long beforeMb = Environment.WorkingSet / (1024 * 1024);
                // Forced + blocking: every call site runs on a background thread
                // (Task.Run), so pay the blocking Gen2 collection here and
                // actually release dead window trees. GCCollectionMode.Optimized
                // is frequently skipped by the runtime, which left the managed
                // heap unshrunk while only the working set was paged out.
                GC.Collect(2, GCCollectionMode.Forced, true);
                GC.WaitForPendingFinalizers();
                GC.Collect(2, GCCollectionMode.Forced, true);
                using (var currentProcess = Process.GetCurrentProcess())
                {
                    EmptyWorkingSet(currentProcess.Handle);
                }
                LogService.Log(
                    $"TrimMemory: working set {beforeMb}MB -> {Environment.WorkingSet / (1024 * 1024)}MB",
                    "DEBUG", "MemoryHelper");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TrimMemory error: {ex.Message}");
            }
            finally
            {
                System.Threading.Volatile.Write(ref _trimInProgress, 0);
            }
        }
    }
}
