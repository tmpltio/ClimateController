using ClimateController.Execution;
using Microsoft.Extensions.Logging;

namespace ClimateController.Thermostats;

internal abstract class Thermostat(ILogger<Thermostat> logger, TimeSpan tick) : Worker
{
    protected ILogger<Thermostat> logger = logger;

    private int stateChanged = 0;

    public abstract string Name { get; }

    public abstract Feature Features { get; }

    public abstract ThermostatHandler Handler { get; }

    protected abstract Task InitializeAsync(CancellationToken cancellationToken);

    protected abstract Task ControlAsync(CancellationToken cancellationToken);

    public override async Task RunAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Initializing {name} thermostat", Name);

        await InitializeAsync(cancellationToken);
        configuredSource.TrySetResult();

        logger.LogInformation("Starting {name} thermostat control loop", Name);

        while (!cancellationToken.IsCancellationRequested)
        {
            var delayTask = Task.Delay(tick, cancellationToken);
            try
            {
                await ControlAsync(cancellationToken);
                if (Interlocked.Exchange(ref stateChanged, 0) != 0)
                {
                    BroadcastStateChange(cancellationToken);
                }
                await delayTask;
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("Cancelled {name} thermostat control loop", Name);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "{name} thermostat control loop error", Name);

                await delayTask;
            }
        }
    }

    protected void OnStateChanged() =>
        Interlocked.Exchange(ref stateChanged, 1);

    private void BroadcastStateChange(CancellationToken cancellationToken)
    {
        logger.LogDebug("Broadcasting {name} state change", Name);

        var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cancellationTokenSource.CancelAfter(tick);
        _ = Handler.BroadcastStatusAsync(cancellationTokenSource.Token);
    }
}