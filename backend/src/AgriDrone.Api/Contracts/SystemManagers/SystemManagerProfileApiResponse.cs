namespace AgriDrone.Api.Contracts.SystemManagers;

public sealed record SystemManagerProfileApiResponse(
    Guid Id,
    Guid UserId,
    string Email,
    string FullName,
    string Status,
    string Availability,
    string QualificationStatus,
    DateTimeOffset? QualificationExpiresAt,
    long Version);
