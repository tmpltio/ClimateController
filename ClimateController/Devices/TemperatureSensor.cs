namespace ClimateController.Devices;

internal abstract class TemperatureSensor
{
    public abstract Task<decimal> GetTemperatureAsync(CancellationToken cancellationToken = default);
}