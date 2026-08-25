/**
 * geetRPCS - Segoe Fluent glyph constants
 * Single source of truth for the icon glyphs used by the ModernWpf windows AND
 * the WinForms tray menu. The font is Segoe Fluent Icons (Windows 11) with a
 * Segoe MDL2 Assets fallback (Windows 10) — the codepoints are identical in
 * both fonts (verified against the official Microsoft MDL2 codepoint list).
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
using System.Windows.Media;

namespace geetRPCS.UI.Modern
{
    /// <summary>Segoe Fluent Icons glyphs (shared so no window hardcodes the codepoints).</summary>
    public static class FluentGlyphs
    {
        /// <summary>Font family with a Segoe MDL2 Assets fallback for Windows 10.
        /// A FontFamily instance (not a string) so XAML can bind it via x:Static
        /// without the string-to-FontFamily type converter.</summary>
        public static FontFamily FontFamily { get; } =
            new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");

        // --- WPF window glyphs ---
        /// <summary>Refresh (circular arrow) — E72C.</summary>
        public const string Refresh = "\uE72C";
        /// <summary>Delete (trash can) — E74D.</summary>
        public const string Delete = "\uE74D";
        /// <summary>Pin (outline) — E718.</summary>
        public const string Pin = "\uE718";
        /// <summary>Pinned (filled) — E840.</summary>
        public const string Pinned = "\uE840";
        /// <summary>Info (circle with an i) — E946.</summary>
        public const string Info = "\uE946";

        // --- Tray menu glyphs (WinForms renders these as item images) ---
        /// <summary>Play — E768.</summary>
        public const string Play = "\uE768";
        /// <summary>Pause — E769.</summary>
        public const string Pause = "\uE769";
        /// <summary>Lock (private mode) — E72E.</summary>
        public const string Lock = "\uE72E";
        /// <summary>Mouse (mouse-energy tracking) — E962.</summary>
        public const string Mouse = "\uE962";
        /// <summary>Palette (tray animation) — E790.</summary>
        public const string Palette = "\uE790";
        /// <summary>Moon / quiet-hours crescent (theme) — E708.</summary>
        public const string Moon = "\uE708";
        /// <summary>Send (telemetry) — E724.</summary>
        public const string Send = "\uE724";
        /// <summary>Update restore (auto-update) — E777.</summary>
        public const string UpdateRestore = "\uE777";
        /// <summary>Settings gear (manage apps) — E713.</summary>
        public const string Settings = "\uE713";
        /// <summary>Edit pencil (change app id) — E70F.</summary>
        public const string Edit = "\uE70F";
        /// <summary>Message bubble (default presence editor) — E8BD.</summary>
        public const string Chat = "\uE8BD";
        /// <summary>Help circle-question mark (help &amp; guide) — E897.</summary>
        public const string Help = "\uE897";
        /// <summary>Area chart (statistics) — E9D2.</summary>
        public const string Chart = "\uE9D2";
        /// <summary>View eye (preview window) — E890.</summary>
        public const string View = "\uE890";
        /// <summary>Flag (run on startup) — E7C1.</summary>
        public const string Flag = "\uE7C1";
        /// <summary>Lightning bolt (quick actions) — E945.</summary>
        public const string Bolt = "\uE945";
        /// <summary>Globe (language) — E774.</summary>
        public const string Globe = "\uE774";
        /// <summary>Document (open log / export JSON) — E8A5.</summary>
        public const string Document = "\uE8A5";
        /// <summary>Power button (exit) — E7E8.</summary>
        public const string Power = "\uE7E8";
        /// <summary>Calendar (today) — E787.</summary>
        public const string Calendar = "\uE787";
        /// <summary>Calendar week (this week) — E8C0.</summary>
        public const string CalendarWeek = "\uE8C0";
        /// <summary>Stopwatch (all-time stats) — E916.</summary>
        public const string Stopwatch = "\uE916";
        /// <summary>Save (export CSV) — E74E.</summary>
        public const string Save = "\uE74E";
        /// <summary>Open folder — E838.</summary>
        public const string FolderOpen = "\uE838";
        /// <summary>Add plus (manage shortcuts) — E710.</summary>
        public const string Add = "\uE710";
        /// <summary>CheckMark (submenu selection indicator) — E73E.</summary>
        public const string CheckMark = "\uE73E";

        // ----------------------------------------------------------------
        // WinForms tray-menu rendering
        // ----------------------------------------------------------------
        private static string _iconFontName;

        /// <summary>Installed Segoe icon font: Segoe Fluent Icons on Windows 11,
        /// Segoe MDL2 Assets on Windows 10. Cached after first resolution.</summary>
        public static string GetIconFontName()
        {
            if (_iconFontName != null) return _iconFontName;
            using (var fonts = new System.Drawing.Text.InstalledFontCollection())
            {
                foreach (var family in fonts.Families)
                {
                    if (family.Name.Equals("Segoe Fluent Icons", StringComparison.OrdinalIgnoreCase))
                        return _iconFontName = "Segoe Fluent Icons";
                    if (family.Name.Equals("Segoe MDL2 Assets", StringComparison.OrdinalIgnoreCase))
                        return _iconFontName = "Segoe MDL2 Assets";
                }
            }
            return _iconFontName = "Segoe MDL2 Assets"; // Windows 10 always ships it
        }

        /// <summary>Renders a Segoe Fluent glyph into a small monochrome bitmap for
        /// WinForms tray-menu items. A ToolStripMenuItem cannot mix fonts inside one
        /// item's text, and PUA glyphs get no GDI font-fallback — so the glyph is
        /// drawn with the icon font into the item's Image instead. The color follows
        /// the active theme (ThemePalette.TextSecondary), so icons keep good contrast
        /// on the menu's light/dark background instead of a fixed neutral gray.
        /// NOTE: the color is baked into the bitmap, so re-create the image (menu
        /// rebuild) when the theme changes.</summary>
        public static System.Drawing.Bitmap CreateMenuGlyph(string glyph, int size = 16)
            => CreateMenuGlyph(glyph, ThemePalette.TextSecondary, size);

        /// <summary>Renders a glyph in an explicit color — used for theme-aware
        /// rendering, e.g. ThemePalette.TextSecondary resolved under the current
        /// theme (white-ish in dark mode, black-ish in light mode).</summary>
        public static System.Drawing.Bitmap CreateMenuGlyph(string glyph, System.Drawing.Color color, int size = 16)
        {
            var bmp = new System.Drawing.Bitmap(size, size);
            using (var g = System.Drawing.Graphics.FromImage(bmp))
            {
                g.Clear(System.Drawing.Color.Transparent);
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                using (var font = new System.Drawing.Font(GetIconFontName(), size - 1f,
                           System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel))
                using (var brush = new System.Drawing.SolidBrush(color))
                using (var sf = new System.Drawing.StringFormat
                {
                    Alignment = System.Drawing.StringAlignment.Center,
                    LineAlignment = System.Drawing.StringAlignment.Center
                })
                {
                    g.DrawString(glyph, font, brush, new System.Drawing.RectangleF(0, 0, size, size), sf);
                }
            }
            return bmp;
        }

        /// <summary>Removes a single leading emoji (plus an optional variation
        /// selector U+FE0F and following whitespace) from a string. Used where a
        /// Fluent glyph replaces the emoji (tray menu items, dialog titles).</summary>
        public static string StripLeadingEmoji(string text)
        {
            if (string.IsNullOrEmpty(text)) return text ?? "";
            int i = 0;
            bool stripped = false;
            if (i + 1 < text.Length && char.IsHighSurrogate(text[i]) && char.IsLowSurrogate(text[i + 1]))
            {
                i += 2; // most emoji are surrogate pairs (e.g. 🔄, 📊, 🚀)
                stripped = true;
            }
            else if (IsBmpEmoji(text[i]))
            {
                i += 1; // BMP symbols (e.g. ✅, ❌, ⚙)
                stripped = true;
            }
            if (!stripped) return text;
            // Drop a trailing variation selector: "👁️" / "⚙️" / "▶️" are
            // pair+U+FE0F (the pair alone is not the full emoji sequence).
            if (i < text.Length && text[i] == '\uFE0F') i += 1;
            while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
            return text.Substring(i);
        }

        private static bool IsBmpEmoji(char c)
        {
            int code = c;
            // Arrows (2190-21FF), Misc Technical (2300-23FF, e.g. ⏸ U+23F8,
            // ⏹ U+23F9), Geometric Shapes (25A0-25FF, e.g. ▶ U+25B6, ◀ U+25C0),
            // Misc Symbols + Dingbats (2600-27BF) and Misc Symbols and Arrows
            // (2B00-2BFF). All symbol blocks — menu labels never legitimately
            // start with a letter from these ranges.
            return (code >= 0x2190 && code <= 0x21FF) ||
                   (code >= 0x2300 && code <= 0x23FF) ||
                   (code >= 0x25A0 && code <= 0x25FF) ||
                   (code >= 0x2600 && code <= 0x27BF) ||
                   (code >= 0x2B00 && code <= 0x2BFF);
        }
    }
}
