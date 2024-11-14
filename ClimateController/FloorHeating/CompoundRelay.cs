using ClimateController.Devices;

namespace ClimateController.FloorHeating;

internal sealed class CompoundRelay(IEnumerable<IRelay> relays) : IRelay
{
    public Task DisableAsync(CancellationToken cancellationToken = default) =>
        Task.WhenAll(relays.Select(relay => relay.DisableAsync(cancellationToken)));

    public Task EnableAsync(CancellationToken cancellationToken = default) =>
        Task.WhenAll(relays.Select(relay => relay.EnableAsync(cancellationToken)));
}