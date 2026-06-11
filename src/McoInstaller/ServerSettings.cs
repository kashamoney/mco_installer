namespace McoInstaller;

public sealed class ServerSettings
{
    public string ServerName { get; set; } = "Motor City Online";
    public string ServerIp { get; set; } = "26.226.71.102";
    public int PatchServerPort { get; set; } = 80;
    public string ShardUrl { get; set; } = "http://26.226.71.102/ShardList/";
    public string TickerUrl { get; set; } = "https://pastebin.com/raw/9h9sduQV";
    public string RadminNetworkName { get; set; } = "Motor City Online";
    public string RadminNetworkPassword { get; set; } = "123456";
    public string PreferredInstallPath { get; set; } = @"C:\Program Files (x86)\EA Games\Motor City Online";
    public string GameExecutable { get; set; } = "MCity.exe";
}
