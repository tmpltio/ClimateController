namespace ClimateController.Devices;

internal abstract class HumiditySensor
{
    public abstract Task<float> GetHumidityAsync(CancellationToken cancellationToken = default);
}