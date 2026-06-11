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
            LogCertificateValidation(certificate, log);
            return;
        }

        ImportWithCertUtil(certificatePath, log);

        if (!IsInstalledInLocalMachineRoot(certificate))
        {
            throw new InvalidOperationException("certutil finished, but Windows did not report the certificate in LocalMachine\\Root afterward.");
        }

        log("Installed trusted root certificate.");
        LogCertificateValidation(certificate, log);
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

    private static void LogCertificateValidation(X509Certificate2 certificate, Action<string> log)
    {
        var now = DateTime.Now;
        if (now < certificate.NotBefore || now > certificate.NotAfter)
        {
            log($"WARNING: Certificate is outside its validity period ({certificate.NotBefore:g} to {certificate.NotAfter:g}).");
        }

        if (!IsCertificateAuthority(certificate))
        {
            log("WARNING: Certificate is not marked as a certificate authority; root trust may not work as expected.");
        }

        using var rsa = certificate.GetRSAPublicKey();
        if (rsa is not null && rsa.KeySize < 2048)
        {
            log($"WARNING: Certificate RSA key is {rsa.KeySize} bits; modern Windows/TLS clients may reject it.");
        }

        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        var valid = chain.Build(certificate);
        if (valid)
        {
            log("Certificate validates in the Windows trust chain.");
            return;
        }

        var statuses = chain.ChainStatus
            .Select(status => $"{status.Status}: {status.StatusInformation.Trim()}")
            .Where(status => !string.IsNullOrWhiteSpace(status))
            .Distinct();
        log("WARNING: Certificate is installed, but Windows chain validation failed: " + string.Join("; ", statuses));
    }

    private static bool IsCertificateAuthority(X509Certificate2 certificate)
    {
        return certificate.Extensions
            .OfType<X509BasicConstraintsExtension>()
            .Any(extension => extension.CertificateAuthority);
    }
}
