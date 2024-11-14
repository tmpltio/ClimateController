namespace ClimateController.Devices;

internal abstract class TemperatureSensor
{
    public abstract Task<float> GetTemperatureAsync(CancellationToken cancellationToken = default);
}