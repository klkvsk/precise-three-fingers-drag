﻿using Microsoft.Win32;
using System.Diagnostics;

namespace PreciseThreeFingersDrag
{
    internal class AutostartHelper
    {
        private const string APP_KEY = "PreciseThreeFingersDrag";

        private static RegistryKey? RegKey => Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);

        public static bool IsEnabled => (string)(RegKey?.GetValue(APP_KEY) ?? "") == Process.GetCurrentProcess().MainModule?.FileName;

        public static void Enable()
        {
            RegKey?.SetValue(APP_KEY, Process.GetCurrentProcess().MainModule?.FileName ?? "");
        }

        public static void Disable()
        {
            RegKey?.DeleteValue(APP_KEY, false);
        }
    }
}
