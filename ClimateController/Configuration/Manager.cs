using System.Collections.Immutable;
using System.Configuration;
using System.Net;
using System.Net.NetworkInformation;
using Microsoft.Extensions.Logging;

namespace ClimateController.Configuration;

internal static class Manager
{
    static Manager()
    {
        var homeSection = (HomeSection)ConfigurationManager.GetSection("home");
        LogLevel = homeSection.Log.Level;
        ServerPort = homeSection.Server.Port;
        Tick = homeSection.Control.Tick;
        ValveEndPoint = new IPEndPoint(homeSection.Valve.Address, homeSection.Valve.Port);
        Levels = homeSection
            .Levels
            .Cast<LevelElement>()
            .GroupJoin(homeSection.Rooms.Cast<RoomElement>(), level => level.Kind, room => room.Level, (level, rooms) => new
            {
                Level = level.Kind,
                ManifoldEndPoint = new IPEndPoint(level.Manifold.Address, level.Manifold.Port),
                level.Pump,
                Rooms = rooms
            })
            .ToImmutableDictionary(group => group.Level, group => new Level(group.ManifoldEndPoint, group.Pump.Index, group.Rooms));
    }

    public static LogLevel LogLevel { get; }

    public static int ServerPort { get; }

    public static TimeSpan Tick { get; }

    public static IPEndPoint ValveEndPoint { get; }

    public static IReadOnlyDictionary<string, Level> Levels { get; }

    public sealed class Room(PhysicalAddress sensorAddress, RelaysCollection loops)
    {
        public PhysicalAddress SensorAddress { get; } = sensorAddress;

        public IReadOnlyCollection<ushort> Loops { get; } = loops
                .Cast<RelayElement>()
                .Select(loop => loop.Index)
                .ToImmutableArray();
    }

    public sealed class Level(IPEndPoint manifoldEndPoint, ushort pump, IEnumerable<RoomElement> roomElements)
    {
        public IPEndPoint ManifoldEndPoint { get; } = manifoldEndPoint;

        public ushort Pump { get; } = pump;

        public IReadOnlyDictionary<string, Room> Rooms { get; } = roomElements
            .ToImmutableDictionary(room => room.Name, room => new Room(room.Sensor.Address, room.Loops));
    }
}