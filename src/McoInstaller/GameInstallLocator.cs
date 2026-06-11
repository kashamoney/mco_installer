using Microsoft.Win32;
using System.Text.RegularExpressions;

namespace McoInstaller;

internal static class GameInstallLocator
{
    private static readonly string[] ExeNames = ["MCity.exe", "mcity.exe", "mcity_d.exe", "MCityD.exe"];
    private static readonly string[] MotorCityRegistryPaths =
    [
        @"SOFTWARE\Electronic Arts\Motor City",
        @"SOFTWARE\Electronic Arts\Motor City Online",
        @"SOFTWARE\EA GAMES\Motor City",
        @"SOFTWARE\EA GAMES\Motor City Online"
    ];

    public static string DefaultInstallPath
    {
        get
        {
            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            if (string.IsNullOrWhiteSpace(programFilesX86))
            {
                programFilesX86 = @"C:\Program Files (x86)";
            }

            return Path.Combine(programFilesX86, "EA Games", "Motor City Online");
        }
    }

    public static string? Locate(string? preferredPath)
    {
        foreach (var candidate in EnumerateCandidateDirectories(preferredPath)
                     .Select(NormalizeDirectoryPath)
                     .Where(path => path is not null)
                     .Cast<string>()
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (IsGameInstallDirectory(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    public static string? NormalizeInstallDirectory(string? path)
    {
        return NormalizeDirectoryPath(path);
    }

    public static bool IsGameInstallDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            return false;
        }

        return ExeNames.Any(name => File.Exists(Path.Combine(path, name)));
    }

    public static string? FindGameExecutable(string installPath, ServerSettings settings)
    {
        var preferred = Path.Combine(installPath, settings.GameExecutable);
        if (File.Exists(preferred))
        {
            return preferred;
        }

        return ExeNames
            .Select(name => Path.Combine(installPath, name))
            .FirstOrDefault(File.Exists);
    }

    private static IEnumerable<string> EnumerateCandidateDirectories(string? preferredPath)
    {
        yield return DefaultInstallPath;

        if (!string.IsNullOrWhiteSpace(preferredPath))
        {
            yield return preferredPath;
        }

        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

        foreach (var path in EnumerateCommonInstallPaths(programFilesX86, programFiles))
        {
            yield return path;
        }

        foreach (var scannedPath in EnumerateLikelyExistingFolders(programFilesX86, programFiles))
        {
            yield return scannedPath;
        }

        foreach (var registryPath in ReadRegistryInstallPaths())
        {
            yield return registryPath;
        }

        foreach (var uninstallPath in ReadUninstallInstallPaths())
        {
            yield return uninstallPath;
        }
    }

    private static IEnumerable<string> EnumerateCommonInstallPaths(string programFilesX86, string programFiles)
    {
        foreach (var root in EnumerateFixedDriveRoots())
        {
            yield return Path.Combine(root, "Games", "MCO");
            yield return Path.Combine(root, "Games", "Motor City Online");
            yield return Path.Combine(root, "MCO");
            yield return Path.Combine(root, "Motor City Online");
        }

        foreach (var path in new[]
                 {
                     Path.Combine(programFilesX86, "EA GAMES", "Motor City Online"),
                     Path.Combine(programFilesX86, "EA GAMES", "Motor City"),
                     Path.Combine(programFilesX86, "Electronic Arts", "Motor City Online"),
                     Path.Combine(programFilesX86, "Electronic Arts", "Motor City"),
                     Path.Combine(programFilesX86, "Motor City Online"),
                     Path.Combine(programFiles, "EA GAMES", "Motor City Online"),
                     Path.Combine(programFiles, "EA GAMES", "Motor City"),
                     Path.Combine(programFiles, "Electronic Arts", "Motor City Online"),
                     Path.Combine(programFiles, "Electronic Arts", "Motor City"),
                     Path.Combine(programFiles, "Motor City Online")
                 })
        {
            yield return path;
        }
    }

    private static IEnumerable<string> EnumerateLikelyExistingFolders(string programFilesX86, string programFiles)
    {
        var parents = EnumerateFixedDriveRoots()
            .SelectMany(root => new[]
            {
                Path.Combine(root, "Games"),
                Path.Combine(root, "EA GAMES"),
                Path.Combine(root, "Electronic Arts")
            })
            .Concat([
                Path.Combine(programFilesX86, "EA GAMES"),
                Path.Combine(programFilesX86, "Electronic Arts"),
                Path.Combine(programFiles, "EA GAMES"),
                Path.Combine(programFiles, "Electronic Arts")
            ])
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var parent in parents)
        {
            if (!Directory.Exists(parent))
            {
                continue;
            }

            IEnumerable<string> children;
            try
            {
                children = Directory.EnumerateDirectories(parent, "*", SearchOption.TopDirectoryOnly);
            }
            catch
            {
                continue;
            }

            foreach (var child in children)
            {
                var name = Path.GetFileName(child);
                if (name.Contains("Motor", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("MCO", StringComparison.OrdinalIgnoreCase))
                {
                    yield return child;
                }
            }
        }
    }

    private static IEnumerable<string> EnumerateFixedDriveRoots()
    {
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (drive.DriveType != DriveType.Fixed)
            {
                continue;
            }

            if (!drive.IsReady)
            {
                continue;
            }

            yield return drive.RootDirectory.FullName;
        }
    }

    private static IEnumerable<string> ReadRegistryInstallPaths()
    {
        foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
        {
            foreach (var view in new[] { RegistryView.Registry32, RegistryView.Registry64 })
            {
                IEnumerable<string> paths;
                try
                {
                    paths = ReadRegistryInstallPaths(hive, view).ToArray();
                }
                catch
                {
                    continue;
                }

                foreach (var path in paths)
                {
                    yield return path;
                }
            }
        }
    }

    private static IEnumerable<string> ReadRegistryInstallPaths(RegistryHive hive, RegistryView view)
    {
        using var baseKey = RegistryKey.OpenBaseKey(hive, view);
        foreach (var registryPath in MotorCityRegistryPaths)
        {
            using var root = baseKey.OpenSubKey(registryPath);
            if (root is null)
            {
                continue;
            }

            foreach (var path in ReadStringValuesRecursively(root))
            {
                var candidate = NormalizePossiblePath(path);
                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    yield return candidate;
                }
            }
        }
    }

