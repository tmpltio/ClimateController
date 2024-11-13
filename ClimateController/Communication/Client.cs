using System.Net.Sockets;

namespace ClimateController.Communication;

internal sealed class Client(TcpClient client)
{
    private readonly SemaphoreSlim sendLock = new(1, 1);

    public ValueTask<int> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        client.Client.ReceiveAsync(buffer, cancellationToken);

    public async Task SendAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        await sendLock.WaitAsync(cancellationToken);
        try
        {
            await client.Client.SendAsync(buffer, cancellationToken);
        }
        finally
        {
            sendLock.Release();
        }
    }
}