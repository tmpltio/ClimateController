using System.Collections.Concurrent;
using ClimateController.Devices;

namespace ClimateController.FloorHeating;

internal sealed class DependentRelay(Relay relay)
{
    private readonly ConcurrentDictionary<SignalingRelay, bool> states = [];

    private readonly SemaphoreSlim updateLock = new(1, 1);

    public Relay this[Relay relay]
    {
        get
        {
            var signallingRelay = new SignalingRelay(this, relay);
            states.GetOrAdd(signallingRelay, false);

            return signallingRelay;
        }
    }

    private async Task UpdateDependentAsync(CancellationToken cancellationToken)
    {
        await updateLock.WaitAsync(cancellationToken);
        try
        {
            var state = states
                .Values
                .Any(state => state);
            if (state)
            {
                await relay.EnableAsync(cancellationToken);
            }
            else
            {
                await relay.DisableAsync(cancellationToken);
            }
        }
        finally
        {
            updateLock.Release();
        }
    }

    private sealed class SignalingRelay(DependentRelay dependentRelay, Relay relay) : Relay
    {
        public override async Task DisableAsync(CancellationToken cancellationToken = default)
        {
            await relay.DisableAsync(cancellationToken);
            dependentRelay.states[this] = false;
            await dependentRelay.UpdateDependentAsync(cancellationToken);
        }

        public override async Task EnableAsync(CancellationToken cancellationToken = default)
        {
            await relay.EnableAsync(cancellationToken);
            dependentRelay.states[this] = true;
            await dependentRelay.UpdateDependentAsync(cancellationToken);
        }
    }
}