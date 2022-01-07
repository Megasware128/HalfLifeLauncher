using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Hosting;
using System.CommandLine.NamingConventionBinder;
using System.CommandLine.Parsing;
using System.Diagnostics;
using Microsoft.Extensions.Options;

var hlDir = new DirectoryInfo($@"C:\Program Files (x86)\Steam\steamapps\common\Half-Life");

var gameOption = new Option<string>(new[] { "--game", "-g" }, () => new LaunchOptions().Game, "The game to play")
    .AddCompletions(c => hlDir.EnumerateDirectories().Where(d => d.EnumerateFiles().Any(f => f.Name == "liblist.gam")).Select(d => d.Name));

var command = new RootCommand("Launcher for Half-Life")
{
    new Option<string>(new[] { "--map", "-m" }, () => new LaunchOptions().Map, "The map to play").AddCompletions(c =>
    {
        var game = c.ParseResult.GetValueForOption<string>(gameOption);
        var mapsDir = new DirectoryInfo($@"{hlDir.FullName}\{game ?? new LaunchOptions().Game}\maps");
        var mapFiles = mapsDir.EnumerateFiles("*.bsp");
        return mapFiles.Select(f => f.Name.Replace(".bsp", ""));
    }),
    gameOption,
    new Option<int>(new[] { "--maxplayers", "-mp" }, () => new LaunchOptions().MaxPlayers, "The maximum number of players"),
    new Option<bool>(new[] { "--lan", "-l" }, () => new LaunchOptions().Lan, "Whether to play in LAN mode")
};

command.AddCommand(new Command("ipaddress"));

command.Handler = CommandHandler.Create<IHost, CancellationToken>((host, ct) => host.RunAsync(ct));

return await new CommandLineBuilder(command)
    .UseHost(args => Host.CreateDefaultBuilder(args), host => host
        .ConfigureAppConfiguration(config =>
        {
            config.SetBasePath(Path.GetDirectoryName(typeof(Program).Assembly.Location));
        })
        .ConfigureServices((hostContext, services) =>
        {
            services.AddHttpClient<LogIpAddressService>();
            services.AddHostedService<LogIpAddressService>();
            if (command.Parse(args).CommandResult.Command.Name == "ipaddress")
            {
                return;
            }
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

class LogIpAddressService : BackgroundService
{
    private readonly ILogger logger;
    private readonly HttpClient httpClient;

    public LogIpAddressService(ILogger<LogIpAddressService> logger, HttpClient httpClient)
    {
        this.logger = logger;
        this.httpClient = httpClient;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Get external ipv4 address and log it
        var externalIp = await httpClient.GetStringAsync("https://api.ipify.org?format=text");
        logger.LogInformation($"External IP: {externalIp}");
    }
}

class LaunchService : BackgroundService
{
    private readonly LaunchOptions launchOptions;
    private readonly IHostApplicationLifetime lifetime;

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
                FileName = @"C:\Program Files (x86)\Steam\Steam.exe",
                Arguments = $"-applaunch 70 -game {launchOptions.Game} +sv_lan {Convert.ToByte(launchOptions.Lan)} +maxplayers {launchOptions.MaxPlayers} +map {launchOptions.Map}"
            }
        };

        steam.Start();

        return Task.CompletedTask;
    }
}
