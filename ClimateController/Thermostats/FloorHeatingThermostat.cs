using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClimateController.Communication;
using ClimateController.Devices;

namespace ClimateController.Thermostats;

internal sealed class FloorHeatingThermostat : Thermostat
{
    private const float fallbackTemperature = 20.0f;

    private const float fallbackHumidity = 50.0f;

    private readonly ITemperatureSensor temperatureSensor;

    private readonly IHumiditySensor humiditySensor;

    private readonly IRelay heatingLoop;

    private readonly CommunicationHandler handler;

    private float currentTemperature = float.NaN;

    private float currentHumidity = float.NaN;

    private State currentState = State.Off;

    private float targetTemperature = float.NaN;

    private State targetState = State.Off;

    private FloorHeatingThermostat(ITemperatureSensor temperatureSensor, IHumiditySensor humiditySensor, IRelay heatingLoop)
    {
        this.temperatureSensor = temperatureSensor;
        this.humiditySensor = humiditySensor;
        this.heatingLoop = heatingLoop;
        handler = new CommunicationHandler(this);
    }

    private enum State
    {
        Off,
        Heat
    }

    public override Feature Features =>
        Feature.Heat |
        Feature.TemperatureSensor |
        Feature.HumiditySensor;

    public override ThermostatHandler Handler =>
        handler;

    private float CurrentTemperature
    {
        set => UpdateStatus(ref currentTemperature, value);
    }

    private float TargetTemperature
    {
        set => UpdateStatus(ref targetTemperature, value);
    }

    private float CurrentHumidity
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

    public static async Task<Thermostat> CreateAsync(ITemperatureSensor temperatureSensor, IHumiditySensor humiditySensor, IRelay heatingLoop, CancellationToken cancellationToken = default)
    {
        var thermostat = new FloorHeatingThermostat(temperatureSensor, humiditySensor, heatingLoop);
        var disablingTask = thermostat.DisableAsync(cancellationToken);
        var updateTask = thermostat.UpdateClimateAsync(cancellationToken);
        await Task.WhenAll(disablingTask, updateTask);
        thermostat.targetTemperature = thermostat.currentTemperature;

        return thermostat;
    }

    public override async Task ControlAsync(CancellationToken cancellationToken = default)
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
        await heatingLoop.DisableAsync(cancellationToken);
        CurrentState = State.Off;
    }

    private async Task EnableAsync(CancellationToken cancellationToken)
    {
        await heatingLoop.EnableAsync(cancellationToken);
        CurrentState = State.Heat;
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
                    var received = await client.ReceiveAsync(buffer, cancellationToken);
                    if (received == 0)
                    {
                        break;
                    }
                    var request = JsonSerializer.Deserialize<Request>(Encoding.UTF8.GetString(buffer.AsSpan()[..received]));
                    var response = request.Type switch
                    {
                        Type.GetStatus => GetStatus(Type.GetStatus),
                        Type.SetControl => SetControl(request),
                        _ => new Response { Type = request.Type, Success = false }
                    };
                    await client.SendAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(response)), cancellationToken);
                }
            }
            catch
            { }
        }

        private Response GetStatus(Type type)
        {
            var failure = float.IsNaN(thermostat.currentTemperature)
                || float.IsNaN(thermostat.targetTemperature)
                || float.IsNaN(thermostat.currentHumidity);

            return new()
            {
                Type = type,
                Success = !failure,
                Status = new()
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
                }
            };
        }

        private Response SetControl(Request request)
        {
            if (request.TargetTemperature.HasValue)
            {
                thermostat.TargetTemperature = request.TargetTemperature.Value;
            }
            if (request.TargetState.HasValue)
            {
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
            public required readonly Type Type { init; get; }

            [JsonPropertyName("target_temperature")]
            public readonly float? TargetTemperature { init; get; }

            [JsonConverter(typeof(JsonStringEnumConverter))]
            [JsonPropertyName("target_state")]
            public readonly State? TargetState { init; get; }
        }

        private readonly record struct Response
        {
            [JsonConverter(typeof(JsonStringEnumConverter))]
            [JsonPropertyName("type")]
            public required readonly Type Type { init; get; }

            [JsonPropertyName("success")]
            public required readonly bool Success { init; get; }

            [JsonPropertyName("status")]
            public readonly Status? Status { init; get; }
        }

        private readonly record struct Status
        {
            [JsonPropertyName("current_temperature")]
            public required readonly float CurrentTemperature { init; get; }

            [JsonPropertyName("target_temperature")]
            public required readonly float TargetTemperature { init; get; }

            [JsonPropertyName("current_humidity")]
            public required readonly float CurrentHumidity { init; get; }

            [JsonConverter(typeof(JsonStringEnumConverter))]
            [JsonPropertyName("current_state")]
            public required readonly State CurrentState { init; get; }

            [JsonConverter(typeof(JsonStringEnumConverter))]
            [JsonPropertyName("target_state")]
            public required readonly State TargetState { init; get; }
        }
    }
}