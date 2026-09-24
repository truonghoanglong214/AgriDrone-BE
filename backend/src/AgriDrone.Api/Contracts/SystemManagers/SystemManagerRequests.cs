using AgriDrone.Modules.Identity.Domain.SystemManagers;

namespace AgriDrone.Api.Contracts.SystemManagers;

public sealed record CreateSystemManagerProfileRequest(Guid UserId);

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
