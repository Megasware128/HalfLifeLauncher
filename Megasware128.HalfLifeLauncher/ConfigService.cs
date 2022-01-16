using Microsoft.Extensions.Options;

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
