using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Net;
using System.Net.Sockets;
using ClimateController.Devices;
using Microsoft.Extensions.Logging;

namespace ClimateController.FloorHeating;

internal sealed class Manifold
{
    private readonly ILogger<Relay> logger;

    private readonly IPEndPoint endPoint;

    private TcpClient? client = null;

    private readonly SemaphoreSlim sendLock = new(1, 1);

    public Manifold(ILogger<Relay> logger, IPEndPoint endPoint, ushort pump, IEnumerable<KeyValuePair<string, IEnumerable<ushort>>> rooms)
    {
        this.logger = logger;
        this.endPoint = endPoint;
        Pump = new ManifoldRelay(this, pump);
        Relays = rooms
            .SelectMany(room => room.Value, (room, index) => new
            {
                Room = room.Key,
                Relay = new ManifoldRelay(this, index)
            })
            .GroupBy(loop => loop.Room, loop => loop.Relay)
            .ToFrozenDictionary(group => group.Key, group => new CompoundRelay(group) as Relay);
    }

    public Relay Pump { get; }

    public IReadOnlyDictionary<string, Relay> Relays { get; }

    private async Task<byte[]> SendAsync(byte[] data, CancellationToken cancellationToken)
    {
        await sendLock.WaitAsync(cancellationToken);
        try
        {
            if (client is null)
            {
                client = new TcpClient();
                await client.ConnectAsync(endPoint, cancellationToken);
            }
            await client.Client.SendAsync(data, cancellationToken);
            var buffer = new byte[16];
            var received = await client.Client.ReceiveAsync(buffer, cancellationToken);

            return buffer[..received];
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Manifold {endPoint} communication error", endPoint);

            client?.Dispose();
            client = null;

            throw new DeviceException();
        }
        finally
        {
            sendLock.Release();
        }
    }

    private sealed class ManifoldRelay(Manifold manifold, ushort index) : Relay
    {
        public override async Task DisableAsync(CancellationToken cancellationToken = default)
        {
            manifold.logger.LogDebug("Disabling manifold {endPoint} relay {index}", manifold.endPoint, index);

            if (!await SendAsync(Command.Disable[index], cancellationToken))
            {
                manifold.logger.LogError("Manifold {endPoint} relay {index} disabling failed", manifold.endPoint, index);

                throw new DeviceException();
            }

            manifold.logger.LogDebug("Disabled manifold {endPoint} relay {index}", manifold.endPoint, index);
        }

        public override async Task EnableAsync(CancellationToken cancellationToken = default)
        {
            manifold.logger.LogDebug("Enabling manifold {endPoint} relay {index}", manifold.endPoint, index);

            if (!await SendAsync(Command.Enable[index], cancellationToken))
            {
                manifold.logger.LogError("Manifold {endPoint} relay {index} enabling failed", manifold.endPoint, index);
            }

            manifold.logger.LogDebug("Enabled manifold {endPoint} relay {index}", manifold.endPoint, index);
        }

        private async Task<bool> SendAsync(IEnumerable<byte> query, CancellationToken cancellationToken)
        {
            var request = GetData(query);
            var response = await manifold.SendAsync(request, cancellationToken);

            return response.SequenceEqual(request);
        }

        private static byte[] GetData(IEnumerable<byte> command)
        {
            var crc = Crc.Compute(command);

            return command
                .Append((byte)(crc & 0xFF))
                .Append((byte)(crc >> 8))
                .ToArray();
        }

        private static class Crc
        {
            private const ushort polynomial = 0xA001;

            private static readonly IReadOnlyList<ushort> table;

            static Crc() =>
                table = Enumerable
                .Range(0, 256)
                .Select(index =>
                {
                    ushort value = 0;
                    for (byte i = 0; i < 8; ++i)
                    {
                        if (((value ^ index) & 0x0001) != 0)
                        {
                            value = (ushort)((value >> 1) ^ polynomial);
                        }
                        else
                        {
                            value >>= 1;
                        }
                        index >>= 1;
                    }

                    return value;
                })
                .ToImmutableArray();

            public static ushort Compute(IEnumerable<byte> data) =>
                data
                .Aggregate((ushort)0xFFFF, (crc, @byte) =>
                {
                    byte index = (byte)(crc ^ @byte);

                    return (ushort)((crc >> 8) ^ table[index]);
                });
        }

        private readonly struct Command(bool close)
        {
            public static Command Disable = new(false);

            public static Command Enable = new(true);

            public IEnumerable<byte> this[ushort index] =>
                Enumerable
                .Empty<byte>()
                .Append((byte)0x01)
                .Append((byte)0x05)
                .Append((byte)(index >> 8))
                .Append((byte)(index & 0xFF))
                .Append(close ? (byte)0xFF : (byte)0x00)
                .Append((byte)0x00);
        }
    }
}