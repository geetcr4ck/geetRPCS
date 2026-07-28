/**
 * geetRPCS - Hotkey Service
 * Manages global hotkey registration and callbacks
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
using System.Windows.Forms;
namespace geetRPCS.Services
{
    internal class HotkeyMessageWindow : NativeWindow
    {
        private const int WM_HOTKEY = 0x0312;
        public event Action HotkeyPressed;
        public HotkeyMessageWindow()
        {
            this.CreateHandle(new CreateParams { Parent = IntPtr.Zero, Style = 0 });
        }
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == WM_HOTKEY) HotkeyPressed?.Invoke();
        }
    }
    internal class GlobalHotkey : IDisposable
    {
        private const int WM_HOTKEY = 0x0312;
        private const int MOD_ALT = 0x0001, MOD_CONTROL = 0x0002, MOD_SHIFT = 0x0004, MOD_WIN = 0x0008;
        private readonly HotkeyMessageWindow _window;
        private readonly int _id;
        private bool _isRegistered;
        public event Action HotkeyPressed;
        public GlobalHotkey(Keys modifiers, Keys key)
        {
            _window = new HotkeyMessageWindow();
            _id = this.GetHashCode();
            _window.HotkeyPressed += () => HotkeyPressed?.Invoke();
            Register(modifiers, key);
        }
        private void Register(Keys modifiers, Keys key)
        {
            int fsModifiers = 0;
            if (modifiers.HasFlag(Keys.Alt)) fsModifiers |= MOD_ALT;
            if (modifiers.HasFlag(Keys.Control)) fsModifiers |= MOD_CONTROL;
            if (modifiers.HasFlag(Keys.Shift)) fsModifiers |= MOD_SHIFT;
            if (RegisterHotKey(_window.Handle, _id, fsModifiers, (int)key))
                _isRegistered = true;
            else
                System.Diagnostics.Debug.WriteLine("Failed to register hotkey. It may already be in use.");
        }
        public void Dispose()
        {
            if (_isRegistered)
            {
                UnregisterHotKey(_window.Handle, _id);
                _isRegistered = false;
            }
            _window.DestroyHandle();
        }
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    }
}
