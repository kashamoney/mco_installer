namespace McoInstaller;

public sealed class InstallOptions
{
    public string InstallPath { get; init; } = @"C:\Program Files (x86)\EA Games\Motor City Online";
    public bool ApplyUpdate { get; init; } = true;
    public bool InstallCertificate { get; init; } = true;
    public bool PatchRegistry { get; init; } = true;
    public bool LaunchGame { get; init; }
}
