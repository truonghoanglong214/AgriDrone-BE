namespace AgriDrone.IntegrationContracts.Messaging;

public sealed record IntegrationMessageReadResult<TPayload>
{
    internal IntegrationMessageReadResult(
        IntegrationEventEnvelope<TPayload>? envelope,
        IReadOnlyList<IntegrationMessageError> errors)
    {
        Envelope = envelope;
        Errors = errors;
    }

    public bool IsSuccess => Envelope is not null && Errors.Count == 0;

    public IntegrationEventEnvelope<TPayload>? Envelope { get; }

    public IReadOnlyList<IntegrationMessageError> Errors { get; }

}

public static class IntegrationMessageReadResult
{
    public static IntegrationMessageReadResult<TPayload> Success<TPayload>(
        IntegrationEventEnvelope<TPayload> envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        return new IntegrationMessageReadResult<TPayload>(envelope, []);
    }

    public static IntegrationMessageReadResult<TPayload> Failure<TPayload>(
        IReadOnlyList<IntegrationMessageError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        if (errors.Count == 0)
        {
            throw new ArgumentException(
                "A failed message read must contain at least one error.",
                nameof(errors));
        }

        return new IntegrationMessageReadResult<TPayload>(null, errors);
    }
}
