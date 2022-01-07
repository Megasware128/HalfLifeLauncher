using System.Diagnostics;
using Microsoft.Extensions.Options;

class LaunchService : BackgroundService
{
    private readonly LaunchOptions launchOptions;

    public LaunchService(IOptions<LaunchOptions> launchOptions)
    {
        this.launchOptions = launchOptions.Value;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var steam = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(launchOptions.SteamDirectory, "steam.exe"),
                Arguments = $"-applaunch 70 -game {launchOptions.Game} +sv_lan {Convert.ToByte(launchOptions.Lan)} +maxplayers {launchOptions.MaxPlayers} +map {launchOptions.Map}"
            }
        };

        steam.Start();

        return Task.CompletedTask;
    }
}
