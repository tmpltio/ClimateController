using ClimateController.Communication;

namespace ClimateController.Thermostats;

internal abstract class ThermostatHandler : Handler
{
    public abstract Task BroadcastStatusAsync(CancellationToken cancellationToken = default);
}