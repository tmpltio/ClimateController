using System.Collections.Frozen;
using System.Net;
using ClimateController.Devices;

namespace ClimateController.FloorHeating;

internal sealed class Loops
{
    private readonly FrozenDictionary<string, Relay> relays;

    public Loops(Valve valve, IEnumerable<Manifold> manifolds)
    {
        var dependentValve = new DependentRelay(valve);
        relays = manifolds
            .Select(manifold => new
            {
                Manifold = manifold,
                Pump = new DependentRelay(dependentValve[manifold.Pump])
            })
            .SelectMany(group => group.Manifold.Relays, (group, relay) => new
            {
                Name = relay.Key,
                Relay = group.Pump[relay.Value]
            })
            .ToFrozenDictionary(group => group.Name, group => group.Relay);
    }

    public Relay this[string room] =>
        relays[room];
}