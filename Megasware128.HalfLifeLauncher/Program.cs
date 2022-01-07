using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Hosting;
using System.CommandLine.NamingConventionBinder;
using System.CommandLine.Parsing;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

var hlDir = new DirectoryInfo($@"C:\Program Files (x86)\Steam\steamapps\common\Half-Life");

var gameOption = new Option<string>(new[] { "--game", "-g" }, () => new LaunchOptions().Game, "The game to play")
    .AddCompletions(c => hlDir.EnumerateDirectories().Where(d => d.EnumerateFiles().Any(f => f.Name == "liblist.gam")).Select(d => d.Name));

var command = new RootCommand
{
    new Option<string>(new[] { "--map", "-m" }, () => new LaunchOptions().Map, "The map to play").AddCompletions(c =>
    {
        var game = c.ParseResult.GetValueForOption<string>(gameOption);
        var mapsDir = new DirectoryInfo($@"{hlDir.FullName}\{game}\maps");
        var mapFiles = mapsDir.EnumerateFiles("*.bsp");
        return mapFiles.Select(f => f.Name.Replace(".bsp", ""));
    }),
    gameOption,
    new Option<int>(new[] { "--maxplayers", "-mp" }, () => new LaunchOptions().MaxPlayers, "The maximum number of players"),
    new Option<bool>(new[] { "--lan", "-l" }, () => new LaunchOptions().Lan, "Whether to play in LAN mode")
};

command.Handler = CommandHandler.Create<IHost, CancellationToken>((host, ct) => host.RunAsync(ct));

return await new CommandLineBuilder(command)
    .UseHost(args => Host.CreateDefaultBuilder(args), host => host
        .ConfigureServices((hostContext, services) =>
        {
            services.Configure<LaunchOptions>(hostContext.Configuration);
            services.AddHostedService<LaunchService>();
        }))
    .UseDefaults()
    .Build()
    .InvokeAsync(args);

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
