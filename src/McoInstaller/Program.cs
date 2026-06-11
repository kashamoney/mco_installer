namespace McoInstaller;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        if (!AdminUtil.IsAdministrator() && AdminUtil.TryRestartElevated())
        {
            return;
        }

        Application.Run(new MainForm());
    }
}
