namespace AgriDrone.Modules.Surveys.Application.Abstractions.Persistence;

/// <summary>
/// Test seam invoked after a real database flush and before the approval
/// transaction commits. Production uses a no-op implementation.
/// </summary>
public interface ISurveyApprovalFailureInjector
{
    Task AfterFlushAsync(
        SurveyApprovalPersistenceCheckpoint checkpoint,
        CancellationToken cancellationToken = default);
}

public enum SurveyApprovalPersistenceCheckpoint
{
    IdempotencyClaim,
    Tenant,
    Farm,
    PrimaryManagerAssignment,
    OwnerInvitation,
    Outbox
}
