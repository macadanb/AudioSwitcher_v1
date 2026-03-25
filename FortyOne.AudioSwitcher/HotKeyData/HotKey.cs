﻿using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using AudioSwitcher.AudioApi;

namespace FortyOne.AudioSwitcher.HotKeyData
{
    // NUEVA CLASE: Event Args personalizado para pasar el dispositivo que se debe activar
    public class HotKeyPressedEventArgs : EventArgs
    {
        public Guid TargetDeviceId { get; private set; }
        public bool IsLongPress { get; private set; }

        public HotKeyPressedEventArgs(Guid targetDeviceId, bool isLongPress)
        {
            TargetDeviceId = targetDeviceId;
            IsLongPress = isLongPress;
        }
    }

    public class HotKey : IDisposable
    {
        public bool IsRegistered;

        public HotKey()
        {
            Modifiers = Modifiers.None;
            Key = Keys.None;
            LongPressDelay = 500;
            LongPressDeviceId = Guid.Empty;
        }

        public Guid DeviceId { get; set; }
        public Guid LongPressDeviceId { get; set; }
        public int LongPressDelay { get; set; }

        public IDevice Device
        {
            get { return AudioDeviceManager.Controller.GetDevice(DeviceId); }
        }

        // Propiedad para obtener el dispositivo de long press
        public IDevice LongPressDevice
        {
            get 
            { 
                if (LongPressDeviceId == Guid.Empty)
                    return null;
                return AudioDeviceManager.Controller.GetDevice(LongPressDeviceId); 
            }
        }

        public string DeviceName
        {
            get
            {
                if (Device == null)
                    return "Unknown Device";
                return Device.FullName;
            }
        }

        public string HotKeyString
        {
            get
            {
                var keystring = "";
                if ((Modifiers & Modifiers.Alt) > 0)
                    keystring += "Alt+";
                if ((Modifiers & Modifiers.Control) > 0)
                    keystring += "Ctrl+";
                if ((Modifiers & Modifiers.Shift) > 0)
                    keystring += "Shift+";
                if ((Modifiers & Modifiers.Win) > 0)
                    keystring += "Win+";
                keystring += Key.ToString();

                return keystring;
            }
        }

        private HotKeyNativeWindow HotKeyWindow { get; set; }
        public Modifiers Modifiers { get; set; }
        public Keys Key { get; set; }

        public void Dispose()
        {
            if (HotKeyWindow != null)
                HotKeyWindow.UnregisterHotkey();
        }

        // MODIFICADO: Ahora el evento usa la nueva clase HotKeyPressedEventArgs
        public event EventHandler<HotKeyPressedEventArgs> HotKeyPressed;

        public bool RegisterHotkey()
        {
            if (HotKeyWindow == null)
                HotKeyWindow = new HotKeyNativeWindow(this);

            try
            {
                if (Key != Keys.None)
                {
                    HotKeyWindow.RegisterHotkey();
                }
                else
                {
                    if (HotKeyWindow.Handle != IntPtr.Zero)
                        HotKeyWindow.DestroyHandle();
                    HotKeyWindow = null;
                }

                IsRegistered = true;
            }
            catch
            {
                if (HotKeyWindow.Handle != IntPtr.Zero)
                    HotKeyWindow.DestroyHandle();
                HotKeyWindow = null;

                IsRegistered = false;
            }

            return IsRegistered;
        }

        public void RegisterHotkey(Modifiers modifiers, Keys key)
        {
            Modifiers = modifiers;
            Key = key;
            RegisterHotkey();
        }

        public void UnregisterHotkey()
        {
            if (IsRegistered && HotKeyWindow != null)
                HotKeyWindow.UnregisterHotkey();

            IsRegistered = false;
        }

        public void ActivateWindow(IntPtr hWnd)
        {
            var hForeground = NativeMethods.GetForegroundWindow();
            if (hWnd != hForeground)
            {
                var hForegroundThread = NativeMethods.GetWindowThreadProcessId(hForeground, IntPtr.Zero);
                var hCurrentThread = NativeMethods.GetWindowThreadProcessId(hWnd, IntPtr.Zero);

                if (hForegroundThread != hCurrentThread)
                {
                    NativeMethods.AttachThreadInput(hForegroundThread, hCurrentThread, true);
                    NativeMethods.SetForegroundWindow(hWnd);
                    NativeMethods.AttachThreadInput(hForegroundThread, hCurrentThread, false);
                }
                else
                {
                    NativeMethods.SetForegroundWindow(hWnd);
                }

                if (NativeMethods.IsIconic(hWnd))
                {
                    NativeMethods.ShowWindow(hWnd, NativeMethods.ShowWindowCommand.SW_RESTORE);
                }
                else
                {
                    NativeMethods.ShowWindow(hWnd, NativeMethods.ShowWindowCommand.SW_SHOW);
                }
            }
        }

        // MODIFICADO: El evento OnHotKey ahora recibe el targetDeviceId y si es long press
        protected virtual void OnHotKey(Guid targetDeviceId, bool isLongPress)
        {
            if (HotKeyPressed != null)
                HotKeyPressed(this, new HotKeyPressedEventArgs(targetDeviceId, isLongPress));
        }

