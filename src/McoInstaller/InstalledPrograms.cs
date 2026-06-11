using Microsoft.Win32;

namespace McoInstaller;

internal static class InstalledPrograms
{
    public static bool IsInstalled(string displayName)
    {
        return IsInstalledInView(displayName, RegistryView.Registry32) ||
               IsInstalledInView(displayName, RegistryView.Registry64);
    }

    private static bool IsInstalledInView(string displayName, RegistryView view)
    {
        using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
        using var uninstall = hklm.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
        if (uninstall is null)
        {
            return false;
        }

        foreach (var subKeyName in uninstall.GetSubKeyNames())
        {
            using var subKey = uninstall.OpenSubKey(subKeyName);
            if (subKey?.GetValue("DisplayName") is string value &&
                value.Contains(displayName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
