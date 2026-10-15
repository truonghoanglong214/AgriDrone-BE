using AgriDrone.Modules.Identity.Domain.SystemManagers;

namespace AgriDrone.Api.Contracts.SystemManagers;

public sealed record CreateSystemManagerProfileRequest(Guid UserId);

public sealed class GetSystemManagersRequest
{
    public SystemManagerProfileStatus? Status { get; init; }

    public SystemManagerAvailabilityStatus? Availability { get; init; }

    public FlightQualificationStatus? QualificationStatus { get; init; }

    public int PageNumber { get; init; } = 1;

    public int PageSize { get; init; } = 20;
}

public sealed record SystemManagerStateChangeRequest(
    string Reason,
    long ExpectedVersion);

public sealed record UpdateSystemManagerAvailabilityRequest(
    SystemManagerAvailabilityStatus Availability,
    string Reason,
    long ExpectedVersion);

public sealed record UpdateSystemManagerQualificationRequest(
    FlightQualificationStatus Status,
    DateTimeOffset? ExpiresAt,
    string Reason,
    long ExpectedVersion);

public sealed record AssignPrimaryFarmManagerRequest(
    Guid SystemManagerProfileId,
    string Reason,
    long? ExpectedCurrentAssignmentVersion);

public sealed record EndPrimaryFarmManagerAssignmentRequest(
    string Reason,
    long ExpectedVersion);
