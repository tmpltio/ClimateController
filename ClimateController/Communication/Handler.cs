namespace ClimateController.Communication;

internal abstract class Handler
{
    private readonly HashSet<Client> clients = [];

    private readonly SemaphoreSlim modifyLock = new(1, 1);

    public async Task HandleClientAsync(Client client, CancellationToken cancellationToken = default)
    {
        await AddClientAsync(client, cancellationToken);
        await HandleCommunicationAsync(client, cancellationToken);
        await RemoveClientAsync(client, cancellationToken);
    }

    protected abstract Task HandleCommunicationAsync(Client client, CancellationToken cancellationToken);

    protected Task BroadcastAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) =>
        Task.WhenAll(clients.Select(client => client.SendAsync(buffer, cancellationToken)));

    private async Task AddClientAsync(Client client, CancellationToken cancellationToken)
    {
        await modifyLock.WaitAsync(cancellationToken);
        try
        {
            clients.Add(client);
        }
        finally
        {
            modifyLock.Release();
        }
    }

    private async Task RemoveClientAsync(Client client, CancellationToken cancellationToken)
    {
        await modifyLock.WaitAsync(cancellationToken);
        try
        {
            clients.Remove(client);
        }
        finally
        {
            modifyLock.Release();
        }
    }
}