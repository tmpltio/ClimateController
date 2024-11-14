using ClimateController.Communication;
using ClimateController.Configuration;
using ClimateController.Devices;
using ClimateController.FloorHeating;
using ClimateController.Thermostats;
using Microsoft.Extensions.Logging;

namespace ClimateController.Home;

internal static class Extensions
{
    public static IEnumerable<Manifold> BuildManifolds(this IEnumerable<Manager.Level> levels, ILogger<Relay> logger) =>
        levels
        .Select(level => new Manifold(logger, level.ManifoldEndPoint, level.Pump, level.Rooms.BuildRoomLoops()));

    public static IEnumerable<KeyValuePair<string, SensorsServer.Sensor>> GetNamedSensors(this IEnumerable<Manager.Level> levels, SensorsServer sensorsServer) =>
        levels
        .SelectMany(level => level.Rooms, (_, room) => KeyValuePair.Create(room.Key, sensorsServer[room.Value.SensorAddress]));

    public static IEnumerable<Thermostat> BuildThermostats(this IEnumerable<KeyValuePair<string, SensorsServer.Sensor>> rooms, ILogger<Thermostat> logger, TimeSpan tick, Loops loops) =>
        rooms
        .Select(room => new FloorHeatingThermostat(logger, tick, room.Key, GetFloorHeatingThermostatDevices(room.Key, room.Value, loops)));

    public static IEnumerable<Room> BuildRooms(this IEnumerable<Thermostat> thermostats, ILogger<Server> logger) =>
        thermostats
        .Select(thermostat => new Room(thermostat, new Server(logger, thermostat.Handler)));

    public static IEnumerable<Task> GetConfigurationTasks(this IEnumerable<Room> rooms) =>
        rooms
        .Select(room => new Task[] { room.Thermostat.Configured, room.Server.Configured })
        .SelectMany(tasks => tasks);

    public static IEnumerable<Task> GetWorkersTasks(this IEnumerable<Room> rooms, CancellationToken cancellationToken) =>
        rooms
        .Select(room => Task.WhenAny(room.Thermostat.RunAsync(cancellationToken), room.Server.RunAsync(cancellationToken)));

    private static IEnumerable<KeyValuePair<string, IEnumerable<ushort>>> BuildRoomLoops(this IEnumerable<KeyValuePair<string, Manager.Room>> rooms) =>
        rooms
        .Select(room => KeyValuePair.Create(room.Key, room.Value.Loops.AsEnumerable()));

    private static FloorHeatingThermostat.Devices GetFloorHeatingThermostatDevices(string name, SensorsServer.Sensor sensor, Loops loops) =>
        new()
        {
            TemperatureSensor = sensor,
            HumiditySensor = sensor,
            HeatingLoop = loops[name]
        };
}