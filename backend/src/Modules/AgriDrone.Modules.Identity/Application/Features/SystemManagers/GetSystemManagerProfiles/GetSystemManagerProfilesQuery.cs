using AgriDrone.Modules.Identity.Domain.SystemManagers;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Pagination;
using MediatR;

namespace AgriDrone.Modules.Identity.Application.Features.SystemManagers.GetSystemManagerProfiles;

public sealed record GetSystemManagerProfilesQuery(
    SystemManagerProfileStatus? Status,
    SystemManagerAvailabilityStatus? Availability,
    FlightQualificationStatus? QualificationStatus,
    int PageNumber,
    int PageSize)
    : IRequest<Result<PagedResult<SystemManagerProfileResponse>>>;
