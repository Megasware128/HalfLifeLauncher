using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureLogging(logging =>
    {
        if (args[0] == "complete")
        {
            logging.ClearProviders();
        }
    })
    .ConfigureAppConfiguration((hostingContext, config) =>
    {
        if (args[0] == "complete")
        {
            config.AddCommandLine(args[1].Split(' '));
        }
    })
    .ConfigureServices((hostContext, services) =>
    {
        services.Configure<LaunchOptions>(hostContext.Configuration);

        if (args[0] == "complete")
        {
            services.AddHostedService<CompleteService>();
        }
        else
        {
            services.AddHostedService<LaunchService>();
        }
    })
    .Build();

await host.RunAsync();

class CompleteService : BackgroundService
{
    private readonly LaunchOptions launchOptions;
    private readonly IHostApplicationLifetime lifetime;

    public CompleteService(IOptions<LaunchOptions> launchOptions, IHostApplicationLifetime lifetime)
    {
        this.launchOptions = launchOptions.Value;
        this.lifetime = lifetime;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var dir = new DirectoryInfo(@"C:\Program Files (x86)\Steam\steamapps\common\Half-Life\decay\maps");
        var files = dir.GetFiles("*.bsp");
        foreach (var file in files.Where(x => x.Name.StartsWith(launchOptions.Map)))
        {
            Console.WriteLine(file.Name.Replace(".bsp", string.Empty));
        }

        lifetime.StopApplication();
        return Task.CompletedTask;
    }
}

class LaunchOptions
{
    public string Map { get; set; } = "dy_accident1";
    public string Game { get; set; } = "decay";
    public int MaxPlayers { get; set; } = 3;
    public bool Lan { get; set; }
}

class LaunchService : BackgroundService
{
    private readonly LaunchOptions launchOptions;
    private readonly IHostApplicationLifetime lifetime;

    public LaunchService(IOptions<LaunchOptions> launchOptions, IHostApplicationLifetime lifetime)
    {
        this.launchOptions = launchOptions.Value;
        this.lifetime = lifetime;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var steam = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = @"C:\Program Files (x86)\Steam\Steam.exe",
                Arguments = $"-applaunch 70 -game {launchOptions.Game} +sv_lan {Convert.ToByte(launchOptions.Lan)} +maxplayers {launchOptions.MaxPlayers} +map {launchOptions.Map}"
            }
        };

        steam.Start();

        lifetime.StopApplication();
        return Task.CompletedTask;
    }
}