        private class HotKeyNativeWindow : NativeWindow
        {
            private const int WM_HOTKEY = 0x312;
            private static readonly IntPtr HWND_MESSAGE = new IntPtr(-3);

            public HotKeyNativeWindow(HotKey owner)
            {
                Owner = owner;
                _pollTimer = new Timer();
                _pollTimer.Interval = 20;
                _pollTimer.Tick += PollTimer_Tick;
            }

            private HotKey Owner { get; set; }
            private short HotKeyID { get; set; }
            private bool _isProcessing;
            private bool _isLongPressTriggered;
            private Timer _pollTimer;
            private DateTime _pressStartTime;
            private bool _isWaiting;

            ~HotKeyNativeWindow()
            {
                try
                {
                    UnregisterHotkey();
                    _pollTimer?.Dispose();
                }
                catch { }
            }

            public override void DestroyHandle()
            {
                UnregisterHotkey();
                base.DestroyHandle();
            }

            public override void ReleaseHandle()
            {
                UnregisterHotkey();
                base.ReleaseHandle();
            }

            public void RegisterHotkey()
            {
                if (HandleCreated() && Owner.Key != Keys.None)
                {
                    if (HotKeyID == 0)
                        HotKeyID = NativeMethods.GlobalAddAtom(Guid.NewGuid().ToString("N"));

                    if (!NativeMethods.RegisterHotKey(Handle, HotKeyID, (int)Owner.Modifiers, (int)Owner.Key))
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    }
                }
            }

            public void UnregisterHotkey()
            {
                if (Handle != IntPtr.Zero && HotKeyID != 0)
                {
                    NativeMethods.UnregisterHotKey(Handle, HotKeyID);
                    NativeMethods.GlobalDeleteAtom(HotKeyID);
                    HotKeyID = 0;
                }
                _pollTimer.Stop();
                _isWaiting = false;
            }

            private bool HandleCreated()
            {
                if (Handle == IntPtr.Zero)
                {
                    var createParams = new CreateParams();
                    createParams.Caption = Guid.NewGuid().ToString("N");
                    createParams.Style = 0;
                    createParams.ExStyle = 0;
                    createParams.ClassStyle = 0;
                    if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                    {
                        createParams.Parent = HWND_MESSAGE;
                    }
                    CreateHandle(createParams);
                }
                return (Handle != IntPtr.Zero);
            }

            private void PollTimer_Tick(object sender, EventArgs e)
            {
                if (!_isWaiting)
                    return;

                int vk = (int)Owner.Key;
                bool keyIsDown = (NativeMethods.GetAsyncKeyState(vk) & 0x8000) != 0;

                if (keyIsDown)
                {
                    // Tecla aún presionada - verificar si alcanzó long press
                    if (!_isLongPressTriggered && 
                        Owner.LongPressDeviceId != Guid.Empty && 
                        (DateTime.Now - _pressStartTime).TotalMilliseconds >= Owner.LongPressDelay)
                    {
                        _isLongPressTriggered = true;
                        ExecuteHotKey(true);
                    }
                }
                else
                {
                    // Tecla liberada
                    _pollTimer.Stop();
                    _isWaiting = false;

                    if (!_isLongPressTriggered && !_isProcessing)
                    {
                        // Single tap
                        ExecuteHotKey(false);
                    }
                    _isLongPressTriggered = false;
                }
            }

            // MODIFICADO: Ahora ejecuta el hotkey pasando el targetDeviceId correcto
            private void ExecuteHotKey(bool isLongPress)
            {
                if (_isProcessing)
                    return;

                _isProcessing = true;

                try
                {
                    Guid targetDeviceId;
                    if (isLongPress && Owner.LongPressDeviceId != Guid.Empty)
                        targetDeviceId = Owner.LongPressDeviceId;
                    else
                        targetDeviceId = Owner.DeviceId;

                    // Ya no cambiamos temporalmente Owner.DeviceId
                    // Simplemente pasamos el targetDeviceId directamente al evento
                    Owner.OnHotKey(targetDeviceId, isLongPress);
                }
                finally
                {
                    _isProcessing = false;
                }
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WM_HOTKEY)
                {
                    if (_isProcessing || _isWaiting)
                    {
                        base.WndProc(ref m);
                        return;
                    }

                    int vk = (int)Owner.Key;
                    bool keyIsDown = (NativeMethods.GetAsyncKeyState(vk) & 0x8000) != 0;

                    if (keyIsDown)
                    {
                        // Tecla presionada - iniciar polling
                        _isWaiting = true;
                        _isLongPressTriggered = false;
                        _pressStartTime = DateTime.Now;
                        
                        if (Owner.LongPressDeviceId == Guid.Empty || Owner.LongPressDelay <= 0)
                        {
                            // Sin long press configurado, ejecutar inmediatamente
                            _isWaiting = false;
                            ExecuteHotKey(false);
                        }
                        else
                        {
                            // Iniciar polling
                            _pollTimer.Start();
                        }
                    }
                }

                base.WndProc(ref m);
            }
        }
    }
}