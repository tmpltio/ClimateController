using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ClimateController.Devices;
using Microsoft.Extensions.Logging;

namespace ClimateController.FloorHeating;

internal sealed class Valve(ILogger<Relay> logger, IPEndPoint endPoint) : Relay
{
    private const string off = "turn=off";

    private const string on = "turn=on";

    private const string path = "/relay/0";

    public override async Task DisableAsync(CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Closing valve");

        var status = await SendAsync(off, cancellationToken);
        if (status.IsOn)
        {
            logger.LogError("Valve closing failed");

            throw new DeviceException();
        }

        logger.LogDebug("Closed valve");
    }

    public override async Task EnableAsync(CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Opening valve");

        var status = await SendAsync(on, cancellationToken);
        if (status.Overpower)
        {
            logger.LogError("Valve relay overpower");

            throw new DeviceException();
        }
        else if (!status.IsOn)
        {
            logger.LogError("Valve opening failed");

            throw new DeviceException();
        }

        logger.LogDebug("Opened valve");
    }

    private async Task<Status> SendAsync(string query, CancellationToken cancellationToken)
    {
        var builder = new UriBuilder
        {
            Host = endPoint.Address.ToString(),
            Port = endPoint.Port,
            Path = path,
            Query = query
        };
        using var client = new HttpClient
        {
            BaseAddress = builder.Uri
        };

        try
        {
            return await client.GetFromJsonAsync<Status>(builder.Uri, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Valve communication error");

            throw new DeviceException();
        }
    }

    private readonly record struct Status
    {
        [JsonPropertyName("ison")]
        public required readonly bool IsOn { init; get; }

        [JsonPropertyName("overpower")]
        public required readonly bool Overpower { init; get; }
    }
}