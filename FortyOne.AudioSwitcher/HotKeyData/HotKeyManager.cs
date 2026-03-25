﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using FortyOne.AudioSwitcher.Configuration;

namespace FortyOne.AudioSwitcher.HotKeyData
{
    public static class HotKeyManager
    {
        private static readonly List<HotKey> _hotkeys = new List<HotKey>();
        public static BindingList<HotKey> HotKeys = new BindingList<HotKey>();
        
        // Diccionario para rastrear qué hotkeys están actualmente procesando
        private static readonly HashSet<string> _processingKeys = new HashSet<string>();

        static HotKeyManager()
        {
            LoadHotKeys();
            RefreshHotkeys();
        }

        // MODIFICADO: El evento ahora usa HotKeyPressedEventArgs
        public static event EventHandler<HotKeyPressedEventArgs> HotKeyPressed;

        // Método para obtener una clave única para un hotkey
        private static string GetHotKeyKey(HotKey hk)
        {
            return $"{(int)hk.Modifiers}_{(int)hk.Key}";
        }

        // Verificar si un hotkey ya está siendo procesado
        public static bool IsHotKeyProcessing(HotKey hk)
        {
            lock (_processingKeys)
            {
                return _processingKeys.Contains(GetHotKeyKey(hk));
            }
        }

        // Marcar hotkey como en procesamiento
        public static void SetHotKeyProcessing(HotKey hk, bool processing)
        {
            lock (_processingKeys)
            {
                var key = GetHotKeyKey(hk);
                if (processing)
                    _processingKeys.Add(key);
                else
                    _processingKeys.Remove(key);
            }
        }

        public static void ClearAll()
        {
            lock (_processingKeys)
            {
                _processingKeys.Clear();
            }

            foreach (var hk in _hotkeys)
            {
                hk.UnregisterHotkey();
            }

            Program.Settings.HotKeys = "";
            LoadHotKeys();
            RefreshHotkeys();
        }

        public static void LoadHotKeys()
        {
            try
            {
                foreach (var hk in _hotkeys)
                {
                    hk.UnregisterHotkey();
                }

                _hotkeys.Clear();

                var hotkeydata = Program.Settings.HotKeys;
                if (string.IsNullOrEmpty(hotkeydata))
                {
                    RefreshHotkeys();
                    return;
                }

                // Usamos una expresión regular para extraer el contenido entre corchetes [ ... ]
                var regex = new Regex(@"\[([^\]]+)\]");
                var matches = regex.Matches(hotkeydata);

                foreach (Match match in matches)
                {
                    var content = match.Groups[1].Value;
                    var parts = content.Split(',');

                    // Formato esperado: [Key, Modifiers, DeviceId, LongPressDeviceId, LongPressDelay]
                    if (parts.Length < 3) continue;

                    var hk = new HotKey();
                    hk.Key = (Keys)int.Parse(parts[0]);
                    hk.Modifiers = (Modifiers)int.Parse(parts[1]);
                    
                    // Dispositivo para Single Press
                    hk.DeviceId = new Guid(parts[2]);

                    // Dispositivo para Long Press (Opcional, compatible con versiones anteriores)
                    if (parts.Length >= 4)
                    {
                        hk.LongPressDeviceId = new Guid(parts[3]);
                    }
                    else
                    {
                        hk.LongPressDeviceId = Guid.Empty;
                    }

                    // Delay para Long Press (Opcional)
                    if (parts.Length >= 5)
                    {
                        hk.LongPressDelay = int.Parse(parts[4]);
                    }
                    else
                    {
                        // Si no existe, usamos el valor global de la configuración o 500ms
                        hk.LongPressDelay = 500; 
                    }

                    _hotkeys.Add(hk);
                    hk.HotKeyPressed += hk_HotKeyPressed;
                    hk.RegisterHotkey();
                }
            }
            catch
            {
                // Si hay un error crítico en el formato, reiniciamos para evitar crashes
                Program.Settings.HotKeys = "";
            }
        }

        // MODIFICADO: El manejador ahora recibe HotKeyPressedEventArgs
        private static void hk_HotKeyPressed(object sender, HotKeyPressedEventArgs e)
        {
            if (HotKeyPressed != null && sender is HotKey hk)
            {
                // Prevenir ejecución si ya está procesando
                if (IsHotKeyProcessing(hk))
                    return;

                SetHotKeyProcessing(hk, true);
                try
                {
                    // Pasamos el evento con la información del dispositivo objetivo
                    HotKeyPressed(sender, e);
                }
                finally
                {
                    SetHotKeyProcessing(hk, false);
                }
            }
        }

        public static void SaveHotKeys()
        {
            var hotkeydata = "";
            foreach (var hk in _hotkeys)
            {
                // Guardamos los 5 parámetros en el string de configuración
                hotkeydata += string.Format("[{0},{1},{2},{3},{4}]", 
                    (int)hk.Key, 
                    (int)hk.Modifiers, 
                    hk.DeviceId, 
                    hk.LongPressDeviceId, 
                    hk.LongPressDelay);
            }
            Program.Settings.HotKeys = hotkeydata;

            RefreshHotkeys();
        }

        public static bool AddHotKey(HotKey hk)
        {
            // Verificar duplicados (misma tecla y modificadores)
            if (DuplicateHotKey(hk))
                return false;

            hk.HotKeyPressed += hk_HotKeyPressed;
            hk.RegisterHotkey();

            if (!hk.IsRegistered)
                return false;

            _hotkeys.Add(hk);

            SaveHotKeys();
            return true;
        }

        public static void RefreshHotkeys()
        {
            HotKeys.Clear();
            var filterInvalid = !Program.Settings.ShowUnknownDevicesInHotkeyList;
            IEnumerable<HotKey> hotkeyList = _hotkeys;
            
            if (filterInvalid)
                hotkeyList = hotkeyList.Where(x => x.Device != null);
            
            foreach (var k in hotkeyList)
            {
                HotKeys.Add(k);
            }
        }

        public static bool DuplicateHotKey(HotKey hk)
        {
            return _hotkeys.Any(k => hk.Key == k.Key && hk.Modifiers == k.Modifiers);
        }

        public static void DeleteHotKey(HotKey hk)
        {
            // Limpiar del diccionario de procesamiento
            lock (_processingKeys)
            {
                var key = GetHotKeyKey(hk);
                _processingKeys.Remove(key);
            }
            
            // Aseguramos que se libere el HotKey de Windows
            hk.UnregisterHotkey();
            _hotkeys.Remove(hk);
            SaveHotKeys();
        }
    }
}