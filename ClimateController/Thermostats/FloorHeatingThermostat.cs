using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClimateController.Communication;
using ClimateController.Devices;
using Microsoft.Extensions.Logging;

namespace ClimateController.Thermostats;

internal sealed class FloorHeatingThermostat : Thermostat
{
    private const decimal fallbackTemperature = 18.0m;

    private const byte fallbackHumidity = 50;

    private readonly TemperatureSensor temperatureSensor;

    private readonly HumiditySensor humiditySensor;

    private readonly Relay heatingLoop;

    private decimal currentTemperature = decimal.MaxValue;

    private decimal targetTemperature = decimal.MaxValue;

    private byte currentHumidity = byte.MaxValue;

    private State currentState = State.Off;

    private State targetState = State.Off;

    public FloorHeatingThermostat(ILogger<Thermostat> logger, TimeSpan tick, string name, Devices devices) : base(logger, tick)
    {
        temperatureSensor = devices.TemperatureSensor;
        humiditySensor = devices.HumiditySensor;
        heatingLoop = devices.HeatingLoop;
        Name = name;
        Handler = new CommunicationHandler(this);
    }

    private enum State
    {
        Off,
        Heat
    }

    public override string Name { get; }

    public override Feature Features =>
        Feature.Heat |
        Feature.TemperatureSensor |
        Feature.HumiditySensor;

    public override ThermostatHandler Handler { get; }

    private decimal CurrentTemperature
    {
        set => UpdateStatus(ref currentTemperature, value);
    }

    private decimal TargetTemperature
    {
        set => UpdateStatus(ref targetTemperature, value);
    }

    private byte CurrentHumidity
    {
        set => UpdateStatus(ref currentHumidity, value);
    }

    private State CurrentState
    {
        set => UpdateStatus(ref currentState, value);
    }

    private State TargetState
    {
        set => UpdateStatus(ref targetState, value);
    }

