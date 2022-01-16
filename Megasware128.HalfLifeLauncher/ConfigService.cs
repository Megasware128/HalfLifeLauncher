using Microsoft.Extensions.Options;

class ConfigService : BackgroundService
{
    private readonly ConfigOptions _options;
    private readonly IConfiguration _config;
    private readonly ILogger _logger;

    public ConfigService(IOptions<ConfigOptions> options, IConfiguration config, ILogger<ConfigService> logger)
    {
        _options = options.Value;
        _config = config;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        switch (_options.Config)
        {
            case ConfigArgument.Get:
                _logger.LogInformation("Half-Life directory: {HalfLifeDirectory}", _config["HalfLifeDirectory"]);
                _logger.LogInformation("Steam directory: {SteamDirectory}", _config["SteamDirectory"]);
                break;
            case ConfigArgument.Set:
                _config["HalfLifeDirectory"] = _options.HalfLifeDirectory;
                _config["SteamDirectory"] = _options.SteamDirectory;
                break;
        }

        return Task.CompletedTask;
    }
}
