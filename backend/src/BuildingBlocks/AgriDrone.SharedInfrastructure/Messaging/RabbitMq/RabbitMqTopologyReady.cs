namespace AgriDrone.SharedInfrastructure.Messaging.RabbitMq;

internal sealed class RabbitMqTopologyReady
{
    private readonly TaskCompletionSource _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task WaitAsync(CancellationToken cancellationToken) =>
        _completion.Task.WaitAsync(cancellationToken);

    public void MarkReady() => _completion.TrySetResult();
}
