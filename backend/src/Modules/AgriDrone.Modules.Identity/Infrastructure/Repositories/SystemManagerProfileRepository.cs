using AgriDrone.Modules.Identity.Domain.SystemManagers;
using AgriDrone.Modules.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Identity.Infrastructure.Repositories;

internal sealed class SystemManagerProfileRepository(IdentityDbContext dbContext)
    : ISystemManagerProfileRepository
{
    public Task<SystemManagerProfile?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        dbContext.SystemManagerProfiles
            .Include(profile => profile.User)
            .SingleOrDefaultAsync(
                profile => profile.Id == id,
                cancellationToken);

    public Task<SystemManagerProfile?> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        dbContext.SystemManagerProfiles
            .Include(profile => profile.User)
            .SingleOrDefaultAsync(
                profile => profile.UserId == userId,
                cancellationToken);

    public void Add(SystemManagerProfile profile) =>
        dbContext.SystemManagerProfiles.Add(profile);
}
