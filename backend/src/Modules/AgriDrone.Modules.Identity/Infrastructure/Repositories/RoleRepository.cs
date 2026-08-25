using AgriDrone.Modules.Identity.Domain.Roles;
using AgriDrone.Modules.Identity.Domain.Users;
using AgriDrone.Modules.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Identity.Infrastructure.Repositories;

internal sealed class RoleRepository(IdentityDbContext dbContext)
    : IRoleRepository
{
    public Task<Role?> GetByCodeAsync(
        string code,
        CancellationToken cancellationToken = default) =>
        dbContext.Roles
            .AsNoTracking()
            .SingleOrDefaultAsync(
                role => role.Code == code,
                cancellationToken);

    public Task<bool> HasAssignedActiveUserAsync(
        string code,
        CancellationToken cancellationToken = default) =>
        dbContext.UserRoles
            .AnyAsync(
                userRole =>
                    userRole.Role.Code == code &&
                    userRole.User.Status == UserStatus.Active &&
                    userRole.User.DeletedAt == null,
                cancellationToken);
}
