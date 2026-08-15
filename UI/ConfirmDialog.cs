/**
 * geetRPCS - Confirm Dialog
 * Shared dark-theme Yes/No confirmation dialog. Matches the visual language
 * of the Change Application ID dialog (Discord-style palette, flat buttons,
 * app icon, Enter = Yes, Esc = No).
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
using System.IO;
using System.Windows.Forms;
using geetRPCS.Services;

namespace geetRPCS.UI
{
    internal static class ConfirmDialog
    {
        /// <summary>Shows a Yes/No confirmation. Returns true when Yes was chosen.</summary>
        public static bool Show(string message, string title)
        {
            Color bg = Color.FromArgb(47, 49, 54);
            Color blurple = Color.FromArgb(88, 101, 242);
            Color blurpleHover = Color.FromArgb(71, 82, 196);
            Color blurpleDown = Color.FromArgb(60, 69, 165);
            Color btnBg = Color.FromArgb(78, 80, 88);
            Color btnHover = Color.FromArgb(109, 111, 120);
            Color btnDown = Color.FromArgb(92, 94, 102);

            Font font = new Font("Segoe UI", 9);
            const int PAD = 24;
            const int CLIENT_W = 440;

            using Form form = new Form()
            {
                FormBorderStyle = FormBorderStyle.FixedDialog,
                Text = title,
                StartPosition = FormStartPosition.CenterScreen,
                MaximizeBox = false,
                MinimizeBox = false,
                ShowInTaskbar = false,
                BackColor = bg,
                ForeColor = Color.White
            };
            try
            {
                string iconPath = Utils.AppPaths.IconPath;
                if (File.Exists(iconPath)) form.Icon = new Icon(iconPath);
            }
            catch { }

            int contentW = CLIENT_W - 2 * PAD;
            int y = PAD;

            var lbl = new Label()
            {
                Left = PAD,
                Top = y,
                Width = contentW,
                Text = message,
                AutoSize = false,
                Font = font,
                ForeColor = Color.White
            };
            lbl.Height = TextRenderer.MeasureText(message, font, new Size(contentW, 0), TextFormatFlags.WordBreak).Height + 2;
            form.Controls.Add(lbl);
            y += lbl.Height + 24;

            int btnW = 88, btnH = 32, gap = 8;
            var btnNo = MakeButton(LanguageManager.Current.BtnNo ?? "No", btnBg, btnHover, btnDown, font);
            btnNo.Bounds = new Rectangle(CLIENT_W - PAD - 2 * btnW - gap, y, btnW, btnH);
            btnNo.DialogResult = DialogResult.Cancel;
            btnNo.TabIndex = 0;

            var btnYes = MakeButton(LanguageManager.Current.BtnYes ?? "Yes", blurple, blurpleHover, blurpleDown, font);
            btnYes.Bounds = new Rectangle(CLIENT_W - PAD - btnW, y, btnW, btnH);
            btnYes.DialogResult = DialogResult.OK;
            btnYes.TabIndex = 1;

            form.Controls.Add(btnNo);
            form.Controls.Add(btnYes);
            form.AcceptButton = btnYes;
            form.CancelButton = btnNo;
            form.ClientSize = new Size(CLIENT_W, y + btnH + PAD);
            return form.ShowDialog() == DialogResult.OK;
        }

        /// <summary>Flat dark-theme button with hover/pressed states (Discord style).</summary>
        private static Button MakeButton(string text, Color bg, Color hover, Color down, Font font)
        {
            return new Button
            {
                Text = text,
                Font = font,
                ForeColor = Color.White,
                BackColor = bg,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0, MouseOverBackColor = hover, MouseDownBackColor = down },
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
        }
    }
}
