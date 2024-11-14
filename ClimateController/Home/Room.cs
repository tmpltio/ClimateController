using ClimateController.Communication;
using ClimateController.Thermostats;

namespace ClimateController.Home;

internal sealed class Room(string name, Thermostat thermostat)
{
    private readonly Server server = new();

    public string Name =>
        name;

    public Thermostat Thermostat =>
        thermostat;

    public Server Server =>
        server;
}