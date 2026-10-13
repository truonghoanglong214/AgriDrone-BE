using AgriDrone.Modules.Surveys.Domain;

namespace AgriDrone.Modules.Surveys.Application.Abstractions.Approvals;

/// <summary>
/// Approval-only request persistence. The returned aggregate must be tracked by
/// the specialized approval DbContext and row-locked until its transaction ends.
/// </summary>
public interface ISurveyApprovalRequestRepository
{
    Task<SurveyRequest?> GetForApprovalAsync(
        Guid surveyRequestId,
        CancellationToken cancellationToken = default);
}
