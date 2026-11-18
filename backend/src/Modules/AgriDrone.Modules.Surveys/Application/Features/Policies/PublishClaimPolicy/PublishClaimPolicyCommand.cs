using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Policies.PublishClaimPolicy;

public sealed record PublishClaimPolicyCommand(
    int CalendarWindowDays,
    string TimeZoneId,
    bool ExcludeNoticeDate,
    int? ExpectedLatestVersion,
    string ReasonCode,
    string? EvidenceReference = null)
    : IRequest<Result<PublishClaimPolicyResponse>>;

public sealed record PublishClaimPolicyResponse(
    Guid PolicyId,
    int VersionNumber,
    Guid? PredecessorVersionId,
    int CalendarWindowDays,
    string TimeZoneId,
    bool ExcludeNoticeDate,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string ReasonCode,
    string? EvidenceReference);
