using System.Reflection;
using System.Text.Json;

namespace McoInstaller;

public sealed class PayloadPackage
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private PayloadPackage(string rootDirectory, bool isExternal, ServerSettings settings)
    {
        RootDirectory = rootDirectory;
        IsExternal = isExternal;
        Settings = settings;
    }

    public string RootDirectory { get; }
    public bool IsExternal { get; }
    public ServerSettings Settings { get; }
    public string? UpdateZipPath { get; private set; }
    public string? UpdateDirectoryPath { get; private set; }
    public string? CertificatePath { get; private set; }
    public string? PublicKeyPath { get; private set; }
    public string? RadminInstallerPath { get; private set; }

    public bool HasUpdate => File.Exists(UpdateZipPath) || Directory.Exists(UpdateDirectoryPath);
    public bool HasCertificate => File.Exists(CertificatePath);
    public bool HasPublicKey => File.Exists(PublicKeyPath);
    public bool HasRadminInstaller => File.Exists(RadminInstallerPath);

    public static PayloadPackage Load(Action<string>? log = null)
    {
        var external = FindExternalPayloadDirectory();
        if (external is not null)
        {
            log?.Invoke($"Using external payload folder: {external}");
            return FromDirectory(external, isExternal: true);
        }

        var extracted = ExtractEmbeddedPayload(log);
        log?.Invoke($"Using embedded payload: {extracted}");
        return FromDirectory(extracted, isExternal: false);
    }

    public string Describe()
    {
        var parts = new List<string>
        {
            HasUpdate ? "update: found" : "update: missing",
            HasCertificate ? "certificate: found" : "certificate: missing",
            HasPublicKey ? "pub.key: found" : "pub.key: missing",
            HasRadminInstaller ? "Radmin VPN: bundled" : "Radmin VPN: not bundled"
        };

        return string.Join(" | ", parts);
    }

    private static PayloadPackage FromDirectory(string rootDirectory, bool isExternal)
    {
        var settings = LoadSettings(rootDirectory);
        var package = new PayloadPackage(rootDirectory, isExternal, settings)
        {
            UpdateZipPath = FindFirstFile(rootDirectory, "update.zip", "mco-update.zip", "sunset-update.zip"),
            UpdateDirectoryPath = FindFirstDirectory(rootDirectory, "update"),
            CertificatePath = FindFirstFile(rootDirectory, "server.crt", "server.cer"),
            PublicKeyPath = FindFirstFile(rootDirectory, "pub.key", "pubori.key"),
            RadminInstallerPath = FindFirstMatchingFile(rootDirectory, static name =>
                name.Contains("radmin", StringComparison.OrdinalIgnoreCase) &&
                name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        };

        return package;
    }

    private static ServerSettings LoadSettings(string rootDirectory)
    {
        var path = Path.Combine(rootDirectory, "server.json");
        if (!File.Exists(path))
        {
            return new ServerSettings();
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<ServerSettings>(json, JsonOptions) ?? new ServerSettings();
    }

    private static string? FindExternalPayloadDirectory()
    {
        var baseDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDir, "payload"),
            Path.Combine(Environment.CurrentDirectory, "payload")
        };

        return candidates.FirstOrDefault(IsUsablePayloadDirectory);
    }

    private static bool IsUsablePayloadDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return false;
        }

        return File.Exists(Path.Combine(path, "server.json")) ||
               Directory.EnumerateFileSystemEntries(path).Any();
    }

    private static string ExtractEmbeddedPayload(Action<string>? log)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resources = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith("payload/", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var root = Path.Combine(Path.GetTempPath(), "McoInstallerPayload", assembly.GetName().Version?.ToString() ?? "dev");
        ResetTempDirectory(root);

        foreach (var resourceName in resources)
        {
            var relative = resourceName["payload/".Length..].Replace('/', Path.DirectorySeparatorChar);
            if (string.IsNullOrWhiteSpace(relative))
            {
                continue;
            }

            var destination = Path.GetFullPath(Path.Combine(root, relative));
            if (!destination.StartsWith(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase))
            {
                log?.Invoke($"Skipped suspicious embedded resource: {resourceName}");
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            using var input = assembly.GetManifestResourceStream(resourceName);
            if (input is null)
            {
                continue;
            }

            using var output = File.Create(destination);
            input.CopyTo(output);
        }

        if (!resources.Any())
        {
            Directory.CreateDirectory(root);
        }

        return root;
    }

    private static void ResetTempDirectory(string root)
    {
        var fullRoot = Path.GetFullPath(root);
        var tempRoot = Path.GetFullPath(Path.GetTempPath());
        if (!fullRoot.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Refusing to reset a payload directory outside the temp folder.");
        }

        if (Directory.Exists(fullRoot))
        {
            Directory.Delete(fullRoot, recursive: true);
        }

        Directory.CreateDirectory(fullRoot);
    }

    private static string? FindFirstDirectory(string rootDirectory, params string[] names)
    {
        foreach (var name in names)
        {
            var path = Path.Combine(rootDirectory, name);
            if (Directory.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    private static string? FindFirstFile(string rootDirectory, params string[] names)
    {
        foreach (var name in names)
        {
            var path = Path.Combine(rootDirectory, name);
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    private static string? FindFirstMatchingFile(string rootDirectory, Func<string, bool> predicate)
    {
        if (!Directory.Exists(rootDirectory))
        {
            return null;
        }

        return Directory.EnumerateFiles(rootDirectory, "*", SearchOption.TopDirectoryOnly)
            .FirstOrDefault(path => predicate(Path.GetFileName(path)));
    }
}
