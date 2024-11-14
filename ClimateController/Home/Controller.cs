using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClimateController.Communication;
using ClimateController.Thermostats;

namespace ClimateController.Home;

internal sealed class Controller(int port, IReadOnlyCollection<Room> rooms)
{
    public async Task RunAsync(TimeSpan tick, CancellationToken cancellationToken = default)
    {
        var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var server = new Server(port);
        var tasks = rooms
            .Select(room => room.Server.RunAsync(room.Thermostat.Handler, cancellationTokenSource.Token))
            .Append(server.RunAsync(new CommunicationHandler(rooms), cancellationTokenSource.Token))
            .Concat(rooms.Select(room => ControlRoom(room, tick, cancellationTokenSource.Token)));

        try
        {
            await Task.WhenAny(tasks);
        }
        catch
        {
            cancellationTokenSource.Cancel();
        }
    }

    private static async Task ControlRoom(Room room, TimeSpan tick, CancellationToken cancellationToken)
    {
        var stateChanged = 0;
        room.Thermostat.StateChanged += () => Interlocked.Exchange(ref stateChanged, 1);
        while (!cancellationToken.IsCancellationRequested)
        {
            var delayTask = Task.Delay(tick, cancellationToken);
            try
            {
                await room.Thermostat.ControlAsync(cancellationToken);
                if (stateChanged != 0)
                {
                    var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    cancellationTokenSource.CancelAfter(tick);
                    Interlocked.Exchange(ref stateChanged, 0);
                    await room.Thermostat.Handler.BroadcastStatusAsync(cancellationTokenSource.Token);
                }
            }
            finally
            {
                await delayTask;
            }
        }
    }

    private sealed class CommunicationHandler(IReadOnlyCollection<Room> rooms) : Handler
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
                var builder = new StringBuilder(room.Name);
                builder.Append(room.Thermostat.Features);
                var bytes = Encoding.UTF8.GetBytes(builder.ToString());
                var hash = MD5.HashData(bytes);

                return new(hash);
            };
            var roomsConfiguration = rooms
                .Select(room => new RoomConfiguration
                {
                    Name = room.Name,
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