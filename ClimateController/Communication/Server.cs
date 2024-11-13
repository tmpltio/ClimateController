using System.Net;
using System.Net.Sockets;
using ClimateController.Execution;
using Microsoft.Extensions.Logging;

namespace ClimateController.Communication;

internal sealed class Server(ILogger<Server> logger, Handler handler, int port = 0) : Worker
{
    private readonly TcpListener listener = new(IPAddress.Any, port);

    public int Port =>
        (listener.Server.LocalEndPoint as IPEndPoint)
        ?.Port
        ?? throw new InvalidOperationException("Server end point is invalid");

    public override async Task RunAsync(CancellationToken cancellationToken)
    {
        listener.Start();
        cancellationToken.Register(listener.Stop);
        configuredSource.TrySetResult();

        logger.LogInformation("Started listening on port {port}", Port);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var client = await listener.AcceptTcpClientAsync(cancellationToken);

                logger.LogDebug("Client connected from {endPoint}", client.Client.RemoteEndPoint);

                _ = handler
                    .HandleClientAsync(new(client), cancellationToken)
                    .ContinueWith(_ => logger.LogDebug("Client disconnected from {endPoint}", client.Client.RemoteEndPoint), CancellationToken.None)
                    .ContinueWith(_ => client.Dispose(), CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("Cancelled listening on port {port}", Port);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Accepting client on port {port} failed", Port);
            }
        }
    }
}