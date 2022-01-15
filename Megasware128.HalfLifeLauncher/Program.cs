using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Completions;
using System.CommandLine.Hosting;
using System.CommandLine.NamingConventionBinder;
using System.CommandLine.Parsing;
using System.Text;
using Microsoft.Extensions.Configuration.Ini;
using Microsoft.Extensions.Options;

var config = new ConfigurationBuilder()
    .SetBasePath(Path.GetDirectoryName(typeof(Program).Assembly.Location))
    .AddJsonFile("appsettings.json")
    .Build();

var hlDir = new DirectoryInfo(config["HalfLifeDirectory"]);

var gameOption = new Option<string>(new[] { "--game", "-g" }, () => new LaunchOptions().Game, "The game to play")
    .AddCompletions(c => hlDir.EnumerateDirectories().Where(d => d.EnumerateFiles().Any(f => f.Name == "liblist.gam")).Select(d => d.Name));

string GetDefaultMap()
{
    var game = new RootCommand { gameOption }.Parse(args).GetValueForOption(gameOption);
    var dir = new DirectoryInfo(Path.Combine(hlDir.FullName, game ?? new LaunchOptions().Game));
    var liblist = dir.EnumerateFiles("liblist.gam").First();
    var lines = File.ReadAllLines(liblist.FullName);
    var startmap = lines.First(l => l.StartsWith("startmap", StringComparison.OrdinalIgnoreCase));
    return startmap.Substring(startmap.IndexOf(' ') + 1).Replace(".bsp", string.Empty);
}

IEnumerable<string> GetMapCompletions(CompletionContext context)
{
    var game = context.ParseResult.GetValueForOption<string>(gameOption);
    var mapsDir = new DirectoryInfo(Path.Combine(hlDir.FullName, game ?? new LaunchOptions().Game, "maps"));
    var mapFiles = mapsDir.EnumerateFiles("*.bsp");
    return mapFiles.Select(f => f.Name.Replace(".bsp", string.Empty));
}

var command = new RootCommand("Launcher for Half-Life")
{
    new Option<string>(new[] { "--map", "-m" }, GetDefaultMap, "The map to play").AddCompletions(GetMapCompletions),
    gameOption,
    new Option<int>(new[] { "--maxplayers", "-mp" }, () => new LaunchOptions().MaxPlayers, "The maximum number of players"),
    new Option<bool>(new[] { "--lan", "-l" }, () => new LaunchOptions().Lan, "Whether to play in LAN mode")
};

command.AddGlobalOption(new Option<bool>(new[] { "--local", "-l" }, () => false, "Whether to get the local IP address"));

command.AddCommand(new Command("ipaddress", "Get the IP address of the local machine"));

var configArgument = new Argument<ConfigArgument>("config", "Get or set the configuration");

var configCommand = new Command("config", "Get or set the configuration")
{
    configArgument,
    new Option<string>(new[] { "--half-life-directory", "-hl" }, () => config["HalfLifeDirectory"], "The directory containing Half-Life"),
    new Option<string>(new[] { "--steam-directory", "-s" }, () => config["SteamDirectory"], "The directory containing Steam")
};

command.AddCommand(configCommand);

command.Handler = CommandHandler.Create(() => { }); // Empty handler to prevent subcommands from being required

return await new CommandLineBuilder(command)
    .UseHost(args => Host.CreateDefaultBuilder(args), host => host
        .ConfigureAppConfiguration(config =>
        {
            config.SetBasePath(Path.GetDirectoryName(typeof(Program).Assembly.Location));

            config.Add<SettingsFileConfigurationSource>(builder =>
            {
                builder.Path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Megasware128", "HalfLifeLauncher", "settings.ini");
                builder.Optional = true;
            });
        })
        .ConfigureServices((hostContext, services) =>
        {
            var commandResult = command.Parse(args).CommandResult;

            if (commandResult.Command.Name == "config")
            {
                switch (commandResult.FindResultFor(configArgument)!.GetValueOrDefault<ConfigArgument>())
                {
                    case ConfigArgument.Get:
                        Console.WriteLine($"Half-Life directory: {config["HalfLifeDirectory"]}");
                        Console.WriteLine($"Steam directory: {config["SteamDirectory"]}");
                        break;
                    case ConfigArgument.Set:
                        services.AddHostedService<ConfigService>()
                                .AddOptions<ConfigOptions>()
                                .Bind(config)
                                .BindCommandLine();
                        break;
                }

                return;
            }

            services.AddHttpClient();
            services.AddHostedService<LogIpAddressService>().AddOptions<IpAddressOptions>().BindCommandLine();
            if (commandResult.Command.Name == "ipaddress")
            {
                return;
            }
            services.AddHostedService<LaunchService>()
                .AddOptions<LaunchOptions>()
                .Bind(hostContext.Configuration)
                .BindCommandLine();
        }))
    .UseDefaults()
    .Build()
    .InvokeAsync(args);

record ConfigOptions(string HalfLifeDirectory, string SteamDirectory);

class ConfigService : BackgroundService
{
    private readonly ConfigOptions _options;
    private readonly IConfiguration _config;

    public ConfigService(IOptions<ConfigOptions> options, IConfiguration config)
    {
        _options = options.Value;
        _config = config;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _config["HalfLifeDirectory"] = _options.HalfLifeDirectory;
        _config["SteamDirectory"] = _options.SteamDirectory;

        return Task.CompletedTask;
    }
}

enum ConfigArgument { Get, Set }

class SettingsFileConfigProvider : IniConfigurationProvider
{
    private FileStream? _stream;

    public SettingsFileConfigProvider(IniConfigurationSource source) : base(source)
    {
    }
    
    public override void Set(string key, string value)
    {
        base.Set(key, value);
        
        if(_stream is null)
        {
            _stream = File.OpenWrite(Source.Path);
            _stream.Position = 0;
        }

        _stream.Write(Encoding.UTF8.GetBytes($"{key}={value}\n"));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        
        _stream?.Dispose();
    }
}

class SettingsFileConfigurationSource : IniConfigurationSource
{
    public override IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        EnsureDefaults(builder);
        return new SettingsFileConfigProvider(this);
    }
}
