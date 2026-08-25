using AgriDrone.Modules.Identity.Application.Abstractions;
using AgriDrone.Modules.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Identity.Infrastructure.Initialization;

internal sealed class SystemAdminBootstrapLock(IdentityDbContext dbContext)
    : ISystemAdminBootstrapLock
{
    private const long LockKey = 8_241_906_202_608_24;

    public async Task AcquireAsync(
        CancellationToken cancellationToken = default)
    {
        if (dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "The System Admin bootstrap lock must be acquired inside a database transaction.");
        }

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({LockKey});",
            cancellationToken);
    }
}
