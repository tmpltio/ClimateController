namespace ClimateController.Devices;

internal interface IHumiditySensor
{
    public abstract Task<float> GetHumidityAsync(CancellationToken cancellationToken = default);
}