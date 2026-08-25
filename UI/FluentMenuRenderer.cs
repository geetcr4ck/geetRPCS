/**
 * geetRPCS - Fluent-style tray menu renderer
 * Custom ToolStripProfessionalRenderer for the tray context menu and its
 * drop-downs. Paints the menu background, rounded hover highlight, hairline
 * separators, accent-colored checkmarks and theme-colored text/arrows from
 * ThemePalette (resolved at paint time), so the WinForms menu follows the
 * app's light/dark theme just like the WPF windows — including live switches.
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

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace geetRPCS.UI
{
    /// <summary>Fluent-style renderer for the tray menu (background, hover, separators,
    /// checks, text and arrows all come from ThemePalette). Drop-down submenus inherit
    /// this renderer automatically from the owning ContextMenuStrip.</summary>
    internal sealed class FluentMenuRenderer : ToolStripProfessionalRenderer
    {
        private const int CornerRadius = 4;

        public FluentMenuRenderer()
        {
            // Keep the popup window itself OS-shaped (rounded menu corners are a
            // window-level feature; only the item hover gets Fluent rounding).
            RoundedEdges = false;
        }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using (var brush = new SolidBrush(ThemePalette.Background))
                e.Graphics.FillRectangle(brush, e.AffectedBounds);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            using (var pen = new Pen(ThemePalette.Divider))
            {
                var r = new Rectangle(0, 0, e.AffectedBounds.Width - 1, e.AffectedBounds.Height - 1);
                var hole = e.ConnectedArea;
                if (hole.IsEmpty || hole.Height <= 0)
                {
                    e.Graphics.DrawRectangle(pen, r);
                    return;
                }
                // A child dropdown joins its parent along e.ConnectedArea; leave
                // the border open there (like the base professional renderer) or
                // two facing hairlines make the panels look detached.
                e.Graphics.DrawLine(pen, r.Left, r.Top, r.Right, r.Top);
                e.Graphics.DrawLine(pen, r.Left, r.Bottom, r.Right, r.Bottom);
                if (hole.Left <= 0)
                {
                    e.Graphics.DrawLine(pen, r.Left, r.Top, r.Left, System.Math.Min(hole.Top - 1, r.Bottom));
                    e.Graphics.DrawLine(pen, r.Left, System.Math.Min(hole.Bottom + 1, r.Bottom), r.Left, r.Bottom);
                }
                else
                    e.Graphics.DrawLine(pen, r.Left, r.Top, r.Left, r.Bottom);
                if (hole.Right >= r.Right)
                {
                    e.Graphics.DrawLine(pen, r.Right, r.Top, r.Right, System.Math.Min(hole.Top - 1, r.Bottom));
                    e.Graphics.DrawLine(pen, r.Right, System.Math.Min(hole.Bottom + 1, r.Bottom), r.Right, r.Bottom);
                }
                else
                    e.Graphics.DrawLine(pen, r.Right, r.Top, r.Right, r.Bottom);
            }
        }

        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
        {
            // The default image margin draws a light gradient bar that would clash
            // with a dark menu; fill it with the menu background instead.
            using (var brush = new SolidBrush(ThemePalette.Background))
                e.Graphics.FillRectangle(brush, e.AffectedBounds);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            var item = e.Item;
            if ((item.Selected || item.Pressed) && item.Enabled && item.Width > 2 && item.Height > 2)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var rect = new Rectangle(1, 1, item.Width - 2, item.Height - 2);
                using (var path = RoundedRect(rect, CornerRadius))
                using (var brush = new SolidBrush(ThemePalette.HoverFill))
                    g.FillPath(brush, path);
            }
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? ThemePalette.Foreground : ThemePalette.TextSecondary;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            // Fluent-style accent checkmark (replaces the default gray system check).
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = e.ImageRectangle;
            float x = r.Left + r.Width / 2f - 4f;
            float y = r.Top + r.Height / 2f - 2f;
            // AccentGlyph: the raw accent can be too light for the menu background
            // (e.g. a bright cyan on the light theme) — the check must stay visible.
            using (var pen = new Pen(ThemePalette.AccentGlyph, 2f))
                g.DrawLines(pen, new[]
                {
                    new PointF(x, y + 2f),
                    new PointF(x + 3f, y + 5f),
                    new PointF(x + 8f, y - 1f)
                });
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            if (e.Vertical) { base.OnRenderSeparator(e); return; }
            // Fluent separators are thin hairlines inset from the edges.
            var rect = e.Item.ContentRectangle;
            int y = rect.Top + rect.Height / 2;
            using (var pen = new Pen(ThemePalette.Divider))
                e.Graphics.DrawLine(pen, rect.Left + 12, y, rect.Right - 12, y);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = ThemePalette.TextSecondary;
            base.OnRenderArrow(e);
        }

        private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
