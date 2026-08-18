using AgriDrone.SharedKernel.Application.Abstractions.Execution;

namespace AgriDrone.SharedInfrastructure.Execution;

internal sealed class ScopedExecutionContext
    : IExecutionContext,
      IExecutionContextInitializer
{
    private ExecutionContextSnapshot? _current;

    public bool IsInitialized => _current is not null;

    public Guid? TenantId => _current?.TenantId;

    public Guid? ActorId => _current?.ActorId;

    public Guid CorrelationId => _current?.CorrelationId ?? Guid.Empty;

    public Guid? MessageId => _current?.MessageId;

    public ExecutionContextSource Source =>
        _current?.Source ?? ExecutionContextSource.Unknown;

    public IDisposable Begin(ExecutionContextSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Validate(snapshot);

        if (_current is not null)
        {
            throw new InvalidOperationException(
                "The execution context has already been initialized for this scope.");
        }

        _current = snapshot;
        return new ContextLease(this, snapshot);
    }

    private static void Validate(ExecutionContextSnapshot snapshot)
    {
        if (snapshot.CorrelationId == Guid.Empty)
        {
            throw new ArgumentException(
                "CorrelationId is required.",
                nameof(snapshot));
        }

        if (snapshot.TenantId == Guid.Empty)
        {
            throw new ArgumentException(
                "TenantId cannot be an empty GUID when provided.",
                nameof(snapshot));
        }

        if (snapshot.ActorId == Guid.Empty)
        {
            throw new ArgumentException(
                "ActorId cannot be an empty GUID when provided.",
                nameof(snapshot));
        }

        if (snapshot.MessageId == Guid.Empty)
        {
            throw new ArgumentException(
                "MessageId cannot be an empty GUID when provided.",
                nameof(snapshot));
        }

        if (snapshot.Source == ExecutionContextSource.Unknown)
        {
            throw new ArgumentException(
                "Execution context source is required.",
                nameof(snapshot));
        }

        if (snapshot.Source == ExecutionContextSource.Http &&
            snapshot.MessageId.HasValue)
        {
            throw new ArgumentException(
                "HTTP execution context cannot contain a MessageId.",
                nameof(snapshot));
        }

        if (snapshot.Source == ExecutionContextSource.RabbitMq &&
            (!snapshot.TenantId.HasValue || !snapshot.MessageId.HasValue))
        {
            throw new ArgumentException(
                "RabbitMQ execution context requires TenantId and MessageId.",
                nameof(snapshot));
        }
    }

    private void End(ExecutionContextSnapshot snapshot)
    {
        if (!ReferenceEquals(_current, snapshot))
        {
            throw new InvalidOperationException(
                "Execution context lease does not own the current context.");
        }

        _current = null;
    }

    private sealed class ContextLease(
        ScopedExecutionContext owner,
        ExecutionContextSnapshot snapshot) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            owner.End(snapshot);
            _disposed = true;
        }
    }
}
