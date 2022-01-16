using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Completions;
using System.CommandLine.Hosting;
using System.CommandLine.NamingConventionBinder;
using System.CommandLine.Parsing;
using System.Text;
using Microsoft.Extensions.Configuration.Ini;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

var settingsFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Megasware128", "HalfLifeLauncher", "settings.ini");

var config = new ConfigurationBuilder()
    .SetBasePath(Path.GetDirectoryName(typeof(Program).Assembly.Location))
    .AddJsonFile("appsettings.json")
    .AddSettingsFile(settingsFile, true)
    .Build();

var hlDir = new DirectoryInfo(config["HalfLifeDirectory"]);

var gameOption = new Option<string>(new[] { "--game", "-g" }, () => new LaunchOptions().Game, "The game to play")
    .AddCompletions(c => hlDir.EnumerateDirectories().Where(d => d.EnumerateFiles().Any(f => f.Name == "liblist.gam")).Select(d => d.Name));

string GetDefaultMap()
{
    var game = new RootCommand { gameOption }.Parse(args).GetValueForOption(gameOption);
    var path = Path.Combine(hlDir.FullName, game ?? new LaunchOptions().Game);
    if (!Directory.Exists(path))
    {
        return string.Empty;
    }
    var dir = new DirectoryInfo(path);
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

            config.AddSettingsFile(settingsFile, true);
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

class ConfigOptions
{
    public string HalfLifeDirectory { get; set; }
    public string SteamDirectory { get; set; }
}

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

        if (_stream is null)
        {
            var path = Source.FileProvider.GetFileInfo(Source.Path).PhysicalPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            _stream = File.OpenWrite(path);
            _stream.SetLength(0);
            _stream.Seek(0, SeekOrigin.Begin);
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

static class SettingsConfigurationExtensions
{
    public static IConfigurationBuilder AddSettingsFile(this IConfigurationBuilder builder, string path)
    {
        return AddSettingsFile(builder, provider: null, path: path, optional: false, reloadOnChange: false);
    }

    public static IConfigurationBuilder AddSettingsFile(this IConfigurationBuilder builder, string path, bool optional)
    {
        return AddSettingsFile(builder, provider: null, path: path, optional: optional, reloadOnChange: false);
    }

    public static IConfigurationBuilder AddSettingsFile(this IConfigurationBuilder builder, string path, bool optional, bool reloadOnChange)
    {
        return AddSettingsFile(builder, provider: null, path: path, optional: optional, reloadOnChange: reloadOnChange);
    }

    public static IConfigurationBuilder AddSettingsFile(this IConfigurationBuilder builder, IFileProvider? provider, string path, bool optional, bool reloadOnChange)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }
        if (string.IsNullOrEmpty(path))
        {
            throw new ArgumentException("Path cannot be null or empty.", nameof(path));
        }

        return builder.AddIniFile(s =>
        {
            s.FileProvider = provider;
            s.Path = path;
            s.Optional = optional;
            s.ReloadOnChange = reloadOnChange;
            s.ResolveFileProvider();
        });
    }

    public static IConfigurationBuilder AddSettingsFile(this IConfigurationBuilder builder, Action<IniConfigurationSource> configureSource)
        => builder.Add(configureSource);
}
