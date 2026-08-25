/**
 * geetRPCS - Theme Palette
 * Resolves the ModernWpf theme brushes to System.Drawing colors so the
 * WinForms tray context menu (FluentMenuRenderer glyphs and backgrounds)
 * follows the current light/dark theme just like the WPF windows. Colors
 * are resolved at paint-time, so a live theme switch is picked up by the
 * next menu repaint. Fallbacks keep the original Discord-dark palette when
 * WPF is unavailable.
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

namespace geetRPCS.UI
{
    internal static class ThemePalette
    {
        public static Color Background => FromKey("ApplicationPageBackgroundThemeBrush", 47, 49, 54);
        public static Color Foreground => FromKey("TextFillColorPrimaryBrush", 255, 255, 255);
        public static Color Accent => FromKey("AccentFillColorDefaultBrush", 88, 101, 242);
        public static Color AccentHover => FromKey("AccentFillColorSecondaryBrush", 71, 82, 196);
        public static Color AccentPressed => FromKey("AccentFillColorTertiaryBrush", 60, 69, 165);
        public static Color AccentForeground => FromKey("TextOnAccentFillColorPrimaryBrush", 255, 255, 255);
        public static Color HoverFill => FromKey("SubtleFillColorSecondaryBrush", 62, 64, 70);
        public static Color TextSecondary => FromKey("TextFillColorSecondaryBrush", 168, 170, 176);
        public static Color Divider => FromKey("DividerStrokeColorDefaultBrush", 38, 39, 43);
        public static Color NeutralButton => FromKey("ControlFillColorSecondaryBrush", 78, 80, 88);
        public static Color NeutralButtonHover => FromKey("ControlFillColorTertiaryBrush", 109, 111, 120);
        public static Color NeutralButtonPressed => FromKey("ControlFillColorTertiaryBrush", 92, 94, 102);

        /// <summary>Accent color guaranteed to keep ~3:1 contrast against the current
        /// theme background — for small ON-state icons and checkmarks. Windows accents
        /// can be very light (e.g. a bright cyan that vanishes on the light menu
        /// background), so the accent is blended toward black (light theme) or white
        /// (dark theme) until the ratio is met. Resolved at access time like the rest
        /// of the palette, so a live theme switch is picked up.</summary>
        public static Color AccentGlyph
        {
            get
            {
                Color accent = Accent;
                Color bg = Background;
                if (ContrastRatio(accent, bg) >= 3.0) return accent;
                bool darkBg = RelativeLuminance(bg) < 0.4;
                Color target = darkBg ? Color.White : Color.Black;
                for (int step = 5; step <= 95; step += 5)
                {
                    double t = step / 100.0;
                    Color blended = Color.FromArgb(
                        (byte)Math.Round(accent.R + (target.R - accent.R) * t),
                        (byte)Math.Round(accent.G + (target.G - accent.G) * t),
                        (byte)Math.Round(accent.B + (target.B - accent.B) * t));
                    if (ContrastRatio(blended, bg) >= 3.0) return blended;
                }
                return target;
            }
        }

        /// <summary>WCAG 2.x relative luminance of a color (sRGB linearization).</summary>
        internal static double RelativeLuminance(Color c)
        {
            double L(double v) => v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
            return 0.2126 * L(c.R / 255.0) + 0.7152 * L(c.G / 255.0) + 0.0722 * L(c.B / 255.0);
        }

        /// <summary>WCAG 2.x contrast ratio between two colors (1..21).</summary>
        internal static double ContrastRatio(Color a, Color b)
        {
            double la = RelativeLuminance(a);
            double lb = RelativeLuminance(b);
            double hi = Math.Max(la, lb);
            double lo = Math.Min(la, lb);
            return (hi + 0.05) / (lo + 0.05);
        }

        /// <summary>Reads a ModernWpf theme brush for the current actual theme (fallback = Discord-dark color).</summary>
        private static Color FromKey(string key, byte r, byte g, byte b)
        {
            try
            {
                var brush = System.Windows.Application.Current?.TryFindResource(key)
                    as System.Windows.Media.SolidColorBrush;
                if (brush != null)
                {
                    var c = brush.Color;
                    // Brushes with Opacity (e.g. accent hover) apply it to the color.
                    double alpha = Math.Round(c.A * brush.Opacity);
                    return Color.FromArgb((byte)alpha, c.R, c.G, c.B);
                }
            }
            catch { }
            return Color.FromArgb(r, g, b);
        }
    }
}
