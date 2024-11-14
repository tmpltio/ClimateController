using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ClimateController.Devices;

namespace ClimateController.FloorHeating;

internal sealed class Valve(IPEndPoint endPoint) : Relay
{
    private const string off = "turn=off";

    private const string on = "turn=on";

    private const string path = "/relay/0";

    public override async Task DisableAsync(CancellationToken cancellationToken = default)
    {
        var status = await SendAsync(off, cancellationToken);
        if (status.IsOn)
        {
            throw new DeviceException("Valve relay opening failed");
        }
    }

    public override async Task EnableAsync(CancellationToken cancellationToken = default)
    {
        var status = await SendAsync(on, cancellationToken);
        if (status.Overpower)
        {
            throw new DeviceException("Valve relay overpower");
        }
        else if (!status.IsOn)
        {
            throw new DeviceException("Valve relay closing failed");
        }
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
            throw new DeviceException(exception.Message);
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