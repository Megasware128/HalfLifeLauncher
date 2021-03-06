using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Options;

namespace Megasware128.HalfLifeLauncher.Services;

class LogIpAddressService : BackgroundService
{
    private readonly ILogger logger;
    private readonly HttpClient httpClient;
    private readonly IpAddressOptions ipAddressOptions;

    public LogIpAddressService(ILogger<LogIpAddressService> logger, HttpClient httpClient, IOptions<IpAddressOptions> ipAddressOptions)
    {
        this.logger = logger;
        this.httpClient = httpClient;
        this.ipAddressOptions = ipAddressOptions.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (ipAddressOptions.Local)
        {
            logger.LogInformation("Local IP address: {LocalIp}", await GetLocalIpAddressAsync());
        }
        else
        {
            logger.LogInformation("Public IP address: {PublicIp}", await GetPublicIpAddressAsync());
        }
    }

    private static async Task<IPAddress?> GetLocalIpAddressAsync()
    {
        var host = await Dns.GetHostEntryAsync(Dns.GetHostName());
        return host.AddressList.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);
    }

    private async Task<string> GetPublicIpAddressAsync() => await httpClient.GetStringAsync("https://api.ipify.org?format=text");
}
