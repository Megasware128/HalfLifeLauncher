using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Completions;
using System.CommandLine.Hosting;
using System.CommandLine.NamingConventionBinder;
using System.CommandLine.Parsing;

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
    var dir = new DirectoryInfo(Path.Combine(config["HalfLifeDirectory"], game ?? new LaunchOptions().Game));
    var liblist = dir.EnumerateFiles("liblist.gam").First();
    var lines = File.ReadAllLines(liblist.FullName);
    var startmap = lines.First(l => l.StartsWith("startmap", StringComparison.OrdinalIgnoreCase));
    return startmap.Substring(startmap.IndexOf(' ') + 1).Replace(".bsp", "");
}

IEnumerable<string> GetMapCompletions(CompletionContext context)
{
    var game = context.ParseResult.GetValueForOption<string>(gameOption);
    var mapsDir = new DirectoryInfo(Path.Combine(hlDir.FullName, game ?? new LaunchOptions().Game, "maps"));
    var mapFiles = mapsDir.EnumerateFiles("*.bsp");
    return mapFiles.Select(f => f.Name.Replace(".bsp", ""));
}

var command = new RootCommand("Launcher for Half-Life")
{
    new Option<string>(new[] { "--map", "-m" }, GetDefaultMap, "The map to play").AddCompletions(GetMapCompletions),
    gameOption,
    new Option<int>(new[] { "--maxplayers", "-mp" }, () => new LaunchOptions().MaxPlayers, "The maximum number of players"),
    new Option<bool>(new[] { "--lan", "-l" }, () => new LaunchOptions().Lan, "Whether to play in LAN mode")
};

command.AddCommand(new Command("ipaddress", "Get the IP address of the local machine")
{
    new Option<bool>(new[] { "--local", "-l" }, () => false, "Whether to get the local IP address")
});

command.Handler = CommandHandler.Create(() => { }); // Empty handler to prevent subcommands from being required

return await new CommandLineBuilder(command)
    .UseHost(args => Host.CreateDefaultBuilder(args), host => host
        .ConfigureAppConfiguration(config =>
        {
            config.SetBasePath(Path.GetDirectoryName(typeof(Program).Assembly.Location));
        })
        .ConfigureServices((hostContext, services) =>
        {
            services.AddHttpClient();
            services.AddHostedService<LogIpAddressService>();
            if (command.Parse(args).CommandResult.Command.Name == "ipaddress")
            {
                services.AddOptions<IpAddressOptions>().BindCommandLine();
                return;
            }
            services.AddOptions<LaunchOptions>().Bind(hostContext.Configuration).BindCommandLine();
            services.AddHostedService<LaunchService>();
        }))
    .UseDefaults()
    .Build()
    .InvokeAsync(args);
