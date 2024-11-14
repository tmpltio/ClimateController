namespace ClimateController.Devices;

internal abstract class Relay
{
    public abstract Task DisableAsync(CancellationToken cancellationToken = default);

    public abstract Task EnableAsync(CancellationToken cancellationToken = default);
}