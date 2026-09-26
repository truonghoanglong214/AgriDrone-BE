using AgriDrone.Modules.Identity.Domain.SystemManagers;
using AgriDrone.Modules.Identity.Domain.Users;

namespace AgriDrone.Modules.Identity.Application.Features.SystemManagers;

public sealed record SystemManagerProfileResponse(
    Guid Id,
    Guid UserId,
    string Email,
    string FullName,
    SystemManagerProfileStatus Status,
    SystemManagerAvailabilityStatus Availability,
    FlightQualificationStatus QualificationStatus,
    DateTimeOffset? QualificationExpiresAt,
    long Version);

internal static class SystemManagerProfileResponseMapper
{
    public static SystemManagerProfileResponse ToResponse(
        SystemManagerProfile profile,
        User? user = null) =>
        new(
            profile.Id,
            profile.UserId,
            (user ?? profile.User).Email,
            (user ?? profile.User).FullName,
            profile.Status,
            profile.Availability,
            profile.QualificationStatus,
            profile.QualificationExpiresAt,
            profile.Version);
}
