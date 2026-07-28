/**
 * geetRPCS - Tray Animator
 * Manages animated tray icons for state changes
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
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Threading;
namespace geetRPCS.Services
{
    public class TrayIconAnimator : IDisposable
    {
        private readonly NotifyIcon _trayIcon;
        private readonly Icon _originalIcon;
        private readonly Bitmap _cachedBitmap;
        private System.Threading.Timer _animationTimer;
        private int _animationFrame;
        private bool _isAnimating;
        private static Action<string> _logAction;
        private readonly Control _marshalForm; // Passed from Program
        private const int AnimationDuration = 800; // milliseconds
        private const int FrameCount = 12; // Increased for smoother easing
        public TrayIconAnimator(NotifyIcon trayIcon, string iconPath, Control marshalForm, Action<string> logAction = null)
        {
            _trayIcon = trayIcon ?? throw new ArgumentNullException(nameof(trayIcon));
            if (iconPath == null) throw new ArgumentNullException(nameof(iconPath));
            _originalIcon = new Icon(iconPath);
            _cachedBitmap = _originalIcon.ToBitmap();
            _logAction = logAction;
            _marshalForm = marshalForm;
            Log("TrayIconAnimator initialized with Eased Rotation (Leak-Proof)");
        }
        public void AnimateOnSwitch()
        {
            try
            {
                if (_isAnimating)
                {
                    Log("Animation already running, resetting...");
                    _animationFrame = 0;
                    return;
                }
                Log($"Starting eased rotation - Duration: {AnimationDuration}ms, Frames: {FrameCount}");
                _isAnimating = true;
                _animationFrame = 0;
                int interval = AnimationDuration / FrameCount;
                _animationTimer = new System.Threading.Timer(
                    OnAnimationTick,
                    null,
                    interval,
                    interval
                );
            }
            catch (Exception ex)
            {
                Log($"AnimateOnSwitch error: {ex.Message}");
            }
        }
        public void Stop()
        {
            _animationTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            _animationTimer?.Dispose();
            _animationTimer = null;
            _isAnimating = false;
            _animationFrame = 0;
            if (_trayIcon != null && _originalIcon != null)
            {
                try
                {
                    if (_marshalForm != null && _marshalForm.InvokeRequired)
                    {
                        _marshalForm.Invoke(new Action(RestoreOriginalIcon));
                    }
                    else
                    {
                        RestoreOriginalIcon();
                    }
                }
                catch { }
            }
        }
        private void RestoreOriginalIcon()
        {
            var oldIcon = _trayIcon.Icon;
            _trayIcon.Icon = _originalIcon;
            if (oldIcon != null && oldIcon != _originalIcon)
            {
                try { DestroyIcon(oldIcon.Handle); oldIcon.Dispose(); } catch { }
            }
        }
        private void OnAnimationTick(object state)
        {
            try
            {
                if (_animationFrame >= FrameCount)
                {
                    Stop();
                    return;
                }
                float t = (float)(_animationFrame + 1) / FrameCount;
                float easedT = t < 0.5f ? 2 * t * t : 1 - (float)Math.Pow(-2 * t + 2, 2) / 2;
                float brightnessBoost = 4 * t * (1 - t) * 0.45f;
                float angle = easedT * 360f;
                Icon animatedIcon = CreateAnimatedIcon(angle, brightnessBoost);
                if (animatedIcon != null && _trayIcon != null)
                {
                    if (_marshalForm != null && _marshalForm.InvokeRequired)
                    {
                        _marshalForm.Invoke(new Action(() => UpdateTrayIcon(animatedIcon)));
                    }
                    else
                    {
                        UpdateTrayIcon(animatedIcon);
                    }
                }
                _animationFrame++;
            }
            catch (Exception ex)
            {
                Log($"Animation tick error: {ex.Message}");
                Stop();
            }
        }
        private void UpdateTrayIcon(Icon newIcon)
        {
            var oldIcon = _trayIcon.Icon;
            _trayIcon.Icon = newIcon;
            if (oldIcon != null && oldIcon != _originalIcon)
            {
                try { DestroyIcon(oldIcon.Handle); oldIcon.Dispose(); } catch { }
            }
        }
        private Icon CreateAnimatedIcon(float angle, float brightness)
        {
            try
            {
                var bmp = _cachedBitmap;
                {
                    Bitmap animatedBmp = new Bitmap(bmp.Width, bmp.Height);
                    using (Graphics g = Graphics.FromImage(animatedBmp))
                    {
                        g.Clear(Color.Transparent);
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                        ColorMatrix colorMatrix = new ColorMatrix(new float[][]
                        {
                            new float[] {1, 0, 0, 0, 0},
                            new float[] {0, 1, 0, 0, 0},
                            new float[] {0, 0, 1, 0, 0},
                            new float[] {0, 0, 0, 1, 0},
                            new float[] {brightness, brightness, brightness, 0, 1}
                        });
                        using (ImageAttributes attributes = new ImageAttributes())
                        {
                            attributes.SetColorMatrix(colorMatrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
                            g.TranslateTransform(bmp.Width / 2f, bmp.Height / 2f);
                            g.RotateTransform(angle);
                            g.TranslateTransform(-bmp.Width / 2f, -bmp.Height / 2f);
                            g.DrawImage(bmp,
                                new Rectangle(0, 0, bmp.Width, bmp.Height),
                                0, 0, bmp.Width, bmp.Height,
                                GraphicsUnit.Pixel,
                                attributes);
                        }
                    }
                    IntPtr hIcon = animatedBmp.GetHicon();
                    Icon newIcon = Icon.FromHandle(hIcon);
                    Icon clonedIcon = (Icon)newIcon.Clone();
                    DestroyIcon(hIcon);
                    animatedBmp.Dispose();
                    return clonedIcon;
                }
            }
            catch (Exception ex)
            {
                Log($"CreateAnimatedIcon error: {ex.Message}");
                return null;
            }
        }
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);
        private static void Log(string message)
        {
            try
            {
                // Use centralized LogService for DEBUG level
                LogService.Debug($"[TrayAnimator] {message}", "TrayAnimator");
                _logAction?.Invoke($"[TrayAnimator] {message}");
            }
            catch { }
        }
        public void Dispose()
        {
            Log("Disposing TrayIconAnimator");
            Stop();
            _animationTimer?.Dispose();
            _cachedBitmap?.Dispose();
            _originalIcon?.Dispose();
        }
    }
}
