using ClimateController.Devices;

namespace ClimateController.FloorHeating;

internal sealed class CompoundRelay(IEnumerable<Relay> relays) : Relay
{
    public override Task DisableAsync(CancellationToken cancellationToken = default) =>
        Task.WhenAll(relays.Select(relay => relay.DisableAsync(cancellationToken)));

    public override Task EnableAsync(CancellationToken cancellationToken = default) =>
        Task.WhenAll(relays.Select(relay => relay.EnableAsync(cancellationToken)));
}