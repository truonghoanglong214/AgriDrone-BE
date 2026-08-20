using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.IntegrationContracts.Messaging.Validation
{
    public static class IntegrationEnvelopeValidator
    {
        private static readonly TimeSpan AllowedFutureClockSkew =
            TimeSpan.FromMinutes(5);

        public static string? Validate<TPayload>(
            IntegrationEventEnvelope<TPayload>? envelope,
            string expectedEventType,
            int supportedSchemaVersion,
            DateTimeOffset utcNow)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(expectedEventType);

            if (supportedSchemaVersion <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(supportedSchemaVersion),
                    supportedSchemaVersion,
                    "Supported schema version must be greater than zero.");
            }

            if (utcNow == default)
            {
                throw new ArgumentException(
                    "Current UTC time is required.",
                    nameof(utcNow));
            }

            if (envelope is null)
            {
                return "Envelope is null.";
            }

            if (envelope.MessageId == Guid.Empty)
            {
                return "MessageId is required.";
            }

            if (envelope.CorrelationId == Guid.Empty)
            {
                return "CorrelationId is required.";
            }

            if (envelope.TenantId == Guid.Empty)
            {
                return "TenantId is required.";
            }

            if (envelope.ActorId == Guid.Empty)
            {
                return "ActorId cannot be an empty GUID when provided.";
            }

            if (envelope.OccurredAt == default)
            {
                return "OccurredAt is required.";
            }

            if (envelope.OccurredAt.Offset != TimeSpan.Zero)
            {
                return "OccurredAt must use the UTC offset.";
            }

            // Old events remain valid for replay. Only reject timestamps that
            // exceed the allowed clock skew in the future.
            if (envelope.OccurredAt > utcNow.Add(AllowedFutureClockSkew))
            {
                return "OccurredAt exceeds the allowed future clock skew.";
            }

            if (envelope.SchemaVersion <= 0)
            {
                return "SchemaVersion must be greater than zero.";
            }

            if (envelope.SchemaVersion != supportedSchemaVersion)
            {
                return $"Unsupported SchemaVersion: {envelope.SchemaVersion}.";
            }

            if (string.IsNullOrWhiteSpace(envelope.EventType))
            {
                return "EventType is required.";
            }

            if (envelope.EventType.Length >
                IntegrationContractLimits.MaximumEventTypeLength)
            {
                return $"EventType cannot exceed {IntegrationContractLimits.MaximumEventTypeLength} characters.";
            }

            if (!string.Equals(
                    envelope.EventType,
                    expectedEventType,
                    StringComparison.Ordinal))
            {
                return $"Unexpected EventType: {envelope.EventType}.";
            }

            if (envelope.Payload is null)
            {
                return "Payload is required.";
            }

            return null;
        }
    }
}
