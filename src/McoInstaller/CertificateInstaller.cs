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
        using var store = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
        store.Open(OpenFlags.ReadWrite);

        var existing = store.Certificates.Find(X509FindType.FindByThumbprint, certificate.Thumbprint, validOnly: false);
        if (existing.Count > 0)
        {
            log($"Trusted root certificate already installed: {certificate.Subject}");
            return;
        }

        store.Add(certificate);
        log($"Installed trusted root certificate: {certificate.Subject}");
    }
}
