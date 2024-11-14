using ClimateController.Communication;
using ClimateController.Thermostats;

namespace ClimateController.Home;

internal readonly struct Room(Thermostat thermostat, Server server)
{
    public Thermostat Thermostat =>
        thermostat;

    public Server Server =>
        server;
}