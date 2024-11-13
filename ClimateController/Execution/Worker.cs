namespace ClimateController.Execution;

internal abstract class Worker
{
    protected readonly TaskCompletionSource configuredSource = new();

    public Task Configured =>
        configuredSource.Task;

    public abstract Task RunAsync(CancellationToken cancellationToken);
}