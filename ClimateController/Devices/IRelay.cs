namespace ClimateController.Devices;

internal interface IRelay
{
    public abstract Task DisableAsync(CancellationToken cancellationToken = default);

    public abstract Task EnableAsync(CancellationToken cancellationToken = default);
}