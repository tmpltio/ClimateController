using System.Collections.Immutable;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClimateController.Communication;
using ClimateController.Configuration;
using ClimateController.Devices;
using ClimateController.Execution;
using ClimateController.FloorHeating;
using ClimateController.Thermostats;
using Microsoft.Extensions.Logging;

namespace ClimateController.Home;

internal sealed class Controller : Worker
{
    private readonly SensorsServer sensorsServer;

    private readonly IEnumerable<Room> rooms;

    private readonly Server homeServer;

    private Controller(SensorsServer sensorsServer, IEnumerable<Room> rooms, Server homeServer)
    {
        this.sensorsServer = sensorsServer;
        this.rooms = rooms;
        this.homeServer = homeServer;
        _ = Task
            .WhenAll(rooms.GetConfigurationTasks())
            .ContinueWith(_ => configuredSource.TrySetResult());
    }

    public static Controller Build(ILoggerFactory loggerFactory)
    {
        var sensorsServerLogger = loggerFactory.CreateLogger<SensorsServer>();
        var relayLogger = loggerFactory.CreateLogger<Relay>();
        var thermostatLogger = loggerFactory.CreateLogger<Thermostat>();
        var serverLogger = loggerFactory.CreateLogger<Server>();

        var loops = BuildLoops(relayLogger);
        var sensorsServer = new SensorsServer(sensorsServerLogger, Manager.ServerPort);
        var rooms = Manager
            .Levels
            .Values
            .GetNamedSensors(sensorsServer)
            .BuildThermostats(thermostatLogger, Manager.Tick, loops)
            .BuildRooms(serverLogger)
            .ToImmutableArray();
        var homeServer = new Server(serverLogger, new CommunicationHandler(rooms), Manager.ServerPort);

        return new(sensorsServer, rooms, homeServer);
    }

    public override async Task RunAsync(CancellationToken cancellationToken)
    {
        var roomsTask = Task.WhenAny(rooms.GetWorkersTasks(cancellationToken));
        await Configured;
        await Task.WhenAny(roomsTask, sensorsServer.RunAsync(cancellationToken), homeServer.RunAsync(cancellationToken));
    }

    private static Loops BuildLoops(ILogger<Relay> logger)
    {
        var valve = new Valve(logger, Manager.ValveEndPoint);
        var manifolds = Manager
            .Levels
            .Values
            .BuildManifolds(logger);

        return new(valve, manifolds);
    }

    private sealed class CommunicationHandler(IEnumerable<Room> rooms) : Handler
    {
        protected override async Task HandleCommunicationAsync(Client client, CancellationToken cancellationToken)
        {
            var version = Assembly
                .GetExecutingAssembly()
                .GetName()
                .Version
                ?? new(0, 0, 0, 0);
            static Guid guidGenerator(Room room)
            {
                var builder = new StringBuilder(room.Thermostat.Name);
                builder.Append(room.Thermostat.Features);
                var bytes = Encoding.UTF8.GetBytes(builder.ToString());
                var hash = MD5.HashData(bytes);

                return new(hash);
            };
            var roomsConfiguration = rooms
                .Select(room => new RoomConfiguration
                {
                    Name = room.Thermostat.Name,
                    Serial = guidGenerator(room),
                    Features = room.Thermostat.Features,
                    Port = room.Server.Port
                });
            var homeConfiguration = new HomeConfiguration
            {
                Version = $"{version.Major}.{version.Minor}.{version.Build}",
                Rooms = roomsConfiguration
            };
            await client.SendAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(homeConfiguration)), cancellationToken);
        }

        private readonly record struct HomeConfiguration
        {
            [JsonPropertyName("version")]
            public required readonly string Version { init; get; }

            [JsonPropertyName("rooms")]
            public required readonly IEnumerable<RoomConfiguration> Rooms { init; get; }
        }

        private readonly record struct RoomConfiguration
        {
            [JsonPropertyName("name")]
            public required readonly string Name { init; get; }

            [JsonPropertyName("serial")]
            public required readonly Guid Serial { init; get; }

            [JsonPropertyName("features")]
            public required readonly Feature Features { init; get; }

            [JsonPropertyName("port")]
            public required readonly int Port { init; get; }
        }
    }
}