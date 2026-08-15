/**
 * geetRPCS - Info Dialog
 * Shared dark-theme single-button message dialog (info / error / warning).
 * Matches the visual language of ConfirmDialog and the Change Application ID dialog.
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
    internal static class InfoDialog
    {
        /// <summary>Shows a message with a single OK button (Discord-style dark theme).</summary>
        public static void Show(string message, string title)
        {
            Color bg = Color.FromArgb(47, 49, 54);
            Color blurple = Color.FromArgb(88, 101, 242);
            Color blurpleHover = Color.FromArgb(71, 82, 196);
            Color blurpleDown = Color.FromArgb(60, 69, 165);

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

            int btnW = 88, btnH = 32;
            var ok = new Button
            {
                Text = LanguageManager.Current.BtnOk ?? "OK",
                Font = font,
                ForeColor = Color.White,
                BackColor = blurple,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0, MouseOverBackColor = blurpleHover, MouseDownBackColor = blurpleDown },
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            ok.Bounds = new Rectangle(CLIENT_W - PAD - btnW, y, btnW, btnH);
            ok.DialogResult = DialogResult.OK;

            form.Controls.Add(ok);
            form.AcceptButton = ok;
            form.CancelButton = ok;
            form.ClientSize = new Size(CLIENT_W, y + btnH + PAD);
            form.ShowDialog();
        }
    }
}
