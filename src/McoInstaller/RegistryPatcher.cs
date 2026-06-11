using Microsoft.Win32;
using System.Globalization;

namespace McoInstaller;

internal static class RegistryPatcher
{
    private const string MotorCityRegistryPath = @"SOFTWARE\Electronic Arts\Motor City";
    private const string NetworkPlaySystemPath = @"SOFTWARE\Electronic Arts\Network Play System";
    private const string EaComAuthPath = @"SOFTWARE\EACom\AuthAuth";
    private const string AppPathRegistryPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\mcity.exe";
    private const string AuthLoginBaseService = "AuthLogin";

    public static void Patch(ServerSettings settings, string installPath, Action<string> log)
    {
        var normalizedInstallPath = NormalizeInstallPath(installPath);
        PatchView(RegistryView.Registry32, settings, normalizedInstallPath, log);
        PatchView(RegistryView.Registry64, settings, normalizedInstallPath, log);
        log("Registry patch complete.");
    }

    private static void PatchView(RegistryView view, ServerSettings settings, string installPath, Action<string> log)
    {
        using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
        using var root = hklm.CreateSubKey(MotorCityRegistryPath, writable: true);
        if (root is null)
        {
            throw new InvalidOperationException("Could not open the Motor City registry key.");
        }

        log($"Patching Motor City registry keys ({view})...");

        SetStringWhereFound(root, "PatchServerIP", settings.ServerIp);
        SetStringWhereFound(root, "PatchServerPort", PortString(settings));
        SetStringWhereFound(root, "AuthLoginServer", settings.ServerIp);
        SetStringWhereFound(root, "ShardUrl", settings.ShardUrl);

        root.SetValue("PatchServerIP", settings.ServerIp, RegistryValueKind.String);
        root.SetValue("PatchServerPort", PortString(settings), RegistryValueKind.String);
        WriteRootPatchSkipValues(root);
        WriteAuthAuthKey(root, settings);

        using (var versionKey = root.CreateSubKey("1.0", writable: true))
        {
            if (versionKey is not null)
            {
                WriteVersionKey(versionKey, settings, installPath);
            }
        }
        RemoveTickerUrlOutsideVersionKey(root);

        foreach (var authKey in FindOrCreateNamedKeys(root, "AuthAuth"))
        {
            using (authKey)
            {
                WriteAuthAuthKey(authKey, settings);
            }
        }

        WriteInstallPathValues(root, installPath);
        WriteNetworkPlaySystemKeys(hklm, settings, installPath);
        WriteEaComAuthKeys(hklm, settings, installPath);
        WriteAppPath(hklm, installPath, settings);
    }

