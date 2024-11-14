namespace ClimateController.Thermostats;

internal abstract class Thermostat
{
    public delegate void StateChangedHandler();

    public event StateChangedHandler? StateChanged = null;

    public abstract Feature Features { get; }

    public abstract ThermostatHandler Handler { get; }

    public abstract Task ControlAsync(CancellationToken cancellationToken = default);

    protected void OnStateChanged() =>
        StateChanged?.Invoke();
}