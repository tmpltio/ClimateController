namespace ClimateController.Devices;

internal interface ITemperatureSensor
{
    public abstract Task<float> GetTemperatureAsync(CancellationToken cancellationToken = default);
}