    private static string NormalizeInstallPath(string installPath)
    {
        return Path.GetFullPath(installPath.Trim().Trim('"'))
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static string PortString(ServerSettings settings)
    {
        return settings.PatchServerPort.ToString(CultureInfo.InvariantCulture);
    }

    private static void WriteVersionKey(RegistryKey versionKey, ServerSettings settings, string installPath)
    {
        WriteInstallPathValues(versionKey, installPath);
        versionKey.SetValue("InstLev", 2, RegistryValueKind.DWord);
        versionKey.SetValue("Language", 0, RegistryValueKind.DWord);
        versionKey.SetValue("SrcDrive", Path.GetPathRoot(installPath) ?? string.Empty, RegistryValueKind.String);
        versionKey.SetValue("ShardUrl", settings.ShardUrl, RegistryValueKind.String);
        versionKey.SetValue("TickerUrl", settings.TickerUrl, RegistryValueKind.String);
    }

    private static void WriteNetworkPlaySystemKeys(RegistryKey hklm, ServerSettings settings, string installPath)
    {
        using var npsRoot = hklm.CreateSubKey(NetworkPlaySystemPath, writable: true);
        npsRoot?.SetValue("Path", Path.Combine(installPath, "nps"), RegistryValueKind.String);

        using var system = hklm.CreateSubKey(NetworkPlaySystemPath + @"\System", writable: true);
        if (system is null)
        {
            return;
        }

        system.SetValue("Auth_NPS_AAI_Hostname", settings.ServerIp, RegistryValueKind.String);
        system.SetValue("Auth_NPS_AAI_Port", settings.PatchServerPort, RegistryValueKind.DWord);
        system.SetValue("AuthUse_NPS_AAI", 1, RegistryValueKind.DWord);
        system.SetValue("Path", Path.Combine(installPath, "nps"), RegistryValueKind.String);
    }

    private static void WriteEaComAuthKeys(RegistryKey hklm, ServerSettings settings, string installPath)
    {
        using var auth = hklm.CreateSubKey(EaComAuthPath, writable: true);
        if (auth is null)
        {
            return;
        }

        var npsPath = Path.Combine(installPath, "nps");
        auth.SetValue(string.Empty, npsPath, RegistryValueKind.String);
        auth.SetValue("Path", npsPath, RegistryValueKind.String);
        auth.SetValue("Load Path", npsPath, RegistryValueKind.String);
        auth.SetValue("AuthLoginServer", settings.ServerIp, RegistryValueKind.String);
        auth.SetValue("AuthLoginBaseService", AuthLoginBaseService, RegistryValueKind.String);
    }

    private static void WriteAppPath(RegistryKey hklm, string installPath, ServerSettings settings)
    {
        using var appPath = hklm.CreateSubKey(AppPathRegistryPath, writable: true);
        if (appPath is null)
        {
            return;
        }

        var executablePath = GameInstallLocator.FindGameExecutable(installPath, settings) ??
                             Path.Combine(installPath, settings.GameExecutable);
        appPath.SetValue(string.Empty, executablePath, RegistryValueKind.String);
        appPath.SetValue("Path", installPath, RegistryValueKind.String);
    }

    private static void WriteAuthAuthKey(RegistryKey parent, ServerSettings settings)
    {
        using var auth = parent.CreateSubKey("AuthAuth", writable: true);
        if (auth is null)
        {
            return;
        }

        auth.SetValue("AuthLoginServer", settings.ServerIp, RegistryValueKind.String);
        auth.SetValue("AuthLoginBaseService", AuthLoginBaseService, RegistryValueKind.String);
    }

    private static void WriteRootPatchSkipValues(RegistryKey root)
    {
        root.SetValue("GamePatch", string.Empty, RegistryValueKind.String);
        root.SetValue("UpdateInfoPatch", string.Empty, RegistryValueKind.String);
        root.SetValue("NPSPatch", string.Empty, RegistryValueKind.String);
    }

    private static void RemoveTickerUrlOutsideVersionKey(RegistryKey root)
    {
        root.DeleteValue("TickerUrl", throwOnMissingValue: false);

        foreach (var subKeyName in root.GetSubKeyNames())
        {
            if (string.Equals(subKeyName, "1.0", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            using var subKey = root.OpenSubKey(subKeyName, writable: true);
            if (subKey is not null)
            {
                RemoveTickerUrlFromTree(subKey);
            }
        }
    }

    private static void RemoveTickerUrlFromTree(RegistryKey key)
    {
        key.DeleteValue("TickerUrl", throwOnMissingValue: false);

        foreach (var subKeyName in key.GetSubKeyNames())
        {
            using var subKey = key.OpenSubKey(subKeyName, writable: true);
            if (subKey is not null)
            {
                RemoveTickerUrlFromTree(subKey);
            }
        }
    }

    private static void WriteInstallPathValues(RegistryKey root, string installPath)
    {
        if (string.IsNullOrWhiteSpace(installPath))
        {
            return;
        }

        root.SetValue("InstallDir", installPath, RegistryValueKind.String);
        root.SetValue("Path", installPath, RegistryValueKind.String);
    }

    private static void SetStringWhereFound(RegistryKey root, string valueName, string value)
    {
        var changed = SetStringWhereFoundRecursive(root, valueName, value);
        if (!changed)
        {
            root.SetValue(valueName, value, RegistryValueKind.String);
        }
    }

    private static void SetDwordWhereFound(RegistryKey root, string valueName, int value)
    {
        var changed = SetDwordWhereFoundRecursive(root, valueName, value);
        if (!changed)
        {
            root.SetValue(valueName, value, RegistryValueKind.DWord);
        }
    }

    private static bool SetStringWhereFoundRecursive(RegistryKey key, string valueName, string value)
    {
        var changed = false;
        if (HasValue(key, valueName))
        {
            key.SetValue(valueName, value, RegistryValueKind.String);
            changed = true;
        }

        foreach (var subKeyName in key.GetSubKeyNames())
        {
            using var subKey = key.OpenSubKey(subKeyName, writable: true);
            if (subKey is not null && SetStringWhereFoundRecursive(subKey, valueName, value))
            {
                changed = true;
            }
        }

        return changed;
    }

    private static bool SetDwordWhereFoundRecursive(RegistryKey key, string valueName, int value)
    {
        var changed = false;
        if (HasValue(key, valueName))
        {
            key.SetValue(valueName, value, RegistryValueKind.DWord);
            changed = true;
        }

        foreach (var subKeyName in key.GetSubKeyNames())
        {
            using var subKey = key.OpenSubKey(subKeyName, writable: true);
            if (subKey is not null && SetDwordWhereFoundRecursive(subKey, valueName, value))
            {
                changed = true;
            }
        }

        return changed;
    }

    private static bool HasValue(RegistryKey key, string valueName)
    {
        return key.GetValueNames().Any(name => string.Equals(name, valueName, StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<RegistryKey> FindOrCreateNamedKeys(RegistryKey root, string keyName)
    {
        var matches = FindNamedKeys(root, keyName).ToList();
        if (matches.Count > 0)
        {
            return matches;
        }

        var created = root.CreateSubKey(keyName, writable: true);
        return created is null ? [] : [created];
    }

    private static IEnumerable<RegistryKey> FindNamedKeys(RegistryKey root, string keyName)
    {
        foreach (var subKeyName in root.GetSubKeyNames())
        {
            RegistryKey? subKey = null;
            try
            {
                subKey = root.OpenSubKey(subKeyName, writable: true);
            }
            catch
            {
                subKey?.Dispose();
                continue;
            }

            if (subKey is null)
            {
                continue;
            }

            if (string.Equals(subKeyName, keyName, StringComparison.OrdinalIgnoreCase))
            {
                yield return subKey;
            }
            else
            {
                foreach (var nested in FindNamedKeys(subKey, keyName))
                {
                    yield return nested;
                }

                subKey.Dispose();
            }
        }
    }

}
