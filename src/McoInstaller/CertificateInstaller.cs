using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;

namespace McoInstaller;

internal static class CertificateInstaller
{
    public static void InstallTrustedRoot(string certificatePath, Action<string> log)
    {
        if (!File.Exists(certificatePath))
        {
            throw new FileNotFoundException("Certificate file not found.", certificatePath);
        }

        using var certificate = new X509Certificate2(certificatePath);
        log($"Certificate subject: {certificate.Subject}");
        log($"Certificate thumbprint: {certificate.Thumbprint}");
        log($"Installing certificate to LocalMachine\\Root (Trusted Root Certification Authorities)...");

        if (IsInstalledInLocalMachineRoot(certificate))
        {
            log("Trusted root certificate already installed.");
            return;
        }

        ImportWithCertUtil(certificatePath, log);

        if (!IsInstalledInLocalMachineRoot(certificate))
        {
            throw new InvalidOperationException("certutil finished, but Windows did not report the certificate in LocalMachine\\Root afterward.");
        }

        log("Installed trusted root certificate.");
    }

    private static bool IsInstalledInLocalMachineRoot(X509Certificate2 certificate)
    {
        using var store = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
        store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);

        var existing = store.Certificates.Find(X509FindType.FindByThumbprint, certificate.Thumbprint, validOnly: false);
        return existing.Count > 0;
    }

    private static void ImportWithCertUtil(string certificatePath, Action<string> log)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "certutil.exe",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        startInfo.ArgumentList.Add("-f");
        startInfo.ArgumentList.Add("-addstore");
        startInfo.ArgumentList.Add("Root");
        startInfo.ArgumentList.Add(certificatePath);

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            throw new InvalidOperationException("Could not start certutil.exe.");
        }

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        LogCertUtilOutput(output, log);
        LogCertUtilOutput(error, log);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"certutil failed to add the certificate to LocalMachine\\Root. Exit code: {process.ExitCode}.");
        }

        log("certutil import completed.");
    }

    private static void LogCertUtilOutput(string output, Action<string> log)
    {
        foreach (var line in output.Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            log("certutil: " + line);
        }
    }
}
