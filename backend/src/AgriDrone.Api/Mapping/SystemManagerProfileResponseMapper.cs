using AgriDrone.Api.Contracts.SystemManagers;
using AgriDrone.Modules.Identity.Application.Features.SystemManagers;
using AgriDrone.Modules.Identity.Domain.SystemManagers;
using AgriDrone.SharedKernel.Application.Pagination;

namespace AgriDrone.Api.Mapping;

internal static class SystemManagerProfileResponseMapper
{
    public static PagedResult<SystemManagerProfileApiResponse> ToResponse(
        PagedResult<SystemManagerProfileResponse> page) =>
        new(
            page.Items.Select(ToResponse).ToArray(),
            page.PageNumber,
            page.PageSize,
            page.TotalCount);

    public static SystemManagerProfileApiResponse ToResponse(
        SystemManagerProfileResponse response) =>
        new(
            response.Id,
            response.UserId,
            response.Email,
            response.FullName,
            MapStatus(response.Status),
            MapAvailability(response.Availability),
            MapQualificationStatus(response.QualificationStatus),
            response.QualificationExpiresAt,
            response.Version);

    private static string MapStatus(SystemManagerProfileStatus status) =>
        status switch
        {
            SystemManagerProfileStatus.Active => "ACTIVE",
            SystemManagerProfileStatus.Suspended => "SUSPENDED",
            _ => throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "Unsupported System Manager profile status.")
        };

    private static string MapAvailability(
        SystemManagerAvailabilityStatus availability) =>
        availability switch
        {
            SystemManagerAvailabilityStatus.Available => "AVAILABLE",
            SystemManagerAvailabilityStatus.Unavailable => "UNAVAILABLE",
            _ => throw new ArgumentOutOfRangeException(
                nameof(availability),
                availability,
                "Unsupported System Manager availability status.")
        };

    private static string MapQualificationStatus(
        FlightQualificationStatus status) =>
        status switch
        {
            FlightQualificationStatus.Pending => "PENDING",
            FlightQualificationStatus.Qualified => "QUALIFIED",
            FlightQualificationStatus.Suspended => "SUSPENDED",
            FlightQualificationStatus.Revoked => "REVOKED",
            _ => throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "Unsupported System Manager qualification status.")
        };
}
