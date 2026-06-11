using System.Diagnostics;
using System.IO.Compression;

namespace McoInstaller;

public sealed class InstallerEngine
{
    private readonly Action<string> _log;

    public InstallerEngine(Action<string> log)
    {
        _log = log;
    }

    public async Task RunAsync(InstallOptions options, PayloadPackage payload, CancellationToken cancellationToken)
    {
        _log("Starting setup...");
        ValidateOptions(options, payload);

        var installPath = ResolveInstallPath(options.InstallPath);
        _log($"Using game folder: {installPath}");

        if (options.ApplyUpdate)
        {
            ApplyUpdate(payload, installPath);
        }

        CopyPublicKeyIfPresent(payload, installPath);

        if (options.InstallCertificate)
        {
            if (payload.HasCertificate)
            {
                CertificateInstaller.InstallTrustedRoot(payload.CertificatePath!, _log);
            }
            else
            {
                _log("No server certificate bundled; skipping certificate install.");
            }
        }

        if (options.PatchRegistry)
        {
            RegistryPatcher.Patch(payload.Settings, installPath, _log);
        }

        if (options.InstallRadmin)
        {
            await RunRadminInstallerIfPresentAsync(payload, cancellationToken);
            _log($"Radmin VPN network: {payload.Settings.RadminNetworkName}");
            _log($"Radmin VPN password: {payload.Settings.RadminNetworkPassword}");
        }

        if (options.LaunchGame)
        {
            LaunchGame(installPath, payload.Settings);
        }

        _log("Setup complete.");
    }

    private static void ValidateOptions(InstallOptions options, PayloadPackage payload)
    {
        if (options.ApplyUpdate && !payload.HasUpdate)
        {
            throw new InvalidOperationException("The update payload is missing. Add payload\\update.zip or payload\\update\\ and rebuild.");
        }
    }

    private string ResolveInstallPath(string preferredPath)
    {
        var path = GameInstallLocator.NormalizeInstallDirectory(preferredPath);
        if (path is null || !GameInstallLocator.IsGameInstallDirectory(path))
        {
            throw new InvalidOperationException("Could not find the game install folder. Choose the folder that contains MCity.exe.");
        }

        return path;
    }

    private void ApplyUpdate(PayloadPackage payload, string installPath)
    {
        if (payload.UpdateZipPath is not null && File.Exists(payload.UpdateZipPath))
        {
            _log($"Applying update archive: {Path.GetFileName(payload.UpdateZipPath)}");
            var temp = Path.Combine(Path.GetTempPath(), "McoInstallerUpdate", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);

            try
            {
                ZipFile.ExtractToDirectory(payload.UpdateZipPath, temp);
                var source = SelectUpdateRoot(temp);
                CopyDirectory(source, installPath);
            }
            finally
            {
                if (Directory.Exists(temp))
                {
                    Directory.Delete(temp, recursive: true);
                }
            }

            _log("Update archive applied.");
            return;
        }

        if (payload.UpdateDirectoryPath is not null && Directory.Exists(payload.UpdateDirectoryPath))
        {
            _log("Applying update folder...");
            CopyDirectory(payload.UpdateDirectoryPath, installPath);
            _log("Update folder applied.");
        }
    }

    private static string SelectUpdateRoot(string extractionRoot)
    {
        var nestedUpdateDirectory = Path.Combine(extractionRoot, "update");
        if (Directory.Exists(nestedUpdateDirectory))
        {
            return nestedUpdateDirectory;
        }

        if (LooksLikeGameRoot(extractionRoot))
        {
            return extractionRoot;
        }

        var directories = Directory.GetDirectories(extractionRoot);
        var files = Directory.GetFiles(extractionRoot);

        var gameRoot = directories.FirstOrDefault(LooksLikeGameRoot);
        if (gameRoot is not null)
        {
            return gameRoot;
        }

        if (directories.Length == 1 && files.Length == 0)
        {
            return directories[0];
        }

        return extractionRoot;
    }

    private static bool LooksLikeGameRoot(string directory)
    {
        return File.Exists(Path.Combine(directory, "MCity.exe")) ||
               File.Exists(Path.Combine(directory, "mcity.exe")) ||
               Directory.Exists(Path.Combine(directory, "GameData")) ||
               Directory.Exists(Path.Combine(directory, "Data"));
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);

        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDirectory, directory);
            Directory.CreateDirectory(Path.Combine(destinationDirectory, relative));
        }

        foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDirectory, file);
            var destination = Path.Combine(destinationDirectory, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            ClearReadOnly(destination);
            File.Copy(file, destination, overwrite: true);
        }
    }

    private static void ClearReadOnly(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReadOnly) != 0)
        {
            File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
        }
    }

    private void CopyPublicKeyIfPresent(PayloadPackage payload, string installPath)
    {
        if (!payload.HasPublicKey)
        {
            _log("No public key bundled; leaving the installed pub.key as-is.");
            return;
        }

        var destination = Path.Combine(installPath, "pub.key");
        ClearReadOnly(destination);
        File.Copy(payload.PublicKeyPath!, destination, overwrite: true);
        _log($"Copied {Path.GetFileName(payload.PublicKeyPath)} to pub.key.");
    }

    private async Task RunRadminInstallerIfPresentAsync(PayloadPackage payload, CancellationToken cancellationToken)
    {
        if (!payload.HasRadminInstaller)
        {
            _log("No Radmin VPN installer bundled; install Radmin VPN manually if needed.");
            return;
        }

        if (InstalledPrograms.IsInstalled("Radmin VPN"))
        {
            _log("Radmin VPN already appears to be installed.");
            return;
        }

        var logPath = Path.Combine(Path.GetTempPath(), "McoInstaller-RadminVPN-Install.log");
        _log("Installing Radmin VPN silently...");
        _log($"Radmin install log: {logPath}");

        var exitCode = await ProcessRunner.RunAsync(
            payload.RadminInstallerPath!,
            Path.GetDirectoryName(payload.RadminInstallerPath!),
            [
                "/VERYSILENT",
                "/SUPPRESSMSGBOXES",
                "/NORESTART",
                "/SP-",
                "/LOG=" + logPath
            ],
            _log,
            cancellationToken);

        if (exitCode == 0 || InstalledPrograms.IsInstalled("Radmin VPN"))
        {
            _log("Radmin VPN install complete.");
            return;
        }

        _log($"Silent Radmin VPN install exited with code {exitCode}; opening the installer normally.");
        await ProcessRunner.RunInteractiveAsync(payload.RadminInstallerPath!, Path.GetDirectoryName(payload.RadminInstallerPath!), _log, cancellationToken);
    }

    private void LaunchGame(string installPath, ServerSettings settings)
    {
        var executable = GameInstallLocator.FindGameExecutable(installPath, settings);
        if (executable is null)
        {
            _log("Could not find MCity.exe to launch.");
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = installPath,
            UseShellExecute = true
        });

        _log("Launched Motor City Online.");
    }
}