    protected override async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var disablingTask = DisableAsync(cancellationToken);
        var updateTask = UpdateClimateAsync(cancellationToken);
        await Task.WhenAll(disablingTask, updateTask);
        targetTemperature = currentTemperature;
    }

    protected override async Task ControlAsync(CancellationToken cancellationToken = default)
    {
        await UpdateClimateAsync(cancellationToken);
        var controlTask = (currentTemperature, currentState, targetState) switch
        {
            (_, State.Off, State.Off) => Task.CompletedTask,
            (_, _, State.Off) => DisableAsync(cancellationToken),
            var (temperature, state, _) when temperature >= targetTemperature && state == State.Off => Task.CompletedTask,
            var (temperature, _, _) when temperature >= targetTemperature => DisableAsync(cancellationToken),
            (_, State.Heat, _) => Task.CompletedTask,
            (_, _, _) => EnableAsync(cancellationToken)
        };
        await controlTask;
    }

    private void UpdateStatus<T>(ref T current, T @new) where T : notnull
    {
        var old = current;
        current = @new;
        if (!current.Equals(old))
        {
            OnStateChanged();
        }
    }

    private async Task UpdateClimateAsync(CancellationToken cancellationToken)
    {
        var temperatureTask = temperatureSensor.GetTemperatureAsync(cancellationToken);
        var humidityTask = humiditySensor.GetHumidityAsync(cancellationToken);
        await Task.WhenAll(temperatureTask, humidityTask);
        CurrentTemperature = await temperatureTask;
        CurrentHumidity = await humidityTask;
    }

    private async Task DisableAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Disabling {name} floor heating", Name);

        await heatingLoop.DisableAsync(cancellationToken);
        CurrentState = State.Off;
    }

    private async Task EnableAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Enabling {name} floor heating", Name);

        await heatingLoop.EnableAsync(cancellationToken);
        CurrentState = State.Heat;
    }

    public readonly struct Devices
    {
        public required TemperatureSensor TemperatureSensor { init; get; }

        public required HumiditySensor HumiditySensor { init; get; }

        public required Relay HeatingLoop { init; get; }
    }

    private sealed class CommunicationHandler(FloorHeatingThermostat thermostat) : ThermostatHandler
    {
        private enum Type
        {
            SetControl,
            GetStatus,
            NotifyStatus
        }

        public override async Task BroadcastStatusAsync(CancellationToken cancellationToken = default)
        {
            var status = GetStatus(Type.NotifyStatus);
            await BroadcastAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(status)), cancellationToken);
        }

        protected override async Task HandleCommunicationAsync(Client client, CancellationToken cancellationToken)
        {
            try
            {
                var buffer = new byte[1024];
                while (!cancellationToken.IsCancellationRequested)
                {
                    thermostat.logger.LogDebug("{name} waiting for request", thermostat.Name);

                    var received = await client.ReceiveAsync(buffer, cancellationToken);
                    if (received == 0)
                    {
                        thermostat.logger.LogDebug("{name} communication finished", thermostat.Name);

                        break;
                    }
                    var request = JsonSerializer.Deserialize<Request>(Encoding.UTF8.GetString(buffer.AsSpan()[..received]));

                    thermostat.logger.LogDebug("{name} received {type} request", thermostat.Name, request.Type);

                    var response = request.Type switch
                    {
                        Type.GetStatus => GetStatus(Type.GetStatus),
                        Type.SetControl => SetControl(request),
                        _ => new Response
                        {
                            Type = request.Type,
                            Success = false
                        }
                    };

                    thermostat.logger.LogDebug("{name} sending {type} response with success = {success}", thermostat.Name, response.Type, response.Success);

                    await client.SendAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(response)), cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                thermostat.logger.LogDebug("{name} communication cancelled", thermostat.Name);
            }
            catch (Exception exception)
            {
                thermostat.logger.LogError(exception, "{name} communication error", thermostat.Name);
            }
        }

        private Response GetStatus(Type type)
        {
            var failure = thermostat.currentTemperature.Equals(decimal.MaxValue)
                || thermostat.targetTemperature.Equals(decimal.MaxValue)
                || thermostat.currentHumidity.Equals(byte.MaxValue);
            var status = new Status
            {
                CurrentTemperature = failure
                    ? fallbackTemperature
                    : thermostat.currentTemperature,
                TargetTemperature = failure
                    ? fallbackTemperature
                    : thermostat.targetTemperature,
                CurrentHumidity = failure
                    ? fallbackHumidity
                    : thermostat.currentHumidity,
                CurrentState = thermostat.currentState,
                TargetState = thermostat.targetState
            };

            thermostat.logger.LogDebug("Getting status from {name} for message {type}: {status}", thermostat.Name, type, status);

            return new()
            {
                Type = type,
                Success = !failure,
                Status = status
            };
        }

        private Response SetControl(Request request)
        {
            if (request.TargetTemperature.HasValue)
            {
                thermostat.logger.LogInformation("Setting {name} target temperature to {temperature} [°C]", thermostat.Name, request.TargetTemperature.Value);

                thermostat.TargetTemperature = decimal.Round(request.TargetTemperature.Value, 1, MidpointRounding.ToEven);
            }
            if (request.TargetState.HasValue)
            {
                thermostat.logger.LogInformation("Setting {name} target state to {state}", thermostat.Name, request.TargetState.Value);

                thermostat.TargetState = request.TargetState.Value;
            }

            return new()
            {
                Type = Type.SetControl,
                Success = true
            };
        }

        private readonly record struct Request
        {
            [JsonConverter(typeof(JsonStringEnumConverter))]
            [JsonPropertyName("type")]
            public required Type Type { init; get; }

            [JsonPropertyName("target_temperature")]
            public decimal? TargetTemperature { init; get; }

            [JsonConverter(typeof(JsonStringEnumConverter))]
            [JsonPropertyName("target_state")]
            public State? TargetState { init; get; }
        }

        private readonly record struct Response
        {
            [JsonConverter(typeof(JsonStringEnumConverter))]
            [JsonPropertyName("type")]
            public required Type Type { init; get; }

            [JsonPropertyName("success")]
            public required bool Success { init; get; }

            [JsonPropertyName("status")]
            public Status? Status { init; get; }
        }

        private readonly record struct Status
        {
            [JsonPropertyName("current_temperature")]
            public required decimal CurrentTemperature { init; get; }

            [JsonPropertyName("target_temperature")]
            public required decimal TargetTemperature { init; get; }

            [JsonPropertyName("current_humidity")]
            public required byte CurrentHumidity { init; get; }

            [JsonConverter(typeof(JsonStringEnumConverter))]
            [JsonPropertyName("current_state")]
            public required State CurrentState { init; get; }

            [JsonConverter(typeof(JsonStringEnumConverter))]
            [JsonPropertyName("target_state")]
            public required State TargetState { init; get; }
        }
    }
}