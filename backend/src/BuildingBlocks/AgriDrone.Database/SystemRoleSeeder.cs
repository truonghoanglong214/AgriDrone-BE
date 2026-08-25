using AgriDrone.Modules.Identity.Domain.Roles;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Database;

internal static class SystemRoleSeeder
{
    public static void Seed(DbContext dbContext)
    {
        foreach (var role in SystemRoles.All)
        {
            dbContext.Database.ExecuteSqlInterpolated(CreateUpsertCommand(role));
        }
    }

    public static async Task SeedAsync(
        DbContext dbContext,
        CancellationToken cancellationToken)
    {
        foreach (var role in SystemRoles.All)
        {
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                CreateUpsertCommand(role),
                cancellationToken);
        }
    }

    private static FormattableString CreateUpsertCommand(
        SystemRoleDefinition role) => $"""
        INSERT INTO identity.roles
            (id, code, name, description, created_at)
        VALUES
            (gen_random_uuid(), {role.Code}, {role.Name}, {role.Description}, NOW())
        ON CONFLICT (code) DO UPDATE
        SET
            name = EXCLUDED.name,
            description = EXCLUDED.description;
        """;
}
