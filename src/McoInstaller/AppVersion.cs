using System.Reflection;

namespace McoInstaller;

internal static class AppVersion
{
    public static string Informational { get; } = GetInformationalVersion();
    public static string Display => "v" + Informational;

    private static string GetInformationalVersion()
    {
        var version = typeof(AppVersion).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(version))
        {
            return version;
        }

        return typeof(AppVersion).Assembly.GetName().Version?.ToString(3) ?? "dev";
    }
}
