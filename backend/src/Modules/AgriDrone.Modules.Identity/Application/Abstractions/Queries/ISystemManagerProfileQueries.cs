using AgriDrone.Modules.Identity.Application.Features.SystemManagers;
using AgriDrone.Modules.Identity.Domain.SystemManagers;
using AgriDrone.SharedKernel.Application.Pagination;

namespace AgriDrone.Modules.Identity.Application.Abstractions.Queries;

internal interface ISystemManagerProfileQueries
{
    Task<PagedResult<SystemManagerProfileResponse>> GetPageAsync(
        SystemManagerProfileStatus? status,
        SystemManagerAvailabilityStatus? availability,
        FlightQualificationStatus? qualificationStatus,
        PagedRequest pageRequest,
        CancellationToken cancellationToken = default);

    Task<SystemManagerProfileResponse?> GetByIdAsync(
        Guid profileId,
        CancellationToken cancellationToken = default);
}
