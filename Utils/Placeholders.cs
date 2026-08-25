/**
 * geetRPCS - Text Placeholder
 * Handles string placeholder expansion for RPC text
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
using System.Runtime.InteropServices;
using System.Text;
using geetRPCS.Services;

namespace geetRPCS.Utils
{
    internal static class Placeholders
    {
        public static void Reload() => AppConfigManager.Reload();
        public static string GetAppName(string processName)
        {
            var app = AppConfigManager.FindExact(processName);
            return app?.AppName ?? processName;
        }
        public static string GetWindowTitle(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return "";
            int len = GetWindowTextLengthW(hWnd);
            if (len <= 0) return "";
            var sb = new StringBuilder(len + 1);
            GetWindowTextW(hWnd, sb, sb.Capacity);
            string title = sb.ToString();
            if (string.IsNullOrWhiteSpace(title) || title.Length <= 3) return "";
            return title;
        }
        // ponytail: (hwnd, title)-keyed cache, 10s TTL, clear-at-16; add LRU if
        // many browser windows are ever tracked at once.
        private sealed class AccNameEntry
        {
            public string Title;
            public string Name;
            public long Tick;
        }
        private static readonly object _accNameGate = new object();
        private static readonly Dictionary<IntPtr, AccNameEntry> _accNameCache = new Dictionary<IntPtr, AccNameEntry>();
        private const long AccNameCacheTtlMs = 10_000;
        private const int AccNameCacheCap = 16;

        /// <summary>Accessible window name for private-mode detection. The MSAA
        /// round-trip is a cross-process COM call (tens of ms under load), and it
        /// runs on every presence build (~5s while a browser is tracked), so the
        /// result is cached per HWND + native title. A 10s TTL covers the rare
        /// annotation flip that leaves the native title unchanged.</summary>
        public static string GetAccessibleWindowName(IntPtr hWnd, string nativeTitle)
        {
            if (hWnd == IntPtr.Zero) return "";
            long now = Environment.TickCount64;
            lock (_accNameGate)
            {
                if (_accNameCache.TryGetValue(hWnd, out var hit)
                    && hit.Title == nativeTitle
                    && now - hit.Tick < AccNameCacheTtlMs)
                    return hit.Name;
            }
            string name = QueryAccessibleWindowName(hWnd);
            lock (_accNameGate)
            {
                if (_accNameCache.Count >= AccNameCacheCap) _accNameCache.Clear();
                _accNameCache[hWnd] = new AccNameEntry { Title = nativeTitle, Name = name, Tick = now };
            }
            return name;
        }

        private static string QueryAccessibleWindowName(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return "";

            // Chromium exposes the private-mode annotation through its accessible
            // window name even when GetWindowTextW contains only the page title.
            uint[] objectIds = { OBJID_CLIENT, OBJID_WINDOW };
            foreach (uint objectId in objectIds)
            {
                IAccessible accessible = null;
                try
                {
                    Guid iid = IAccessibleGuid;
                    int result = AccessibleObjectFromWindow(hWnd, objectId, ref iid, out accessible);
                    if (result >= 0 && accessible != null)
                    {
                        string name = accessible.get_accName(CHILDID_SELF);
                        if (!string.IsNullOrWhiteSpace(name)) return name;
                    }
                }
                catch
                {
                    // Try the next object ID, then fall back to the native title.
                }
                finally
                {
                    if (accessible != null && Marshal.IsComObject(accessible))
                        Marshal.ReleaseComObject(accessible);
                }
            }

            return "";
        }

        private const uint OBJID_WINDOW = 0;
        private const uint OBJID_CLIENT = 0xFFFFFFFC;
        private const int CHILDID_SELF = 0;
        private static readonly Guid IAccessibleGuid = new Guid("618736e0-3c3d-11cf-810c-00aa00389b71");

        [DllImport("oleacc.dll")]
        private static extern int AccessibleObjectFromWindow(
            IntPtr hWnd,
            uint dwObjectId,
            ref Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out IAccessible ppvObject);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowTextLengthW(IntPtr hWnd);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowTextW(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        /// <summary>
        /// Minimal MSAA IAccessible interop. The .NET Framework "Accessibility"
        /// assembly does not exist on .NET 8, so only the vtable prefix up to
        /// get_accName is declared here (IAccessible derives from IDispatch).
        /// </summary>
        [ComImport]
        [Guid("618736E0-3C3D-11CF-810C-00AA00389B71")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAccessible
        {
            // IDispatch
            void GetTypeInfoCount(out uint pctinfo);
            void GetTypeInfo(uint iTInfo, uint lcid, out IntPtr ppTInfo);
            void GetIDsOfNames(ref Guid riid,
                [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPWStr, SizeParamIndex = 2)] string[] rgszNames,
                uint cNames, uint lcid, [Out] IntPtr rgDispId);
            void Invoke(uint dispIdMember, ref Guid riid, uint lcid, ushort wFlags,
                IntPtr pDispParams, IntPtr pVarResult, IntPtr pExcepInfo, IntPtr puArgErr);
            // IAccessible
            [return: MarshalAs(UnmanagedType.Interface)] object accParent();
            int accChildCount();
            [return: MarshalAs(UnmanagedType.Interface)] object accChild(object varChild);
            [return: MarshalAs(UnmanagedType.BStr)] string get_accName(object varChild);
        }
    }
}