    private static IEnumerable<string> ReadUninstallInstallPaths()
    {
        foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
        {
            foreach (var view in new[] { RegistryView.Registry32, RegistryView.Registry64 })
            {
                IEnumerable<string> paths;
                try
                {
                    paths = ReadUninstallInstallPaths(hive, view).ToArray();
                }
                catch
                {
                    continue;
                }

                foreach (var path in paths)
                {
                    yield return path;
                }
            }
        }
    }

    private static IEnumerable<string> ReadUninstallInstallPaths(RegistryHive hive, RegistryView view)
    {
        using var baseKey = RegistryKey.OpenBaseKey(hive, view);
        using var uninstall = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
        if (uninstall is null)
        {
            yield break;
        }

        foreach (var subKeyName in uninstall.GetSubKeyNames())
        {
            using var subKey = uninstall.OpenSubKey(subKeyName);
            if (subKey is null || !IsMotorCityDisplayName(subKey.GetValue("DisplayName") as string))
            {
                continue;
            }

            foreach (var valueName in new[] { "InstallLocation", "DisplayIcon", "UninstallString", "InstallSource" })
            {
                var candidate = NormalizePossiblePath(subKey.GetValue(valueName) as string);
                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    yield return candidate;
                }
            }
        }
    }

    private static bool IsMotorCityDisplayName(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return false;
        }

        return displayName.Contains("Motor City", StringComparison.OrdinalIgnoreCase) ||
               displayName.Equals("MCO", StringComparison.OrdinalIgnoreCase) ||
               displayName.Contains("MotorCity", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> ReadStringValuesRecursively(RegistryKey key)
    {
        foreach (var valueName in key.GetValueNames())
        {
            if (key.GetValue(valueName) is string value && !string.IsNullOrWhiteSpace(value))
            {
                yield return value;
            }
        }

        foreach (var subKeyName in key.GetSubKeyNames())
        {
            using var subKey = key.OpenSubKey(subKeyName);
            if (subKey is null)
            {
                continue;
            }

            foreach (var value in ReadStringValuesRecursively(subKey))
            {
                yield return value;
            }
        }
    }

    private static string? NormalizePossiblePath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        foreach (var possiblePath in ExtractPossiblePaths(value))
        {
            var normalized = NormalizeDirectoryPath(possiblePath);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                return normalized;
            }
        }

        return null;
    }

    private static IEnumerable<string> ExtractPossiblePaths(string value)
    {
        var expanded = Environment.ExpandEnvironmentVariables(value);
        var quoted = Regex.Matches(expanded, "\"([^\"]+)\"");
        foreach (Match match in quoted)
        {
            yield return match.Groups[1].Value;
        }

        var trimmed = expanded.Trim().Trim('"');
        trimmed = Regex.Replace(trimmed, @",[-\d]+$", string.Empty);
        yield return trimmed;

        var exeIndex = trimmed.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        if (exeIndex >= 0)
        {
            yield return trimmed[..(exeIndex + 4)];
        }
    }

    private static string? NormalizeDirectoryPath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim().Trim('"');
        if (trimmed.Contains("://", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (File.Exists(trimmed))
        {
            return Path.GetDirectoryName(trimmed);
        }

        if (Directory.Exists(trimmed))
        {
            return Path.GetFullPath(trimmed.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        }

        return null;
    }
}
