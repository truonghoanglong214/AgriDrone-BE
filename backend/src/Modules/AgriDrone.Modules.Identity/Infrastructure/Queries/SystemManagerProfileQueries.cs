using AgriDrone.Modules.Identity.Application.Abstractions.Queries;
using AgriDrone.Modules.Identity.Application.Features.SystemManagers;
using AgriDrone.Modules.Identity.Domain.SystemManagers;
using AgriDrone.Modules.Identity.Infrastructure.Persistence;
using AgriDrone.SharedInfrastructure.Persistence.Pagination;
using AgriDrone.SharedKernel.Application.Pagination;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Identity.Infrastructure.Queries;

internal sealed class SystemManagerProfileQueries(IdentityDbContext dbContext)
    : ISystemManagerProfileQueries
{
    public Task<PagedResult<SystemManagerProfileResponse>> GetPageAsync(
        SystemManagerProfileStatus? status,
        SystemManagerAvailabilityStatus? availability,
        PagedRequest pageRequest,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.SystemManagerProfiles.AsNoTracking();

        if (status.HasValue)
        {
            query = query.Where(profile => profile.Status == status.Value);
        }

        if (availability.HasValue)
        {
            query = query.Where(
                profile => profile.Availability == availability.Value);
        }

        return Project(query
                .OrderByDescending(profile => profile.CreatedAt)
                .ThenByDescending(profile => profile.Id))
            .ToPagedResultAsync(pageRequest, cancellationToken);
    }

    public Task<SystemManagerProfileResponse?> GetByIdAsync(
        Guid profileId,
        CancellationToken cancellationToken = default) =>
        Project(dbContext.SystemManagerProfiles
                .AsNoTracking()
                .Where(profile => profile.Id == profileId))
            .SingleOrDefaultAsync(cancellationToken);

    private static IQueryable<SystemManagerProfileResponse> Project(
        IQueryable<SystemManagerProfile> query) =>
        query.Select(profile => new SystemManagerProfileResponse(
            profile.Id,
            profile.UserId,
            profile.User.Email,
            profile.User.FullName,
            profile.Status,
            profile.Availability,
            profile.Version));
}
