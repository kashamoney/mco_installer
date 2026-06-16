namespace McoInstaller;

public sealed class ServerSettings
{
    public string ServerName { get; set; } = "Motor City Online";
    public string ServerIp { get; set; } = "51.79.84.64";
    public int PatchServerPort { get; set; } = 80;
    public string CreateAccountUrl { get; set; } = "51.79.84.64/SubscribeEntry.jsp?prodID=REG-MCO";
    public string ShardUrl { get; set; } = "http://51.79.84.64/ShardList/";
    public string ShardUrlDev { get; set; } = "http://51.79.84.64/ShardList/";
    public string TickerUrl { get; set; } = "http://rusty-motors.com/Ticker";
    public string RadminNetworkName { get; set; } = "Motor City Online";
    public string RadminNetworkPassword { get; set; } = "123456";
    public string PreferredInstallPath { get; set; } = @"C:\Program Files (x86)\EA Games\Motor City Online";
    public string GameExecutable { get; set; } = "MCity.exe";
}
