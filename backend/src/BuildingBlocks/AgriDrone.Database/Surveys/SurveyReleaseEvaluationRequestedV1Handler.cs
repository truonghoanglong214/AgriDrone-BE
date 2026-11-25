using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.IntegrationContracts.Surveys;
using AgriDrone.SharedInfrastructure.Messaging.Consumers;
using AgriDrone.SharedInfrastructure.Messaging.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AgriDrone.Database.Surveys;

/// <summary>
/// Retries bounded V3 release evaluation triggers asynchronously outside payment/invoice transactions.
/// Invokes ISurveySettlementReleaseCoordinator with NO open outer SQL transaction so Rank 1 locks
/// always precede Order/Receipt locks.
/// </summary>
internal sealed class SurveyReleaseEvaluationRequestedV1Handler(
    AgriDroneSchemaDbContext db,
    ISurveySettlementReleaseCoordinator coordinator,
    TimeProvider timeProvider) : IIntegrationMessageHandler<SurveyReleaseEvaluationRequestedV1>
{
    private const string ConsumerName = IntegrationConsumerNames.Be1SurveyReleaseEvaluationRequestedV1;

    public async Task<IntegrationMessageProcessingResult> HandleAsync(
        IntegrationEventEnvelope<SurveyReleaseEvaluationRequestedV1> envelope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        var payload = envelope.Payload;

        if (!Enum.TryParse<SurveyReleaseTriggerSource>(payload.TriggerSource, out var triggerSource) ||
            !Enum.IsDefined(triggerSource) || payload.TriggerSource != triggerSource.ToString())
        {
            return IntegrationMessageProcessingResult.DeadLetter(
                "SettlementRelease.InvalidTriggerSource", "The release trigger source is unsupported.");
        }

        var existingInbox = await db.Set<InboxMessage>().AsNoTracking()
            .SingleOrDefaultAsync(
                m => m.ConsumerName == ConsumerName && m.MessageId == envelope.MessageId,
                cancellationToken);

        if (existingInbox is not null)
        {
            return existingInbox.Status switch
            {
                InboxMessageStatus.Completed => IntegrationMessageProcessingResult.Acknowledge(),
                InboxMessageStatus.Failed => IntegrationMessageProcessingResult.DeadLetter(
                    existingInbox.ErrorCode ?? "INBOX_PREVIOUSLY_FAILED",
                    existingInbox.LastError),
                _ => IntegrationMessageProcessingResult.Retry("Release evaluation message is still processing.")
            };
        }

        EvaluateSurveySettlementReleaseResult outcome;
        try
        {
            outcome = await coordinator.EvaluateAndReleaseAsync(
                new EvaluateSurveySettlementReleaseRequest(
                    OrderId: payload.OrderId,
                    TriggerSource: triggerSource,
                    CorrelationId: envelope.CorrelationId,
                    OperationId: payload.OperationId,
                    PayloadFingerprint: payload.PayloadFingerprint,
                    TriggerActorId: payload.TriggerActorId,
                    ExpectedTenantId: payload.TenantId,
                    ExpectedOrderVersion: payload.ExpectedOrderVersion,
                    ExpectedApprovedResultRevisionId: payload.ExpectedApprovedResultRevisionId,
                    ExpectedApprovedResultFingerprint: payload.ExpectedApprovedResultFingerprint,
                    ExpectedCountSnapshotId: payload.ExpectedCountSnapshotId,
                    ExpectedCountSnapshotFingerprint: payload.ExpectedCountSnapshotFingerprint,
                    ExpectedInvoiceRevisionId: payload.ExpectedInvoiceRevisionId,
                    ExpectedInvoiceRevisionNumber: payload.ExpectedInvoiceRevisionNumber,
                    ExpectedInvoiceFingerprint: payload.ExpectedInvoiceFingerprint,
                    ExpectedCommercialPolicyVersionId: payload.ExpectedCommercialPolicyVersionId,
                    ExpectedSurveyServicePriceId: payload.ExpectedSurveyServicePriceId,
                    ExpectedPricePerPole: payload.ExpectedPricePerPole,
                    ExpectedCurrency: payload.ExpectedCurrency,
                    TriggerPaymentId: payload.TriggerPaymentId,
                    TriggerFundingReceiptId: payload.TriggerFundingReceiptId,
                    ExpectedFarmId: payload.FarmId),
                cancellationToken);
        }
        catch (PostgresException ex) when (ex.SqlState is
            PostgresErrorCodes.SerializationFailure or
            PostgresErrorCodes.DeadlockDetected or
            PostgresErrorCodes.LockNotAvailable)
        {
            return IntegrationMessageProcessingResult.Retry(ex.Message);
        }
        catch (TimeoutException ex)
        {
            return IntegrationMessageProcessingResult.Retry(ex.Message);
        }

        if (outcome.Outcome == SurveySettlementReleaseOutcome.StaleContext &&
            string.Equals(outcome.ErrorCode, "SettlementRelease.ConcurrencyConflict", StringComparison.Ordinal))
        {
            return IntegrationMessageProcessingResult.Retry(outcome.ErrorMessage);
        }

        if (outcome.Outcome == SurveySettlementReleaseOutcome.Blocked &&
            outcome.ErrorCode == SurveySettlementReleaseCoordinator.ReleasePersistencePendingCode)
        {
            return IntegrationMessageProcessingResult.Retry(outcome.ErrorMessage);
        }

        var now = timeProvider.GetUtcNow();
        var inbox = InboxMessage.Start(ConsumerName, envelope, now);

        switch (outcome.Outcome)
        {
            case SurveySettlementReleaseOutcome.Released:
            case SurveySettlementReleaseOutcome.Replay:
            case SurveySettlementReleaseOutcome.InsufficientFunding:
            case SurveySettlementReleaseOutcome.StaleContext:
            case SurveySettlementReleaseOutcome.ReconciliationHold:
                inbox.Complete($"{outcome.Outcome}:{outcome.ErrorCode ?? "OK"}", now);
                await PersistInboxRecordAsync(inbox, cancellationToken);
                return IntegrationMessageProcessingResult.Acknowledge();

            case SurveySettlementReleaseOutcome.Forbidden:
            case SurveySettlementReleaseOutcome.NotFound:
            case SurveySettlementReleaseOutcome.Blocked:
                inbox.Fail(
                    outcome.ErrorCode ?? outcome.Outcome.ToString(),
                    outcome.ErrorMessage ?? "Settlement release evaluation failed.",
                    now);
                await PersistInboxRecordAsync(inbox, cancellationToken);
                return IntegrationMessageProcessingResult.DeadLetter(
                    outcome.ErrorCode ?? outcome.Outcome.ToString(),
                    outcome.ErrorMessage);

            default:
                return IntegrationMessageProcessingResult.Retry($"Unhandled outcome '{outcome.Outcome}'.");
        }
    }

    private async Task PersistInboxRecordAsync(InboxMessage message, CancellationToken cancellationToken)
    {
        try
        {
            db.Set<InboxMessage>().Add(message);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "pk_inbox_messages"
        })
        {
            // Concurrent delivery already committed the inbox record.
        }
        finally
        {
            db.ChangeTracker.Clear();
        }
    }
}
