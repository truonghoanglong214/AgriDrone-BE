using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Orders.GetManagerSurveyOrderCaptureAttempts;

public sealed record GetManagerSurveyOrderCaptureAttemptsQuery(Guid OrderId)
    : IRequest<Result<IReadOnlyList<ManagerSurveyOrderCaptureAttemptResponse>>>;

public sealed record ManagerSurveyOrderCaptureAttemptResponse(
    Guid CaptureAttemptId,
    int AttemptIndex,
    CaptureAttemptStatus Status,
    uint Version,
    Guid? MissionId,
    Guid? ParentAttemptId,
    Guid? ParentMissionId,
    DateTimeOffset? StartedAt,
    DateTimeOffset? EndedAt,
    IReadOnlyList<ManagerSurveyOrderCaptureAttemptHistoryResponse> History,
    Guid? ManifestId = null,
    string? ManifestFingerprint = null,
    Guid? ProcessingJobId = null,
    uint? OrderVersion = null,
    Guid? MappingSourceAttemptId = null);

public sealed record ManagerSurveyOrderCaptureAttemptHistoryResponse(
    uint TransitionSequence,
    string Action,
    CaptureAttemptStatus? FromStatus,
    CaptureAttemptStatus ToStatus,
    string Reason,
    DateTimeOffset CreatedAt);
