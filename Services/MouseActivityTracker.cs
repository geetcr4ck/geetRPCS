/**
 * geetRPCS - Activity Tracker
 * Tracks mouse activity for energy-weighted presence
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
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace geetRPCS.Services
{
    public class MouseActivityTracker : IDisposable
    {
        // --- Definitions ---
        #region Constants & Enums
        private const double HIGH_ENERGY_VELOCITY = 1200;   // Vigorous mouse movement
        private const double MEDIUM_ENERGY_VELOCITY = 400;  // Active work
        private const double LOW_ENERGY_VELOCITY = 100;     // Casual browsing
        private const int IDLE_TIMEOUT_SECONDS = 30;
        private const int HIGH_CLICKS_PER_MINUTE = 80;      // Intense clicking (gaming, editing)
        private const int MEDIUM_CLICKS_PER_MINUTE = 25;    // Regular clicking
        private const int STATE_STABILITY_THRESHOLD = 4;    // ~2 seconds (4 × 500ms analysis intervals)
        public enum EnergyLevel { Sleeping, Relaxing, Normal, Focused, Rush }
        #endregion
        #region ----- Fields -----

        private IntPtr _mouseHookHandle = IntPtr.Zero;
        private LowLevelMouseProc _mouseProc;
        private bool _isHookInstalled, _isRunning, _isEnabled = true;

        private readonly object _accumLock = new object();
        private double _pendingDistance;
        private int _pendingClicks;
        private Point _lastHookPosition;

        private double _averageVelocity;
        private int _clicksPerMinute;
        private DateTime _lastMoveTime, _lastClickTime;
        private Thread _analysisThread;
        private EnergyLevel _currentEnergy = EnergyLevel.Normal;
        private EnergyLevel _pendingEnergy = EnergyLevel.Normal;
        private int _stabilityCounter = 0;

        private readonly object _readLock = new object();

        public event Action<EnergyLevel, double, int> OnEnergyChanged;
        #endregion

        // --- Low Level Interaction ---
        #region Win32 API
        private const int WH_MOUSE_LL = 14;
        private const int WM_MOUSEMOVE = 0x0200;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_MBUTTONDOWN = 0x0207;
        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int x, y; }
        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData, flags, time;
            public IntPtr dwExtraInfo;
        }
        #endregion
        #region ----- Constructor -----
        public MouseActivityTracker()
        {
            _lastHookPosition = Point.Empty;
            _lastMoveTime = _lastClickTime = DateTime.UtcNow;
            _pendingDistance = 0;
            _pendingClicks = 0;
            _clicksPerMinute = 0;
            _averageVelocity = 0;
        }
        #endregion
        #region ----- Public Methods -----
        public void Start()
        {
            if (_isRunning) return;
            try
            {
                _isRunning = true;
                InstallHook();
                StartAnalysisThread();
                Log("Mouse Activity Tracker started", "INFO");
            }
            catch (Exception ex)
            {
                Log($"Failed to start Mouse Activity Tracker: {ex.Message}", "ERROR");
                _isRunning = false;
            }
        }
        public void Stop()
        {
            _isRunning = false;
            UninstallHook();
            Log("Mouse Activity Tracker stopped", "INFO");
        }
        public void SetEnabled(bool enabled)
        {
            _isEnabled = enabled;
            if (!enabled)
            {
                lock (_readLock) _currentEnergy = EnergyLevel.Normal;
            }
            Log($"Mouse Activity Tracker enabled: {enabled}", "INFO");
        }
        public EnergyLevel GetCurrentEnergy()
        {
            if (!_isEnabled) return EnergyLevel.Normal;
            lock (_readLock) { return _currentEnergy; }
        }
        public string GetEnergyEmoji() => GetCurrentEnergy() switch
        {
            EnergyLevel.Sleeping => "💤",
            EnergyLevel.Relaxing => "☕",
            EnergyLevel.Normal => "🎯",
            EnergyLevel.Focused => "🔥",
            EnergyLevel.Rush => "⚡",
            _ => "🎯"
        };
        public string GetEnergyText() => GetCurrentEnergy() switch
        {
            EnergyLevel.Sleeping => LanguageManager.Current.EnergySleeping ?? "Zzz...",
            EnergyLevel.Relaxing => LanguageManager.Current.EnergyRelaxing ?? "Relaxing",
            EnergyLevel.Normal => LanguageManager.Current.EnergyNormal ?? "Working",
            EnergyLevel.Focused => LanguageManager.Current.EnergyFocused ?? "Focused",
            EnergyLevel.Rush => LanguageManager.Current.EnergyRush ?? "Rush Mode!",
            _ => "Working"
        };
        public string GetEnergyStateText()
        {
            if (!_isEnabled) return null;
            return $"{GetEnergyEmoji()} {GetEnergyText()}";
        }
        public (double velocity, int clicksPerMinute, EnergyLevel energy) GetStats()
        {
            lock (_readLock) { return (_averageVelocity, _clicksPerMinute, _currentEnergy); }
        }
        #endregion
        #region ----- Hook Management -----
        private void InstallHook()
        {
            if (_isHookInstalled) return;
            _mouseProc = MouseHookCallback;
            using (var process = Process.GetCurrentProcess())
            using (var module = process.MainModule)
            {
                _mouseHookHandle = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc,
                    GetModuleHandle(module.ModuleName), 0);
            }
            if (_mouseHookHandle == IntPtr.Zero)
            {
                int error = Marshal.GetLastWin32Error();
                Log($"Failed to install mouse hook. Error code: {error}", "ERROR");
            }
            else
            {
                _isHookInstalled = true;
                Log("Mouse hook installed successfully", "INFO");
            }
        }
        private void UninstallHook()
        {
            if (!_isHookInstalled || _mouseHookHandle == IntPtr.Zero) return;
            UnhookWindowsHookEx(_mouseHookHandle);
            _mouseHookHandle = IntPtr.Zero;
            _isHookInstalled = false;
            Log("Mouse hook uninstalled", "INFO");
        }

        private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && _isEnabled)
            {
                try
                {
                    int message = wParam.ToInt32();
                    if (message == WM_MOUSEMOVE)
                    {
                        int x = Marshal.ReadInt32(lParam);
                        int y = Marshal.ReadInt32(lParam, 4);

                        if (!_lastHookPosition.IsEmpty)
                        {
                            int dx = x - _lastHookPosition.X;
                            int dy = y - _lastHookPosition.Y;
                            double dist = Math.Sqrt(dx * dx + dy * dy);

                            lock (_accumLock)
                            {
                                _pendingDistance += dist;
                            }
                        }
                        _lastHookPosition = new Point(x, y);
                    }
                    else if (message == WM_LBUTTONDOWN || message == WM_RBUTTONDOWN || message == WM_MBUTTONDOWN)
                    {
                         lock (_accumLock)
                         {
                             _pendingClicks++;
                         }
                    }
                }
                catch { }
            }
            return CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);
        }
        #endregion
        #region ----- Data Processing -----
        private void StartAnalysisThread()
        {
            _analysisThread = new Thread(AnalysisLoop) { IsBackground = true, Name = "MouseActivityAnalyzer" };
            _analysisThread.Start();
        }
        private void AnalysisLoop()
        {
            const int VELOCITY_BUFFER_SIZE = 10; // 5 seconds average (0.5s * 10)
            const int CLICK_BUFFER_SIZE = 120;   // 60 seconds average (0.5s * 120)

            double[] velocityBuffer = new double[VELOCITY_BUFFER_SIZE];
            int[] clickBuffer = new int[CLICK_BUFFER_SIZE];
            int vIndex = 0;
            int cIndex = 0;

            while (_isRunning)
            {
                try
                {
                    Thread.Sleep(500);
                    if (!_isEnabled) continue;

                    double snappedDistance;
                    int snappedClicks;

                    lock (_accumLock)
                    {
                        snappedDistance = _pendingDistance;
                        snappedClicks = _pendingClicks;
                        _pendingDistance = 0;
                        _pendingClicks = 0;
                    }

                    DateTime now = DateTime.UtcNow;

                    if (snappedDistance > 0) _lastMoveTime = now;
                    if (snappedClicks > 0) _lastClickTime = now;

                    // Velocity (px/s) - interval is approx 0.5s
                    double currentVelocity = snappedDistance / 0.5;
                    velocityBuffer[vIndex] = currentVelocity;
                    vIndex = (vIndex + 1) % VELOCITY_BUFFER_SIZE;

                    double avgVelocity = 0;
                    foreach (var v in velocityBuffer) avgVelocity += v;
                    avgVelocity /= VELOCITY_BUFFER_SIZE;

                    // Clicks (CPM) - stored in 0.5s buckets
                    clickBuffer[cIndex] = snappedClicks;
                    cIndex = (cIndex + 1) % CLICK_BUFFER_SIZE;

                    int totalClicksInMinute = 0;
                    foreach (var c in clickBuffer) totalClicksInMinute += c;

                    // Update State
                    EnergyLevel newEnergy;
                    TimeSpan timeSinceLastMove = now - _lastMoveTime;
                    TimeSpan timeSinceLastClick = now - _lastClickTime;

                    if (timeSinceLastMove.TotalSeconds > IDLE_TIMEOUT_SECONDS &&
                        timeSinceLastClick.TotalSeconds > IDLE_TIMEOUT_SECONDS)
                    {
                        newEnergy = EnergyLevel.Sleeping;
                    }
                    else
                    {
                        newEnergy = CalculateEnergyLevel(avgVelocity, totalClicksInMinute);
                    }

                    // Update public properties with hysteresis for stability
                    lock (_readLock)
                    {
                        _averageVelocity = avgVelocity;
                        _clicksPerMinute = totalClicksInMinute;

                        // State stability hysteresis: require consistent state for ~2 seconds before changing
                        if (newEnergy == _pendingEnergy)
                        {
                            _stabilityCounter++;
                            if (_stabilityCounter >= STATE_STABILITY_THRESHOLD && newEnergy != _currentEnergy)
                            {
                                _currentEnergy = newEnergy;
                                try { OnEnergyChanged?.Invoke(newEnergy, avgVelocity, totalClicksInMinute); } catch {}
                                Log($"Energy: {newEnergy} (V: {avgVelocity:F0}, CPM: {totalClicksInMinute})", "INFO");
                            }
                        }
                        else
                        {
                            // New pending state, reset counter
                            _pendingEnergy = newEnergy;
                            _stabilityCounter = 1;
                        }
                    }
                }
                catch (Exception ex) { Log($"Analysis loop error: {ex.Message}", "ERROR"); }
            }
        }
        private EnergyLevel CalculateEnergyLevel(double velocity, int clicksPerMinute)
        {
            int velocityScore = velocity >= HIGH_ENERGY_VELOCITY ? 4 :
                               velocity >= MEDIUM_ENERGY_VELOCITY ? 2 :
                               velocity >= LOW_ENERGY_VELOCITY ? 1 : 0;
            int clickScore = clicksPerMinute >= HIGH_CLICKS_PER_MINUTE ? 4 :
                            clicksPerMinute >= MEDIUM_CLICKS_PER_MINUTE ? 2 :
                            clicksPerMinute > 0 ? 1 : 0;
            int totalScore = velocityScore + clickScore;
            return totalScore switch
            {
                >= 6 => EnergyLevel.Rush,
                >= 4 => EnergyLevel.Focused,
                >= 2 => EnergyLevel.Normal,
                _ => EnergyLevel.Relaxing
            };
        }
        #endregion
        #region ----- Helpers -----
        private static void Log(string message, string level = "INFO")
        {
            // Delegate to centralized LogService
            LogService.Log(message, level, "MouseTracker");
        }
        #endregion
        #region ----- Dispose -----
        public void Dispose()
        {
            Stop();
            _analysisThread = null;
        }
        #endregion
    }
}
