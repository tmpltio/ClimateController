using System.Collections.Concurrent;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClimateController.Devices;
using ClimateController.Execution;
using Microsoft.Extensions.Logging;

namespace ClimateController.Home;

internal sealed class SensorsServer(ILogger<SensorsServer> logger, int port) : Worker
{
    private readonly ConcurrentDictionary<PhysicalAddress, CompoundSensor> climateData = [];

    [JsonConverter(typeof(JsonStringEnumConverter))]
    private enum Method
    {
        NotifyEvent,
        NotifyStatus,
        NotifyFullStatus
    }

    public Sensor this[PhysicalAddress address] =>
        GetSensor(address);

    public override async Task RunAsync(CancellationToken cancellationToken)
    {
        using var client = new UdpClient(port);
        configuredSource.TrySetResult();

        logger.LogInformation("Started listening on port {port}", port);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var result = await client.ReceiveAsync(cancellationToken);
                HandleMessage(result.Buffer);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Communication error");
            }
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
            sensor.Climate.Temperature = decimal.Round(parameters.Temperature.Value, 1, MidpointRounding.ToEven);
            sensor.Climate.Humidity = (byte)decimal.Round(parameters.Humidity.Value, MidpointRounding.ToEven);

            logger.LogDebug("Received notification from {address} with temperature {temperature} [°C] and humidity {humidity} [%]", address, sensor.Climate.Temperature, sensor.Climate.Humidity);
        }
    }

    private CompoundSensor GetSensor(PhysicalAddress address) =>
        climateData.GetOrAdd(address, _ => new CompoundSensor());

    public abstract class Sensor
    {
        protected abstract TemperatureSensor TemperatureSensor { get; }

        protected abstract HumiditySensor HumiditySensor { get; }

        public static implicit operator TemperatureSensor(Sensor sensor) =>
            sensor.TemperatureSensor;

        public static implicit operator HumiditySensor(Sensor sensor) =>
            sensor.HumiditySensor;
    }

    private sealed class Climate
    {
        public decimal Temperature { get; set; } = decimal.MaxValue;

        public byte Humidity { get; set; } = byte.MaxValue;
    }

    private sealed class CompoundSensor : Sensor
    {
        public CompoundSensor()
        {
            Climate = new();
            TemperatureSensor = new TemperatureSubsensor(this);
            HumiditySensor = new HumiditySubsensor(this);
        }

        public Climate Climate { get; }

        protected override TemperatureSensor TemperatureSensor { get; }

        protected override HumiditySensor HumiditySensor { get; }

        private sealed class TemperatureSubsensor(CompoundSensor compoundSensor) : TemperatureSensor
        {
            public override Task<decimal> GetTemperatureAsync(CancellationToken cancellationToken = default) =>
                Task.FromResult(compoundSensor.Climate.Temperature);
        }

        private sealed class HumiditySubsensor(CompoundSensor compoundSensor) : HumiditySensor
        {
            public override Task<byte> GetHumidityAsync(CancellationToken cancellationToken = default) =>
                Task.FromResult(compoundSensor.Climate.Humidity);
        }
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
        public required readonly decimal Value { init; get; }
    }

    private readonly record struct Humidity
    {
        [JsonPropertyName("rh")]
        public required readonly decimal Value { init; get; }
    }
}