using AgriDrone.Modules.Identity.Domain.Roles;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Database;

internal static class SystemRoleSeeder
{
    public static void Seed(DbContext dbContext)
    {
        var roles = dbContext.Set<Role>();

        foreach (var role in SystemRoles.All)
        {
            var existingRole = roles.SingleOrDefault(
                candidate => candidate.Code == role.Code);

            if (existingRole is null)
            {
                roles.Add(Role.CreateSystemRole(
                    role.Code,
                    role.Name,
                    role.Description,
                    DateTimeOffset.UtcNow));
                continue;
            }

            existingRole.UpdateDefinition(role.Name, role.Description);
        }

        dbContext.SaveChanges();
    }

    public static async Task SeedAsync(
        DbContext dbContext,
        CancellationToken cancellationToken)
    {
        var roles = dbContext.Set<Role>();

        foreach (var role in SystemRoles.All)
        {
            var existingRole = await roles.SingleOrDefaultAsync(
                candidate => candidate.Code == role.Code,
                cancellationToken);

            if (existingRole is null)
            {
                roles.Add(Role.CreateSystemRole(
                    role.Code,
                    role.Name,
                    role.Description,
                    DateTimeOffset.UtcNow));
                continue;
            }

            existingRole.UpdateDefinition(role.Name, role.Description);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
