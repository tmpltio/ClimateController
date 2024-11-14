namespace ClimateController.Devices;

internal abstract class HumiditySensor
{
    public abstract Task<byte> GetHumidityAsync(CancellationToken cancellationToken = default);
}