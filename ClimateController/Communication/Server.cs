using System.Net;
using System.Net.Sockets;

namespace ClimateController.Communication;

internal sealed class Server(int port = 0)
{
    private readonly TcpListener listener = new(IPAddress.Any, port);

    public int Port =>
        (listener.Server.LocalEndPoint as IPEndPoint)
        ?.Port
        ?? 0;

    public async Task RunAsync(Handler handler, CancellationToken cancellationToken = default)
    {
        listener.Start();
        cancellationToken.Register(listener.Stop);
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var client = await listener.AcceptTcpClientAsync(cancellationToken);
                _ = handler
                    .HandleClientAsync(new(client), cancellationToken)
                    .ContinueWith(_ => client.Dispose(), CancellationToken.None);
            }
            catch
            { }
        }
    }
}