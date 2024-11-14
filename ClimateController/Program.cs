using System.Collections.Frozen;
using ClimateController.Configuration;
using ClimateController.FloorHeating;
using ClimateController.Home;
using ClimateController.Thermostats;

namespace ClimateController;

internal static class Program
{
    public static async Task Main()
    {
        var cancellationTokenSource = new CancellationTokenSource();

        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
            cancellationTokenSource.Cancel();

        var sensorsServer = new SensorsServer(Manager.ServerPort);
        var manifolds = Manager
            .Levels
            .Values
            .Select(level => new Manifold(level.ManifoldEndPoint, level.Pump, level.Rooms.ToFrozenDictionary(room => room.Key, room => room.Value.Loops.AsEnumerable())));
        var loops = new Loops(Manager.ValveEndPoint, manifolds);
        var creationTasks = Manager
            .Levels
            .Values
            .SelectMany(level => level.Rooms)
            .Select(async room => new Room(room.Key, await FloorHeatingThermostat.CreateAsync(sensorsServer[room.Value.SensorAddress], sensorsServer[room.Value.SensorAddress], loops[room.Key], cancellationTokenSource.Token)));
        var rooms = await Task.WhenAll(creationTasks);
        var controller = new Controller(Manager.ServerPort, rooms);

        var sensorsServerTask = sensorsServer.RunAsync(cancellationTokenSource.Token);
        var controllerTask = controller.RunAsync(Manager.ControlTick, cancellationTokenSource.Token);

        try
        {
            await Task.WhenAny(sensorsServerTask, controllerTask);
            cancellationTokenSource.Cancel();
        }
        catch (OperationCanceledException)
        { }
    }
}