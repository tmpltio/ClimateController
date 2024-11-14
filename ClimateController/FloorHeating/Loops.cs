using System.Collections.Frozen;
using System.Net;
using ClimateController.Devices;

namespace ClimateController.FloorHeating;

internal sealed class Loops
{
    private readonly FrozenDictionary<string, Relay> relays;

    public Loops(IPEndPoint valveEndPoint, IEnumerable<Manifold> manifolds)
    {
        var valve = new DependentRelay(new Valve(valveEndPoint));
        relays = manifolds
            .Select(manifold => new
            {
                Manifold = manifold,
                Pump = new DependentRelay(valve[manifold.Pump])
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