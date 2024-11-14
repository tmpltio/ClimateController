using System.Collections.Concurrent;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClimateController.Devices;

namespace ClimateController.Home;

internal sealed class SensorsServer(int port)
{
    private readonly ConcurrentDictionary<PhysicalAddress, CompoundSensor> climateData = [];

    [JsonConverter(typeof(JsonStringEnumConverter))]
    private enum Method
    {
        NotifyEvent,
        NotifyStatus,
        NotifyFullStatus
    }

    public ISensor this[PhysicalAddress address] =>
        GetSensor(address);

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        using var client = new UdpClient(port);
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var result = await client.ReceiveAsync(cancellationToken);
                HandleMessage(result.Buffer);
            }
            catch
            { }
        }
    }

    private void HandleMessage(ReadOnlyMemory<byte> buffer)
    {
        var message = JsonSerializer.Deserialize<Message>(Encoding.UTF8.GetString(buffer.Span));
        if (message.Method == Method.NotifyFullStatus)
        {
            var parameters = JsonSerializer.Deserialize<Parameters>(message.Parameters);
            var address = PhysicalAddress.Parse(parameters.System.Address);
            var sensor = GetSensor(address);
            sensor.Temperature = parameters.Temperature.Value;
            sensor.Humidity = parameters.Humidity.Value;
        }
    }

    private CompoundSensor GetSensor(PhysicalAddress address) =>
        climateData.GetOrAdd(address, _ => new CompoundSensor());

    public interface ISensor : ITemperatureSensor, IHumiditySensor
    { }

    private sealed class CompoundSensor : ISensor
    {
        public float Temperature { get; set; } = float.NaN;

        public float Humidity { get; set; } = float.NaN;

        public Task<float> GetTemperatureAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Temperature);

        public Task<float> GetHumidityAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Humidity);
    }

    private readonly record struct Message
    {
        [JsonPropertyName("method")]
        public required readonly Method Method { init; get; }

        [JsonPropertyName("params")]
        public required readonly JsonDocument Parameters { init; get; }
    }

    private readonly record struct Parameters
    {
        [JsonPropertyName("sys")]
        public required readonly System System { init; get; }

        [JsonPropertyName("temperature:0")]
        public required readonly Temperature Temperature { init; get; }

        [JsonPropertyName("humidity:0")]
        public required readonly Humidity Humidity { init; get; }
    }

    private readonly record struct System
    {
        [JsonPropertyName("mac")]
        public required readonly string Address { init; get; }
    }

    private readonly record struct Temperature
    {
        [JsonPropertyName("tC")]
        public required readonly float Value { init; get; }
    }

    private readonly record struct Humidity
    {
        [JsonPropertyName("rh")]
        public required readonly float Value { init; get; }
    }
}