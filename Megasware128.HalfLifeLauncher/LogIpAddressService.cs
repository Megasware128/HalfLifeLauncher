using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Options;

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
            logger.LogInformation($"Local IP address: {await GetLocalIpAddressAsync()}");
        }
        else
        {
            logger.LogInformation($"Public IP address: {await GetPublicIpAddressAsync()}");
        }
    }

    private async Task<IPAddress> GetLocalIpAddressAsync()
    {
        var host = await Dns.GetHostEntryAsync(Dns.GetHostName());
        return host.AddressList.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);
    }

    private async Task<string> GetPublicIpAddressAsync()
    {
        return await httpClient.GetStringAsync("https://api.ipify.org?format=text");
    }
}
