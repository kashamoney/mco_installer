using System.Diagnostics;

namespace McoInstaller;

internal static class ProcessRunner
{
    public static async Task<int> RunAsync(
        string fileName,
        string? workingDirectory,
        IEnumerable<string> arguments,
        Action<string> log,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory ?? Path.GetDirectoryName(fileName) ?? Environment.CurrentDirectory
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            throw new InvalidOperationException($"Could not start {fileName}.");
        }

        log($"Waiting for {Path.GetFileName(fileName)} to exit...");
        await process.WaitForExitAsync(cancellationToken);
        return process.ExitCode;
    }

    public static async Task<int> RunInteractiveAsync(string fileName, string? workingDirectory, Action<string> log, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = true,
            WorkingDirectory = workingDirectory ?? Path.GetDirectoryName(fileName) ?? Environment.CurrentDirectory
        };

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            throw new InvalidOperationException($"Could not start {fileName}.");
        }

        log($"Waiting for {Path.GetFileName(fileName)} to exit...");
        await process.WaitForExitAsync(cancellationToken);
        return process.ExitCode;
    }
}
