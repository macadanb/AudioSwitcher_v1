﻿using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace FortyOne.AudioSwitcher
{
    public static class IconExtractor
    {
        /// <summary>
        /// Extrae un ícono de un archivo DLL o EXE
        /// </summary>
        /// <param name="file">Ruta del archivo</param>
        /// <param name="number">Índice del ícono (puede ser negativo)</param>
        /// <param name="largeIcon">True para ícono grande (32x32), False para pequeño (16x16)</param>
        /// <returns>Ícono extraído o null si falla</returns>
        public static Icon Extract(string file, int number, bool largeIcon)
        {
            if (string.IsNullOrEmpty(file))
                return null;

            IntPtr large = IntPtr.Zero;
            IntPtr small = IntPtr.Zero;
            int result = 0;

            try
            {
                // Asegurar que el archivo existe
                string expandedFile = Environment.ExpandEnvironmentVariables(file);
                
                // Para índices negativos, convertir a positivo para la API
                int indexToUse = number;
                if (number < 0)
                {
                    // Los índices negativos en mmres.dll son válidos, los pasamos tal cual
                    // ExtractIconExW maneja índices negativos
                    indexToUse = number;
                }

                result = ExtractIconEx(expandedFile, indexToUse, out large, out small, 1);
                
                if (result <= 0 || (large == IntPtr.Zero && small == IntPtr.Zero))
                    return null;

                IntPtr iconHandle = largeIcon ? large : small;
                
                if (iconHandle == IntPtr.Zero)
                {
                    // Si el tamaño solicitado no está disponible, usar el otro
                    iconHandle = largeIcon ? small : large;
                    if (iconHandle == IntPtr.Zero)
                        return null;
                }

                // Crear un ícono desde el handle y luego clonarlo para poder destruir el original
                using (var tempIcon = Icon.FromHandle(iconHandle))
                {
                    return tempIcon.Clone() as Icon;
                }
            }
            catch
            {
                return null;
            }
            finally
            {
                // Liberar los handles
                if (large != IntPtr.Zero)
                    DestroyIcon(large);
                if (small != IntPtr.Zero)
                    DestroyIcon(small);
            }
        }

        /// <summary>
        /// Extrae un ícono usando el índice especificado, intentando con ambos tamaños
        /// </summary>
        public static Icon ExtractAny(string file, int number)
        {
            var icon = Extract(file, number, true);
            if (icon != null)
                return icon;
            return Extract(file, number, false);
        }

        /// <summary>
        /// Extrae el ícono primario de un archivo (índice 0)
        /// </summary>
        public static Icon ExtractPrimary(string file)
        {
            return ExtractAny(file, 0);
        }

        /// <summary>
        /// Extrae un ícono de un archivo usando su ruta y un índice opcional
        /// Soporta formatos como "ruta.dll,5" o "ruta.ico"
        /// </summary>
        public static Icon ExtractFromPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            try
            {
                string expandedPath = Environment.ExpandEnvironmentVariables(path);
                expandedPath = expandedPath.Trim().Trim('"');

                // Verificar si es un archivo .ico directo
                if (expandedPath.EndsWith(".ico", StringComparison.OrdinalIgnoreCase) && System.IO.File.Exists(expandedPath))
                {
                    using (var fs = new System.IO.FileStream(expandedPath, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read))
                    {
                        return new Icon(fs);
                    }
                }

                // Verificar si tiene formato "archivo.dll,indice"
                var parts = expandedPath.Split(',');
                if (parts.Length >= 2)
                {
                    string filePath = parts[0].Trim();
                    if (int.TryParse(parts[1].Trim(), out int index))
                    {
                        return Extract(filePath, index, true);
                    }
                }

                // Si no, tratar como archivo simple
                if (System.IO.File.Exists(expandedPath))
                {
                    return ExtractPrimary(expandedPath);
                }
            }
            catch
            {
                // Ignorar errores
            }

            return null;
        }

        [DllImport("Shell32.dll", EntryPoint = "ExtractIconExW", CharSet = CharSet.Unicode, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        private static extern int ExtractIconEx(string sFile, int iIndex, out IntPtr piLargeVersion, out IntPtr piSmallVersion, int amountIcons);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);
    }
}